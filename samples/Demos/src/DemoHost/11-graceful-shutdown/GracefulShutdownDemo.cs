using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

internal static class GracefulShutdownDemo
{
    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string cooperativeQueue = options.ResourceName(demo, "cooperative");
        string timeoutQueue = options.ResourceName(demo, "timeout");
        var cooperativeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cooperativeCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeoutStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeoutCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"Cooperative queue: {cooperativeQueue}");
        Console.WriteLine($"Timeout queue: {timeoutQueue}");

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
                .MapQueue(cooperativeQueue, async Task (GracefulShutdownMessage body, CancellationToken cancellationToken) =>
                {
                    Console.WriteLine("Cooperative handler started.");
                    _ = cooperativeStarted.TrySetResult();
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("Cooperative handler observed cancellation.");
                        _ = cooperativeCanceled.TrySetResult();
                    }
                })
                .WithPrefetch(1)
                .WithConsumerTag("oragon-demo-11-cooperative")
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredService<IConnection>()))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>())
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    _ = await channel.QueueDeclareAsync(
                        queue: cooperativeQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(cooperativeQueue, cancellationToken).ConfigureAwait(false);
                })
                .WithGracefulShutdown(options =>
                {
                    options.CancelContextTokenOnStop = true;
                    options.WaitForInFlightMessages = true;
                    options.DrainTimeout = TimeSpan.FromSeconds(2);
                });

            _ = host.Services
                .MapQueue(timeoutQueue, async Task (GracefulShutdownMessage body, CancellationToken cancellationToken) =>
                {
                    Console.WriteLine("Timeout handler started.");
                    _ = timeoutStarted.TrySetResult();
                    await Task.Delay(TimeSpan.FromMilliseconds(900), CancellationToken.None).ConfigureAwait(false);
                    Console.WriteLine("Timeout handler completed after StopAsync returned.");
                    _ = timeoutCompleted.TrySetResult();
                })
                .WithPrefetch(1)
                .WithConsumerTag("oragon-demo-11-timeout")
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredService<IConnection>()))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>())
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    _ = await channel.QueueDeclareAsync(
                        queue: timeoutQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(timeoutQueue, cancellationToken).ConfigureAwait(false);
                })
                .WithGracefulShutdown(options =>
                {
                    options.CancelContextTokenOnStop = true;
                    options.WaitForInFlightMessages = true;
                    options.DrainTimeout = TimeSpan.FromMilliseconds(200);
                });

            await host.StartAsync().ConfigureAwait(false);
            hostStarted = true;

            using IChannel publishChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);
            await PublishMessageAsync(
                publishChannel,
                cooperativeQueue,
                new GracefulShutdownMessage("cooperative")).ConfigureAwait(false);
            await PublishMessageAsync(
                publishChannel,
                timeoutQueue,
                new GracefulShutdownMessage("timeout")).ConfigureAwait(false);

            await cooperativeStarted.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            await timeoutStarted.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            Stopwatch stopWatch = Stopwatch.StartNew();
            await host.StopAsync().ConfigureAwait(false);
            stopWatch.Stop();
            hostStopped = true;

            await cooperativeCanceled.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            bool timeoutWasStillRunning = !timeoutCompleted.Task.IsCompleted;
            await timeoutCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            QueueDeclareOk cooperativeState = await WaitForReadyCountAsync(
                publishChannel,
                cooperativeQueue,
                expectedCount: 0,
                timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            QueueDeclareOk timeoutState = await WaitForReadyCountAsync(
                publishChannel,
                timeoutQueue,
                expectedCount: 0,
                timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            var failures = new List<string>();
            Check(failures, "cooperativeCanceled", cooperativeCanceled.Task.IsCompletedSuccessfully, cooperativeCanceled.Task.Status);
            Check(failures, "timeoutWasStillRunning", timeoutWasStillRunning, timeoutWasStillRunning);
            Check(failures, "stopDuration", stopWatch.Elapsed < TimeSpan.FromSeconds(2), stopWatch.Elapsed);
            Check(failures, "cooperativeReady", cooperativeState.MessageCount == 0, cooperativeState.MessageCount);
            Check(failures, "timeoutReady", timeoutState.MessageCount == 0, timeoutState.MessageCount);

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            Console.WriteLine($"Cooperative canceled: {cooperativeCanceled.Task.IsCompletedSuccessfully}");
            Console.WriteLine($"Timeout handler was still running after StopAsync: {timeoutWasStillRunning}");
            Console.WriteLine($"StopAsync duration: {stopWatch.Elapsed}");
            Console.WriteLine($"Cooperative ready messages: {cooperativeState.MessageCount}");
            Console.WriteLine($"Timeout ready messages: {timeoutState.MessageCount}");
            Console.WriteLine(failures.Count == 0 ? "Demo 11 succeeded." : "Demo 11 failed.");

            return failures.Count == 0 ? 0 : 1;
        }
        catch (TimeoutException exception)
        {
            Console.Error.WriteLine($"Timed out waiting for graceful shutdown demo: {exception.Message}");
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
        GracefulShutdownMessage message)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = $"graceful-{message.Mode}",
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

            await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
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

internal sealed record GracefulShutdownMessage(string Mode);
