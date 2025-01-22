using Moq;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Abstractions;

public class AckResultTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldAcknowledgeMessage()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();

        var contextMock = new Mock<IAmqpContext>();
        _ = contextMock.Setup(it => it.Channel).Returns(channelMock.Object);

        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();

        var basicDeliverEventArgs = new BasicDeliverEventArgs(
                consumerTag: Guid.NewGuid().ToString(),
                deliveryTag: 2,
                redelivered: false,
                exchange: Guid.NewGuid().ToString(),
                routingKey: Guid.NewGuid().ToString(),
                properties: basicPropertiesMock.Object,
                body: null,
                cancellationToken: default);

        _ = contextMock.Setup(it => it.Request).Returns(basicDeliverEventArgs);

        var ackResult = AmqpResults.Ack();

        // Act
        await ackResult.ExecuteAsync(contextMock.Object);

        // Assert
        channelMock.Verify(c => c.BasicAckAsync(basicDeliverEventArgs.DeliveryTag, false, default), Times.Once);
    }
}
