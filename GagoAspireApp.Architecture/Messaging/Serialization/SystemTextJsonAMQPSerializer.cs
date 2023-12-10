using RabbitMQ.Client;
using System;
using System.Diagnostics;
using System.Text;

namespace GagoAspireApp.Architecture.Messaging.Serialization;


public class SystemTextJsonAMQPSerializer : AMQPBaseSerializer
{

    public SystemTextJsonAMQPSerializer(ActivitySource activitySource) : base(activitySource, nameof(SystemTextJsonAMQPSerializer)) { }


    protected override TResponse DeserializeInternal<TResponse>(IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
    {
        string message = Encoding.UTF8.GetString(body.ToArray());
        return System.Text.Json.JsonSerializer.Deserialize<TResponse>(message);
    }

    protected override byte[] SerializeInternal<T>(IBasicProperties basicProperties, T objectToSerialize)
    {
        return Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(objectToSerialize));
    }
}

