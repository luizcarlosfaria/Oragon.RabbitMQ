using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using Oragon.RabbitMQ.Consumer.DynamicQueues;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

internal static class AttentionWithPrimitivesDemo
{
    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string attentionQueue = options.ResourceName(demo, "attention");
        string noisyWorkQueue = options.ResourceName(demo, "work.noisy");
        string smallWorkQueue = options.ResourceName(demo, "work.small");
        var state = new AttentionDemoState(expectedWorkMessages: 6);

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"Attention queue: {attentionQueue}");
        Console.WriteLine($"Noisy work queue: {noisyWorkQueue}");
        Console.WriteLine($"Small work queue: {smallWorkQueue}");

        using IConnection connection = await RabbitMqDemoClient.CreateConnectionAsync(options).ConfigureAwait(false);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));
        builder.Services.AddRabbitMQConsumer();
        builder.Services.AddSystemTextJsonAmqpSerializer();
        builder.Services.AddSingleton(connection);
        builder.Services.AddSingleton<IAmqpConcurrencyGate, InMemoryAttentionGate>();
        builder.Services.AddSingleton(state);

        IHost host = builder.Build();
        bool hostStarted = false;
        bool hostStopped = false;
        try
        {
            _ = host.Services
                .MapQueue(attentionQueue, async Task<IAmqpResult> (
                    AttentionSignal attention,
                    [FromServices] IAmqpDynamicQueueConsumer dynamicConsumer,
                    [FromServices] IAmqpConcurrencyGate gate,
                    [FromServices] AttentionDemoState demoState,
                    CancellationToken cancellationToken) =>
                {
                    demoState.AttentionOrder.Enqueue(attention.EntityId);
                    string gateKey = $"attention:{attention.EntityId}";

                    IAmqpConcurrencyLease lease = await gate.TryAcquireAsync(
                        new AmqpConcurrencyGateRequest(
                            Key: gateKey,
                            LeaseTime: TimeSpan.FromSeconds(30),
                            Metadata: new Dictionary<string, object>
                            {
                                ["entityId"] = attention.EntityId,
                                ["attentionId"] = attention.AttentionId,
                            }),
                        cancellationToken).ConfigureAwait(false);

                    try
                    {
                        if (!lease.Acquired)
                        {
                            _ = Interlocked.Increment(ref demoState.GateBlockedAttempts);
                            Console.WriteLine($"Attention blocked by gate: {attention.EntityId}");
                            return AmqpResults.Ack();
                        }

                        DynamicQueueConsumeResult result = await dynamicConsumer.ConsumeAsync(
                            new DynamicQueueConsumeRequest<AttentionWorkMessage>
                            {
                                QueueName = attention.WorkQueueName,
                                Connection = connection,
                                PrefetchCount = 1,
                                MaxLocalConcurrency = 1,
                                MaxMessages = attention.MaxMessages,
                                IdleTimeout = TimeSpan.FromMilliseconds(150),
                                Metadata = new Dictionary<string, object>
                                {
                                    ["entityId"] = attention.EntityId,
                                    ["attentionId"] = attention.AttentionId,
                                },
                                BeforeStartAsync = (context, ct) =>
                                {
                                    demoState.DynamicStartInitialCounts.Enqueue(
                                        $"{attention.EntityId}:{context.InitialReadyCount}");
                                    return ValueTask.FromResult(DynamicQueueStartDecision.Allow());
                                },
                                AfterStopAsync = (context, ct) =>
                                {
                                    demoState.DynamicStopStatuses.Enqueue(
                                        $"{attention.EntityId}:{context.Result.Status}:{context.Result.RemainingReadyCount}");
                                    return ValueTask.CompletedTask;
                                },
                                OnMessageAsync = async (work, context) =>
                                {
                                    await Task.Delay(TimeSpan.FromMilliseconds(80), context.CancellationToken).ConfigureAwait(false);
                                    demoState.RecordWork(attention.EntityId, work.Id);
                                    return AmqpResults.Ack();
                                },
                            },
                            cancellationToken).ConfigureAwait(false);

                        bool hasMoreWork = result.RemainingReadyCount > 0
                            || result.Status == DynamicQueueConsumeStatus.MaxMessagesReached
                            || result.Status == DynamicQueueConsumeStatus.MaxDurationReached
                            || result.Status == DynamicQueueConsumeStatus.InitialQueueLengthReached
                            || result.Status == DynamicQueueConsumeStatus.Interrupted;

                        if (hasMoreWork)
                        {
                            _ = Interlocked.Increment(ref demoState.AttentionRequeues);
                            Console.WriteLine($"Attention requeued to tail: {attention.EntityId} remaining={result.RemainingReadyCount}");
                            return AmqpResults.RequeueToTail();
                        }

                        Console.WriteLine($"Attention completed: {attention.EntityId} status={result.Status}");
                        return AmqpResults.Ack();
                    }
                    finally
                    {
                        await lease.DisposeAsync().ConfigureAwait(false);
                    }
                })
                .WithPrefetch(4)
                .WithDispatchConcurrency(4)
                .WithConsumerTag("oragon-demo-14-attention")
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredService<IConnection>()))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>())
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    await DeclareAndPurgeAsync(channel, attentionQueue, cancellationToken).ConfigureAwait(false);
                    await DeclareAndPurgeAsync(channel, noisyWorkQueue, cancellationToken).ConfigureAwait(false);
                    await DeclareAndPurgeAsync(channel, smallWorkQueue, cancellationToken).ConfigureAwait(false);
                })
                .WithGracefulShutdown(options =>
                {
                    options.CancelContextTokenOnStop = true;
                    options.WaitForInFlightMessages = true;
                    options.DrainTimeout = TimeSpan.FromSeconds(5);
                });

            await host.StartAsync().ConfigureAwait(false);
            hostStarted = true;

            using IChannel publishChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);
            await PublishWorkAsync(publishChannel, noisyWorkQueue, "noisy", count: 5).ConfigureAwait(false);
            await PublishWorkAsync(publishChannel, smallWorkQueue, "small", count: 1).ConfigureAwait(false);
            await PublishAttentionAsync(publishChannel, attentionQueue, new AttentionSignal("noisy-1", "noisy", noisyWorkQueue, MaxMessages: 2)).ConfigureAwait(false);
            await PublishAttentionAsync(publishChannel, attentionQueue, new AttentionSignal("noisy-duplicate", "noisy", noisyWorkQueue, MaxMessages: 2)).ConfigureAwait(false);
            await PublishAttentionAsync(publishChannel, attentionQueue, new AttentionSignal("small-1", "small", smallWorkQueue, MaxMessages: 10)).ConfigureAwait(false);

            await state.AllWorkProcessed.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            await WaitForReadyCountAsync(publishChannel, noisyWorkQueue, expectedCount: 0, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            await WaitForReadyCountAsync(publishChannel, smallWorkQueue, expectedCount: 0, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            QueueDeclareOk attentionState = await WaitForReadyCountAsync(
                publishChannel,
                attentionQueue,
                expectedCount: 0,
                TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            await host.StopAsync().ConfigureAwait(false);
            hostStopped = true;

            string[] attentionOrder = state.AttentionOrder.ToArray();
            string[] workOrder = state.WorkOrder.ToArray();
            var failures = new List<string>();
            Check(failures, "totalWork", state.TotalWorkProcessed == 6, state.TotalWorkProcessed);
            Check(failures, "noisyWork", state.NoisyWorkProcessed == 5, state.NoisyWorkProcessed);
            Check(failures, "smallWork", state.SmallWorkProcessed == 1, state.SmallWorkProcessed);
            Check(failures, "gateBlocked", state.GateBlockedAttempts >= 1, state.GateBlockedAttempts);
            Check(failures, "attentionRequeues", state.AttentionRequeues >= 1, state.AttentionRequeues);
            Check(failures, "dynamicHooks", !state.DynamicStartInitialCounts.IsEmpty && !state.DynamicStopStatuses.IsEmpty, "missing hook data");
            Check(failures, "attentionReady", attentionState.MessageCount == 0, attentionState.MessageCount);
            Check(failures, "usesNoMapAttentionQueue", true, "MapAttentionQueue is intentionally absent");

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            Console.WriteLine($"Attention order: {string.Join(",", attentionOrder)}");
            Console.WriteLine($"Work order: {string.Join(",", workOrder)}");
            Console.WriteLine($"Gate blocked attempts: {state.GateBlockedAttempts}");
            Console.WriteLine($"Attention requeues: {state.AttentionRequeues}");
            Console.WriteLine($"Dynamic start counts: {string.Join(",", state.DynamicStartInitialCounts)}");
            Console.WriteLine($"Dynamic stop statuses: {string.Join(",", state.DynamicStopStatuses)}");
            Console.WriteLine($"Attention ready messages: {attentionState.MessageCount}");
            Console.WriteLine(failures.Count == 0 ? "Demo 14 succeeded." : "Demo 14 failed.");

            return failures.Count == 0 ? 0 : 1;
        }
        catch (TimeoutException exception)
        {
            Console.Error.WriteLine($"Timed out waiting for attention primitives demo: {exception.Message}");
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

    private static async Task DeclareAndPurgeAsync(
        IChannel channel,
        string queueName,
        CancellationToken cancellationToken)
    {
        _ = await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _ = await channel.QueuePurgeAsync(queueName, cancellationToken).ConfigureAwait(false);
    }

    private static async Task PublishWorkAsync(
        IChannel channel,
        string queueName,
        string entityId,
        int count)
    {
        for (int index = 1; index <= count; index++)
        {
            var message = new AttentionWorkMessage($"{entityId}-work-{index}", entityId);
            await PublishJsonAsync(channel, queueName, message, $"work-{entityId}-{index}").ConfigureAwait(false);
        }
    }

    private static Task PublishAttentionAsync(
        IChannel channel,
        string attentionQueue,
        AttentionSignal attention)
    {
        return PublishJsonAsync(channel, attentionQueue, attention, $"attention-{attention.AttentionId}");
    }

    private static async Task PublishJsonAsync<T>(
        IChannel channel,
        string queueName,
        T message,
        string messageId)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = messageId,
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: true,
            basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(message)).ConfigureAwait(false);
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

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return state;
    }

    private static void Check(List<string> failures, string name, bool success, object? actual)
    {
        if (!success)
        {
            failures.Add($"{name} mismatch. Actual: {actual ?? "(null)"}");
        }
    }
}

internal sealed record AttentionSignal(
    string AttentionId,
    string EntityId,
    string WorkQueueName,
    int MaxMessages);

internal sealed record AttentionWorkMessage(string Id, string EntityId);

internal sealed class AttentionDemoState
{
    private readonly int expectedWorkMessages;
    private int totalWorkProcessed;
    private int noisyWorkProcessed;
    private int smallWorkProcessed;

    public AttentionDemoState(int expectedWorkMessages)
    {
        this.expectedWorkMessages = expectedWorkMessages;
    }

    public ConcurrentQueue<string> AttentionOrder { get; } = new();

    public ConcurrentQueue<string> WorkOrder { get; } = new();

    public ConcurrentQueue<string> DynamicStartInitialCounts { get; } = new();

    public ConcurrentQueue<string> DynamicStopStatuses { get; } = new();

    public TaskCompletionSource AllWorkProcessed { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int GateBlockedAttempts;

    public int AttentionRequeues;

    public int TotalWorkProcessed => Volatile.Read(ref this.totalWorkProcessed);

    public int NoisyWorkProcessed => Volatile.Read(ref this.noisyWorkProcessed);

    public int SmallWorkProcessed => Volatile.Read(ref this.smallWorkProcessed);

    public void RecordWork(string entityId, string workId)
    {
        this.WorkOrder.Enqueue($"{entityId}:{workId}");

        if (entityId == "noisy")
        {
            _ = Interlocked.Increment(ref this.noisyWorkProcessed);
        }
        else if (entityId == "small")
        {
            _ = Interlocked.Increment(ref this.smallWorkProcessed);
        }

        if (Interlocked.Increment(ref this.totalWorkProcessed) == this.expectedWorkMessages)
        {
            _ = this.AllWorkProcessed.TrySetResult();
        }
    }
}

internal sealed class InMemoryAttentionGate : IAmqpConcurrencyGate
{
    private readonly ConcurrentDictionary<string, byte> keys = new(StringComparer.Ordinal);

    public ValueTask<IAmqpConcurrencyLease> TryAcquireAsync(
        AmqpConcurrencyGateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool acquired = this.keys.TryAdd(request.Key, 0);
        return ValueTask.FromResult<IAmqpConcurrencyLease>(
            new InMemoryAttentionLease(this.keys, request.Key, acquired));
    }
}

internal sealed class InMemoryAttentionLease : IAmqpConcurrencyLease
{
    private readonly ConcurrentDictionary<string, byte> keys;
    private int disposed;

    public InMemoryAttentionLease(
        ConcurrentDictionary<string, byte> keys,
        string key,
        bool acquired)
    {
        this.keys = keys;
        this.Key = key;
        this.Acquired = acquired;
    }

    public bool Acquired { get; }

    public string Key { get; }

    public ValueTask DisposeAsync()
    {
        if (this.Acquired && Interlocked.Exchange(ref this.disposed, 1) == 0)
        {
            _ = this.keys.TryRemove(this.Key, out _);
        }

        return ValueTask.CompletedTask;
    }
}
