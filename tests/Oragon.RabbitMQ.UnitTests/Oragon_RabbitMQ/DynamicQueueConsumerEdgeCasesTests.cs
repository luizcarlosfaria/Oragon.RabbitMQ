using Microsoft.Extensions.DependencyInjection;
using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.DynamicQueues;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class DynamicQueueConsumerEdgeCasesTests
{
    [Fact]
    public async Task ConsumeAsync_WhenBeforeStartReturnsSkip_ShouldReturnSkippedAndInvokeAfterStop()
    {
        // Arrange
        ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
        Mock<IChannel> channelMock = CreateChannelMock("attention-queue", 0);
        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        bool afterStopCalled = false;
        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            MaxMessages = 1,
            BeforeStartAsync = (context, cancellationToken) => ValueTask.FromResult(DynamicQueueStartDecision.Skip()),
            AfterStopAsync = (context, cancellationToken) =>
            {
                afterStopCalled = true;
                return ValueTask.CompletedTask;
            },
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        // Act
        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Skipped, result.Status);
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
        channelMock.Verify(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenBeforeStartReturnsDefer_ShouldReturnDeferredAndInvokeAfterStop()
    {
        // Arrange
        ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
        Mock<IChannel> channelMock = CreateChannelMock("attention-queue", 0);
        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        bool afterStopCalled = false;
        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            MaxMessages = 1,
            BeforeStartAsync = (context, cancellationToken) => ValueTask.FromResult(DynamicQueueStartDecision.Defer()),
            AfterStopAsync = (context, cancellationToken) =>
            {
                afterStopCalled = true;
                return ValueTask.CompletedTask;
            },
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        // Act
        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Deferred, result.Status);
        Assert.True(afterStopCalled);
        channelMock.Verify(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenBeforeStartReturnsFail_ShouldReturnFaultedWithExceptionAndInvokeAfterStop()
    {
        // Arrange
        ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
        Mock<IChannel> channelMock = CreateChannelMock("attention-queue", 0);
        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        InvalidOperationException startException = new("start hook refused");
        bool afterStopCalled = false;
        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            MaxMessages = 1,
            BeforeStartAsync = (context, cancellationToken) => ValueTask.FromResult(DynamicQueueStartDecision.Fail(startException)),
            AfterStopAsync = (context, cancellationToken) =>
            {
                afterStopCalled = true;
                return ValueTask.CompletedTask;
            },
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        // Act
        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Faulted, result.Status);
        Assert.Same(startException, result.Exception);
        Assert.True(afterStopCalled);
        channelMock.Verify(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenCallerTokenAlreadyCanceledAndQueueDeclareThrowsOperationCanceled_ShouldReturnInterrupted()
    {
        // Arrange
        ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
        Mock<IChannel> channelMock = new Mock<IChannel>();
        OperationCanceledException thrownException = new("declare canceled");
        _ = channelMock.Setup(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ThrowsAsync(thrownException);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(request, cts.Token);

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Interrupted, result.Status);
        Assert.Same(thrownException, result.Exception);
        channelMock.Verify(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenBasicQosThrows_ShouldReturnFaulted()
    {
        // Arrange
        ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
        Mock<IChannel> channelMock = CreateChannelMock("attention-queue", 0);
        InvalidOperationException thrownException = new("qos failed");
        _ = channelMock.Setup(it => it.BasicQosAsync(It.IsAny<uint>(), It.IsAny<ushort>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(thrownException);

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            MaxMessages = 1,
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        // Act
        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Faulted, result.Status);
        Assert.Same(thrownException, result.Exception);
        channelMock.Verify(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenNoConnectionProvidedAndContextAccessorHasConnection_ShouldUseAccessorConnection()
    {
        // Arrange
        ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
        Mock<IChannel> channelMock = CreateChannelMock("attention-queue", 0);
        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        Mock<IAmqpContext> contextMock = new Mock<IAmqpContext>();
        _ = contextMock.SetupGet(it => it.Connection).Returns(connectionMock.Object);
        AmqpContextAccessor accessor = new() { Current = contextMock.Object };

        DynamicQueueConsumer consumer = new(services, accessor);
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            StopAfterInitialQueueLength = true,
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        // Act
        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Empty, result.Status);
        connectionMock.Verify(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenNoConnectionProvidedAndAccessorEmpty_ShouldResolveConnectionFromServiceProvider()
    {
        // Arrange
        Mock<IChannel> channelMock = CreateChannelMock("attention-queue", 0);
        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(connectionMock.Object)
            .BuildServiceProvider();

        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            StopAfterInitialQueueLength = true,
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        // Act
        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Empty, result.Status);
        connectionMock.Verify(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenChannelFactoryProvided_ShouldUseFactoryChannelAndNotCreateChannelFromConnection()
    {
        // Arrange
        ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
        Mock<IChannel> channelMock = CreateChannelMock("attention-queue", 0);
        Mock<IConnection> connectionMock = new Mock<IConnection>();

        bool factoryReceivedConnection = false;
        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            StopAfterInitialQueueLength = true,
            ChannelFactory = (serviceProvider, connection, cancellationToken) =>
            {
                factoryReceivedConnection = ReferenceEquals(connectionMock.Object, connection);
                return ValueTask.FromResult(channelMock.Object);
            },
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        // Act
        DynamicQueueConsumeResult result = await consumer.ConsumeAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Empty, result.Status);
        Assert.True(factoryReceivedConnection);
        connectionMock.Verify(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeAsync_WhenMaxMessagesReachedAndSecondDeliveryArrivesWhileFirstInFlight_ShouldNackSecondWithRequeue()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        Mock<IAmqpSerializer> serializerMock = new Mock<IAmqpSerializer>();
        _ = serializerMock.Setup(it => it.Deserialize<string>(It.IsAny<BasicDeliverEventArgs>())).Returns("message");

        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        Mock<IChannel> channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
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
        _ = channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.Setup(it => it.BasicAckAsync(1, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _ = channelMock.Setup(it => it.BasicNackAsync(2, false, true, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        TaskCompletionSource handlerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirstHandler = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            MaxMessages = 1,
            MaxLocalConcurrency = 2,
            OnMessageAsync = async (message, context) =>
            {
                handlerStarted.SetResult();
                await releaseFirstHandler.Task;
                return AmqpResults.Ack();
            },
        };

        // Act
        Task<DynamicQueueConsumeResult> consumeTask = consumer.ConsumeAsync(request, CancellationToken.None);
        await WaitUntilAsync(() => capturedConsumer != null);

        Task firstDeliveryTask = capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: "consumer-tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "attention-queue",
            properties: Mock.Of<IReadOnlyBasicProperties>(),
            body: ReadOnlyMemory<byte>.Empty);

        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: "consumer-tag",
            deliveryTag: 2,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "attention-queue",
            properties: Mock.Of<IReadOnlyBasicProperties>(),
            body: ReadOnlyMemory<byte>.Empty);

        releaseFirstHandler.SetResult();
        DynamicQueueConsumeResult result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));
        await firstDeliveryTask.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.MaxMessagesReached, result.Status);
        Assert.Equal(1, result.MessagesReceived);
        channelMock.Verify(it => it.BasicNackAsync(2, false, true, It.IsAny<CancellationToken>()), Times.Once);
        channelMock.Verify(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenInitialQueueLengthReachedAndSecondDeliveryArrivesWhileFirstInFlight_ShouldNackSecondWithRequeue()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        Mock<IAmqpSerializer> serializerMock = new Mock<IAmqpSerializer>();
        _ = serializerMock.Setup(it => it.Deserialize<string>(It.IsAny<BasicDeliverEventArgs>())).Returns("message");

        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        Mock<IChannel> channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 1, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
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
        _ = channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.Setup(it => it.BasicAckAsync(1, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _ = channelMock.Setup(it => it.BasicNackAsync(2, false, true, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        TaskCompletionSource handlerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirstHandler = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            StopAfterInitialQueueLength = true,
            MaxLocalConcurrency = 2,
            OnMessageAsync = async (message, context) =>
            {
                handlerStarted.SetResult();
                await releaseFirstHandler.Task;
                return AmqpResults.Ack();
            },
        };

        // Act
        Task<DynamicQueueConsumeResult> consumeTask = consumer.ConsumeAsync(request, CancellationToken.None);
        await WaitUntilAsync(() => capturedConsumer != null);

        Task firstDeliveryTask = capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: "consumer-tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "attention-queue",
            properties: Mock.Of<IReadOnlyBasicProperties>(),
            body: ReadOnlyMemory<byte>.Empty);

        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: "consumer-tag",
            deliveryTag: 2,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "attention-queue",
            properties: Mock.Of<IReadOnlyBasicProperties>(),
            body: ReadOnlyMemory<byte>.Empty);

        releaseFirstHandler.SetResult();
        DynamicQueueConsumeResult result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));
        await firstDeliveryTask.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.InitialQueueLengthReached, result.Status);
        Assert.Equal(1, result.MessagesReceived);
        channelMock.Verify(it => it.BasicNackAsync(2, false, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenDeliveryArrivesAfterCompletionAlreadySet_ShouldNackWithoutInvokingHandler()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        Mock<IAmqpSerializer> serializerMock = new Mock<IAmqpSerializer>();
        _ = serializerMock.Setup(it => it.Deserialize<string>(It.IsAny<BasicDeliverEventArgs>())).Returns("message");

        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        Mock<IChannel> channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
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
        _ = channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.Setup(it => it.BasicAckAsync(1, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _ = channelMock.Setup(it => it.BasicNackAsync(It.IsAny<ulong>(), false, true, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        int handlerInvocations = 0;
        TaskCompletionSource handlerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirstHandler = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            MaxMessages = 1,
            MaxLocalConcurrency = 2,
            OnMessageAsync = async (message, context) =>
            {
                _ = Interlocked.Increment(ref handlerInvocations);
                handlerStarted.SetResult();
                await releaseFirstHandler.Task;
                return AmqpResults.Ack();
            },
        };

        // Act: first delivery reserves the only slot and blocks in-flight.
        Task<DynamicQueueConsumeResult> consumeTask = consumer.ConsumeAsync(request, CancellationToken.None);
        await WaitUntilAsync(() => capturedConsumer != null);

        Task firstDeliveryTask = capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: "consumer-tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "attention-queue",
            properties: Mock.Of<IReadOnlyBasicProperties>(),
            body: ReadOnlyMemory<byte>.Empty);

        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Second delivery exhausts the slot and completes the consumption cycle (completion is now set).
        await capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: "consumer-tag",
            deliveryTag: 2,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "attention-queue",
            properties: Mock.Of<IReadOnlyBasicProperties>(),
            body: ReadOnlyMemory<byte>.Empty);

        // Third delivery arrives while the first is still in-flight, but completion is already set.
        await capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: "consumer-tag",
            deliveryTag: 3,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "attention-queue",
            properties: Mock.Of<IReadOnlyBasicProperties>(),
            body: ReadOnlyMemory<byte>.Empty);

        releaseFirstHandler.SetResult();
        DynamicQueueConsumeResult result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));
        await firstDeliveryTask.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(1, handlerInvocations);
        Assert.Equal(1, result.MessagesReceived);
        channelMock.Verify(it => it.BasicNackAsync(2, false, true, It.IsAny<CancellationToken>()), Times.Once);
        channelMock.Verify(it => it.BasicNackAsync(3, false, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenHandlerReturnsNack_ShouldCountAsNacked()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        Mock<IAmqpSerializer> serializerMock = new Mock<IAmqpSerializer>();
        _ = serializerMock.Setup(it => it.Deserialize<string>(It.IsAny<BasicDeliverEventArgs>())).Returns("message");

        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        Mock<IChannel> channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
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
        _ = channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.Setup(it => it.BasicNackAsync(1, false, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            MaxMessages = 1,
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Nack(false)),
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
        DynamicQueueConsumeResult result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(1, result.MessagesNacked);
        Assert.Equal(0, result.MessagesRejected);
        Assert.Equal(0, result.MessagesAcked);
        channelMock.Verify(it => it.BasicNackAsync(1, false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenHandlerReturnsReject_ShouldCountAsRejected()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        Mock<IAmqpSerializer> serializerMock = new Mock<IAmqpSerializer>();
        _ = serializerMock.Setup(it => it.Deserialize<string>(It.IsAny<BasicDeliverEventArgs>())).Returns("message");

        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        Mock<IChannel> channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
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
        _ = channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.Setup(it => it.BasicRejectAsync(1, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            MaxMessages = 1,
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Reject(false)),
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
        DynamicQueueConsumeResult result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(1, result.MessagesRejected);
        Assert.Equal(0, result.MessagesNacked);
        Assert.Equal(0, result.MessagesAcked);
        channelMock.Verify(it => it.BasicRejectAsync(1, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenHandlerObservesCallerCancellation_ShouldReturnInterrupted()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        Mock<IAmqpSerializer> serializerMock = new Mock<IAmqpSerializer>();
        _ = serializerMock.Setup(it => it.Deserialize<string>(It.IsAny<BasicDeliverEventArgs>())).Returns("message");

        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        Mock<IChannel> channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
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
        _ = channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        TaskCompletionSource handlerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            InFlightDrainTimeout = TimeSpan.FromSeconds(5),
            OnMessageAsync = async (message, context) =>
            {
                handlerStarted.SetResult();
                await Task.Delay(TimeSpan.FromSeconds(5), context.CancellationToken);
                return AmqpResults.Ack();
            },
        };

        using CancellationTokenSource cts = new();

        // Act
        Task<DynamicQueueConsumeResult> consumeTask = consumer.ConsumeAsync(request, cts.Token);
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
        cts.Cancel();
        DynamicQueueConsumeResult result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));
        await deliveryTask.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Interrupted, result.Status);
    }

    [Fact]
    public async Task ConsumeAsync_WhenHandlerThrows_ShouldNackAndReturnFaultedWithHandlerException()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        Mock<IAmqpSerializer> serializerMock = new Mock<IAmqpSerializer>();
        _ = serializerMock.Setup(it => it.Deserialize<string>(It.IsAny<BasicDeliverEventArgs>())).Returns("message");

        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        Mock<IChannel> channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
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
        _ = channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.Setup(it => it.BasicNackAsync(1, false, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        InvalidOperationException handlerException = new("handler failure");
        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            MaxMessages = 1,
            OnMessageAsync = (message, context) => throw handlerException,
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
        DynamicQueueConsumeResult result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Faulted, result.Status);
        Assert.Same(handlerException, result.Exception);
        Assert.Equal(1, result.MessagesNacked);
        channelMock.Verify(it => it.BasicNackAsync(1, false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WhenHandlerThrowsAndUnhandledNackAlsoThrows_ShouldReturnFaultedWithoutCountingNack()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        Mock<IAmqpSerializer> serializerMock = new Mock<IAmqpSerializer>();
        _ = serializerMock.Setup(it => it.Deserialize<string>(It.IsAny<BasicDeliverEventArgs>())).Returns("message");

        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        Mock<IChannel> channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
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
        _ = channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.Setup(it => it.BasicNackAsync(1, false, false, It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("nack failed"));
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        InvalidOperationException handlerException = new("handler failure");
        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            MaxMessages = 1,
            OnMessageAsync = (message, context) => throw handlerException,
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
        DynamicQueueConsumeResult result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Faulted, result.Status);
        Assert.Same(handlerException, result.Exception);
        Assert.Equal(0, result.MessagesNacked);
    }

    [Fact]
    public async Task ConsumeAsync_WhenBrokerCancelsConsumer_ShouldReturnInterruptedWithBrokerCanceledFlag()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(Mock.Of<IAmqpSerializer>())
            .BuildServiceProvider();

        Mock<IChannel> channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
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
        _ = channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        using CancellationTokenSource cts = new();

        // Act
        Task<DynamicQueueConsumeResult> consumeTask = consumer.ConsumeAsync(request, cts.Token);
        await WaitUntilAsync(() => capturedConsumer != null);
        await capturedConsumer.HandleChannelShutdownAsync(
            this,
            new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED"));
        DynamicQueueConsumeResult result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Interrupted, result.Status);
        Assert.True(result.BrokerCanceledConsumer);
    }

    [Fact]
    public async Task ConsumeAsync_WhenNoStopRuleFiresAndCallerTokenIsCanceledWhileWaiting_ShouldReturnInterrupted()
    {
        // Arrange
        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(Mock.Of<IAmqpSerializer>())
            .BuildServiceProvider();

        Mock<IChannel> channelMock = new Mock<IChannel>();
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

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
        {
            QueueName = "attention-queue",
            Connection = connectionMock.Object,
            OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
        };

        using CancellationTokenSource cts = new();

        // Act: no delivery ever arrives; only cancellation should terminate consumption.
        Task<DynamicQueueConsumeResult> consumeTask = consumer.ConsumeAsync(request, cts.Token);
        await WaitUntilAsync(() => channelMock.Invocations.Any(invocation => invocation.Method.Name == nameof(IChannel.BasicConsumeAsync)));
        await Task.Delay(TimeSpan.FromMilliseconds(150));
        cts.Cancel();
        DynamicQueueConsumeResult result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(DynamicQueueConsumeStatus.Interrupted, result.Status);
        channelMock.VerifyAll();
    }

    [Fact]
    public async Task ConsumeAsync_WhenRemainingReadyCountQueryThrows_ShouldReturnNullRemainingReadyCount()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        Mock<IAmqpSerializer> serializerMock = new Mock<IAmqpSerializer>();
        _ = serializerMock.Setup(it => it.Deserialize<string>(It.IsAny<BasicDeliverEventArgs>())).Returns("message");

        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        Mock<IChannel> channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0))
            .ThrowsAsync(new InvalidOperationException("queue declare failed on remaining count check"));
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
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
        _ = channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.Setup(it => it.BasicAckAsync(1, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
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
        DynamicQueueConsumeResult result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(1, result.MessagesReceived);
        Assert.Null(result.RemainingReadyCount);
    }

    [Fact]
    public async Task ConsumeAsync_WhenInFlightDrainTimesOutAndCloseChannelThrows_ShouldSwallowCleanupException()
    {
        // Arrange
        AsyncEventingBasicConsumer capturedConsumer = null;
        Mock<IAmqpSerializer> serializerMock = new Mock<IAmqpSerializer>();
        _ = serializerMock.Setup(it => it.Deserialize<string>(It.IsAny<BasicDeliverEventArgs>())).Returns("message");

        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        Mock<IChannel> channelMock = new Mock<IChannel>();
        _ = channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync("attention-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 1, 0))
            .ReturnsAsync(new QueueDeclareOk("attention-queue", 0, 0));
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
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
        _ = channelMock.Setup(it => it.BasicCancelAsync("consumer-tag", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = channelMock.SetupGet(it => it.IsOpen).Returns(true);
        _ = channelMock.Setup(it => it.BasicAckAsync(1, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _ = channelMock.Setup(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("close failed"));

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        TaskCompletionSource handlerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseHandler = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DynamicQueueConsumer consumer = new(services, new AmqpContextAccessor());
        DynamicQueueConsumeRequest<string> request = new()
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

        // Assert: drain timed out, channel not closed yet because in-flight work is still pending.
        Assert.True(result.InFlightDrainTimedOut);

        releaseHandler.SetResult();
        await deliveryTask.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => channelMock.Invocations.Any(invocation => invocation.Method.Name == nameof(IChannel.CloseAsync)));

        // The best-effort cleanup after a timed-out drain must swallow the CloseAsync failure.
        channelMock.Verify(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IChannel> CreateChannelMock(string queueName, uint messageCount)
    {
        Mock<IChannel> channelMock = new Mock<IChannel>();
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
