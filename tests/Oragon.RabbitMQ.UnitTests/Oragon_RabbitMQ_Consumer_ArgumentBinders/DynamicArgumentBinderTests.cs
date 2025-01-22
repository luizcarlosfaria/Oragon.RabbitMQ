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
        Assert.Equal(testValue, result);
        basicPropertiesMock.Verify();
        contextMock.Verify();
    }

    [Fact]
    public void FromHeaderBustBeStringFlow()
    {
        Delegate test = ([FromAmqpHeader("test")] int value) => string.Empty;
        ParameterInfo parameterInfo = test.Method.GetParameters().First();
        FromAmqpHeaderAttribute attr = parameterInfo.GetCustomAttribute<FromAmqpHeaderAttribute>() ?? throw new InvalidOperationException("Not Found!");

        _ = Assert.Throws<InvalidOperationException>(() => attr.Build(parameterInfo));

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
