using Dawn;

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
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual async Task ExecuteAsync(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        await context.Channel.BasicAckAsync(context.Request.DeliveryTag, false).ConfigureAwait(false);
    }
}

