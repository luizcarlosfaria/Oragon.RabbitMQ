using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

internal static class SerializersDemo
{
    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string systemTextQueue = options.ResourceName(demo, "system-text-json");
        string newtonsoftQueue = options.ResourceName(demo, "newtonsoft");
        var systemTextObserved = new TaskCompletionSource<SerializerSystemTextMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newtonsoftObserved = new TaskCompletionSource<SerializerNewtonsoftMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"System.Text.Json queue: {systemTextQueue}");
        Console.WriteLine($"Newtonsoft.Json queue: {newtonsoftQueue}");

        using IConnection connection = await RabbitMqDemoClient.CreateConnectionAsync(options).ConfigureAwait(false);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));
        builder.Services.AddRabbitMQConsumer();
        builder.Services.AddSingleton(connection);
        builder.Services.AddSystemTextJsonAmqpSerializer(
            key: "stj",
            options: new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        builder.Services.AddNewtonsoftAmqpSerializer(
            key: "newtonsoft",
            options: new JsonSerializerSettings
            {
                Converters = { new StringEnumConverter() },
            });

        IHost host = builder.Build();
        bool hostStarted = false;
        bool hostStopped = false;
        try
        {
            _ = host.Services
                .MapQueue(systemTextQueue, (SerializerSystemTextMessage body) =>
                {
                    Console.WriteLine($"System.Text.Json received: source={body.Source} value={body.Value}");
                    _ = systemTextObserved.TrySetResult(body);
                    return Task.CompletedTask;
                })
                .WithPrefetch(1)
                .WithConsumerTag("oragon-demo-07-system-text-json")
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredService<IConnection>()))
                .WithSerializer(services =>
                    services.GetRequiredKeyedService<IAmqpSerializer>("stj"))
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    _ = await channel.QueueDeclareAsync(
                        queue: systemTextQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(systemTextQueue, cancellationToken).ConfigureAwait(false);
                });

            _ = host.Services
                .MapQueue(newtonsoftQueue, (SerializerNewtonsoftMessage body) =>
                {
                    Console.WriteLine($"Newtonsoft.Json received: source={body.Source} mode={body.Mode}");
                    _ = newtonsoftObserved.TrySetResult(body);
                    return Task.CompletedTask;
                })
                .WithPrefetch(1)
                .WithConsumerTag("oragon-demo-07-newtonsoft")
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredService<IConnection>()))
                .WithSerializer(services =>
                    services.GetRequiredKeyedService<IAmqpSerializer>("newtonsoft"))
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    _ = await channel.QueueDeclareAsync(
                        queue: newtonsoftQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(newtonsoftQueue, cancellationToken).ConfigureAwait(false);
                });

            await host.StartAsync().ConfigureAwait(false);
            hostStarted = true;

            using IChannel publishChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);
            await PublishRawJsonAsync(
                publishChannel,
                systemTextQueue,
                """{"source":"system-text-json","value":7}""",
                "serializer-stj").ConfigureAwait(false);

            await PublishRawJsonAsync(
                publishChannel,
                newtonsoftQueue,
                """{"Source":"newtonsoft","Mode":"Fast"}""",
                "serializer-newtonsoft").ConfigureAwait(false);

            SerializerSystemTextMessage systemTextMessage = await systemTextObserved.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            SerializerNewtonsoftMessage newtonsoftMessage = await newtonsoftObserved.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            QueueDeclareOk systemTextState = await WaitForReadyCountAsync(
                publishChannel,
                systemTextQueue,
                expectedCount: 0,
                timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            QueueDeclareOk newtonsoftState = await WaitForReadyCountAsync(
                publishChannel,
                newtonsoftQueue,
                expectedCount: 0,
                timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            await host.StopAsync().ConfigureAwait(false);
            hostStopped = true;

            var failures = new List<string>();
            Check(failures, "systemTextSource", systemTextMessage.Source == "system-text-json", systemTextMessage.Source);
            Check(failures, "systemTextValue", systemTextMessage.Value == 7, systemTextMessage.Value);
            Check(failures, "newtonsoftSource", newtonsoftMessage.Source == "newtonsoft", newtonsoftMessage.Source);
            Check(failures, "newtonsoftMode", newtonsoftMessage.Mode == SerializerMode.Fast, newtonsoftMessage.Mode);
            Check(failures, "systemTextReady", systemTextState.MessageCount == 0, systemTextState.MessageCount);
            Check(failures, "newtonsoftReady", newtonsoftState.MessageCount == 0, newtonsoftState.MessageCount);

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            Console.WriteLine($"System.Text.Json value: {systemTextMessage.Value}");
            Console.WriteLine($"Newtonsoft.Json mode: {newtonsoftMessage.Mode}");
            Console.WriteLine($"System.Text.Json ready messages: {systemTextState.MessageCount}");
            Console.WriteLine($"Newtonsoft.Json ready messages: {newtonsoftState.MessageCount}");
            Console.WriteLine(failures.Count == 0 ? "Demo 07 succeeded." : "Demo 07 failed.");

            return failures.Count == 0 ? 0 : 1;
        }
        catch (TimeoutException exception)
        {
            Console.Error.WriteLine($"Timed out waiting for serializer demo: {exception.Message}");
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

    private static async Task PublishRawJsonAsync(
        IChannel channel,
        string queueName,
        string json,
        string messageId)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = messageId,
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(json)).ConfigureAwait(false);
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

internal sealed record SerializerSystemTextMessage(string Source, int Value);

internal sealed record SerializerNewtonsoftMessage(string Source, SerializerMode Mode);

internal enum SerializerMode
{
    Unknown = 0,
    Fast = 1,
}
