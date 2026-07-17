using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

internal static class StandaloneTopologyDlqDemo
{
    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string queueName = options.ResourceName(demo, "input");
        string dlqName = options.ResourceName(demo, "dlq");
        string exchangeName = options.ResourceName(demo, "exchange");
        string deadLetterExchangeName = options.ResourceName(demo, "dlx");
        const string inputRoutingKey = "work";
        const string deadLetterRoutingKey = "failed";

        int serializationFailures = 0;
        int processFailures = 0;

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"Exchange: {exchangeName}");
        Console.WriteLine($"Queue: {queueName}");
        Console.WriteLine($"DLX: {deadLetterExchangeName}");
        Console.WriteLine($"DLQ: {dlqName}");

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));
        builder.Services.AddRabbitMQConsumer();
        builder.Services.AddSystemTextJsonAmqpSerializer();
        builder.Services.AddSingleton<IConnectionFactory>(_ => RabbitMqDemoClient.CreateConnectionFactory(options));

        IHost host = builder.Build();
        bool hostStarted = false;
        bool hostStopped = false;
        try
        {
            await host.Services.WaitRabbitMQAsync().ConfigureAwait(false);

            _ = host.Services
                .MapQueue(queueName, (StandaloneTopologyDlqMessage body) =>
                {
                    Console.WriteLine($"Handler received: id={body.Id} mode={body.Mode}");
                    if (string.Equals(body.Mode, "throw", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Demo process failure.");
                    }

                    return Task.CompletedTask;
                })
                .WithPrefetch(1)
                .WithConsumerTag("oragon-demo-02-standalone-topology-dlq")
                .WithConnection((services, cancellationToken) =>
                    services.GetRequiredService<IConnectionFactory>()
                        .CreateConnectionAsync(cancellationToken: cancellationToken))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>())
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    await channel.ExchangeDeclareAsync(
                        exchange: exchangeName,
                        type: ExchangeType.Direct,
                        durable: true,
                        autoDelete: false,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    await channel.ExchangeDeclareAsync(
                        exchange: deadLetterExchangeName,
                        type: ExchangeType.Direct,
                        durable: true,
                        autoDelete: false,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueueDeclareAsync(
                        queue: dlqName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    await channel.QueueBindAsync(
                        queue: dlqName,
                        exchange: deadLetterExchangeName,
                        routingKey: deadLetterRoutingKey,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueueDeclareAsync(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: QueueArguments.WithDeadLetter(deadLetterExchangeName, deadLetterRoutingKey),
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    await channel.QueueBindAsync(
                        queue: queueName,
                        exchange: exchangeName,
                        routingKey: inputRoutingKey,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(dlqName, cancellationToken).ConfigureAwait(false);
                    _ = await channel.QueuePurgeAsync(queueName, cancellationToken).ConfigureAwait(false);
                })
                .WhenSerializationFail((context, exception) =>
                {
                    _ = Interlocked.Increment(ref serializationFailures);
                    Console.WriteLine($"Serialization failure routed to DLQ: {exception.GetType().Name}");
                    return AmqpResults.Reject(false);
                })
                .WhenProcessFail((context, exception) =>
                {
                    _ = Interlocked.Increment(ref processFailures);
                    Console.WriteLine($"Process failure routed to DLQ: {exception.GetType().Name}");
                    return AmqpResults.Nack(false);
                });

            await host.StartAsync().ConfigureAwait(false);
            hostStarted = true;

            using IConnection publishConnection = await RabbitMqDemoClient.CreateConnectionAsync(options).ConfigureAwait(false);
            using IChannel publishChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(publishConnection).ConfigureAwait(false);

            await PublishRawAsync(
                publishChannel,
                exchangeName,
                inputRoutingKey,
                Encoding.UTF8.GetBytes("{ invalid-json"),
                "invalid-json").ConfigureAwait(false);

            await PublishRawAsync(
                publishChannel,
                exchangeName,
                inputRoutingKey,
                JsonSerializer.SerializeToUtf8Bytes(new StandaloneTopologyDlqMessage("process-failure", "throw")),
                "process-failure").ConfigureAwait(false);

            QueueDeclareOk dlqState = await WaitForQueueMessageCountAsync(
                publishChannel,
                dlqName,
                expectedCount: 2,
                timeout: TimeSpan.FromSeconds(20)).ConfigureAwait(false);

            await host.StopAsync().ConfigureAwait(false);
            hostStopped = true;

            bool success =
                dlqState.MessageCount == 2
                && serializationFailures == 1
                && processFailures == 1;

            Console.WriteLine($"Serialization failures observed: {serializationFailures}");
            Console.WriteLine($"Process failures observed: {processFailures}");
            Console.WriteLine($"DLQ ready messages: {dlqState.MessageCount}");
            Console.WriteLine(success ? "Demo 02 succeeded." : "Demo 02 failed.");

            return success ? 0 : 1;
        }
        finally
        {
            if (hostStarted && !hostStopped)
            {
                await host.StopAsync().ConfigureAwait(false);
            }

            if (host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                host.Dispose();
            }
        }
    }

    private static async Task PublishRawAsync(
        IChannel channel,
        string exchange,
        string routingKey,
        ReadOnlyMemory<byte> body,
        string messageId)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = messageId,
            DeliveryMode = DeliveryModes.Persistent,
        };

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body).ConfigureAwait(false);
    }

    private static async Task<QueueDeclareOk> WaitForQueueMessageCountAsync(
        IChannel channel,
        string queueName,
        uint expectedCount,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        QueueDeclareOk state;

        do
        {
            state = await channel.QueueDeclarePassiveAsync(queueName).ConfigureAwait(false);
            if (state.MessageCount >= expectedCount)
            {
                return state;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return state;
    }
}

internal sealed record StandaloneTopologyDlqMessage(string Id, string Mode);
