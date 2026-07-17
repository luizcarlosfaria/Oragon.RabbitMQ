---
title: Topology
nextjs:
  metadata:
    title: Topology
    description: Declare and diagnose RabbitMQ topology.
---

Topology is application-owned. Oragon.RabbitMQ provides hooks and helpers, but it does not migrate existing queues automatically. {% .lead %}

---

## Declare before consume

```csharp
app.MapQueue("orders", handler)
    .WithTopology(async (services, channel, cancellationToken) =>
    {
        await channel.ExchangeDeclareAsync("orders", ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            "orders",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: QueueArguments
                .Quorum()
                .WithDeadLetter("orders.dlx", "orders.failed"),
            cancellationToken: cancellationToken);
    });
```

## Queue arguments

```csharp
var arguments = QueueArguments
    .Quorum()
    .WithSingleActiveConsumer()
    .WithDeadLetter("orders.dlx", "orders.failed")
    .WithMaxPriority(10);
```

## Diagnostics

`QueueArgumentDiagnostics.Compare(expected, actual)` reports missing or different arguments. It never deletes, recreates, or migrates queues.

Immutable RabbitMQ arguments such as queue type, quorum, priority, and SAC may require operational migration. The library leaves that decision to the application.
