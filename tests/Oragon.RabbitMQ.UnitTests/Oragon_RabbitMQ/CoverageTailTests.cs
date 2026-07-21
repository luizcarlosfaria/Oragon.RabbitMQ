// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.Dispatch;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

/// <summary>
/// Grab-bag of small coverage-tail scenarios that did not fit an existing test class:
/// ConsumerServer's fail-fast propagation when a consumer fails to start, the remaining
/// RabbitMQExtensions.AsString branches (plain string / unsupported type), Dispatcher's
/// static-method (no closure target) invocation path, and ConsumerDescriptor.BuildConsumerAsync
/// not locking the descriptor when the connection factory itself throws.
/// </summary>
public class CoverageTailTests
{
    public class TestService
    {
        public Task HandleAsync(TestMessage msg) => Task.CompletedTask;
    }

    public class TestMessage
    {
        public string Value { get; set; }
    }

    private sealed class Message
    {
        public string Data { get; set; }
    }

    private static class StaticHandlerHost
    {
        public static void Handle(Message msg)
        {
            // Intentionally empty: this handler only exists to be dispatched as a static
            // method group, so the compiled invoker takes the handler.Target == null branch.
        }
    }

    #region ConsumerServer - StartAsync fail-fast

    [Fact]
    public async Task StartAsync_WhenConsumerFailsToStart_ShouldPropagateException()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        Mock<IChannel> channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.IsAny<string>(), false, It.IsAny<string>(), true, false,
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        IChannel channel = channelMock.Object;

        Mock<IConnection> connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        IConnection connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        ServiceProvider sp = services.BuildServiceProvider();

        _ = sp.MapQueue("queue-1", ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        ConsumerServer consumerServer = sp.GetRequiredService<ConsumerServer>();

        // Act & Assert - fail-fast: the exception must propagate instead of being swallowed
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => consumerServer.StartAsync(CancellationToken.None));
        Assert.Equal("boom", exception.Message);
    }

    #endregion

    #region RabbitMQExtensions.AsString

    [Fact]
    public void AsString_ValueIsPlainString_ShouldReturnSameString()
    {
        // Arrange
        IDictionary<string, object> headers = new Dictionary<string, object> { ["data"] = "already-a-string" };

        // Act
        string result = headers.AsString("data");

        // Assert
        Assert.Equal("already-a-string", result);
    }

    [Fact]
    public void AsString_ValueIsUnsupportedType_ShouldReturnNull()
    {
        // Arrange
        IDictionary<string, object> headers = new Dictionary<string, object> { ["data"] = 42 };

        // Act
        string result = headers.AsString("data");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Dispatcher - static method handler

    [Fact]
    public async Task Dispatch_StaticMethodHandler_ShouldInvokeAndAck()
    {
        // Arrange
        Action<Message> handler = StaticHandlerHost.Handle;
        Assert.Null(handler.Target); // sanity check: static method group has no closure target

        Mock<IServiceProvider> serviceProviderMock = new Mock<IServiceProvider>();
        Mock<IAmqpContext> contextMock = new Mock<IAmqpContext>();
        _ = contextMock.Setup(it => it.MessageObject).Returns(new Message { Data = "oragon-rabbitmq-data" });

        ConsumerDescriptor consumerDescriptor = new ConsumerDescriptor(serviceProviderMock.Object, "oragon-rabbitmq-queueName", handler);
        Dispatcher dispatcher = new Dispatcher(consumerDescriptor);

        // Act
        IAmqpResult result = await dispatcher.DispatchAsync(contextMock.Object);

        // Assert
        _ = Assert.IsType<AckResult>(result);
    }

    #endregion

    #region ConsumerDescriptor - BuildConsumerAsync when ConnectionFactory throws

    [Fact]
    public async Task BuildConsumerAsync_WhenConnectionFactoryThrows_ShouldNotLockDescriptor()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddRabbitMQConsumer();
        _ = services.AddLogging();
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        ServiceProvider sp = services.BuildServiceProvider();

        ConsumerDescriptor descriptor = new ConsumerDescriptor(
            sp,
            "test-queue",
            ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        _ = descriptor.WithConnection((_, _) => throw new InvalidOperationException("connection factory failed"));

        // Act
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => descriptor.BuildConsumerAsync(CancellationToken.None));

        // Assert
        Assert.Equal("connection factory failed", exception.Message);
        _ = descriptor.WithPrefetch(7);
        Assert.Equal((ushort)7, descriptor.PrefetchCount);
    }

    #endregion
}
