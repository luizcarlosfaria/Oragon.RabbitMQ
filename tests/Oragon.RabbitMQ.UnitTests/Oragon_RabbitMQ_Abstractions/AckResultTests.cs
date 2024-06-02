using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.Consumer.Actions.Tests
{
    public class AckResultTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldAcknowledgeMessage()
        {
            // Arrange
            var channelMock = new Mock<IChannel>();
            var deliveryArgs = new BasicDeliverEventArgs(
                consumerTag: string.Empty,
                deliveryTag: 1,
                redelivered: false,
                exchange: string.Empty,
                routingKey: string.Empty,
                properties: default,
                body: default);

            var ackResult = new AckResult();

            // Act
            await ackResult.ExecuteAsync(channelMock.Object, deliveryArgs);

            // Assert
            channelMock.Verify(c => c.BasicAckAsync(deliveryArgs.DeliveryTag, false, default), Times.Once);
        }
    }
}
