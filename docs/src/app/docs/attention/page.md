---
title: Attention with primitives
nextjs:
  metadata:
    title: Attention with primitives
    description: Build an attention flow without MapAttentionQueue.
---

Attention is a pattern built from primitives, not a dedicated API in this milestone. {% .lead %}

---

## Shape

1. A work message is published to a granular work queue.
2. An attention signal is published to a shared attention queue.
3. A regular `MapQueue` consumer receives the signal.
4. The handler optionally acquires an application-owned gate.
5. The handler uses `IAmqpDynamicQueueConsumer` to consume the target work queue for a bounded window.

## Why no MapAttentionQueue

The library cannot own domain semantics such as channel lifecycle, tenant state, lock keys, migration policy, or business scheduling. It provides `MapQueue`, dynamic queue consumption, gates, retry helpers, publish results, and graceful shutdown so applications can assemble the pattern explicitly.

See `samples/Attention.Primitives/README.md` for a full composition example.
