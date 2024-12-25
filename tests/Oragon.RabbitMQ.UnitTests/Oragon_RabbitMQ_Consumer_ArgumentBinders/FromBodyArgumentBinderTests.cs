using Moq;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Oragon.RabbitMQ.Serialization;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;
using Microsoft.Extensions.DependencyInjection;


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
