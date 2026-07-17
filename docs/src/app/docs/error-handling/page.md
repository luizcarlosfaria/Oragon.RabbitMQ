---
title: Error handling
nextjs:
  metadata:
    title: Error handling
    description: Configure serialization, processing, and result execution failures.
---

Failures are handled at three different points. Configure each point explicitly when defaults are not enough. {% .lead %}

---

## Serialization failure

If deserialization fails, the default result is `Reject(false)`.

```csharp
.WhenSerializationFail((context, exception) => AmqpResults.Reject(false))
```

## Handler failure

If the handler throws, the default result is `Nack(false)`.

```csharp
.WhenProcessFail((context, exception) => AmqpResults.Nack(false))
```

## Result execution failure

If an `IAmqpResult` fails while ACKing, publishing, replying, or forwarding, the default fallback is `Nack(false)`.

```csharp
.WhenResultExecutionFail((context, exception) => AmqpResults.Nack(false))
```

If the fallback also fails, the consumer attempts a final `BasicNackAsync` when it has enough context.
