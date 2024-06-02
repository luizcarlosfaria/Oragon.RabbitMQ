using System.Threading.Tasks;
using Dawn;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Oragon.RabbitMQ.Consumer.Actions.Tests
{
    public class NackResultTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldCallBasicNackAsync_WithCorrectArguments()
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

            var nackResult = new NackResult(true);

            // Act
            await nackResult.ExecuteAsync(channelMock.Object, deliveryArgs);

            // Assert
            channelMock.Verify(c => c.BasicNackAsync(deliveryArgs.DeliveryTag, false, nackResult.Requeue, default), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldThrowArgumentNullException_WhenChannelIsNull()
        {
            // Arrange
            IChannel channel = new Mock<IChannel>().Object;

            var deliveryArgs = new BasicDeliverEventArgs(
                consumerTag: string.Empty,
                deliveryTag: 9,
                redelivered: false,
                exchange: string.Empty,
                routingKey: string.Empty,
                properties: default,
                body: default);
            var nackResult = new NackResult(true);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => nackResult.ExecuteAsync(null, deliveryArgs));

            await Assert.ThrowsAsync<ArgumentNullException>(() => nackResult.ExecuteAsync(channel, null));
        }

        [Fact]
        public async Task ExecuteAsync_ShouldThrowArgumentNullException_WhenDeliveryIsNull()
        {
            // Arrange
            IChannel channel = new Mock<IChannel>().Object;
            BasicDeliverEventArgs delivery = null;
            var nackResult = new NackResult(true);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => nackResult.ExecuteAsync(channel, delivery));
        }
    }
}
