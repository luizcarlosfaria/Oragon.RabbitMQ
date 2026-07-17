---
title: Health checks
nextjs:
  metadata:
    title: Health checks
    description: Verify RabbitMQ availability.
---

Health checks are provided by the Aspire client package when using `builder.AddRabbitMQClient(...)`. {% .lead %}

---

For standalone applications, register connection and broker checks according to your hosting platform. A readiness check should confirm that RabbitMQ is reachable before traffic or schedulers depend on consumers.

`WaitRabbitMQAsync()` can be used in setup flows to wait until the broker is available before declaring topology or publishing test messages.
