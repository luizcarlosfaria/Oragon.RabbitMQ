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
    /// Create a new QueueServiceWorker to bind a queue with an function
    /// </summary>
    /// <typeparam name="TService">Service Type will be used to determine which service will be used to connect on queue</typeparam>
    /// <typeparam name="TRequest">Type of message sent by publisher to Consumer. Must be exactly same Type that functionToExecute parameter requests.</typeparam>
    /// <typeparam name="TResponse">Type of returned message sent by Consumer to publisher. Must be exactly same Type that functionToExecute returns.</typeparam>
    /// <param name="services">Services </param>
    /// <param name="config">Configuration handler</param>
    public static void MapQueueRPC<TService, TRequest, TResponse>(this IServiceCollection services, Action<AsyncQueueConsumerParameters<TService, TRequest, Task<TResponse>>> config)
        where TResponse : class
        where TRequest : class
    {
        _ = Guard.Argument(services).NotNull();
        _ = Guard.Argument(config).NotNull();


        _ = services.AddSingleton<IHostedService>(sp =>
            {
                var parameters = new AsyncQueueConsumerParameters<TService, TRequest, Task<TResponse>>();
                _ = parameters.WithServiceProvider(sp);
                _ = parameters.WithDisplayLoopInConsoleEvery(TimeSpan.FromMinutes(1));
                _ = parameters.WithTestQueueRetryCount(5);
                _ = parameters.WithConnection((sp) => sp.GetRequiredService<IConnection>());
                _ = parameters.WithDispatchInRootScope();
                _ = parameters.WithSerializer(sp.GetRequiredService<IAMQPSerializer>());

                config(parameters);

                return new AsyncRpcConsumer<TService, TRequest, TResponse>(
                    sp.GetService<ILogger<AsyncRpcConsumer<TService, TRequest, TResponse>>>(),
                    parameters,
                    sp
                );
            });
    }

    /// <summary>
    /// Create a new QueueServiceWorker to bind a queue with an function
    /// </summary>
    /// <typeparam name="TService">Service Type will be used to determine which service will be used to connect on queue</typeparam>
    /// <typeparam name="TRequest">Type of message sent by publisher to Consumer. Must be exactly same Type that functionToExecute parameter requests.</typeparam>    
    /// <param name="services">Dependency Injection Service Collection</param>
    /// <param name="config">Configuration handler</param>
    public static void MapQueue<TService, TRequest>(this IServiceCollection services, Action<AsyncQueueConsumerParameters<TService, TRequest, Task>> config)
        where TRequest : class
    {
        _ = Guard.Argument(services).NotNull();
        _ = Guard.Argument(config).NotNull();


        _ = services.AddSingleton<IHostedService>(sp =>
        {

            var parameters = new AsyncQueueConsumerParameters<TService, TRequest, Task>();
            _ = parameters.WithServiceProvider(sp);
            _ = parameters.WithDisplayLoopInConsoleEvery(TimeSpan.FromMinutes(1));
            _ = parameters.WithTestQueueRetryCount(5);
            _ = parameters.WithConnection((sp) => sp.GetRequiredService<IConnection>());
            _ = parameters.WithDispatchInRootScope();
            _ = parameters.WithSerializer(sp.GetRequiredService<IAMQPSerializer>());

            config(parameters);

            return new AsyncQueueConsumer<TService, TRequest, Task>(
                    sp.GetService<ILogger<AsyncQueueConsumer<TService, TRequest, Task>>>(),
                    parameters,
                    sp
                );
        });
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
