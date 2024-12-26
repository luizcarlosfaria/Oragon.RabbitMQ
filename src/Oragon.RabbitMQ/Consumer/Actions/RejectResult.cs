namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Create a instance of RejectResult that will reject the message
/// </summary>
public class RejectResult : IAMQPResult
{

    /// <summary>
    /// Singleton instance of RejectResult with requeue
    /// </summary>
    public static RejectResult WithRequeue { get; } = new RejectResult(true);

    /// <summary>
    /// Singleton instance of RejectResult without requeue
    /// </summary>
    public static RejectResult WithoutRequeue { get; } = new RejectResult(false);



    /// <summary>
    /// Define if the message should be requeued
    /// </summary>
    public bool Requeue { get; }


    /// <summary>
    /// Create a instance of RejectResult
    /// </summary>
    /// <param name="requeue"></param>
    private RejectResult(bool requeue)
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

        await context.Channel.BasicRejectAsync(context.Request.DeliveryTag, this.Requeue).ConfigureAwait(false);
    }
}
