namespace Oragon.RabbitMQ;



/// <summary>
/// Represents errors that occur during remote operations.
/// </summary>
[Serializable]
public class AmqpRemoteException : Exception
{
    private readonly string remoteStackTrace;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmqpRemoteException"/> class.
    /// </summary>
    public AmqpRemoteException() : this(message: null, remoteStackTrace: null, inner: null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AmqpRemoteException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public AmqpRemoteException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AmqpRemoteException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public AmqpRemoteException(string message, Exception inner) : base(message, inner) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AmqpRemoteException"/> class with a specified error message, a reference to the remote stack trace, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="remoteStackTrace">The remote stack trace.</param>
    /// <param name="inner">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public AmqpRemoteException(string message, string remoteStackTrace, Exception inner) : base(message, inner)
    {
        this.remoteStackTrace = remoteStackTrace;
    }

    /// <summary>
    /// Gets the string representation of the frames on the call stack at the time the current exception was thrown.
    /// </summary>
    public override string StackTrace => this.remoteStackTrace;

}
