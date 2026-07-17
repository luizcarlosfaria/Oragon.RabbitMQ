---
title: Standalone hosting
nextjs:
  metadata:
    title: Standalone hosting
    description: Run Oragon.RabbitMQ in a Generic Host or worker service.
---

Use standalone hosting when the process is a worker, console app, or service without .NET Aspire. {% .lead %}

---

## Minimal host

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oragon.RabbitMQ;
using RabbitMQ.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRabbitMQConsumer();
builder.Services.AddSystemTextJsonAmqpSerializer();
builder.Services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
{
    Uri = new Uri("amqp://guest:guest@localhost:5672"),
});
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IConnectionFactory>()
        .CreateConnectionAsync()
        .GetAwaiter()
        .GetResult());

using IHost app = builder.Build();

app.MapQueue("orders", (OrderCreated message) => Task.CompletedTask)
    .WithTopology((channel, cancellationToken) =>
        channel.QueueDeclareAsync("orders", durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken));

await app.RunAsync();
```

## When to use

- services deployed as containers or Windows/Linux services;
- local demos with `docker compose`;
- workers that need explicit control of connection registration.

The standalone sample in `samples/Standalone` shows this style with HTTP endpoints and RabbitMQ consumers in the same host.
