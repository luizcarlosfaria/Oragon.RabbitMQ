using DotNet.Testcontainers.Builders;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Oragon.RabbitMQ.Benchmarks.Infrastructure;

public static class RabbitMqFixture
{
    private const string ContainerImage = "rabbitmq:4-management-alpine";

    private static readonly SemaphoreSlim s_lock = new(1, 1);
    private static RabbitMqContainer s_container;
    private static ConnectionFactory s_connectionFactory;

    public static async Task EnsureStartedAsync()
    {
        if (s_container != null) return;

        await s_lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (s_container != null) return;

            s_container = new RabbitMqBuilder(ContainerImage)
                .WithDockerEndpoint(Environment.OSVersion.Platform == PlatformID.Unix
                    ? "unix:///var/run/docker.sock"
                    : Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? "tcp://localhost:2375"
                    : throw new NotSupportedException("Unsupported platform"))
                .WithImage(ContainerImage)
                .WithExposedPort(15672)
                .WithWaitStrategy(
                    Wait
                    .ForUnixContainer()
                    .UntilInternalTcpPortIsAvailable(15672, it => it
                        .WithTimeout(TimeSpan.FromSeconds(120))
                        .WithRetries(20)
                        .WithInterval(TimeSpan.FromSeconds(3))
                    )
                )
                .Build();

            await s_container.StartAsync().ConfigureAwait(false);

            s_connectionFactory = new ConnectionFactory
            {
                Uri = new Uri(s_container.GetConnectionString())
            };
        }
        finally
        {
            _ = s_lock.Release();
        }
    }

    public static async Task<IConnection> CreateConnectionAsync()
    {
        if (s_connectionFactory == null)
            throw new InvalidOperationException("RabbitMQ fixture not started. Call EnsureStartedAsync first.");

        return await s_connectionFactory.CreateConnectionAsync().ConfigureAwait(false);
    }

    public static string GenerateQueueName() => $"bench-{Guid.NewGuid():N}";

    public static async Task PreloadQueueAsync(IConnection connection, string queueName, int messageCount, ReadOnlyMemory<byte> serializedBody)
    {
        using IChannel channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true
            )).ConfigureAwait(false);

        _ = await channel.QueueDeclareAsync(queueName, durable: false, exclusive: false, autoDelete: false).ConfigureAwait(false);

        for (int i = 0; i < messageCount; i++)
        {
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: false,
                body: serializedBody).ConfigureAwait(false);
        }

        await channel.CloseAsync().ConfigureAwait(false);
    }

    public static async Task<uint> GetQueueMessageCountAsync(IConnection connection, string queueName)
    {
        using IChannel channel = await connection.CreateChannelAsync().ConfigureAwait(false);
        QueueDeclareOk result = await channel.QueueDeclarePassiveAsync(queueName).ConfigureAwait(false);
        await channel.CloseAsync().ConfigureAwait(false);
        return result.MessageCount;
    }

    public static async Task DeleteQueueAsync(IConnection connection, string queueName)
    {
        try
        {
            using IChannel channel = await connection.CreateChannelAsync().ConfigureAwait(false);
            _ = await channel.QueueDeleteAsync(queueName).ConfigureAwait(false);
            await channel.CloseAsync().ConfigureAwait(false);
        }
        catch
        {
            // Queue may already be deleted
        }
    }

    public static async Task WarmupAsync()
    {
        await EnsureStartedAsync().ConfigureAwait(false);
        using IConnection connection = await CreateConnectionAsync().ConfigureAwait(false);
        string warmupQueue = GenerateQueueName();

        using IChannel channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true
            )).ConfigureAwait(false);

        _ = await channel.QueueDeclareAsync(warmupQueue, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);

        byte[] body = System.Text.Encoding.UTF8.GetBytes("{\"Id\":1,\"Value\":\"warmup\"}");
        for (int i = 0; i < 10; i++)
        {
            await channel.BasicPublishAsync(string.Empty, warmupQueue, false, body).ConfigureAwait(false);
        }

        _ = await channel.QueueDeleteAsync(warmupQueue).ConfigureAwait(false);
        await channel.CloseAsync().ConfigureAwait(false);
    }
}
