using System.Threading.Tasks;
using Dawn;
using Moq;
using Oragon.RabbitMQ.Consumer.Actions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Abstractions;

public class RejectResultTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCallBasicRejectAsync()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        var deliveryArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 9,
            redelivered: false,
            exchange: string.Empty,
            routingKey: string.Empty,
            properties: default,
            body: default);

        var rejectResult = new RejectResult(true);

        // Act
        await rejectResult.ExecuteAsync(channelMock.Object, deliveryArgs);

        // Assert
        channelMock.Verify(c => c.BasicRejectAsync(deliveryArgs.DeliveryTag, rejectResult.Requeue, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotNullCheckChannelAndDelivery()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        var deliveryArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 9,
            redelivered: false,
            exchange: string.Empty,
            routingKey: string.Empty,
            properties: default,
            body: default);


        var rejectResult = new RejectResult(true);

        // Act
        await rejectResult.ExecuteAsync(channelMock.Object, deliveryArgs);

        // Assert
        Guard.Argument(channelMock.Object).NotNull();
        Guard.Argument(deliveryArgs).NotNull();
    }
}
