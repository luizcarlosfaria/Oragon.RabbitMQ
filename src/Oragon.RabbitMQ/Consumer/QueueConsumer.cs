// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Dawn;
using System.Diagnostics.CodeAnalysis;
using Oragon.RabbitMQ.Consumer.Dispatch;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Oragon.RabbitMQ.Serialization;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.Consumer.Actions;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using RabbitMQ.Client.Exceptions;
using System.Text;

namespace Oragon.RabbitMQ.Consumer;


/// <summary>
/// Represents a consumer that consumes messages from a queue.
/// </summary>
public class QueueConsumer : IHostedAmqpConsumer
{
    private readonly ILogger logger;
    private readonly QueueConsumerBuilder parameters;
    private IDispatcher dispatcher;
    private AsyncEventingBasicConsumer asyncBasicConsumer;
    private IConnection connection;
    private IChannel channel;
    private string consumerTag;
    private CancellationTokenSource cancellationTokenSource;
    private IAMQPSerializer serializer;

    /// <summary>
    /// Gets a value indicating whether the consumer is consuming messages.
    /// </summary>
    public bool WasStarted { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the consumer is consuming messages.
    /// </summary>
    public bool IsConsuming { get; private set; }


    /// <summary>
    /// Gets a value indicating whether the consumer is initialized.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueConsumer"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="queueConsumerBuilder"></param>
    public QueueConsumer(ILogger logger, QueueConsumerBuilder queueConsumerBuilder)
    {
        this.logger = logger;
        this.parameters = queueConsumerBuilder;

    }

    /// <summary>
    /// Initializes the consumer.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (this.IsInitialized) throw new InvalidOperationException("The consumer is already initialized");

        this.dispatcher = new Dispatcher(this.parameters.Handler);

        await this.ValidateAsync(cancellationToken).ConfigureAwait(true);

        this.connection = await this.parameters.ConnectionFactory(this.parameters.ApplicationServiceProvider, cancellationToken).ConfigureAwait(true);

        await this.WaitQueueCreationAsync().ConfigureAwait(true);

        this.serializer = this.parameters.SerializerFactory(this.parameters.ApplicationServiceProvider);

        this.channel = await this.parameters.ChannelFactory(this.connection, cancellationToken).ConfigureAwait(true);

        await this.channel.BasicQosAsync(0, this.parameters.PrefetchCount, false, cancellationToken).ConfigureAwait(true);

        this.asyncBasicConsumer = new AsyncEventingBasicConsumer(this.channel);
        this.asyncBasicConsumer.ReceivedAsync += this.ReceiveAsync;
        this.asyncBasicConsumer.RegisteredAsync += this.RegisteredAsync;
        this.asyncBasicConsumer.UnregisteredAsync += this.UnregisteredAsync;
        this.asyncBasicConsumer.ShutdownAsync += this.ShutdownAsync;

        this.IsInitialized = true;
    }

    /// <summary>
    /// Waits for the queue creation asynchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected virtual async Task WaitQueueCreationAsync()
    {
        _ = await Policy
            .Handle<OperationInterruptedException>()
            .WaitAndRetryAsync(5, retryAttempt =>
            {
                var timeToWait = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                this.logger.LogWarning("Queue {QueueName} not found... We will try in {Tempo}.", this.parameters.QueueName, timeToWait);
                return timeToWait;
            })
            .ExecuteAsync(async () =>
            {
                using var testModel = await this.connection.CreateChannelAsync().ConfigureAwait(true);
                _ = await testModel.QueueDeclarePassiveAsync(this.parameters.QueueName).ConfigureAwait(true);
                return Task.CompletedTask;
            }).ConfigureAwait(true);
    }

    /// <summary>
    /// Validates the consumer configuration.
    /// </summary>
    public async Task ValidateAsync(CancellationToken cancellationToken)
    {
        IConnection connection = await this.parameters.ConnectionFactory(this.parameters.ApplicationServiceProvider, cancellationToken).ConfigureAwait(true);
        using IChannel channel = await this.parameters.ChannelFactory(connection, cancellationToken).ConfigureAwait(true);

        QueueDeclareOk queue = await channel.QueueDeclarePassiveAsync(this.parameters.QueueName, cancellationToken).ConfigureAwait(true);

        await channel.CloseAsync(cancellationToken).ConfigureAwait(true);


        using IServiceScope scope = this.parameters.ApplicationServiceProvider.CreateScope();

        foreach (FromServicesArgumentBinder binder in this.dispatcher.GetArgumentBindersOfType<FromServicesArgumentBinder>())
        {
            try
            {
                var service = binder.GetValue(scope.ServiceProvider);
            }
            catch (Exception)
            {
                var exceptionMessage = $"Error on get service {binder.ParameterType} ";
                if(!string.IsNullOrWhiteSpace(binder.ServiceKey))
                {
                    exceptionMessage += $" with key '{binder.ServiceKey}'";
                }
                throw new InvalidOperationException(exceptionMessage);
            }

        }

    }

    /// <summary>
    /// Starts the consumer asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!this.IsInitialized) throw new InvalidOperationException("The consumer is not initialized");
        if (this.IsConsuming) throw new InvalidOperationException("The consumer is already started");

        this.cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        this.consumerTag = await this.channel.BasicConsumeAsync(
            queue: this.parameters.QueueName,
            autoAck: false,
            consumer: this.asyncBasicConsumer,
            consumerTag: this.parameters.ConsumerTag,
            arguments: null,
            exclusive: false,
            noLocal: true,
            cancellationToken: this.cancellationTokenSource.Token)
            .ConfigureAwait(true);

        this.WasStarted = true;

        this.IsConsuming = true;
    }

    [SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "<Pending>")]
    private async Task ReceiveAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        using (IServiceScope scope = this.parameters.ApplicationServiceProvider.CreateScope())
        {
            var canProceed = this.TryDeserialize(eventArgs, this.dispatcher.GetMessageType(), out var incomingMessage);

            IAmqpContext context = new AmqpContext(eventArgs, scope.ServiceProvider, this.serializer, this.connection, this.channel, this.parameters.QueueName, incomingMessage, this.cancellationTokenSource.Token);

            IAMQPResult result =
                canProceed
                ? await this.dispatcher.DispatchAsync(context).ConfigureAwait(false)
                : new RejectResult(false);

            await result.ExecuteAsync(context).ConfigureAwait(true);
        }
    }

    private Task UnregisteredAsync(object sender, ConsumerEventArgs eventArgs)
    {
        return Task.CompletedTask;
    }

    private Task RegisteredAsync(object sender, ConsumerEventArgs eventArgs)
    {
        //await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        return Task.CompletedTask;
    }
    private Task ShutdownAsync(object sender, ShutdownEventArgs eventArgs)
    {
        return Task.CompletedTask;
    }


    private static readonly Action<ILogger, Exception, Exception> s_logErrorOnDesserialize = LoggerMessage.Define<Exception>(LogLevel.Error, new EventId(1, "MessageObject rejected during deserialization"), "MessageObject rejected during deserialization {ExceptionDetails}");


    /// <summary>
    /// Tries to deserialize the received item.
    /// </summary>
    /// <param name="eventArgs">The received item</param>
    /// <param name="type"></param>
    /// <param name="incomingMessage">The deserialized incomingMessage.</param>
    /// <returns><c>true</c> if deserialization is successful; otherwise, <c>false</c>.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "Tratamento de exceçào global, isolando uma micro-operação")]
    private bool TryDeserialize(BasicDeliverEventArgs eventArgs, Type type, out object incomingMessage)
    {
        _ = Guard.Argument(eventArgs).NotNull();
        _ = Guard.Argument(type).NotNull();
        var returnValue = true;

        incomingMessage = default;
        try
        {
            incomingMessage = this.serializer.Deserialize(eventArgs: eventArgs, type: type);

            //_ = receiveActivity?.SetTag("incomingMessage", incomingMessage);
        }
        catch (Exception exception)
        {
            returnValue = false;

            s_logErrorOnDesserialize(this.logger, exception, exception);
        }

        return returnValue;
    }


    /// <summary>
    /// Stops the consumer asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (this.WasStarted)
        {
            await this.channel.BasicCancelAsync(this.consumerTag, false, cancellationToken).ConfigureAwait(true);
        }
        this.IsConsuming = false;
    }


    /// <summary>
    /// Disposes the consumer asynchronously.
    /// </summary>
    /// <returns>A value task that represents the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        //if (this.Channel != null && this.WasStarted && this.IsConsumming && !string.IsNullOrWhiteSpace(this.consumerTag))
        //{
        //    await this.Channel.BasicCancelAsync(this.consumerTag, true).ConfigureAwait(false);
        //}
        //if (this.Channel != null)
        //{
        //    this.Channel.Dispose();
        //    this.Channel = null;
        //}

        GC.SuppressFinalize(this);

        return new ValueTask(Task.CompletedTask);
    }


}

