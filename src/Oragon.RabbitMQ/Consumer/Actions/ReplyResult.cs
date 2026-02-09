// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer.Actions;


/// <summary>
/// Represents a result that sends a reply to the sender.
/// </summary>
/// <typeparam name="T">The type of the object to reply with.</typeparam>
public class ReplyResult<T> : IAmqpResult
{

    private readonly T objectToReply;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplyResult{T}"/> class.
    /// </summary>
    /// <param name="objectToReply">The object to reply with.</param>
    internal ReplyResult(T objectToReply)
    {
        this.objectToReply = objectToReply;
    }

    /// <summary>
    /// Sends the reply to the sender.
    /// </summary>
    /// <param name="context">The Amqp context in which to send the reply.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        string replyTo = context.Request.BasicProperties.ReplyTo;
        if (string.IsNullOrWhiteSpace(replyTo))
        {
            throw new InvalidOperationException(
                $"Cannot reply to message {context.Request.BasicProperties.MessageId}: ReplyTo property is not set.");
        }

        // Create a dedicated channel for reply to avoid race conditions
        // when the consumer channel is used concurrently
        using IChannel replyChannel = await context.Connection.CreateChannelAsync().ConfigureAwait(true);

        try
        {
            var replyBasicProperties = new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = Guid.NewGuid().ToString("D"),
                CorrelationId = context.Request.BasicProperties.MessageId ?? context.Request.BasicProperties.CorrelationId,
            };

            await replyChannel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: replyTo,
                mandatory: true,
                basicProperties: replyBasicProperties,
                body: context.Serializer.Serialize(replyBasicProperties, this.objectToReply)).ConfigureAwait(true);
        }
        finally
        {
            await replyChannel.CloseAsync().ConfigureAwait(true);
        }
    }
}
