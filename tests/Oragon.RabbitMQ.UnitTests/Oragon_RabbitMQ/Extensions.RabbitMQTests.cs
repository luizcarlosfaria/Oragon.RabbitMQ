using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class Extensions_RabbitMQ_Tests
{
    [Fact]
    public void CreateBasicProperties_Should_Return_New_BasicProperties()
    {
        // Arrange
        var channel = new Mock<IChannel>().Object;

        // Act
        var result = channel.CreateBasicProperties();

        // Assert
        Assert.NotNull(result);
        _ = Assert.IsType<BasicProperties>(result);
    }

    [Fact]
    public void SetMessageId_Should_Set_MessageId_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        var messageId = "12345";

        // Act
        var result = basicProperties.SetMessageId(messageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(messageId, result.MessageId);
    }

    [Fact]
    public void SetCorrelationId_With_IReadOnlyBasicProperties_Should_Set_CorrelationId_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        var originalBasicProperties = new BasicProperties { MessageId = "12345" };

        // Act
        var result = basicProperties.SetCorrelationId(originalBasicProperties);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(originalBasicProperties.MessageId, result.CorrelationId);
    }

    [Fact]
    public void SetCorrelationId_With_String_Should_Set_CorrelationId_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        var correlationId = "54321";

        // Act
        var result = basicProperties.SetCorrelationId(correlationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(correlationId, result.CorrelationId);
    }

    [Fact]
    public void SetDurable_Should_Set_Persistent_Property_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        var durable = true;

        // Act
        var result = basicProperties.SetDurable(durable);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(durable, result.Persistent);
    }

    [Fact]
    public void SetTransient_Should_Set_Durable_Property_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        var transient = true;

        // Act
        var result = basicProperties.SetTransient(transient);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(!transient, result.Persistent);
    }

    [Fact]
    public void SetReplyTo_Should_Set_ReplyTo_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        var replyTo = "reply_queue";

        // Act
        var result = basicProperties.SetReplyTo(replyTo);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(replyTo, result.ReplyTo);
    }

    [Fact]
    public void SetAppId_Should_Set_AppId_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        var appId = "my_app";

        // Act
        var result = basicProperties.SetAppId(appId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(appId, result.AppId);
    }

    [Fact]
    public void SetException_Should_Set_Exception_Headers_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        var exception = new Exception("Test Exception");
        var exceptionType = exception.GetType();

        // Act
        var result = basicProperties.SetException(exception);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Headers);
        Assert.Equal($"{exceptionType.Namespace}.{exceptionType.Name}, {exceptionType.Assembly.FullName}", result.Headers["exception.type"]);
        Assert.Equal(exception.Message, result.Headers["exception.message"]);
        Assert.Equal(exception.StackTrace, result.Headers["exception.stacktrace"]);
    }

    [Fact]
    public void TrySetException_Should_Set_Exception_Headers_On_BasicProperties_When_Exception_Is_Not_Null()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        var exception = new Exception("Test Exception");
        var exceptionType = exception.GetType();

        // Act
        var result = basicProperties.TrySetException(exception);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Headers);
        Assert.Equal($"{exceptionType.Namespace}.{exceptionType.Name}, {exceptionType.Assembly.FullName}", result.Headers["exception.type"]);
        Assert.Equal(exception.Message, result.Headers["exception.message"]);
        Assert.Equal(exception.StackTrace, result.Headers["exception.stacktrace"]);
    }

    [Fact]
    public void TrySetException_Should_Not_Set_Exception_Headers_On_BasicProperties_When_Exception_Is_Null()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        Exception exception = null;

        // Act
        var result = basicProperties.TrySetException(exception);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Headers);
    }


    [Fact]
    public void Unbox_Should_Convert_IConnectionFactory_To_ConnectionFactory()
    {
        // Arrange
        IConnectionFactory connectionFactory = new ConnectionFactory();

        // Act
        var result = connectionFactory.Unbox();

        // Assert
        Assert.NotNull(result);
        _ = Assert.IsType<ConnectionFactory>(result);
    }

    [Fact]
    public void SetupApplication()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddRabbitMQConsumer();
        var app = builder.Build();

        var consumer = app.Services.GetRequiredService<ConsumerServer>();
        Assert.NotNull(consumer);

        var hostedServices = app.Services.GetServices<IHostedService>();

        Assert.Contains(hostedServices, it => it is ConsumerServer);
    }
}
