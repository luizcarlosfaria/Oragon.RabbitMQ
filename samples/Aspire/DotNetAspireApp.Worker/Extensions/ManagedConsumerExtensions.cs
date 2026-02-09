using DotNetAspireApp.Common.Messages.Commands;
using DotNetAspireApp.Worker.Areas;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DotNetAspireApp.Worker.Extensions;

public static class ManagedConsumerExtensions
{
    public static Task ConfigureManagedConsumer(this IHost host)
    {
        _ = host.MapQueue("events-managed", ([FromServices] EmailService svc, [FromBody] DoSomethingCommand cmd)
            => svc.DoSomethingAsync(cmd).ConfigureAwait(false))
        .WithPrefetch(Constants.Prefetch)
        .WithDispatchConcurrency(Constants.ConsumerDispatchConcurrency);

        return Task.CompletedTask;
    }

    
}
