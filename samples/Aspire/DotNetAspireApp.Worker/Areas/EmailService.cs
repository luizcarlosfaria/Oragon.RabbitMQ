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
using System.Threading.RateLimiting;

namespace DotNetAspireApp.Worker.Areas;

public static class EmailServiceExtensions
{

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
