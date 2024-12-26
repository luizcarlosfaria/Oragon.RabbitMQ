// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.


// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using DotNetAspireApp.Common.Messages.Commands;
using RabbitMQ.Client;

namespace DotNetAspireApp.Worker.Areas;

public static class EmailServiceExtensions
{

    public static async Task ConfigureRabbitMQAsync(this IHost host)
    {
        IConnectionFactory connectionFactory = host.Services.GetRequiredService<IConnectionFactory>();

        using IConnection connection = await connectionFactory.CreateConnectionAsync().ConfigureAwait(false);

        using IChannel channel = await connection.CreateChannelAsync().ConfigureAwait(false);

        await channel.ExchangeDeclareAsync("events", type: ExchangeType.Fanout, durable: true, autoDelete: false).ConfigureAwait(false);

        _ = await channel.QueueDeclareAsync("events-managed", durable: true, exclusive: false, autoDelete: false).ConfigureAwait(false);

        _ = await channel.QueueDeclareAsync("events-unmanaged", durable: true, exclusive: false, autoDelete: false).ConfigureAwait(false);

        await channel.QueueBindAsync("events-managed", "events", string.Empty).ConfigureAwait(false);

        await channel.QueueBindAsync("events-unmanaged", "events", string.Empty).ConfigureAwait(false);
    }



}

public class EmailService
{
    public Task DoSomethingAsync(DoSomethingCommand command)
    {
        //enviar email

        //Console.WriteLine($"Begin {command.Seq} | {command.Max} | {command.Text}");

        //await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

        //Console.WriteLine($"End   {command.Seq} | {command.Max} | {command.Text}");

        //return Task.CompletedTask;

        return Task.CompletedTask;
    }
}
