using Moq;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Oragon.RabbitMQ.Serialization;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;


namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Consumer_ArgumentBinders;
public class DynamicArgumentBinderTests
{

    [Fact]
    public void DynamicArgumentBinderFlow()
    {
        string originalMessageId = Guid.NewGuid().ToString("D");
        var channelMock = new Mock<IChannel>();

        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        basicPropertiesMock.SetupGet(it => it.MessageId).Returns(originalMessageId).Verifiable(Times.Once());

        var amqpSerializer = new Mock<IAMQPSerializer>();

        var basicDeliverEventArgs = new BasicDeliverEventArgs(
                consumerTag: Guid.NewGuid().ToString(),
                deliveryTag: 2,
                redelivered: false,
                exchange: Guid.NewGuid().ToString(),
                routingKey: Guid.NewGuid().ToString(),
                properties: basicPropertiesMock.Object,
                body: null,
                cancellationToken: default);

        var contextMock = new Mock<IAmqpContext>();
        contextMock.Setup(it => it.Channel).Returns(channelMock.Object).Verifiable(Times.Once());
        contextMock.Setup(it => it.Request).Returns(basicDeliverEventArgs).Verifiable(Times.Exactly(2));
        contextMock.Setup(it => it.Serializer).Returns(amqpSerializer.Object).Verifiable(Times.Once());

        // Act
        object result = new DynamicArgumentBinder((context) => context.Request.BasicProperties.MessageId).GetValue(contextMock.Object);

        // Assert
        Assert.Equal(originalMessageId, result);
    }

}
