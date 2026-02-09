using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using Oragon.RabbitMQ.TestsExtensions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class QueueConsumerExtendedTests
{
    public class TestService
    {
        public Task HandleAsync(TestMessage msg) => Task.CompletedTask;
    }

    public class TestMessage
    {
        public string Value { get; set; }
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

        var channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var sp = services.BuildServiceProvider();

        _ = sp.MapQueue(queueName, ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        return (sp, channelMock, connectionMock);
    }

    #region InitializeAsync

    [Fact]
    public async Task InitializeAsync_AlreadyInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var (sp, channelMock, connectionMock) = BuildTestInfrastructure();
        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        await consumerServer.StartAsync(CancellationToken.None);

        var queueConsumer = (QueueConsumer)consumerServer.Consumers.Single();

        // Assert - already initialized
        Assert.True(queueConsumer.IsInitialized);

        // Act & Assert - second InitializeAsync should throw
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => queueConsumer.InitializeAsync(CancellationToken.None));
    }

    #endregion

    #region StartAsync

    [Fact]
    public async Task StartAsync_NotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<QueueConsumer>>();
        var descriptorSp = Mock.Of<IServiceProvider>();
        var descriptor = new ConsumerDescriptor(descriptorSp, "test-queue",
            ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        var queueConsumer = new QueueConsumer(loggerMock.Object, descriptor);

        // Act & Assert - should throw because not initialized
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => queueConsumer.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_AlreadyConsuming_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var (sp, channelMock, connectionMock) = BuildTestInfrastructure();
        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        await consumerServer.StartAsync(CancellationToken.None);

        var queueConsumer = (QueueConsumer)consumerServer.Consumers.Single();

        Assert.True(queueConsumer.IsConsuming);

        // Act & Assert - second StartAsync should throw
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => queueConsumer.StartAsync(CancellationToken.None));
    }

    #endregion

    #region StopAsync

    [Fact]
    public async Task StopAsync_WhenStarted_ShouldCallBasicCancel()
    {
        // Arrange
        var (sp, channelMock, connectionMock) = BuildTestInfrastructure();
        _ = channelMock.Setup(it => it.BasicCancelAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()));

        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        await consumerServer.StartAsync(CancellationToken.None);

        var queueConsumer = (QueueConsumer)consumerServer.Consumers.Single();
        Assert.True(queueConsumer.WasStarted);

        // Act
        await queueConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.False(queueConsumer.IsConsuming);
        channelMock.Verify(it => it.BasicCancelAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_WhenNotStarted_ShouldNotCallBasicCancel()
    {
        // Arrange
        var (sp, channelMock, connectionMock) = BuildTestInfrastructure();

        var descriptor = sp.GetRequiredService<ConsumerServer>().ConsumerDescriptors.Single();
        var consumer = await descriptor.BuildConsumerAsync(CancellationToken.None);
        var queueConsumer = (QueueConsumer)consumer;

        Assert.True(queueConsumer.IsInitialized);
        Assert.False(queueConsumer.WasStarted);

        // Act
        await queueConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.False(queueConsumer.IsConsuming);
        channelMock.Verify(it => it.BasicCancelAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region State Properties

    [Fact]
    public async Task StateProperties_BeforeInitialize_ShouldBeAllFalse()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<QueueConsumer>>();
        var descriptorSp = Mock.Of<IServiceProvider>();
        var descriptor = new ConsumerDescriptor(descriptorSp, "test-queue",
            ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        var queueConsumer = new QueueConsumer(loggerMock.Object, descriptor);

        // Assert
        Assert.False(queueConsumer.IsInitialized);
        Assert.False(queueConsumer.WasStarted);
        Assert.False(queueConsumer.IsConsuming);
    }

    [Fact]
    public async Task StateProperties_AfterInitialize_ShouldBeInitializedOnly()
    {
        // Arrange
        var (sp, channelMock, connectionMock) = BuildTestInfrastructure();
        var descriptor = sp.GetRequiredService<ConsumerServer>().ConsumerDescriptors.Single();

        // Act
        var consumer = await descriptor.BuildConsumerAsync(CancellationToken.None);
        var queueConsumer = (QueueConsumer)consumer;

        // Assert
        Assert.True(queueConsumer.IsInitialized);
        Assert.False(queueConsumer.WasStarted);
        Assert.False(queueConsumer.IsConsuming);
    }

    [Fact]
    public async Task StateProperties_AfterStart_ShouldBeAllTrue()
    {
        // Arrange
        var (sp, channelMock, connectionMock) = BuildTestInfrastructure();
        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        // Act
        await consumerServer.StartAsync(CancellationToken.None);
        var queueConsumer = (QueueConsumer)consumerServer.Consumers.Single();

        // Assert
        Assert.True(queueConsumer.IsInitialized);
        Assert.True(queueConsumer.WasStarted);
        Assert.True(queueConsumer.IsConsuming);
    }

    [Fact]
    public async Task StateProperties_AfterStop_WasStartedShouldRemainTrue()
    {
        // Arrange
        var (sp, channelMock, connectionMock) = BuildTestInfrastructure();
        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        await consumerServer.StartAsync(CancellationToken.None);
        var queueConsumer = (QueueConsumer)consumerServer.Consumers.Single();

        // Act
        await queueConsumer.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(queueConsumer.IsInitialized);
        Assert.True(queueConsumer.WasStarted);
        Assert.False(queueConsumer.IsConsuming);
    }

    #endregion

    #region TopologyInitializer

    [Fact]
    public async Task InitializeAsync_WithTopology_ShouldInvokeTopologyInitializer()
    {
        // Arrange
        bool topologyInvoked = false;

        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.IsAny<string>(), false, It.IsAny<string>(), true, false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("tag");
        var channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var sp = services.BuildServiceProvider();

        _ = sp.MapQueue("test-queue", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg))
            .WithTopology((ch, ct) =>
            {
                topologyInvoked = true;
                return Task.CompletedTask;
            });

        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        // Act
        await consumerServer.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(topologyInvoked);
    }

    #endregion

    #region DisposeAsync

    [Fact]
    public async Task DisposeAsync_AfterStart_ShouldCleanup()
    {
        // Arrange
        var (sp, channelMock, connectionMock) = BuildTestInfrastructure();
        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        await consumerServer.StartAsync(CancellationToken.None);
        var queueConsumer = (QueueConsumer)consumerServer.Consumers.Single();
        Assert.True(queueConsumer.WasStarted);
        Assert.True(queueConsumer.IsConsuming);

        // Act
        await queueConsumer.DisposeAsync();

        // Assert - WasStarted remains true (historical flag), BasicCancel was called during dispose
        Assert.True(queueConsumer.WasStarted);
        channelMock.Verify(it => it.BasicCancelAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_NotStarted_ShouldNotThrow()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<QueueConsumer>>();
        var descriptorSp = Mock.Of<IServiceProvider>();
        var descriptor = new ConsumerDescriptor(descriptorSp, "test-queue",
            ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        var queueConsumer = new QueueConsumer(loggerMock.Object, descriptor);

        // Act
        await queueConsumer.DisposeAsync();

        // Assert - state should remain unchanged
        Assert.False(queueConsumer.WasStarted);
        Assert.False(queueConsumer.IsInitialized);
        Assert.False(queueConsumer.IsConsuming);
    }

    #endregion

    #region Message Processing - Nack on exception in result execution

    [Fact]
    public async Task ReceiveAsync_WhenResultExecutionThrows_ShouldNackMessage()
    {
        // Arrange
        string consumerTag = "consumerTag";
        string queueName = "test-queue";

        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        AsyncEventingBasicConsumer queueConsumer = null;

        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.Is<string>(queue => queue == queueName),
            false,
            It.IsAny<string>(),
            true,
            false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .Callback((string queue, bool autoAck, string tag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken) => queueConsumer = (AsyncEventingBasicConsumer)consumer)
            .ReturnsAsync(consumerTag);

        // Ack will throw to test error handling
        _ = channelMock.Setup(it => it.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Ack failed"));
        _ = channelMock.Setup(it => it.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));

        var channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var sp = services.BuildServiceProvider();

        _ = sp.MapQueue(queueName, ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        var hostedService = sp.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);

        SafeRunner.Wait(() => queueConsumer != null);

        ReadOnlyBasicProperties properties = new BasicProperties().ToReadOnly();
        byte[] bytes = Encoding.UTF8.GetBytes("{}");

        // Act
        await queueConsumer.HandleBasicDeliverAsync(
            consumerTag: consumerTag,
            deliveryTag: 1,
            redelivered: false,
            exchange: "e",
            routingKey: "r",
            properties: properties,
            body: new ReadOnlyMemory<byte>(bytes));

        // Assert - Nack should have been called as fallback
        channelMock.Verify(it => it.BasicNackAsync(1, false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Message Processing - Null deserialized message

    [Fact]
    public async Task ReceiveAsync_NullDeserializedMessage_ShouldStillProcess()
    {
        // Arrange
        string consumerTag = "consumerTag";
        string queueName = "test-queue";

        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        AsyncEventingBasicConsumer queueConsumer = null;

        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.Is<string>(queue => queue == queueName),
            false,
            It.IsAny<string>(),
            true,
            false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .Callback((string queue, bool autoAck, string tag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken) => queueConsumer = (AsyncEventingBasicConsumer)consumer)
            .ReturnsAsync(consumerTag);

        _ = channelMock.Setup(it => it.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));
        // Nack should NOT be called, but if handler throws on null it will happen
        _ = channelMock.Setup(it => it.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));

        var channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var sp = services.BuildServiceProvider();

        _ = sp.MapQueue(queueName, ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        var hostedService = sp.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);

        SafeRunner.Wait(() => queueConsumer != null);

        // Send empty body (whitespace) - deserializer should return null for empty body
        ReadOnlyBasicProperties properties = new BasicProperties().ToReadOnly();
        byte[] bytes = Encoding.UTF8.GetBytes("   ");

        // Act - should not throw
        await queueConsumer.HandleBasicDeliverAsync(
            consumerTag: consumerTag,
            deliveryTag: 1,
            redelivered: false,
            exchange: "e",
            routingKey: "r",
            properties: properties,
            body: new ReadOnlyMemory<byte>(bytes));

        // Assert - either Ack or Nack was called (message was processed, not stuck)
        int totalCalls =
            channelMock.Invocations.Count(i => i.Method.Name == "BasicAckAsync") +
            channelMock.Invocations.Count(i => i.Method.Name == "BasicNackAsync") +
            channelMock.Invocations.Count(i => i.Method.Name == "BasicRejectAsync");
        Assert.True(totalCalls >= 1, "Message should have been acknowledged, nacked, or rejected");
    }

    #endregion

    #region WithPrefetch affects BasicQos

    [Fact]
    public async Task InitializeAsync_ShouldCallBasicQosWithPrefetchCount()
    {
        // Arrange
        ushort expectedPrefetch = 42;

        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.IsAny<string>(), false, It.IsAny<string>(), true, false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("tag");
        _ = channelMock.Setup(it => it.BasicQosAsync(0, expectedPrefetch, false, It.IsAny<CancellationToken>()));

        var channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var sp = services.BuildServiceProvider();

        _ = sp.MapQueue("test-queue", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg))
            .WithPrefetch(expectedPrefetch);

        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        // Act
        await consumerServer.StartAsync(CancellationToken.None);

        // Assert
        channelMock.Verify(it => it.BasicQosAsync(0, expectedPrefetch, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
