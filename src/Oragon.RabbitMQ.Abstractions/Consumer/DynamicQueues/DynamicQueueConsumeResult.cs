// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.DynamicQueues;

/// <summary>
/// Dynamic queue consumption result.
/// </summary>
public sealed record DynamicQueueConsumeResult
{
    /// <summary>
    /// Gets the final status.
    /// </summary>
    public required DynamicQueueConsumeStatus Status { get; init; }

    /// <summary>
    /// Gets the queue name.
    /// </summary>
    public required string QueueName { get; init; }

    /// <summary>
    /// Gets the initial ready message count.
    /// </summary>
    public long? InitialReadyCount { get; init; }

    /// <summary>
    /// Gets the remaining ready message count.
    /// </summary>
    public long? RemainingReadyCount { get; init; }

    /// <summary>
    /// Gets the number of received messages.
    /// </summary>
    public int MessagesReceived { get; init; }

    /// <summary>
    /// Gets the number of acked messages.
    /// </summary>
    public int MessagesAcked { get; init; }

    /// <summary>
    /// Gets the number of nacked messages.
    /// </summary>
    public int MessagesNacked { get; init; }

    /// <summary>
    /// Gets the number of rejected messages.
    /// </summary>
    public int MessagesRejected { get; init; }

    /// <summary>
    /// Gets elapsed time.
    /// </summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    /// Gets a value indicating whether broker canceled the consumer.
    /// </summary>
    public bool BrokerCanceledConsumer { get; init; }

    /// <summary>
    /// Gets a value indicating whether in-flight drain timed out.
    /// </summary>
    public bool InFlightDrainTimedOut { get; init; }

    /// <summary>
    /// Gets the failure exception.
    /// </summary>
    public Exception Exception { get; init; }
}
