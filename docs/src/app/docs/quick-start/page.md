---
title: Quick start
nextjs:
  metadata:
    title: Quick start
    description: Create the first RabbitMQ consumer with Oragon.RabbitMQ.
---

A minimal consumer registers the infrastructure, registers a serializer, provides a RabbitMQ connection, and maps a queue to a handler. {% .lead %}

---

## Message and service

The handler can receive a typed message and services from the container.

```csharp
public sealed record OrderCreated(string OrderId, decimal Total);

public sealed class OrderService
{
    public Task HandleAsync(OrderCreated message)
    {
        Console.WriteLine($"Order {message.OrderId}: {message.Total}");
        return Task.CompletedTask;
    }
}
```

## Mapping the queue

Register the consumer infrastructure, serializer, connection, handler service, then build the host and use `MapQueue`.

```csharp
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using RabbitMQ.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRabbitMQConsumer();
builder.Services.AddAmqpSerializer(options: JsonSerializerOptions.Web);
builder.Services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
{
    Uri = new Uri("amqp://guest:guest@localhost:5672"),
});
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IConnectionFactory>()
        .CreateConnectionAsync()
        .GetAwaiter()
        .GetResult());
builder.Services.AddSingleton<OrderService>();

var app = builder.Build();

app.MapQueue("orders", ([FromServices] OrderService svc, [FromBody] OrderCreated msg) =>
    svc.HandleAsync(msg))
    .WithTopology((channel, cancellationToken) =>
        channel.QueueDeclareAsync(
            queue: "orders",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken));

await app.RunAsync();
```

When the handler completes successfully and does not return an `IAmqpResult`, Oragon.RabbitMQ acknowledges the message with `Ack`.

## Basic configuration

Use `WithPrefetch` to control how many messages can be in flight on the consumer and `WithDispatchConcurrency` to define how many handlers can run in parallel.

```csharp
app.MapQueue("orders", ([FromServices] OrderService svc, OrderCreated msg) =>
    svc.HandleAsync(msg))
    .WithPrefetch(20)
    .WithDispatchConcurrency(4);
```

{% callout type="warning" title="Concurrency changes ordering" %}
With `WithDispatchConcurrency` greater than 1, processing order is not guaranteed. Use thread-safe and idempotent handlers.
{% /callout %}

## Publish a test message

Use RabbitMQ.Client or any publisher to send JSON to the queue.

```csharp
using IChannel channel = await connection.CreateChannelAsync(
    new CreateChannelOptions(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true));

var properties = new BasicProperties
{
    ContentType = "application/json",
    MessageId = Guid.NewGuid().ToString("D"),
};

byte[] body = JsonSerializer.SerializeToUtf8Bytes(new OrderCreated("A-100", 42.50m));

await channel.BasicPublishAsync(
    exchange: string.Empty,
    routingKey: "orders",
    mandatory: true,
    basicProperties: properties,
    body: body);
```
