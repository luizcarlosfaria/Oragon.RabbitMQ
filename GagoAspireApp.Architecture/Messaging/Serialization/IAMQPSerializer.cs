using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;

namespace GagoAspireApp.Architecture.Messaging.Serialization;

public interface IAMQPSerializer
{
    TResponse Deserialize<TResponse>(BasicDeliverEventArgs eventArgs);

    byte[] Serialize<T>(IBasicProperties basicProperties, T objectToSerialize);
}
