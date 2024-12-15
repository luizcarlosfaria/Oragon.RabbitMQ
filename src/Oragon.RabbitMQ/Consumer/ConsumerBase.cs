// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Dawn;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// Base class for consumers.
/// </summary>
[SuppressMessage("Performance", "CA1848", Justification = "Use the LoggerMessage delegates")]
[SuppressMessage("Performance", "CA2254", Justification = "Template should be a static expression")]
public abstract class ConsumerBase : IHostedAmqpConsumer
{
    /// <summary>
    /// Gets a value indicating whether the consumer is consuming.
    /// </summary>
    protected bool IsConsumming { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the consumer was started.
    /// </summary>
    protected bool WasStarted { get; private set; }

    /// <summary>
    /// The logger.
    /// </summary>
    protected ILogger Logger { get; private set; }

    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// The connection to RabbitMQ.
    /// </summary>
    protected IConnection Connection { get; private set; }

    /// <summary>
    /// The consumer instance.
    /// </summary>
    protected IAsyncBasicConsumer Consumer { get; private set; }

    private string consumerTag;

    private readonly ConsumerBaseParameters parameters;

    /// <summary>
    /// The channel for communication with RabbitMQ.
    /// </summary>
    protected IChannel Channel { get; private set; }

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumerBase"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="parameters">The consumer base parameters.</param>
    /// <param name="serviceProvider">The service provider.</param>
    protected ConsumerBase(ILogger logger, ConsumerBaseParameters parameters, IServiceProvider serviceProvider)
    {
        this.Logger = Guard.Argument(logger).NotNull().Value;
        this.parameters = Guard.Argument(parameters).NotNull().Value;
        this.parameters.Validate();
        this.serviceProvider = serviceProvider;
        this.consumerTag = $"{this.parameters.QueueName}-{Guid.NewGuid().ToString("D").Split("-").Last()}";
    }

    #endregion


    /// <summary>
    /// Starts the consumer.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        this.Connection = this.parameters.ConnectionFactoryFunc(this.serviceProvider);

        this.Validate();

        if (this.parameters.Configurer != null)
        {
            using var tmpModel = await this.Connection.CreateChannelAsync(cancellationToken).ConfigureAwait(true);
            await this.parameters.Configurer(this.serviceProvider, tmpModel).ConfigureAwait(true);
        }

        await this.WaitQueueCreationAsync().ConfigureAwait(true);

        this.Channel = await this.Connection.CreateChannelAsync(cancellationToken).ConfigureAwait(true);

        await this.Channel.BasicQosAsync(0, this.parameters.PrefetchCount, false, cancellationToken).ConfigureAwait(true);

        this.Consumer = this.BuildConsumer();

        var startTime = DateTimeOffset.UtcNow;

        this.Logger.LogInformation($"Consuming Queue {this.parameters.QueueName} since: {startTime}");

        this.IsConsumming = true;

        this.WasStarted = true;

        this.consumerTag = await this.Channel.BasicConsumeAsync(
            queue: this.parameters.QueueName,
            autoAck: false,
            consumer: this.Consumer,
            consumerTag: this.consumerTag,
            arguments: null,
            exclusive: false,
            noLocal: true,
            cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        //var timeToDisplay = (int)this.parameters.DisplayLoopInConsoleEvery.TotalSeconds;

        //long loopCount = 0;
        //while (!cancellationToken.IsCancellationRequested)
        //{
        //    loopCount++;
        //    var logMessage = $"Consuming Queue {this.parameters.QueueName} since: {startTime} uptime: {DateTimeOffset.Now - startTime}";

        //    if (loopCount % timeToDisplay == 0)
        //        this.Logger.LogInformation(logMessage);
        //    else
        //        this.Logger.LogTrace(logMessage);

        //    await Task.Delay(1000, cancellationToken).ConfigureAwait(true);
        //}
    }

    /// <summary>
    /// Stops the consumer.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (this.WasStarted)
        {
            await this.Channel.BasicCancelAsync(this.consumerTag, false, CancellationToken.None).ConfigureAwait(true);
        }
        this.IsConsumming = false;
    }


    /// <summary>
    /// Validate state and parameters before run
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    protected virtual void Validate()
    {
        
    }

    /// <summary>
    /// Waits for the queue creation asynchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected virtual async Task WaitQueueCreationAsync()
    {
        _ = await Policy
            .Handle<OperationInterruptedException>()
            .WaitAndRetryAsync(this.parameters.TestQueueRetryCount, retryAttempt =>
            {
                var timeToWait = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                this.Logger.LogWarning("Queue {QueueName} not found... We will try in {Tempo}.", this.parameters.QueueName, timeToWait);
                return timeToWait;
            })
            .ExecuteAsync(async () =>
            {
                using var testModel = await this.Connection.CreateChannelAsync().ConfigureAwait(true);
                _ = await testModel.QueueDeclarePassiveAsync(this.parameters.QueueName).ConfigureAwait(true);
                return Task.CompletedTask;
            }).ConfigureAwait(true);
    }

    /// <summary>
    /// Builds the consumer.
    /// </summary>
    /// <returns>The built <see cref="IAsyncBasicConsumer"/>.</returns>
    protected abstract IAsyncBasicConsumer BuildConsumer();


    /// <summary>
    /// Disposes the consumer.
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        if (this.Channel != null && this.WasStarted && this.IsConsumming && !string.IsNullOrWhiteSpace(this.consumerTag))
        {
            await this.Channel.BasicCancelAsync(this.consumerTag, true).ConfigureAwait(false);
        }
        if (this.Channel != null)
        {
            this.Channel.Dispose();
            this.Channel = null;
        }

        GC.SuppressFinalize(this);
    }
}
