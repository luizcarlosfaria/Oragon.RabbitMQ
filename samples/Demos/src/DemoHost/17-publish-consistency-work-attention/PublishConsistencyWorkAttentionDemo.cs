using System.Globalization;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Oragon.RabbitMQ.Demos;

internal static class PublishConsistencyWorkAttentionDemo
{
    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string workQueue = options.ResourceName(demo, "work");
        string attentionQueue = options.ResourceName(demo, "attention");
        string missingWorkQueue = options.ResourceName(demo, $"missing-work-{Guid.NewGuid():N}");
        string missingAttentionQueue = options.ResourceName(demo, $"missing-attention-{Guid.NewGuid():N}");

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"Work queue: {workQueue}");
        Console.WriteLine($"Attention queue: {attentionQueue}");
        Console.WriteLine($"Missing work queue: {missingWorkQueue}");
        Console.WriteLine($"Missing attention queue: {missingAttentionQueue}");

        using IConnection connection = await RabbitMqDemoClient.CreateConnectionAsync(options).ConfigureAwait(false);
        using IChannel publishChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);

        await RabbitMqDemoClient.DeclareDurableQueueAsync(publishChannel, workQueue).ConfigureAwait(false);
        await RabbitMqDemoClient.DeclareDurableQueueAsync(publishChannel, attentionQueue).ConfigureAwait(false);
        await RabbitMqDemoClient.PurgeQueueAsync(publishChannel, workQueue).ConfigureAwait(false);
        await RabbitMqDemoClient.PurgeQueueAsync(publishChannel, attentionQueue).ConfigureAwait(false);

        WorkAttentionPublishOutcome happyPath = await PublishWorkThenAttentionAsync(
            publishChannel,
            workQueue,
            attentionQueue,
            new WorkAttentionOperation("happy", "orders", "channel-42")).ConfigureAwait(false);

        QueueDeclareOk attentionAfterHappy = await publishChannel.QueueDeclarePassiveAsync(attentionQueue).ConfigureAwait(false);

        WorkAttentionPublishOutcome workRoutingFailure = await PublishWorkThenAttentionAsync(
            publishChannel,
            missingWorkQueue,
            attentionQueue,
            new WorkAttentionOperation("work-routing-failure", "orders", "channel-42")).ConfigureAwait(false);

        QueueDeclareOk attentionAfterWorkFailure = await publishChannel.QueueDeclarePassiveAsync(attentionQueue).ConfigureAwait(false);

        WorkAttentionPublishOutcome attentionRoutingFailure = await PublishWorkThenAttentionAsync(
            publishChannel,
            workQueue,
            missingAttentionQueue,
            new WorkAttentionOperation("attention-routing-failure", "orders", "channel-42")).ConfigureAwait(false);

        bool reconciledAttention = false;
        if (attentionRoutingFailure.WorkConfirmed && !attentionRoutingFailure.AttentionConfirmed)
        {
            await PublishAttentionAsync(
                publishChannel,
                attentionQueue,
                new AttentionConsistencyMessage(
                    attentionRoutingFailure.OperationId,
                    "orders",
                    "channel-42",
                    "reconciled-after-attention-publish-failure")).ConfigureAwait(false);
            reconciledAttention = true;
        }

        QueueDeclareOk finalWorkState = await publishChannel.QueueDeclarePassiveAsync(workQueue).ConfigureAwait(false);
        QueueDeclareOk finalAttentionState = await publishChannel.QueueDeclarePassiveAsync(attentionQueue).ConfigureAwait(false);

        var failures = new List<string>();
        Check(failures, "happyWorkConfirmed", happyPath.WorkConfirmed, happyPath);
        Check(failures, "happyAttentionConfirmed", happyPath.AttentionConfirmed, happyPath);
        Check(failures, "workFailureDetected", !workRoutingFailure.WorkConfirmed && !workRoutingFailure.AttentionAttempted, workRoutingFailure);
        Check(failures, "attentionNotPublishedAfterWorkFailure", attentionAfterWorkFailure.MessageCount == attentionAfterHappy.MessageCount, attentionAfterWorkFailure.MessageCount);
        Check(failures, "attentionFailureWorkConfirmed", attentionRoutingFailure.WorkConfirmed, attentionRoutingFailure);
        Check(failures, "attentionFailureDetected", attentionRoutingFailure.AttentionAttempted && !attentionRoutingFailure.AttentionConfirmed, attentionRoutingFailure);
        Check(failures, "reconciledAttention", reconciledAttention, reconciledAttention);
        Check(failures, "workReady", finalWorkState.MessageCount == 2, finalWorkState.MessageCount);
        Check(failures, "attentionReady", finalAttentionState.MessageCount == 2, finalAttentionState.MessageCount);

        foreach (string failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        PrintOutcome("happy", happyPath);
        PrintOutcome("work-routing-failure", workRoutingFailure);
        PrintOutcome("attention-routing-failure", attentionRoutingFailure);
        Console.WriteLine($"Reconciled attention: {reconciledAttention}");
        Console.WriteLine($"Work ready messages: {finalWorkState.MessageCount}");
        Console.WriteLine($"Attention ready messages: {finalAttentionState.MessageCount}");
        Console.WriteLine(failures.Count == 0 ? "Demo 17 succeeded." : "Demo 17 failed.");

        return failures.Count == 0 ? 0 : 1;
    }

    private static async Task<WorkAttentionPublishOutcome> PublishWorkThenAttentionAsync(
        IChannel channel,
        string workQueue,
        string attentionQueue,
        WorkAttentionOperation operation)
    {
        try
        {
            await PublishWorkAsync(
                channel,
                workQueue,
                new WorkConsistencyMessage(operation.OperationId, operation.Type, operation.ChannelId)).ConfigureAwait(false);
        }
        catch (PublishException exception)
        {
            return WorkAttentionPublishOutcome.WorkFailed(operation.OperationId, exception);
        }

        try
        {
            await PublishAttentionAsync(
                channel,
                attentionQueue,
                new AttentionConsistencyMessage(operation.OperationId, operation.Type, operation.ChannelId, "normal")).ConfigureAwait(false);
        }
        catch (PublishException exception)
        {
            return WorkAttentionPublishOutcome.AttentionFailed(operation.OperationId, exception);
        }

        return WorkAttentionPublishOutcome.Success(operation.OperationId);
    }

    private static Task PublishWorkAsync(
        IChannel channel,
        string queueName,
        WorkConsistencyMessage message)
    {
        return PublishJsonAsync(channel, queueName, role: "work", message.OperationId, message);
    }

    private static Task PublishAttentionAsync(
        IChannel channel,
        string queueName,
        AttentionConsistencyMessage message)
    {
        return PublishJsonAsync(channel, queueName, role: "attention", message.OperationId, message);
    }

    private static async Task PublishJsonAsync<T>(
        IChannel channel,
        string queueName,
        string role,
        string operationId,
        T message)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = $"{role}-{operationId}",
            CorrelationId = operationId,
            Headers = new Dictionary<string, object?>
            {
                ["demo-case"] = "17-publish-consistency-work-attention",
                ["role"] = role,
            },
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: true,
            basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(message)).ConfigureAwait(false);
    }

    private static void PrintOutcome(string label, WorkAttentionPublishOutcome outcome)
    {
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{label}: work={outcome.WorkConfirmed} attentionAttempted={outcome.AttentionAttempted} attention={outcome.AttentionConfirmed} failure={outcome.FailureStage ?? "none"}"));
    }

    private static void Check(List<string> failures, string name, bool success, object? actual)
    {
        if (!success)
        {
            failures.Add($"{name} mismatch. Actual: {actual ?? "(null)"}");
        }
    }
}

internal sealed record WorkAttentionOperation(string OperationId, string Type, string ChannelId);

internal sealed record WorkConsistencyMessage(string OperationId, string Type, string ChannelId);

internal sealed record AttentionConsistencyMessage(string OperationId, string Type, string ChannelId, string Reason);

internal sealed record WorkAttentionPublishOutcome(
    string OperationId,
    bool WorkConfirmed,
    bool AttentionAttempted,
    bool AttentionConfirmed,
    string? FailureStage,
    bool FailureWasReturn)
{
    public static WorkAttentionPublishOutcome Success(string operationId) =>
        new(operationId, WorkConfirmed: true, AttentionAttempted: true, AttentionConfirmed: true, FailureStage: null, FailureWasReturn: false);

    public static WorkAttentionPublishOutcome WorkFailed(string operationId, PublishException exception) =>
        new(operationId, WorkConfirmed: false, AttentionAttempted: false, AttentionConfirmed: false, FailureStage: "work", exception.IsReturn);

    public static WorkAttentionPublishOutcome AttentionFailed(string operationId, PublishException exception) =>
        new(operationId, WorkConfirmed: true, AttentionAttempted: true, AttentionConfirmed: false, FailureStage: "attention", exception.IsReturn);
}
