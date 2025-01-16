using Moq;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Oragon.RabbitMQ.Serialization;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Consumer_Actions;

public class DelayedResultTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldPublishMessageWithTTLAndAcknowledgeOriginalMessageAsync()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        var contextMock = new Mock<IAmqpContext>();
        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        var amqpSerializerMock = new Mock<IAMQPSerializer>();

        var originalMessageId = Guid.NewGuid().ToString("D");
        var delayedQueueName = "test-queue-delayed";
        var ttl = TimeSpan.FromSeconds(5);
        var message = new { Text = "Test Message" };

        basicPropertiesMock.SetupGet(it => it.MessageId).Returns(originalMessageId);

        var basicDeliverEventArgs = new BasicDeliverEventArgs(
            consumerTag: Guid.NewGuid().ToString(),
            deliveryTag: 2,
            redelivered: false,
            exchange: Guid.NewGuid().ToString(),
            routingKey: Guid.NewGuid().ToString(),
            properties: basicPropertiesMock.Object,
            body: null,
            cancellationToken: default);

        contextMock.Setup(it => it.Channel).Returns(channelMock.Object);
        contextMock.Setup(it => it.Request).Returns(basicDeliverEventArgs);
        contextMock.Setup(it => it.QueueName).Returns("test-queue");
        contextMock.Setup(it => it.Serializer).Returns(amqpSerializerMock.Object);

        amqpSerializerMock.Setup(it => it.Serialize(It.IsAny<BasicProperties>(), It.Is<object>(dto => dto == message)))
            .Returns(new byte[] { 1, 2, 3 });

        var delayedResult = new DelayedResult(message, ttl);

        // Act
        await delayedResult.ExecuteAsync(contextMock.Object);

        // Assert
        channelMock.Verify(c => c.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: delayedQueueName,
            mandatory: true,
            basicProperties: It.Is<BasicProperties>(bp => bp.Expiration == ((int)ttl.TotalMilliseconds).ToString() && bp.MessageId != null && bp.CorrelationId == originalMessageId),
            body: It.IsAny<ReadOnlyMemory<byte>>(),
            cancellationToken: default), Times.Once);

        channelMock.Verify(c => c.BasicAckAsync(basicDeliverEventArgs.DeliveryTag, false, default), Times.Once);
    }
}
