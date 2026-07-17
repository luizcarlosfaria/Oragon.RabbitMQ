using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

internal static class RpcRequestReplyDemo
{
    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string serverQueue = options.ResourceName(demo, "server");
        string correlationId = $"rpc-{Guid.NewGuid():N}";
        var request = new RpcRequest(13, 29);

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"Server queue: {serverQueue}");
        Console.WriteLine($"CorrelationId: {correlationId}");

        using IConnection connection = await RabbitMqDemoClient.CreateConnectionAsync(options).ConfigureAwait(false);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));
        builder.Services.AddRabbitMQConsumer();
        builder.Services.AddSystemTextJsonAmqpSerializer();
        builder.Services.AddSingleton(connection);
        builder.Services.AddScoped(_ => new RpcCalculatorService(adjustment: 0));

        IHost host = builder.Build();
        bool hostStarted = false;
        bool hostStopped = false;
        try
        {
            _ = host.Services
                .MapQueue(serverQueue, ([FromServices] RpcCalculatorService service, RpcRequest body) =>
                {
                    RpcResponse response = service.Add(body);
                    Console.WriteLine($"RPC handled: {body.Left}+{body.Right}={response.Result}");
                    return AmqpResults.ReplyAndAck(response);
                })
                .WithPrefetch(1)
                .WithConsumerTag("oragon-demo-05-rpc-request-reply")
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredService<IConnection>()))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>())
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    _ = await channel.QueueDeclareAsync(
                        queue: serverQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(serverQueue, cancellationToken).ConfigureAwait(false);
                });

            await host.StartAsync().ConfigureAwait(false);
            hostStarted = true;

            using IChannel clientChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);
            QueueDeclareOk replyQueue = await clientChannel.QueueDeclareAsync(
                queue: string.Empty,
                durable: false,
                exclusive: true,
                autoDelete: true).ConfigureAwait(false);

            Console.WriteLine($"Reply queue: {replyQueue.QueueName}");

            await PublishRequestAsync(
                clientChannel,
                serverQueue,
                replyQueue.QueueName,
                correlationId,
                request).ConfigureAwait(false);

            RpcClientResult? response = await WaitForReplyAsync(
                clientChannel,
                replyQueue.QueueName,
                correlationId,
                TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            RpcClientResult? timeoutResult = await WaitForReplyAsync(
                clientChannel,
                replyQueue.QueueName,
                "missing-correlation",
                TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);

            await host.StopAsync().ConfigureAwait(false);
            hostStopped = true;

            QueueDeclareOk serverState = await clientChannel.QueueDeclarePassiveAsync(serverQueue).ConfigureAwait(false);

            var failures = new List<string>();
            Check(failures, "response", response?.Message?.Result == 42, response?.Message?.Result);
            Check(failures, "correlation", response?.CorrelationId == correlationId, response?.CorrelationId);
            Check(failures, "timeout", timeoutResult == null, timeoutResult?.CorrelationId);
            Check(failures, "serverReady", serverState.MessageCount == 0, serverState.MessageCount);

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            Console.WriteLine($"Response result: {response?.Message?.Result}");
            Console.WriteLine($"Response correlation: {response?.CorrelationId}");
            Console.WriteLine($"Timeout returned null: {timeoutResult == null}");
            Console.WriteLine($"Server ready messages: {serverState.MessageCount}");
            Console.WriteLine(failures.Count == 0 ? "Demo 05 succeeded." : "Demo 05 failed.");

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

    private static async Task PublishRequestAsync(
        IChannel channel,
        string serverQueue,
        string replyQueue,
        string correlationId,
        RpcRequest request)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            CorrelationId = correlationId,
            ReplyTo = replyQueue,
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: serverQueue,
            mandatory: true,
            basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(request)).ConfigureAwait(false);
    }

    private static async Task<RpcClientResult?> WaitForReplyAsync(
        IChannel channel,
        string replyQueue,
        string correlationId,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);

        do
        {
            BasicGetResult? result = await channel.BasicGetAsync(replyQueue, autoAck: false).ConfigureAwait(false);
            if (result == null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
                continue;
            }

            if (string.Equals(result.BasicProperties.CorrelationId, correlationId, StringComparison.Ordinal))
            {
                RpcResponse? message = JsonSerializer.Deserialize<RpcResponse>(result.Body.Span);
                await channel.BasicAckAsync(result.DeliveryTag, multiple: false).ConfigureAwait(false);
                return new RpcClientResult(message, result.BasicProperties.CorrelationId);
            }

            await channel.BasicNackAsync(result.DeliveryTag, multiple: false, requeue: true).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return null;
    }

    private static void Check(List<string> failures, string name, bool success, object? actual)
    {
        if (!success)
        {
            failures.Add($"{name} mismatch. Actual: {actual ?? "(null)"}");
        }
    }
}

internal sealed record RpcRequest(int Left, int Right);

internal sealed record RpcResponse(int Result);

internal sealed record RpcClientResult(RpcResponse? Message, string? CorrelationId);

internal sealed class RpcCalculatorService
{
    private readonly int adjustment;

    public RpcCalculatorService(int adjustment)
    {
        this.adjustment = adjustment;
    }

    public RpcResponse Add(RpcRequest request) => new(request.Left + request.Right + this.adjustment);
}
