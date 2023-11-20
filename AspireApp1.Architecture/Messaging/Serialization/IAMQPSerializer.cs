using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AspireApp1.Architecture.Messaging.Serialization;

public interface IAMQPSerializer
{
    TResponse Deserialize<TResponse>(BasicDeliverEventArgs eventArgs);

    byte[] Serialize<T>(IBasicProperties basicProperties, T objectToSerialize);
}
