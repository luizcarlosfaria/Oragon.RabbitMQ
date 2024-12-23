using DotNet.Testcontainers.Builders;
using Testcontainers.RabbitMq;

namespace Oragon.RabbitMQ.IntegratedTests;
public static class TestExtensions
{
    public static RabbitMqContainer BuildRabbitMQ(this RabbitMqBuilder builder)
    {
        var _rabbitMqContainer = new RabbitMqBuilder()
            .WithDockerEndpoint(Environment.OSVersion.Platform == PlatformID.Unix
                                ? "unix:///var/run/docker.sock"
                                : Environment.OSVersion.Platform == PlatformID.Win32NT
                                ? "tcp://localhost:2375"
                                : throw new NotImplementedException("Plataforma não suportada"))
            .WithImage(Constants.RabbitMQContainerImage)
            .WithExposedPort(15672)
            .WithWaitStrategy(
                Wait
                .ForUnixContainer()
                .UntilPortIsAvailable(15672, it => it
                                                    .WithTimeout(TimeSpan.FromSeconds(120))
                                                    .WithRetries(20)
                                                    .WithInterval(TimeSpan.FromSeconds(3))
                                    )
            )
            .Build();

        return _rabbitMqContainer;
    }

}
