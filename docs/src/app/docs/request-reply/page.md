---
title: Request/reply
nextjs:
  metadata:
    title: Request/reply
    description: Reply to messages with ReplyTo and correlation identifiers.
---

Use `AmqpResults.Reply(...)` when the incoming message has `ReplyTo`. {% .lead %}

---

```csharp
app.MapQueue("calculator.rpc", (SumRequest request) =>
{
    return AmqpResults.Reply(new SumResponse(request.A + request.B));
});
```

The reply result publishes to the `ReplyTo` queue with a persistent message. Its correlation id is the original message id when present, otherwise the original correlation id.

Return `AmqpResults.ReplyAndAck(...)` or `AmqpResults.Compose(AmqpResults.Reply(...), AmqpResults.Ack())` when the original delivery must be acknowledged after the reply is published.
