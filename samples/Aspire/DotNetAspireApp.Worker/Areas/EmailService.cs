// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.


// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using DotNetAspireApp.Common.Messages.Commands;
using Oragon.RabbitMQ;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Oragon.RabbitMQ.Serialization;
using Oragon.RabbitMQ.Consumer.Dispatch;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using Oragon.RabbitMQ.Consumer.Actions;

namespace DotNetAspireApp.Worker.Areas;

public static class EmailServiceExtensions
{
    public static void AddEmailService(this IServiceCollection services)
    {
        // Add services to the container.
        _ = services.AddSingleton<EmailService>();
    }


    public static async Task ConfigureRabbitMQAsync(this IHost host)
    {
        var connectionFactory = host.Services.GetRequiredService<IConnectionFactory>();

        using var connection = await connectionFactory.CreateConnectionAsync().ConfigureAwait(false);

        using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

        await channel.ExchangeDeclareAsync("events", type: ExchangeType.Fanout, durable: true, autoDelete: false).ConfigureAwait(false);

        _ = await channel.QueueDeclareAsync("events-managed", durable: true, exclusive: false, autoDelete: false).ConfigureAwait(false);

        _ = await channel.QueueDeclareAsync("events-unmanaged", durable: true, exclusive: false, autoDelete: false).ConfigureAwait(false);

        await channel.QueueBindAsync("events-managed", "events", string.Empty).ConfigureAwait(false);

        await channel.QueueBindAsync("events-unmanaged", "events", string.Empty).ConfigureAwait(false);
    }


    public static void AddManagedEmailService(this IHost host)
    {
        _ = host.MapQueue("events-managed", ([FromServices] EmailService svc, [FromBody] DoSomethingCommand cmd) => svc.DoSomethingAsync(cmd).ConfigureAwait(false))
            .WithPrefetch(DotNetAspireApp.Worker.Constants.Parallelism * DotNetAspireApp.Worker.Constants.PrefetchFactor);
    }

    public static async Task AddUnmanagedEmailServiceAsync(this IHost host)
    {
        var connectionFactory = host.Services.GetRequiredService<IConnectionFactory>();

        using var connection = await connectionFactory.CreateConnectionAsync().ConfigureAwait(false);

        using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

        await channel.BasicQosAsync(0, DotNetAspireApp.Worker.Constants.Parallelism * DotNetAspireApp.Worker.Constants.PrefetchFactor, false).ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(channel);

        var serializer = host.Services.GetRequiredService<IAMQPSerializer>();

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            using var scope = host.Services.CreateScope();

            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

            var message = serializer.Deserialize<DoSomethingCommand>(ea);

            try
            {
                await emailService.DoSomethingAsync(message).ConfigureAwait(false);

                await channel.BasicAckAsync(ea.DeliveryTag, false).ConfigureAwait(false);

            }
            catch (Exception)
            {

                await channel.BasicNackAsync(ea.DeliveryTag, false, false).ConfigureAwait(false);

                throw;

            }
        };

        _ = await channel.BasicConsumeAsync(queue: "events-unmanaged", autoAck: false, consumerTag: "events-unmanaged-1", noLocal: false, exclusive: false, arguments: null, consumer: consumer).ConfigureAwait(false);

        var timeToDisplay = 1;

        long loopCount = 0;
        while (true)
        {
            loopCount++;
            await Task.Delay(1000).ConfigureAwait(false);
        }
    }
}

public class EmailService
{
    public async Task DoSomethingAsync(DoSomethingCommand command)
    {
        //enviar email

        //Console.WriteLine($"Begin {command.Seq} | {command.Max} | {command.Text}");

        //await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

        //Console.WriteLine($"End   {command.Seq} | {command.Max} | {command.Text}");

        //return Task.CompletedTask;
    }
}
