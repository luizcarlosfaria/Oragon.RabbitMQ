// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.DynamicQueues;
using Oragon.RabbitMQ.Serialization;
using Oragon.RabbitMQ.TestsExtensions;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Testcontainers.RabbitMq;

namespace Oragon.RabbitMQ.IntegratedTests;

public class AttentionPrimitivesIntegratedTests : IAsyncLifetime
{
    public class ExampleMessage
    {
        public string Name { get; set; }

        public int Age { get; set; }
    }

    private readonly RabbitMqContainer rabbitMqContainer = new RabbitMqBuilder(Constants.RabbitMQContainerImage).BuildRabbitMQ();

    private static TimeSpan WaitTimeout => Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(10);

    public Task InitializeAsync()
    {
        return this.rabbitMqContainer.StartAsync();
    }

    public Task DisposeAsync()
    {
        return this.rabbitMqContainer.DisposeAsync().AsTask();
    }

    [Fact(Timeout = 30000)]
    public async Task DynamicConsumer_WhenStopAfterInitialQueueLength_ShouldIgnoreMessagesPublishedAfterStart()
    {
        string queue = NewQueueName();
        using IConnection attentionConnection = await this.CreateConnectionAsync().ConfigureAwait(true);
        using IConnection executionConnection = await this.CreateConnectionAsync().ConfigureAwait(true);
        using IChannel channel = await executionConnection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));

        _ = await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        await PublishAsync(channel, queue, new ExampleMessage { Name = "first", Age = 1 }).ConfigureAwait(true);
        await PublishAsync(channel, queue, new ExampleMessage { Name = "second", Age = 2 }).ConfigureAwait(true);

        await using ServiceProvider sp = BuildServiceProvider(attentionConnection);
        IAmqpDynamicQueueConsumer consumer = sp.GetRequiredService<IAmqpDynamicQueueConsumer>();
        var receivedNames = new List<string>();
        int received = 0;

        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(
            new DynamicQueueConsumeRequest<ExampleMessage>
            {
                QueueName = queue,
                Connection = executionConnection,
                PrefetchCount = 1,
                MaxLocalConcurrency = 1,
                StopAfterInitialQueueLength = true,
                OnMessageAsync = async (message, context) =>
                {
                    Assert.Same(executionConnection, context.Connection);
                    receivedNames.Add(message.Name);

                    if (Interlocked.Increment(ref received) == 1)
                    {
                        using IChannel publishChannel = await context.Connection.CreateChannelAsync(cancellationToken: context.CancellationToken).ConfigureAwait(true);
                        await PublishAsync(publishChannel, queue, new ExampleMessage { Name = "late", Age = 3 }, context.CancellationToken).ConfigureAwait(true);
                    }

                    return AmqpResults.Ack();
                },
            },
            CancellationToken.None).ConfigureAwait(true);

        _ = await channel.QueueDeclarePassiveAsync(queue);

        Assert.Equal(DynamicQueueConsumeStatus.InitialQueueLengthReached, result.Status);
        Assert.Equal(2, result.InitialReadyCount);
        Assert.Equal(2, result.MessagesReceived);
        Assert.Equal(2, result.MessagesAcked);
        Assert.Equal(1, result.RemainingReadyCount);
        Assert.Equal(["first", "second"], receivedNames);
    }

    [Fact(Timeout = 30000)]
    public async Task DynamicConsumer_WhenMaxMessagesIsReached_ShouldLeaveRemainingMessagesReady()
    {
        string queue = NewQueueName();
        using IConnection connection = await this.CreateConnectionAsync().ConfigureAwait(true);
        using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));

        _ = await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        await PublishAsync(channel, queue, new ExampleMessage { Name = "one", Age = 1 }).ConfigureAwait(true);
        await PublishAsync(channel, queue, new ExampleMessage { Name = "two", Age = 2 }).ConfigureAwait(true);
        await PublishAsync(channel, queue, new ExampleMessage { Name = "three", Age = 3 }).ConfigureAwait(true);

        await using ServiceProvider sp = BuildServiceProvider(connection);
        IAmqpDynamicQueueConsumer consumer = sp.GetRequiredService<IAmqpDynamicQueueConsumer>();

        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(
            new DynamicQueueConsumeRequest<ExampleMessage>
            {
                QueueName = queue,
                Connection = connection,
                PrefetchCount = 1,
                MaxLocalConcurrency = 1,
                MaxMessages = 2,
                OnMessageAsync = (_, _) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
            },
            CancellationToken.None).ConfigureAwait(true);

        QueueDeclareOk queueState = await channel.QueueDeclarePassiveAsync(queue);

        Assert.Equal(DynamicQueueConsumeStatus.MaxMessagesReached, result.Status);
        Assert.Equal(3, result.InitialReadyCount);
        Assert.Equal(2, result.MessagesReceived);
        Assert.Equal(2, result.MessagesAcked);
        Assert.Equal(1, result.RemainingReadyCount);
        Assert.Equal(1u, queueState.MessageCount);
    }

    [Fact(Timeout = 30000)]
    public async Task DynamicConsumer_WhenQueueIsEmpty_ShouldStopByIdleTimeout()
    {
        string queue = NewQueueName();
        using IConnection connection = await this.CreateConnectionAsync().ConfigureAwait(true);
        using IChannel channel = await connection.CreateChannelAsync();

        _ = await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: null);

        await using ServiceProvider sp = BuildServiceProvider(connection);
        IAmqpDynamicQueueConsumer consumer = sp.GetRequiredService<IAmqpDynamicQueueConsumer>();

        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(
            new DynamicQueueConsumeRequest<ExampleMessage>
            {
                QueueName = queue,
                Connection = connection,
                IdleTimeout = TimeSpan.FromMilliseconds(150),
                OnMessageAsync = (_, _) => throw new InvalidOperationException("The empty queue should not deliver messages."),
            },
            CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(DynamicQueueConsumeStatus.IdleTimeoutReached, result.Status);
        Assert.Equal(0, result.InitialReadyCount);
        Assert.Equal(0, result.MessagesReceived);
    }

    [Fact(Timeout = 30000)]
    public async Task DynamicConsumer_WhenQueueDoesNotExist_ShouldReturnQueueMissing()
    {
        string queue = NewQueueName();
        using IConnection connection = await this.CreateConnectionAsync().ConfigureAwait(true);
        await using ServiceProvider sp = BuildServiceProvider(connection);
        IAmqpDynamicQueueConsumer consumer = sp.GetRequiredService<IAmqpDynamicQueueConsumer>();

        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(
            new DynamicQueueConsumeRequest<ExampleMessage>
            {
                QueueName = queue,
                Connection = connection,
                MaxMessages = 1,
                OnMessageAsync = (_, _) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
            },
            CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(DynamicQueueConsumeStatus.QueueMissing, result.Status);
        Assert.IsType<OperationInterruptedException>(result.Exception);
    }

    [Fact(Timeout = 30000)]
    public async Task RequeueToTail_WhenUsedByMapQueue_ShouldRepublishCurrentDeliveryAfterReadyMessages()
    {
        string queue = NewQueueName();
        using IConnection connection = await this.CreateConnectionAsync().ConfigureAwait(true);
        using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));

        _ = await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        await PublishAsync(channel, queue, new ExampleMessage { Name = "first", Age = 1 }).ConfigureAwait(true);
        await PublishAsync(channel, queue, new ExampleMessage { Name = "second", Age = 2 }).ConfigureAwait(true);

        using var countdown = new CountdownEvent(3);
        var deliveryOrder = new List<string>();
        object sync = new();
        int deliveryNumber = 0;

        await using ServiceProvider sp = BuildServiceProvider(connection);
        _ = sp.MapQueue(queue, IAmqpResult (ExampleMessage message, IAmqpContext context) =>
            {
                int currentDelivery = Interlocked.Increment(ref deliveryNumber);
                lock (sync)
                {
                    deliveryOrder.Add(message.Name);
                }

                if (!countdown.IsSet)
                {
                    countdown.Signal();
                }

                return currentDelivery == 1
                    ? AmqpResults.Compose(AmqpResults.RequeueToTail(), AmqpResults.Ack())
                    : AmqpResults.Ack();
            })
            .WithPrefetch(1)
            .WithDispatchConcurrency(1)
            .WithConsumerTag(queue)
            .WithConnection((serviceProvider, cancellationToken) => Task.FromResult(serviceProvider.GetRequiredService<IConnection>()))
            .WithSerializer(serviceProvider => serviceProvider.GetRequiredService<IAmqpSerializer>());

        IHostedService hostedService = sp.GetRequiredService<IHostedService>();

        await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(true);
        bool allDeliveriesReceived = countdown.Wait(WaitTimeout);
        await hostedService.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.True(allDeliveriesReceived, "The queue did not deliver the expected messages within the timeout.");
        Assert.Equal(["first", "second", "first"], deliveryOrder);
    }

    [Fact(Timeout = 30000)]
    public async Task MapQueue_WhenGracefulShutdownWaitsForInFlightMessages_ShouldCompleteHandlerBeforeStopReturns()
    {
        string queue = NewQueueName();
        using IConnection connection = await this.CreateConnectionAsync().ConfigureAwait(true);
        using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));

        _ = await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        await PublishAsync(channel, queue, new ExampleMessage { Name = "work", Age = 1 }).ConfigureAwait(true);

        using var handlerStarted = new ManualResetEventSlim(false);
        var releaseHandler = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool handlerCompleted = false;

        await using ServiceProvider sp = BuildServiceProvider(connection);
        _ = sp.MapQueue(queue, async Task (ExampleMessage _) =>
            {
                handlerStarted.Set();
                await releaseHandler.Task.ConfigureAwait(true);
                handlerCompleted = true;
            })
            .WithPrefetch(1)
            .WithDispatchConcurrency(1)
            .WithConsumerTag(queue)
            .WithConnection((serviceProvider, cancellationToken) => Task.FromResult(serviceProvider.GetRequiredService<IConnection>()))
            .WithSerializer(serviceProvider => serviceProvider.GetRequiredService<IAmqpSerializer>())
            .WithGracefulShutdown(options =>
            {
                options.WaitForInFlightMessages = true;
                options.DrainTimeout = TimeSpan.FromSeconds(5);
            });

        IHostedService hostedService = sp.GetRequiredService<IHostedService>();

        await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(true);
        Assert.True(handlerStarted.Wait(WaitTimeout), "The handler did not start within the timeout.");

        Task stopTask = hostedService.StopAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(200)).ConfigureAwait(true);

        Assert.False(stopTask.IsCompleted, "StopAsync returned before the in-flight handler completed.");

        releaseHandler.SetResult(null);
        await stopTask.WaitAsync(WaitTimeout).ConfigureAwait(true);

        Assert.True(handlerCompleted);
    }

    private ConnectionFactory CreateConnectionFactory()
    {
        return new ConnectionFactory
        {
            Uri = new Uri(this.rabbitMqContainer.GetConnectionString()),
        };
    }

    private async Task<IConnection> CreateConnectionAsync()
    {
        IConnection connection = null;

        await SafeRunner.ExecuteWithRetry<BrokerUnreachableException>(
            async () => connection = await this.CreateConnectionFactory().CreateConnectionAsync().ConfigureAwait(false)).ConfigureAwait(true);

        return connection;
    }

    private static ServiceProvider BuildServiceProvider(IConnection connection)
    {
        ServiceCollection services = new();
        services.AddRabbitMQConsumer();
        services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        services.AddSingleton(new ActivitySource("test"));
        services.AddNewtonsoftAmqpSerializer();
        services.AddSingleton(connection ?? throw new InvalidOperationException("Connection is null"));

        return services.BuildServiceProvider();
    }

    private static async Task PublishAsync(
        IChannel channel,
        string queueName,
        ExampleMessage message,
        CancellationToken cancellationToken = default)
    {
        byte[] body = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(message));

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            mandatory: true,
            body: body,
            cancellationToken: cancellationToken).ConfigureAwait(true);
    }

    private static string NewQueueName()
    {
        return $"attention-primitives-{Guid.NewGuid():N}";
    }
}
