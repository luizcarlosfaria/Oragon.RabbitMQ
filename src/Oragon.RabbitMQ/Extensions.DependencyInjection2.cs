// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Oragon.RabbitMQ;

/// <summary>
/// Extensions for Dependency Injection
/// </summary>
public  static partial class DependencyInjectionExtensions
{
    private const string ActivitySourceName = "Oragon.RabbitMQ.Client";
    private static readonly ActivitySource s_activitySource = new(ActivitySourceName);
    private const string DefaultConfigSectionName = "Oragon:RabbitMQ:Client";

    /// <summary>
    /// Registers <see cref="IConnection"/> as a singleton in the services provided by the <paramref name="builder"/>.
    /// Enables retries, corresponding health check, logging, and telemetry.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="RabbitMQClientSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <param name="configureConnectionFactory">An optional method that can be used for customizing the <see cref="ConnectionFactory"/>. It's invoked after the options are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:RabbitMQ:Client" section.</remarks>
    public static void AddRabbitMQClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<RabbitMQClientSettings> configureSettings = null,
        Action<ConnectionFactory> configureConnectionFactory = null)
    {
        AddRabbitMQClient(builder, DefaultConfigSectionName, configureSettings, configureConnectionFactory, connectionName, serviceKey: null);
    }

    /// <summary>
    /// Registers <see cref="IConnection"/> as a keyed singleton for the given <paramref name="name"/> in the services provided by the <paramref name="builder"/>.
    /// Enables retries, corresponding health check, logging, and telemetry.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The name of the component, which is used as the <see cref="ServiceDescriptor.ServiceKey"/> of the service and also to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="RabbitMQClientSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <param name="configureConnectionFactory">An optional method that can be used for customizing the <see cref="ConnectionFactory"/>. It's invoked after the options are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:RabbitMQ:Client:{name}" section.</remarks>
    public static void AddKeyedRabbitMQClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<RabbitMQClientSettings> configureSettings = null,
        Action<ConnectionFactory> configureConnectionFactory = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        AddRabbitMQClient(builder, $"{DefaultConfigSectionName}:{name}", configureSettings, configureConnectionFactory, connectionName: name, serviceKey: name);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
    private static void AddRabbitMQClient(
        IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<RabbitMQClientSettings> configureSettings,
        Action<ConnectionFactory> configureConnectionFactory,
        string connectionName,
        object serviceKey)
    {
        ArgumentNullException.ThrowIfNull(builder);

        IConfigurationSection configSection = builder.Configuration.GetSection(configurationSectionName);

        var settings = new RabbitMQClientSettings();
        configSection.Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ConnectionString = connectionString;
        }

        configureSettings?.Invoke(settings);

        IConnectionFactory CreateConnectionFactory(IServiceProvider sp)
        {
            // ensure the log forwarder is initialized
            sp.GetRequiredService<RabbitMQEventSourceLogForwarder>().Start();

            var factory = new ConnectionFactory();

            IConfigurationSection configurationOptionsSection = configSection.GetSection("ConnectionFactory");
            configurationOptionsSection.Bind(factory);

            // the connection string from settings should win over the one from the ConnectionFactory section
            var connectionString = settings.ConnectionString;
            if (!string.IsNullOrEmpty(connectionString))
            {
                factory.Uri = new(connectionString);
            }

            configureConnectionFactory?.Invoke(factory);

            return factory;
        }

        if (serviceKey is null)
        {
            _ = builder.Services.AddSingleton(CreateConnectionFactory);
            _ = builder.Services.AddSingleton(sp => CreateConnection(sp.GetRequiredService<IConnectionFactory>(), settings.MaxConnectRetryCount));
        }
        else
        {
            _ = builder.Services.AddKeyedSingleton(serviceKey, (sp, _) => CreateConnectionFactory(sp));
            _ = builder.Services.AddKeyedSingleton(serviceKey, (sp, key) => CreateConnection(sp.GetRequiredKeyedService<IConnectionFactory>(key), settings.MaxConnectRetryCount));
        }

        _ = builder.Services.AddSingleton<RabbitMQEventSourceLogForwarder>();

        if (!settings.DisableTracing)
        {
            // Note that RabbitMQ.Client v6.6 doesn't have built-in support for tracing. See https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1261

            _ = builder.Services.AddOpenTelemetry()
                .WithTracing(traceBuilder => traceBuilder.AddSource(ActivitySourceName));
        }

        if (!settings.DisableHealthChecks)
        {
            builder.TryAddHealthCheck(new HealthCheckRegistration(
                serviceKey is null ? "RabbitMQ.Client" : $"RabbitMQ.Client_{connectionName}",
                sp =>
                {
                    try
                    {
                        // if the IConnection can't be resolved, make a health check that will fail
                        var options = new RabbitMQHealthCheckOptions
                        {
                            Connection = serviceKey is null ? sp.GetRequiredService<IConnection>() : sp.GetRequiredKeyedService<IConnection>(serviceKey)
                        };
                        return new RabbitMQHealthCheck(options);
                    }
                    catch (Exception ex)
                    {
                        return new FailedHealthCheck(ex);
                    }
                },
                failureStatus: default,
                tags: default));
        }
    }

    private sealed class FailedHealthCheck(Exception ex) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, exception: ex));
        }
    }

    private static IConnection CreateConnection(IConnectionFactory connectionFactory, int retryCount)
    {
        var resiliencePipelineBuilder = new ResiliencePipelineBuilder();
        if (retryCount > 0)
        {
            resiliencePipelineBuilder = resiliencePipelineBuilder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                                    .Handle<SocketException>()
                                    .Handle<BrokerUnreachableException>(),
                UseJitter = true,
                BackoffType = DelayBackoffType.Exponential,
                MaxRetryAttempts = retryCount,
                Delay = TimeSpan.FromSeconds(5),
            });
        }
        ResiliencePipeline resiliencePipeline = resiliencePipelineBuilder.Build();

        using Activity activity = s_activitySource.StartActivity("Rabbitmq connect", ActivityKind.Client);

        return resiliencePipeline.Execute(factory =>
        {
            return factory.CreateConnectionAsync().ConfigureAwait(true).GetAwaiter().GetResult();

        }, connectionFactory);
    }

}


/// <summary>
/// Provides the client configuration settings for connecting to a RabbitMQ message broker.
/// </summary>
public sealed class RabbitMQClientSettings
{
    /// <summary>
    /// Gets or sets the connection string of the RabbitMQ server to connect to.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// <para>Gets or sets the maximum number of connection retry attempts.</para>
    /// <para>Default value is 5, set it to 0 to disable the retry mechanism.</para>
    /// </summary>
    public int MaxConnectRetryCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the RabbitMQ health check is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the OpenTelemetry tracing is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableTracing { get; set; }
}
