---
title: Consumer descriptor
nextjs:
  metadata:
    title: Consumer descriptor
    description: Configure queue consumers with ConsumerDescriptor.
---

`MapQueue(...)` returns a `ConsumerDescriptor`. Configure it before the host starts; after a consumer is built, the descriptor is locked. {% .lead %}

---

## Core methods

| Method | Default | Impact |
| --- | --- | --- |
| `WithPrefetch(1)` | `1` | Broker deliveries buffered per consumer |
| `WithDispatchConcurrency(...)` | RabbitMQ default | Local handler dispatch concurrency |
| `WithConsumerTag(...)` | Broker-generated | Stable consumer tag |
| `WithExclusive(false)` | `false` | Exclusive consumer flag |
| `WithConnection(...)` | DI `IConnection` | Selects a connection |
| `WithChannel(...)` | Dedicated consumer channel | Custom channel creation |
| `WithTopology(...)` | none | Declares topology before consumption |
| `WithSerializer(...)` | DI `IAmqpSerializer` | Selects serializer |
| `WithGracefulShutdown(...)` | disabled | Opt-in shutdown drain behavior |

`WithChannel(...)` and `WithTopology(...)` have overloads that receive `IServiceProvider`, so custom logic can resolve keyed services or client-owned providers.

## Failure policies

```csharp
app.MapQueue("orders", handler)
    .WhenSerializationFail((context, exception) => AmqpResults.Reject(false))
    .WhenProcessFail((context, exception) => AmqpResults.Nack(false))
    .WhenResultExecutionFail((context, exception) => AmqpResults.Nack(false));
```

The default result execution failure policy is `Nack(false)`.
