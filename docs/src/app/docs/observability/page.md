---
title: Observability
nextjs:
  metadata:
    title: Observability
    description: Observe consumers, failures, and dynamic queue cycles.
---

Operational visibility should include both broker metrics and application-level outcomes. {% .lead %}

---

## Recommended signals

- deliveries received, acked, nacked, and rejected;
- handler duration and exceptions;
- result execution failures;
- dynamic queue stop status;
- initial and remaining ready message count;
- idle timeout and max duration stops;
- publish failures before ACK.

Use RabbitMQ management for queue depth and connection state, and application telemetry for handler and domain outcomes.
