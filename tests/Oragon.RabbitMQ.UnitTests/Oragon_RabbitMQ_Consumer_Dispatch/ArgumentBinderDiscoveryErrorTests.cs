// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.Dispatch;
using RabbitMQ.Client;
using BasicDeliverEventArgs = global::RabbitMQ.Client.Events.BasicDeliverEventArgs;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Consumer_Dispatch;

/// <summary>
/// Covers discovery-failure branches of ArgumentBinderExtensions (MVC attribute rejection, unrecognized
/// parameter names for string/byte?/Guid/Guid?/DateTimeOffset/DateTimeOffset?, and the "optional AMQP
/// metadata requires a nullable parameter" guard) that are not exercised by
/// ConventionArgumentBinderTests, plus the "metadata absent binds to null" branches for priority,
/// deliveryMode and timestamp.
/// </summary>
public class ArgumentBinderDiscoveryErrorTests
{
    private sealed class Message
    {
        public string Data { get; set; }
    }

    private static ConsumerDescriptor CreateDescriptor(Delegate handler)
    {
        Mock<IServiceProvider> serviceProviderMock = new Mock<IServiceProvider>();
        return new ConsumerDescriptor(serviceProviderMock.Object, "oragon-rabbitmq-queueName", handler);
    }

    private static InvalidOperationException AssertDispatcherThrows(Delegate handler)
    {
        ConsumerDescriptor descriptor = CreateDescriptor(handler);

        return Assert.Throws<InvalidOperationException>(() => new Dispatcher(descriptor));
    }

    private static IAmqpContext BuildContextWithoutOptionalMetadata()
    {
        Mock<IReadOnlyBasicProperties> basicPropertiesMock = new Mock<IReadOnlyBasicProperties>();
        _ = basicPropertiesMock.Setup(it => it.IsPriorityPresent()).Returns(false);
        _ = basicPropertiesMock.Setup(it => it.IsDeliveryModePresent()).Returns(false);
        _ = basicPropertiesMock.Setup(it => it.IsTimestampPresent()).Returns(false);

        BasicDeliverEventArgs request = new(
            consumerTag: "oragon-rabbitmq-consumerTag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "oragon-rabbitmq-exchangeName",
            routingKey: "oragon-rabbitmq-routingKey",
            properties: basicPropertiesMock.Object,
            body: null,
            cancellationToken: default);

        Mock<IAmqpContext> contextMock = new Mock<IAmqpContext>();
        _ = contextMock.Setup(it => it.Request).Returns(request);
        _ = contextMock.Setup(it => it.MessageObject).Returns(new Message { Data = "oragon-rabbitmq-data" });

        return contextMock.Object;
    }

    private static async Task DispatchAndAssertAck(Delegate handler, IAmqpContext context)
    {
        ConsumerDescriptor descriptor = CreateDescriptor(handler);
        Dispatcher dispatcher = new Dispatcher(descriptor);

        IAmqpResult result = await dispatcher.DispatchAsync(context);

        _ = Assert.IsType<AckResult>(result);
    }

    #region MVC attributes

    [Fact]
    public void BuildArgumentBinders_ParameterWithMvcFromBodyAttribute_ShouldThrow()
    {
        // Arrange
        Delegate handler = ([Microsoft.AspNetCore.Mvc.FromBody] Message msg) => { };

        // Act
        InvalidOperationException exception = AssertDispatcherThrows(handler);

        // Assert
        Assert.Contains("ASP.NET MVC", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Unrecognized parameter names

    [Fact]
    public void BuildArgumentBinders_UnrecognizedStringParameterName_ShouldThrow()
    {
        // Arrange
        Delegate handler = (Message msg, string foo) => { };

        // Act
        InvalidOperationException exception = AssertDispatcherThrows(handler);

        // Assert
        Assert.Equal("Can't determine binder for foo", exception.Message);
    }

    [Fact]
    public void BuildArgumentBinders_UnrecognizedNullableByteParameterName_ShouldThrow()
    {
        // Arrange
        Delegate handler = (Message msg, byte? foo) => { };

        // Act
        InvalidOperationException exception = AssertDispatcherThrows(handler);

        // Assert
        Assert.Equal("Can't determine binder for foo", exception.Message);
    }

    [Fact]
    public void BuildArgumentBinders_UnrecognizedGuidParameterName_ShouldThrow()
    {
        // Arrange
        Delegate handler = (Message msg, Guid foo) => { };

        // Act
        InvalidOperationException exception = AssertDispatcherThrows(handler);

        // Assert
        Assert.Equal("Can't determine binder for foo", exception.Message);
    }

    [Fact]
    public void BuildArgumentBinders_UnrecognizedNullableGuidParameterName_ShouldThrow()
    {
        // Arrange
        Delegate handler = (Message msg, Guid? foo) => { };

        // Act
        InvalidOperationException exception = AssertDispatcherThrows(handler);

        // Assert
        Assert.Equal("Can't determine binder for foo", exception.Message);
    }

    [Fact]
    public void BuildArgumentBinders_UnrecognizedNullableDateTimeOffsetParameterName_ShouldThrow()
    {
        // Arrange
        Delegate handler = (Message msg, DateTimeOffset? foo) => { };

        // Act
        InvalidOperationException exception = AssertDispatcherThrows(handler);

        // Assert
        Assert.Equal("Can't determine binder for foo", exception.Message);
    }

    [Fact]
    public void BuildArgumentBinders_UnrecognizedDateTimeOffsetParameterName_ShouldThrow()
    {
        // Arrange
        Delegate handler = (Message msg, DateTimeOffset foo) => { };

        // Act
        InvalidOperationException exception = AssertDispatcherThrows(handler);

        // Assert
        Assert.Equal("Can't determine binder for foo", exception.Message);
    }

    #endregion

    #region Optional AMQP metadata requires a nullable parameter

    [Fact]
    public void BuildArgumentBinders_NonNullableGuidMessageId_ShouldThrowRequestingNullable()
    {
        // Arrange
        Delegate handler = (Message msg, Guid messageId) => { };

        // Act
        InvalidOperationException exception = AssertDispatcherThrows(handler);

        // Assert
        Assert.Equal("Can't bind messageId: AMQP messageId is optional. Use string or Guid?.", exception.Message);
    }

    [Fact]
    public void BuildArgumentBinders_NonNullableDateTimeOffsetTimestamp_ShouldThrowRequestingNullable()
    {
        // Arrange
        Delegate handler = (Message msg, DateTimeOffset timestamp) => { };

        // Act
        InvalidOperationException exception = AssertDispatcherThrows(handler);

        // Assert
        Assert.Equal("Can't bind timestamp: AMQP timestamp is optional. Use DateTimeOffset?, long? or AmqpTimestamp?.", exception.Message);
    }

    #endregion

    #region Optional metadata absent binds to null instead of throwing

    [Fact]
    public async Task Dispatch_PriorityAbsent_BindsNullableByteParameterAsNull()
    {
        // Arrange
        IAmqpContext context = BuildContextWithoutOptionalMetadata();
        byte? captured = 1;
        Delegate handler = (Message msg, byte? priority) => { captured = priority; };

        // Act
        await DispatchAndAssertAck(handler, context);

        // Assert
        Assert.Null(captured);
    }

    [Fact]
    public async Task Dispatch_DeliveryModeAbsent_BindsNullableByteParameterAsNull()
    {
        // Arrange
        IAmqpContext context = BuildContextWithoutOptionalMetadata();
        byte? captured = 1;
        Delegate handler = (Message msg, byte? deliveryMode) => { captured = deliveryMode; };

        // Act
        await DispatchAndAssertAck(handler, context);

        // Assert
        Assert.Null(captured);
    }

    [Fact]
    public async Task Dispatch_TimestampAbsent_BindsNullableLongParameterAsNull()
    {
        // Arrange
        IAmqpContext context = BuildContextWithoutOptionalMetadata();
        long? captured = 1L;
        Delegate handler = (Message msg, long? timestamp) => { captured = timestamp; };

        // Act
        await DispatchAndAssertAck(handler, context);

        // Assert
        Assert.Null(captured);
    }

    [Fact]
    public async Task Dispatch_TimestampAbsent_BindsNullableAmqpTimestampParameterAsNull()
    {
        // Arrange
        IAmqpContext context = BuildContextWithoutOptionalMetadata();
        AmqpTimestamp? captured = new AmqpTimestamp(1);
        Delegate handler = (Message msg, AmqpTimestamp? timestamp) => { captured = timestamp; };

        // Act
        await DispatchAndAssertAck(handler, context);

        // Assert
        Assert.Null(captured);
    }

    #endregion
}
