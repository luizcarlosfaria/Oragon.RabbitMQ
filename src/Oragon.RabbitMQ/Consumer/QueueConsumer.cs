// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

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
using Oragon.RabbitMQ.Consumer.ArgumentBinders;

namespace Oragon.RabbitMQ.Consumer;


/// <summary>
/// Represents a consumer that consumes messages from a queue.
/// </summary>
public class QueueConsumer : IHostedAmqpConsumer
{
    private readonly ILogger logger;
    private readonly ConsumerDescriptor consumerDescriptor;
    private Dispatcher dispatcher;
    private AsyncEventingBasicConsumer asyncBasicConsumer;
    private IConnection connection;
    private IChannel channel;
    private string consumerTag;
    private CancellationTokenSource cancellationTokenSource;
    private IAmqpSerializer serializer;

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
    /// <param name="consumerDescriptor"></param>
    public QueueConsumer(ILogger logger, ConsumerDescriptor consumerDescriptor)
    {
        this.logger = logger;
        this.consumerDescriptor = consumerDescriptor;

    }

    /// <summary>
    /// Initializes the consumer.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (this.IsInitialized) throw new InvalidOperationException("The consumer is already initialized");

        this.dispatcher = new Dispatcher(this.consumerDescriptor);

        await this.ValidateAsync(cancellationToken).ConfigureAwait(true);

        this.serializer = this.consumerDescriptor.SerializerFactory(this.consumerDescriptor.ApplicationServiceProvider);

        this.connection = await this.consumerDescriptor.ConnectionFactory(this.consumerDescriptor.ApplicationServiceProvider, cancellationToken).ConfigureAwait(true);

        this.channel = await this.consumerDescriptor.ChannelFactory(this.connection, cancellationToken).ConfigureAwait(true);

        if (this.consumerDescriptor.TopologyInitializer != null)
        {
            await this.consumerDescriptor.TopologyInitializer(this.channel, cancellationToken).ConfigureAwait(true);
        }
        
        await this.WaitQueueCreationAsync().ConfigureAwait(true);

        await this.channel.BasicQosAsync(0, this.consumerDescriptor.PrefetchCount, false, cancellationToken).ConfigureAwait(true);

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
    // Add this static field to define the LoggerMessage delegate
    private static readonly Action<ILogger, string, TimeSpan, Exception> s_logQueueNotFound = LoggerMessage.Define<string, TimeSpan>(
        LogLevel.Warning,
        new EventId(2, nameof(WaitQueueCreationAsync)),
        "Queue {QueueName} not found... We will try in {Tempo}."
    );

    /// <summary>
    /// Waits for the queue creation asynchronously.
    /// </summary>
    /// <returns></returns>
    protected virtual async Task WaitQueueCreationAsync()
    {
        await Policy
            .Handle<OperationInterruptedException>()
            .WaitAndRetryAsync(5, retryAttempt =>
            {
                var timeToWait = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                s_logQueueNotFound(this.logger, this.consumerDescriptor.QueueName, timeToWait, null);
                return timeToWait;
            })
            .ExecuteAsync(async () =>
            {
                using IChannel testModel = await this.connection.CreateChannelAsync().ConfigureAwait(true);

                _ = await testModel.QueueDeclarePassiveAsync(this.consumerDescriptor.QueueName).ConfigureAwait(true);

                await testModel.CloseAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
    }

    /// <summary>
    /// Validates the consumer configuration.
    /// </summary>
    public async Task ValidateAsync(CancellationToken cancellationToken)
    {
        IConnection connection1 = await this.consumerDescriptor.ConnectionFactory(this.consumerDescriptor.ApplicationServiceProvider, cancellationToken).ConfigureAwait(true);
        IConnection connection2 = await this.consumerDescriptor.ConnectionFactory(this.consumerDescriptor.ApplicationServiceProvider, cancellationToken).ConfigureAwait(true);
        var mustReuseConnection = connection1 == connection2;

        try
        {
            if (!mustReuseConnection)
            {
                await connection2.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
            }

            using IChannel testChannel = await this.consumerDescriptor.ChannelFactory(connection1, cancellationToken).ConfigureAwait(true);            

            await testChannel.CloseAsync(cancellationToken).ConfigureAwait(true);

            using IServiceScope scope = this.consumerDescriptor.ApplicationServiceProvider.CreateScope();

            foreach (FromServicesArgumentBinder binder in this.dispatcher.GetArgumentBindersOfType<FromServicesArgumentBinder>())
            {
                try
                {
                    _ = binder.GetValue(scope.ServiceProvider);
                }
                catch (Exception exception)
                {
                    var exceptionMessage = $"Error on get service {binder.ParameterType} ";
                    if (!string.IsNullOrWhiteSpace(binder.ServiceKey))
                    {
                        exceptionMessage += $" with key '{binder.ServiceKey}'";
                    }
                    throw new InvalidOperationException(exceptionMessage, exception);
                }

            }
        }
        finally
        {
            if (!mustReuseConnection)
            {
                await connection1.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
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
            queue: this.consumerDescriptor.QueueName,
            autoAck: false,
            consumer: this.asyncBasicConsumer,
            consumerTag: this.consumerDescriptor.ConsumerTag,
            arguments: null,
            exclusive: this.consumerDescriptor.Exclusive,
            noLocal: true,
            cancellationToken: this.cancellationTokenSource.Token)
            .ConfigureAwait(true);

        this.WasStarted = true;

        this.IsConsuming = true;
    }

    [SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "<Pending>")]
    private async Task ReceiveAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        IAmqpContext context = null;
        try
        {
            using (IServiceScope scope = this.consumerDescriptor.ApplicationServiceProvider.CreateScope())
            {
                (bool canProceed, Exception exception) = this.TryDeserialize(eventArgs, this.dispatcher.MessageType, out var incomingMessage);

                context = this.BuildAmqpContext(eventArgs, scope, incomingMessage);

                IAmqpResult result =
                    canProceed
                    ? await this.dispatcher.DispatchAsync(context).ConfigureAwait(true)
                    : this.consumerDescriptor.ResultForSerializationFailure(context, exception);

                await result.ExecuteAsync(context).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            s_logErrorOnExecuteResult(this.logger, eventArgs.DeliveryTag, ex);

            await this.TryNackMessageAsync(context, eventArgs.DeliveryTag).ConfigureAwait(true);
        }
    }

    private async Task TryNackMessageAsync(IAmqpContext context, ulong deliveryTag)
    {
        if (context?.Channel == null || context.Channel.IsClosed)
        {
            s_logChannelClosedCannotNack(this.logger, deliveryTag, null);
            return;
        }

        try
        {
            // requeue: false → message goes to dead-letter queue (if configured)
            await context.Channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false).ConfigureAwait(true);
        }
        catch (Exception nackEx)
        {
            s_logFailedToNack(this.logger, deliveryTag, nackEx);
        }
    }

    private AmqpContext BuildAmqpContext(BasicDeliverEventArgs eventArgs, IServiceScope scope, object incomingMessage)
    {
        return new AmqpContext(this.logger, this.cancellationTokenSource.Token)
        {
            Request = eventArgs,
            ServiceProvider = scope.ServiceProvider,
            Serializer = this.serializer,
            Connection = this.connection,
            Channel = this.channel,
            QueueName = this.consumerDescriptor.QueueName,
            MessageObject = incomingMessage,
        };
    }

    private Task RegisteredAsync(object sender, ConsumerEventArgs eventArgs)
    {
        s_logConsumerRegistered(this.logger, this.consumerDescriptor.QueueName, null);
        return Task.CompletedTask;
    }

    private Task UnregisteredAsync(object sender, ConsumerEventArgs eventArgs)
    {
        this.IsConsuming = false;
        s_logConsumerUnregistered(this.logger, this.consumerDescriptor.QueueName, null);
        return Task.CompletedTask;
    }

    private Task ShutdownAsync(object sender, ShutdownEventArgs eventArgs)
    {
        this.IsConsuming = false;
        s_logConsumerShutdown(this.logger, this.consumerDescriptor.QueueName, eventArgs?.ReplyText ?? "Unknown", null);
        return Task.CompletedTask;
    }


    private static readonly Action<ILogger, Exception, Exception> s_logErrorOnDeserialize = LoggerMessage.Define<Exception>(LogLevel.Error, new EventId(1, "MessageObject rejected during deserialization"), "MessageObject rejected during deserialization {ExceptionDetails}");

    private static readonly Action<ILogger, ulong, Exception> s_logErrorOnExecuteResult = LoggerMessage.Define<ulong>(LogLevel.Error, new EventId(3, "ErrorOnExecuteResult"), "Error executing result for message {DeliveryTag}");

    private static readonly Action<ILogger, ulong, Exception> s_logFailedToNack = LoggerMessage.Define<ulong>(LogLevel.Critical, new EventId(4, "FailedToNack"), "Failed to Nack message {DeliveryTag}. Message may be stuck in unacked state.");

    private static readonly Action<ILogger, ulong, Exception> s_logChannelClosedCannotNack = LoggerMessage.Define<ulong>(LogLevel.Critical, new EventId(5, "ChannelClosedCannotNack"), "Channel is closed, cannot Nack message {DeliveryTag}. Message may be stuck in unacked state.");

    private static readonly Action<ILogger, string, Exception> s_logConsumerRegistered = LoggerMessage.Define<string>(LogLevel.Information, new EventId(6, "ConsumerRegistered"), "Consumer registered on queue {QueueName}");

    private static readonly Action<ILogger, string, Exception> s_logConsumerUnregistered = LoggerMessage.Define<string>(LogLevel.Warning, new EventId(7, "ConsumerUnregistered"), "Consumer unregistered from queue {QueueName}");

    private static readonly Action<ILogger, string, string, Exception> s_logConsumerShutdown = LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(8, "ConsumerShutdown"), "Consumer shutdown on queue {QueueName}. Reason: {Reason}");


    /// <summary>
    /// Tries to deserialize the received item.
    /// </summary>
    /// <param name="eventArgs">The received item</param>
    /// <param name="type"></param>
    /// <param name="incomingMessage">The deserialized incomingMessage.</param>
    /// <returns><c>true</c> if deserialization is successful; otherwise, <c>false</c>.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "Tratamento de exceção global, isolando uma micro-operação")]
    private (bool, Exception) TryDeserialize(BasicDeliverEventArgs eventArgs, Type type, out object incomingMessage)
    {
        ArgumentNullException.ThrowIfNull(eventArgs, nameof(eventArgs));
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        incomingMessage = default;
        try
        {
            incomingMessage = this.serializer.Deserialize(eventArgs: eventArgs, type: type);
        }
        catch (Exception exception)
        {
            s_logErrorOnDeserialize(this.logger, exception, exception);

            return (false, exception);
        }

        return (true, null);
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
    public async ValueTask DisposeAsync()
    {
        if (this.WasStarted)
        {
            this.cancellationTokenSource?.Cancel();
            this.cancellationTokenSource?.Dispose();
        }

        if (this.channel != null && this.WasStarted && this.IsConsuming && !string.IsNullOrWhiteSpace(this.consumerTag))
        {
            await this.channel.BasicCancelAsync(this.consumerTag, true).ConfigureAwait(true);
        }

        this.channel?.Dispose();
        this.channel = null;

        GC.SuppressFinalize(this);
    }


}

