using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.Benchmarks.Infrastructure;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.Benchmarks;

public static class SmokeTest
{
    public static async Task<int> RunAsync()
    {
        int failures = 0;

        try
        {
            Log("=== Benchmark Smoke Test ===");
            Log("");

            // 1. RabbitMQ Container
            Log("[1/6] Starting RabbitMQ container...");
            await RabbitMqFixture.EnsureStartedAsync().ConfigureAwait(false);
            Log("  OK - Container started");

            // 2. Connection
            Log("[2/6] Testing connection...");
            using IConnection connection = await RabbitMqFixture.CreateConnectionAsync().ConfigureAwait(false);
            Log($"  OK - Connected: {connection.IsOpen}");

            // Build shared DI for native tests
            var services = new ServiceCollection();
            _ = services.AddAmqpSerializer(options: MessagePayloads.JsonOptions);
            using ServiceProvider nativeSp = services.BuildServiceProvider();
            IAmqpSerializer nativeSerializer = nativeSp.GetRequiredService<IAmqpSerializer>();

            // 3. Native consumer (publish + consume)
            Log("[3/6] Testing native consumer pipeline...");
            failures += await TestNativeConsumerAsync(connection, nativeSp, nativeSerializer).ConfigureAwait(false);

            // 4. Oragon consumer (publish + consume via MapQueue)
            Log("[4/6] Testing Oragon consumer pipeline...");
            failures += await TestOragonConsumerAsync(connection).ConfigureAwait(false);

            // 5. Serialization paths
            Log("[5/6] Testing serialization paths...");
            failures += TestSerialization();

            // 6. RPC path (Oragon ReplyAndAck)
            Log("[6/6] Testing Oragon RPC (ReplyAndAck)...");
            failures += await TestOragonRpcAsync(connection).ConfigureAwait(false);

            Log("");
            if (failures == 0)
                Log("=== ALL SMOKE TESTS PASSED ===");
            else
                Log($"=== {failures} SMOKE TEST(S) FAILED ===");
        }
        catch (Exception ex)
        {
            Log($"FATAL: {ex}");
            failures++;
        }

        return failures;
    }

    private static async Task<int> TestNativeConsumerAsync(IConnection connection, IServiceProvider serviceProvider, IAmqpSerializer serializer)
    {
        string queueName = RabbitMqFixture.GenerateQueueName();
        try
        {
            await RabbitMqFixture.PreloadQueueAsync(connection, queueName, 5, MessagePayloads.SmallBytes).ConfigureAwait(false);

            using var countdown = new CountdownEvent(5);
            var (channel, consumerTag) = await NativeConsumerHelper.StartConsumingNoOpAsync<SmallMessage>(
                connection, queueName, 5, 1, countdown, serviceProvider, serializer).ConfigureAwait(false);

            bool drained = countdown.Wait(TimeSpan.FromSeconds(10));
            await NativeConsumerHelper.StopConsumingAsync(channel, consumerTag).ConfigureAwait(false);

            if (drained)
            {
                Log("  OK - Native consumed 5 messages");
                return 0;
            }
            else
            {
                Log($"  FAIL - Native drained only {5 - countdown.CurrentCount}/5 messages");
                return 1;
            }
        }
        finally
        {
            await RabbitMqFixture.DeleteQueueAsync(connection, queueName).ConfigureAwait(false);
        }
    }

    private static async Task<int> TestOragonConsumerAsync(IConnection connection)
    {
        string queueName = RabbitMqFixture.GenerateQueueName();
        try
        {
            await RabbitMqFixture.PreloadQueueAsync(connection, queueName, 5, MessagePayloads.SmallBytes).ConfigureAwait(false);

            using var countdown = new CountdownEvent(5);
            await using OragonConsumerHelper helper = await OragonConsumerHelper.StartConsumingNoOpAsync<SmallMessage>(
                connection, queueName, 5, 1, countdown).ConfigureAwait(false);

            bool drained = countdown.Wait(TimeSpan.FromSeconds(10));

            if (drained)
            {
                Log("  OK - Oragon consumed 5 messages");
                return 0;
            }
            else
            {
                Log($"  FAIL - Oragon drained only {5 - countdown.CurrentCount}/5 messages");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Log($"  FAIL - Oragon consumer error: {ex.Message}");
            return 1;
        }
        finally
        {
            await RabbitMqFixture.DeleteQueueAsync(connection, queueName).ConfigureAwait(false);
        }
    }

    private static int TestSerialization()
    {
        int failures = 0;

        // Oragon serializer (triple-copy path)
        var oragonSerializer = new SystemTextJsonAmqpSerializer(MessagePayloads.JsonOptions);
        var eventArgs = new BasicDeliverEventArgs(
            consumerTag: "smoke",
            deliveryTag: 1,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "smoke-queue",
            properties: new BasicProperties(),
            body: MessagePayloads.SmallBytes);

        object oragonResult = oragonSerializer.Deserialize(eventArgs, typeof(SmallMessage));
        if (oragonResult is SmallMessage sm1 && sm1.Id == 1)
        {
            Log("  OK - Oragon serializer (ToArray>GetString>Deserialize)");
        }
        else
        {
            Log("  FAIL - Oragon serializer returned unexpected result");
            failures++;
        }

        // Native Span path
        object nativeResult = JsonSerializer.Deserialize(eventArgs.Body.Span, typeof(SmallMessage), MessagePayloads.JsonOptions);
        if (nativeResult is SmallMessage sm2 && sm2.Id == 1)
        {
            Log("  OK - Native serializer (Deserialize from Span)");
        }
        else
        {
            Log("  FAIL - Native Span serializer returned unexpected result");
            failures++;
        }

        // Native Utf8JsonReader path
        var reader = new Utf8JsonReader(eventArgs.Body.Span);
        object readerResult = JsonSerializer.Deserialize(ref reader, typeof(SmallMessage), MessagePayloads.JsonOptions);
        if (readerResult is SmallMessage sm3 && sm3.Id == 1)
        {
            Log("  OK - Native serializer (Utf8JsonReader)");
        }
        else
        {
            Log("  FAIL - Native Utf8JsonReader serializer returned unexpected result");
            failures++;
        }

        return failures;
    }

    private static async Task<int> TestOragonRpcAsync(IConnection connection)
    {
        string requestQueue = RabbitMqFixture.GenerateQueueName();
        string replyQueue = RabbitMqFixture.GenerateQueueName();

        try
        {
            using IChannel setupChannel = await connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true)).ConfigureAwait(false);

            await setupChannel.QueueDeclareAsync(requestQueue, false, false, false).ConfigureAwait(false);
            await setupChannel.QueueDeclareAsync(replyQueue, false, false, false).ConfigureAwait(false);

            using var replyReceived = new ManualResetEventSlim(false);

            // Reply listener
            using IChannel replyChannel = await connection.CreateChannelAsync().ConfigureAwait(false);
            var replyConsumer = new AsyncEventingBasicConsumer(replyChannel);
            replyConsumer.ReceivedAsync += async (_, ea) =>
            {
                await replyChannel.BasicAckAsync(ea.DeliveryTag, false).ConfigureAwait(false);
                replyReceived.Set();
            };
            string replyConsumerTag = await replyChannel.BasicConsumeAsync(replyQueue, false, replyConsumer).ConfigureAwait(false);

            // Oragon RPC consumer
            var services = new ServiceCollection();
            services.AddRabbitMQConsumer();
            _ = services.AddAmqpSerializer(options: MessagePayloads.JsonOptions);
            _ = services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
            _ = services.AddSingleton(connection);

            ServiceProvider sp = services.BuildServiceProvider();

            _ = sp.MapQueue(requestQueue, (SmallMessage msg) => AmqpResults.ReplyAndAck(msg))
                .WithPrefetch(1)
                .WithDispatchConcurrency(1)
                .WithConnection((svc, ct) => Task.FromResult(
                    svc.GetRequiredService<IConnection>()));

            IHostedService hostedService = sp.GetRequiredService<IHostedService>();
            await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(false);

            // Publish request
            var props = new BasicProperties
            {
                MessageId = Guid.NewGuid().ToString("D"),
                ReplyTo = replyQueue
            };
            await setupChannel.BasicPublishAsync(string.Empty, requestQueue, false, props, MessagePayloads.SmallBytes).ConfigureAwait(false);

            bool replied = replyReceived.Wait(TimeSpan.FromSeconds(10));

            await hostedService.StopAsync(CancellationToken.None).ConfigureAwait(false);
            await replyChannel.BasicCancelAsync(replyConsumerTag).ConfigureAwait(false);
            await replyChannel.CloseAsync().ConfigureAwait(false);
            await setupChannel.CloseAsync().ConfigureAwait(false);
            await sp.DisposeAsync().ConfigureAwait(false);

            if (replied)
            {
                Log("  OK - Oragon RPC (ReplyAndAck) received reply");
                return 0;
            }
            else
            {
                Log("  FAIL - Oragon RPC did not receive reply within 10s");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Log($"  FAIL - Oragon RPC error: {ex.Message}");
            return 1;
        }
        finally
        {
            await RabbitMqFixture.DeleteQueueAsync(connection, requestQueue).ConfigureAwait(false);
            await RabbitMqFixture.DeleteQueueAsync(connection, replyQueue).ConfigureAwait(false);
        }
    }

    private static void Log(string message)
    {
        Console.WriteLine(message);
    }
}
