using Dawn;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace DotNetAspire.Architecture.Messaging.Consumer;

public abstract class ConsumerBase : BackgroundService
{

    protected readonly ILogger logger;
    private readonly IServiceProvider serviceProvider;
    protected IConnection connection;
    protected IBasicConsumer consumer;
    private string consumerTag;
    private ConsumerBaseParameters parameters;

    protected IModel Model { get; private set; }


    #region Constructors 

    protected ConsumerBase(ILogger logger, ConsumerBaseParameters parameters, IServiceProvider serviceProvider)
    {
        this.logger = Guard.Argument(logger).NotNull().Value;
        this.parameters = Guard.Argument(parameters).NotNull().Value;
        this.parameters.Validate();
        this.serviceProvider = serviceProvider;
    }


    #endregion

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.connection = this.parameters.ConnectionFactoryFunc(this.serviceProvider);

        if (this.parameters.Configurer != null)
        {
            using var tmpModel = this.connection.CreateModel();
            this.parameters.Configurer(this.serviceProvider, tmpModel);
        }

        await this.WaitQueueCreationAsync();

        this.Model = this.connection.CreateModel();

        this.Model.BasicQos(0, this.parameters.PrefetchCount, false);

        this.consumer = this.BuildConsumer();

        DateTimeOffset startTime = DateTimeOffset.UtcNow;

        this.logger.LogInformation($"Consuming Queue {this.parameters.QueueName} since: {startTime}");

        this.consumerTag = this.Model.BasicConsume(
                         queue: this.parameters.QueueName,
                         autoAck: false,
                         consumer: this.consumer);

        int timeToDisplay = (int)this.parameters.DisplayLoopInConsoleEvery.TotalSeconds;


        long loopCount = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            loopCount++;
            string logMessage = $"Consuming Queue {this.parameters.QueueName} since: {startTime} uptime: {DateTimeOffset.Now - startTime}";

            if (loopCount % timeToDisplay == 0)
                this.logger.LogInformation(logMessage);
            else
                this.logger.LogTrace(logMessage);

            await Task.Delay(1000, stoppingToken);
        }
    }

    protected virtual async Task WaitQueueCreationAsync()
    {
        await Policy
        .Handle<OperationInterruptedException>()
            .WaitAndRetryAsync(this.parameters.TestQueueRetryCount, retryAttempt =>
            {
                var timeToWait = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                this.logger.LogWarning("Queue {queueName} not found... We will try in {tempo}.", this.parameters.QueueName, timeToWait);
                return timeToWait;
            })
            .ExecuteAsync(() =>
            {
                using IModel testModel = this.connection.CreateModel();
                testModel.QueueDeclarePassive(this.parameters.QueueName);
                return Task.CompletedTask;
            });
    }

    protected abstract IBasicConsumer BuildConsumer();

    public override void Dispose()
    {
        if (this.Model != null && !string.IsNullOrWhiteSpace(this.consumerTag))
            this.Model.BasicCancelNoWait(this.consumerTag);
        if (this.Model != null)
        {
            this.Model.Dispose();
            this.Model = null;
        }

        base.Dispose();
    }

}
