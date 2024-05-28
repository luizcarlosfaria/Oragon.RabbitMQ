using Dawn;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;

namespace DotNetAspire.Architecture.Messaging.Consumer.Actions;

/// <summary>
/// Acknowledges the message
/// </summary>
public class AckResult : IAMQPResult
{

    /// <summary>
    /// Perform ack on channel
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="delivery"></param>
    /// <returns></returns>
    public async Task Execute(IChannel channel, BasicDeliverEventArgs delivery)
    {
        _ = Guard.Argument(channel).NotNull();
        _ = Guard.Argument(delivery).NotNull();

        await channel.BasicAckAsync(delivery.DeliveryTag, false).ConfigureAwait(true);
    }
}