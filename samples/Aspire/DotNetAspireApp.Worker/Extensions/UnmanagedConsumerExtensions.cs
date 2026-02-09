using DotNetAspireApp.Common.Messages.Commands;
using DotNetAspireApp.Worker.Areas;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DotNetAspireApp.Worker.Extensions;

public static class UnmanagedConsumerExtensions
{

    public static async Task ConfigureUnManagedConsumer(this IHost host)
    {

        IConnectionFactory connectionFactory = host.Services.GetRequiredService<IConnectionFactory>();

        IConnection connection = await connectionFactory.CreateConnectionAsync().ConfigureAwait(false);

        var channelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                                                  publisherConfirmationTrackingEnabled: false,
                                                  outstandingPublisherConfirmationsRateLimiter: null,
                                                  consumerDispatchConcurrency: Constants.ConsumerDispatchConcurrency);

        IChannel channel = await connection.CreateChannelAsync(channelOptions).ConfigureAwait(false);

        await channel.BasicQosAsync(0, Constants.Prefetch, false).ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(channel);

        IAmqpSerializer serializer = host.Services.GetRequiredService<IAmqpSerializer>();

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            using IServiceScope scope = host.Services.CreateScope();
            EmailService emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
            DoSomethingCommand message;
            try
            {
                message = serializer.Deserialize<DoSomethingCommand>(ea);
            }
            catch
            {
                await channel.BasicRejectAsync(ea.DeliveryTag, false).ConfigureAwait(false);
                return;
            }
            try
            {
                await emailService.DoSomethingAsync(message).ConfigureAwait(false);
                await channel.BasicAckAsync(ea.DeliveryTag, false).ConfigureAwait(false);
            }
            catch
            {
                await channel.BasicNackAsync(ea.DeliveryTag, false, false).ConfigureAwait(false);
            }
        };

        _ = await channel.BasicConsumeAsync(queue: "events-unmanaged",
                                            autoAck: false,
                                            consumerTag: "events-unmanaged-1",
                                            noLocal: true,
                                            exclusive: false,
                                            arguments: null,
                                            consumer: consumer).ConfigureAwait(false);


    }

}
