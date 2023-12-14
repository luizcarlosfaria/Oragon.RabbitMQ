using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GagoAspireApp.Architecture.Messaging.Serialization;

public interface IAMQPSerializer
{
    TResponse Deserialize<TResponse>(BasicDeliverEventArgs eventArgs);

    byte[] Serialize<T>(IBasicProperties basicProperties, T objectToSerialize);
}
