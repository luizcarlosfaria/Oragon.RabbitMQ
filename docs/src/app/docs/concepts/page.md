---
title: Concepts
nextjs:
  metadata:
    title: Concepts
    description: Design decisions behind Oragon.RabbitMQ.
---

Oragon.RabbitMQ is opinionated: it tries to make RabbitMQ consumers predictable, explicit, and close to the Minimal APIs mental model. {% .lead %}

---

## Application logic decoupled from infrastructure

The handler should look like application code, not transport code. The library resolves DI scope, deserialization, AMQP context, and message acknowledgment so the domain service stays simple and testable.

```csharp
app.MapQueue("orders", ([FromServices] OrderService svc, OrderCreated msg) =>
    svc.HandleAsync(msg));
```

The service does not need to know about `BasicDeliverEventArgs`, `IChannel`, or ACK/NACK unless that control is part of the use case.

## Manual acknowledgment by default

Consumption uses manual acknowledgment. The automatic flow applies:

- `BasicAck` when the handler completes successfully.
- `BasicReject(requeue: false)` when the message cannot be deserialized.
- `BasicNack(requeue: false)` when the handler throws.

This favors dead-lettering and operational inspection instead of infinite reprocessing.

## Control when needed

When the domain needs to decide, return `IAmqpResult`.

```csharp
app.MapQueue("queueName", async ([FromServices] ApplicationService svc, ApplicationCommandOrEvent msg) =>
{
    if (await svc.CanProcessAsync(msg))
    {
        await svc.ProcessAsync(msg);
        return AmqpResults.Ack();
    }

    return AmqpResults.Nack(requeue: true);
});
```

## Explicit concurrency

`WithPrefetch` and `WithDispatchConcurrency` are separate on purpose. Prefetch controls broker pressure; dispatch concurrency controls how many handlers run in parallel.

```csharp
app.MapQueue("queueName", handler)
    .WithPrefetch(100)
    .WithDispatchConcurrency(8);
```

Use concurrency greater than 1 only when the handler is safe for parallelism and processing order is not a requirement.

## Pragmatic extensibility

The project lets each consumer replace serializer, connection, channel, topology, and error policies. The goal is not to hide RabbitMQ, but to reduce repetitive and risky code.
