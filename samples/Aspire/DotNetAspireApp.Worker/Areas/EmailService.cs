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
    static readonly Meter s_meter = new Meter($"{nameof(EmailService)}.{nameof(DoSomethingAsync)}");
    static readonly Counter<int> s_metricCounter = s_meter.CreateCounter<int>($"{nameof(EmailService)}.{nameof(DoSomethingAsync)}.count".ToLowerInvariant());
    static readonly Histogram<int> s_metricHits = s_meter.CreateHistogram<int>($"{nameof(EmailService)}.{nameof(DoSomethingAsync)}.hits".ToLowerInvariant());

    private static readonly ActivitySource s_activitySource = new ActivitySource("DotNetAspireApp.Worker");

    public async Task DoSomethingAsync(DoSomethingCommand command)
    {
        var tags = new TagList([
            new KeyValuePair<string, object?>("service", nameof(EmailService)),
            new KeyValuePair<string, object?>("service.method", nameof(DoSomethingAsync)),
            new KeyValuePair<string, object?>("service.arg.text", command.Text),
            new KeyValuePair<string, object?>("service.arg.seq", command.Seq),
        ]);
        s_metricHits.Record(1, tags);
        s_metricCounter.Add(1, tags);


        using var activity = s_activitySource.StartActivity("DotNetAspireApp.Worker", ActivityKind.Consumer);
        _ = activity?.AddTag("service", nameof(EmailService));
        _ = activity?.AddTag("service.method", nameof(DoSomethingAsync));
        _ = activity?.AddTag("service.arg.text", command.Text);
        _ = activity?.AddTag("service.arg.seq", command.Seq);

        Console.WriteLine($"Consumer Recebeu | {command.Text}");


        string logText = $"{command.Text} ({command.Seq} of {command.Max})";

        Console.WriteLine($"Begin | {logText}");

        await Task.Delay(TimeSpan.FromMilliseconds(300)).ConfigureAwait(false);

        Console.WriteLine($"End | {logText}");
    }
}
