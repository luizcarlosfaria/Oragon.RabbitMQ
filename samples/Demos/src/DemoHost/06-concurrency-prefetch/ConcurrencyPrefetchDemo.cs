using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

internal static class ConcurrencyPrefetchDemo
{
    private const int MessageCount = 4;

    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string sequentialQueue = options.ResourceName(demo, "sequential");
        string parallelQueue = options.ResourceName(demo, "parallel");
        var sequentialProbe = new ConcurrencyProbe(expectedMessages: MessageCount);
        var parallelProbe = new ConcurrencyProbe(expectedMessages: MessageCount);

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"Sequential queue: {sequentialQueue}");
        Console.WriteLine($"Parallel queue: {parallelQueue}");

        using IConnection connection = await RabbitMqDemoClient.CreateConnectionAsync(options).ConfigureAwait(false);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));
        builder.Services.AddRabbitMQConsumer();
        builder.Services.AddSystemTextJsonAmqpSerializer();
        builder.Services.AddSingleton(connection);

        IHost host = builder.Build();
        bool hostStarted = false;
        bool hostStopped = false;
        try
        {
            _ = host.Services
                .MapQueue(sequentialQueue, (ConcurrencyMessage message) =>
                    sequentialProbe.HandleAsync("sequential", message))
                .WithPrefetch(1)
                .WithDispatchConcurrency(1)
                .WithConsumerTag("oragon-demo-06-sequential")
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredService<IConnection>()))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>())
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    _ = await channel.QueueDeclareAsync(
                        queue: sequentialQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(sequentialQueue, cancellationToken).ConfigureAwait(false);
                });

            _ = host.Services
                .MapQueue(parallelQueue, (ConcurrencyMessage message) =>
                    parallelProbe.HandleAsync("parallel", message))
                .WithPrefetch(8)
                .WithDispatchConcurrency(4)
                .WithConsumerTag("oragon-demo-06-parallel")
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredService<IConnection>()))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>())
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    _ = await channel.QueueDeclareAsync(
                        queue: parallelQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(parallelQueue, cancellationToken).ConfigureAwait(false);
                });

            await host.StartAsync().ConfigureAwait(false);
            hostStarted = true;

            using IChannel publishChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);
            await PublishMessagesAsync(
                publishChannel,
                sequentialQueue,
                [
                    new ConcurrencyMessage(1, 120),
                    new ConcurrencyMessage(2, 90),
                    new ConcurrencyMessage(3, 60),
                    new ConcurrencyMessage(4, 30),
                ]).ConfigureAwait(false);

            await PublishMessagesAsync(
                publishChannel,
                parallelQueue,
                [
                    new ConcurrencyMessage(1, 240),
                    new ConcurrencyMessage(2, 160),
                    new ConcurrencyMessage(3, 80),
                    new ConcurrencyMessage(4, 20),
                ]).ConfigureAwait(false);

            await sequentialProbe.Completed.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            await parallelProbe.Completed.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            QueueDeclareOk sequentialState = await WaitForReadyCountAsync(
                publishChannel,
                sequentialQueue,
                expectedCount: 0,
                timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            QueueDeclareOk parallelState = await WaitForReadyCountAsync(
                publishChannel,
                parallelQueue,
                expectedCount: 0,
                timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            await host.StopAsync().ConfigureAwait(false);
            hostStopped = true;

            int[] expectedOrder = [1, 2, 3, 4];
            int[] sequentialFinishOrder = sequentialProbe.FinishOrder.ToArray();
            int[] parallelFinishOrder = parallelProbe.FinishOrder.ToArray();

            var failures = new List<string>();
            Check(failures, "sequentialMaxConcurrency", sequentialProbe.MaxConcurrency == 1, sequentialProbe.MaxConcurrency);
            Check(failures, "sequentialStartOrder", sequentialProbe.StartOrder.ToArray().SequenceEqual(expectedOrder), FormatOrder(sequentialProbe.StartOrder));
            Check(failures, "sequentialFinishOrder", sequentialFinishOrder.SequenceEqual(expectedOrder), FormatOrder(sequentialProbe.FinishOrder));
            Check(failures, "parallelMaxConcurrency", parallelProbe.MaxConcurrency >= 2, parallelProbe.MaxConcurrency);
            Check(failures, "parallelFinishOrder", !parallelFinishOrder.SequenceEqual(expectedOrder), FormatOrder(parallelProbe.FinishOrder));
            Check(failures, "sequentialReady", sequentialState.MessageCount == 0, sequentialState.MessageCount);
            Check(failures, "parallelReady", parallelState.MessageCount == 0, parallelState.MessageCount);

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            Console.WriteLine($"Sequential start order: {FormatOrder(sequentialProbe.StartOrder)}");
            Console.WriteLine($"Sequential finish order: {FormatOrder(sequentialProbe.FinishOrder)}");
            Console.WriteLine($"Sequential max concurrency: {sequentialProbe.MaxConcurrency}");
            Console.WriteLine($"Parallel start order: {FormatOrder(parallelProbe.StartOrder)}");
            Console.WriteLine($"Parallel finish order: {FormatOrder(parallelProbe.FinishOrder)}");
            Console.WriteLine($"Parallel max concurrency: {parallelProbe.MaxConcurrency}");
            Console.WriteLine($"Sequential ready messages: {sequentialState.MessageCount}");
            Console.WriteLine($"Parallel ready messages: {parallelState.MessageCount}");
            Console.WriteLine(failures.Count == 0 ? "Demo 06 succeeded." : "Demo 06 failed.");

            return failures.Count == 0 ? 0 : 1;
        }
        catch (TimeoutException exception)
        {
            Console.Error.WriteLine($"Timed out waiting for concurrency demo: {exception.Message}");
            return 1;
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

    private static async Task PublishMessagesAsync(
        IChannel channel,
        string queueName,
        IReadOnlyList<ConcurrencyMessage> messages)
    {
        foreach (ConcurrencyMessage message in messages)
        {
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = $"concurrency-{message.Sequence}",
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: JsonSerializer.SerializeToUtf8Bytes(message)).ConfigureAwait(false);
        }
    }

    private static async Task<QueueDeclareOk> WaitForReadyCountAsync(
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
            if (state.MessageCount == expectedCount)
            {
                return state;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return state;
    }

    private static string FormatOrder(ConcurrentQueue<int> order) => string.Join(",", order.ToArray());

    private static void Check(List<string> failures, string name, bool success, object? actual)
    {
        if (!success)
        {
            failures.Add($"{name} mismatch. Actual: {actual ?? "(null)"}");
        }
    }
}

internal sealed record ConcurrencyMessage(int Sequence, int DelayMs);

internal sealed class ConcurrencyProbe
{
    private readonly int expectedMessages;
    private int completedMessages;
    private int currentConcurrency;
    private int maxConcurrency;

    public ConcurrencyProbe(int expectedMessages)
    {
        this.expectedMessages = expectedMessages;
    }

    public ConcurrentQueue<int> StartOrder { get; } = new();

    public ConcurrentQueue<int> FinishOrder { get; } = new();

    public TaskCompletionSource Completed { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int MaxConcurrency => Volatile.Read(ref this.maxConcurrency);

    public async Task HandleAsync(string label, ConcurrencyMessage message)
    {
        this.StartOrder.Enqueue(message.Sequence);
        int current = Interlocked.Increment(ref this.currentConcurrency);
        UpdateMax(ref this.maxConcurrency, current);

        Console.WriteLine($"{label} start {message.Sequence} delay={message.DelayMs}ms current={current}");
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(message.DelayMs)).ConfigureAwait(false);
            this.FinishOrder.Enqueue(message.Sequence);
            Console.WriteLine($"{label} finish {message.Sequence}");
        }
        finally
        {
            _ = Interlocked.Decrement(ref this.currentConcurrency);
            if (Interlocked.Increment(ref this.completedMessages) == this.expectedMessages)
            {
                _ = this.Completed.TrySetResult();
            }
        }
    }

    private static void UpdateMax(ref int target, int value)
    {
        while (true)
        {
            int current = Volatile.Read(ref target);
            if (value <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref target, value, current) == current)
            {
                return;
            }
        }
    }
}
