using System.Reflection;
using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;


namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Consumer_ArgumentBinders;
public class FromBodyArgumentBinderTests
{
    sealed class Service { }

    [Fact]
    public void FromServicesArgumentBinderFlow()
    {
        Type type = typeof(Service);
        var service = new Service();

        Delegate test = ([FromBody] Service value) => string.Empty;
        ParameterInfo parameterInfo = test.Method.GetParameters().First();
        FromBodyAttribute attr = parameterInfo.GetCustomAttribute<FromBodyAttribute>() ?? throw new InvalidOperationException("Not Found!");
        IAmqpArgumentBinder binder = attr.Build(parameterInfo);

        var contextMock = new Mock<IAmqpContext>();
        contextMock.Setup(it => it.MessageObject).Returns(service).Verifiable(Times.Once());

        // Act
        object result = binder.GetValue(contextMock.Object);

        // Assert
        Assert.Same(service, result);
    }

}
