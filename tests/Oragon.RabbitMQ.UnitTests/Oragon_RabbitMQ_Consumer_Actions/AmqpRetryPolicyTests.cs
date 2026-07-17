using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Abstractions;

public class AmqpRetryPolicyTests
{
    [Fact]
    public void ByDeliveryCount_WhenAttemptIsBelowLimit_ShouldRejectWithRequeue()
    {
        // Arrange
        AmqpRetryPolicy policy = AmqpRetryPolicy.ByDeliveryCount(3);
        IAmqpContext context = BuildContext(1L);

        // Act
        IAmqpResult result = policy.GetResult(context);

        // Assert
        RejectResult reject = Assert.IsType<RejectResult>(result);
        Assert.True(reject.Requeue);
    }

    [Fact]
    public void ByDeliveryCount_WhenAttemptReachesLimit_ShouldNackWithoutRequeue()
    {
        // Arrange
        AmqpRetryPolicy policy = AmqpRetryPolicy.ByDeliveryCount(3);
        IAmqpContext context = BuildContext(2L);

        // Act
        IAmqpResult result = policy.GetResult(context);

        // Assert
        NackResult nack = Assert.IsType<NackResult>(result);
        Assert.False(nack.Requeue);
    }

    [Fact]
    public void ByDeliveryCount_WhenMaxAttemptsIsZero_ShouldThrowArgumentOutOfRangeException()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => AmqpRetryPolicy.ByDeliveryCount(0));
    }

    [Fact]
    public void ByDeliveryCount_WhenRedeliveredWithoutBrokerDeliveryCount_ShouldThrowInvalidOperationException()
    {
        // Arrange
        AmqpRetryPolicy policy = AmqpRetryPolicy.ByDeliveryCount(3);
        IAmqpContext context = BuildContext(null, redelivered: true);

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => policy.GetResult(context));
        Assert.Contains("x-delivery-count", exception.Message, StringComparison.Ordinal);
    }

    private static IAmqpContext BuildContext(long? deliveryCount, bool redelivered = false)
    {
        var propertiesMock = new Mock<IReadOnlyBasicProperties>();
        IDictionary<string, object> headers = deliveryCount.HasValue
            ? new Dictionary<string, object> { ["x-delivery-count"] = deliveryCount.Value }
            : new Dictionary<string, object>();
        _ = propertiesMock.SetupGet(it => it.Headers).Returns(headers);

        var request = new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: 1,
            redelivered: redelivered,
            exchange: string.Empty,
            routingKey: string.Empty,
            properties: propertiesMock.Object,
            body: ReadOnlyMemory<byte>.Empty,
            cancellationToken: CancellationToken.None);

        var contextMock = new Mock<IAmqpContext>();
        _ = contextMock.SetupGet(it => it.Request).Returns(request);

        return contextMock.Object;
    }
}
