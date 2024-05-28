using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.Serialization;

/// <summary>
/// Define a implementation of a serializer for AMQP
/// </summary>
public interface IAMQPSerializer
{
    /// <summary>
    /// Desserialize a mesage from a BasicDeliverEventArgs
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="eventArgs"></param>
    /// <returns></returns>
    TMessage Deserialize<TMessage>(BasicDeliverEventArgs eventArgs);


    /// <summary>
    /// Serialize a message to a byte array
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="basicProperties"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    byte[] Serialize<T>(IBasicProperties basicProperties, T message);
}
