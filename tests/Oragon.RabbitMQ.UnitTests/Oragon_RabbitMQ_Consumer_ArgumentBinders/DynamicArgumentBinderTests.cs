using Moq;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Oragon.RabbitMQ.Serialization;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using System.Reflection;


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

        var amqpSerializer = new Mock<IAmqpSerializer>();

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

    [Fact]
    public void FromHeaderFlow()
    {
        Delegate test = ([FromAmqpHeader("test")] string value) => string.Empty;
        ParameterInfo parameterInfo = test.Method.GetParameters().First();
        FromAmqpHeaderAttribute attr = parameterInfo.GetCustomAttribute<FromAmqpHeaderAttribute>() ?? throw new InvalidOperationException("Not Found!");
        IAmqpArgumentBinder binder = attr.Build(parameterInfo);


        string testValue = Guid.NewGuid().ToString("D");

        Dictionary<string, object> headers = new Dictionary<string, object>() { { "test", testValue } };

        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        basicPropertiesMock.SetupGet(it => it.Headers).Returns(headers).Verifiable(Times.Exactly(2));

        var amqpSerializer = new Mock<IAmqpSerializer>();

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
        contextMock.Setup(it => it.Request).Returns(basicDeliverEventArgs).Verifiable(Times.Once());

        // Act
        object result = binder.GetValue(contextMock.Object);

        // Assert
        Assert.Equal(testValue, result);
        basicPropertiesMock.Verify();
        contextMock.Verify();
    }

    [Fact]
    public void FromHeaderTypedValueFlow()
    {
        Delegate test = ([FromAmqpHeader("test")] int value) => string.Empty;
        ParameterInfo parameterInfo = test.Method.GetParameters().First();
        FromAmqpHeaderAttribute attr = parameterInfo.GetCustomAttribute<FromAmqpHeaderAttribute>() ?? throw new InvalidOperationException("Not Found!");
        IAmqpArgumentBinder binder = attr.Build(parameterInfo);

        Dictionary<string, object> headers = new Dictionary<string, object>() { { "test", "12" } };

        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        basicPropertiesMock.SetupGet(it => it.Headers).Returns(headers).Verifiable(Times.Exactly(2));

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
        contextMock.Setup(it => it.Request).Returns(basicDeliverEventArgs).Verifiable(Times.Once());

        // Act
        object result = binder.GetValue(contextMock.Object);

        // Assert
        Assert.Equal(12, result);
        basicPropertiesMock.Verify();
        contextMock.Verify();

    }

    [Fact]
    public void FromHeaderTypedValue_WhenHeaderIsMissingAndParameterIsNotNullable_ShouldThrowInvalidOperationException()
    {
        Delegate test = ([FromAmqpHeader("test")] int value) => string.Empty;
        ParameterInfo parameterInfo = test.Method.GetParameters().First();
        FromAmqpHeaderAttribute attr = parameterInfo.GetCustomAttribute<FromAmqpHeaderAttribute>() ?? throw new InvalidOperationException("Not Found!");
        IAmqpArgumentBinder binder = attr.Build(parameterInfo);

        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        basicPropertiesMock.SetupGet(it => it.Headers).Returns(new Dictionary<string, object>()).Verifiable(Times.Once());

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
        contextMock.Setup(it => it.Request).Returns(basicDeliverEventArgs).Verifiable(Times.Once());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => binder.GetValue(contextMock.Object));

        Assert.Contains("Required AMQP header 'test'", exception.Message, StringComparison.Ordinal);
        basicPropertiesMock.Verify();
        contextMock.Verify();
    }

    [Fact]
    public void FromHeaderWithNullDictionaryFlow()
    {
        Delegate test = ([FromAmqpHeader("test")] string value) => string.Empty;
        ParameterInfo parameterInfo = test.Method.GetParameters().First();
        FromAmqpHeaderAttribute attr = parameterInfo.GetCustomAttribute<FromAmqpHeaderAttribute>() ?? throw new InvalidOperationException("Not Found!");
        IAmqpArgumentBinder binder = attr.Build(parameterInfo);


        string testValue = Guid.NewGuid().ToString("D");

        Dictionary<string, object> headers = null;

        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        basicPropertiesMock.SetupGet(it => it.Headers).Returns(headers).Verifiable(Times.Once());

        var amqpSerializer = new Mock<IAmqpSerializer>();

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
        contextMock.Setup(it => it.Request).Returns(basicDeliverEventArgs).Verifiable(Times.Once());

        // Act
        object result = binder.GetValue(contextMock.Object);

        // Assert
        Assert.Null(result);
        basicPropertiesMock.Verify();
        contextMock.Verify();
    }

}
