using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.AspireClient;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

internal static class KeyedRabbitMqDemo
{
    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string primaryQueue = options.ResourceName(demo, "primary");
        string secondaryQueue = options.ResourceName(demo, "secondary");
        var observations = new ConcurrentBag<KeyedRabbitMqObservation>();
        var primaryObserved = new TaskCompletionSource<KeyedRabbitMqObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondaryObserved = new TaskCompletionSource<KeyedRabbitMqObservation>(TaskCreationOptions.RunContinuationsAsynchronously);

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"Primary queue: {primaryQueue}");
        Console.WriteLine($"Secondary queue: {secondaryQueue}");

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));
        builder.Services.AddRabbitMQConsumer();
        builder.Services.AddSystemTextJsonAmqpSerializer();
        builder.AddKeyedRabbitMQClient(
            "primary",
            settings =>
            {
                settings.ConnectionString = options.AmqpUri;
                settings.MaxConnectRetryCount = 0;
                settings.DisableTracing = true;
                settings.DisableHealthChecks = true;
            },
            factory =>
            {
                factory.ClientProvidedName = "oragon-demo-09-primary";
                factory.AutomaticRecoveryEnabled = false;
                factory.TopologyRecoveryEnabled = false;
            });
        builder.AddKeyedRabbitMQClient(
            "secondary",
            settings =>
            {
                settings.ConnectionString = options.AmqpUri;
                settings.MaxConnectRetryCount = 0;
                settings.DisableTracing = true;
                settings.DisableHealthChecks = true;
            },
            factory =>
            {
                factory.ClientProvidedName = "oragon-demo-09-secondary";
                factory.AutomaticRecoveryEnabled = false;
                factory.TopologyRecoveryEnabled = false;
            });

        IHost host = builder.Build();
        bool hostStarted = false;
        bool hostStopped = false;
        try
        {
            _ = host.Services
                .MapQueue(primaryQueue, (KeyedRabbitMqMessage body, IConnection connection) =>
                    ObserveAsync("primary", body, connection, observations, primaryObserved))
                .WithPrefetch(1)
                .WithConsumerTag("oragon-demo-09-primary")
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredKeyedService<IConnection>("primary")))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>())
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    _ = await channel.QueueDeclareAsync(
                        queue: primaryQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(primaryQueue, cancellationToken).ConfigureAwait(false);
                });

            _ = host.Services
                .MapQueue(secondaryQueue, (KeyedRabbitMqMessage body, IConnection connection) =>
                    ObserveAsync("secondary", body, connection, observations, secondaryObserved))
                .WithPrefetch(1)
                .WithConsumerTag("oragon-demo-09-secondary")
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredKeyedService<IConnection>("secondary")))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>())
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    _ = await channel.QueueDeclareAsync(
                        queue: secondaryQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(secondaryQueue, cancellationToken).ConfigureAwait(false);
                });

            await host.StartAsync().ConfigureAwait(false);
            hostStarted = true;

            IConnection primaryConnection = host.Services.GetRequiredKeyedService<IConnection>("primary");
            IConnection secondaryConnection = host.Services.GetRequiredKeyedService<IConnection>("secondary");
            using IChannel primaryPublishChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(primaryConnection).ConfigureAwait(false);
            using IChannel secondaryPublishChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(secondaryConnection).ConfigureAwait(false);

            await PublishMessageAsync(
                primaryPublishChannel,
                primaryQueue,
                new KeyedRabbitMqMessage("primary", "from primary connection")).ConfigureAwait(false);
            await PublishMessageAsync(
                secondaryPublishChannel,
                secondaryQueue,
                new KeyedRabbitMqMessage("secondary", "from secondary connection")).ConfigureAwait(false);

            KeyedRabbitMqObservation primary = await primaryObserved.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            KeyedRabbitMqObservation secondary = await secondaryObserved.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            bool missingKeyFailedClearly = await VerifyMissingKeyFailsClearlyAsync().ConfigureAwait(false);

            await host.StopAsync().ConfigureAwait(false);
            hostStopped = true;

            QueueDeclareOk primaryState = await primaryPublishChannel.QueueDeclarePassiveAsync(primaryQueue).ConfigureAwait(false);
            QueueDeclareOk secondaryState = await secondaryPublishChannel.QueueDeclarePassiveAsync(secondaryQueue).ConfigureAwait(false);

            var failures = new List<string>();
            Check(failures, "primaryRoute", primary.Route == "primary", primary.Route);
            Check(failures, "primaryConnection", primary.ClientProvidedName == "oragon-demo-09-primary", primary.ClientProvidedName);
            Check(failures, "secondaryRoute", secondary.Route == "secondary", secondary.Route);
            Check(failures, "secondaryConnection", secondary.ClientProvidedName == "oragon-demo-09-secondary", secondary.ClientProvidedName);
            Check(failures, "observationCount", observations.Count == 2, observations.Count);
            Check(failures, "primaryReady", primaryState.MessageCount == 0, primaryState.MessageCount);
            Check(failures, "secondaryReady", secondaryState.MessageCount == 0, secondaryState.MessageCount);
            Check(failures, "missingKey", missingKeyFailedClearly, missingKeyFailedClearly);

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            Console.WriteLine($"Primary consumer connection: {primary.ClientProvidedName}");
            Console.WriteLine($"Secondary consumer connection: {secondary.ClientProvidedName}");
            Console.WriteLine($"Misconfigured key failed clearly: {missingKeyFailedClearly}");
            Console.WriteLine($"Primary ready messages: {primaryState.MessageCount}");
            Console.WriteLine($"Secondary ready messages: {secondaryState.MessageCount}");
            Console.WriteLine(failures.Count == 0 ? "Demo 09 succeeded." : "Demo 09 failed.");

            return failures.Count == 0 ? 0 : 1;
        }
        catch (TimeoutException exception)
        {
            Console.Error.WriteLine($"Timed out waiting for keyed RabbitMQ demo: {exception.Message}");
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

    private static Task ObserveAsync(
        string expectedRoute,
        KeyedRabbitMqMessage message,
        IConnection connection,
        ConcurrentBag<KeyedRabbitMqObservation> observations,
        TaskCompletionSource<KeyedRabbitMqObservation> observed)
    {
        var observation = new KeyedRabbitMqObservation(
            message.Route,
            expectedRoute,
            connection.ClientProvidedName ?? string.Empty);

        observations.Add(observation);
        Console.WriteLine(
            $"{expectedRoute} handler received route={message.Route} client={connection.ClientProvidedName}");
        _ = observed.TrySetResult(observation);
        return Task.CompletedTask;
    }

    private static async Task PublishMessageAsync(
        IChannel channel,
        string queueName,
        KeyedRabbitMqMessage message)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = $"keyed-{message.Route}",
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: true,
            basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(message)).ConfigureAwait(false);
    }

    private static async Task<bool> VerifyMissingKeyFailsClearlyAsync()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.None));
        builder.Services.AddRabbitMQConsumer();
        builder.Services.AddSystemTextJsonAmqpSerializer();

        IHost host = builder.Build();
        try
        {
            _ = host.Services
                .MapQueue("oragon.demo.09.missing-key", (KeyedRabbitMqMessage _) => Task.CompletedTask)
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredKeyedService<IConnection>("missing")))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>());

            await host.StartAsync().ConfigureAwait(false);
            await host.StopAsync().ConfigureAwait(false);
            return false;
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine($"Missing keyed connection error: {exception.Message}");
            return exception.Message.Contains("RabbitMQ.Client.IConnection", StringComparison.Ordinal);
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

    private static void Check(List<string> failures, string name, bool success, object? actual)
    {
        if (!success)
        {
            failures.Add($"{name} mismatch. Actual: {actual ?? "(null)"}");
        }
    }
}

internal sealed record KeyedRabbitMqMessage(string Route, string Payload);

internal sealed record KeyedRabbitMqObservation(
    string Route,
    string ExpectedRoute,
    string ClientProvidedName);
