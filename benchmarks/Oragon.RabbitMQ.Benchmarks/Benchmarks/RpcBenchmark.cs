using System.Collections.Concurrent;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.Benchmarks.Infrastructure;
using Oragon.RabbitMQ.Consumer.Actions;
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
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
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
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
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
            SmallMessage msg = JsonSerializer.Deserialize<SmallMessage>(ea.Body.Span, MessagePayloads.JsonOptions);

            // Reply using dedicated channel (fair comparison with Oragon)
            using IChannel dedicatedReplyChannel = await this.connection.CreateChannelAsync().ConfigureAwait(false);
            var replyProps = new BasicProperties
            {
                CorrelationId = ea.BasicProperties.MessageId,
                MessageId = Guid.NewGuid().ToString("D")
            };
            byte[] replyBody = MessagePayloads.SerializeToBytes(msg);
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
            SmallMessage msg = JsonSerializer.Deserialize<SmallMessage>(ea.Body.Span, MessagePayloads.JsonOptions);

            // Reply using the SAME channel (optimized, avoids channel creation overhead)
            var replyProps = new BasicProperties
            {
                CorrelationId = ea.BasicProperties.MessageId,
                MessageId = Guid.NewGuid().ToString("D")
            };
            byte[] replyBody = MessagePayloads.SerializeToBytes(msg);
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

        // Start Oragon consumer with ReplyAndAck
        var services = new ServiceCollection();
        services.AddRabbitMQConsumer();
        _ = services.AddAmqpSerializer(options: MessagePayloads.JsonOptions);
        _ = services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        _ = services.AddSingleton(this.connection);

        ServiceProvider sp = services.BuildServiceProvider();

        _ = sp.MapQueue(this.requestQueue, (SmallMessage msg) => AmqpResults.ReplyAndAck(msg))
            .WithPrefetch(1)
            .WithDispatchConcurrency(1)
            .WithConnection((svc, ct) => Task.FromResult(svc.GetRequiredService<IConnection>()));

        IHostedService hostedService = sp.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(false);

        // Publish request
        ReadOnlyMemory<byte> body = MessagePayloads.GetBytesForSize(this.MessageSize);
        var props = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString("D"),
            ReplyTo = this.replyQueue
        };
        await this.publishChannel.BasicPublishAsync(string.Empty, this.requestQueue, false, props, body).ConfigureAwait(false);

        _ = replyReceived.Wait(TimeSpan.FromSeconds(10));

        await hostedService.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await replyChannel.BasicCancelAsync(replyConsumerTag).ConfigureAwait(false);
        await replyChannel.CloseAsync().ConfigureAwait(false);
        await sp.DisposeAsync().ConfigureAwait(false);
    }
}
