namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Acknowledges the message
/// </summary>
public class AckResult : IAMQPResult
{
    /// <summary>
    /// Singleton instance of AckResult
    /// </summary>
    public static AckResult ForSuccess { get; } = new AckResult();


    /// <summary>
    /// Create a instance of AckResult
    /// </summary>
    private AckResult() { }


    /// <summary>
    /// Perform ack on channel
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual async Task ExecuteAsync(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        await context.Channel.BasicAckAsync(context.Request.DeliveryTag, false).ConfigureAwait(false);
    }
}

