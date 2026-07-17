---
title: Retry and DLQ
nextjs:
  metadata:
    title: Retry and DLQ
    description: Use delivery count, retry policies, and dead-letter queues.
---

Retry policy should be explicit and tied to broker topology. {% .lead %}

---

## Quorum delivery count

RabbitMQ quorum queues expose `x-delivery-count`. Bind it with `attempts` or read it through `AmqpHeaders`.

```csharp
app.MapQueue("orders", (OrderCreated message, IAmqpContext context) =>
{
    return AmqpRetryPolicy.ByDeliveryCount(5).GetResult(context);
});
```

When using a handler parameter only, return the result manually:

```csharp
app.MapQueue("orders", (OrderCreated message, long? attempts) =>
    attempts.GetValueOrDefault() + 1 < 5
        ? AmqpResults.Nack(true)
        : AmqpResults.Nack(false));
```

## DLQ topology

```csharp
QueueArguments
    .Quorum()
    .WithDeadLetter("orders.dlx", "orders.failed");
```

Classic queues do not provide `x-delivery-count`; use DLX policies, delayed exchanges, or application headers when classic queue retry counting is required.
