using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.Dispatch;
using RabbitMQ.Client;
using BasicDeliverEventArgs = global::RabbitMQ.Client.Events.BasicDeliverEventArgs;


namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Consumer_ArgumentBinders;
public class ConventionArgumentBinderTests
{
    sealed class Message
    {
        public string Data { get; set; }
    }

    private static IAmqpContext BuildContext(byte priority = 0, IDictionary<string, object> headers = null)
    {
        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        _ = basicPropertiesMock.SetupGet(it => it.Priority).Returns(priority);
        _ = basicPropertiesMock.SetupGet(it => it.Headers).Returns(headers);

        BasicDeliverEventArgs request = new BasicDeliverEventArgs(
            consumerTag: "oragon-rabbitmq-consumerTag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "oragon-rabbitmq-exchangeName",
            routingKey: "oragon-rabbitmq-routingKey",
            properties: basicPropertiesMock.Object,
            body: null,
            cancellationToken: default);

        var contextMock = new Mock<IAmqpContext>();
        _ = contextMock.Setup(it => it.Request).Returns(request);
        _ = contextMock.Setup(it => it.MessageObject).Returns(new Message { Data = "oragon-rabbitmq-data" });

        return contextMock.Object;
    }

    private static async Task DispatchAsync(Delegate handler, IAmqpContext context)
    {
        var serviceProviderMock = new Mock<IServiceProvider>();

        var consumerDescriptor = new ConsumerDescriptor(serviceProviderMock.Object, "oragon-rabbitmq-queueName", handler);

        var dispatcher = new Dispatcher(consumerDescriptor);

        IAmqpResult result = await dispatcher.DispatchAsync(context);

        Assert.True(result is AckResult);
    }

    [Fact]
    public async Task PriorityBindsToByteParameter()
    {
        IAmqpContext context = BuildContext(priority: 7);

        await DispatchAsync((Message msg, byte priority) => Assert.Equal((byte)7, priority), context);
    }

    [Fact]
    public async Task PriorityBindsToIntParameter()
    {
        IAmqpContext context = BuildContext(priority: 7);

        await DispatchAsync((Message msg, int priority) => Assert.Equal(7, priority), context);
    }

    [Fact]
    public async Task PriorityBindsToLongParameter()
    {
        IAmqpContext context = BuildContext(priority: 7);

        await DispatchAsync((Message msg, long priority) => Assert.Equal(7L, priority), context);
    }

    [Fact]
    public async Task DeliveryCountBindsToLongParameter()
    {
        IAmqpContext context = BuildContext(headers: new Dictionary<string, object> { ["x-delivery-count"] = 5L });

        await DispatchAsync((Message msg, long deliveryCount) => Assert.Equal(5L, deliveryCount), context);
    }

    [Fact]
    public async Task DeliveryCountBindsToIntParameter()
    {
        IAmqpContext context = BuildContext(headers: new Dictionary<string, object> { ["x-delivery-count"] = 5L });

        await DispatchAsync((Message msg, int deliveryCount) => Assert.Equal(5, deliveryCount), context);
    }

    [Fact]
    public async Task DeliveryCountBindsToNullableLongParameter()
    {
        IAmqpContext context = BuildContext(headers: new Dictionary<string, object> { ["x-delivery-count"] = 5L });

        await DispatchAsync((Message msg, long? deliveryCount) => Assert.Equal(5L, deliveryCount), context);
    }

    [Fact]
    public async Task DeliveryCountBindsToNullableIntParameter()
    {
        IAmqpContext context = BuildContext(headers: new Dictionary<string, object> { ["x-delivery-count"] = 5L });

        await DispatchAsync((Message msg, int? deliveryCount) => Assert.Equal(5, deliveryCount), context);
    }

    [Fact]
    public async Task DeliveryCountBindsToAttemptsParameterName()
    {
        IAmqpContext context = BuildContext(headers: new Dictionary<string, object> { ["x-delivery-count"] = 5L });

        await DispatchAsync((Message msg, long attempts) => Assert.Equal(5L, attempts), context);
    }

    [Fact]
    public async Task DeliveryCountBindsWhenHeaderValueIsInt()
    {
        IAmqpContext context = BuildContext(headers: new Dictionary<string, object> { ["x-delivery-count"] = 5 });

        await DispatchAsync((Message msg, long deliveryCount) => Assert.Equal(5L, deliveryCount), context);
    }

    [Fact]
    public async Task DeliveryCountIsZeroWhenHeaderIsMissing()
    {
        IAmqpContext context = BuildContext(headers: new Dictionary<string, object>());

        await DispatchAsync((Message msg, long deliveryCount, int attempts) =>
        {
            Assert.Equal(0L, deliveryCount);
            Assert.Equal(0, attempts);
        }, context);
    }

    [Fact]
    public async Task DeliveryCountIsZeroWhenHeadersAreNull()
    {
        IAmqpContext context = BuildContext(headers: null);

        await DispatchAsync((Message msg, long deliveryCount) => Assert.Equal(0L, deliveryCount), context);
    }

    [Fact]
    public async Task DeliveryCountIsNullForNullableLongWhenHeaderIsMissing()
    {
        IAmqpContext context = BuildContext(headers: new Dictionary<string, object>());

        await DispatchAsync((Message msg, long? deliveryCount) => Assert.Null(deliveryCount), context);
    }

    [Fact]
    public async Task DeliveryCountIsNullForNullableLongWhenHeadersAreNull()
    {
        IAmqpContext context = BuildContext(headers: null);

        await DispatchAsync((Message msg, long? deliveryCount) => Assert.Null(deliveryCount), context);
    }

    [Fact]
    public async Task DeliveryCountIsNullForNullableIntWhenHeaderIsMissing()
    {
        IAmqpContext context = BuildContext(headers: new Dictionary<string, object>());

        await DispatchAsync((Message msg, int? deliveryCount) => Assert.Null(deliveryCount), context);
    }

    [Fact]
    public async Task DeliveryCountIsNullForNullableIntWhenHeadersAreNull()
    {
        IAmqpContext context = BuildContext(headers: null);

        await DispatchAsync((Message msg, int? deliveryCount) => Assert.Null(deliveryCount), context);
    }

    [Theory]
    [InlineData(typeof(byte))]
    [InlineData(typeof(int))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(long))]
    [InlineData(typeof(long?))]
    public void UnconventionalNumericParameterNameThrows(Type parameterType)
    {
        var serviceProviderMock = new Mock<IServiceProvider>();

        Delegate handler = parameterType switch
        {
            var type when type == typeof(byte) => (Message msg, byte count) => { },
            var type when type == typeof(int) => (Message msg, int count) => { },
            var type when type == typeof(int?) => (Message msg, int? count) => { },
            var type when type == typeof(long) => (Message msg, long count) => { },
            _ => (Message msg, long? count) => { },
        };

        var consumerDescriptor = new ConsumerDescriptor(serviceProviderMock.Object, "oragon-rabbitmq-queueName", handler);

        _ = Assert.Throws<InvalidOperationException>(() => new Dispatcher(consumerDescriptor));
    }
}
