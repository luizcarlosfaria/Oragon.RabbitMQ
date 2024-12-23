using Moq;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Oragon.RabbitMQ.Serialization;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;
using Microsoft.Extensions.DependencyInjection;


namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Consumer_ArgumentBinders;
public class FromServicesArgumentBinderTests
{
    class Service { }

    [Fact]
    public void FromServicesArgumentBinderFlow()
    {
        Type type = typeof(Service);
        var service = new Service();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(it => it.GetService(It.IsAny<Type>())).Returns(service).Verifiable(Times.Once());

        var contextMock = new Mock<IAmqpContext>();
        contextMock.Setup(it => it.ServiceProvider).Returns(serviceProviderMock.Object).Verifiable(Times.Once());

        // Act
        object result = new FromServicesArgumentBinder(type).GetValue(contextMock.Object);

        // Assert
        Assert.Same(service, result);
    }

    [Fact]
    public void FromServicesArgumentBinderKeyedFlow()
    {
        Type type = typeof(Service);
        var service = new Service();

        var serviceProviderMock = new Mock<IKeyedServiceProvider>();
        serviceProviderMock.Setup(it => it.GetRequiredKeyedService(It.IsAny<Type>(), It.Is<object?>(it => (string)it == "aa"))).Returns(service).Verifiable(Times.Once());

        var contextMock = new Mock<IAmqpContext>();
        contextMock.Setup(it => it.ServiceProvider).Returns(serviceProviderMock.Object).Verifiable(Times.Once());

        // Act
        object result = new FromServicesArgumentBinder(type, "aa").GetValue(contextMock.Object);

        // Assert
        Assert.Same(service, result);
    }


}
