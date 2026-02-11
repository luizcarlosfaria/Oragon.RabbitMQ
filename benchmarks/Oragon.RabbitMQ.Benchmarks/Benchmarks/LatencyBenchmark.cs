using BenchmarkDotNet.Attributes;
using Oragon.RabbitMQ.Benchmarks.Infrastructure;
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

    private Func<SmallMessage, Task> GetHandler() => this.HandlerType switch
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

    [Benchmark(Baseline = true, Description = "Native Latency")]
    public async Task Native_SingleMessage()
    {
        string queueName = RabbitMqFixture.GenerateQueueName();
        using var countdown = new CountdownEvent(1);

        try
        {
            _ = await this.publishChannel.QueueDeclareAsync(queueName, durable: false, exclusive: false, autoDelete: false).ConfigureAwait(false);

            var (channel, consumerTag) = await NativeConsumerHelper.StartConsumingAsync(
                this.connection, queueName, 1, 1, this.GetHandler(), countdown).ConfigureAwait(false);

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
            _ => throw new ArgumentException()
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
