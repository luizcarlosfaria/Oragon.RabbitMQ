// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// Represents the context for AMQP operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AmqpContext"/> class.
/// </remarks>
/// <param name="delivery">The delivery event arguments.</param>
/// <param name="serviceProvider"></param>
/// <param name="serializer"></param>
/// <param name="connection"></param>
/// <param name="channel"></param>
/// <param name="queueName"></param>
/// <param name="message"></param>
/// <param name="cancellationToken"></param>
[GenerateAutomaticInterface]
public class AmqpContext(BasicDeliverEventArgs delivery,
                         IServiceProvider serviceProvider,
                         IAMQPSerializer serializer,
                         IConnection connection,
                         IChannel channel,
                         string queueName,
                         object message,
                         CancellationToken cancellationToken) : IAmqpContext
{

    /// <summary>
    /// Gets the delivery event arguments.
    /// </summary>
    public BasicDeliverEventArgs Request { get; } = delivery;

    /// <summary>
    /// Gets the channel for AMQP operations.
    /// </summary>
    public IChannel Channel { get; } = channel;

    /// <summary>
    /// Gets the connection for AMQP operations.
    /// </summary>
    public IConnection Connection { get; } = connection;

    /// <summary>
    /// Gets the name of the queue.
    /// </summary>
    public string QueueName { get; } = queueName;

    /// <summary>
    /// Gets or sets the message to be sent.
    /// </summary>
    public object MessageObject { get; } = message;

    /// <summary>
    /// Gets the service provider.
    /// </summary>
    public IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <summary>
    /// Gets the serializer.
    /// </summary>
    public IAMQPSerializer Serializer { get; } = serializer;

    /// <summary>
    /// Gets the service provider.
    /// </summary>
    public CancellationToken CancellationToken { get; } = cancellationToken;
}

