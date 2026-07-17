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

internal static class DynamicQueueConsumerDemo
{
    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string maxMessagesQueue = options.ResourceName(demo, "max-messages");
        string maxDurationQueue = options.ResourceName(demo, "max-duration");
        string idleQueue = options.ResourceName(demo, "idle");
        string initialLengthQueue = options.ResourceName(demo, "initial-length");
        string emptySnapshotQueue = options.ResourceName(demo, "empty-snapshot");
        string missingQueue = options.ResourceName(demo, $"missing-{Guid.NewGuid():N}");

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"MaxMessages queue: {maxMessagesQueue}");
        Console.WriteLine($"MaxDuration queue: {maxDurationQueue}");
        Console.WriteLine($"IdleTimeout queue: {idleQueue}");
        Console.WriteLine($"InitialQueueLength queue: {initialLengthQueue}");
        Console.WriteLine($"Empty snapshot queue: {emptySnapshotQueue}");
        Console.WriteLine($"Missing queue: {missingQueue}");

        using IConnection connection = await RabbitMqDemoClient.CreateConnectionAsync(options).ConfigureAwait(false);
        using IChannel setupChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);

        await DeclareAndPurgeAsync(setupChannel, maxMessagesQueue).ConfigureAwait(false);
        await DeclareAndPurgeAsync(setupChannel, maxDurationQueue).ConfigureAwait(false);
        await DeclareAndPurgeAsync(setupChannel, idleQueue).ConfigureAwait(false);
        await DeclareAndPurgeAsync(setupChannel, initialLengthQueue).ConfigureAwait(false);
        await DeclareAndPurgeAsync(setupChannel, emptySnapshotQueue).ConfigureAwait(false);

        await PublishMessagesAsync(setupChannel, maxMessagesQueue, "max", count: 3).ConfigureAwait(false);
        await PublishMessagesAsync(setupChannel, maxDurationQueue, "duration", count: 10).ConfigureAwait(false);
        await PublishMessagesAsync(setupChannel, initialLengthQueue, "initial", count: 2).ConfigureAwait(false);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));
        builder.Services.AddRabbitMQConsumer();
        builder.Services.AddSystemTextJsonAmqpSerializer();
        builder.Services.AddSingleton(connection);

        IHost host = builder.Build();
        try
        {
            using IServiceScope scope = host.Services.CreateScope();
            IAmqpDynamicQueueConsumer consumer = scope.ServiceProvider.GetRequiredService<IAmqpDynamicQueueConsumer>();

            bool beforeStartObserved = false;
            bool afterStopObserved = false;

            DynamicQueueConsumeResult maxMessages = await consumer.ConsumeAsync(
                new DynamicQueueConsumeRequest<DynamicQueueDemoMessage>
                {
                    QueueName = maxMessagesQueue,
                    Connection = connection,
                    PrefetchCount = 1,
                    MaxLocalConcurrency = 1,
                    MaxMessages = 2,
                    Metadata = new Dictionary<string, object> { ["scenario"] = "max-messages" },
                    BeforeStartAsync = (context, cancellationToken) =>
                    {
                        beforeStartObserved = context.InitialReadyCount == 3
                            && ReferenceEquals(context.Services, scope.ServiceProvider)
                            && string.Equals((string)context.Metadata["scenario"], "max-messages", StringComparison.Ordinal);
                        return ValueTask.FromResult(DynamicQueueStartDecision.Allow());
                    },
                    AfterStopAsync = (context, cancellationToken) =>
                    {
                        afterStopObserved = context.Result.Status == DynamicQueueConsumeStatus.MaxMessagesReached
                            && ReferenceEquals(context.Services, scope.ServiceProvider);
                        return ValueTask.CompletedTask;
                    },
                    OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
                },
                CancellationToken.None).ConfigureAwait(false);

            DynamicQueueConsumeResult maxDuration = await consumer.ConsumeAsync(
                new DynamicQueueConsumeRequest<DynamicQueueDemoMessage>
                {
                    QueueName = maxDurationQueue,
                    Connection = connection,
                    PrefetchCount = 1,
                    MaxLocalConcurrency = 1,
                    MaxDuration = TimeSpan.FromMilliseconds(250),
                    OnMessageAsync = async (message, context) =>
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(120), context.CancellationToken).ConfigureAwait(false);
                        return AmqpResults.Ack();
                    },
                },
                CancellationToken.None).ConfigureAwait(false);

            DynamicQueueConsumeResult idleTimeout = await consumer.ConsumeAsync(
                new DynamicQueueConsumeRequest<DynamicQueueDemoMessage>
                {
                    QueueName = idleQueue,
                    Connection = connection,
                    IdleTimeout = TimeSpan.FromMilliseconds(150),
                    OnMessageAsync = (message, context) =>
                        throw new InvalidOperationException("Idle scenario should not receive a message."),
                },
                CancellationToken.None).ConfigureAwait(false);

            DynamicQueueConsumeResult initialLength = await consumer.ConsumeAsync(
                new DynamicQueueConsumeRequest<DynamicQueueDemoMessage>
                {
                    QueueName = initialLengthQueue,
                    Connection = connection,
                    PrefetchCount = 1,
                    MaxLocalConcurrency = 1,
                    StopAfterInitialQueueLength = true,
                    OnMessageAsync = async (message, context) =>
                    {
                        if (message.Sequence == 1)
                        {
                            using IChannel lateChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(context.Connection).ConfigureAwait(false);
                            await PublishMessagesAsync(lateChannel, context.QueueName, "late", count: 1).ConfigureAwait(false);
                        }

                        return AmqpResults.Ack();
                    },
                },
                CancellationToken.None).ConfigureAwait(false);

            DynamicQueueConsumeResult emptySnapshot = await consumer.ConsumeAsync(
                new DynamicQueueConsumeRequest<DynamicQueueDemoMessage>
                {
                    QueueName = emptySnapshotQueue,
                    Connection = connection,
                    StopAfterInitialQueueLength = true,
                    OnMessageAsync = (message, context) =>
                        throw new InvalidOperationException("Empty snapshot scenario should not receive a message."),
                },
                CancellationToken.None).ConfigureAwait(false);

            DynamicQueueConsumeResult queueMissing = await consumer.ConsumeAsync(
                new DynamicQueueConsumeRequest<DynamicQueueDemoMessage>
                {
                    QueueName = missingQueue,
                    Connection = connection,
                    MaxMessages = 1,
                    OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
                },
                CancellationToken.None).ConfigureAwait(false);

            bool missingStopRuleFailedClearly = await VerifyMissingStopRuleFailsClearlyAsync(
                consumer,
                maxMessagesQueue,
                connection).ConfigureAwait(false);

            QueueDeclareOk maxMessagesState = await setupChannel.QueueDeclarePassiveAsync(maxMessagesQueue).ConfigureAwait(false);
            QueueDeclareOk initialLengthState = await setupChannel.QueueDeclarePassiveAsync(initialLengthQueue).ConfigureAwait(false);

            var failures = new List<string>();
            Check(failures, "maxMessagesStatus", maxMessages.Status == DynamicQueueConsumeStatus.MaxMessagesReached, maxMessages.Status);
            Check(failures, "maxMessagesReceived", maxMessages.MessagesReceived == 2, maxMessages.MessagesReceived);
            Check(failures, "maxMessagesAcked", maxMessages.MessagesAcked == 2, maxMessages.MessagesAcked);
            Check(failures, "maxMessagesRemaining", maxMessagesState.MessageCount == 1, maxMessagesState.MessageCount);
            Check(failures, "hooks", beforeStartObserved && afterStopObserved, $"{beforeStartObserved}/{afterStopObserved}");
            Check(failures, "maxDurationStatus", maxDuration.Status == DynamicQueueConsumeStatus.MaxDurationReached, maxDuration.Status);
            Check(failures, "idleTimeoutStatus", idleTimeout.Status == DynamicQueueConsumeStatus.IdleTimeoutReached, idleTimeout.Status);
            Check(failures, "initialLengthStatus", initialLength.Status == DynamicQueueConsumeStatus.InitialQueueLengthReached, initialLength.Status);
            Check(failures, "initialLengthReceived", initialLength.MessagesReceived == 2, initialLength.MessagesReceived);
            Check(failures, "initialLengthRemaining", initialLengthState.MessageCount == 1, initialLengthState.MessageCount);
            Check(failures, "emptySnapshotStatus", emptySnapshot.Status == DynamicQueueConsumeStatus.Empty, emptySnapshot.Status);
            Check(failures, "emptySnapshotInitial", emptySnapshot.InitialReadyCount == 0, emptySnapshot.InitialReadyCount);
            Check(failures, "queueMissingStatus", queueMissing.Status == DynamicQueueConsumeStatus.QueueMissing, queueMissing.Status);
            Check(failures, "missingStopRule", missingStopRuleFailedClearly, missingStopRuleFailedClearly);

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            PrintResult("MaxMessages", maxMessages);
            PrintResult("MaxDuration", maxDuration);
            PrintResult("IdleTimeout", idleTimeout);
            PrintResult("InitialQueueLength", initialLength);
            PrintResult("EmptySnapshot", emptySnapshot);
            PrintResult("QueueMissing", queueMissing);
            Console.WriteLine($"Missing stop rule failed clearly: {missingStopRuleFailedClearly}");
            Console.WriteLine(failures.Count == 0 ? "Demo 13 succeeded." : "Demo 13 failed.");

            return failures.Count == 0 ? 0 : 1;
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

    private static async Task DeclareAndPurgeAsync(IChannel channel, string queueName)
    {
        await RabbitMqDemoClient.DeclareDurableQueueAsync(channel, queueName).ConfigureAwait(false);
        await RabbitMqDemoClient.PurgeQueueAsync(channel, queueName).ConfigureAwait(false);
    }

    private static async Task PublishMessagesAsync(
        IChannel channel,
        string queueName,
        string label,
        int count)
    {
        for (int index = 1; index <= count; index++)
        {
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = $"{label}-{index}",
            };

            var message = new DynamicQueueDemoMessage($"{label}-{index}", index);

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: JsonSerializer.SerializeToUtf8Bytes(message)).ConfigureAwait(false);
        }
    }

    private static async Task<bool> VerifyMissingStopRuleFailsClearlyAsync(
        IAmqpDynamicQueueConsumer consumer,
        string queueName,
        IConnection connection)
    {
        try
        {
            _ = await consumer.ConsumeAsync(
                new DynamicQueueConsumeRequest<DynamicQueueDemoMessage>
                {
                    QueueName = queueName,
                    Connection = connection,
                    OnMessageAsync = (message, context) => Task.FromResult<IAmqpResult>(AmqpResults.Ack()),
                },
                CancellationToken.None).ConfigureAwait(false);

            return false;
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine($"Missing stop rule error: {exception.Message}");
            return exception.Message.Contains("stop rule", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void PrintResult(string label, DynamicQueueConsumeResult result)
    {
        Console.WriteLine(
            $"{label}: status={result.Status} initial={result.InitialReadyCount} received={result.MessagesReceived} acked={result.MessagesAcked} remaining={result.RemainingReadyCount}");
    }

    private static void Check(List<string> failures, string name, bool success, object? actual)
    {
        if (!success)
        {
            failures.Add($"{name} mismatch. Actual: {actual ?? "(null)"}");
        }
    }
}

internal sealed record DynamicQueueDemoMessage(string Id, int Sequence);
