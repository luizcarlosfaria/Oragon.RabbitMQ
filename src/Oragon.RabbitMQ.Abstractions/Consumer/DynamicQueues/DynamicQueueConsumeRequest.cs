// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ.Consumer.Actions;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer.DynamicQueues;

/// <summary>
/// Request used by <see cref="IAmqpDynamicQueueConsumer"/>.
/// </summary>
/// <typeparam name="T">Message body type.</typeparam>
public sealed class DynamicQueueConsumeRequest<T>
{
    /// <summary>
    /// Gets the queue name to consume.
    /// </summary>
    public required string QueueName { get; init; }

    /// <summary>
    /// Gets the explicit connection to use. When absent, the current AMQP context connection is used when available.
    /// </summary>
    public IConnection Connection { get; init; }

    /// <summary>
    /// Gets an optional connection factory used when <see cref="Connection"/> is not provided.
    /// </summary>
    public Func<IServiceProvider, CancellationToken, ValueTask<IConnection>> ConnectionFactory { get; init; }

    /// <summary>
    /// Gets an optional channel factory.
    /// </summary>
    public Func<IServiceProvider, IConnection, CancellationToken, ValueTask<IChannel>> ChannelFactory { get; init; }

    /// <summary>
    /// Gets the prefetch count.
    /// </summary>
    public ushort PrefetchCount { get; init; } = 1;

    /// <summary>
    /// Gets the maximum number of messages to process.
    /// </summary>
    public int? MaxMessages { get; init; }

    /// <summary>
    /// Gets the maximum total duration.
    /// </summary>
    public TimeSpan? MaxDuration { get; init; }

    /// <summary>
    /// Gets the maximum idle time without deliveries.
    /// </summary>
    public TimeSpan? IdleTimeout { get; init; }

    /// <summary>
    /// Gets a value indicating whether the consumer stops after processing the initial ready count.
    /// </summary>
    public bool StopAfterInitialQueueLength { get; init; }

    /// <summary>
    /// Gets the maximum local message concurrency.
    /// </summary>
    public ushort MaxLocalConcurrency { get; init; } = 1;

    /// <summary>
    /// Gets the maximum time to wait for in-flight handlers after a stop rule is reached.
    /// </summary>
    public TimeSpan InFlightDrainTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets optional metadata passed to extension hooks.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; }

    /// <summary>
    /// Gets a hook executed before the dynamic consumer starts consuming messages.
    /// </summary>
    public Func<DynamicQueueStartContext, CancellationToken, ValueTask<DynamicQueueStartDecision>> BeforeStartAsync { get; init; }

    /// <summary>
    /// Gets a hook executed after the dynamic consumer stops.
    /// </summary>
    public Func<DynamicQueueStopContext, CancellationToken, ValueTask> AfterStopAsync { get; init; }

    /// <summary>
    /// Gets the message handler.
    /// </summary>
    public required Func<T, IAmqpContext, Task<IAmqpResult>> OnMessageAsync { get; init; }
}
