using OpenTelemetry.Trace;

namespace DotNetAspire.Architecture.Messaging;
public static class MessagingTelemetryNames
{
    private static List<string> names = [
        "gaGO.io/RabbitMQ/AsyncQueueConsumer",
        "gaGO.io/RabbitMQ/AsyncRpcConsumer",
        "gaGO.io/RabbitMQ/MessagePublisher"
        ];

    public static string GetName(string name)
    {
        string fullName = $"gaGO.io/RabbitMQ/{name}";
        return !names.Contains(fullName)
            ? throw new InvalidOperationException($"Name '{name}' is not registred ")
            : fullName;
    }

    public static TracerProviderBuilder AddRabbitMQInstrumentation(this TracerProviderBuilder tracerProviderBuilder)
    {
        foreach (string name in names)
        {
            tracerProviderBuilder.AddSource(name);
        }
        return tracerProviderBuilder;
    }
}
