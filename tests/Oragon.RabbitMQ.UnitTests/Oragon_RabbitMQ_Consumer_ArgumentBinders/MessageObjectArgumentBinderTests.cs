using Moq;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Oragon.RabbitMQ.Serialization;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;


namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Consumer_ArgumentBinders;
public class MessageObjectArgumentBinderTests
{
    class Service { }
    [Fact]
    public void MessageObjectArgumentBinderFlow()
    {
        var service = new Service();

        var contextMock = new Mock<IAmqpContext>();
        contextMock.Setup(it => it.MessageObject).Returns(service).Verifiable(Times.Once());

        // Act
        object result = new MessageObjectArgumentBinder(typeof(Service)).GetValue(contextMock.Object);

        // Assert
        Assert.Equal(service, result);
    }

}
