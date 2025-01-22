namespace Oragon.RabbitMQ.UnitTests;

public class AmqpRemoteExceptionTests
{
    [Fact]
    public void AmqpRemoteException_DefaultConstructor_ShouldCreateInstance()
    {
        // Arrange

        // Act
        var exception = new AmqpRemoteException();

        // Assert
        Assert.NotNull(exception);
        _ = Assert.IsType<AmqpRemoteException>(exception);
    }

    [Fact]
    public void AmqpRemoteException_MessageConstructor_ShouldCreateInstanceWithMessage()
    {
        // Arrange
        var message = "Test exception message";

        // Act
        var exception = new AmqpRemoteException(message);

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(message, exception.Message);
        _ = Assert.IsType<AmqpRemoteException>(exception);
    }

    [Fact]
    public void AmqpRemoteException_MessageInnerExceptionConstructor_ShouldCreateInstanceWithMessageAndInnerException()
    {
        // Arrange
        var message = "Test exception message";
        var innerException = new Exception("Inner exception");

        // Act
        var exception = new AmqpRemoteException(message, innerException);

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(message, exception.Message);
        Assert.Equal(innerException, exception.InnerException);
        _ = Assert.IsType<AmqpRemoteException>(exception);
    }

    [Fact]
    public void AmqpRemoteException_MessageRemoteStackTraceInnerExceptionConstructor_ShouldCreateInstanceWithMessageRemoteStackTraceAndInnerException()
    {
        // Arrange
        var message = "Test exception message";
        var remoteStackTrace = "Test remote stack trace";
        var innerException = new Exception("Inner exception");

        // Act
        var exception = new AmqpRemoteException(message, remoteStackTrace, innerException);

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(message, exception.Message);
        Assert.Equal(remoteStackTrace, exception.StackTrace);
        Assert.Equal(innerException, exception.InnerException);
        _ = Assert.IsType<AmqpRemoteException>(exception);
    }
}
