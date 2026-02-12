using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Oragon.RabbitMQ.Benchmarks.Infrastructure;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[GcServer(true)]
public class LatencyBenchmark
{
    [Params("Small", "Medium", "Large")]
    public string MessageSize;

    [Params("NoOp", "CpuBound", "IoBound")]
    public string HandlerType;

    private IConnection connection;
    private IChannel publishChannel;
    private ServiceProvider nativeServiceProvider;
    private IAmqpSerializer nativeSerializer;

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

    private Func<TMessage, Task> GetHandler<TMessage>() => this.HandlerType switch
    {
        "NoOp" => _ => Task.CompletedTask,
        "CpuBound" => _ =>
        {
            int hash = 0;
            for (int i = 0; i < 1000; i++) hash = HashCode.Combine(hash, i);
            return Task.CompletedTask;
        },
        "IoBound" => _ => Task.Delay(5),
        _ => throw new ArgumentException()
    };

    private async Task<(IChannel Channel, string ConsumerTag)> StartNativeConsumer<TMessage>(
        string queueName, CountdownEvent countdown)
    {
        return await NativeConsumerHelper.StartConsumingAsync(
            this.connection, queueName, 1, 1,
            this.GetHandler<TMessage>(), countdown,
            this.nativeServiceProvider, this.nativeSerializer).ConfigureAwait(false);
    }

    [Benchmark(Baseline = true, Description = "Native Latency")]
    public async Task Native_SingleMessage()
    {
        string queueName = RabbitMqFixture.GenerateQueueName();
        using var countdown = new CountdownEvent(1);

        try
        {
            _ = await this.publishChannel.QueueDeclareAsync(queueName, durable: false, exclusive: false, autoDelete: false).ConfigureAwait(false);

            var (channel, consumerTag) = this.MessageSize switch
            {
                "Small" => await this.StartNativeConsumer<SmallMessage>(queueName, countdown).ConfigureAwait(false),
                "Medium" => await this.StartNativeConsumer<MediumMessage>(queueName, countdown).ConfigureAwait(false),
                "Large" => await this.StartNativeConsumer<LargeMessage>(queueName, countdown).ConfigureAwait(false),
                _ => throw new ArgumentException()
            };

            ReadOnlyMemory<byte> body = MessagePayloads.GetBytesForSize(this.MessageSize);
            await this.publishChannel.BasicPublishAsync(string.Empty, queueName, false, body).ConfigureAwait(false);

            _ = countdown.Wait(TimeSpan.FromSeconds(10));
            await NativeConsumerHelper.StopConsumingAsync(channel, consumerTag).ConfigureAwait(false);
        }
        finally
        {
            await RabbitMqFixture.DeleteQueueAsync(this.connection, queueName).ConfigureAwait(false);
        }
    }

    private async Task<OragonConsumerHelper> StartOragonConsumer<TMessage>(
        string queueName, CountdownEvent countdown)
    {
        return this.HandlerType switch
        {
            "NoOp" => await OragonConsumerHelper.StartConsumingNoOpAsync<TMessage>(
                this.connection, queueName, 1, 1, countdown).ConfigureAwait(false),
            "CpuBound" => await OragonConsumerHelper.StartConsumingCpuBoundAsync<TMessage>(
                this.connection, queueName, 1, 1, countdown).ConfigureAwait(false),
            "IoBound" => await OragonConsumerHelper.StartConsumingIoBoundAsync<TMessage>(
                this.connection, queueName, 1, 1, countdown).ConfigureAwait(false),
            _ => throw new ArgumentException($"Unknown HandlerType: {this.HandlerType}")
        };
    }

    [Benchmark(Description = "Oragon Latency")]
    public async Task Oragon_SingleMessage()
    {
        string queueName = RabbitMqFixture.GenerateQueueName();
        using var countdown = new CountdownEvent(1);

        try
        {
            _ = await this.publishChannel.QueueDeclareAsync(queueName, durable: false, exclusive: false, autoDelete: false).ConfigureAwait(false);

            await using var helper = (this.MessageSize switch
            {
                "Small" => await this.StartOragonConsumer<SmallMessage>(queueName, countdown).ConfigureAwait(false),
                "Medium" => await this.StartOragonConsumer<MediumMessage>(queueName, countdown).ConfigureAwait(false),
                "Large" => await this.StartOragonConsumer<LargeMessage>(queueName, countdown).ConfigureAwait(false),
                _ => throw new ArgumentException()
            }).ConfigureAwait(false);

            ReadOnlyMemory<byte> body = MessagePayloads.GetBytesForSize(this.MessageSize);
            await this.publishChannel.BasicPublishAsync(string.Empty, queueName, false, body).ConfigureAwait(false);

            _ = countdown.Wait(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await RabbitMqFixture.DeleteQueueAsync(this.connection, queueName).ConfigureAwait(false);
        }
    }
}
