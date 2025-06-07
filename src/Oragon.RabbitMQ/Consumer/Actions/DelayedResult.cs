using RabbitMQ.Client;
using System.Text;
using System.Threading.Tasks;

namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Delays the message by publishing it to a delayed queue with a TTL.
/// </summary>
public class DelayedResult : IAMQPResult
{
    private readonly object message;
    private readonly TimeSpan ttl;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelayedResult"/> class with the specified message and TTL.
    /// </summary>
    /// <param name="message">The message to be delayed.</param>
    /// <param name="ttl">The TTL (Time-To-Live) as a TimeSpan.</param>
    public DelayedResult(object message, TimeSpan ttl)
    {
        this.message = message;
        this.ttl = ttl;
    }

    /// <summary>
    /// Executes the result by publishing the message to the delayed queue and acknowledging the original message.
    /// </summary>
    /// <param name="context">The AMQP context in which to execute the result.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        var delayedQueueName = $"{context.QueueName}-delayed";
        var properties = context.Channel.CreateBasicProperties();

        // Copy all properties from old BasicProperties to new BasicProperties
        properties.AppId = context.Request.BasicProperties.AppId;
        properties.ClusterId = context.Request.BasicProperties.ClusterId;
        properties.ContentEncoding = context.Request.BasicProperties.ContentEncoding;
        properties.ContentType = context.Request.BasicProperties.ContentType;
        properties.CorrelationId = context.Request.BasicProperties.CorrelationId;
        properties.DeliveryMode = context.Request.BasicProperties.DeliveryMode;
        properties.Expiration = context.Request.BasicProperties.Expiration;
        properties.Headers = context.Request.BasicProperties.Headers;
        properties.MessageId = context.Request.BasicProperties.MessageId;
        properties.Persistent = context.Request.BasicProperties.Persistent;
        properties.Priority = context.Request.BasicProperties.Priority;
        properties.ReplyTo = context.Request.BasicProperties.ReplyTo;
        properties.Timestamp = context.Request.BasicProperties.Timestamp;
        properties.Type = context.Request.BasicProperties.Type;
        properties.UserId = context.Request.BasicProperties.UserId;

        // Only TTL will be changed
        properties.Expiration = ((int)this.ttl.TotalMilliseconds).ToString();

        var body = context.Serializer.Serialize(properties, this.message);

        await context.Channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: delayedQueueName,
            mandatory: true,
            basicProperties: properties,
            body: body).AsTask();

        await context.Channel.BasicAckAsync(context.Request.DeliveryTag, false).ConfigureAwait(false);
    }
}
