using BenchmarkDotNet.Attributes;
using Oragon.RabbitMQ.Benchmarks.Infrastructure;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[GcServer(true)]
public class ThroughputBenchmark
{
    [Params(1000, 5000)]
    public int MessageCount;

    [Params("Small", "Medium", "Large")]
    public string MessageSize;

    [Params(1, 50, 100)]
    public ushort PrefetchCount;

    private IConnection connection;
    private string queueName;

    [GlobalSetup]
    public void GlobalSetup()
    {
        RabbitMqFixture.WarmupAsync().GetAwaiter().GetResult();
        this.connection = RabbitMqFixture.CreateConnectionAsync().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.connection?.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        this.queueName = RabbitMqFixture.GenerateQueueName();
        ReadOnlyMemory<byte> body = MessagePayloads.GetBytesForSize(this.MessageSize);
        RabbitMqFixture.PreloadQueueAsync(this.connection, this.queueName, this.MessageCount, body).GetAwaiter().GetResult();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        RabbitMqFixture.DeleteQueueAsync(this.connection, this.queueName).GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true, Description = "Native NoOp")]
    public async Task Native_NoOpHandler()
    {
        using var countdown = new CountdownEvent(this.MessageCount);

        var (channel, consumerTag) = this.MessageSize switch
        {
            "Small" => await NativeConsumerHelper.StartConsumingNoOpAsync<SmallMessage>(
                this.connection, this.queueName, this.PrefetchCount, 1, countdown).ConfigureAwait(false),
            "Medium" => await NativeConsumerHelper.StartConsumingNoOpAsync<MediumMessage>(
                this.connection, this.queueName, this.PrefetchCount, 1, countdown).ConfigureAwait(false),
            "Large" => await NativeConsumerHelper.StartConsumingNoOpAsync<LargeMessage>(
                this.connection, this.queueName, this.PrefetchCount, 1, countdown).ConfigureAwait(false),
            _ => throw new ArgumentException()
        };

        _ = countdown.Wait(TimeSpan.FromSeconds(60));
        await NativeConsumerHelper.StopConsumingAsync(channel, consumerTag).ConfigureAwait(false);
    }

    [Benchmark(Description = "Oragon NoOp")]
    public async Task Oragon_NoOpHandler()
    {
        using var countdown = new CountdownEvent(this.MessageCount);

        OragonConsumerHelper helper = this.MessageSize switch
        {
            "Small" => await OragonConsumerHelper.StartConsumingNoOpAsync<SmallMessage>(
                this.connection, this.queueName, this.PrefetchCount, 1, countdown).ConfigureAwait(false),
            "Medium" => await OragonConsumerHelper.StartConsumingNoOpAsync<MediumMessage>(
                this.connection, this.queueName, this.PrefetchCount, 1, countdown).ConfigureAwait(false),
            "Large" => await OragonConsumerHelper.StartConsumingNoOpAsync<LargeMessage>(
                this.connection, this.queueName, this.PrefetchCount, 1, countdown).ConfigureAwait(false),
            _ => throw new ArgumentException()
        };

        _ = countdown.Wait(TimeSpan.FromSeconds(60));
        await helper.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(Description = "Native CpuBound")]
    public async Task Native_CpuBoundHandler()
    {
        using var countdown = new CountdownEvent(this.MessageCount);

        var (channel, consumerTag) = this.MessageSize switch
        {
            "Small" => await NativeConsumerHelper.StartConsumingCpuBoundAsync<SmallMessage>(
                this.connection, this.queueName, this.PrefetchCount, 1, countdown).ConfigureAwait(false),
            "Medium" => await NativeConsumerHelper.StartConsumingCpuBoundAsync<MediumMessage>(
                this.connection, this.queueName, this.PrefetchCount, 1, countdown).ConfigureAwait(false),
            "Large" => await NativeConsumerHelper.StartConsumingCpuBoundAsync<LargeMessage>(
                this.connection, this.queueName, this.PrefetchCount, 1, countdown).ConfigureAwait(false),
            _ => throw new ArgumentException()
        };

        _ = countdown.Wait(TimeSpan.FromSeconds(60));
        await NativeConsumerHelper.StopConsumingAsync(channel, consumerTag).ConfigureAwait(false);
    }

    [Benchmark(Description = "Oragon CpuBound")]
    public async Task Oragon_CpuBoundHandler()
    {
        using var countdown = new CountdownEvent(this.MessageCount);

        OragonConsumerHelper helper = this.MessageSize switch
        {
            "Small" => await OragonConsumerHelper.StartConsumingCpuBoundAsync<SmallMessage>(
                this.connection, this.queueName, this.PrefetchCount, 1, countdown).ConfigureAwait(false),
            "Medium" => await OragonConsumerHelper.StartConsumingCpuBoundAsync<MediumMessage>(
                this.connection, this.queueName, this.PrefetchCount, 1, countdown).ConfigureAwait(false),
            "Large" => await OragonConsumerHelper.StartConsumingCpuBoundAsync<LargeMessage>(
                this.connection, this.queueName, this.PrefetchCount, 1, countdown).ConfigureAwait(false),
            _ => throw new ArgumentException()
        };

        _ = countdown.Wait(TimeSpan.FromSeconds(60));
        await helper.DisposeAsync().ConfigureAwait(false);
    }
}
