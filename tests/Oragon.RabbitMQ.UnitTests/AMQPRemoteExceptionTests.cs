using Xunit;
using Oragon.RabbitMQ;

namespace Oragon.RabbitMQ.UnitTests;

public class AMQPRemoteExceptionTests
{
    [Fact]
    public void AMQPRemoteException_DefaultConstructor_ShouldCreateInstance()
    {
        // Arrange

        // Act
        var exception = new AMQPRemoteException();

        // Assert
        Assert.NotNull(exception);
        _ = Assert.IsType<AMQPRemoteException>(exception);
    }

    [Fact]
    public void AMQPRemoteException_MessageConstructor_ShouldCreateInstanceWithMessage()
    {
        // Arrange
        var message = "Test exception message";

        // Act
        var exception = new AMQPRemoteException(message);

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(message, exception.Message);
        _ = Assert.IsType<AMQPRemoteException>(exception);
    }

    [Fact]
    public void AMQPRemoteException_MessageInnerExceptionConstructor_ShouldCreateInstanceWithMessageAndInnerException()
    {
        // Arrange
        var message = "Test exception message";
        var innerException = new Exception("Inner exception");

        // Act
        var exception = new AMQPRemoteException(message, innerException);

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(message, exception.Message);
        Assert.Equal(innerException, exception.InnerException);
        _ = Assert.IsType<AMQPRemoteException>(exception);
    }

    [Fact]
    public void AMQPRemoteException_MessageRemoteStackTraceInnerExceptionConstructor_ShouldCreateInstanceWithMessageRemoteStackTraceAndInnerException()
    {
        // Arrange
        var message = "Test exception message";
        var remoteStackTrace = "Test remote stack trace";
        var innerException = new Exception("Inner exception");

        // Act
        var exception = new AMQPRemoteException(message, remoteStackTrace, innerException);

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(message, exception.Message);
        Assert.Equal(remoteStackTrace, exception.StackTrace);
        Assert.Equal(innerException, exception.InnerException);
        _ = Assert.IsType<AMQPRemoteException>(exception);
    }
}
