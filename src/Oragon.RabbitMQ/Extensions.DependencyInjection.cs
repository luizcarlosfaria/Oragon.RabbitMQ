// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions()
        {
            MaxRetryAttempts = 5,
            DelayGenerator = static args =>
            {
                TimeSpan delay = args.AttemptNumber switch
                {
                    <= 5 => TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber)),
                    _ => TimeSpan.FromMinutes(3)
                };
                return new ValueTask<TimeSpan?>(delay);
            }
        })
        .AddTimeout(TimeSpan.FromSeconds(10)) // Add 10 seconds timeout
        .Build();

        await pipeline.ExecuteAsync(async (cancellationToken) =>
        {
            //do not dispose connection
            IConnection connection = string.IsNullOrWhiteSpace(keyedServiceKey)
                ? serviceProvider.GetRequiredService<IConnection>()
                : serviceProvider.GetRequiredKeyedService<IConnection>(keyedServiceKey);

            if (connection.IsOpen)
            {
                return;
            }
            await connection.CloseAsync(cancellationToken).ConfigureAwait(false);

            throw new InvalidOperationException("Connection is not open");
        }).ConfigureAwait(false);

    }

}
