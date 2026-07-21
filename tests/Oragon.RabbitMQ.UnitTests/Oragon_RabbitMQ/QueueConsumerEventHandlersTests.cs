using Microsoft.Extensions.DependencyInjection;
using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class QueueConsumerEventHandlersTests
{
    public class TestService
    {
        public Task HandleAsync(TestMessage msg) => Task.CompletedTask;
    }

    public class TestMessage
    {
        public string Value { get; set; }
    }

    private static async Task<(ServiceProvider sp, Mock<IChannel> channelMock, Mock<IConnection> connectionMock, AsyncEventingBasicConsumer consumer)> BuildStartedConsumerAsync(string queueName = "test-queue")
    {
        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        AsyncEventingBasicConsumer capturedConsumer = null;

        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.IsAny<string>(), false, It.IsAny<string>(), true, false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .Callback((string queue, bool autoAck, string tag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken) => capturedConsumer = (AsyncEventingBasicConsumer)consumer)
            .ReturnsAsync("consumerTag");
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

        ConsumerServer consumerServer = sp.GetRequiredService<ConsumerServer>();
        await consumerServer.StartAsync(CancellationToken.None);

        return (sp, channelMock, connectionMock, capturedConsumer);
    }

    #region Consumer channel shutdown (AsyncEventingBasicConsumer.HandleChannelShutdownAsync)

    [Fact]
    public async Task HandleChannelShutdownAsync_WhenChannelIsShutdown_ShouldSetIsConsumingFalse()
    {
        // Arrange
        (ServiceProvider sp, Mock<IChannel> channelMock, Mock<IConnection> connectionMock, AsyncEventingBasicConsumer capturedConsumer) = await BuildStartedConsumerAsync();
        var queueConsumer = (QueueConsumer)sp.GetRequiredService<ConsumerServer>().Consumers.Single();
        Assert.True(queueConsumer.IsConsuming);

        // Act
        await capturedConsumer.HandleChannelShutdownAsync(this, new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED"));

        // Assert
        Assert.False(queueConsumer.IsConsuming);
    }

    #endregion

    #region Connection shutdown (raised directly on the IConnection mock)

    [Fact]
    public async Task ConnectionShutdownAsync_WhenConnectionIsShutdown_ShouldSetIsConsumingFalse()
    {
        // Arrange
        (ServiceProvider sp, Mock<IChannel> channelMock, Mock<IConnection> connectionMock, AsyncEventingBasicConsumer capturedConsumer) = await BuildStartedConsumerAsync();
        var queueConsumer = (QueueConsumer)sp.GetRequiredService<ConsumerServer>().Consumers.Single();
        Assert.True(queueConsumer.IsConsuming);

        var shutdownArgs = new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED");

        // Act
        connectionMock.Raise(c => c.ConnectionShutdownAsync += null, this, shutdownArgs);

        // Assert
        Assert.False(queueConsumer.IsConsuming);
    }

    [Fact]
    public async Task ConnectionShutdownAsync_WhenEventArgsIsNull_ShouldFallBackToDefaultsAndNotThrow()
    {
        // Arrange - the handler defensively guards every property behind `eventArgs?.` / `??`;
        // raising with a null ShutdownEventArgs exercises those fallback branches directly,
        // bypassing AsyncEventingBasicConsumer's own (non-null-safe) base implementation.
        (ServiceProvider sp, Mock<IChannel> channelMock, Mock<IConnection> connectionMock, AsyncEventingBasicConsumer capturedConsumer) = await BuildStartedConsumerAsync();
        var queueConsumer = (QueueConsumer)sp.GetRequiredService<ConsumerServer>().Consumers.Single();
        Assert.True(queueConsumer.IsConsuming);

        // Act & Assert - must not throw despite the null event args
        connectionMock.Raise(c => c.ConnectionShutdownAsync += null, this, (ShutdownEventArgs)null);

        Assert.False(queueConsumer.IsConsuming);
    }

    #endregion

    #region Connection blocked / unblocked (raised directly on the IConnection mock)

    [Fact]
    public async Task ConnectionBlockedAsync_WhenConnectionIsBlocked_ShouldNotThrow()
    {
        // Arrange
        (ServiceProvider sp, Mock<IChannel> channelMock, Mock<IConnection> connectionMock, AsyncEventingBasicConsumer capturedConsumer) = await BuildStartedConsumerAsync();
        var queueConsumer = (QueueConsumer)sp.GetRequiredService<ConsumerServer>().Consumers.Single();

        var blockedArgs = new ConnectionBlockedEventArgs("memory alarm");

        // Act & Assert - only logs, must not throw and must not affect consuming state
        connectionMock.Raise(c => c.ConnectionBlockedAsync += null, this, blockedArgs);

        Assert.True(queueConsumer.IsConsuming);
    }

    [Fact]
    public async Task ConnectionUnblockedAsync_WhenConnectionIsUnblocked_ShouldNotThrow()
    {
        // Arrange
        (ServiceProvider sp, Mock<IChannel> channelMock, Mock<IConnection> connectionMock, AsyncEventingBasicConsumer capturedConsumer) = await BuildStartedConsumerAsync();
        var queueConsumer = (QueueConsumer)sp.GetRequiredService<ConsumerServer>().Consumers.Single();

        // Act & Assert - only logs, must not throw and must not affect consuming state
        connectionMock.Raise(c => c.ConnectionUnblockedAsync += null, this, AsyncEventArgs.Empty);

        Assert.True(queueConsumer.IsConsuming);
    }

    #endregion
}
