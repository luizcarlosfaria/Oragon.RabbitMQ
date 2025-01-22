using System.Diagnostics;
using Oragon.RabbitMQ.Serialization;
using Oragon.RabbitMQ;
using DotNetAspireApp.Worker.Areas;
using System.Text.Json;
using Oragon.RabbitMQ.AspireClient;
using DotNetAspireApp.Common.Messages.Commands;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;

public static class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.AddRabbitMQConsumer();

        _ = builder.Services.AddSingleton(sp => new ActivitySource("RabbitMQ.Gago", "1.0.0"));

        builder.AddRabbitMQClient("rabbitmq", null, connectionFactory =>
        {
            connectionFactory.ConsumerDispatchConcurrency = DotNetAspireApp.Worker.Constants.ConsumerDispatchConcurrency;
            connectionFactory.TopologyRecoveryEnabled = false;
            connectionFactory.AutomaticRecoveryEnabled = false;
            connectionFactory.ClientProvidedName = "DotNetAspireApp.Worker";
        });

        _ = builder.Services.AddAmqpSerializer(options: JsonSerializerOptions.Default);

        _ = builder.Services.AddSingleton<EmailService>();

        _ = builder.AddServiceDefaults();

        WebApplication app = builder.Build();

        await app.Services.WaitRabbitMQAsync().ConfigureAwait(false);

        await app.ConfigureRabbitMQAsync().ConfigureAwait(false);

        await app.ConfigureManagedConsumer().ConfigureAwait(false); // Managed Implementation

        await app.ConfigureUnManagedConsumer().ConfigureAwait(false); // UnManaged Implementation (Native Client Only)

        //await app.Services.WaitRabbitMQAsync().ConfigureAwait(false);

        await app.RunAsync().ConfigureAwait(false);
    }


    private static Task ConfigureManagedConsumer(this IHost host)
    {
        _ = host.MapQueue("events-managed", ([FromServices] EmailService svc, [FromBody] DoSomethingCommand cmd)
            => svc.DoSomethingAsync(cmd).ConfigureAwait(false))
        .WithPrefetch(DotNetAspireApp.Worker.Constants.Prefetch)
        .WithDispatchConcurrency(DotNetAspireApp.Worker.Constants.ConsumerDispatchConcurrency);

        return Task.CompletedTask;
    }

    private static async Task ConfigureUnManagedConsumer(this IHost host)
    {

        IConnectionFactory connectionFactory = host.Services.GetRequiredService<IConnectionFactory>();

        IConnection connection = await connectionFactory.CreateConnectionAsync().ConfigureAwait(false);

        var channelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: false,
                                                      publisherConfirmationTrackingEnabled: false,
                                                      outstandingPublisherConfirmationsRateLimiter: null,
                                                      consumerDispatchConcurrency: DotNetAspireApp.Worker.Constants.ConsumerDispatchConcurrency);

        IChannel channel = await connection.CreateChannelAsync(channelOptions).ConfigureAwait(false);

        await channel.BasicQosAsync(0, DotNetAspireApp.Worker.Constants.Prefetch, false).ConfigureAwait(false);

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
