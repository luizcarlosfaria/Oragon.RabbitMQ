using System.Diagnostics.CodeAnalysis;
using Dawn;
using Microsoft.Extensions.Hosting;
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
public abstract class ConsumerBase : BackgroundService
{
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
    protected IBasicConsumer Consumer { get; private set; }

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
    /// Executes the consumer asynchronously.
    /// </summary>
    /// <param name="stoppingToken">The stopping token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.Connection = this.parameters.ConnectionFactoryFunc(this.serviceProvider);

        if (this.parameters.Configurer != null)
        {
            using var tmpModel = await this.Connection.CreateChannelAsync(stoppingToken).ConfigureAwait(true);
            this.parameters.Configurer(this.serviceProvider, tmpModel);
        }

        await this.WaitQueueCreationAsync().ConfigureAwait(true);

        this.Channel = await this.Connection.CreateChannelAsync(stoppingToken).ConfigureAwait(true);

        await this.Channel.BasicQosAsync(0, this.parameters.PrefetchCount, false, stoppingToken).ConfigureAwait(true);

        this.Consumer = this.BuildConsumer();

        var startTime = DateTimeOffset.UtcNow;

        this.Logger.LogInformation($"Consuming Queue {this.parameters.QueueName} since: {startTime}");

        this.consumerTag = await this.Channel.BasicConsumeAsync(
            queue: this.parameters.QueueName,
            autoAck: false,
            consumer: this.Consumer,
            consumerTag: this.consumerTag,
            arguments: null,
            exclusive: false,
            noLocal: true,
            cancellationToken: stoppingToken)
            .ConfigureAwait(true);

        var timeToDisplay = (int)this.parameters.DisplayLoopInConsoleEvery.TotalSeconds;

        long loopCount = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            loopCount++;
            var logMessage = $"Consuming Queue {this.parameters.QueueName} since: {startTime} uptime: {DateTimeOffset.Now - startTime}";

            if (loopCount % timeToDisplay == 0)
                this.Logger.LogInformation(logMessage);
            else
                this.Logger.LogTrace(logMessage);

            await Task.Delay(1000, stoppingToken).ConfigureAwait(true);
        }
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
                using IChannel testModel = await this.Connection.CreateChannelAsync().ConfigureAwait(true);
                _ = await testModel.QueueDeclarePassiveAsync(this.parameters.QueueName).ConfigureAwait(true);
                return Task.CompletedTask;
            }).ConfigureAwait(true);
    }

    /// <summary>
    /// Builds the consumer.
    /// </summary>
    /// <returns>The built <see cref="IBasicConsumer"/>.</returns>
    protected abstract IBasicConsumer BuildConsumer();


    /// <summary>
    /// Disposes the consumer.
    /// </summary>
    public override void Dispose()
    {
        GC.SuppressFinalize(this);

        this.DisposeAsync()
            .GetAwaiter()
            .GetResult();

        base.Dispose();
    }

    
    private async Task DisposeAsync()
    {
        if (this.Channel != null && !string.IsNullOrWhiteSpace(this.consumerTag))
        {
            await this.Channel.BasicCancelAsync(this.consumerTag, true).ConfigureAwait(false);
        }
        if (this.Channel != null)
        {
            this.Channel.Dispose();
            this.Channel = null;
        }
    }
}
