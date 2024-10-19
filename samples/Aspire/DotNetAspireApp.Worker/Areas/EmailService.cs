// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.


// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using DotNetAspireApp.Common.Messages.Commands;
using Oragon.RabbitMQ;

namespace DotNetAspireApp.Worker.Areas;

public static class EmailServiceExtensions
{
    public static IServiceCollection AddEmailService(this IServiceCollection services)
    {
        // Add services to the container.
        _ = services.AddSingleton<EmailService>();

        // Add services to the container.
        services.MapQueue<EmailService, DoSomethingCommand>(config => config
            .WithDispatchInRootScope()
            .WithAdapter((svc, msg) => svc.DoSomethingAsync(msg))
            .WithQueueName("events")
            .WithPrefetchCount(DotNetAspireApp.Worker.Constants.Parallelism * DotNetAspireApp.Worker.Constants.PrefetchFactor)
            .WithTopology((sp, channel) => channel.QueueDeclareAsync("events", durable: true, exclusive: false, autoDelete: false))
        );

        return services;
    }

}

public class EmailService
{
    public async Task DoSomethingAsync(DoSomethingCommand command)
    {
       //enviar email

        Console.WriteLine($"Begin");

        await Task.Delay(TimeSpan.FromMilliseconds(300)).ConfigureAwait(false);

        Console.WriteLine($"End");
    }
}
