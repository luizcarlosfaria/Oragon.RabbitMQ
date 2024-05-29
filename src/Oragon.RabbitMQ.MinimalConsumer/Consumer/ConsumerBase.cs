using Dawn;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Oragon.RabbitMQ.Consumer;

public abstract class ConsumerBase : BackgroundService
{

    protected readonly ILogger logger;
    private readonly IServiceProvider serviceProvider;
    protected IConnection connection;
    protected IBasicConsumer consumer;
    private string consumerTag;
    private ConsumerBaseParameters parameters;

    protected IChannel Model { get; private set; }


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
        connection = parameters.ConnectionFactoryFunc(serviceProvider);

        if (parameters.Configurer != null)
        {
            using var tmpModel = connection.CreateModel();
            parameters.Configurer(serviceProvider, tmpModel);
        }

        await WaitQueueCreationAsync();

        Model = connection.CreateModel();

        Model.BasicQos(0, parameters.PrefetchCount, false);

        consumer = BuildConsumer();

        var startTime = DateTimeOffset.UtcNow;

        logger.LogInformation($"Consuming Queue {parameters.QueueName} since: {startTime}");

        consumerTag = Model.BasicConsume(
                         queue: parameters.QueueName,
                         autoAck: false,
                         consumer: consumer);

        var timeToDisplay = (int)parameters.DisplayLoopInConsoleEvery.TotalSeconds;


        long loopCount = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            loopCount++;
            var logMessage = $"Consuming Queue {parameters.QueueName} since: {startTime} uptime: {DateTimeOffset.Now - startTime}";

            if (loopCount % timeToDisplay == 0)
                logger.LogInformation(logMessage);
            else
                logger.LogTrace(logMessage);

            await Task.Delay(1000, stoppingToken);
        }
    }

    protected virtual async Task WaitQueueCreationAsync()
    {
        await Policy
        .Handle<OperationInterruptedException>()
            .WaitAndRetryAsync(parameters.TestQueueRetryCount, retryAttempt =>
            {
                var timeToWait = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                logger.LogWarning("Queue {queueName} not found... We will try in {tempo}.", parameters.QueueName, timeToWait);
                return timeToWait;
            })
            .ExecuteAsync(() =>
            {
                using IChannel testModel = connection.CreateModel();
                testModel.QueueDeclarePassive(parameters.QueueName);
                return Task.CompletedTask;
            });
    }

    protected abstract IBasicConsumer BuildConsumer();

    public override void Dispose()
    {
        if (Model != null && !string.IsNullOrWhiteSpace(consumerTag))
            Model.BasicCancelNoWait(consumerTag);
        if (Model != null)
        {
            Model.Dispose();
            Model = null;
        }

        base.Dispose();
    }

}
