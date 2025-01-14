// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Initializes a new instance of the <see cref="ReplyResult"/> class with the specified reply.
/// </summary>
/// <param name="objectToReply">The reply to be sent.</param>
public class ReplyResult(object objectToReply) : IAMQPResult
{
    /// <summary>
    /// Sends the reply to the sender.
    /// </summary>
    /// <param name="context">The AMQP context in which to send the reply.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ExecuteAsync(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        var replyBasicProperties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString("D"),
            CorrelationId = context.Request.BasicProperties.MessageId
        };

        return context.Channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: context.Request.BasicProperties.ReplyTo,
            mandatory: true,
            basicProperties: replyBasicProperties,
            body: context.Serializer.Serialize(replyBasicProperties, objectToReply)).AsTask();
    }
}
