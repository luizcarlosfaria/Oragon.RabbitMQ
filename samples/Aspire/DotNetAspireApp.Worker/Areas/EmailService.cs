// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.


// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using DotNetAspireApp.Common.Messages.Commands;
using Oragon.RabbitMQ;
using Polly.Retry;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.DependencyInjection;
using Oragon.RabbitMQ.Serialization;

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

        using var connection = await connectionFactory.CreateConnectionAsync().ConfigureAwait(true);

        using var channel = await connection.CreateChannelAsync().ConfigureAwait(true);

        await channel.ExchangeDeclareAsync("events", type: ExchangeType.Fanout, durable: true, autoDelete: false).ConfigureAwait(true);

        _ = await channel.QueueDeclareAsync("events-managed", durable: true, exclusive: false, autoDelete: false).ConfigureAwait(true);

        _ = await channel.QueueDeclareAsync("events-unmanaged", durable: true, exclusive: false, autoDelete: false).ConfigureAwait(true);

        await channel.QueueBindAsync("events-managed", "events", string.Empty).ConfigureAwait(true);

        await channel.QueueBindAsync("events-unmanaged", "events", string.Empty).ConfigureAwait(true);
    }


    public static void AddManagedEmailService(this IHost host)
    {
        // Add services to the container.
        host.MapQueue<EmailService, DoSomethingCommand>(config => config
            .WithDispatchInRootScope()
            .WithAdapter((svc, msg) => svc.DoSomethingAsync(msg))
            .WithQueueName("events-managed")
            .WithPrefetchCount(DotNetAspireApp.Worker.Constants.Parallelism * DotNetAspireApp.Worker.Constants.PrefetchFactor)
        );
    }

    public static async Task AddUnmanagedEmailServiceAsync(this IHost host)
    {
        var connectionFactory = host.Services.GetRequiredService<IConnectionFactory>();

        using var connection = await connectionFactory.CreateConnectionAsync().ConfigureAwait(true);

        using var channel = await connection.CreateChannelAsync().ConfigureAwait(true);

        await channel.BasicQosAsync(0, DotNetAspireApp.Worker.Constants.Parallelism * DotNetAspireApp.Worker.Constants.PrefetchFactor, false).ConfigureAwait(true);

        var consumer = new AsyncEventingBasicConsumer(channel);

        var serializer = host.Services.GetRequiredService<IAMQPSerializer>();

        consumer.Received += async (sender, ea) =>
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

        _ = await channel.BasicConsumeAsync(queue: "events-unmanaged", autoAck: false, consumerTag: "events-unmanaged-1", noLocal: false, exclusive: false, arguments: null, consumer: consumer).ConfigureAwait(true);

        var timeToDisplay = 1;

        long loopCount = 0;
        while (true)
        {
            loopCount++;
            await Task.Delay(1000).ConfigureAwait(true);
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
