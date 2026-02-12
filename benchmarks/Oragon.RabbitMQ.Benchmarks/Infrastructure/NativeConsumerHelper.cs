using Microsoft.Extensions.DependencyInjection;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.Benchmarks.Infrastructure;

public static class NativeConsumerHelper
{
    public static async Task<(IChannel Channel, string ConsumerTag)> StartConsumingAsync<TMessage>(
        IConnection connection,
        string queueName,
        ushort prefetchCount,
        ushort dispatchConcurrency,
        Func<TMessage, Task> handler,
        CountdownEvent countdown,
        IServiceProvider serviceProvider,
        IAmqpSerializer serializer)
    {
        IChannel channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false,
                consumerDispatchConcurrency: dispatchConcurrency
            )).ConfigureAwait(false);

        await channel.BasicQosAsync(0, prefetchCount, false).ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, eventArgs) =>
        {
            using IServiceScope scope = serviceProvider.CreateScope();
            TMessage message;
            try
            {
                message = serializer.Deserialize<TMessage>(eventArgs);
            }
            catch
            {
                await channel.BasicRejectAsync(eventArgs.DeliveryTag, false).ConfigureAwait(false);
                return;
            }
            try
            {
                await handler(message).ConfigureAwait(false);
                countdown.Signal();
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false).ConfigureAwait(false);
            }
            catch
            {
                await channel.BasicNackAsync(eventArgs.DeliveryTag, false, false).ConfigureAwait(false);
            }
        };

        string consumerTag = await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumerTag: string.Empty,
            noLocal: true,
            exclusive: false,
            arguments: null,
            consumer: consumer).ConfigureAwait(false);

        return (channel, consumerTag);
    }

    public static async Task<(IChannel Channel, string ConsumerTag)> StartConsumingNoOpAsync<TMessage>(
        IConnection connection,
        string queueName,
        ushort prefetchCount,
        ushort dispatchConcurrency,
        CountdownEvent countdown,
        IServiceProvider serviceProvider,
        IAmqpSerializer serializer)
    {
        return await StartConsumingAsync<TMessage>(
            connection, queueName, prefetchCount, dispatchConcurrency,
            _ => Task.CompletedTask,
            countdown, serviceProvider, serializer).ConfigureAwait(false);
    }

    public static async Task<(IChannel Channel, string ConsumerTag)> StartConsumingCpuBoundAsync<TMessage>(
        IConnection connection,
        string queueName,
        ushort prefetchCount,
        ushort dispatchConcurrency,
        CountdownEvent countdown,
        IServiceProvider serviceProvider,
        IAmqpSerializer serializer)
    {
        return await StartConsumingAsync<TMessage>(
            connection, queueName, prefetchCount, dispatchConcurrency,
            _ =>
            {
                // Simulate light CPU work (hash computation)
                int hash = 0;
                for (int i = 0; i < 1000; i++)
                {
                    hash = HashCode.Combine(hash, i);
                }
                return Task.CompletedTask;
            },
            countdown, serviceProvider, serializer).ConfigureAwait(false);
    }

    public static async Task StopConsumingAsync(IChannel channel, string consumerTag)
    {
        await channel.BasicCancelAsync(consumerTag).ConfigureAwait(false);
        channel.Dispose();
    }
}
