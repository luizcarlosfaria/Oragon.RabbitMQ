---
title: Operational pitfalls
nextjs:
  metadata:
    title: Operational pitfalls
    description: Avoid common RabbitMQ consumer mistakes.
---

Most production issues come from hidden ownership assumptions. {% .lead %}

---

## Avoid these traps

- Changing immutable queue arguments without an operational migration plan.
- Setting high prefetch and high concurrency for non-idempotent handlers.
- Using `Nack(true)` as a delay mechanism.
- Publishing a replacement message after acknowledging the source.
- Treating Redis locks or domain lifecycle state as library responsibilities.
- Forgetting that classic queues do not expose `x-delivery-count`.

Use explicit topology helpers, diagnostics, and failure policies to make these decisions visible in code.
