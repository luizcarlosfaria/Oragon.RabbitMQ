// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// Builds instances of <see cref="QueueConsumer"/>.
/// </summary>
[GenerateAutomaticInterface]
public class ConsumerDescriptor : IConsumerDescriptor
{
    private const string LockedMessage = "ConsumerDescriptor is locked";

    private volatile bool isLocked;

    /// <summary>
    /// Gets the service provider.
    /// </summary>
    public IServiceProvider ApplicationServiceProvider { get; private set; }

    /// <summary>
    /// Gets the name of the queue.
    /// </summary>
    public string QueueName { get; private set; }

    /// <summary>
    /// Gets the handler delegate.
    /// </summary>
    public Delegate Handler { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IConsumerDescriptor"/> class.
    /// </summary>
    /// <param name="applicationServiceProvider"></param>
    /// <param name="queueName"></param>
    /// <param name="handler"></param>
    public ConsumerDescriptor(IServiceProvider applicationServiceProvider, string queueName, Delegate handler)
    {
        this.ApplicationServiceProvider = applicationServiceProvider;
        this.QueueName = queueName;
        this.Handler = handler;

        _ = this.WithDispatchConcurrency(global::RabbitMQ.Client.Constants.DefaultConsumerDispatchConcurrency)

            .WithPrefetch(1)

            .WhenSerializationFail((amqpContext, exception) => AmqpResults.Reject(false))

            .WhenProcessFail((amqpContext, exception) => AmqpResults.Nack(false))

            .WithConnection((sp, ct) => Task.FromResult(sp.GetRequiredService<IConnection>()))

            .WithSerializer((sp) => sp.GetRequiredService<IAmqpSerializer>())

            .WithChannel((connection, ct) => connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: false,
                    publisherConfirmationTrackingEnabled: false,
                    outstandingPublisherConfirmationsRateLimiter: null,
                    consumerDispatchConcurrency: this.ConsumerDispatchConcurrency
                ),
                ct
            ));
    }

    #region ConsumerDispatchConcurrency / WithDispatchConcurrency(ushort consumerDispatchConcurrency)

    /// <summary>
    /// Gets the handler delegate.
    /// </summary>
    public ushort ConsumerDispatchConcurrency { get; private set; }

    /// <summary>
    /// Set to a value greater than one to enable concurrent processing. For a concurrency greater than one <see cref="IAsyncBasicConsumer"/>
    /// will be offloaded to the worker thread pool so it is important to choose the value for the concurrency wisely to avoid thread pool overloading.
    /// <see cref="IAsyncBasicConsumer"/> can handle concurrency much more efficiently due to the non-blocking nature of the consumer.
    ///
    ///
    /// For concurrency greater than one this removes the guarantee that consumers handle messages in the order they receive them.
    /// In addition to that consumers need to be thread/concurrency safe.
    /// </summary>
    /// <param name="consumerDispatchConcurrency"></param>
    /// <returns>The current instance of <see cref="IConsumerDescriptor"/>.</returns>
    public IConsumerDescriptor WithDispatchConcurrency(ushort consumerDispatchConcurrency)
    {
        if (consumerDispatchConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(consumerDispatchConcurrency));
        if (this.isLocked) throw new InvalidOperationException(LockedMessage);

        this.ConsumerDispatchConcurrency = consumerDispatchConcurrency;

        return this;
    }
    #endregion

    #region PrefetchCount / WithPrefetch(ushort prefetchCount)

    /// <summary>
    /// Gets the PrefetchCount.
    /// </summary>
    public ushort PrefetchCount { get; private set; }

    /// <summary>
    /// Sets the PrefetchCount.
    /// </summary>
    /// <param name="prefetchCount"></param>
    /// <returns>The current instance of <see cref="IConsumerDescriptor"/>.</returns>
    public IConsumerDescriptor WithPrefetch(ushort prefetchCount)
    {
        if (prefetchCount < 1) throw new ArgumentOutOfRangeException(nameof(prefetchCount));
        if (this.isLocked) throw new InvalidOperationException(LockedMessage);

        this.PrefetchCount = prefetchCount;

        return this;
    }
    #endregion

    #region ConsumerTag / WithConsumerTag(string consumerTag)

    /// <summary>
    /// Gets the ConsumerTag.
    /// </summary>
    public string ConsumerTag { get; private set; }

    /// <summary>
    /// Sets the ConsumerTag.
    /// </summary>
    /// <param name="consumerTag"></param>
    /// <returns>The current instance of <see cref="IConsumerDescriptor"/>.</returns>
    public IConsumerDescriptor WithConsumerTag(string consumerTag)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(consumerTag);
        if (this.isLocked) throw new InvalidOperationException(LockedMessage);

        this.ConsumerTag = consumerTag;

        return this;
    }
    #endregion

    #region Exclusive / WithExclusive(bool exclusive = true)
    /// <summary>
    /// Gets the handler delegate.
    /// </summary>
    public bool Exclusive { get; private set; }

    /// <summary>
    /// Sets the Exclusive.
    /// </summary>
    /// <param name="exclusive"></param>
    /// <returns>The current instance of <see cref="IConsumerDescriptor"/>.</returns>
    public IConsumerDescriptor WithExclusive(bool exclusive = true)
    {
        if (this.isLocked) throw new InvalidOperationException(LockedMessage);

        this.Exclusive = exclusive;

        return this;
    }
    #endregion

    #region ConnectionFactory / WithConnection(Func<IServiceProvider, CancellationToken, Task<IConnection>> connectionFactory)
    /// <summary>
    /// Gets the connectionFactory.
    /// </summary>
    public Func<IServiceProvider, CancellationToken, Task<IConnection>> ConnectionFactory { get; private set; }


    /// <summary>
    /// Sets the connection using a factory function.
    /// </summary>
    /// <param name="connectionFactory">The factory function to create the connection.</param>
    /// <returns>Returns the updated consumer descriptor instance.</returns>
    public IConsumerDescriptor WithConnection(Func<IServiceProvider, CancellationToken, Task<IConnection>> connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        if (this.isLocked) throw new InvalidOperationException(LockedMessage);

        this.ConnectionFactory = connectionFactory;

        return this;
    }
    #endregion

    #region SerializerFactory / WithSerializer(Func<IServiceProvider, IAmqpSerializer> serializerFactory)

    /// <summary>
    /// Gets the serializer.
    /// </summary>
    public Func<IServiceProvider, IAmqpSerializer> SerializerFactory { get; private set; }

    /// <summary>
    /// Sets the serializer using a factory function.
    /// </summary>
    /// <param name="serializerFactory">The factory function to create the serializer.</param>
    /// <returns>The current instance of <see cref="ConsumerDescriptor"/>.</returns>
    public IConsumerDescriptor WithSerializer(Func<IServiceProvider, IAmqpSerializer> serializerFactory)
    {
        ArgumentNullException.ThrowIfNull(serializerFactory);
        if (this.isLocked) throw new InvalidOperationException(LockedMessage);

        this.SerializerFactory = serializerFactory;

        return this;
    }
    #endregion

    #region ChannelFactory / WithChannel(Func<IConnection, CancellationToken, Task<IChannel>> channelFactory)
    /// <summary>
    /// Gets the serializer.
    /// </summary>
    public Func<IConnection, CancellationToken, Task<IChannel>> ChannelFactory { get; private set; }

    /// <summary>
    /// Sets the serializer using a factory function.
    /// </summary>
    /// <param name="channelFactory">The factory function to create the channel</param>    
    /// <returns>The current instance of <see cref="ConsumerDescriptor"/>.</returns>
    public IConsumerDescriptor WithChannel(Func<IConnection, CancellationToken, Task<IChannel>> channelFactory)
    {
        ArgumentNullException.ThrowIfNull(channelFactory);
        if (this.isLocked) throw new InvalidOperationException(LockedMessage);

        this.ChannelFactory = channelFactory;

        return this;
    }
    #endregion


    #region TopologyInitializer / WithTopology(Func<IChannel, CancellationToken, Task> channelInitializer)
    /// <summary>
    /// Gets the serializer.
    /// </summary>
    public Func<IChannel, CancellationToken, Task> TopologyInitializer { get; private set; }

    /// <summary>
    /// Configures a channel initializer to be invoked when a channel is created.
    /// </summary>
    /// <param name="topologyInitializer">A delegate that represents the asynchronous operation to initialize the channel.  The delegate receives the
    /// channel to be initialized and a <see cref="CancellationToken"/>  to observe for cancellation.</param>
    /// <returns>The current <see cref="IConsumerDescriptor"/> instance, allowing for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the descriptor is locked and cannot be modified.</exception>
    public IConsumerDescriptor WithTopology(Func<IChannel, CancellationToken, Task> topologyInitializer)
    {
        ArgumentNullException.ThrowIfNull(topologyInitializer);

        if (this.isLocked) throw new InvalidOperationException(LockedMessage);

        this.TopologyInitializer = topologyInitializer;

        return this;
    }
    #endregion

    #region ResultForSerializationFailure / WhenSerializationFail(IAmqpResult amqpResult)

    /// <summary>
    /// Gets the ResultForSerializationFailure.
    /// </summary>
    public Func<IAmqpContext, Exception, IAmqpResult> ResultForSerializationFailure { get; private set; }


    /// <summary>
    /// Define the behavior when the serialization fails.
    /// </summary>
    /// <param name="amqpResult"></param>
    /// <returns></returns>
    public IConsumerDescriptor WhenSerializationFail(Func<IAmqpContext, Exception, IAmqpResult> amqpResult)
    {
        ArgumentNullException.ThrowIfNull(amqpResult);
        if (this.isLocked) throw new InvalidOperationException(LockedMessage);

        this.ResultForSerializationFailure = amqpResult;

        return this;
    }
    #endregion

    #region ResultForProcessFailure / WhenProcessFail(IAmqpResult amqpResult)

    /// <summary>
    /// Gets the ResultForProcessFailure.
    /// </summary>
    public Func<IAmqpContext, Exception, IAmqpResult> ResultForProcessFailure { get; private set; }


    /// <summary>
    /// Define the behavior when the process fails.
    /// </summary>
    /// <param name="amqpResult"></param>
    /// <returns></returns>
    public IConsumerDescriptor WhenProcessFail(Func<IAmqpContext, Exception, IAmqpResult> amqpResult)
    {
        ArgumentNullException.ThrowIfNull(amqpResult);
        if (this.isLocked) throw new InvalidOperationException(LockedMessage);

        this.ResultForProcessFailure = amqpResult;

        return this;
    }
    #endregion

    #region Validate & BuildConsumerAsync(CancellationToken cancellationToken)

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(this.ApplicationServiceProvider);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(this.QueueName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.ConsumerDispatchConcurrency);
        ArgumentNullException.ThrowIfNull(this.Handler);
        ArgumentNullException.ThrowIfNull(this.ConnectionFactory);
        ArgumentNullException.ThrowIfNull(this.SerializerFactory);
        ArgumentNullException.ThrowIfNull(this.ChannelFactory);
        ArgumentNullException.ThrowIfNull(this.ResultForProcessFailure);
        ArgumentNullException.ThrowIfNull(this.ResultForSerializationFailure);
    }


    /// <summary>
    /// Builds a new instance of <see cref="IHostedAmqpConsumer"/>.
    /// </summary>
    /// <returns></returns>
    public async Task<IHostedAmqpConsumer> BuildConsumerAsync(CancellationToken cancellationToken)
    {
        this.Validate();
        this.isLocked = true;
        var queueConsumer = new QueueConsumer(this.ApplicationServiceProvider.GetRequiredService<ILogger<QueueConsumer>>(), this);
        await queueConsumer.InitializeAsync(cancellationToken).ConfigureAwait(true);
        return queueConsumer;
    }
    #endregion

}

