using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oragon.RabbitMQ.AspireClient;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_AspireClient;

public class AspireRabbitMQExtensionsTests
{
    [Fact]
    public void AddRabbitMQClient_ShouldRegisterConnectionFactoryUsingConnectionString()
    {
        // Arrange
        var builder = CreateBuilder([
            new KeyValuePair<string, string>("ConnectionStrings:orders", "amqp://guest:guest@localhost:5672/")
        ]);

        // Act
        builder.AddRabbitMQClient("orders");
        using var serviceProvider = builder.Services.BuildServiceProvider();
        var connectionFactory = serviceProvider.GetRequiredService<IConnectionFactory>();

        // Assert
        var factory = Assert.IsType<ConnectionFactory>(connectionFactory);
        Assert.Equal("localhost", factory.HostName);
        Assert.Equal(5672, factory.Port);
        Assert.Equal("guest", factory.UserName);
        Assert.Equal("guest", factory.Password);
        Assert.Equal("/", factory.VirtualHost);
    }

    [Fact]
    public void AddRabbitMQClient_ShouldApplySettingsAndFactoryConfiguration()
    {
        // Arrange
        var builder = CreateBuilder([
            new KeyValuePair<string, string>("Aspire:RabbitMQ:Client:ConnectionFactory:HostName", "config-host")
        ]);

        // Act
        builder.AddRabbitMQClient(
            "orders",
            configureSettings: settings => settings.ConnectionString = "amqp://guest:guest@custom-host:5672/",
            configureConnectionFactory: factory => factory.ClientProvidedName = "unit-test-client");

        using var serviceProvider = builder.Services.BuildServiceProvider();
        var connectionFactory = serviceProvider.GetRequiredService<IConnectionFactory>();

        // Assert
        var factory = Assert.IsType<ConnectionFactory>(connectionFactory);
        Assert.Equal("custom-host", factory.HostName);
        Assert.Equal(5672, factory.Port);
        Assert.Equal("guest", factory.UserName);
        Assert.Equal("guest", factory.Password);
        Assert.Equal("/", factory.VirtualHost);
        Assert.Equal("unit-test-client", factory.ClientProvidedName);
    }

    [Fact]
    public void AddKeyedRabbitMQClient_ShouldRegisterKeyedConnectionFactory()
    {
        // Arrange
        var builder = CreateBuilder([
            new KeyValuePair<string, string>("ConnectionStrings:billing", "amqp://guest:guest@billing-host:5672/")
        ]);

        // Act
        builder.AddKeyedRabbitMQClient("billing");
        using var serviceProvider = builder.Services.BuildServiceProvider();
        var connectionFactory = serviceProvider.GetRequiredKeyedService<IConnectionFactory>("billing");

        // Assert
        var factory = Assert.IsType<ConnectionFactory>(connectionFactory);
        Assert.Equal("billing-host", factory.HostName);
        Assert.Equal(5672, factory.Port);
        Assert.Equal("guest", factory.UserName);
        Assert.Equal("guest", factory.Password);
        Assert.Equal("/", factory.VirtualHost);
    }

    private static HostApplicationBuilder CreateBuilder(IEnumerable<KeyValuePair<string, string>> configurationValues)
    {
        var settings = new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            Configuration = new ConfigurationManager()
        };

        var builder = new HostApplicationBuilder(settings);
        builder.Configuration.AddInMemoryCollection(configurationValues);
        return builder;
    }
}
