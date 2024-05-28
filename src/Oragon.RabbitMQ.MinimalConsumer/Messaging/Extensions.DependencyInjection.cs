using DotNetAspire.Architecture.Messaging.Consumer;
using DotNetAspire.Architecture.Messaging.Serialization;
using Dawn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Diagnostics;

namespace DotNetAspire.Architecture.Messaging;

public static class DependencyInjectionExtensions
{

    /// <summary>
    /// Create a new QueueServiceWorker to bind a queue with an function
    /// </summary>
    /// <typeparam name="TService">Service Type will be used to determine which service will be used to connect on queue</typeparam>
    /// <typeparam name="TRequest">Type of message sent by publisher to consumer. Must be exactly same Type that functionToExecute parameter requests.</typeparam>
    /// <typeparam name="TResponse">Type of returned message sent by consumer to publisher. Must be exactly same Type that functionToExecute returns.</typeparam>
    /// <param name="services">Dependency Injection Service Collection</param>
    /// <param name="queueName">Name of queue</param>
    /// <param name="functionToExecute">Function to execute when any message are consumed from queue</param>
    public static void MapQueueRPC<TService, TRequest, TResponse>(this IServiceCollection services, Action<AsyncQueueConsumerParameters<TService, TRequest, Task<TResponse>>> config)
        where TResponse : class
        where TRequest : class
    {
        Guard.Argument(services).NotNull();
        Guard.Argument(config).NotNull();


        services.AddSingleton<IHostedService>(sp =>
            {
                var parameters = new AsyncQueueConsumerParameters<TService, TRequest, Task<TResponse>>();
                parameters.WithServiceProvider(sp);
                parameters.WithDisplayLoopInConsoleEvery(TimeSpan.FromMinutes(1));
                parameters.WithTestQueueRetryCount(5);
                parameters.WithConnectionFactoryFunc((sp) => sp.GetRequiredService<IConnection>());
                parameters.WithDispatchInRootScope();                
                parameters.WithSerializer(sp.GetRequiredService<IAMQPSerializer>());

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
    /// <typeparam name="TRequest">Type of message sent by publisher to consumer. Must be exactly same Type that functionToExecute parameter requests.</typeparam>
    /// <typeparam name="TResponse">Type of returned message sent by consumer to publisher. Must be exactly same Type that functionToExecute returns.</typeparam>
    /// <param name="services">Dependency Injection Service Collection</param>
    /// <param name="queueName">Name of queue</param>
    /// <param name="functionToExecute">Function to execute when any message are consumed from queue</param>
    public static void MapQueue<TService, TRequest>(this IServiceCollection services, Action<AsyncQueueConsumerParameters<TService, TRequest, Task>> config)
        where TRequest : class
    {
        Guard.Argument(services).NotNull();
        Guard.Argument(config).NotNull();


        services.AddSingleton<IHostedService>(sp =>
        {

            var parameters = new AsyncQueueConsumerParameters<TService, TRequest, Task>();
            parameters.WithServiceProvider(sp);
            parameters.WithDisplayLoopInConsoleEvery(TimeSpan.FromMinutes(1));
            parameters.WithTestQueueRetryCount(5);
            parameters.WithConnectionFactoryFunc((sp) => sp.GetRequiredService<IConnection>());
            parameters.WithDispatchInRootScope();
            parameters.WithSerializer(sp.GetRequiredService<IAMQPSerializer>());

            config(parameters);

            return new AsyncQueueConsumer<TService, TRequest, Task>(
                    sp.GetService<ILogger<AsyncQueueConsumer<TService, TRequest, Task>>>(),
                    parameters,
                    sp
                );
        });
    }
}
