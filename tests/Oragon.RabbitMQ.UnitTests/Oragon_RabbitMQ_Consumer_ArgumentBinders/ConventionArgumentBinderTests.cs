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

    private static IAmqpContext BuildContext(
        byte priority = 0,
        IDictionary<string, object> headers = null,
        Action<Mock<IReadOnlyBasicProperties>> configureProperties = null)
    {
        var basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        _ = basicPropertiesMock.SetupGet(it => it.Priority).Returns(priority);
        _ = basicPropertiesMock.Setup(it => it.IsPriorityPresent()).Returns(priority > 0);
        _ = basicPropertiesMock.SetupGet(it => it.Headers).Returns(headers);
        configureProperties?.Invoke(basicPropertiesMock);

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
    public async Task PriorityBindsToNullableByteParameter()
    {
        IAmqpContext context = BuildContext(priority: 7);

        await DispatchAsync((Message msg, byte? priority) => Assert.Equal((byte)7, priority), context);
    }

    [Fact]
    public async Task PriorityBindsToNullableIntParameter()
    {
        IAmqpContext context = BuildContext(priority: 7);

        await DispatchAsync((Message msg, int? priority) => Assert.Equal(7, priority), context);
    }

    [Fact]
    public async Task PriorityBindsToNullableLongParameter()
    {
        IAmqpContext context = BuildContext(priority: 7);

        await DispatchAsync((Message msg, long? priority) => Assert.Equal(7L, priority), context);
    }

    [Fact]
    public async Task StringBasicPropertiesBindByConvention()
    {
        string messageTypeAlias = null;
        IAmqpContext context = BuildContext(configureProperties: properties =>
        {
            _ = properties.SetupGet(it => it.ContentType).Returns("application/json");
            _ = properties.SetupGet(it => it.ContentEncoding).Returns("utf-8");
            _ = properties.SetupGet(it => it.CorrelationId).Returns("correlation-1");
            _ = properties.SetupGet(it => it.ReplyTo).Returns("reply-queue");
            _ = properties.SetupGet(it => it.Expiration).Returns("30000");
            _ = properties.SetupGet(it => it.MessageId).Returns("message-1");
            _ = properties.SetupGet(it => it.Type).Returns("message-type");
            _ = properties.SetupGet(it => it.UserId).Returns("user-1");
            _ = properties.SetupGet(it => it.AppId).Returns("app-1");
            _ = properties.SetupGet(it => it.ClusterId).Returns("cluster-1");
        });

        await DispatchAsync((
            Message msg,
            string contentType,
            string contentEncoding,
            string correlationId,
            string replyTo,
            string expiration,
            string messageId,
            string type,
            string messageType,
            string userId,
            string appId,
            string clusterId) =>
        {
            messageTypeAlias = messageType;
            Assert.Equal("application/json", contentType);
            Assert.Equal("utf-8", contentEncoding);
            Assert.Equal("correlation-1", correlationId);
            Assert.Equal("reply-queue", replyTo);
            Assert.Equal("30000", expiration);
            Assert.Equal("message-1", messageId);
            Assert.Equal("message-type", type);
            Assert.Equal("user-1", userId);
            Assert.Equal("app-1", appId);
            Assert.Equal("cluster-1", clusterId);
        }, context);

        Assert.Equal("message-type", messageTypeAlias);
    }

    [Fact]
    public async Task TypedBasicPropertiesBindByConvention()
    {
        IDictionary<string, object> expectedHeaders = new Dictionary<string, object> { ["x-test"] = "value" };
        var timestamp = new AmqpTimestamp(1234567890);
        DateTimeOffset expectedTimestamp = DateTimeOffset.FromUnixTimeSeconds(timestamp.UnixTime);
        IAmqpContext context = BuildContext(headers: expectedHeaders, configureProperties: properties =>
        {
            _ = properties.SetupGet(it => it.DeliveryMode).Returns(DeliveryModes.Persistent);
            _ = properties.Setup(it => it.IsDeliveryModePresent()).Returns(true);
            _ = properties.SetupGet(it => it.Timestamp).Returns(timestamp);
            _ = properties.Setup(it => it.IsTimestampPresent()).Returns(true);
        });

        await DispatchAsync((
            Message msg,
            DeliveryModes? deliveryMode,
            IDictionary<string, object> headers,
            IReadOnlyDictionary<string, object> readOnlyHeaders,
            AmqpTimestamp? timestamp) =>
        {
            Assert.Equal(DeliveryModes.Persistent, deliveryMode);
            Assert.Same(expectedHeaders, headers);
            Assert.Same(expectedHeaders, readOnlyHeaders);
            Assert.Equal(1234567890, timestamp?.UnixTime);
        }, context);

        await DispatchAsync((Message msg, byte? deliveryMode) => Assert.Equal((byte)DeliveryModes.Persistent, deliveryMode), context);
        await DispatchAsync((Message msg, int? deliveryMode) => Assert.Equal((int)DeliveryModes.Persistent, deliveryMode), context);
        await DispatchAsync((Message msg, long? deliveryMode) => Assert.Equal((long)DeliveryModes.Persistent, deliveryMode), context);
        await DispatchAsync((Message msg, long? timestamp) => Assert.Equal(1234567890, timestamp), context);
        await DispatchAsync((Message msg, DateTimeOffset? timestamp) => Assert.Equal(expectedTimestamp, timestamp), context);
    }

    [Fact]
    public async Task MessageIdBindsToNullableGuidWhenValid()
    {
        Guid expected = Guid.NewGuid();
        IAmqpContext context = BuildContext(configureProperties: properties =>
        {
            _ = properties.SetupGet(it => it.MessageId).Returns(expected.ToString("D"));
        });

        await DispatchAsync((Message msg, Guid? messageId) => Assert.Equal(expected, messageId), context);
    }

    [Fact]
    public async Task MessageIdBindsToNullableGuidAsNullWhenMissing()
    {
        IAmqpContext context = BuildContext(configureProperties: properties =>
        {
            _ = properties.SetupGet(it => it.MessageId).Returns((string)null);
        });

        await DispatchAsync((Message msg, Guid? messageId) => Assert.Null(messageId), context);
    }

    [Fact]
    public async Task MessageIdBindsToNullableGuidAsNullWhenInvalid()
    {
        IAmqpContext context = BuildContext(configureProperties: properties =>
        {
            _ = properties.SetupGet(it => it.MessageId).Returns("not-a-guid");
        });

        await DispatchAsync((Message msg, Guid? messageId) => Assert.Null(messageId), context);
    }

    [Fact]
    public async Task NullableTimestampBindsToNullWhenTimestampIsAbsent()
    {
        IAmqpContext context = BuildContext(configureProperties: properties =>
        {
            _ = properties.SetupGet(it => it.Timestamp).Returns(new AmqpTimestamp(0));
            _ = properties.Setup(it => it.IsTimestampPresent()).Returns(false);
        });

        await DispatchAsync((Message msg, DateTimeOffset? timestamp) => Assert.Null(timestamp), context);
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

        await DispatchAsync((Message msg, long? attempts) => Assert.Equal(5L, attempts), context);
    }

    [Fact]
    public async Task DeliveryCountBindsWhenHeaderValueIsInt()
    {
        IAmqpContext context = BuildContext(headers: new Dictionary<string, object> { ["x-delivery-count"] = 5 });

        await DispatchAsync((Message msg, long? deliveryCount) => Assert.Equal(5L, deliveryCount), context);
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
    [InlineData(typeof(byte), "priority")]
    [InlineData(typeof(int), "priority")]
    [InlineData(typeof(long), "priority")]
    [InlineData(typeof(DeliveryModes), "deliveryMode")]
    [InlineData(typeof(byte), "deliveryMode")]
    [InlineData(typeof(int), "deliveryMode")]
    [InlineData(typeof(long), "deliveryMode")]
    [InlineData(typeof(long), "timestamp")]
    [InlineData(typeof(DateTimeOffset), "timestamp")]
    [InlineData(typeof(AmqpTimestamp), "timestamp")]
    [InlineData(typeof(int), "deliveryCount")]
    [InlineData(typeof(long), "deliveryCount")]
    public void OptionalMetadataNonNullableParameterThrows(Type parameterType, string parameterName)
    {
        var serviceProviderMock = new Mock<IServiceProvider>();

        Delegate handler = (parameterType, parameterName) switch
        {
            ({ } type, "priority") when type == typeof(byte) => (Message msg, byte priority) => { },
            ({ } type, "priority") when type == typeof(int) => (Message msg, int priority) => { },
            ({ } type, "priority") when type == typeof(long) => (Message msg, long priority) => { },
            ({ } type, "deliveryMode") when type == typeof(DeliveryModes) => (Message msg, DeliveryModes deliveryMode) => { },
            ({ } type, "deliveryMode") when type == typeof(byte) => (Message msg, byte deliveryMode) => { },
            ({ } type, "deliveryMode") when type == typeof(int) => (Message msg, int deliveryMode) => { },
            ({ } type, "deliveryMode") when type == typeof(long) => (Message msg, long deliveryMode) => { },
            ({ } type, "timestamp") when type == typeof(long) => (Message msg, long timestamp) => { },
            ({ } type, "timestamp") when type == typeof(DateTimeOffset) => (Message msg, DateTimeOffset timestamp) => { },
            ({ } type, "timestamp") when type == typeof(AmqpTimestamp) => (Message msg, AmqpTimestamp timestamp) => { },
            ({ } type, "deliveryCount") when type == typeof(int) => (Message msg, int deliveryCount) => { },
            _ => (Message msg, long deliveryCount) => { },
        };

        var consumerDescriptor = new ConsumerDescriptor(serviceProviderMock.Object, "oragon-rabbitmq-queueName", handler);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new Dispatcher(consumerDescriptor));
        Assert.Contains("optional", exception.Message, StringComparison.OrdinalIgnoreCase);
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
