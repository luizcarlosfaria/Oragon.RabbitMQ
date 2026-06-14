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

After registering `OrderService`, use `MapQueue`.

```csharp
builder.Services.AddSingleton<OrderService>();

var app = builder.Build();

app.MapQueue("orders", ([FromServices] OrderService svc, [FromBody] OrderCreated msg) =>
    svc.HandleAsync(msg));

app.Run();
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

## Topology

If the application should declare the queue during startup, use `WithTopology`.

```csharp
app.MapQueue("orders", ([FromServices] OrderService svc, OrderCreated msg) =>
    svc.HandleAsync(msg))
    .WithTopology(async (channel, cancellationToken) =>
    {
        await channel.QueueDeclareAsync(
            queue: "orders",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    });
```
