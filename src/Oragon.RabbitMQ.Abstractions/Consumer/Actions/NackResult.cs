using Dawn;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Nack the message
/// </summary>
public class NackResult : IAMQPResult
{

    /// <summary>
    /// Indicates if the message should be requeued
    /// </summary>
    public bool Requeue { get; }

    /// <summary>
    /// Creates a new instance of <see cref="NackResult"/>
    /// </summary>
    /// <param name="requeue"></param>
    public NackResult(bool requeue)
    {
        this.Requeue = requeue;
    }

    /// <summary>
    /// Perform nack on channel
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="delivery"></param>
    /// <returns></returns>
    public async Task Execute(IChannel channel, BasicDeliverEventArgs delivery)
    {
        _ = Guard.Argument(channel).NotNull();
        _ = Guard.Argument(delivery).NotNull();

        await channel.BasicNackAsync(delivery.DeliveryTag, false, this.Requeue).ConfigureAwait(true);
    }
}
