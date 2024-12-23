using RabbitMQ.Client;
using Oragon.RabbitMQ.Serialization;
using System.Diagnostics.CodeAnalysis;
using Dawn;

namespace Oragon.RabbitMQ.Publisher;

#nullable enable

/// <summary>
/// Basic publisher for RabbitMQ.
/// </summary>
/// <param name="connection"></param>
/// <param name="serializer"></param>
public class MessagePublisher(IConnection connection, IAMQPSerializer serializer)
{
    //private static readonly ActivitySource? s_activitySource = new(MessagingTelemetryNames.GetName(nameof(MessagePublisher)));
    //private static readonly TextMapPropagator s_propagator = Propagators.DefaultTextMapPropagator;

    private readonly IAMQPSerializer serializer = serializer;
    private readonly IConnection connection = connection;

    /// <summary>
    /// Send a message to the RabbitMQ.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="exchange"></param>
    /// <param name="routingKey"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    [SuppressMessage("Usage", "CA2201", Justification = "Do not raise reserved exception types")]
    public async Task SendAsync<T>(string exchange, string routingKey, T message, CancellationToken cancellationToken)
    {
        using IChannel model = await this.connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        await this.SendAsync(model, exchange, routingKey, message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a message to the RabbitMQ.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="channel"></param>
    /// <param name="exchange"></param>
    /// <param name="routingKey"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    [SuppressMessage("Usage", "CA2201", Justification = "Do not raise reserved exception types")]
    public async Task SendAsync<T>(IChannel channel, string exchange, string routingKey, T message, CancellationToken cancellationToken)
    {
        Guard.Argument(channel).NotNull();

        BasicProperties properties = channel.CreateBasicProperties().EnsureHeaders().SetDurable(true);

        var body = this.serializer.Serialize(basicProperties: properties, message: message);

        await channel.BasicPublishAsync(exchange, routingKey, false, properties, body, cancellationToken).ConfigureAwait(false);
    }


}
