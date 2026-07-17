namespace Oragon.RabbitMQ.Demos;

public sealed record DemoCase(
    string Id,
    string Slug,
    string Phase,
    string Objective,
    bool RequiresRabbitMq,
    string[] Features)
{
    public string Command => $"{this.Id}-{this.Slug}";

    public string ReadmePath => $"samples/Demos/cases/{this.Command}/README.md";
}
