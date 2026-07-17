using Microsoft.Extensions.DependencyInjection;
using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.DynamicQueues;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class DynamicQueueConsumerTests
{
    [Fact]
    public async Task ConsumeAsync_WhenInitialQueueLengthIsZero_ShouldReturnEmpty()
    {
        // Arrange
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var channelMock = CreateChannelMock("attention-queue", 0);
        var connectionMock = new Mock<IConnection>();
        connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object)
            .Verifiable(Times.Once());

        var consumer = new DynamicQueueConsumer(services, new AmqpContextAccessor());
        var beforeStartCalled = false;
        var afterStopCalled = false;

        var request = new DynamicQueueConsumeRequest<string>
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            StopAfterInitialQueueLength = true,
            BeforeStartAsync = (context, cancellationToken) =>
            {
                beforeStartCalled = ReferenceEquals(services, context.Services);
                return ValueTask.FromResult(DynamicQueueStartDecision.Allow());
            },
            AfterStopAsync = (context, cancellationToken) =>
            {
                afterStopCalled = ReferenceEquals(services, context.Services);
                return ValueTask.CompletedTask;
            },
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        // Act
        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Empty, result.Status);
        Assert.Equal(0, result.InitialReadyCount);
        Assert.True(beforeStartCalled);
        Assert.True(afterStopCalled);
        channelMock.Verify(it => it.BasicConsumeAsync(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()), Times.Never);
        connectionMock.VerifyAll();
    }

    [Fact]
    public async Task ConsumeAsync_WhenQueueStartsEmptyAndOnlyMaxMessagesExists_ShouldWaitForDeliveryLimit()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        var serializerMock = new Mock<IAmqpSerializer>();
        _ = serializerMock
            .Setup(it => it.Deserialize<string>(It.IsAny<BasicDeliverEventArgs>()))
            .Returns("message");

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        var channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        channelMock.Setup(it => it.BasicConsumeAsync(
                "attention-queue",
                false,
                string.Empty,
                true,
                false,
                null,
                It.IsAny<IAsyncBasicConsumer>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, bool, string, bool, bool, IDictionary<string, object>, IAsyncBasicConsumer, CancellationToken>((_, _, _, _, _, _, consumer, _) =>
            {
                capturedConsumer = Assert.IsType<AsyncEventingBasicConsumer>(consumer);
            })
            .ReturnsAsync("consumer-tag");
        channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.Setup(it => it.BasicAckAsync(1, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        var consumer = new DynamicQueueConsumer(services, new AmqpContextAccessor());
        var request = new DynamicQueueConsumeRequest<string>
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            MaxMessages = 1,
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        // Act
        Task<DynamicQueueConsumeResult> consumeTask = consumer.ConsumeAsync(request, CancellationToken.None);
        await WaitUntilAsync(() => capturedConsumer != null);
        await capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: "consumer-tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "attention-queue",
            properties: Mock.Of<IReadOnlyBasicProperties>(),
            body: ReadOnlyMemory<byte>.Empty);
        DynamicQueueConsumeResult result = await consumeTask;

        // Assert
        Assert.Null(result.Exception);
        Assert.Equal(DynamicQueueConsumeStatus.MaxMessagesReached, result.Status);
        Assert.Equal(0, result.InitialReadyCount);
        Assert.Equal(1, result.MessagesReceived);
        Assert.Equal(1, result.MessagesAcked);
        channelMock.Verify(it => it.BasicConsumeAsync(
            "attention-queue",
            false,
            string.Empty,
            true,
            false,
            null,
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenQueueIsEmptyAndIdleTimeoutExists_ShouldOpenConsumerUntilIdleTimeout()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(Mock.Of<IAmqpSerializer>())
            .BuildServiceProvider();

        var channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        channelMock.Setup(it => it.BasicConsumeAsync(
                "attention-queue",
                false,
                string.Empty,
                true,
                false,
                null,
                It.IsAny<IAsyncBasicConsumer>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("consumer-tag")
            .Verifiable(Times.Once());
        channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        var consumer = new DynamicQueueConsumer(services, new AmqpContextAccessor());
        var request = new DynamicQueueConsumeRequest<string>
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            IdleTimeout = TimeSpan.FromMilliseconds(10),
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        // Act
        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.IdleTimeoutReached, result.Status);
        Assert.Equal(0, result.MessagesReceived);
        channelMock.VerifyAll();
    }

    [Fact]
    public async Task ConsumeAsync_WhenConnectionFactoryIsProvided_ShouldReceiveServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var channelMock = CreateChannelMock("attention-queue", 0);
        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        var factoryReceivedServiceProvider = false;
        var consumer = new DynamicQueueConsumer(services, new AmqpContextAccessor());
        var request = new DynamicQueueConsumeRequest<string>
        {
            QueueName = "attention-queue",
            StopAfterInitialQueueLength = true,
            ConnectionFactory = (serviceProvider, cancellationToken) =>
            {
                factoryReceivedServiceProvider = ReferenceEquals(services, serviceProvider);
                return ValueTask.FromResult(connectionMock.Object);
            },
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        // Act
        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Empty, result.Status);
        Assert.True(factoryReceivedServiceProvider);
    }

    [Fact]
    public async Task ConsumeAsync_WhenMaxDurationIsReached_ShouldCancelMessageContextToken()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        var serializerMock = new Mock<IAmqpSerializer>();
        _ = serializerMock
            .Setup(it => it.Deserialize<string>(It.IsAny<BasicDeliverEventArgs>()))
            .Returns("message");

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        var channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 1, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        channelMock.Setup(it => it.BasicConsumeAsync(
                "attention-queue",
                false,
                string.Empty,
                true,
                false,
                null,
                It.IsAny<IAsyncBasicConsumer>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, bool, string, bool, bool, IDictionary<string, object>, IAsyncBasicConsumer, CancellationToken>((_, _, _, _, _, _, consumer, _) =>
            {
                capturedConsumer = Assert.IsType<AsyncEventingBasicConsumer>(consumer);
            })
            .ReturnsAsync("consumer-tag");
        channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokenWasCanceled = false;
        var consumer = new DynamicQueueConsumer(services, new AmqpContextAccessor());
        var request = new DynamicQueueConsumeRequest<string>
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            MaxDuration = TimeSpan.FromMilliseconds(500),
            OnMessageAsync = async (message, context) =>
            {
                handlerStarted.SetResult();
                using CancellationTokenRegistration registration = context.CancellationToken.Register(() => tokenWasCanceled = true);
                await Task.Delay(TimeSpan.FromSeconds(5), context.CancellationToken);

                return AmqpResults.Ack();
            },
        };

        // Act
        Task<DynamicQueueConsumeResult> consumeTask = consumer.ConsumeAsync(request, CancellationToken.None);
        await WaitUntilAsync(() => capturedConsumer != null);
        await capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: "consumer-tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "attention-queue",
            properties: Mock.Of<IReadOnlyBasicProperties>(),
            body: ReadOnlyMemory<byte>.Empty);
        DynamicQueueConsumeResult result = await consumeTask;

        // Assert
        Assert.Null(result.Exception);
        Assert.Equal(DynamicQueueConsumeStatus.MaxDurationReached, result.Status);
        Assert.True(tokenWasCanceled);
        Assert.True(handlerStarted.Task.IsCompletedSuccessfully);
        channelMock.Verify(it => it.BasicAckAsync(1, false, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeAsync_WhenInFlightDoesNotDrainBeforeTimeout_ShouldReturnTimedOutResultWithoutClosingChannel()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        var serializerMock = new Mock<IAmqpSerializer>();
        _ = serializerMock
            .Setup(it => it.Deserialize<string>(It.IsAny<BasicDeliverEventArgs>()))
            .Returns("message");

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        var channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 1, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        channelMock.Setup(it => it.BasicConsumeAsync(
                "attention-queue",
                false,
                string.Empty,
                true,
                false,
                null,
                It.IsAny<IAsyncBasicConsumer>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, bool, string, bool, bool, IDictionary<string, object>, IAsyncBasicConsumer, CancellationToken>((_, _, _, _, _, _, consumer, _) =>
            {
                capturedConsumer = Assert.IsType<AsyncEventingBasicConsumer>(consumer);
            })
            .ReturnsAsync("consumer-tag");
        channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.BasicAckAsync(1, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new DynamicQueueConsumer(services, new AmqpContextAccessor());
        var request = new DynamicQueueConsumeRequest<string>
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            MaxDuration = TimeSpan.FromMilliseconds(20),
            InFlightDrainTimeout = TimeSpan.FromMilliseconds(20),
            OnMessageAsync = async (message, context) =>
            {
                handlerStarted.SetResult();

                await releaseHandler.Task;

                return AmqpResults.Ack();
            },
        };

        // Act
        Task<DynamicQueueConsumeResult> consumeTask = consumer.ConsumeAsync(request, CancellationToken.None);
        await WaitUntilAsync(() => capturedConsumer != null);
        Task deliveryTask = capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: "consumer-tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "attention-queue",
            properties: Mock.Of<IReadOnlyBasicProperties>(),
            body: ReadOnlyMemory<byte>.Empty);

        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        DynamicQueueConsumeResult result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.Null(result.Exception);
        Assert.Equal(DynamicQueueConsumeStatus.MaxDurationReached, result.Status);
        Assert.True(result.InFlightDrainTimedOut);
        Assert.Equal(1, result.MessagesReceived);
        channelMock.Verify(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

        releaseHandler.SetResult();
        await deliveryTask.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => channelMock.Invocations.Any(invocation => invocation.Method.Name == nameof(IChannel.CloseAsync)));
        channelMock.Verify(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenStopRuleIsMissing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var consumer = new DynamicQueueConsumer(services, new AmqpContextAccessor());
        var request = new DynamicQueueConsumeRequest<string>
        {
            QueueName = "attention-queue",
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        // Act & Assert
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeAsync(request, CancellationToken.None));
    }

    private static Mock<IChannel> CreateChannelMock(string queueName, uint messageCount)
    {
        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.QueueDeclarePassiveAsync(queueName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk(queueName, messageCount, 0));
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return channelMock;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
    }
}
