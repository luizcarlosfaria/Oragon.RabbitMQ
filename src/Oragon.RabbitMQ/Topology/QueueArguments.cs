// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ;

/// <summary>
/// Helpers for common RabbitMQ queue arguments.
/// </summary>
public sealed class QueueArguments : Dictionary<string, object>
{
    /// <summary>Queue type argument key.</summary>
    public const string QueueTypeKey = "x-queue-type";

    /// <summary>Single active consumer argument key.</summary>
    public const string SingleActiveConsumerKey = "x-single-active-consumer";

    /// <summary>Dead-letter exchange argument key.</summary>
    public const string DeadLetterExchangeKey = "x-dead-letter-exchange";

    /// <summary>Dead-letter routing key argument key.</summary>
    public const string DeadLetterRoutingKeyKey = "x-dead-letter-routing-key";

    /// <summary>Maximum priority argument key.</summary>
    public const string MaxPriorityKey = "x-max-priority";

    /// <summary>RabbitMQ quorum queue type value.</summary>
    public const string QuorumQueueType = "quorum";

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueArguments"/> class.
    /// </summary>
    public QueueArguments()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    /// <summary>
    /// Creates an empty queue argument set.
    /// </summary>
    /// <returns>Queue arguments.</returns>
    public static QueueArguments Empty() => new();

    /// <summary>
    /// Creates queue arguments for a quorum queue.
    /// </summary>
    /// <returns>Queue arguments.</returns>
    public static QueueArguments Quorum() => Empty().WithQuorum();

    /// <summary>
    /// Creates queue arguments for single active consumer.
    /// </summary>
    /// <returns>Queue arguments.</returns>
    public static QueueArguments SingleActiveConsumer() => Empty().WithSingleActiveConsumer();

    /// <summary>
    /// Creates queue arguments for dead-lettering.
    /// </summary>
    /// <param name="exchange">Dead-letter exchange.</param>
    /// <param name="routingKey">Optional dead-letter routing key.</param>
    /// <param name="arguments">Optional existing argument set to mutate.</param>
    /// <returns>Queue arguments.</returns>
    public static QueueArguments WithDeadLetter(
        string exchange,
        string routingKey = null,
        QueueArguments arguments = null) =>
        (arguments ?? Empty()).WithDeadLetter(exchange, routingKey);

    /// <summary>
    /// Creates queue arguments for priority queues.
    /// </summary>
    /// <param name="maxPriority">Maximum priority.</param>
    /// <param name="arguments">Optional existing argument set to mutate.</param>
    /// <returns>Queue arguments.</returns>
    public static QueueArguments WithMaxPriority(byte maxPriority, QueueArguments arguments = null) =>
        (arguments ?? Empty()).WithMaxPriority(maxPriority);

    /// <summary>
    /// Marks the queue as quorum.
    /// </summary>
    /// <returns>The current argument set.</returns>
    public QueueArguments WithQuorum()
    {
        this[QueueTypeKey] = QuorumQueueType;
        return this;
    }

    /// <summary>
    /// Enables single active consumer.
    /// </summary>
    /// <returns>The current argument set.</returns>
    public QueueArguments WithSingleActiveConsumer()
    {
        this[SingleActiveConsumerKey] = true;
        return this;
    }

    /// <summary>
    /// Configures dead-lettering.
    /// </summary>
    /// <param name="exchange">Dead-letter exchange.</param>
    /// <param name="routingKey">Optional dead-letter routing key.</param>
    /// <returns>The current argument set.</returns>
    public QueueArguments WithDeadLetter(string exchange, string routingKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);

        this[DeadLetterExchangeKey] = exchange;
        if (routingKey != null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);
            this[DeadLetterRoutingKeyKey] = routingKey;
        }

        return this;
    }

    /// <summary>
    /// Configures maximum message priority.
    /// </summary>
    /// <param name="maxPriority">Maximum priority.</param>
    /// <returns>The current argument set.</returns>
    public QueueArguments WithMaxPriority(byte maxPriority)
    {
        ArgumentOutOfRangeException.ThrowIfZero(maxPriority);

        this[MaxPriorityKey] = maxPriority;
        return this;
    }
}
