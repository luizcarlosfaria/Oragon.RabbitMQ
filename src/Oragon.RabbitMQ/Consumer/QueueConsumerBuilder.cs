// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Dawn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// Builds instances of <see cref="QueueConsumer"/>.
/// </summary>
[GenerateAutomaticInterface]
public class QueueConsumerBuilder : IQueueConsumerBuilder
{
    private bool IsLocked;

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
    /// Gets the handler delegate.
    /// </summary>
    public ushort ConsumerDispatchConcurrency { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IQueueConsumerBuilder"/> class.
    /// </summary>
    /// <param name="applicationServiceProvider"></param>
    /// <param name="queueName"></param>
    /// <param name="handler"></param>
    public QueueConsumerBuilder(IServiceProvider applicationServiceProvider, string queueName, Delegate handler)
    {
        this.ApplicationServiceProvider = applicationServiceProvider;
        this.QueueName = queueName;
        this.Handler = handler;
        this.ConsumerDispatchConcurrency = global::RabbitMQ.Client.Constants.DefaultConsumerDispatchConcurrency;
        this.PrefetchCount = 1;

        this.ConnectionFactory = (sp, ct) => Task.FromResult(sp.GetRequiredService<IConnection>());

        this.SerializerFactory = (sp) => sp.GetRequiredService<IAMQPSerializer>();

        this.ChannelFactory = (connection, ct) =>
            connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: false,
                    publisherConfirmationTrackingEnabled: false,
                    outstandingPublisherConfirmationsRateLimiter: null,
                    consumerDispatchConcurrency: this.ConsumerDispatchConcurrency
                ),
                ct
            );
    }

    /// <summary>
    /// Sets the Consumer Dispatch Concurrency.
    /// </summary>
    /// <param name="consumerDispatchConcurrency"></param>
    /// <returns>The current instance of <see cref="IQueueConsumerBuilder"/>.</returns>
    public IQueueConsumerBuilder WithConsumerDispatchConcurrency(ushort consumerDispatchConcurrency)
    {
        _ = Guard.Argument(consumerDispatchConcurrency).GreaterThan<ushort>(0);
        _ = Guard.Argument(this.IsLocked).False();

        this.ConsumerDispatchConcurrency = consumerDispatchConcurrency;

        return this;
    }

    /// <summary>
    /// Gets the PrefetchCount.
    /// </summary>
    public ushort PrefetchCount { get; private set; }

    /// <summary>
    /// Sets the PrefetchCount.
    /// </summary>
    /// <param name="prefetchCount"></param>
    /// <returns>The current instance of <see cref="IQueueConsumerBuilder"/>.</returns>
    public IQueueConsumerBuilder WithPrefetch(ushort prefetchCount)
    {
        _ = Guard.Argument(prefetchCount).GreaterThan<ushort>(0);
        _ = Guard.Argument(this.IsLocked).False();

        this.PrefetchCount = prefetchCount;

        return this;
    }

    /// <summary>
    /// Gets the ConsumerTag.
    /// </summary>
    public string ConsumerTag { get; private set; }

    /// <summary>
    /// Sets the ConsumerTag.
    /// </summary>
    /// <param name="consumerTag"></param>
    /// <returns>The current instance of <see cref="IQueueConsumerBuilder"/>.</returns>
    public IQueueConsumerBuilder WithConsumerTag(string consumerTag)
    {
        _ = Guard.Argument(consumerTag).NotNull().NotEmpty().NotWhiteSpace();
        _ = Guard.Argument(this.IsLocked).False();

        this.ConsumerTag = consumerTag;

        return this;
    }

    /// <summary>
    /// Gets the handler delegate.
    /// </summary>
    public bool Exclusive { get; private set; }

    /// <summary>
    /// Sets the Exclusive.
    /// </summary>
    /// <param name="exclusive"></param>
    /// <returns>The current instance of <see cref="IQueueConsumerBuilder"/>.</returns>
    public IQueueConsumerBuilder WithExclusive(bool exclusive = true)
    {
        _ = Guard.Argument(this.IsLocked).False();

        this.Exclusive = exclusive;

        return this;
    }

    /// <summary>
    /// Gets the connectionFactory.
    /// </summary>
    public Func<IServiceProvider, CancellationToken, Task<IConnection>> ConnectionFactory { get; private set; }

    /// <summary>
    /// Sets the connection using a factory function.
    /// </summary>
    /// <param name="connectionFactory">The factory function to create the connection.</param>
    /// <returns>The current instance of <see cref="QueueConsumerBuilder"/>.</returns>
    public IQueueConsumerBuilder WithConnection(Func<IServiceProvider, CancellationToken, Task<IConnection>> connectionFactory)
    {
        _ = Guard.Argument(connectionFactory).NotNull();
        _ = Guard.Argument(this.IsLocked).False();

        this.ConnectionFactory = connectionFactory;

        return this;
    }
    /// <summary>
    /// Gets the serializer.
    /// </summary>
    public Func<IServiceProvider, IAMQPSerializer> SerializerFactory { get; private set; }

    /// <summary>
    /// Sets the serializer using a factory function.
    /// </summary>
    /// <param name="serializerFactory">The factory function to create the serializer.</param>
    /// <returns>The current instance of <see cref="QueueConsumerBuilder"/>.</returns>
    public IQueueConsumerBuilder WithSerializer(Func<IServiceProvider, IAMQPSerializer> serializerFactory)
    {
        _ = Guard.Argument(serializerFactory).NotNull();
        _ = Guard.Argument(this.IsLocked).False();

        this.SerializerFactory = serializerFactory;

        return this;
    }

    /// <summary>
    /// Gets the serializer.
    /// </summary>
    public Func<IConnection, CancellationToken, Task<IChannel>> ChannelFactory { get; private set; }

    /// <summary>
    /// Sets the serializer using a factory function.
    /// </summary>
    /// <param name="channelFactory">The factory function to create the channel</param>    
    /// <returns>The current instance of <see cref="QueueConsumerBuilder"/>.</returns>
    public IQueueConsumerBuilder WithChannel(Func<IConnection, CancellationToken, Task<IChannel>> channelFactory)
    {
        _ = Guard.Argument(channelFactory).NotNull();
        _ = Guard.Argument(this.IsLocked).False();

        this.ChannelFactory = channelFactory;

        return this;
    }



    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public void Validate()
    {
        _ = Guard.Argument(this.ApplicationServiceProvider).NotNull();
        _ = Guard.Argument(this.QueueName).NotNull().NotEmpty().NotWhiteSpace();
        _ = Guard.Argument(this.ConsumerDispatchConcurrency).GreaterThan<ushort>(0);
        _ = Guard.Argument(this.Handler).NotNull();
        _ = Guard.Argument(this.ConnectionFactory).NotNull();
        _ = Guard.Argument(this.SerializerFactory).NotNull();
        _ = Guard.Argument(this.ChannelFactory).NotNull();
    }


    /// <summary>
    /// Builds a new instance of <see cref="IHostedAmqpConsumer"/>.
    /// </summary>
    /// <returns></returns>
    public async Task<IHostedAmqpConsumer> BuildAsync(CancellationToken cancellationToken)
    {
        this.Validate();
        this.IsLocked = true;
        var queueConsumer = new QueueConsumer(this.ApplicationServiceProvider.GetRequiredService<ILogger<QueueConsumer>>(), this);
        await queueConsumer.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return queueConsumer;
    }

}

