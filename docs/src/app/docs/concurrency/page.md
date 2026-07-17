---
title: Prefetch and concurrency
nextjs:
  metadata:
    title: Prefetch and concurrency
    description: Tune prefetch and handler concurrency.
---

Prefetch controls broker-side buffering. Dispatch concurrency controls how many deliveries can be processed locally at the same time. {% .lead %}

---

## Conservative default

```csharp
app.MapQueue("orders", handler)
    .WithPrefetch(1)
    .WithDispatchConcurrency(1);
```

This is the safest shape when ordering, idempotency, or external side effects matter.

## Parallel processing

```csharp
app.MapQueue("orders", handler)
    .WithPrefetch(20)
    .WithDispatchConcurrency(4);
```

Use higher values only when handlers are thread-safe and messages can be processed independently.

## Dynamic queues

`DynamicQueueConsumeRequest<T>` has both `PrefetchCount` and `MaxLocalConcurrency`. This allows a temporary consumer to drain a bounded slice without opening a permanent consumer for every queue.
