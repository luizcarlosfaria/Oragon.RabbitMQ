using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Benchmarks.Infrastructure;

public sealed class OragonConsumerHelper : IAsyncDisposable
{
    private ServiceProvider serviceProvider;
    private IHostedService hostedService;

    public static async Task<OragonConsumerHelper> StartConsumingNoOpAsync<TMessage>(
        IConnection connection,
        string queueName,
        ushort prefetchCount,
        ushort dispatchConcurrency,
        CountdownEvent countdown)
    {
        return await StartConsumingAsync<TMessage>(
            connection, queueName, prefetchCount, dispatchConcurrency,
            countdown, _ => Task.CompletedTask).ConfigureAwait(false);
    }

    public static async Task<OragonConsumerHelper> StartConsumingCpuBoundAsync<TMessage>(
        IConnection connection,
        string queueName,
        ushort prefetchCount,
        ushort dispatchConcurrency,
        CountdownEvent countdown)
    {
        return await StartConsumingAsync<TMessage>(
            connection, queueName, prefetchCount, dispatchConcurrency,
            countdown, _ =>
            {
                int hash = 0;
                for (int i = 0; i < 1000; i++)
                {
                    hash = HashCode.Combine(hash, i);
                }
                return Task.CompletedTask;
            }).ConfigureAwait(false);
    }

    public static async Task<OragonConsumerHelper> StartConsumingIoBoundAsync<TMessage>(
        IConnection connection,
        string queueName,
        ushort prefetchCount,
        ushort dispatchConcurrency,
        CountdownEvent countdown)
    {
        return await StartConsumingAsync<TMessage>(
            connection, queueName, prefetchCount, dispatchConcurrency,
            countdown, _ => Task.Delay(5)).ConfigureAwait(false);
    }

    public static async Task<OragonConsumerHelper> StartConsumingWithHandlerAsync<TMessage>(
        IConnection connection,
        string queueName,
        ushort prefetchCount,
        ushort dispatchConcurrency,
        CountdownEvent countdown,
        Func<TMessage, Task> handler)
    {
        return await StartConsumingAsync<TMessage>(
            connection, queueName, prefetchCount, dispatchConcurrency,
            countdown, handler).ConfigureAwait(false);
    }

    private static async Task<OragonConsumerHelper> StartConsumingAsync<TMessage>(
        IConnection connection,
        string queueName,
        ushort prefetchCount,
        ushort dispatchConcurrency,
        CountdownEvent countdown,
        Func<TMessage, Task> handler)
    {
        var helper = new OragonConsumerHelper();

        var services = new ServiceCollection();
        services.AddRabbitMQConsumer();
        _ = services.AddAmqpSerializer(options: MessagePayloads.JsonOptions);
        _ = services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        _ = services.AddSingleton(connection);

        helper.serviceProvider = services.BuildServiceProvider();

        // Capture countdown and handler in the closure directly.
        // Do NOT register CountdownEvent in DI — it implements IDisposable
        // and the scoped container would dispose the shared instance after
        // the first message scope ends.
        _ = helper.serviceProvider.MapQueue(queueName,
                (TMessage msg) =>
                {
                    Task result = handler(msg);
                    _ = countdown.Signal();
                    return result;
                })
            .WithPrefetch(prefetchCount)
            .WithDispatchConcurrency(dispatchConcurrency)
            .WithConnection((sp, ct) => Task.FromResult(sp.GetRequiredService<IConnection>()));

        helper.hostedService = helper.serviceProvider.GetRequiredService<IHostedService>();
        await helper.hostedService.StartAsync(CancellationToken.None).ConfigureAwait(false);

        return helper;
    }

    public async Task StopAsync()
    {
        if (this.hostedService != null)
        {
            await this.hostedService.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await this.StopAsync().ConfigureAwait(false);
        if (this.serviceProvider != null)
        {
            await this.serviceProvider.DisposeAsync().ConfigureAwait(false);
        }
    }
}
