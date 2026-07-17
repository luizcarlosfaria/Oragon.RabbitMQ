// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.DynamicQueues;

/// <summary>
/// Dynamic queue consumption status.
/// </summary>
public enum DynamicQueueConsumeStatus
{
    /// <summary>
    /// Consumption completed.
    /// </summary>
    Completed,

    /// <summary>
    /// The queue was empty.
    /// </summary>
    Empty,

    /// <summary>
    /// Start hook skipped the cycle.
    /// </summary>
    Skipped,

    /// <summary>
    /// Start hook deferred the cycle.
    /// </summary>
    Deferred,

    /// <summary>
    /// The queue does not exist.
    /// </summary>
    QueueMissing,

    /// <summary>
    /// Maximum message count reached.
    /// </summary>
    MaxMessagesReached,

    /// <summary>
    /// Maximum total duration reached.
    /// </summary>
    MaxDurationReached,

    /// <summary>
    /// Idle timeout reached.
    /// </summary>
    IdleTimeoutReached,

    /// <summary>
    /// Initial ready count reached.
    /// </summary>
    InitialQueueLengthReached,

    /// <summary>
    /// Consumption was interrupted by cancellation.
    /// </summary>
    Interrupted,

    /// <summary>
    /// Consumption failed.
    /// </summary>
    Faulted,
}
