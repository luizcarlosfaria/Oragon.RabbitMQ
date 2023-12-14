using Dawn;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;

namespace GagoAspireApp.Architecture.Messaging.Serialization;

public abstract class AMQPBaseSerializer : IAMQPSerializer
{
    private readonly ActivitySource activitySource;
    private readonly string name;

    public AMQPBaseSerializer(ActivitySource activitySource, string name)
    {
        this.activitySource = activitySource;
        this.name = name;
    }

    protected abstract TResponse DeserializeInternal<TResponse>(IBasicProperties basicProperties, ReadOnlyMemory<byte> body);

    protected abstract byte[] SerializeInternal<T>(IBasicProperties basicProperties, T objectToSerialize);


    public TResponse Deserialize<TResponse>(BasicDeliverEventArgs eventArgs)
    {
        Guard.Argument(eventArgs).NotNull();
        Guard.Argument(eventArgs.BasicProperties).NotNull();

        TResponse? returnValue = this.DeserializeInternal<TResponse>(eventArgs.BasicProperties, eventArgs.Body);

        return returnValue;
    }

    public byte[] Serialize<T>(IBasicProperties basicProperties, T objectToSerialize)
    {
        Guard.Argument(basicProperties).NotNull();
        
        byte[] returnValue = this.SerializeInternal(basicProperties, objectToSerialize); ;
        
        return returnValue;
    }
}
