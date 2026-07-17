---
title: RabbitMQ management
nextjs:
  metadata:
    title: RabbitMQ management
    description: Inspect broker state while using Oragon.RabbitMQ.
---

The RabbitMQ management UI remains the source of truth for broker topology and queue state. {% .lead %}

---

Use it to inspect:

- queue type and arguments;
- consumer count and consumer tags;
- ready and unacked messages;
- dead-letter queues;
- redeliveries and quorum delivery count behavior;
- channel and connection state.

`QueueArgumentDiagnostics` can compare expected arguments with a snapshot you obtain from broker APIs, but it does not mutate topology.
