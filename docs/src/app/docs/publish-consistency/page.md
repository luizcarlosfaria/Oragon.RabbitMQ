---
title: Publish consistency
nextjs:
  metadata:
    title: Publish consistency
    description: Publish safely before acknowledging source messages.
---

Publishing during consumption must be ordered carefully: publish first, then ack the source message. {% .lead %}

---

## Results that publish

`Reply`, `Forward`, and `RequeueToTail` create dedicated channels with publisher confirmations enabled. `RequeueToTail` only republishes the current delivery; compose it with `Ack()` when the original delivery should be acknowledged after a successful publish.

## Work plus attention

For workflows that publish a work message and then an attention signal, prefer an outbox when both messages must be coordinated across process crashes.

```csharp
await outbox.AddAsync(new WorkMessage(...));
await outbox.AddAsync(new AttentionRequest(...));
```

The library provides publish results and consumer primitives; it does not implement a business outbox or define attention message contracts.
