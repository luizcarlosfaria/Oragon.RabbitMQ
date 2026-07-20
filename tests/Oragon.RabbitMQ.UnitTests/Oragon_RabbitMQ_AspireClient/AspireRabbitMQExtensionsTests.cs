using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Oragon.RabbitMQ.AspireClient;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_AspireClient;

public class AspireRabbitMQExtensionsTests
{
    [Fact]
    public void AddRabbitMQClient_ShouldRegisterConnectionFactoryUsingConnectionString()
    {
        // Arrange
        HostApplicationBuilder builder = CreateBuilder([
            new KeyValuePair<string, string>("ConnectionStrings:orders", "amqp://guest:guest@localhost:5672/")
        ]);

        // Act
        builder.AddRabbitMQClient("orders");
        using ServiceProvider serviceProvider = builder.Services.BuildServiceProvider();
        IConnectionFactory connectionFactory = serviceProvider.GetRequiredService<IConnectionFactory>();

        // Assert
        ConnectionFactory factory = Assert.IsType<ConnectionFactory>(connectionFactory);
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
        HostApplicationBuilder builder = CreateBuilder([
            new KeyValuePair<string, string>("Aspire:RabbitMQ:Client:ConnectionFactory:HostName", "config-host")
        ]);

        // Act
        builder.AddRabbitMQClient(
            "orders",
            configureSettings: settings => settings.ConnectionString = "amqp://guest:guest@custom-host:5672/",
            configureConnectionFactory: factory => factory.ClientProvidedName = "unit-test-client");

        using ServiceProvider serviceProvider = builder.Services.BuildServiceProvider();
        IConnectionFactory connectionFactory = serviceProvider.GetRequiredService<IConnectionFactory>();

        // Assert
        ConnectionFactory factory = Assert.IsType<ConnectionFactory>(connectionFactory);
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
        HostApplicationBuilder builder = CreateBuilder([
            new KeyValuePair<string, string>("ConnectionStrings:billing", "amqp://guest:guest@billing-host:5672/")
        ]);

        // Act
        builder.AddKeyedRabbitMQClient("billing");
        using ServiceProvider serviceProvider = builder.Services.BuildServiceProvider();
        IConnectionFactory connectionFactory = serviceProvider.GetRequiredKeyedService<IConnectionFactory>("billing");

        // Assert
        ConnectionFactory factory = Assert.IsType<ConnectionFactory>(connectionFactory);
        Assert.Equal("billing-host", factory.HostName);
        Assert.Equal(5672, factory.Port);
        Assert.Equal("guest", factory.UserName);
        Assert.Equal("guest", factory.Password);
        Assert.Equal("/", factory.VirtualHost);
    }

    [Fact]
    public void AddRabbitMQClient_ShouldRegisterDefaultHealthCheck()
    {
        // Arrange
        HostApplicationBuilder builder = CreateBuilder([]);

        // Act
        builder.AddRabbitMQClient("orders", settings => settings.DisableTracing = true);
        using ServiceProvider serviceProvider = builder.Services.BuildServiceProvider();

        // Assert
        HealthCheckRegistration registration = GetHealthCheckRegistration(serviceProvider, "RabbitMQ.Client");
        Assert.Equal(HealthStatus.Unhealthy, registration.FailureStatus);
    }

    [Fact]
    public void AddKeyedRabbitMQClient_ShouldRegisterKeyedHealthCheck()
    {
        // Arrange
        HostApplicationBuilder builder = CreateBuilder([]);

        // Act
        builder.AddKeyedRabbitMQClient("billing", settings => settings.DisableTracing = true);
        using ServiceProvider serviceProvider = builder.Services.BuildServiceProvider();

        // Assert
        HealthCheckRegistration registration = GetHealthCheckRegistration(serviceProvider, "RabbitMQ.Client_billing");
        Assert.Equal(HealthStatus.Unhealthy, registration.FailureStatus);
    }

    [Fact]
    public void AddRabbitMQClient_ShouldNotRegisterHealthCheckWhenDisabled()
    {
        // Arrange
        HostApplicationBuilder builder = CreateBuilder([]);

        // Act
        builder.AddRabbitMQClient("orders", settings =>
        {
            settings.DisableHealthChecks = true;
            settings.DisableTracing = true;
        });

        using ServiceProvider serviceProvider = builder.Services.BuildServiceProvider();
        HealthCheckServiceOptions options = serviceProvider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        // Assert
        Assert.DoesNotContain(options.Registrations, registration => registration.Name == "RabbitMQ.Client");
    }

    [Fact]
    public async Task RabbitMQHealthCheck_ShouldReturnHealthyWhenConnectionAndChannelAreOpen()
    {
        // Arrange
        HostApplicationBuilder builder = CreateBuilder([]);
        builder.AddRabbitMQClient("orders", settings => settings.DisableTracing = true);

        var connectionMock = new Mock<IConnection>();
        var channelMock = new Mock<IChannel>();

        _ = connectionMock.SetupGet(connection => connection.IsOpen).Returns(true);
        _ = connectionMock
            .Setup(connection => connection.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        _ = builder.Services.AddSingleton(connectionMock.Object);

        using ServiceProvider serviceProvider = builder.Services.BuildServiceProvider();
        HealthCheckRegistration registration = GetHealthCheckRegistration(serviceProvider, "RabbitMQ.Client");
        IHealthCheck healthCheck = registration.Factory(serviceProvider);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext
        {
            Registration = registration
        });

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task RabbitMQHealthCheck_ShouldReturnFailureStatusWhenConnectionIsClosed()
    {
        // Arrange
        HostApplicationBuilder builder = CreateBuilder([]);
        builder.AddRabbitMQClient("orders", settings => settings.DisableTracing = true);

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.SetupGet(connection => connection.IsOpen).Returns(false);
        _ = builder.Services.AddSingleton(connectionMock.Object);

        using ServiceProvider serviceProvider = builder.Services.BuildServiceProvider();
        HealthCheckRegistration registration = GetHealthCheckRegistration(serviceProvider, "RabbitMQ.Client");
        IHealthCheck healthCheck = registration.Factory(serviceProvider);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext
        {
            Registration = registration
        });

        // Assert
        Assert.Equal(registration.FailureStatus, result.Status);
        Assert.Equal("RabbitMQ connection is closed.", result.Description);
        connectionMock.Verify(connection => connection.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RabbitMQHealthCheck_ShouldReturnFailureStatusWhenChannelCreationFails()
    {
        // Arrange
        HostApplicationBuilder builder = CreateBuilder([]);
        builder.AddRabbitMQClient("orders", settings => settings.DisableTracing = true);

        var expectedException = new InvalidOperationException("channel failed");
        var connectionMock = new Mock<IConnection>();

        _ = connectionMock.SetupGet(connection => connection.IsOpen).Returns(true);
        _ = connectionMock
            .Setup(connection => connection.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        _ = builder.Services.AddSingleton(connectionMock.Object);

        using ServiceProvider serviceProvider = builder.Services.BuildServiceProvider();
        HealthCheckRegistration registration = GetHealthCheckRegistration(serviceProvider, "RabbitMQ.Client");
        IHealthCheck healthCheck = registration.Factory(serviceProvider);

        // Act
        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext
        {
            Registration = registration
        });

        // Assert
        Assert.Equal(registration.FailureStatus, result.Status);
        Assert.Same(expectedException, result.Exception);
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

    private static HealthCheckRegistration GetHealthCheckRegistration(IServiceProvider serviceProvider, string name)
    {
        HealthCheckServiceOptions options = serviceProvider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        return Assert.Single(options.Registrations, registration => registration.Name == name);
    }
}
