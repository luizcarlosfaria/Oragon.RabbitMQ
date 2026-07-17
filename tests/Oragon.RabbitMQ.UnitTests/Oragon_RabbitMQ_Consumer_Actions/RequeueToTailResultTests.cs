using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Abstractions;

public class RequeueToTailResultTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldPublishOnDedicatedChannelWithoutAckingOriginalDelivery()
    {
        // Arrange
        const string targetQueue = "target-queue";
        byte[] body = [1, 2, 3];
        var published = false;
        BasicProperties capturedProperties = null;

        var publishChannelMock = new Mock<IChannel>();
        publishChannelMock.Setup(c => c.BasicPublishAsync(
                string.Empty,
                targetQueue,
                false,
                It.IsAny<BasicProperties>(),
                It.Is<ReadOnlyMemory<byte>>(memory => memory.ToArray().SequenceEqual(body)),
                It.IsAny<CancellationToken>()))
            .Callback((string exchange, string routingKey, bool mandatory, BasicProperties properties, ReadOnlyMemory<byte> messageBody, CancellationToken cancellationToken) =>
            {
                published = true;
                capturedProperties = properties;
            })
            .Returns(new ValueTask())
            .Verifiable(Times.Once());
        publishChannelMock.Setup(c => c.CloseAsync(
                It.IsAny<ushort>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        var consumerChannelMock = new Mock<IChannel>();

        var connectionMock = new Mock<IConnection>();
        connectionMock.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(publishChannelMock.Object)
            .Verifiable(Times.Once());

        var headers = new Dictionary<string, object>
        {
            ["x-custom"] = "keep",
            ["x-death"] = "keep",
            ["x-delivery-count"] = 4L,
            ["x-first-death-queue"] = "keep",
            ["x-last-death-reason"] = "keep",
        };

        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        _ = basicPropertiesMock.SetupGet(it => it.ContentType).Returns("application/json");
        _ = basicPropertiesMock.SetupGet(it => it.ContentEncoding).Returns("utf-8");
        _ = basicPropertiesMock.SetupGet(it => it.Headers).Returns(headers);
        _ = basicPropertiesMock.SetupGet(it => it.DeliveryMode).Returns(DeliveryModes.Persistent);
        _ = basicPropertiesMock.SetupGet(it => it.Priority).Returns((byte)7);
        _ = basicPropertiesMock.SetupGet(it => it.CorrelationId).Returns("correlation-1");
        _ = basicPropertiesMock.SetupGet(it => it.ReplyTo).Returns("reply-queue");
        _ = basicPropertiesMock.SetupGet(it => it.Expiration).Returns("30000");
        _ = basicPropertiesMock.SetupGet(it => it.MessageId).Returns("message-1");
        _ = basicPropertiesMock.SetupGet(it => it.Timestamp).Returns(new AmqpTimestamp(1234567890));
        _ = basicPropertiesMock.SetupGet(it => it.Type).Returns("message-type");
        _ = basicPropertiesMock.SetupGet(it => it.UserId).Returns("user-1");
        _ = basicPropertiesMock.SetupGet(it => it.AppId).Returns("app-1");
        _ = basicPropertiesMock.SetupGet(it => it.ClusterId).Returns("cluster-1");

        var request = new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: 10,
            redelivered: false,
            exchange: string.Empty,
            routingKey: targetQueue,
            properties: basicPropertiesMock.Object,
            body: new ReadOnlyMemory<byte>(body),
            cancellationToken: CancellationToken.None);

        var contextMock = new Mock<IAmqpContext>();
        _ = contextMock.SetupGet(it => it.Connection).Returns(connectionMock.Object);
        _ = contextMock.SetupGet(it => it.Channel).Returns(consumerChannelMock.Object);
        _ = contextMock.SetupGet(it => it.Request).Returns(request);
        _ = contextMock.SetupGet(it => it.CancellationToken).Returns(CancellationToken.None);

        RequeueToTailResult result = AmqpResults.RequeueToTail(targetQueue);

        // Act
        await result.ExecuteAsync(contextMock.Object);

        // Assert
        Assert.True(published);
        Assert.NotNull(capturedProperties);
        Assert.Equal("application/json", capturedProperties.ContentType);
        Assert.Equal("utf-8", capturedProperties.ContentEncoding);
        Assert.Equal(DeliveryModes.Persistent, capturedProperties.DeliveryMode);
        Assert.Equal((byte)7, capturedProperties.Priority);
        Assert.Equal("correlation-1", capturedProperties.CorrelationId);
        Assert.Equal("reply-queue", capturedProperties.ReplyTo);
        Assert.Equal("30000", capturedProperties.Expiration);
        Assert.Equal("message-1", capturedProperties.MessageId);
        Assert.Equal(1234567890, capturedProperties.Timestamp.UnixTime);
        Assert.Equal("message-type", capturedProperties.Type);
        Assert.Null(capturedProperties.UserId);
        Assert.Equal("app-1", capturedProperties.AppId);
        Assert.Equal("cluster-1", capturedProperties.ClusterId);
        Assert.True(capturedProperties.Headers.ContainsKey("x-custom"));
        Assert.True(capturedProperties.Headers.ContainsKey("x-death"));
        Assert.False(capturedProperties.Headers.ContainsKey("x-delivery-count"));
        Assert.True(capturedProperties.Headers.ContainsKey("x-first-death-queue"));
        Assert.True(capturedProperties.Headers.ContainsKey("x-last-death-reason"));
        consumerChannelMock.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        publishChannelMock.VerifyAll();
        connectionMock.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_WithAllApplicationProperties_ShouldCopyAllApplicationVisiblePropertyGroups()
    {
        // Arrange
        BasicProperties capturedProperties = null;

        var publishChannelMock = new Mock<IChannel>();
        publishChannelMock.Setup(c => c.BasicPublishAsync(
                string.Empty,
                "target-queue",
                false,
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string exchange, string routingKey, bool mandatory, BasicProperties properties, ReadOnlyMemory<byte> messageBody, CancellationToken cancellationToken) =>
            {
                capturedProperties = properties;
            })
            .Returns(new ValueTask());
        publishChannelMock.Setup(c => c.CloseAsync(
                It.IsAny<ushort>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var consumerChannelMock = new Mock<IChannel>();

        var connectionMock = new Mock<IConnection>();
        connectionMock.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(publishChannelMock.Object);

        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        _ = basicPropertiesMock.SetupGet(it => it.Expiration).Returns("30000");
        _ = basicPropertiesMock.SetupGet(it => it.MessageId).Returns("message-1");
        _ = basicPropertiesMock.SetupGet(it => it.Timestamp).Returns(new AmqpTimestamp(1234567890));
        _ = basicPropertiesMock.SetupGet(it => it.UserId).Returns("user-1");
        _ = basicPropertiesMock.SetupGet(it => it.AppId).Returns("app-1");
        _ = basicPropertiesMock.SetupGet(it => it.ClusterId).Returns("cluster-1");

        var request = new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: 10,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "target-queue",
            properties: basicPropertiesMock.Object,
            body: ReadOnlyMemory<byte>.Empty,
            cancellationToken: CancellationToken.None);

        var contextMock = new Mock<IAmqpContext>();
        _ = contextMock.SetupGet(it => it.Connection).Returns(connectionMock.Object);
        _ = contextMock.SetupGet(it => it.Channel).Returns(consumerChannelMock.Object);
        _ = contextMock.SetupGet(it => it.Request).Returns(request);
        _ = contextMock.SetupGet(it => it.CancellationToken).Returns(CancellationToken.None);

        RequeueToTailResult result = AmqpResults.RequeueToTail(
            "target-queue",
            options => options.CopyProperties = AmqpPropertyCopy.AllApplicationProperties);

        // Act
        await result.ExecuteAsync(contextMock.Object);

        // Assert
        Assert.NotNull(capturedProperties);
        Assert.Equal("30000", capturedProperties.Expiration);
        Assert.Equal("message-1", capturedProperties.MessageId);
        Assert.Equal(1234567890, capturedProperties.Timestamp.UnixTime);
        Assert.Equal("user-1", capturedProperties.UserId);
        Assert.Equal("app-1", capturedProperties.AppId);
        Assert.Equal("cluster-1", capturedProperties.ClusterId);
        consumerChannelMock.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithCopyOptions_ShouldCopyOnlySelectedPropertyGroups()
    {
        // Arrange
        BasicProperties capturedProperties = null;

        var publishChannelMock = new Mock<IChannel>();
        publishChannelMock.Setup(c => c.BasicPublishAsync(
                string.Empty,
                "target-queue",
                false,
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string exchange, string routingKey, bool mandatory, BasicProperties properties, ReadOnlyMemory<byte> messageBody, CancellationToken cancellationToken) =>
            {
                capturedProperties = properties;
            })
            .Returns(new ValueTask());
        publishChannelMock.Setup(c => c.CloseAsync(
                It.IsAny<ushort>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var consumerChannelMock = new Mock<IChannel>();

        var connectionMock = new Mock<IConnection>();
        connectionMock.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(publishChannelMock.Object);

        var headers = new Dictionary<string, object>
        {
            ["x-custom"] = "keep",
            ["x-death"] = "keep",
            ["x-delivery-count"] = 4L,
        };

        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        _ = basicPropertiesMock.SetupGet(it => it.ContentType).Returns("application/json");
        _ = basicPropertiesMock.SetupGet(it => it.Headers).Returns(headers);
        _ = basicPropertiesMock.SetupGet(it => it.Priority).Returns((byte)7);
        _ = basicPropertiesMock.SetupGet(it => it.MessageId).Returns("message-1");

        var request = new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: 10,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "target-queue",
            properties: basicPropertiesMock.Object,
            body: ReadOnlyMemory<byte>.Empty,
            cancellationToken: CancellationToken.None);

        var contextMock = new Mock<IAmqpContext>();
        _ = contextMock.SetupGet(it => it.Connection).Returns(connectionMock.Object);
        _ = contextMock.SetupGet(it => it.Channel).Returns(consumerChannelMock.Object);
        _ = contextMock.SetupGet(it => it.Request).Returns(request);
        _ = contextMock.SetupGet(it => it.CancellationToken).Returns(CancellationToken.None);

        RequeueToTailResult result = AmqpResults.RequeueToTail(
            "target-queue",
            options =>
            {
                options.CopyProperties = AmqpPropertyCopy.Priority | AmqpPropertyCopy.Headers;
                options.ConfigureProperties = (_, properties) => properties.AppId = "configured-app";
            });

        // Act
        await result.ExecuteAsync(contextMock.Object);

        // Assert
        Assert.NotNull(capturedProperties);
        Assert.Null(capturedProperties.ContentType);
        Assert.Null(capturedProperties.MessageId);
        Assert.Equal((byte)7, capturedProperties.Priority);
        Assert.Equal("configured-app", capturedProperties.AppId);
        Assert.True(capturedProperties.Headers.ContainsKey("x-custom"));
        Assert.True(capturedProperties.Headers.ContainsKey("x-death"));
        Assert.False(capturedProperties.Headers.ContainsKey("x-delivery-count"));
        consumerChannelMock.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithExplicitUserIdFlag_ShouldCopyUserId()
    {
        // Arrange
        BasicProperties capturedProperties = null;

        var publishChannelMock = new Mock<IChannel>();
        publishChannelMock.Setup(c => c.BasicPublishAsync(
                string.Empty,
                "target-queue",
                false,
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string exchange, string routingKey, bool mandatory, BasicProperties properties, ReadOnlyMemory<byte> messageBody, CancellationToken cancellationToken) =>
            {
                capturedProperties = properties;
            })
            .Returns(new ValueTask());
        publishChannelMock.Setup(c => c.CloseAsync(
                It.IsAny<ushort>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var consumerChannelMock = new Mock<IChannel>();

        var connectionMock = new Mock<IConnection>();
        connectionMock.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(publishChannelMock.Object);

        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        _ = basicPropertiesMock.SetupGet(it => it.UserId).Returns("user-1");
        _ = basicPropertiesMock.SetupGet(it => it.AppId).Returns("app-1");

        var request = new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: 10,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "target-queue",
            properties: basicPropertiesMock.Object,
            body: ReadOnlyMemory<byte>.Empty,
            cancellationToken: CancellationToken.None);

        var contextMock = new Mock<IAmqpContext>();
        _ = contextMock.SetupGet(it => it.Connection).Returns(connectionMock.Object);
        _ = contextMock.SetupGet(it => it.Channel).Returns(consumerChannelMock.Object);
        _ = contextMock.SetupGet(it => it.Request).Returns(request);
        _ = contextMock.SetupGet(it => it.CancellationToken).Returns(CancellationToken.None);

        RequeueToTailResult result = AmqpResults.RequeueToTail(
            "target-queue",
            options => options.CopyProperties = AmqpPropertyCopy.UserId);

        // Act
        await result.ExecuteAsync(contextMock.Object);

        // Assert
        Assert.NotNull(capturedProperties);
        Assert.Equal("user-1", capturedProperties.UserId);
        Assert.Null(capturedProperties.AppId);
    }
}
