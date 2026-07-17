using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

internal static class MinimalConsumerDemo
{
    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string queueName = options.ResourceName(demo, "input");
        var message = new MinimalConsumerMessage(Guid.NewGuid().ToString("N"), "hello from minimal consumer");
        var received = new TaskCompletionSource<MinimalConsumerMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"Queue: {queueName}");

        using IConnection connection = await RabbitMqDemoClient.CreateConnectionAsync(options).ConfigureAwait(false);
        using IChannel setupChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);

        await RabbitMqDemoClient.DeclareDurableQueueAsync(setupChannel, queueName).ConfigureAwait(false);
        await RabbitMqDemoClient.PurgeQueueAsync(setupChannel, queueName).ConfigureAwait(false);

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
                .MapQueue(queueName, (MinimalConsumerMessage body) =>
                {
                    Console.WriteLine($"Received: id={body.Id} text={body.Text}");
                    _ = received.TrySetResult(body);
                    return Task.CompletedTask;
                })
                .WithPrefetch(1)
                .WithConsumerTag("oragon-demo-01-minimal-consumer")
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredService<IConnection>()))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>());

            await host.StartAsync().ConfigureAwait(false);
            hostStarted = true;
            await RabbitMqDemoClient.PublishJsonAsync(setupChannel, queueName, message).ConfigureAwait(false);

            MinimalConsumerMessage actual = await received.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            await host.StopAsync().ConfigureAwait(false);
            hostStopped = true;

            QueueDeclareOk state = await setupChannel.QueueDeclarePassiveAsync(queueName).ConfigureAwait(false);
            bool success = actual == message && state.MessageCount == 0;

            Console.WriteLine($"Ready messages after processing: {state.MessageCount}");
            Console.WriteLine(success ? "Demo 01 succeeded." : "Demo 01 failed.");

            return success ? 0 : 1;
        }
        catch (TimeoutException exception)
        {
            Console.Error.WriteLine($"Timed out waiting for message consumption: {exception.Message}");
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
}

public sealed record MinimalConsumerMessage(string Id, string Text);
