// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.Consumer;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ;

/// <summary>
/// Extensions for Dependency Injection
/// </summary>
public static partial class DependencyInjectionExtensions
{
    private static readonly Action<ILogger, int, Exception> s_logWaitRabbitMQRetry = LoggerMessage.Define<int>(
        LogLevel.Warning,
        new EventId(1, "WaitRabbitMQRetry"),
        "WaitRabbitMQAsync: retrying connection, attempt {AttemptNumber}");

    /// <summary>
    /// Add Oragon RabbitMQ Consumer to Dependency Injection
    /// </summary>
    /// <param name="builder"></param>
    public static void AddRabbitMQConsumer(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddRabbitMQConsumer();
    }

    /// <summary>
    /// Add Oragon RabbitMQ Consumer to Dependency Injection
    /// </summary>
    /// <param name="services"></param>
    public static void AddRabbitMQConsumer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddSingleton<ConsumerServer>();

        _ = services.AddHostedService(sp => sp.GetRequiredService<ConsumerServer>());
    }

    /// <summary>
    /// Create a new QueueServiceWorker to bind a queue with an function
    /// </summary>
    /// <param name="host">IHost</param>
    /// <param name="queueName"></param>
    /// <param name="handler"></param>    
    public static ConsumerDescriptor MapQueue(this IHost host, string queueName, Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(host);

        return host.Services.MapQueue(queueName, handler);
    }

    /// <summary>
    /// Create a new QueueServiceWorker to bind a queue with an function
    /// </summary>
    /// <param name="serviceProvider">Service Provider</param>
    /// <param name="queueName"></param>
    /// <param name="handler"></param>    
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This is a factory, Dispose will be called by Consumer")]
    public static ConsumerDescriptor MapQueue(this IServiceProvider serviceProvider, string queueName, Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(handler);

        var queueConsumerBuilder = new ConsumerDescriptor(serviceProvider, queueName, handler);

        ConsumerServer consumerServer = serviceProvider.GetRequiredService<ConsumerServer>();

        consumerServer.AddConsumerDescriptor(queueConsumerBuilder);

        return queueConsumerBuilder;
    }


    /// <summary>
    /// Wait Connection is available to use
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="keyedServiceKey">Optional if using Keyed Services, used to return a RabbitMQ.Client.IConnection instance</param>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task WaitRabbitMQAsync(this IServiceProvider serviceProvider, string keyedServiceKey = null)
    {
        ILogger logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DependencyInjectionExtensions));

        ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions()
        {
            MaxRetryAttempts = 10,
            UseJitter = true,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromSeconds(5),
            OnRetry = args =>
            {
                s_logWaitRabbitMQRetry(logger, args.AttemptNumber, null);
                return default;
            }
        })
        .AddTimeout(TimeSpan.FromSeconds(30)) // Add 30 seconds timeout
        .Build();



        await pipeline.ExecuteAsync(async (cancellationToken) =>
        {
            //do not dispose connection
            IConnectionFactory connectionFactory = string.IsNullOrWhiteSpace(keyedServiceKey)
                ? serviceProvider.GetRequiredService<IConnectionFactory>()
                : serviceProvider.GetRequiredKeyedService<IConnectionFactory>(keyedServiceKey);

            using IConnection connection = await connectionFactory.CreateConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(true);

            using IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(true);

            if (connection.IsOpen)
            {
                return;
            }

            await channel.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(true);

            await connection.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(true);

            throw new InvalidOperationException("Connection is not open");

        }).ConfigureAwait(true);

    }

}
