---
title: Flow control
nextjs:
  metadata:
    title: Flow control
    description: Control ACK, NACK, Reject, Reply, and Forward.
---

Handlers can let Oragon.RabbitMQ apply the default behavior or return `IAmqpResult` to explicitly control the message outcome. {% .lead %}

---

## Default behavior

When the handler completes successfully, the message is acknowledged.

| Situation | Result |
| --- | --- |
| Handler completes successfully | `Ack` |
| Deserialization failure | `Reject(requeue: false)` |
| Handler exception | `Nack(requeue: false)` |

The defaults favor dead-lettering instead of infinite requeue loops.

## Explicit results

```csharp
app.MapQueue("orders", async ([FromServices] OrderService svc, OrderCreated msg) =>
{
    if (!svc.CanProcess(msg))
    {
        return AmqpResults.Nack(requeue: true);
    }

    await svc.HandleAsync(msg);
    return AmqpResults.Ack();
});
```

## Available results

| Group | Method | Purpose |
| --- | --- | --- |
| Basic | `AmqpResults.Ack()` | Acknowledge the message |
| Basic | `AmqpResults.Nack(requeue)` | Negative acknowledge |
| Basic | `AmqpResults.Reject(requeue)` | Reject the message |
| RPC | `AmqpResults.Reply<T>(T)` | Reply to the caller |
| RPC | `AmqpResults.ReplyAndAck<T>(T)` | Reply and acknowledge |
| Routing | `AmqpResults.Forward<T>(exchange, routingKey, mandatory, params T[])` | Forward to another exchange |
| Routing | `AmqpResults.ForwardAndAck<T>(exchange, routingKey, mandatory, params T[])` | Forward and acknowledge |
| Composition | `AmqpResults.Compose(params IAmqpResult[])` | Execute multiple results |

## Composition

Use `Compose` when a handler needs to combine actions.

```csharp
return AmqpResults.Compose(
    AmqpResults.Forward("events", "orders.accepted", mandatory: true, accepted),
    AmqpResults.Ack());
```

## Error policies

The descriptor can replace the default behavior for deserialization and processing failures.

```csharp
app.MapQueue("orders", handler)
    .WhenSerializationFail((ctx, ex) => AmqpResults.Reject(requeue: false))
    .WhenProcessFail((ctx, ex) => AmqpResults.Nack(requeue: true));
```
