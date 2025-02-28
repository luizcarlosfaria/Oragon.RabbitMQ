// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Runtime.Intrinsics.Arm;
using System.Text.Json;
using MyApp.Application.Purchase;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.AspireClient;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using RabbitMQ.Client;

namespace MyApp;

public static partial class Configure
{

    public static void ConfigureAmqp(this WebApplicationBuilder builder)
    {
        builder.AddRabbitMQConsumer();

        builder.AddRabbitMQClient("rabbitmq");

        builder.Services.AddAmqpSerializer(options: JsonSerializerOptions.Web);
    }

    public static void ConfigureAmqp(this WebApplication app)
    {
        app.CreateObjectsAsync().GetAwaiter().GetResult(); ;
        app.ConfigureAmqpEndpoints();
    }

    private static async Task CreateObjectsAsync(this WebApplication app)
    {
        var connectionFactory = app.Services.GetRequiredService<IConnectionFactory>();

        using var connection = await connectionFactory.CreateConnectionAsync().ConfigureAwait(false);

        using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

        _ = await channel.QueueDeclareAsync(queue: "purchase-dlq", durable: true, exclusive: false, autoDelete: false, arguments: null).ConfigureAwait(false);

        IDictionary<string, object?>? arguments = new Dictionary<string, object?>()
        {
            { "x-dead-letter-exchange", "" },
            { "x-dead-letter-routing-key", "purchase-dlq" }
        };

        _ = await channel.QueueDeclareAsync(queue: "purchase", durable: true, exclusive: false, autoDelete: false, arguments: arguments).ConfigureAwait(false);

        await channel.CloseAsync().ConfigureAwait(false);

        await connection.CloseAsync().ConfigureAwait(false);
    }


    private static void ConfigureAmqpEndpoints(this WebApplication app)
    {
        _ = app.MapQueue("purchase", ([FromServices] IPurchaseService svc, [FromBody] PurchaseRequest request) => svc.Purchase(request))
        .WithDispatchConcurrency(1)
        .WithPrefetch(1);
    }




    public class Xpto { public int id { get; set; } }


}


