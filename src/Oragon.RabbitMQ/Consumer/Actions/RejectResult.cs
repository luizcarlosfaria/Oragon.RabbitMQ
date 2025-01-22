namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Create a instance of RejectResult that will reject the message
/// </summary>
public class RejectResult : IAmqpResult
{

    


    /// <summary>
    /// Define if the message should be requeued
    /// </summary>
    public bool Requeue { get; }


    /// <summary>
    /// Create a instance of RejectResult
    /// </summary>
    /// <param name="requeue"></param>
    internal RejectResult(bool requeue)
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
