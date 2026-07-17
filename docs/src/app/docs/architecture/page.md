---
title: Architecture
nextjs:
  metadata:
    title: Architecture
    description: How Oragon.RabbitMQ maps RabbitMQ deliveries to .NET handlers.
---

Oragon.RabbitMQ is a thin consumer layer over RabbitMQ.Client. It does not hide AMQP; it standardizes hosting, DI scope creation, binding, serialization, and result execution. {% .lead %}

---

## Runtime flow

1. `AddRabbitMQConsumer()` registers `ConsumerServer`.
2. `MapQueue(...)` adds a `ConsumerDescriptor`.
3. Host startup builds and starts `QueueConsumer` instances.
4. Each delivery creates a DI scope and an `IAmqpContext`.
5. The dispatcher binds handler parameters and invokes the handler.
6. The selected `IAmqpResult` executes ACK/NACK/reply/forward behavior.

## Ownership boundaries

The library owns channels it creates. It does not own application domain locks, lifecycle state, topology migrations, Redis clients, or business orchestration.

## Packages

Use `Oragon.RabbitMQ.Abstractions` for contracts that client libraries or shared code need without referencing the core implementation.
