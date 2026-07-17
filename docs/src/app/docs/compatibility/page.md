---
title: Compatibility
nextjs:
  metadata:
    title: Compatibility
    description: Runtime and RabbitMQ compatibility.
---

The repository currently targets `net9.0` and `net10.0` and uses RabbitMQ.Client 7.x. {% .lead %}

---

Applications should verify broker features before relying on them:

- quorum queues for `x-delivery-count`;
- dead-letter exchange configuration for terminal failures;
- single active consumer for broker-enforced single consumer behavior;
- publisher confirmations for reliable publish results.
