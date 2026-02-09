using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class ConsumerServerExtendedTests
{
    public class TestService
    {
        public Task HandleAsync(TestMessage msg) => Task.CompletedTask;
    }

    public class TestMessage
    {
        public string Value { get; set; }
    }

    #region AddConsumerDescriptor

    [Fact]
    public async Task AddConsumerDescriptor_WhenReadOnly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        string queueName = "test-queue";

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

        _ = sp.MapQueue(queueName, ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        // Act - Start makes it read-only
        await consumerServer.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(consumerServer.IsReadOnly);
        var newDescriptor = new ConsumerDescriptor(sp, "another-queue", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));
        _ = Assert.Throws<InvalidOperationException>(() => consumerServer.AddConsumerDescriptor(newDescriptor));
    }

    [Fact]
    public async Task AddConsumerDescriptor_Multiple_ShouldAddAll()
    {
        // Arrange
        await using ConsumerServer consumerServer = new ConsumerServer(Mock.Of<ILogger<ConsumerServer>>());
        var sp = Mock.Of<IServiceProvider>();

        var descriptor1 = new ConsumerDescriptor(sp, "queue-1", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));
        var descriptor2 = new ConsumerDescriptor(sp, "queue-2", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));
        var descriptor3 = new ConsumerDescriptor(sp, "queue-3", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        // Act
        consumerServer.AddConsumerDescriptor(descriptor1);
        consumerServer.AddConsumerDescriptor(descriptor2);
        consumerServer.AddConsumerDescriptor(descriptor3);

        // Assert
        Assert.Equal(3, consumerServer.ConsumerDescriptors.Count());
    }

    #endregion

    #region IsReadOnly State Transitions

    [Fact]
    public async Task IsReadOnly_InitialState_ShouldBeFalse()
    {
        // Arrange & Act
        await using ConsumerServer consumerServer = new ConsumerServer(Mock.Of<ILogger<ConsumerServer>>());

        // Assert
        Assert.False(consumerServer.IsReadOnly);
    }

    [Fact]
    public async Task IsReadOnly_AfterStart_ShouldBeTrue()
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
        var channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var sp = services.BuildServiceProvider();

        _ = sp.MapQueue("test-queue", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        // Act
        await consumerServer.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(consumerServer.IsReadOnly);
    }

    [Fact]
    public async Task IsReadOnly_AfterStartThenStop_ShouldBeFalse()
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
        var channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var sp = services.BuildServiceProvider();

        _ = sp.MapQueue("test-queue", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        // Act
        await consumerServer.StartAsync(CancellationToken.None);
        await consumerServer.StopAsync(CancellationToken.None);

        // Assert
        Assert.False(consumerServer.IsReadOnly);
    }

    #endregion

    #region StartAsync

    [Fact]
    public async Task StartAsync_WithNoDescriptors_ShouldSucceed()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddRabbitMQConsumer();
        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());

        var sp = services.BuildServiceProvider();
        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        // Act & Assert - should not throw
        await consumerServer.StartAsync(CancellationToken.None);

        Assert.True(consumerServer.IsReadOnly);
        Assert.Empty(consumerServer.Consumers);
    }

    [Fact]
    public async Task StartAsync_ShouldBuildAndStartAllConsumers()
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
        var channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var sp = services.BuildServiceProvider();

        _ = sp.MapQueue("queue-1", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));
        _ = sp.MapQueue("queue-2", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        // Act
        await consumerServer.StartAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, consumerServer.Consumers.Count());
        Assert.All(consumerServer.Consumers, consumer =>
        {
            var qc = Assert.IsType<QueueConsumer>(consumer);
            Assert.True(qc.WasStarted);
            Assert.True(qc.IsConsuming);
        });
    }

    #endregion

    #region StopAsync

    [Fact]
    public async Task StopAsync_ShouldCallBasicCancelOnAllConsumers()
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
        _ = channelMock.Setup(it => it.BasicCancelAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));
        var channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var sp = services.BuildServiceProvider();

        _ = sp.MapQueue("queue-1", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        await consumerServer.StartAsync(CancellationToken.None);

        // Act
        await consumerServer.StopAsync(CancellationToken.None);

        // Assert
        channelMock.Verify(it => it.BasicCancelAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DisposeAsync

    [Fact]
    public async Task DisposeAsync_ShouldDisposeAllConsumers()
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
        var channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var sp = services.BuildServiceProvider();

        _ = sp.MapQueue("queue-1", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        await consumerServer.StartAsync(CancellationToken.None);

        // Act
        await consumerServer.DisposeAsync();

        // Assert - consumers list should be cleared
        Assert.Empty(consumerServer.Consumers);
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldNotThrow()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddRabbitMQConsumer();
        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());

        var sp = services.BuildServiceProvider();
        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        await consumerServer.StartAsync(CancellationToken.None);

        // Act & Assert - second dispose should not throw
        await consumerServer.DisposeAsync();
        await consumerServer.DisposeAsync();
    }

    #endregion

    #region Consumers and ConsumerDescriptors properties

    [Fact]
    public async Task Consumers_ShouldReturnCopyOfList()
    {
        // Arrange
        await using ConsumerServer consumerServer = new ConsumerServer(Mock.Of<ILogger<ConsumerServer>>());

        // Act
        var consumers1 = consumerServer.Consumers;
        var consumers2 = consumerServer.Consumers;

        // Assert - should return different list instances (defensive copy)
        Assert.NotSame(consumers1, consumers2);
    }

    [Fact]
    public async Task ConsumerDescriptors_ShouldReturnCopyOfList()
    {
        // Arrange
        await using ConsumerServer consumerServer = new ConsumerServer(Mock.Of<ILogger<ConsumerServer>>());

        // Act
        var descriptors1 = consumerServer.ConsumerDescriptors;
        var descriptors2 = consumerServer.ConsumerDescriptors;

        // Assert - should return different list instances (defensive copy)
        Assert.NotSame(descriptors1, descriptors2);
    }

    #endregion

    #region Full Lifecycle

    [Fact]
    public async Task FullLifecycle_StartStopDispose_ShouldWorkCorrectly()
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
        var channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var sp = services.BuildServiceProvider();

        _ = sp.MapQueue("queue-1", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        var consumerServer = sp.GetRequiredService<ConsumerServer>();

        // Act & Assert - full lifecycle
        Assert.False(consumerServer.IsReadOnly);
        Assert.Empty(consumerServer.Consumers);

        await consumerServer.StartAsync(CancellationToken.None);
        Assert.True(consumerServer.IsReadOnly);
        _ = Assert.Single(consumerServer.Consumers);

        await consumerServer.StopAsync(CancellationToken.None);
        Assert.False(consumerServer.IsReadOnly);

        await consumerServer.DisposeAsync();
        Assert.Empty(consumerServer.Consumers);
    }

    #endregion
}
