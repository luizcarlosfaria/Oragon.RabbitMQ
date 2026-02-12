using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.Benchmarks.Infrastructure;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[GcServer(true)]
public class RpcBenchmark
{
    [Params("Small", "Medium")]
    public string MessageSize;

    private IConnection connection;
    private IChannel publishChannel;
    private string requestQueue;
    private string replyQueue;
    private IAmqpSerializer nativeSerializer;
    private ServiceProvider nativeServiceProvider;

    // Oragon RPC: pre-built infrastructure (P7 fix)
    private ServiceProvider oragonServiceProvider;
    private IHostedService oragonHostedService;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        await RabbitMqFixture.EnsureStartedAsync().ConfigureAwait(false);
        this.connection = await RabbitMqFixture.CreateConnectionAsync().ConfigureAwait(false);
        this.publishChannel = await this.connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true
            )).ConfigureAwait(false);

        var services = new ServiceCollection();
        _ = services.AddAmqpSerializer(options: MessagePayloads.JsonOptions);
        this.nativeServiceProvider = services.BuildServiceProvider();
        this.nativeSerializer = this.nativeServiceProvider.GetRequiredService<IAmqpSerializer>();
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        this.nativeServiceProvider?.Dispose();
        if (this.publishChannel != null)
        {
            await this.publishChannel.CloseAsync().ConfigureAwait(false);
            this.publishChannel.Dispose();
        }
        this.connection?.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        this.requestQueue = RabbitMqFixture.GenerateQueueName();
        this.replyQueue = RabbitMqFixture.GenerateQueueName();

        using IChannel setupChannel = this.connection.CreateChannelAsync().GetAwaiter().GetResult();
        _ = setupChannel.QueueDeclareAsync(this.requestQueue, false, false, false).GetAwaiter().GetResult();
        _ = setupChannel.QueueDeclareAsync(this.replyQueue, false, false, false).GetAwaiter().GetResult();
        setupChannel.CloseAsync().GetAwaiter().GetResult();

        // Pre-build Oragon infrastructure (P7 fix: move out of benchmark method)
        var services = new ServiceCollection();
        services.AddRabbitMQConsumer();
        _ = services.AddAmqpSerializer(options: MessagePayloads.JsonOptions);
        _ = services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        _ = services.AddSingleton(this.connection);

        this.oragonServiceProvider = services.BuildServiceProvider();

        // Dispatch by MessageSize (P6 fix)
        switch (this.MessageSize)
        {
            case "Small":
                _ = this.oragonServiceProvider.MapQueue(this.requestQueue, (SmallMessage msg) => AmqpResults.ReplyAndAck(msg))
                    .WithPrefetch(1).WithDispatchConcurrency(1)
                    .WithConnection((svc, ct) => Task.FromResult(svc.GetRequiredService<IConnection>()));
                break;
            case "Medium":
                _ = this.oragonServiceProvider.MapQueue(this.requestQueue, (MediumMessage msg) => AmqpResults.ReplyAndAck(msg))
                    .WithPrefetch(1).WithDispatchConcurrency(1)
                    .WithConnection((svc, ct) => Task.FromResult(svc.GetRequiredService<IConnection>()));
                break;
        }

        this.oragonHostedService = this.oragonServiceProvider.GetRequiredService<IHostedService>();
        this.oragonHostedService.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        this.oragonHostedService?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        this.oragonServiceProvider?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        this.oragonHostedService = null;
        this.oragonServiceProvider = null;

        RabbitMqFixture.DeleteQueueAsync(this.connection, this.requestQueue).GetAwaiter().GetResult();
        RabbitMqFixture.DeleteQueueAsync(this.connection, this.replyQueue).GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true, Description = "Native RPC (dedicated channel)")]
    public async Task Native_Rpc()
    {
        using var replyReceived = new ManualResetEventSlim(false);

        // Start reply consumer
        using IChannel replyChannel = await this.connection.CreateChannelAsync().ConfigureAwait(false);
        var replyConsumer = new AsyncEventingBasicConsumer(replyChannel);
        replyConsumer.ReceivedAsync += async (_, ea) =>
        {
            await replyChannel.BasicAckAsync(ea.DeliveryTag, false).ConfigureAwait(false);
            replyReceived.Set();
        };
        string replyConsumerTag = await replyChannel.BasicConsumeAsync(this.replyQueue, false, replyConsumer).ConfigureAwait(false);

        // Start request consumer (uses dedicated channel for reply, like Oragon)
        using IChannel requestChannel = await this.connection.CreateChannelAsync().ConfigureAwait(false);
        await requestChannel.BasicQosAsync(0, 1, false).ConfigureAwait(false);
        var requestConsumer = new AsyncEventingBasicConsumer(requestChannel);
        requestConsumer.ReceivedAsync += async (_, ea) =>
        {
            // Dispatch by MessageSize (P6 fix) + use IAmqpSerializer (P2 fix)
            object msg = this.MessageSize switch
            {
                "Small" => this.nativeSerializer.Deserialize<SmallMessage>(ea),
                "Medium" => this.nativeSerializer.Deserialize<MediumMessage>(ea),
                _ => throw new ArgumentException()
            };

            // Reply using dedicated channel (fair comparison with Oragon)
            using IChannel dedicatedReplyChannel = await this.connection.CreateChannelAsync().ConfigureAwait(false);
            var replyProps = new BasicProperties
            {
                CorrelationId = ea.BasicProperties.MessageId,
                MessageId = Guid.NewGuid().ToString("D")
            };
            byte[] replyBody = this.MessageSize switch
            {
                "Small" => MessagePayloads.SerializeToBytes((SmallMessage)msg),
                "Medium" => MessagePayloads.SerializeToBytes((MediumMessage)msg),
                _ => throw new ArgumentException()
            };
            await dedicatedReplyChannel.BasicPublishAsync(string.Empty, ea.BasicProperties.ReplyTo, false, replyProps, replyBody).ConfigureAwait(false);
            await dedicatedReplyChannel.CloseAsync().ConfigureAwait(false);

            await requestChannel.BasicAckAsync(ea.DeliveryTag, false).ConfigureAwait(false);
        };
        string requestConsumerTag = await requestChannel.BasicConsumeAsync(this.requestQueue, false, requestConsumer).ConfigureAwait(false);

        // Publish request
        ReadOnlyMemory<byte> body = MessagePayloads.GetBytesForSize(this.MessageSize);
        var props = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString("D"),
            ReplyTo = this.replyQueue
        };
        await this.publishChannel.BasicPublishAsync(string.Empty, this.requestQueue, false, props, body).ConfigureAwait(false);

        _ = replyReceived.Wait(TimeSpan.FromSeconds(10));

        await replyChannel.BasicCancelAsync(replyConsumerTag).ConfigureAwait(false);
        await replyChannel.CloseAsync().ConfigureAwait(false);
        await requestChannel.BasicCancelAsync(requestConsumerTag).ConfigureAwait(false);
        await requestChannel.CloseAsync().ConfigureAwait(false);
    }

    [Benchmark(Description = "Native RPC (channel reuse)")]
    public async Task Native_Rpc_ChannelReuse()
    {
        using var replyReceived = new ManualResetEventSlim(false);

        // Start reply consumer
        using IChannel replyChannel = await this.connection.CreateChannelAsync().ConfigureAwait(false);
        var replyConsumer = new AsyncEventingBasicConsumer(replyChannel);
        replyConsumer.ReceivedAsync += async (_, ea) =>
        {
            await replyChannel.BasicAckAsync(ea.DeliveryTag, false).ConfigureAwait(false);
            replyReceived.Set();
        };
        string replyConsumerTag = await replyChannel.BasicConsumeAsync(this.replyQueue, false, replyConsumer).ConfigureAwait(false);

        // Start request consumer (reuses same channel for reply - optimized, only safe with concurrency=1)
        using IChannel requestChannel = await this.connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false,
                consumerDispatchConcurrency: 1)).ConfigureAwait(false);
        await requestChannel.BasicQosAsync(0, 1, false).ConfigureAwait(false);
        var requestConsumer = new AsyncEventingBasicConsumer(requestChannel);
        requestConsumer.ReceivedAsync += async (_, ea) =>
        {
            // Dispatch by MessageSize (P6 fix) + use IAmqpSerializer (P2 fix)
            object msg = this.MessageSize switch
            {
                "Small" => this.nativeSerializer.Deserialize<SmallMessage>(ea),
                "Medium" => this.nativeSerializer.Deserialize<MediumMessage>(ea),
                _ => throw new ArgumentException()
            };

            // Reply using the SAME channel (optimized, avoids channel creation overhead)
            var replyProps = new BasicProperties
            {
                CorrelationId = ea.BasicProperties.MessageId,
                MessageId = Guid.NewGuid().ToString("D")
            };
            byte[] replyBody = this.MessageSize switch
            {
                "Small" => MessagePayloads.SerializeToBytes((SmallMessage)msg),
                "Medium" => MessagePayloads.SerializeToBytes((MediumMessage)msg),
                _ => throw new ArgumentException()
            };
            await requestChannel.BasicPublishAsync(string.Empty, ea.BasicProperties.ReplyTo, false, replyProps, replyBody).ConfigureAwait(false);

            await requestChannel.BasicAckAsync(ea.DeliveryTag, false).ConfigureAwait(false);
        };
        string requestConsumerTag = await requestChannel.BasicConsumeAsync(this.requestQueue, false, requestConsumer).ConfigureAwait(false);

        // Publish request
        ReadOnlyMemory<byte> body = MessagePayloads.GetBytesForSize(this.MessageSize);
        var props = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString("D"),
            ReplyTo = this.replyQueue
        };
        await this.publishChannel.BasicPublishAsync(string.Empty, this.requestQueue, false, props, body).ConfigureAwait(false);

        _ = replyReceived.Wait(TimeSpan.FromSeconds(10));

        await replyChannel.BasicCancelAsync(replyConsumerTag).ConfigureAwait(false);
        await replyChannel.CloseAsync().ConfigureAwait(false);
        await requestChannel.BasicCancelAsync(requestConsumerTag).ConfigureAwait(false);
        await requestChannel.CloseAsync().ConfigureAwait(false);
    }

    [Benchmark(Description = "Oragon RPC (ReplyAndAck)")]
    public async Task Oragon_Rpc()
    {
        using var replyReceived = new ManualResetEventSlim(false);

        // Start reply consumer natively (just listening for the reply)
        using IChannel replyChannel = await this.connection.CreateChannelAsync().ConfigureAwait(false);
        var replyConsumer = new AsyncEventingBasicConsumer(replyChannel);
        replyConsumer.ReceivedAsync += async (_, ea) =>
        {
            await replyChannel.BasicAckAsync(ea.DeliveryTag, false).ConfigureAwait(false);
            replyReceived.Set();
        };
        string replyConsumerTag = await replyChannel.BasicConsumeAsync(this.replyQueue, false, replyConsumer).ConfigureAwait(false);

        // Oragon consumer already started in IterationSetup (P7 fix)

        // Publish request
        ReadOnlyMemory<byte> body = MessagePayloads.GetBytesForSize(this.MessageSize);
        var props = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString("D"),
            ReplyTo = this.replyQueue
        };
        await this.publishChannel.BasicPublishAsync(string.Empty, this.requestQueue, false, props, body).ConfigureAwait(false);

        _ = replyReceived.Wait(TimeSpan.FromSeconds(10));

        await replyChannel.BasicCancelAsync(replyConsumerTag).ConfigureAwait(false);
        await replyChannel.CloseAsync().ConfigureAwait(false);
    }
}
