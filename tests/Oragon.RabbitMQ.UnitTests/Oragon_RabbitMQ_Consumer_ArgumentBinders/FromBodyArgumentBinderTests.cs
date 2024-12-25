using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;


namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Consumer_ArgumentBinders;
public class FromBodyArgumentBinderTests
{
    class Service { }

    [Fact]
    public void FromServicesArgumentBinderFlow()
    {
        Type type = typeof(Service);
        var service = new Service();

        var contextMock = new Mock<IAmqpContext>();
        contextMock.Setup(it => it.MessageObject).Returns(service).Verifiable(Times.Once());

        // Act
        object result = new MessageObjectArgumentBinder(type).GetValue(contextMock.Object);

        // Assert
        Assert.Same(service, result);
    }

}
