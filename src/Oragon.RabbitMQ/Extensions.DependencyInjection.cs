// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Dawn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Serialization;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ;

/// <summary>
/// Extensions for Dependency Injection
/// </summary>
public static class DependencyInjectionExtensions
{

    /// <summary>
    /// Add Oragon RabbitMQ Consumer to Dependency Injection
    /// </summary>
    /// <param name="builder"></param>
    public static void AddRabbitMQConsumer(this IHostApplicationBuilder builder)
    {
        _ = Guard.Argument(builder).NotNull();

        builder.Services.AddRabbitMQConsumer();
    }

    /// <summary>
    /// Add Oragon RabbitMQ Consumer to Dependency Injection
    /// </summary>
    /// <param name="services"></param>
    public static void AddRabbitMQConsumer(this IServiceCollection services)
    {
        _ = Guard.Argument(services).NotNull();

        _ = services.AddSingleton<ConsumerServer>();

        _ = services.AddHostedService<ConsumerServer>(sp => sp.GetRequiredService<ConsumerServer>());
    }

    /// <summary>
    /// Create a new QueueServiceWorker to bind a queue with an function
    /// </summary>
    /// <typeparam name="TService">Service Type will be used to determine which service will be used to connect on queue</typeparam>
    /// <typeparam name="TMessage">Type of message sent by publisher to Consumer. Must be exactly same Type that functionToExecute parameter requests.</typeparam>
    /// <typeparam name="TResponse">Type of returned message sent by Consumer to publisher. Must be exactly same Type that functionToExecute returns.</typeparam>
    /// <param name="host">IHost</param>
    /// <param name="config">Configuration handler</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This is a factory, Dispose will be called by Consumer")]
    public static void MapQueueRPC<TService, TMessage, TResponse>(this IHost host, Action<AsyncQueueConsumerParameters<TService, TMessage, Task<TResponse>>> config)
        where TResponse : class
        where TMessage : class
    {
        _ = Guard.Argument(host).NotNull();

        host.Services.MapQueueRPC(config);
    }

        /// <summary>
        /// Create a new QueueServiceWorker to bind a queue with an function
        /// </summary>
        /// <typeparam name="TService">Service Type will be used to determine which service will be used to connect on queue</typeparam>
        /// <typeparam name="TMessage">Type of message sent by publisher to Consumer. Must be exactly same Type that functionToExecute parameter requests.</typeparam>
        /// <typeparam name="TResponse">Type of returned message sent by Consumer to publisher. Must be exactly same Type that functionToExecute returns.</typeparam>
        /// <param name="serviceProvider">Services </param>
        /// <param name="config">Configuration handler</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This is a factory, Dispose will be called by Consumer")]
    public static void MapQueueRPC<TService, TMessage, TResponse>(this IServiceProvider serviceProvider, Action<AsyncQueueConsumerParameters<TService, TMessage, Task<TResponse>>> config)
        where TResponse : class
        where TMessage : class
    {
        _ = Guard.Argument(serviceProvider).NotNull();
        _ = Guard.Argument(config).NotNull();

        var consumerServer = serviceProvider.GetRequiredService<ConsumerServer>();

        var parameters = new AsyncQueueConsumerParameters<TService, TMessage, Task<TResponse>>();
        _ = parameters.WithServiceProvider(serviceProvider);
        _ = parameters.WithDisplayLoopInConsoleEvery(TimeSpan.FromMinutes(1));
        _ = parameters.WithTestQueueRetryCount(5);
        _ = parameters.WithConnection((sp) => sp.GetRequiredService<IConnection>());
        _ = parameters.WithDispatchInRootScope();
        _ = parameters.WithSerializer(serviceProvider.GetRequiredService<IAMQPSerializer>());

        config(parameters);

        var queueConsumer = new AsyncRpcConsumer<TService, TMessage, TResponse>(
                    serviceProvider.GetService<ILogger<AsyncRpcConsumer<TService, TMessage, TResponse>>>(),
                    parameters,
                    serviceProvider
                );
        consumerServer.AddConsumer(queueConsumer);
    }

    /// <summary>
    /// Create a new QueueServiceWorker to bind a queue with an function
    /// </summary>
    /// <typeparam name="TService">Service Type will be used to determine which service will be used to connect on queue</typeparam>
    /// <typeparam name="TMessage">Type of message sent by publisher to Consumer. Must be exactly same Type that functionToExecute parameter requests.</typeparam>    
    /// <param name="host">IHost</param>
    /// <param name="config">Configuration handler</param>
    public static void MapQueue<TService, TMessage>(this IHost host, Action<AsyncQueueConsumerParameters<TService, TMessage, Task>> config)
        where TMessage : class
    {
        _ = Guard.Argument(host).NotNull();

        host.Services.MapQueue(config);
    }

    /// <summary>
    /// Create a new QueueServiceWorker to bind a queue with an function
    /// </summary>
    /// <typeparam name="TService">Service Type will be used to determine which service will be used to connect on queue</typeparam>
    /// <typeparam name="TMessage">Type of message sent by publisher to Consumer. Must be exactly same Type that functionToExecute parameter requests.</typeparam>    
    /// <param name="serviceProvider">Service Provider</param>
    /// <param name="config">Configuration handler</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This is a factory, Dispose will be called by Consumer")]
    public static void MapQueue<TService, TMessage>(this IServiceProvider serviceProvider, Action<AsyncQueueConsumerParameters<TService, TMessage, Task>> config)
        where TMessage : class
    {
        _ = Guard.Argument(serviceProvider).NotNull();
        _ = Guard.Argument(config).NotNull();

        var consumerServer = serviceProvider.GetRequiredService<ConsumerServer>();

        var parameters = new AsyncQueueConsumerParameters<TService, TMessage, Task>();
        _ = parameters.WithServiceProvider(serviceProvider);
        _ = parameters.WithDisplayLoopInConsoleEvery(TimeSpan.FromMinutes(1));
        _ = parameters.WithTestQueueRetryCount(5);
        _ = parameters.WithConnection((sp) => sp.GetRequiredService<IConnection>());
        _ = parameters.WithDispatchInRootScope();
        _ = parameters.WithSerializer(serviceProvider.GetRequiredService<IAMQPSerializer>());

        config(parameters);

        var queueConsumer = new AsyncQueueConsumer<TService, TMessage, Task>(
                    serviceProvider.GetService<ILogger<AsyncQueueConsumer<TService, TMessage, Task>>>(),
                    parameters,
                    serviceProvider
                );

        consumerServer.AddConsumer(queueConsumer);
    }


    /// <summary>
    /// Wait Connection is available to use
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="keyedServiceKey">Optional if using Keyed Services, used to return a RabbitMQ.Client.IConnection instance</param>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task WaitRabbitMQAsync(this IServiceProvider serviceProvider, string keyedServiceKey = null)
    {
        var pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions()
        {
            MaxRetryAttempts = 5,
            DelayGenerator = static args =>
            {
                var delay = args.AttemptNumber switch
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
            var connection = string.IsNullOrWhiteSpace(keyedServiceKey)
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
