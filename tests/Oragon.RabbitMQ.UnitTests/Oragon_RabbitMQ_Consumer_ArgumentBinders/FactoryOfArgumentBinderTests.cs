using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.Dispatch;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using RabbitMQ.Client;
using BasicDeliverEventArgs = global::RabbitMQ.Client.Events.BasicDeliverEventArgs;


namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Consumer_ArgumentBinders;
public class FactoryOfArgumentBinderTests
{
    sealed class Service { }
    sealed class Message
    {
        public string Data { get; set; }
    }


    [Fact]
    public async Task ModelBinderTests()
    {

        // Arrange
        var service = new Service();
        Message message = new Message { Data = "oragon-rabbitmq-data" };

        var serviceProviderMock = new Mock<IServiceProvider>();
        _ = serviceProviderMock.Setup(it => it.GetService(typeof(Service))).Returns(service);

        var connectionMock = new Mock<IConnection>();
        var channelMock = new Mock<IChannel>();
        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        _ = basicPropertiesMock.Setup(it => it.DeliveryMode).Returns(DeliveryModes.Persistent);

        var cancellationToken = new CancellationToken();

        BasicDeliverEventArgs request = new BasicDeliverEventArgs(
            consumerTag: "oragon-rabbitmq-consumerTag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "oragon-rabbitmq-exchangeName",
            routingKey: "oragon-rabbitmq-routingKey",
            properties: basicPropertiesMock.Object,
            body: null,
            cancellationToken: cancellationToken);


        var contextMock = new Mock<IAmqpContext>();
        _ = contextMock.Setup(it => it.QueueName).Returns("oragon-rabbitmq-queueName");
        _ = contextMock.Setup(it => it.Connection).Returns(connectionMock.Object);
        _ = contextMock.Setup(it => it.Channel).Returns(channelMock.Object);
        _ = contextMock.Setup(it => it.ServiceProvider).Returns(serviceProviderMock.Object);
        _ = contextMock.Setup(it => it.CancellationToken).Returns(cancellationToken);
        _ = contextMock.Setup(it => it.Request).Returns(request);
        _ = contextMock.Setup(it => it.MessageObject).Returns(message);
        IAmqpContext currentContext = contextMock.Object;

        // Act
        Delegate handler = (
            [FromServices] Service service_arg,             //resolved by attribute
            IConnection connection_arg,                     //resolved by type
            IChannel channel_arg,                           //resolved by type
            BasicDeliverEventArgs eventArgs_arg,            //resolved by type
            DeliveryModes deliveryMode_arg,                 //resolved by type
            IServiceProvider serviceProvider_arg,           //resolved by type
            IReadOnlyBasicProperties basicProperties_arg,   //resolved by type
            CancellationToken cancellationToken_arg,        //resolved by type
            IAmqpContext context_arg,                       //resolved by type
            string queueName,                               //resolved by type and name
            string exchangeName,                            //resolved by type and name
            string routingKey,                              //resolved by type and name
            string consumerTag,                             //resolved by type and name
            Message message_arg                             //resolved by type and name
            ) =>
        {
            Assert.Same(service, service_arg);
            Assert.Same(connectionMock.Object, connection_arg);
            Assert.Same(channelMock.Object, channel_arg);
            Assert.Same(request, eventArgs_arg);
            Assert.Same(currentContext, context_arg);
            Assert.Equal(DeliveryModes.Persistent, deliveryMode_arg);
            Assert.Same(serviceProviderMock.Object, serviceProvider_arg);
            Assert.Same(basicPropertiesMock.Object, basicProperties_arg);
            Assert.Equal(cancellationToken, cancellationToken_arg);

            Assert.Equal("oragon-rabbitmq-queueName", queueName);
            Assert.Equal("oragon-rabbitmq-exchangeName", exchangeName);
            Assert.Equal("oragon-rabbitmq-routingKey", routingKey);
            Assert.Equal("oragon-rabbitmq-consumerTag", consumerTag);

            Assert.Same(message, message_arg);


        };

        var queueConsumerBuilder = new ConsumerParameters(serviceProviderMock.Object, "oragon-rabbitmq-queueName", handler);

        var dispatcher = new Dispatcher(queueConsumerBuilder);

        IAmqpResult result = await dispatcher.DispatchAsync(currentContext);

        Assert.True(result is AckResult);

    }

}
