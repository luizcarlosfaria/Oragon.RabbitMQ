using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.AspireClient;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

internal static class ObservabilityDashboardDemo
{
    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string inputQueue = options.ResourceName(demo, "input");
        string dlqName = options.ResourceName(demo, "dlq");
        string deadLetterExchange = options.ResourceName(demo, "dlx");
        const string deadLetterRoutingKey = "failed";
        int handledMessages = 0;
        int processFailures = 0;
        var happyPathObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var poisonObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"RabbitMQ Management: {BuildManagementUrl(options.AmqpUri)}");
        Console.WriteLine($"Input queue: {inputQueue}");
        Console.WriteLine($"DLQ: {dlqName}");

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));
        builder.Services.AddRabbitMQConsumer();
        builder.Services.AddSystemTextJsonAmqpSerializer();
        builder.AddRabbitMQClient(
            "messaging",
            settings =>
            {
                settings.ConnectionString = options.AmqpUri;
                settings.MaxConnectRetryCount = 0;
            },
            factory =>
            {
                factory.ClientProvidedName = "oragon-demo-15-observability";
                factory.AutomaticRecoveryEnabled = false;
                factory.TopologyRecoveryEnabled = false;
            });

        IHost host = builder.Build();
        bool hostStarted = false;
        bool hostStopped = false;
        try
        {
            _ = host.Services
                .MapQueue(inputQueue, IAmqpResult (ObservabilityMessage body) =>
                {
                    Console.WriteLine($"message.received id={body.Id} scenario={body.Scenario}");

                    if (string.Equals(body.Scenario, "poison", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Demo poison message failed inside the handler.");
                    }

                    _ = Interlocked.Increment(ref handledMessages);
                    _ = happyPathObserved.TrySetResult();
                    return AmqpResults.Ack();
                })
                .WithPrefetch(1)
                .WithDispatchConcurrency(1)
                .WithConsumerTag("oragon-demo-15-observability")
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

                    _ = await channel.QueuePurgeAsync(inputQueue, cancellationToken).ConfigureAwait(false);
                    _ = await channel.QueuePurgeAsync(dlqName, cancellationToken).ConfigureAwait(false);
                })
                .WhenProcessFail((context, exception) =>
                {
                    _ = Interlocked.Increment(ref processFailures);
                    Console.WriteLine($"message.failed exception={exception.GetType().Name} result=Reject(false)");
                    _ = poisonObserved.TrySetResult();
                    return AmqpResults.Reject(false);
                });

            await host.StartAsync().ConfigureAwait(false);
            hostStarted = true;

            IConnection connection = host.Services.GetRequiredService<IConnection>();
            using IChannel publishChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);

            await PublishMessageAsync(
                publishChannel,
                inputQueue,
                new ObservabilityMessage("happy-1", "happy-path")).ConfigureAwait(false);
            await PublishMessageAsync(
                publishChannel,
                inputQueue,
                new ObservabilityMessage("poison-1", "poison")).ConfigureAwait(false);

            await happyPathObserved.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            await poisonObserved.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            ObservabilityQueueCounts counts = await WaitForQueueCountsAsync(
                publishChannel,
                inputQueue,
                dlqName,
                TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            HealthReport healthReport = await host.Services
                .GetRequiredService<HealthCheckService>()
                .CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);

            await host.StopAsync().ConfigureAwait(false);
            hostStopped = true;

            var failures = new List<string>();
            Check(failures, "handledMessages", handledMessages == 1, handledMessages);
            Check(failures, "processFailures", processFailures == 1, processFailures);
            Check(failures, "inputReady", counts.InputReady == 0, counts.InputReady);
            Check(failures, "dlqReady", counts.DlqReady == 1, counts.DlqReady);
            Check(failures, "connectionOpen", connection.IsOpen, connection.IsOpen);
            Check(failures, "healthStatus", healthReport.Status == HealthStatus.Healthy, healthReport.Status);

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            Console.WriteLine($"Handled messages: {handledMessages}");
            Console.WriteLine($"Process failures: {processFailures}");
            Console.WriteLine($"Input ready messages: {counts.InputReady}");
            Console.WriteLine($"DLQ ready messages: {counts.DlqReady}");
            Console.WriteLine($"Connection open: {connection.IsOpen}");
            Console.WriteLine($"Health status: {healthReport.Status}");
            foreach (KeyValuePair<string, HealthReportEntry> entry in healthReport.Entries)
            {
                Console.WriteLine($"Health check {entry.Key}: {entry.Value.Status}");
            }

            Console.WriteLine(failures.Count == 0 ? "Demo 15 succeeded." : "Demo 15 failed.");
            return failures.Count == 0 ? 0 : 1;
        }
        catch (TimeoutException exception)
        {
            Console.Error.WriteLine($"Timed out waiting for observability demo: {exception.Message}");
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
        ObservabilityMessage message)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = message.Id,
            Headers = new Dictionary<string, object?>
            {
                ["demo-case"] = "15-observability-dashboard",
                ["scenario"] = message.Scenario,
            },
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: true,
            basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(message)).ConfigureAwait(false);
    }

    private static async Task<ObservabilityQueueCounts> WaitForQueueCountsAsync(
        IChannel channel,
        string inputQueue,
        string dlqName,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        ObservabilityQueueCounts counts;

        do
        {
            QueueDeclareOk inputState = await channel.QueueDeclarePassiveAsync(inputQueue).ConfigureAwait(false);
            QueueDeclareOk dlqState = await channel.QueueDeclarePassiveAsync(dlqName).ConfigureAwait(false);
            counts = new ObservabilityQueueCounts(inputState.MessageCount, dlqState.MessageCount);

            if (counts.InputReady == 0 && counts.DlqReady == 1)
            {
                return counts;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return counts;
    }

    private static string BuildManagementUrl(string amqpUri)
    {
        Uri uri = new(amqpUri);
        string scheme = string.Equals(uri.Scheme, "amqps", StringComparison.OrdinalIgnoreCase)
            ? Uri.UriSchemeHttps
            : Uri.UriSchemeHttp;
        var builder = new UriBuilder(scheme, uri.Host, 15672);
        return builder.Uri.ToString();
    }

    private static void Check(List<string> failures, string name, bool success, object? actual)
    {
        if (!success)
        {
            failures.Add($"{name} mismatch. Actual: {actual ?? "(null)"}");
        }
    }
}

internal sealed record ObservabilityMessage(string Id, string Scenario);

internal sealed record ObservabilityQueueCounts(uint InputReady, uint DlqReady);
