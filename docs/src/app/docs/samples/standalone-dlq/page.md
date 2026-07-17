---
title: Standalone with DLQ
nextjs:
  metadata:
    title: Standalone with DLQ
    description: Standalone topology with dead-letter queue.
---

This demo extends the standalone host with DLX/DLQ topology. {% .lead %}

---

Use `QueueArguments.Quorum().WithDeadLetter(...)` in `WithTopology(...)`, then return `Nack(false)` or `Reject(false)` for terminal failures.

The existing `samples/Standalone` project is the base host for this scenario.
