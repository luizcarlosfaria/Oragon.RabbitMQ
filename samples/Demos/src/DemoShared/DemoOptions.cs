namespace Oragon.RabbitMQ.Demos;

public sealed record DemoOptions
{
    public string AmqpUri { get; init; } =
        Environment.GetEnvironmentVariable("AMQP_URI")
        ?? "amqp://guest:guest@localhost:5672/";

    public string Prefix { get; init; } =
        Environment.GetEnvironmentVariable("ORAGON_DEMO_PREFIX")
        ?? "oragon.demo";

    public string ResourceName(DemoCase demo, string resource)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        return $"{this.Prefix}.{demo.Id}.{resource}";
    }
}
