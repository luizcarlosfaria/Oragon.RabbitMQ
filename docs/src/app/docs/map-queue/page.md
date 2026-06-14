---
title: MapQueue
nextjs:
  metadata:
    title: MapQueue
    description: Configure RabbitMQ consumers with a minimal API.
---

`MapQueue` registers a hosted consumer for a RabbitMQ queue and connects each delivery to a .NET delegate. {% .lead %}

---

## Basic shape

```csharp
app.MapQueue("purchase", ([FromServices] IPurchaseService svc, [FromBody] PurchaseRequest request) =>
    svc.Purchase(request));
```

The queue name is required, and the delegate defines the handler contract. The dispatcher resolves parameters by type, attributes, and conventions.

## Fluent descriptor

`MapQueue` returns a `ConsumerDescriptor`. It lets you tune the consumer without leaving the minimal API style.

```csharp
app.MapQueue("purchase", ([FromServices] IPurchaseService svc, PurchaseRequest request) =>
    svc.Purchase(request))
    .WithDispatchConcurrency(1)
    .WithPrefetch(1)
    .WithConsumerTag("purchase-worker")
    .WithExclusive(false);
```

| Method | Purpose |
| --- | --- |
| `WithPrefetch(ushort)` | Number of messages prefetched from the broker |
| `WithDispatchConcurrency(ushort)` | Number of handlers running in parallel |
| `WithConsumerTag(string)` | Custom consumer tag |
| `WithExclusive(bool)` | Exclusive queue consumption |
| `WithTopology(...)` | Declares topology before consumption starts |
| `WithConnection(...)` | Uses a custom connection |
| `WithSerializer(...)` | Uses a custom serializer |
| `WithChannel(...)` | Creates the channel with custom options |

## Fail fast

When the host starts, Oragon.RabbitMQ validates service bindings and tries to confirm that the queue exists. Configuration errors are treated as startup failures, not silent runtime errors.

## Startup topology

Use `WithTopology` when the application is responsible for declaring queues, exchanges, and bindings.

```csharp
app.MapQueue("purchase", handler)
    .WithTopology(async (channel, cancellationToken) =>
    {
        await channel.QueueDeclareAsync("purchase-dlq", durable: true, exclusive: false, autoDelete: false);
        await channel.QueueDeclareAsync("purchase", durable: true, exclusive: false, autoDelete: false);
    });
```
