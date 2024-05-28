using RabbitMQ.Client.Events;
using RabbitMQ.Client;

namespace DotNetAspire.Architecture.Messaging.Consumer.Actions;


/// <summary>
/// Represents a AMQP result that can be executed by the consumer after a message is processed.
/// </summary>
public interface IAMQPResult
{

    /// <summary>
    /// Executes the result.
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="delivery"></param>
    Task Execute(IChannel channel, BasicDeliverEventArgs delivery);
}
