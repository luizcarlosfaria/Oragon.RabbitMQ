using System.Text.Json;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

public static class RabbitMqDemoClient
{
    public static ConnectionFactory CreateConnectionFactory(DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new ConnectionFactory
        {
            Uri = new Uri(options.AmqpUri),
            ClientProvidedName = "oragon-rabbitmq-demos",
        };
    }

    public static Task<IConnection> CreateConnectionAsync(
        DemoOptions options,
        CancellationToken cancellationToken = default)
    {
        return CreateConnectionFactory(options).CreateConnectionAsync(cancellationToken);
    }

    public static async Task<IChannel> CreatePublishChannelAsync(
        IConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task DeclareDurableQueueAsync(
        IChannel channel,
        string queueName,
        IDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        _ = await channel.QueueDeclareAsync(
            queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public static async Task PublishJsonAsync<T>(
        IChannel channel,
        string queueName,
        T message,
        BasicProperties? properties = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(message);

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: true,
            basicProperties: properties ?? new BasicProperties(),
            body: body,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public static async Task PurgeQueueAsync(
        IChannel channel,
        string queueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        _ = await channel.QueuePurgeAsync(queueName, cancellationToken).ConfigureAwait(false);
    }
}
