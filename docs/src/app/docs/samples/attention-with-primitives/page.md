---
title: Attention with primitives
nextjs:
  metadata:
    title: Attention with primitives sample
    description: Compose attention with MapQueue and IAmqpDynamicQueueConsumer.
---

This sample is documented in `samples/Attention.Primitives/README.md`. {% .lead %}

---

It demonstrates:

- attention messages consumed by regular `MapQueue`;
- dynamic work queue consumption with independent stop rules;
- application-owned concurrency gates;
- `IServiceProvider` in hooks;
- no `MapAttentionQueue(...)`;
- no Redis dependency in the core library.
