// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Republishes the current message to the tail of a queue.
/// </summary>
public sealed class RequeueToTailResult : IAmqpResult
{
    // x-delivery-count is quorum-queue delivery state (feeds the broker delivery-limit and
    // AmqpRetryPolicy.ByDeliveryCount); carrying it into a fresh publish would fake failed
    // deliveries. Every other header, including x-death history, is preserved.
    private const string DeliveryCountHeader = "x-delivery-count";

    private readonly RequeueToTailOptions options;

    internal RequeueToTailResult(RequeueToTailOptions options)
    {
        this.options = options ?? new RequeueToTailOptions();
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string targetQueueName = string.IsNullOrWhiteSpace(this.options.QueueName)
            ? context.QueueName
            : this.options.QueueName;

        using IChannel publishChannel = await context.Connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken: context.CancellationToken).ConfigureAwait(true);

        try
        {
            BasicProperties properties = CopyProperties(context.Request.BasicProperties, this.options);

            // mandatory: false — the delivery guarantee is broker receipt (publisher confirms);
            // a missing target queue must not fail the requeue.
            await publishChannel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: targetQueueName,
                mandatory: false,
                basicProperties: properties,
                body: context.Request.Body,
                cancellationToken: context.CancellationToken).ConfigureAwait(true);
        }
        finally
        {
            await publishChannel.CloseAsync(cancellationToken: CancellationToken.None).ConfigureAwait(true);
        }

    }

    private static BasicProperties CopyProperties(IReadOnlyBasicProperties input, RequeueToTailOptions options)
    {
        var output = new BasicProperties();
        AmqpPropertyCopy copy = options.CopyProperties;

        if (copy.HasFlag(AmqpPropertyCopy.Content))
        {
            output.ContentType = input.ContentType;
            output.ContentEncoding = input.ContentEncoding;
        }

        if (copy.HasFlag(AmqpPropertyCopy.DeliveryMode))
        {
            output.DeliveryMode = input.DeliveryMode;
        }

        if (copy.HasFlag(AmqpPropertyCopy.Priority))
        {
            output.Priority = input.Priority;
        }

        if (copy.HasFlag(AmqpPropertyCopy.MessageId))
        {
            output.MessageId = input.MessageId;
        }

        if (copy.HasFlag(AmqpPropertyCopy.CorrelationId))
        {
            output.CorrelationId = input.CorrelationId;
        }

        if (copy.HasFlag(AmqpPropertyCopy.Type))
        {
            output.Type = input.Type;
        }

        if (copy.HasFlag(AmqpPropertyCopy.Reply))
        {
            output.ReplyTo = input.ReplyTo;
        }

        if (copy.HasFlag(AmqpPropertyCopy.Expiration))
        {
            output.Expiration = input.Expiration;
        }

        if (copy.HasFlag(AmqpPropertyCopy.Timestamp))
        {
            output.Timestamp = input.Timestamp;
        }

        if (copy.HasFlag(AmqpPropertyCopy.UserId))
        {
            output.UserId = input.UserId;
        }

        if (copy.HasFlag(AmqpPropertyCopy.AppId))
        {
            output.AppId = input.AppId;
        }

        if (copy.HasFlag(AmqpPropertyCopy.ClusterId))
        {
            output.ClusterId = input.ClusterId;
        }

        if (copy.HasFlag(AmqpPropertyCopy.Headers) && input.Headers != null)
        {
            output.Headers = input.Headers
                .Where(header => !string.Equals(header.Key, DeliveryCountHeader, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(header => header.Key, header => header.Value, StringComparer.Ordinal);
        }

        options.ConfigureProperties?.Invoke(input, output);

        return output;
    }
}
