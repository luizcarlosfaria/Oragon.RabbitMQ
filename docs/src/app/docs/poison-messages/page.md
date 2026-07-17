---
title: Poison messages
nextjs:
  metadata:
    title: Poison messages
    description: Handle messages that cannot be processed successfully.
---

Poison-message handling is a topology and policy decision. {% .lead %}

---

## Common approach

1. Use a DLX/DLQ for terminal failures.
2. Use delivery count or application headers to decide when retry is exhausted.
3. Keep handlers idempotent.
4. Include correlation and message identifiers in logs.

```csharp
app.MapQueue("orders", (OrderCreated message, long? attempts) =>
    attempts.GetValueOrDefault() + 1 < 5
        ? AmqpResults.Nack(true)
        : AmqpResults.Reject(false));
```

Use `RequeueToTail` when immediate broker requeue would starve newer messages.
