using Moq;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Abstractions;

public class NackResultTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCallBasicNackAsync_WithCorrectArguments()
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

        var nackResult = AmqpResults.Nack(true);

        // Act
        await nackResult.ExecuteAsync(contextMock.Object);

        // Assert
        channelMock.Verify(c => c.BasicNackAsync(basicDeliverEventArgs.DeliveryTag, false, nackResult.Requeue, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var channel = new Mock<IChannel>().Object;

        var deliveryArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 9,
            redelivered: false,
            exchange: string.Empty,
            routingKey: string.Empty,
            properties: default,
            body: default);
        var nackResult = AmqpResults.Nack(true);

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => nackResult.ExecuteAsync(null));
    }

}
