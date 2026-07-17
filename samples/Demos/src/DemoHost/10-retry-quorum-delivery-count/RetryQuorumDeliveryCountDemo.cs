using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

internal static class RetryQuorumDeliveryCountDemo
{
    private const int MaxAttempts = 3;

    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string inputQueue = options.ResourceName(demo, "input");
        string dlqName = options.ResourceName(demo, "dlq");
        string deadLetterExchange = options.ResourceName(demo, "dlx");
        const string deadLetterRoutingKey = "failed";
        var attemptsObserved = new ConcurrentQueue<RetryAttemptObservation>();
        var terminalObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"Input quorum queue: {inputQueue}");
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
                .MapQueue(inputQueue, IAmqpResult (RetryQuorumMessage body, long deliveryCount, int? attempts) =>
                {
                    var observation = new RetryAttemptObservation(deliveryCount, attempts);
                    attemptsObserved.Enqueue(observation);

                    int attemptNumber = (int)deliveryCount + 1;
                    Console.WriteLine(
                        $"Retry attempt {attemptNumber}: deliveryCount={deliveryCount} attempts={FormatNullableInt(attempts)}");

                    if (attemptNumber < MaxAttempts)
                    {
                        return AmqpResults.Reject(requeue: true);
                    }

                    _ = terminalObserved.TrySetResult();
                    return AmqpResults.Reject(requeue: false);
                })
                .WithPrefetch(1)
                .WithDispatchConcurrency(1)
                .WithConsumerTag("oragon-demo-10-retry-quorum-delivery-count")
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
                        arguments: QueueArguments.Quorum().WithDeadLetter(deadLetterExchange, deadLetterRoutingKey),
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(inputQueue, cancellationToken).ConfigureAwait(false);
                    _ = await channel.QueuePurgeAsync(dlqName, cancellationToken).ConfigureAwait(false);
                });

            await host.StartAsync().ConfigureAwait(false);
            hostStarted = true;

            using IChannel publishChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);
            await PublishMessageAsync(
                publishChannel,
                inputQueue,
                new RetryQuorumMessage("poison-message")).ConfigureAwait(false);

            await terminalObserved.Task.WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            QueueDeclareOk dlqState = await WaitForReadyCountAsync(
                publishChannel,
                dlqName,
                expectedCount: 1,
                timeout: TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            QueueDeclareOk inputState = await publishChannel.QueueDeclarePassiveAsync(inputQueue).ConfigureAwait(false);

            await host.StopAsync().ConfigureAwait(false);
            hostStopped = true;

            RetryAttemptObservation[] attempts = attemptsObserved.ToArray();
            var failures = new List<string>();
            Check(failures, "attemptCount", attempts.Length == MaxAttempts, attempts.Length);
            Check(failures, "firstAttempt", attempts.ElementAtOrDefault(0) == new RetryAttemptObservation(0, null), FormatAttempts(attempts));
            Check(failures, "secondAttempt", attempts.ElementAtOrDefault(1) == new RetryAttemptObservation(1, 1), FormatAttempts(attempts));
            Check(failures, "thirdAttempt", attempts.ElementAtOrDefault(2) == new RetryAttemptObservation(2, 2), FormatAttempts(attempts));
            Check(failures, "inputReady", inputState.MessageCount == 0, inputState.MessageCount);
            Check(failures, "dlqReady", dlqState.MessageCount == 1, dlqState.MessageCount);

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            Console.WriteLine($"Attempts observed: {FormatAttempts(attempts)}");
            Console.WriteLine($"Input ready messages: {inputState.MessageCount}");
            Console.WriteLine($"DLQ ready messages: {dlqState.MessageCount}");
            Console.WriteLine(failures.Count == 0 ? "Demo 10 succeeded." : "Demo 10 failed.");

            return failures.Count == 0 ? 0 : 1;
        }
        catch (TimeoutException exception)
        {
            Console.Error.WriteLine($"Timed out waiting for retry/quorum demo: {exception.Message}");
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

    private static async Task PublishMessageAsync(
        IChannel channel,
        string queueName,
        RetryQuorumMessage message)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = "retry-quorum-poison",
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

    private static string FormatAttempts(IReadOnlyList<RetryAttemptObservation> attempts)
    {
        return string.Join(
            ",",
            attempts.Select(attempt =>
                $"{attempt.DeliveryCount}/{FormatNullableInt(attempt.Attempts)}"));
    }

    private static string FormatNullableInt(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "null";

    private static void Check(List<string> failures, string name, bool success, object? actual)
    {
        if (!success)
        {
            failures.Add($"{name} mismatch. Actual: {actual ?? "(null)"}");
        }
    }
}

internal sealed record RetryQuorumMessage(string Id);

internal sealed record RetryAttemptObservation(long DeliveryCount, int? Attempts);
