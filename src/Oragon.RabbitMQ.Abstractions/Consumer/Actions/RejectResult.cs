using Dawn;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Create a instance of RejectResult that will reject the message
/// </summary>
/// <param name="requeue"></param>
public class RejectResult(bool requeue) : IAMQPResult
{
    /// <summary>
    /// Define if the message should be requeued
    /// </summary>
    public bool Requeue { get; } = requeue;

    /// <summary>
    /// Perform reject on channel
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="delivery"></param>
    public async Task ExecuteAsync(IChannel channel, BasicDeliverEventArgs delivery)
    {
        _ = Guard.Argument(channel).NotNull();
        _ = Guard.Argument(delivery).NotNull();

        await channel.BasicRejectAsync(delivery.DeliveryTag, this.Requeue).ConfigureAwait(true);
    }
}
