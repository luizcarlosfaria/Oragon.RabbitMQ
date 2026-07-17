---
title: Minimal consumer
nextjs:
  metadata:
    title: Minimal consumer
    description: Minimal MapQueue consumer sample.
---

This demo starts with one host, one serializer, one connection, one queue, and one handler. {% .lead %}

---

Core steps:

1. Register `AddRabbitMQConsumer()`.
2. Register a serializer.
3. Register `IConnection`.
4. Declare the queue in `WithTopology(...)`.
5. Map the queue to a handler.

Use the quick start page as the source snippet for this demo.
