using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Oragon.RabbitMQ.Benchmarks.Infrastructure;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[GcServer(true)]
public class SerializationOverheadBenchmark
{
    [Params("Small", "Medium", "Large")]
    public string MessageSize;

    private BasicDeliverEventArgs syntheticEventArgs;
    private SystemTextJsonAmqpSerializer oragonSerializer;
    private ReadOnlyMemory<byte> rawBody;
    private Type messageType;

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.rawBody = MessagePayloads.GetBytesForSize(this.MessageSize);
        this.messageType = MessagePayloads.GetTypeForSize(this.MessageSize);

        this.oragonSerializer = new SystemTextJsonAmqpSerializer(MessagePayloads.JsonOptions);

        this.syntheticEventArgs = new BasicDeliverEventArgs(
            consumerTag: "bench",
            deliveryTag: 1,
            redelivered: false,
            exchange: string.Empty,
            routingKey: "bench-queue",
            properties: new BasicProperties(),
            body: this.rawBody);
    }

    [Benchmark(Description = "Oragon (ToArray > GetString > Deserialize)")]
    public object Oragon_Deserialize()
    {
        return this.oragonSerializer.Deserialize(this.syntheticEventArgs, this.messageType);
    }

    [Benchmark(Baseline = true, Description = "Native (Deserialize from Span)")]
    public object Native_Deserialize_Span()
    {
        return JsonSerializer.Deserialize(this.syntheticEventArgs.Body.Span, this.messageType, MessagePayloads.JsonOptions);
    }

    [Benchmark(Description = "Native (Utf8JsonReader)")]
    public object Native_Deserialize_Utf8Reader()
    {
        var reader = new Utf8JsonReader(this.syntheticEventArgs.Body.Span);
        return JsonSerializer.Deserialize(ref reader, this.messageType, MessagePayloads.JsonOptions);
    }
}
