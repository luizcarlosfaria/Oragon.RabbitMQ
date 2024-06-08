// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Dawn;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// Represents the parameters for the consumer base class.
/// </summary>
public class ConsumerBaseParameters
{
    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    public string QueueName { get; private set; }

    /// <summary>
    /// Sets the queue name.
    /// </summary>
    /// <param name="queueName">The queue name.</param>
    /// <returns>The updated instance of <see cref="ConsumerBaseParameters"/>.</returns>
    public ConsumerBaseParameters WithQueueName(string queueName)
    {
        this.QueueName = queueName;
        return this;
    }

    /// <summary>
    /// Gets or sets the configurer action for topology.
    /// </summary>
    public Func<IServiceProvider, IChannel, Task> Configurer { get; private set; }

    /// <summary>
    /// Sets the configurer action for topology.
    /// </summary>
    /// <param name="configurer">The configurer action for topology.</param>
    /// <returns>The updated instance of <see cref="ConsumerBaseParameters"/>.</returns>
    public ConsumerBaseParameters WithTopology(Func<IServiceProvider, IChannel, Task> configurer)
    {
        this.Configurer = configurer;
        return this;
    }

    /// <summary>
    /// Gets or sets the prefetch count.
    /// </summary>
    public ushort PrefetchCount { get; private set; }

    /// <summary>
    /// Sets the prefetch count.
    /// </summary>
    /// <param name="prefetchCount">The prefetch count.</param>
    /// <returns>The updated instance of <see cref="ConsumerBaseParameters"/>.</returns>
    public ConsumerBaseParameters WithPrefetchCount(ushort prefetchCount)
    {
        this.PrefetchCount = prefetchCount;
        return this;
    }

    /// <summary>
    /// Gets or sets the connection factory function.
    /// </summary>
    public Func<IServiceProvider, IConnection> ConnectionFactoryFunc { get; private set; }

    /// <summary>
    /// Sets the connection factory function.
    /// </summary>
    /// <param name="connectionFactoryFunc">The connection factory function.</param>
    /// <returns>The updated instance of <see cref="ConsumerBaseParameters"/>.</returns>
    public ConsumerBaseParameters WithConnectionFactoryFunc(Func<IServiceProvider, IConnection> connectionFactoryFunc)
    {
        this.ConnectionFactoryFunc = connectionFactoryFunc;
        return this;
    }

    /// <summary>
    /// Gets or sets the test queue retry count.
    /// </summary>
    public int TestQueueRetryCount { get; private set; }

    /// <summary>
    /// Sets the test queue retry count.
    /// </summary>
    /// <param name="testQueueRetryCount">The test queue retry count.</param>
    /// <returns>The updated instance of <see cref="ConsumerBaseParameters"/>.</returns>
    public ConsumerBaseParameters WithTestQueueRetryCount(int testQueueRetryCount)
    {
        this.TestQueueRetryCount = testQueueRetryCount;
        return this;
    }

    /// <summary>
    /// Gets or sets the display loop in console every time span.
    /// </summary>
    public TimeSpan DisplayLoopInConsoleEvery { get; private set; }

    /// <summary>
    /// Sets the display loop in console every time span.
    /// </summary>
    /// <param name="timeToDisplay">The time span to display.</param>
    /// <returns>The updated instance of <see cref="ConsumerBaseParameters"/>.</returns>
    public ConsumerBaseParameters WithDisplayLoopInConsoleEvery(TimeSpan timeToDisplay)
    {
        this.DisplayLoopInConsoleEvery = timeToDisplay;
        return this;
    }

    /// <summary>
    /// Validates the consumer base parameters.
    /// </summary>
    public virtual void Validate()
    {
        _ = Guard.Argument(this.QueueName).NotNull("QueueName can't be null").NotEmpty("QueueName can't be empty").NotWhiteSpace("QueueName can't be whitespaces");
        _ = Guard.Argument(this.PrefetchCount).NotZero("PrefetchCount must be greater than zero").NotNegative((_) => "PrefetchCount must be greater than zero");
        _ = Guard.Argument(this.TestQueueRetryCount).NotNegative((_) => "TestQueueRetryCount must be greater than zero");
        _ = Guard.Argument(this.ConnectionFactoryFunc).NotNull("ConnectionFactoryFunc can't be null");
    }
}
