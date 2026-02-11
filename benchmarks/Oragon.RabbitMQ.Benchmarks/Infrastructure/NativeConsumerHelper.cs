using System.Text.Json;
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
        CountdownEvent countdown)
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
            TMessage message = JsonSerializer.Deserialize<TMessage>(eventArgs.Body.Span, MessagePayloads.JsonOptions);
            await handler(message).ConfigureAwait(false);
            await channel.BasicAckAsync(eventArgs.DeliveryTag, false).ConfigureAwait(false);
            _ = countdown.Signal();
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
        CountdownEvent countdown)
    {
        return await StartConsumingAsync<TMessage>(
            connection, queueName, prefetchCount, dispatchConcurrency,
            _ => Task.CompletedTask,
            countdown).ConfigureAwait(false);
    }

    public static async Task<(IChannel Channel, string ConsumerTag)> StartConsumingCpuBoundAsync<TMessage>(
        IConnection connection,
        string queueName,
        ushort prefetchCount,
        ushort dispatchConcurrency,
        CountdownEvent countdown)
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
            countdown).ConfigureAwait(false);
    }

    public static async Task StopConsumingAsync(IChannel channel, string consumerTag)
    {
        await channel.BasicCancelAsync(consumerTag).ConfigureAwait(false);
        channel.Dispose();
    }
}
