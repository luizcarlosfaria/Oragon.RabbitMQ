namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Nack the message
/// </summary>
/// <remarks>
/// Creates a new instance of <see cref="NackResult"/>
/// </remarks>
public class NackResult : IAMQPResult
{
    /// <summary>
    /// Singleton instance of NackResult with requeue
    /// </summary>
    public static NackResult WithRequeue { get; } = new NackResult(true);

    /// <summary>
    /// Singleton instance of NackResult without requeue
    /// </summary>
    public static NackResult WithoutRequeue { get; } = new NackResult(false);


    /// <summary>
    /// Indicates if the message should be requeued
    /// </summary>
    public bool Requeue { get; }

    /// <param name="requeue"></param>
    private NackResult(bool requeue)
    {
        this.Requeue = requeue;
    }


    /// <summary>
    /// Perform ack on channel
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual async Task ExecuteAsync(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        await context.Channel.BasicNackAsync(context.Request.DeliveryTag, false, this.Requeue).ConfigureAwait(false);
    }
}
