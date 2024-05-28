namespace Oragon.RabbitMQ;


[Serializable]
public class AMQPRemoteException : Exception
{
    private readonly string remoteStackTrace;

    public AMQPRemoteException() : this(message: null, remoteStackTrace: null, inner: null) { }
    public AMQPRemoteException(string message) : base(message) { }
    public AMQPRemoteException(string message, Exception inner) : base(message, inner) { }
    public AMQPRemoteException(string message, string remoteStackTrace, Exception inner) : base(message, inner) { this.remoteStackTrace = remoteStackTrace; }


    public override string StackTrace => remoteStackTrace;


    protected AMQPRemoteException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
}
