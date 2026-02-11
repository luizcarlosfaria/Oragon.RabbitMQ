using BenchmarkDotNet.Attributes;
using Oragon.RabbitMQ.Benchmarks.Infrastructure;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[GcServer(true)]
public class AllocationBenchmark
{
    [Params(100)]
    public int MessageCount;

    [Params("Small", "Large")]
    public string MessageSize;

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

    [Benchmark(Baseline = true, Description = "Native Allocations")]
    public async Task Native_Allocations()
    {
        using var countdown = new CountdownEvent(this.MessageCount);

        var (channel, consumerTag) = this.MessageSize switch
        {
            "Small" => await NativeConsumerHelper.StartConsumingNoOpAsync<SmallMessage>(
                this.connection, this.queueName, 50, 1, countdown).ConfigureAwait(false),
            "Large" => await NativeConsumerHelper.StartConsumingNoOpAsync<LargeMessage>(
                this.connection, this.queueName, 50, 1, countdown).ConfigureAwait(false),
            _ => throw new ArgumentException()
        };

        _ = countdown.Wait(TimeSpan.FromSeconds(30));
        await NativeConsumerHelper.StopConsumingAsync(channel, consumerTag).ConfigureAwait(false);
    }

    [Benchmark(Description = "Oragon Allocations")]
    public async Task Oragon_Allocations()
    {
        using var countdown = new CountdownEvent(this.MessageCount);

        OragonConsumerHelper helper = this.MessageSize switch
        {
            "Small" => await OragonConsumerHelper.StartConsumingNoOpAsync<SmallMessage>(
                this.connection, this.queueName, 50, 1, countdown).ConfigureAwait(false),
            "Large" => await OragonConsumerHelper.StartConsumingNoOpAsync<LargeMessage>(
                this.connection, this.queueName, 50, 1, countdown).ConfigureAwait(false),
            _ => throw new ArgumentException()
        };

        _ = countdown.Wait(TimeSpan.FromSeconds(30));
        await helper.DisposeAsync().ConfigureAwait(false);
    }
}
