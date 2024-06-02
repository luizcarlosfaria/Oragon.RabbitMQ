using Dawn;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Nack the message
/// </summary>
/// <remarks>
/// Creates a new instance of <see cref="NackResult"/>
/// </remarks>
/// <param name="requeue"></param>
public class NackResult(bool requeue) : IAMQPResult
{

    /// <summary>
    /// Indicates if the message should be requeued
    /// </summary>
    public bool Requeue { get; } = requeue;

    /// <summary>
    /// Perform nack on channel
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="delivery"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(IChannel channel, BasicDeliverEventArgs delivery)
    {
        _ = Guard.Argument(channel).NotNull();
        _ = Guard.Argument(delivery).NotNull();

        await channel.BasicNackAsync(delivery.DeliveryTag, false, this.Requeue).ConfigureAwait(true);
    }
}
