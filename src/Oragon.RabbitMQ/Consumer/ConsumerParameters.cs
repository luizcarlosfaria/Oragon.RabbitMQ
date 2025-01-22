// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Dawn;
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
public class ConsumerParameters : IConsumerParameters
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
    /// Initializes a new instance of the <see cref="IConsumerParameters"/> class.
    /// </summary>
    /// <param name="applicationServiceProvider"></param>
    /// <param name="queueName"></param>
    /// <param name="handler"></param>
    public ConsumerParameters(IServiceProvider applicationServiceProvider, string queueName, Delegate handler)
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
    /// Defaults to <c>null</c>, which will use the value from <see cref="IConnectionFactory.ConsumerDispatchConcurrency"/>
    ///
    /// For concurrency greater than one this removes the guarantee that consumers handle messages in the order they receive them.
    /// In addition to that consumers need to be thread/concurrency safe.
    /// </summary>
    /// <param name="consumerDispatchConcurrency"></param>
    /// <returns>The current instance of <see cref="IConsumerParameters"/>.</returns>
    public IConsumerParameters WithDispatchConcurrency(ushort consumerDispatchConcurrency)
    {
        _ = Guard.Argument(consumerDispatchConcurrency).GreaterThan<ushort>(0);
        _ = Guard.Argument(this.IsLocked).False();

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
    /// <returns>The current instance of <see cref="IConsumerParameters"/>.</returns>
    public IConsumerParameters WithPrefetch(ushort prefetchCount)
    {
        _ = Guard.Argument(prefetchCount).GreaterThan<ushort>(0);
        _ = Guard.Argument(this.IsLocked).False();

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
    /// <returns>The current instance of <see cref="IConsumerParameters"/>.</returns>
    public IConsumerParameters WithConsumerTag(string consumerTag)
    {
        _ = Guard.Argument(consumerTag).NotNull().NotEmpty().NotWhiteSpace();
        _ = Guard.Argument(this.IsLocked).False();

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
    /// <returns>The current instance of <see cref="IConsumerParameters"/>.</returns>
    public IConsumerParameters WithExclusive(bool exclusive = true)
    {
        _ = Guard.Argument(this.IsLocked).False();

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
    /// <returns>The current instance of <see cref="ConsumerParameters"/>.</returns>
    public IConsumerParameters WithConnection(Func<IServiceProvider, CancellationToken, Task<IConnection>> connectionFactory)
    {
        _ = Guard.Argument(connectionFactory).NotNull();
        _ = Guard.Argument(this.IsLocked).False();

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
    /// <returns>The current instance of <see cref="ConsumerParameters"/>.</returns>
    public IConsumerParameters WithSerializer(Func<IServiceProvider, IAmqpSerializer> serializerFactory)
    {
        _ = Guard.Argument(serializerFactory).NotNull();
        _ = Guard.Argument(this.IsLocked).False();

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
    /// <returns>The current instance of <see cref="ConsumerParameters"/>.</returns>
    public IConsumerParameters WithChannel(Func<IConnection, CancellationToken, Task<IChannel>> channelFactory)
    {
        _ = Guard.Argument(channelFactory).NotNull();
        _ = Guard.Argument(this.IsLocked).False();

        this.ChannelFactory = channelFactory;

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
    public IConsumerParameters WhenSerializationFail(Func<IAmqpContext, Exception, IAmqpResult> amqpResult)
    {
        _ = Guard.Argument(amqpResult).NotNull();
        _ = Guard.Argument(this.IsLocked).False();

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
    public IConsumerParameters WhenProcessFail(Func<IAmqpContext, Exception, IAmqpResult> amqpResult)
    {
        _ = Guard.Argument(amqpResult).NotNull();
        _ = Guard.Argument(this.IsLocked).False();

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
        _ = Guard.Argument(this.ApplicationServiceProvider).NotNull();
        _ = Guard.Argument(this.QueueName).NotNull().NotEmpty().NotWhiteSpace();
        _ = Guard.Argument(this.ConsumerDispatchConcurrency).GreaterThan<ushort>(0);
        _ = Guard.Argument(this.Handler).NotNull();
        _ = Guard.Argument(this.ConnectionFactory).NotNull();
        _ = Guard.Argument(this.SerializerFactory).NotNull();
        _ = Guard.Argument(this.ChannelFactory).NotNull();
        _ = Guard.Argument(this.ResultForProcessFailure).NotNull();
        _ = Guard.Argument(this.ResultForSerializationFailure).NotNull();
    }


    /// <summary>
    /// Builds a new instance of <see cref="IHostedAmqpConsumer"/>.
    /// </summary>
    /// <returns></returns>
    public async Task<IHostedAmqpConsumer> BuildConsumerAsync(CancellationToken cancellationToken)
    {
        this.Validate();
        this.IsLocked = true;
        var queueConsumer = new QueueConsumer(this.ApplicationServiceProvider.GetRequiredService<ILogger<QueueConsumer>>(), this);
        await queueConsumer.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return queueConsumer;
    }

    #endregion

}

