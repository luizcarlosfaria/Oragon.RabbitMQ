using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using Oragon.RabbitMQ.TestsExtensions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class QueueConsumerEdgeCasesTests
{
    public class TestService
    {
        public Task HandleAsync(TestMessage msg) => Task.CompletedTask;
    }

    public class TestMessage
    {
        public string Value { get; set; }
    }

    public interface IUnregisteredKeyedService
    {
    }

    private sealed class ThrowingResult : IAmqpResult
    {
        public Task ExecuteAsync(IAmqpContext context) => throw new InvalidOperationException("primary or failure result execution failed");
    }

    private static (ServiceProvider sp, Mock<IChannel> channelMock, Mock<IConnection> connectionMock) BuildTestInfrastructure(
        string queueName = "test-queue",
        Action<Mock<IChannel>> configureChannel = null)
    {
        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.IsAny<string>(), false, It.IsAny<string>(), true, false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("consumerTag");

        configureChannel?.Invoke(channelMock);

        IChannel channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        IConnection connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging();
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        ServiceProvider sp = services.BuildServiceProvider();

        _ = sp.MapQueue(queueName, ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        return (sp, channelMock, connectionMock);
    }

    #region WaitQueueCreationAsync retry (Polly)

    [Fact]
    public async Task InitializeAsync_WhenFirstQueueProbeThrowsOperationInterrupted_ShouldRetryAndSucceed()
    {
        // Arrange
        (ServiceProvider sp, Mock<IChannel> channelMock, Mock<IConnection> connectionMock) = BuildTestInfrastructure();
        _ = channelMock
            .SetupSequence(it => it.QueueDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationInterruptedException())
            .ReturnsAsync(new QueueDeclareOk("test-queue", 0, 0));

        IConsumerDescriptor descriptor = sp.GetRequiredService<ConsumerServer>().ConsumerDescriptors.Single();

        // Act - first probe fails, Polly waits ~2s (2^1) and retries, second probe succeeds
        IHostedAmqpConsumer consumer = await descriptor.BuildConsumerAsync(CancellationToken.None);

        // Assert
        Assert.True(((QueueConsumer)consumer).IsInitialized);
        channelMock.Verify(it => it.QueueDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    #endregion

    #region ValidateServiceBindingsAsync - unregistered keyed service

    [Fact]
    public async Task InitializeAsync_WithUnregisteredKeyedService_ShouldThrowInvalidOperationExceptionWithServiceKeyInMessage()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.IsAny<string>(), false, It.IsAny<string>(), true, false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("tag");
        IChannel channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        _ = services.AddSingleton(connectionMock.Object);

        _ = services.AddLogging();
        _ = services.AddNewtonsoftAmqpSerializer();
        // Intentionally NOT registering IUnregisteredKeyedService with the "minha-chave" key

        ServiceProvider sp = services.BuildServiceProvider();

        _ = sp.MapQueue("test-queue", ([FromServices("minha-chave")] IUnregisteredKeyedService svc, [FromBody] TestMessage msg) => Task.CompletedTask);

        IConsumerDescriptor descriptor = sp.GetRequiredService<ConsumerServer>().ConsumerDescriptors.Single();

        // Act
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => descriptor.BuildConsumerAsync(CancellationToken.None));

        // Assert
        Assert.Contains("with key 'minha-chave'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region DetermineConnectionOwnershipAsync - probe connection open

    [Fact]
    public async Task InitializeAsync_WhenProbeConnectionIsOpen_ShouldCloseAndDisposeProbeOnlyAndPreserveMainConnection()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.IsAny<string>(), false, It.IsAny<string>(), true, false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("tag");

        var runtimeConnectionMock = new Mock<IConnection>();
        _ = runtimeConnectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channelMock.Object);

        var probeConnectionMock = new Mock<IConnection>();
        probeConnectionMock.Setup(it => it.IsOpen).Returns(true);

        var connections = new Queue<IConnection>([
            runtimeConnectionMock.Object,
            probeConnectionMock.Object
        ]);

        _ = services.AddLogging();
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        ServiceProvider sp = services.BuildServiceProvider();

        _ = sp.MapQueue("test-queue", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg))
            .WithConnection((_, _) => Task.FromResult(connections.Dequeue()));

        IConsumerDescriptor descriptor = sp.GetRequiredService<ConsumerServer>().ConsumerDescriptors.Single();

        // Act
        _ = await descriptor.BuildConsumerAsync(CancellationToken.None);

        // Assert - probe connection is closed and disposed, main (runtime) connection is left untouched
        probeConnectionMock.Verify(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        probeConnectionMock.Verify(it => it.Dispose(), Times.Once);
        runtimeConnectionMock.Verify(it => it.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        runtimeConnectionMock.Verify(it => it.Dispose(), Times.Never);
        Assert.Empty(connections);
    }

    #endregion

    #region ReceiveAsync outer catch + TryNackMessageAsync

    [Fact]
    public async Task ReceiveAsync_WhenContextAccessorThrowsAndChannelIsOpen_ShouldNackDelivery()
    {
        // Arrange
        const string consumerTag = "consumerTag";
        const string queueName = "test-queue";

        ServiceCollection services = new();

        var accessorMock = new Mock<IAmqpContextAccessor>();
        _ = accessorMock.SetupSet(a => a.Current = It.Is<IAmqpContext>(v => v != null)).Throws(new InvalidOperationException("accessor failure"));
        _ = services.AddSingleton<IAmqpContextAccessor>(accessorMock.Object);

        services.AddRabbitMQConsumer();

        AsyncEventingBasicConsumer capturedConsumer = null;

        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.Is<string>(queue => queue == queueName),
            false, It.IsAny<string>(), true, false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .Callback((string queue, bool autoAck, string tag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken) => capturedConsumer = (AsyncEventingBasicConsumer)consumer)
            .ReturnsAsync(consumerTag);
        _ = channelMock.Setup(it => it.IsClosed).Returns(false);
        _ = channelMock.Setup(it => it.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        IChannel channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        _ = services.AddSingleton(connectionMock.Object);

        _ = services.AddLogging();
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        ServiceProvider sp = services.BuildServiceProvider();

        _ = sp.MapQueue(queueName, ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        IHostedService hostedService = sp.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);

        SafeRunner.Wait(() => capturedConsumer != null);

        ReadOnlyBasicProperties properties = new BasicProperties().ToReadOnly();
        byte[] bytes = Encoding.UTF8.GetBytes("{}");

        // Act - the accessor setter throws before dispatch even happens; falls into the outer catch
        await capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: consumerTag, deliveryTag: 1, redelivered: false,
            exchange: "e", routingKey: "r", properties: properties, body: new ReadOnlyMemory<byte>(bytes));

        // Assert
        channelMock.Verify(it => it.BasicNackAsync(1, false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveAsync_WhenContextAccessorThrowsAndChannelIsClosed_ShouldNotAttemptNack()
    {
        // Arrange
        const string consumerTag = "consumerTag";
        const string queueName = "test-queue";

        ServiceCollection services = new();

        var accessorMock = new Mock<IAmqpContextAccessor>();
        _ = accessorMock.SetupSet(a => a.Current = It.Is<IAmqpContext>(v => v != null)).Throws(new InvalidOperationException("accessor failure"));
        _ = services.AddSingleton<IAmqpContextAccessor>(accessorMock.Object);

        services.AddRabbitMQConsumer();

        AsyncEventingBasicConsumer capturedConsumer = null;

        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.Is<string>(queue => queue == queueName),
            false, It.IsAny<string>(), true, false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .Callback((string queue, bool autoAck, string tag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken) => capturedConsumer = (AsyncEventingBasicConsumer)consumer)
            .ReturnsAsync(consumerTag);
        _ = channelMock.Setup(it => it.IsClosed).Returns(true);

        IChannel channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        _ = services.AddSingleton(connectionMock.Object);

        _ = services.AddLogging();
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        ServiceProvider sp = services.BuildServiceProvider();

        _ = sp.MapQueue(queueName, ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        IHostedService hostedService = sp.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);

        SafeRunner.Wait(() => capturedConsumer != null);

        ReadOnlyBasicProperties properties = new BasicProperties().ToReadOnly();
        byte[] bytes = Encoding.UTF8.GetBytes("{}");

        // Act
        await capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: consumerTag, deliveryTag: 1, redelivered: false,
            exchange: "e", routingKey: "r", properties: properties, body: new ReadOnlyMemory<byte>(bytes));

        // Assert - channel is closed, so TryNackMessageAsync must not attempt to nack
        channelMock.Verify(it => it.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveAsync_WhenContextAccessorThrowsAndNackAlsoThrows_ShouldSwallowException()
    {
        // Arrange
        const string consumerTag = "consumerTag";
        const string queueName = "test-queue";

        ServiceCollection services = new();

        var accessorMock = new Mock<IAmqpContextAccessor>();
        _ = accessorMock.SetupSet(a => a.Current = It.Is<IAmqpContext>(v => v != null)).Throws(new InvalidOperationException("accessor failure"));
        _ = services.AddSingleton<IAmqpContextAccessor>(accessorMock.Object);

        services.AddRabbitMQConsumer();

        AsyncEventingBasicConsumer capturedConsumer = null;

        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.Is<string>(queue => queue == queueName),
            false, It.IsAny<string>(), true, false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .Callback((string queue, bool autoAck, string tag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken) => capturedConsumer = (AsyncEventingBasicConsumer)consumer)
            .ReturnsAsync(consumerTag);
        _ = channelMock.Setup(it => it.IsClosed).Returns(false);
        _ = channelMock.Setup(it => it.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("nack failed"));

        IChannel channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        _ = services.AddSingleton(connectionMock.Object);

        _ = services.AddLogging();
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        ServiceProvider sp = services.BuildServiceProvider();

        _ = sp.MapQueue(queueName, ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        IHostedService hostedService = sp.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);

        SafeRunner.Wait(() => capturedConsumer != null);

        ReadOnlyBasicProperties properties = new BasicProperties().ToReadOnly();
        byte[] bytes = Encoding.UTF8.GetBytes("{}");

        // Act & Assert - should not throw despite Nack itself failing
        await capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: consumerTag, deliveryTag: 1, redelivered: false,
            exchange: "e", routingKey: "r", properties: properties, body: new ReadOnlyMemory<byte>(bytes));

        channelMock.Verify(it => it.BasicNackAsync(1, false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ExecuteResultAsync - primary and failure result both throw

    [Fact]
    public async Task ExecuteResultAsync_WhenPrimaryAndFailureResultBothThrow_ShouldSwallowException()
    {
        // Arrange
        const string consumerTag = "consumerTag";
        const string queueName = "test-queue";

        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        AsyncEventingBasicConsumer capturedConsumer = null;

        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.Is<string>(queue => queue == queueName),
            false, It.IsAny<string>(), true, false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .Callback((string queue, bool autoAck, string tag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken) => capturedConsumer = (AsyncEventingBasicConsumer)consumer)
            .ReturnsAsync(consumerTag);

        IChannel channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        _ = services.AddSingleton(connectionMock.Object);

        _ = services.AddLogging();
        _ = services.AddNewtonsoftAmqpSerializer();

        ServiceProvider sp = services.BuildServiceProvider();

        _ = sp.MapQueue(queueName, Task<IAmqpResult> (TestMessage msg) => Task.FromResult<IAmqpResult>(new ThrowingResult()))
            .WhenResultExecutionFail((amqpContext, exception) => new ThrowingResult());

        IHostedService hostedService = sp.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);

        SafeRunner.Wait(() => capturedConsumer != null);

        ReadOnlyBasicProperties properties = new BasicProperties().ToReadOnly();
        byte[] bytes = Encoding.UTF8.GetBytes("{}");

        // Act & Assert - both the primary result and the configured failure result throw; nothing should propagate
        await capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: consumerTag, deliveryTag: 1, redelivered: false,
            exchange: "e", routingKey: "r", properties: properties, body: new ReadOnlyMemory<byte>(bytes));
    }

    #endregion

    #region Graceful shutdown - BasicCancel timeout

    [Fact]
    public async Task StopAsync_WhenBasicCancelExceedsDrainTimeout_ShouldCompleteWithoutThrowing()
    {
        // Arrange
        (ServiceProvider sp, Mock<IChannel> channelMock, Mock<IConnection> connectionMock) = BuildTestInfrastructure();
        _ = channelMock
            .Setup(it => it.BasicCancelAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
            .Returns((string tag, bool noWait, CancellationToken cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken));

        ConsumerServer consumerServer = sp.GetRequiredService<ConsumerServer>();
        IConsumerDescriptor descriptor = consumerServer.ConsumerDescriptors.Single();
        _ = descriptor.WithGracefulShutdown(options => options.DrainTimeout = TimeSpan.FromMilliseconds(100));

        await consumerServer.StartAsync(CancellationToken.None);
        var queueConsumer = (QueueConsumer)consumerServer.Consumers.Single();

        // Act - BasicCancel never completes; the shutdown token cancels it after DrainTimeout
        await queueConsumer.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.False(queueConsumer.IsConsuming);
    }

    #endregion

    #region Graceful shutdown - drain timed out

    [Fact]
    public async Task StopAsync_WhenInFlightMessageDoesNotCompleteBeforeDrainTimeout_ShouldCompleteWithDrainTimedOut()
    {
        // Arrange
        const string consumerTag = "consumerTag";
        const string queueName = "test-queue";

        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        AsyncEventingBasicConsumer capturedConsumer = null;

        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.Is<string>(queue => queue == queueName),
            false, It.IsAny<string>(), true, false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .Callback((string queue, bool autoAck, string tag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken) => capturedConsumer = (AsyncEventingBasicConsumer)consumer)
            .ReturnsAsync(consumerTag);
        _ = channelMock.Setup(it => it.BasicCancelAsync(consumerTag, false, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        IChannel channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        _ = services.AddSingleton(connectionMock.Object);

        _ = services.AddLogging();
        _ = services.AddNewtonsoftAmqpSerializer();

        ServiceProvider sp = services.BuildServiceProvider();

        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = sp.MapQueue(queueName, async Task (TestMessage msg) =>
            {
                handlerStarted.SetResult();
                await releaseHandler.Task.ConfigureAwait(false);
            })
            .WithGracefulShutdown(options =>
            {
                options.WaitForInFlightMessages = true;
                options.DrainTimeout = TimeSpan.FromMilliseconds(200);
            });

        IHostedService hostedService = sp.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);

        SafeRunner.Wait(() => capturedConsumer != null);

        ReadOnlyBasicProperties properties = new BasicProperties().ToReadOnly();
        byte[] bytes = Encoding.UTF8.GetBytes("{}");

        Task deliveryTask = capturedConsumer.HandleBasicDeliverAsync(
            consumerTag: consumerTag, deliveryTag: 1, redelivered: false,
            exchange: "e", routingKey: "r", properties: properties, body: new ReadOnlyMemory<byte>(bytes));

        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var queueConsumer = (QueueConsumer)((ConsumerServer)hostedService).Consumers.Single();

        // Act - the handler is still stuck, so the drain must time out instead of hanging forever
        await queueConsumer.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.False(queueConsumer.IsConsuming);

        // Cleanup - release the handler so the pending delivery task can complete
        releaseHandler.SetResult();
        await deliveryTask.WaitAsync(TimeSpan.FromSeconds(3));
    }

    #endregion

    #region DisposeAsync - ObjectDisposedException swallowed

    [Fact]
    public async Task DisposeAsync_WhenConnectionIsOpenGetterAndDisposeBothThrowObjectDisposedException_ShouldNotPropagate()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        var startupChannelMock = new Mock<IChannel>();
        _ = startupChannelMock.Setup(it => it.BasicConsumeAsync(
            It.IsAny<string>(), false, It.IsAny<string>(), true, false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("tag");
        _ = startupChannelMock.Setup(it => it.BasicCancelAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var probeConnectionMock = new Mock<IConnection>();
        probeConnectionMock.Setup(it => it.IsOpen).Returns(false);

        var runtimeConnectionMock = new Mock<IConnection>();
        _ = runtimeConnectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(startupChannelMock.Object);
        runtimeConnectionMock.Setup(it => it.IsOpen).Throws(new ObjectDisposedException("connection"));
        runtimeConnectionMock.Setup(it => it.Dispose()).Throws(new ObjectDisposedException("connection"));

        var connections = new Queue<IConnection>([
            runtimeConnectionMock.Object,
            probeConnectionMock.Object
        ]);

        _ = services.AddLogging();
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        ServiceProvider sp = services.BuildServiceProvider();

        _ = sp.MapQueue("test-queue", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg))
            .WithConnection((_, _) => Task.FromResult(connections.Dequeue()));

        ConsumerServer consumerServer = sp.GetRequiredService<ConsumerServer>();

        await consumerServer.StartAsync(CancellationToken.None);
        var queueConsumer = (QueueConsumer)consumerServer.Consumers.Single();
        Assert.True(queueConsumer.IsConsuming);

        // Act & Assert - IsOpen getter and Dispose() both throw ObjectDisposedException, DisposeAsync must swallow both
        await queueConsumer.DisposeAsync();

        runtimeConnectionMock.Verify(it => it.Dispose(), Times.Once);
    }

    #endregion

    #region DetachConnectionHandlers - ObjectDisposedException swallowed

    [Fact]
    public async Task StopAsync_WhenDetachingConnectionHandlersThrowsObjectDisposedException_ShouldNotPropagate()
    {
        // Arrange
        (ServiceProvider sp, Mock<IChannel> channelMock, Mock<IConnection> connectionMock) = BuildTestInfrastructure();
        _ = channelMock.Setup(it => it.BasicCancelAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _ = connectionMock
            .SetupRemove(c => c.ConnectionShutdownAsync -= It.IsAny<AsyncEventHandler<ShutdownEventArgs>>())
            .Throws(new ObjectDisposedException("connection"));

        ConsumerServer consumerServer = sp.GetRequiredService<ConsumerServer>();

        await consumerServer.StartAsync(CancellationToken.None);
        var queueConsumer = (QueueConsumer)consumerServer.Consumers.Single();

        // Act & Assert - unsubscribing the shutdown handler throws, StopAsync must not propagate it
        await queueConsumer.StopAsync(CancellationToken.None);

        Assert.False(queueConsumer.IsConsuming);
    }

    #endregion
}
