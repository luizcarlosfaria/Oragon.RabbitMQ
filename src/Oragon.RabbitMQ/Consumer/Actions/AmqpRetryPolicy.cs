// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// AMQP retry policy helper.
/// </summary>
public sealed class AmqpRetryPolicy
{
    private readonly int maxAttempts;

    private AmqpRetryPolicy(int maxAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);

        this.maxAttempts = maxAttempts;
    }

    /// <summary>
    /// Creates a retry policy based on RabbitMQ delivery count.
    /// </summary>
    /// <param name="maxAttempts">Maximum attempts before terminal nack.</param>
    /// <returns>A retry policy.</returns>
    public static AmqpRetryPolicy ByDeliveryCount(int maxAttempts) => new(maxAttempts);

    /// <summary>
    /// Gets the result for the current attempt.
    /// </summary>
    /// <param name="context">AMQP context.</param>
    /// <returns>Retry or terminal result.</returns>
    public IAmqpResult GetResult(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        long? brokerDeliveryCount = AmqpHeaders.GetDeliveryCount(context.Request.BasicProperties);
        if (!brokerDeliveryCount.HasValue && context.Request.Redelivered)
        {
            throw new InvalidOperationException(
                "RabbitMQ x-delivery-count header is absent on a redelivered message. AmqpRetryPolicy.ByDeliveryCount requires a queue type that exposes broker delivery count, such as quorum queues.");
        }

        long deliveryCount = brokerDeliveryCount.GetValueOrDefault();
        return deliveryCount + 1 < this.maxAttempts
            ? AmqpResults.Reject(true)
            : AmqpResults.Nack(false);
    }
}
