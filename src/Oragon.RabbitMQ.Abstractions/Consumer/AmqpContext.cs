// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Extensions.Logging;
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
public partial class AmqpContext : IAmqpContext
{
    private readonly ILogger logger;

    /// <summary>
    /// Gets the delivery event arguments.
    /// </summary>
    public required BasicDeliverEventArgs Request { get; init; }

    /// <summary>
    /// Gets the channel for Amqp operations.
    /// </summary>
    public required IChannel Channel { get; init; }

    /// <summary>
    /// Gets the connection for Amqp operations.
    /// </summary>
    public required IConnection Connection { get; init; }

    /// <summary>
    /// Gets the name of the queue.
    /// </summary>
    public required string QueueName { get; init; }

    /// <summary>
    /// Gets or sets the message to be sent.
    /// </summary>
    public required object MessageObject { get; init; }

    /// <summary>
    /// Gets the service provider.
    /// </summary>
    public required IServiceProvider ServiceProvider { get; init; }

    /// <summary>
    /// Gets the serializer.
    /// </summary>
    public required IAmqpSerializer Serializer { get; init; }

    /// <summary>
    /// Gets the service provider.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a new instance of the <see cref="AmqpContext"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="cancellationToken"></param>
    public AmqpContext(ILogger logger,
                             CancellationToken cancellationToken)
    {
        this.logger = logger;

        this.CancellationToken = cancellationToken;
    }


    [LoggerMessage(
    EventId = 3,
    Level = LogLevel.Warning,
    Message = "Exception on handle '{messageType}' from '{queueName}' \r\nContent:\r\n {content} \r\nException:\r\n {exception}")]
    private partial void LogExceptionInternal(string messageType, string queueName, string content, string exception);

    /// <summary>
    /// Logs an exception along with relevant message type and content from the request body.
    /// </summary>
    /// <param name="exception">Handles cases where the provided exception is null.</param>
    public void LogException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception, nameof(exception));

        var messageType = this.MessageObject?.GetType().FullName ?? string.Empty;
        var messageContent = Encoding.UTF8.GetString(this.Request?.Body.ToArray()) ?? string.Empty;

        this.LogExceptionInternal(messageType, this.QueueName, messageContent, exception.ToString());
    }
}

