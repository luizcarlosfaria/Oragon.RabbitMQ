using Moq;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Oragon.RabbitMQ.Serialization;


namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Abstractions;

public class ReplyResultTests
{

    public class ResponseDTO
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    [Fact]
    public async Task ExecuteAsync_MustCallBasiPublishWithCorrelationId()
    {
        // Arrange
        string originalMessageId = Guid.NewGuid().ToString("D");
        string replyTo = Guid.NewGuid().ToString("D");
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.BasicPublishAsync(It.IsAny<string>(), It.Is<string>(s => s == replyTo), It.IsAny<bool>(), It.Is<BasicProperties>(bp => bp.CorrelationId == originalMessageId), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>())).Returns(new ValueTask()).Verifiable(Times.Once());

        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        basicPropertiesMock.SetupGet(it => it.MessageId).Returns(originalMessageId).Verifiable(Times.Once());
        basicPropertiesMock.SetupGet(it => it.ReplyTo).Returns(replyTo).Verifiable(Times.Once());

        var amqpSerializer = new Mock<IAmqpSerializer>();
        amqpSerializer.Setup(it => it.Serialize(It.IsAny<BasicProperties>(), It.Is<ResponseDTO>(dto => dto.Name == "Text1" && dto.Age == 4))).Returns(new byte[] { 1, 2, 3 }).Verifiable(Times.Once());

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

        var result = AmqpResults.Reply(new ResponseDTO() { Name = "Text1", Age = 4 });

        // Act
        await result.ExecuteAsync(contextMock.Object);

        // Assert
        channelMock.VerifyAll();
        basicPropertiesMock.VerifyAll();
        amqpSerializer.VerifyAll();
        contextMock.VerifyAll();
    }

}
