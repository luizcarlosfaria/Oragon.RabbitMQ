using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Oragon.RabbitMQ.Benchmarks.Infrastructure;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[GcServer(true)]
public class ConcurrencyScalingBenchmark
{
    private const int MessageCount = 1000;

    [Params(1, 10, 50, 100)]
    public ushort PrefetchCount;

    [Params(1, 2, 4, 8)]
    public ushort DispatchConcurrency;

    [Params("CpuBound", "IoBound")]
    public string HandlerType;

    private IConnection connection;
    private string queueName;
    private ServiceProvider nativeServiceProvider;
    private IAmqpSerializer nativeSerializer;

    [GlobalSetup]
    public void GlobalSetup()
    {
        RabbitMqFixture.WarmupAsync().GetAwaiter().GetResult();
        this.connection = RabbitMqFixture.CreateConnectionAsync().GetAwaiter().GetResult();

        var services = new ServiceCollection();
        _ = services.AddAmqpSerializer(options: MessagePayloads.JsonOptions);
        this.nativeServiceProvider = services.BuildServiceProvider();
        this.nativeSerializer = this.nativeServiceProvider.GetRequiredService<IAmqpSerializer>();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.nativeServiceProvider?.Dispose();
        this.connection?.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        this.queueName = RabbitMqFixture.GenerateQueueName();
        RabbitMqFixture.PreloadQueueAsync(this.connection, this.queueName, MessageCount, MessagePayloads.SmallBytes)
            .GetAwaiter().GetResult();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        RabbitMqFixture.DeleteQueueAsync(this.connection, this.queueName).GetAwaiter().GetResult();
    }

    private Func<SmallMessage, Task> GetHandler() => this.HandlerType switch
    {
        "CpuBound" => _ =>
        {
            int hash = 0;
            for (int i = 0; i < 1000; i++) hash = HashCode.Combine(hash, i);
            return Task.CompletedTask;
        },
        "IoBound" => _ => Task.Delay(5),
        _ => throw new ArgumentException()
    };

    [Benchmark(Baseline = true, Description = "Native Scaling")]
    public async Task Native_Scaling()
    {
        using var countdown = new CountdownEvent(MessageCount);

        var (channel, consumerTag) = await NativeConsumerHelper.StartConsumingAsync(
            this.connection, this.queueName, this.PrefetchCount, this.DispatchConcurrency,
            this.GetHandler(), countdown, this.nativeServiceProvider, this.nativeSerializer).ConfigureAwait(false);

        _ = countdown.Wait(TimeSpan.FromSeconds(120));
        await NativeConsumerHelper.StopConsumingAsync(channel, consumerTag).ConfigureAwait(false);
    }

    [Benchmark(Description = "Oragon Scaling")]
    public async Task Oragon_Scaling()
    {
        using var countdown = new CountdownEvent(MessageCount);

        await using var helper = (this.HandlerType switch
        {
            "CpuBound" => await OragonConsumerHelper.StartConsumingCpuBoundAsync<SmallMessage>(
                this.connection, this.queueName, this.PrefetchCount, this.DispatchConcurrency, countdown).ConfigureAwait(false),
            "IoBound" => await OragonConsumerHelper.StartConsumingIoBoundAsync<SmallMessage>(
                this.connection, this.queueName, this.PrefetchCount, this.DispatchConcurrency, countdown).ConfigureAwait(false),
            _ => throw new ArgumentException()
        }).ConfigureAwait(false);

        _ = countdown.Wait(TimeSpan.FromSeconds(120));
    }
}
