using OpenTelemetry.Trace;

namespace Oragon.RabbitMQ;
public static class MessagingTelemetryNames
{
    private static List<string> names = [
        "gaGO.io/RabbitMQ/AsyncQueueConsumer",
        "gaGO.io/RabbitMQ/AsyncRpcConsumer",
        "gaGO.io/RabbitMQ/MessagePublisher"
        ];

    public static string GetName(string name)
    {
        var fullName = $"gaGO.io/RabbitMQ/{name}";
        return !names.Contains(fullName)
            ? throw new InvalidOperationException($"Name '{name}' is not registred ")
            : fullName;
    }

    public static TracerProviderBuilder AddRabbitMQInstrumentation(this TracerProviderBuilder tracerProviderBuilder)
    {
        foreach (var name in names)
        {
            tracerProviderBuilder.AddSource(name);
        }
        return tracerProviderBuilder;
    }
}
