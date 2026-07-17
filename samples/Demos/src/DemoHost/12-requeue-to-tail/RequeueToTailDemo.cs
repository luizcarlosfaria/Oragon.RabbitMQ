using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Demos;

internal static class RequeueToTailDemo
{
    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string queueName = options.ResourceName(demo, "input");
        var deliveryOrder = new ConcurrentQueue<string>();
        var finalAObserved = new TaskCompletionSource<RequeueToTailObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        int aWasRequeued = 0;

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"Queue: {queueName}");

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
                .MapQueue(queueName, IAmqpResult (
                    RequeueToTailMessage body,
                    byte priority,
                    string correlationId,
                    IDictionary<string, object> headers) =>
                {
                    deliveryOrder.Enqueue(body.Id);
                    Console.WriteLine($"Delivery observed: {body.Id}");

                    if (body.Id == "A" && Interlocked.CompareExchange(ref aWasRequeued, 1, 0) == 0)
                    {
                        Console.WriteLine("A returned RequeueToTail.");
                        return AmqpResults.RequeueToTail();
                    }

                    if (body.Id == "A")
                    {
                        _ = finalAObserved.TrySetResult(new RequeueToTailObservation(
                            priority,
                            correlationId,
                            headers.ContainsKey("x-app"),
                            headers.ContainsKey("x-death"),
                            headers.ContainsKey("x-delivery-count"),
                            headers.ContainsKey("x-first-death-queue")));
                    }

                    return AmqpResults.Ack();
                })
                .WithPrefetch(1)
                .WithDispatchConcurrency(1)
                .WithConsumerTag("oragon-demo-12-requeue-to-tail")
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredService<IConnection>()))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>())
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    _ = await channel.QueueDeclareAsync(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(queueName, cancellationToken).ConfigureAwait(false);
                });

            await host.StartAsync().ConfigureAwait(false);
            hostStarted = true;

            using IChannel publishChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);
            await PublishMessageAsync(
                publishChannel,
                queueName,
                new RequeueToTailMessage("A"),
                priority: 7,
                correlationId: "correlation-A",
                includeBrokerHeaders: true).ConfigureAwait(false);
            await PublishMessageAsync(
                publishChannel,
                queueName,
                new RequeueToTailMessage("B"),
                priority: 1,
                correlationId: "correlation-B",
                includeBrokerHeaders: false).ConfigureAwait(false);
            await PublishMessageAsync(
                publishChannel,
                queueName,
                new RequeueToTailMessage("C"),
                priority: 1,
                correlationId: "correlation-C",
                includeBrokerHeaders: false).ConfigureAwait(false);

            RequeueToTailObservation finalA = await finalAObserved.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            QueueDeclareOk state = await WaitForReadyCountAsync(
                publishChannel,
                queueName,
                expectedCount: 0,
                timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            await host.StopAsync().ConfigureAwait(false);
            hostStopped = true;

            string observedOrder = string.Join(",", deliveryOrder.ToArray());
            var failures = new List<string>();
            Check(failures, "deliveryOrder", observedOrder == "A,B,C,A", observedOrder);
            Check(failures, "priority", finalA.Priority == 7, finalA.Priority);
            Check(failures, "correlationId", finalA.CorrelationId == "correlation-A", finalA.CorrelationId);
            Check(failures, "applicationHeader", finalA.HasApplicationHeader, finalA.HasApplicationHeader);
            Check(failures, "xDeathPreserved", finalA.HasXDeath, finalA.HasXDeath);
            Check(failures, "xDeliveryCountFiltered", !finalA.HasXDeliveryCount, finalA.HasXDeliveryCount);
            Check(failures, "xFirstDeathPreserved", finalA.HasXFirstDeathQueue, finalA.HasXFirstDeathQueue);
            Check(failures, "ready", state.MessageCount == 0, state.MessageCount);

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            Console.WriteLine($"Delivery order: {observedOrder}");
            Console.WriteLine($"Final A priority: {finalA.Priority}");
            Console.WriteLine($"Final A correlation: {finalA.CorrelationId}");
            Console.WriteLine($"Final A kept x-app: {finalA.HasApplicationHeader}");
            Console.WriteLine($"Final A has x-death: {finalA.HasXDeath}");
            Console.WriteLine($"Final A has x-delivery-count: {finalA.HasXDeliveryCount}");
            Console.WriteLine($"Ready messages: {state.MessageCount}");
            Console.WriteLine(failures.Count == 0 ? "Demo 12 succeeded." : "Demo 12 failed.");

            return failures.Count == 0 ? 0 : 1;
        }
        catch (TimeoutException exception)
        {
            Console.Error.WriteLine($"Timed out waiting for requeue-to-tail demo: {exception.Message}");
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
        RequeueToTailMessage message,
        byte priority,
        string correlationId,
        bool includeBrokerHeaders)
    {
        var headers = new Dictionary<string, object?>
        {
            ["x-app"] = "keep",
        };

        if (includeBrokerHeaders)
        {
            headers["x-death"] = "keep";
            headers["x-delivery-count"] = 5L;
            headers["x-first-death-queue"] = "keep";
        }

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Priority = priority,
            CorrelationId = correlationId,
            MessageId = $"requeue-{message.Id}",
            Headers = headers,
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

internal sealed record RequeueToTailMessage(string Id);

internal sealed record RequeueToTailObservation(
    byte Priority,
    string CorrelationId,
    bool HasApplicationHeader,
    bool HasXDeath,
    bool HasXDeliveryCount,
    bool HasXFirstDeathQueue);
