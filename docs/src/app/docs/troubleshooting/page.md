---
title: Troubleshooting
nextjs:
  metadata:
    title: Troubleshooting
    description: Diagnose common Oragon.RabbitMQ issues.
---

Start by locating the stage that failed: startup, deserialization, handler processing, result execution, or shutdown. {% .lead %}

---

## Startup fails

Check that `IConnection`, `IAmqpSerializer`, handler services, queue names, and topology declarations are registered before the host starts.

## Messages repeat immediately

Avoid using `Nack(true)` as a delay mechanism. Use DLX, delayed exchange, or `RequeueToTail` when the goal is to move a message behind newer work.

## Dynamic consumer never stops

Every dynamic request must have at least one effective stop rule. Check `MaxMessages`, `MaxDuration`, `IdleTimeout`, `StopAfterInitialQueueLength`, or cancellation.

## Shutdown is too abrupt

Enable `WithGracefulShutdown(...)` and set a drain timeout appropriate for handler duration.
