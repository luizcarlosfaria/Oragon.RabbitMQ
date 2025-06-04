// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Represents the result of forwarding objects to a RabbitMQ exchange with a specified routing key.
/// </summary>
/// <remarks>This class encapsulates the details required to forward messages to a RabbitMQ exchange, including
/// the exchange name, routing key,  mandatory flag, and the objects to be forwarded. It is designed to be used in
/// AMQP-based messaging scenarios.</remarks>
/// <typeparam name="T">The type of objects being forwarded.</typeparam>
public class ForwardResult<T> : IAmqpResult
{

    private readonly T[] objectsToForward;


    /// <summary>
    /// Initializes a new instance of the <see cref="ForwardResult{T}"/> class, representing the result of forwarding
    /// objects to a specified exchange and routing key.
    /// </summary>
    /// <param name="exchange">The name of the exchange to which the objects will be forwarded. Cannot be <see langword="null"/>.</param>
    /// <param name="routingKey">The routing key used to route the forwarded objects. Cannot be <see langword="null"/>.</param>
    /// <param name="mandatory">A value indicating whether the forwarding operation is mandatory. If <see langword="true"/>, the operation
    /// requires confirmation that the message was routed successfully.</param>
    /// <param name="replyTo">An optional reply-to address for responses. Can be <see langword="null"/> if no reply-to address is specified.</param>
    /// <param name="objectsToForward">The objects to be forwarded. Cannot be <see langword="null"/> and must contain at least one object.</param>
    internal ForwardResult(string exchange, string routingKey, bool mandatory, string replyTo = null, params T[] objectsToForward)
    {
        ArgumentNullException.ThrowIfNull(exchange, nameof(exchange));
        ArgumentNullException.ThrowIfNull(routingKey, nameof(routingKey));
        ArgumentNullException.ThrowIfNull(objectsToForward, nameof(objectsToForward));

        this.Exchange = exchange;
        this.RoutingKey = routingKey;
        this.Mandatory = mandatory;
        this.ReplyTo = replyTo;
        this.objectsToForward = objectsToForward;
    }

    /// <summary>
    /// Gets the name of the name of exchange on RabbitMQ that will receive this messages.
    /// </summary>
    public string Exchange { get; }

    /// <summary>
    /// Gets the routing key used to delivery the message.
    /// </summary>
    public string RoutingKey { get; }

    /// <summary>
    /// Gets a value indicating whether the associated item is mandatory.
    /// </summary>
    public bool Mandatory { get; }

    /// <summary>
    /// Gets the address to which replies should be sent.
    /// </summary>
    public string ReplyTo { get; }


    /// <summary>
    /// Executes the forwarding operation for the specified AMQP context.
    /// </summary>
    /// <remarks>This method processes the provided <paramref name="context"/> and forwards objects, if any, 
    /// to the reply queue specified in the context's request properties. Each forwarded object is  serialized and
    /// published as a message with a unique message ID and correlation ID.</remarks>
    /// <param name="context">The AMQP context containing the request and channel information.  This parameter cannot be <see
    /// langword="null"/>.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        if (this.objectsToForward != null && this.objectsToForward.Length != 0)
        {
            foreach (T objectToForward in this.objectsToForward)
            {
                await this.ForwardMessage(context, objectToForward).ConfigureAwait(true);
            }
        }
    }

    private Task ForwardMessage(IAmqpContext context, T objectToForward)
    {
        var forwardBasicProperties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString("D"),
            CorrelationId = context.Request.BasicProperties.MessageId,
            ReplyTo = this.ReplyTo
        };

        return context.Channel.BasicPublishAsync(
            exchange: this.Exchange,
            routingKey: this.RoutingKey,
            mandatory: this.Mandatory,
            basicProperties: forwardBasicProperties,
            body: context.Serializer.Serialize(forwardBasicProperties, objectToForward)).AsTask();
    }
}
