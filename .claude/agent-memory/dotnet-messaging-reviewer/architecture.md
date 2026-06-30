# Architecture invariants

## Context accessor scoping (CRITICAL trap)
- `IAmqpContextAccessor` + `AmqpContextAccessor` (plain `{ get; set; }` Current) are registered **Scoped** in `Extensions.DependencyInjection.cs` (`AddRabbitMQConsumer`).
- `IAmqpDynamicQueueConsumer`/`DynamicQueueConsumer` are also **Scoped**.
- `QueueConsumer.ReceiveAsync` (QueueConsumer.cs ~line 231-239) creates a per-message scope and sets `contextAccessor.Current = context` on THAT scope's instance.
- A Scoped accessor means a distinct instance per scope. Any consumer resolved from a different scope (or root) reads `Current == null`. `DynamicQueueConsumer.ResolveConnectionAsync` reads `this.contextAccessor.Current?.Connection` — only non-null if the dynamic consumer was resolved from the SAME message scope. AmbientContext-style accessors are normally Singleton with an AsyncLocal backing field; this one is neither.

## Publish pattern
- Publish-style results (ForwardResult, ReplyResult, RequeueToTailResult) each create a DEDICATED channel per execution via `connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled:true, publisherConfirmationTrackingEnabled:true), ct)`.
- With tracking enabled, `BasicPublishAsync` awaits the broker confirm before returning (RabbitMQ.Client 7.x). So confirms ARE awaited. Cost = one channel open/close per result invocation (per message). High-throughput hot path concern.
- RequeueToTailResult acks the original AFTER the republish+confirm completes — correct at-least-once ordering. ForwardResult does NOT ack (ack handled by ComposableResult wrapper e.g. ForwardAndAck).

## Acknowledgment defaults
- Manual ack, autoAck:false. Processing failure default = `BasicNack(requeue:false)` (TryNackMessageAsync, QueueConsumer ~311). Nack requeue:false -> DLX if configured else dropped.

## AMQP retry semantics (AmqpRetryPolicy)
- Reads `x-delivery-count` header (quorum-only). Classic queues NEVER set x-delivery-count -> GetDeliveryCount returns null -> treated as 0 -> always Nack(requeue:true) until maxAttempts which is never reached on classic queues = infinite hot-loop.
- Nack(requeue:true) requeues to HEAD of queue, not tail -> poison message hot-loops immediately.
