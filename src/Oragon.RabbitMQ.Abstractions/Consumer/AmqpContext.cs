// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// Represents the context for Amqp operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AmqpContext"/> class.
/// </remarks>
[GenerateAutomaticInterface]
public class AmqpContext : IAmqpContext
{

    /// <summary>
    /// Gets the delivery event arguments.
    /// </summary>
    public BasicDeliverEventArgs Request { get; }

    /// <summary>
    /// Gets the channel for Amqp operations.
    /// </summary>
    public IChannel Channel { get; }

    /// <summary>
    /// Gets the connection for Amqp operations.
    /// </summary>
    public IConnection Connection { get; }

    /// <summary>
    /// Gets the name of the queue.
    /// </summary>
    public string QueueName { get; }

    /// <summary>
    /// Gets or sets the message to be sent.
    /// </summary>
    public object MessageObject { get; }

    /// <summary>
    /// Gets the service provider.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the serializer.
    /// </summary>
    public IAmqpSerializer Serializer { get; }

    /// <summary>
    /// Gets the service provider.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a new instance of the <see cref="AmqpContext"/> class.
    /// </summary>
    /// <param name="delivery">The delivery event arguments.</param>
    /// <param name="serviceProvider">Application Service provider (under scope)</param>
    /// <param name="serializer"></param>
    /// <param name="connection"></param>
    /// <param name="channel"></param>
    /// <param name="queueName"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    public AmqpContext(BasicDeliverEventArgs delivery,
                             IServiceProvider serviceProvider,
                             IAmqpSerializer serializer,
                             IConnection connection,
                             IChannel channel,
                             string queueName,
                             object message,
                             CancellationToken cancellationToken)
    {
        this.Request = delivery;
        this.Channel = channel;
        this.Connection = connection;
        this.QueueName = queueName;
        this.MessageObject = message;
        this.ServiceProvider = serviceProvider;
        this.Serializer = serializer;
        this.CancellationToken = cancellationToken;
    }
}

