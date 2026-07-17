---
title: Forwarding and fan-out
nextjs:
  metadata:
    title: Forwarding and fan-out
    description: Forward messages to other exchanges or queues.
---

Forwarding publishes one or more new messages from a handler. {% .lead %}

---

```csharp
app.MapQueue("orders", (OrderCreated message) =>
{
    return AmqpResults.ForwardAndAck(
        exchange: "order-events",
        routingKey: "order.created",
        mandatory: true,
        message);
});
```

`Forward` uses a dedicated publish channel with confirmations enabled. `ForwardAndAck` composes publish and ACK so publish failure prevents the original ack.

Use a custom properties action to set fields such as `ReplyTo`, priority, expiration, or application headers.
