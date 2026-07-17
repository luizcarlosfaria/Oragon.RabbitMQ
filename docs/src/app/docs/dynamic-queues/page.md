---
title: Dynamic queues
nextjs:
  metadata:
    title: Dynamic queues
    description: Consume runtime-selected queues for a bounded window.
---

`IAmqpDynamicQueueConsumer` consumes a queue selected at runtime until one stop rule is reached. {% .lead %}

---

## Request

```csharp
await dynamicConsumer.ConsumeAsync(
    new DynamicQueueConsumeRequest<WorkMessage>
    {
        QueueName = workQueue,
        MaxMessages = 100,
        MaxDuration = TimeSpan.FromMinutes(2),
        IdleTimeout = TimeSpan.FromSeconds(10),
        StopAfterInitialQueueLength = true,
        PrefetchCount = 10,
        MaxLocalConcurrency = 4,
        OnMessageAsync = async (message, context) =>
        {
            var service = context.ServiceProvider.GetRequiredService<WorkService>();
            await service.HandleAsync(message, context.CancellationToken);
            return AmqpResults.Ack();
        },
    },
    cancellationToken);
```

At least one stop rule must be effective: max messages, max duration, idle timeout, initial queue length, or external cancellation.

## Stop rules

| Rule | Stops when |
| --- | --- |
| `MaxMessages` | The requested number of deliveries reached a terminal result |
| `MaxDuration` | The total consumption window elapsed |
| `IdleTimeout` | No new delivery arrived for the configured interval |
| `StopAfterInitialQueueLength` | The initial ready-message snapshot was processed |
| `CancellationToken` | The caller interrupts the cycle |

Rules are combinable. The first one reached determines the final status. `StopAfterInitialQueueLength` uses the ready count observed by `QueueDeclarePassiveAsync` at the start of the cycle; it does not include unacked messages and it does not reserve future messages published by concurrent producers. If that snapshot is zero, the final status is `Empty`.

`MaxMessages` by itself stops only after that many deliveries are processed. If the queue can become quiet before the limit is reached, combine it with `IdleTimeout`, `MaxDuration`, or `StopAfterInitialQueueLength` so the cycle has a deterministic quiet-path exit.

`IdleTimeout` is not a replacement for a maximum duration. Use it when the cycle should stop after the queue becomes quiet, and combine it with `MaxDuration` when a hard upper bound is required.

The `CancellationToken` exposed through `IAmqpContext` is canceled when the caller cancels the operation or when an internal stop rule ends the cycle. Handlers should pass it to downstream calls so `MaxDuration`, shutdown, and other stop decisions can complete cooperatively.

## Connections

The request can provide `Connection`, `ConnectionFactory`, or `ChannelFactory`. If not provided, the consumer uses the current `IAmqpContext` connection when available, then DI `IConnection`.

This allows attention-style flows where the attention message arrives through one RabbitMQ connection while the dynamic work queue is consumed through another connection selected by the application.

The dynamic consumer closes the channel it creates for the cycle. Connections returned by `Connection`, `ConnectionFactory`, the current AMQP context, or DI remain application-owned and are not disposed by the dynamic consumer.

## Hooks

`BeforeStartAsync` receives the queue name, initial ready count, metadata, and `IServiceProvider`. It can return:

| Decision | Behavior |
| --- | --- |
| `Allow()` | Start consuming |
| `Skip()` | Stop without opening a consumer |
| `Defer(delay)` | Stop and report a deferred cycle |
| `Fail(exception)` | Stop with a faulted result |

`AfterStopAsync` receives the final `DynamicQueueConsumeResult`, metadata, and `IServiceProvider`. Use it for metrics, releasing application-owned leases, or scheduling follow-up work.

## Result

`DynamicQueueConsumeResult` reports the queue name, final status, initial and remaining ready counts, elapsed time, ack/nack/reject counters, and failure information. `QueueMissing` and `Empty` are normal operational statuses, not exceptions that crash the host.

## Related demo

Run `13-dynamic-queue-consumer` from `samples/Demos` for the stop-rule lab.
