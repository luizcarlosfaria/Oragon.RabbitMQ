---
title: Roadmap
nextjs:
  metadata:
    title: Roadmap
    description: Current documentation and attention roadmap.
---

The current attention milestone focuses on reusable primitives, not a domain-specific `MapAttentionQueue(...)`. {% .lead %}

---

Implemented direction:

- graceful shutdown for `MapQueue`;
- configurable result execution failure policy;
- typed headers and AMQP property bindings;
- `RequeueToTail`;
- publish results with dedicated confirmation channels;
- dynamic queue consumer primitive;
- application-owned gate contracts;
- retry helper by delivery count;
- topology argument helpers and diagnostics;
- docs and samples that explain attention as composition.

Out of scope:

- official Redis package;
- domain lifecycle locks;
- automatic topology migration;
- `MapAttentionQueue(...)`.
