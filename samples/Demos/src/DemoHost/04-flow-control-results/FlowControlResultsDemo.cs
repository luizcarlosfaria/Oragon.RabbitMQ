using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

internal static class FlowControlResultsDemo
{
    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string inputQueue = options.ResourceName(demo, "input");
        string forwardQueue = options.ResourceName(demo, "forward");
        string replyQueue = options.ResourceName(demo, "reply");
        string dlqName = options.ResourceName(demo, "dlq");
        string deadLetterExchange = options.ResourceName(demo, "dlx");
        const string deadLetterRoutingKey = "failed";

        int handledMessages = 0;
        int resultExecutionFailures = 0;

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"Input queue: {inputQueue}");
        Console.WriteLine($"Forward queue: {forwardQueue}");
        Console.WriteLine($"Reply queue: {replyQueue}");
        Console.WriteLine($"DLQ: {dlqName}");

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
                .MapQueue(inputQueue, IAmqpResult (FlowControlCommand command) =>
                {
                    _ = Interlocked.Increment(ref handledMessages);
                    Console.WriteLine($"Handling scenario: {command.Scenario}");

                    return command.Scenario switch
                    {
                        "ack" => AmqpResults.Ack(),
                        "nack" => AmqpResults.Nack(false),
                        "reject" => AmqpResults.Reject(false),
                        "forward" => AmqpResults.ForwardAndAck(
                            string.Empty,
                            forwardQueue,
                            mandatory: true,
                            new FlowControlForwarded(command.Scenario, command.Payload)),
                        "reply" => AmqpResults.ReplyAndAck(
                            new FlowControlReply(command.Scenario, command.Payload)),
                        "result-fail" => AmqpResults.ReplyAndAck(
                            new FlowControlReply(command.Scenario, command.Payload)),
                        _ => AmqpResults.Ack(),
                    };
                })
                .WithPrefetch(1)
                .WithConsumerTag("oragon-demo-04-flow-control-results")
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredService<IConnection>()))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>())
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    await channel.ExchangeDeclareAsync(
                        exchange: deadLetterExchange,
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
                        exchange: deadLetterExchange,
                        routingKey: deadLetterRoutingKey,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueueDeclareAsync(
                        queue: inputQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: QueueArguments.WithDeadLetter(deadLetterExchange, deadLetterRoutingKey),
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueueDeclareAsync(
                        queue: forwardQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueueDeclareAsync(
                        queue: replyQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(inputQueue, cancellationToken).ConfigureAwait(false);
                    _ = await channel.QueuePurgeAsync(forwardQueue, cancellationToken).ConfigureAwait(false);
                    _ = await channel.QueuePurgeAsync(replyQueue, cancellationToken).ConfigureAwait(false);
                    _ = await channel.QueuePurgeAsync(dlqName, cancellationToken).ConfigureAwait(false);
                })
                .WhenResultExecutionFail((context, exception) =>
                {
                    _ = Interlocked.Increment(ref resultExecutionFailures);
                    Console.WriteLine($"Result execution failure handled: {exception.GetType().Name}");
                    return AmqpResults.Reject(false);
                });

            await host.StartAsync().ConfigureAwait(false);
            hostStarted = true;

            using IChannel publishChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);

            await PublishCommandAsync(publishChannel, inputQueue, new FlowControlCommand("ack", "ack payload")).ConfigureAwait(false);
            await PublishCommandAsync(publishChannel, inputQueue, new FlowControlCommand("nack", "nack payload")).ConfigureAwait(false);
            await PublishCommandAsync(publishChannel, inputQueue, new FlowControlCommand("reject", "reject payload")).ConfigureAwait(false);
            await PublishCommandAsync(publishChannel, inputQueue, new FlowControlCommand("forward", "forward payload")).ConfigureAwait(false);
            await PublishCommandAsync(publishChannel, inputQueue, new FlowControlCommand("reply", "reply payload"), replyQueue).ConfigureAwait(false);
            await PublishCommandAsync(publishChannel, inputQueue, new FlowControlCommand("result-fail", "missing reply-to")).ConfigureAwait(false);

            QueueCounts counts = await WaitForQueueCountsAsync(
                publishChannel,
                inputQueue,
                forwardQueue,
                replyQueue,
                dlqName,
                timeout: TimeSpan.FromSeconds(20)).ConfigureAwait(false);

            MessageInspection<FlowControlForwarded> forward = await InspectMessageAsync<FlowControlForwarded>(
                publishChannel,
                forwardQueue).ConfigureAwait(false);

            MessageInspection<FlowControlReply> reply = await InspectMessageAsync<FlowControlReply>(
                publishChannel,
                replyQueue).ConfigureAwait(false);

            await host.StopAsync().ConfigureAwait(false);
            hostStopped = true;

            var failures = new List<string>();
            Check(failures, "handledMessages", handledMessages == 6, handledMessages);
            Check(failures, "resultExecutionFailures", resultExecutionFailures == 1, resultExecutionFailures);
            Check(failures, "inputReady", counts.InputReady == 0, counts.InputReady);
            Check(failures, "forwardReady", counts.ForwardReady == 1, counts.ForwardReady);
            Check(failures, "replyReady", counts.ReplyReady == 1, counts.ReplyReady);
            Check(failures, "dlqReady", counts.DlqReady == 3, counts.DlqReady);
            Check(failures, "forwardBody", forward.Message?.Payload == "forward payload", forward.Message?.Payload);
            Check(failures, "forwardCorrelation", forward.CorrelationId == "flow-forward", forward.CorrelationId);
            Check(failures, "replyBody", reply.Message?.Payload == "reply payload", reply.Message?.Payload);
            Check(failures, "replyCorrelation", reply.CorrelationId == "flow-reply", reply.CorrelationId);

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            Console.WriteLine($"Handled messages: {handledMessages}");
            Console.WriteLine($"Result execution failures: {resultExecutionFailures}");
            Console.WriteLine($"Input ready messages: {counts.InputReady}");
            Console.WriteLine($"Forward ready messages: {counts.ForwardReady}");
            Console.WriteLine($"Reply ready messages: {counts.ReplyReady}");
            Console.WriteLine($"DLQ ready messages: {counts.DlqReady}");
            Console.WriteLine(failures.Count == 0 ? "Demo 04 succeeded." : "Demo 04 failed.");

            return failures.Count == 0 ? 0 : 1;
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

    private static async Task PublishCommandAsync(
        IChannel channel,
        string inputQueue,
        FlowControlCommand command,
        string? replyTo = null)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = $"flow-{command.Scenario}",
            ReplyTo = replyTo,
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: inputQueue,
            mandatory: true,
            basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(command)).ConfigureAwait(false);
    }

    private static async Task<QueueCounts> WaitForQueueCountsAsync(
        IChannel channel,
        string inputQueue,
        string forwardQueue,
        string replyQueue,
        string dlqName,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        QueueCounts counts;

        do
        {
            counts = await ReadQueueCountsAsync(channel, inputQueue, forwardQueue, replyQueue, dlqName).ConfigureAwait(false);
            if (counts.InputReady == 0 && counts.ForwardReady == 1 && counts.ReplyReady == 1 && counts.DlqReady == 3)
            {
                return counts;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return counts;
    }

    private static async Task<QueueCounts> ReadQueueCountsAsync(
        IChannel channel,
        string inputQueue,
        string forwardQueue,
        string replyQueue,
        string dlqName)
    {
        QueueDeclareOk inputState = await channel.QueueDeclarePassiveAsync(inputQueue).ConfigureAwait(false);
        QueueDeclareOk forwardState = await channel.QueueDeclarePassiveAsync(forwardQueue).ConfigureAwait(false);
        QueueDeclareOk replyState = await channel.QueueDeclarePassiveAsync(replyQueue).ConfigureAwait(false);
        QueueDeclareOk dlqState = await channel.QueueDeclarePassiveAsync(dlqName).ConfigureAwait(false);

        return new QueueCounts(
            inputState.MessageCount,
            forwardState.MessageCount,
            replyState.MessageCount,
            dlqState.MessageCount);
    }

    private static async Task<MessageInspection<T>> InspectMessageAsync<T>(IChannel channel, string queueName)
    {
        BasicGetResult? result = await channel.BasicGetAsync(queueName, autoAck: false).ConfigureAwait(false);
        if (result == null)
        {
            return new MessageInspection<T>(default, null);
        }

        try
        {
            T? message = JsonSerializer.Deserialize<T>(result.Body.Span);
            return new MessageInspection<T>(message, result.BasicProperties.CorrelationId);
        }
        finally
        {
            await channel.BasicNackAsync(result.DeliveryTag, multiple: false, requeue: true).ConfigureAwait(false);
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

internal sealed record FlowControlCommand(string Scenario, string Payload);

internal sealed record FlowControlForwarded(string SourceScenario, string Payload);

internal sealed record FlowControlReply(string SourceScenario, string Payload);

internal sealed record QueueCounts(uint InputReady, uint ForwardReady, uint ReplyReady, uint DlqReady);

internal sealed record MessageInspection<T>(T? Message, string? CorrelationId);
