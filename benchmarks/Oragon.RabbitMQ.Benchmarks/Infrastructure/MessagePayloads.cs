using System.Text.Json;

namespace Oragon.RabbitMQ.Benchmarks.Infrastructure;

public class SmallMessage
{
    public int Id { get; set; }
    public string Value { get; set; }
}

public class MediumMessage
{
    public int Id { get; set; }
    public string Description { get; set; }
    public string[] Tags { get; set; }
}

public class LargeMessage
{
    public int Id { get; set; }
    public List<DataItem> Items { get; set; }
}

public class DataItem
{
    public int Index { get; set; }
    public string Name { get; set; }
    public double Amount { get; set; }
    public string Category { get; set; }
}

public static class MessagePayloads
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static SmallMessage CreateSmall() => new()
    {
        Id = 1,
        Value = "benchmark-test-value"
    };

    public static MediumMessage CreateMedium() => new()
    {
        Id = 1,
        Description = new string('A', 900),
        Tags = ["tag1", "tag2", "tag3", "tag4", "tag5"]
    };

    public static LargeMessage CreateLarge()
    {
        var items = new List<DataItem>(50);
        for (int i = 0; i < 50; i++)
        {
            items.Add(new DataItem
            {
                Index = i,
                Name = $"Item-{i:D4}-{new string('X', 80)}",
                Amount = i * 1.23,
                Category = $"Category-{i % 5}"
            });
        }
        return new LargeMessage { Id = 1, Items = items };
    }

    public static byte[] SerializeToBytes<T>(T message) =>
        JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

    public static ReadOnlyMemory<byte> SmallBytes { get; } = SerializeToBytes(CreateSmall());
    public static ReadOnlyMemory<byte> MediumBytes { get; } = SerializeToBytes(CreateMedium());
    public static ReadOnlyMemory<byte> LargeBytes { get; } = SerializeToBytes(CreateLarge());

    public static ReadOnlyMemory<byte> GetBytesForSize(string size) => size switch
    {
        "Small" => SmallBytes,
        "Medium" => MediumBytes,
        "Large" => LargeBytes,
        _ => throw new ArgumentException($"Unknown size: {size}")
    };

    public static Type GetTypeForSize(string size) => size switch
    {
        "Small" => typeof(SmallMessage),
        "Medium" => typeof(MediumMessage),
        "Large" => typeof(LargeMessage),
        _ => throw new ArgumentException($"Unknown size: {size}")
    };
}
