using Dawn;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Acknowledges the message
/// </summary>
public class AckResult : IAMQPResult
{
    /// <summary>
    /// Create a instance of AckResult
    /// </summary>
    public AckResult() { }


    /// <summary>
    /// Perform ack on channel
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="delivery"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(IChannel channel, BasicDeliverEventArgs delivery)
    {
        _ = Guard.Argument(channel).NotNull();
        _ = Guard.Argument(delivery).NotNull();

        await channel.BasicAckAsync(delivery.DeliveryTag, false).ConfigureAwait(true);
    }
}
