using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.DynamicQueues;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

internal static class ApplicationGatesDemo
{
    private const string GateKey = "attention:orders:channel-42";

    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string workQueue = options.ResourceName(demo, "work");
        var state = new ApplicationGateDemoState();

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"Work queue: {workQueue}");
        Console.WriteLine($"Application gate key: {GateKey}");

        using IConnection connection = await RabbitMqDemoClient.CreateConnectionAsync(options).ConfigureAwait(false);
        using IChannel setupChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);

        await RabbitMqDemoClient.DeclareDurableQueueAsync(setupChannel, workQueue).ConfigureAwait(false);
        await RabbitMqDemoClient.PurgeQueueAsync(setupChannel, workQueue).ConfigureAwait(false);
        await PublishWorkAsync(setupChannel, workQueue, count: 2).ConfigureAwait(false);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));
        builder.Services.AddRabbitMQConsumer();
        builder.Services.AddSystemTextJsonAmqpSerializer();
        builder.Services.AddSingleton(connection);
        builder.Services.AddSingleton<IAmqpConcurrencyGate, ApplicationOwnedInMemoryGate>();
        builder.Services.AddSingleton(state);

        IHost host = builder.Build();
        try
        {
            using IServiceScope scope = host.Services.CreateScope();
            IAmqpDynamicQueueConsumer consumer = scope.ServiceProvider.GetRequiredService<IAmqpDynamicQueueConsumer>();
            var firstLeaseAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            Task<DynamicQueueConsumeResult> firstWorker = consumer.ConsumeAsync(
                CreateRequest(
                    scope.ServiceProvider,
                    connection,
                    workQueue,
                    workerId: "worker-a",
                    state,
                    firstLeaseAcquired),
                CancellationToken.None);

            await firstLeaseAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            DynamicQueueConsumeResult secondWorker = await consumer.ConsumeAsync(
                CreateRequest(
                    scope.ServiceProvider,
                    connection,
                    workQueue,
                    workerId: "worker-b",
                    state,
                    null),
                CancellationToken.None).ConfigureAwait(false);

            DynamicQueueConsumeResult firstResult = await firstWorker.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            DynamicQueueConsumeResult thirdWorker = await consumer.ConsumeAsync(
                CreateRequest(
                    scope.ServiceProvider,
                    connection,
                    workQueue,
                    workerId: "worker-c",
                    state,
                    null),
                CancellationToken.None).ConfigureAwait(false);

            QueueDeclareOk workState = await setupChannel.QueueDeclarePassiveAsync(workQueue).ConfigureAwait(false);

            string[] processed = state.ProcessedMessages.ToArray();
            string[] gateKeys = state.GateKeys.ToArray();
            string[] hookServices = state.HookServiceMatches.ToArray();
            string[] stopStatuses = state.StopStatuses.ToArray();

            var failures = new List<string>();
            Check(failures, "firstStatus", firstResult.Status == DynamicQueueConsumeStatus.MaxMessagesReached, firstResult.Status);
            Check(failures, "secondStatus", secondWorker.Status == DynamicQueueConsumeStatus.Deferred, secondWorker.Status);
            Check(failures, "secondSuggestedDelay", secondWorker.Exception == null, secondWorker.Exception?.Message);
            Check(failures, "thirdStatus", thirdWorker.Status == DynamicQueueConsumeStatus.MaxMessagesReached, thirdWorker.Status);
            Check(failures, "processedCount", processed.Length == 2, string.Join(",", processed));
            Check(failures, "gateAllowed", state.AllowedLeases == 2, state.AllowedLeases);
            Check(failures, "gateDenied", state.DeniedLeases == 1, state.DeniedLeases);
            Check(failures, "applicationKey", gateKeys.All(key => string.Equals(key, GateKey, StringComparison.Ordinal)), string.Join(",", gateKeys));
            Check(failures, "noChannelLifecycleKey", gateKeys.All(key => !key.Contains("channel-lifecycle", StringComparison.Ordinal)), string.Join(",", gateKeys));
            Check(failures, "serviceProviderHooks", hookServices.All(value => string.Equals(value, "matched", StringComparison.Ordinal)), string.Join(",", hookServices));
            Check(failures, "stopHooks", stopStatuses.Length == 3, string.Join(",", stopStatuses));
            Check(failures, "workReady", workState.MessageCount == 0, workState.MessageCount);

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            Console.WriteLine($"Processed messages: {string.Join(",", processed)}");
            Console.WriteLine($"Gate keys: {string.Join(",", gateKeys)}");
            Console.WriteLine($"Allowed leases: {state.AllowedLeases}");
            Console.WriteLine($"Denied leases: {state.DeniedLeases}");
            Console.WriteLine($"Hook service matches: {string.Join(",", hookServices)}");
            Console.WriteLine($"Stop statuses: {string.Join(",", stopStatuses)}");
            Console.WriteLine($"Work ready messages: {workState.MessageCount}");
            Console.WriteLine(failures.Count == 0 ? "Demo 16 succeeded." : "Demo 16 failed.");

            return failures.Count == 0 ? 0 : 1;
        }
        catch (TimeoutException exception)
        {
            Console.Error.WriteLine($"Timed out waiting for application gates demo: {exception.Message}");
            return 1;
        }
        finally
        {
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

    private static DynamicQueueConsumeRequest<ApplicationGateWorkMessage> CreateRequest(
        IServiceProvider expectedServices,
        IConnection connection,
        string workQueue,
        string workerId,
        ApplicationGateDemoState state,
        TaskCompletionSource? leaseAcquired)
    {
        IAmqpConcurrencyLease? activeLease = null;
        IReadOnlyDictionary<string, object> metadata = new Dictionary<string, object>
        {
            ["type"] = "orders",
            ["channelId"] = "channel-42",
            ["workerId"] = workerId,
        };

        return new DynamicQueueConsumeRequest<ApplicationGateWorkMessage>
        {
            QueueName = workQueue,
            Connection = connection,
            PrefetchCount = 1,
            MaxLocalConcurrency = 1,
            MaxMessages = 1,
            Metadata = metadata,
            BeforeStartAsync = async (context, cancellationToken) =>
            {
                state.HookServiceMatches.Enqueue(ReferenceEquals(context.Services, expectedServices) ? "matched" : "different");
                IAmqpConcurrencyGate gate = context.Services.GetRequiredService<IAmqpConcurrencyGate>();
                IAmqpConcurrencyLease lease = await gate.TryAcquireAsync(
                    new AmqpConcurrencyGateRequest(
                        GateKey,
                        TimeSpan.FromSeconds(30),
                        context.Metadata),
                    cancellationToken).ConfigureAwait(false);

                state.GateKeys.Enqueue(lease.Key);

                if (!lease.Acquired)
                {
                    _ = Interlocked.Increment(ref state.deniedLeases);
                    await lease.DisposeAsync().ConfigureAwait(false);
                    return DynamicQueueStartDecision.Defer(TimeSpan.FromMilliseconds(250));
                }

                _ = Interlocked.Increment(ref state.allowedLeases);
                activeLease = lease;
                leaseAcquired?.TrySetResult();
                return DynamicQueueStartDecision.Allow();
            },
            AfterStopAsync = async (context, cancellationToken) =>
            {
                state.HookServiceMatches.Enqueue(ReferenceEquals(context.Services, expectedServices) ? "matched" : "different");
                state.StopStatuses.Enqueue($"{workerId}:{context.Result.Status}");

                if (activeLease != null)
                {
                    await activeLease.DisposeAsync().ConfigureAwait(false);
                    activeLease = null;
                }
            },
            OnMessageAsync = async (message, context) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300), context.CancellationToken).ConfigureAwait(false);
                state.ProcessedMessages.Enqueue($"{workerId}:{message.Id}");
                return AmqpResults.Ack();
            },
        };
    }

    private static async Task PublishWorkAsync(
        IChannel channel,
        string queueName,
        int count)
    {
        for (int index = 1; index <= count; index++)
        {
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = $"gate-work-{index}",
                Headers = new Dictionary<string, object?>
                {
                    ["gate-key"] = GateKey,
                },
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: JsonSerializer.SerializeToUtf8Bytes(new ApplicationGateWorkMessage($"work-{index}"))).ConfigureAwait(false);
        }
    }

    private static void Check(List<string> failures, string name, bool success, object? actual)
    {
        if (!success)
        {
            failures.Add($"{name} mismatch. Actual: {actual ?? "(null)"}");
        }
    }
}

internal sealed record ApplicationGateWorkMessage(string Id);

internal sealed class ApplicationGateDemoState
{
    internal int allowedLeases;
    internal int deniedLeases;

    public ConcurrentQueue<string> ProcessedMessages { get; } = new();

    public ConcurrentQueue<string> GateKeys { get; } = new();

    public ConcurrentQueue<string> HookServiceMatches { get; } = new();

    public ConcurrentQueue<string> StopStatuses { get; } = new();

    public int AllowedLeases => this.allowedLeases;

    public int DeniedLeases => this.deniedLeases;
}

internal sealed class ApplicationOwnedInMemoryGate : IAmqpConcurrencyGate
{
    private readonly ConcurrentDictionary<string, byte> keys = new(StringComparer.Ordinal);

    public ValueTask<IAmqpConcurrencyLease> TryAcquireAsync(
        AmqpConcurrencyGateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool acquired = this.keys.TryAdd(request.Key, 0);
        return ValueTask.FromResult<IAmqpConcurrencyLease>(
            new ApplicationOwnedInMemoryLease(this.keys, request.Key, acquired));
    }
}

internal sealed class ApplicationOwnedInMemoryLease : IAmqpConcurrencyLease
{
    private readonly ConcurrentDictionary<string, byte> keys;
    private int disposed;

    public ApplicationOwnedInMemoryLease(
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
