---
title: Retry with quorum queues
nextjs:
  metadata:
    title: Retry with quorum queues
    description: Demonstrate retry with x-delivery-count.
---

This demo should use a quorum queue so RabbitMQ provides `x-delivery-count`. {% .lead %}

---

Use `AmqpRetryPolicy.ByDeliveryCount(maxAttempts)` or bind `long? attempts` directly in a handler. Terminal failures should route to a DLQ through topology.
