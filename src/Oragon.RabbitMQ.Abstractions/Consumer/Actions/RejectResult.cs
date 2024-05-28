using Dawn;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// 
/// </summary>
public class RejectResult : IAMQPResult
{
    /// <summary>
    /// 
    /// </summary>
    public bool Requeue { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="requeue"></param>
    public RejectResult(bool requeue)
    {
        this.Requeue = requeue;
    }

    /// <summary>
    /// Perform reject on channel
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="delivery"></param>
    public async Task Execute(IChannel channel, BasicDeliverEventArgs delivery)
    {
        _ = Guard.Argument(channel).NotNull();
        _ = Guard.Argument(delivery).NotNull();

        await channel.BasicRejectAsync(delivery.DeliveryTag, this.Requeue).ConfigureAwait(true);
    }
}
