using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "It's for test purposes")]
public class Extensions_RabbitMQ_Tests
{
    [Fact]
    public void CreateBasicProperties_Should_Return_New_BasicProperties()
    {
        // Arrange
        IChannel channel = new Mock<IChannel>().Object;

        // Act
        BasicProperties result = channel.CreateBasicProperties();

        // Assert
        Assert.NotNull(result);
        _ = Assert.IsType<BasicProperties>(result);
    }

    [Fact]
    public void SetMessageId_Should_Set_MessageId_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        string messageId = "12345";

        // Act
        BasicProperties result = basicProperties.SetMessageId(messageId);

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
        BasicProperties result = basicProperties.SetCorrelationId(originalBasicProperties);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(originalBasicProperties.MessageId, result.CorrelationId);
    }

    [Fact]
    public void SetCorrelationId_With_String_Should_Set_CorrelationId_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        string correlationId = "54321";

        // Act
        BasicProperties result = basicProperties.SetCorrelationId(correlationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(correlationId, result.CorrelationId);
    }

    [Fact]
    public void SetDurable_Should_Set_Persistent_Property_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        bool durable = true;

        // Act
        BasicProperties result = basicProperties.SetDurable(durable);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(durable, result.Persistent);
    }

    [Fact]
    public void SetTransient_Should_Set_Durable_Property_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        bool transient = true;

        // Act
        BasicProperties result = basicProperties.SetTransient(transient);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(!transient, result.Persistent);
    }

    [Fact]
    public void SetReplyTo_Should_Set_ReplyTo_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        string replyTo = "reply_queue";

        // Act
        BasicProperties result = basicProperties.SetReplyTo(replyTo);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(replyTo, result.ReplyTo);
    }

    [Fact]
    public void SetAppId_Should_Set_AppId_On_BasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        string appId = "my_app";

        // Act
        BasicProperties result = basicProperties.SetAppId(appId);

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
        Type exceptionType = exception.GetType();

        // Act
        BasicProperties result = basicProperties.SetException(exception);

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
        Type exceptionType = exception.GetType();

        // Act
        BasicProperties result = basicProperties.TrySetException(exception);

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
        BasicProperties result = basicProperties.TrySetException(exception);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Headers);
    }


    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(ushort.MaxValue)]
    public void Parallelism_Should_Set_ConsumerDispatchConcurrency_When_Concurrency_Is_Valid(int concurrency)
    {
        // Arrange
        var connectionFactory = new ConnectionFactory();

        // Act
        ConnectionFactory result = connectionFactory.Parallelism(concurrency);

        // Assert
        Assert.Same(connectionFactory, result);
        Assert.Equal((ushort)concurrency, result.ConsumerDispatchConcurrency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ushort.MaxValue + 1)]
    [InlineData(int.MaxValue)]
    public void Parallelism_Should_Throw_When_Concurrency_Is_Out_Of_UShort_Range(int concurrency)
    {
        // Arrange
        var connectionFactory = new ConnectionFactory();

        // Act & Assert
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => connectionFactory.Parallelism(concurrency));
        Assert.Equal("consumerDispatchConcurrency", exception.ParamName);
    }

    [Fact]
    public void Parallelism_Should_Throw_When_ConnectionFactory_Is_Null()
    {
        // Arrange
        ConnectionFactory connectionFactory = null;

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => connectionFactory.Parallelism(1));
    }

    [Fact]
    public void Unbox_Should_Convert_IConnectionFactory_To_ConnectionFactory()
    {
        // Arrange
        IConnectionFactory connectionFactory = new ConnectionFactory();

        // Act
        ConnectionFactory result = connectionFactory.Unbox();

        // Assert
        Assert.NotNull(result);
        _ = Assert.IsType<ConnectionFactory>(result);
    }

    [Fact]
    public void SetupApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddRabbitMQConsumer();
        WebApplication app = builder.Build();

        ConsumerServer consumer = app.Services.GetRequiredService<ConsumerServer>();
        Assert.NotNull(consumer);

        IEnumerable<IHostedService> hostedServices = app.Services.GetServices<IHostedService>();

        Assert.Contains(hostedServices, it => it is ConsumerServer);
    }
}
