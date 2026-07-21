---
name: dynamic-queue-consumer-tests
description: Patterns for unit-testing Oragon.RabbitMQ.Consumer.DynamicQueues.DynamicQueueConsumer with Moq
metadata:
  type: reference
---

Source: `src/Oragon.RabbitMQ/Consumer/DynamicQueues/DynamicQueueConsumer.cs` (~660 lines).
Existing happy-path tests: `tests/Oragon.RabbitMQ.UnitTests/Oragon_RabbitMQ/DynamicQueueConsumerTests.cs`.
Edge-case tests added 2026-07: `tests/Oragon.RabbitMQ.UnitTests/Oragon_RabbitMQ/DynamicQueueConsumerEdgeCasesTests.cs` (20 `[Fact]`s).

## Core test skeleton
- Mock `IChannel` and `IConnection`; `connectionMock.CreateChannelAsync(...)` returns the channel mock (unless `request.ChannelFactory` is used).
- Capture the internal consumer via the `BasicConsumeAsync` mock's `Callback<...>` — the 8th positional arg is `IAsyncBasicConsumer`; cast with `Assert.IsType<AsyncEventingBasicConsumer>(consumer)`.
- Deliver messages with `capturedConsumer.HandleBasicDeliverAsync(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body)`.
- Simulate a broker-initiated cancel/shutdown with `await capturedConsumer.HandleChannelShutdownAsync(sender, new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED"))` — this fires the consumer's `ShutdownAsync` event, which `DynamicQueueConsumer` wires to set `BrokerCanceledConsumer=true` and complete with `Interrupted`.
- `QueueDeclarePassiveAsync` is called **twice** in the normal successful path: once for the initial ready count, once again at the end for `RemainingReadyCount` (`TryGetRemainingReadyCountAsync`). Use `SetupSequence` with two return values (or a `ReturnsAsync` + `ThrowsAsync` pair to make only the *second* call fail — useful for testing `RemainingReadyCount == null`).

## Forcing concurrent-delivery races deterministically
`TryReserveMessageSlot` (the `MaxMessages`/`StopAfterInitialQueueLength` slot check) runs synchronously **before** the handler is invoked, inside the `ReceivedAsync` lambda, right after acquiring the `MaxLocalConcurrency` semaphore. To test "a second/third delivery arrives while the first is still in-flight":
1. Set `request.MaxLocalConcurrency = 2` (or more) — otherwise the 2nd delivery blocks on the semaphore behind the 1st and never reaches the interesting branch.
2. First delivery's handler uses a `TaskCompletionSource` to signal `handlerStarted` then `await releaseHandler.Task` — keeps it in-flight without completing.
3. Await `handlerStarted.Task` before sending the next delivery, to guarantee the first delivery already reserved its slot.
4. Subsequent deliveries that hit the "slot exhausted" or "completion already set" early-exit branches complete **without** ever calling the handler, so you can `await capturedConsumer.HandleBasicDeliverAsync(...)` directly (no need to store the Task) — they resolve fast (just a `BasicNackAsync(requeue:true)` + maybe `BasicCancelAsync`).
5. To specifically hit the "delivery arrives after `completion` is already set" branch (as opposed to the "slot exhausted" branch, which is a *different* line/condition checked earlier in the same method) — send a **third** delivery after the second has already triggered `TryComplete(...)`. The second delivery's early-exit is what actually sets `completion`; a third delivery is what will observe `completion.Task.IsCompleted == true` and short-circuit before even checking the slot.
6. Finally call `releaseHandler.SetResult()` and `await consumeTask.WaitAsync(timeout)` — the drain loop (`WaitInFlightAsync`) waits for the first handler's `finally` block to decrement `inFlight`, so consumeTask won't return until the first delivery's task has also fully unwound. Await that captured `Task` too afterward for safety.

## Testing the handler-cancellation catch specifically
The `ReceivedAsync` handler has its own `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)` (the **caller's** token, not `stopCts`). To exercise it: pass a real `CancellationTokenSource`'s token as the `cancellationToken` argument to `ConsumeAsync`, have the handler `await Task.Delay(long, context.CancellationToken)` (note: `context.CancellationToken` is `stopCts.Token`, linked from the caller's token, so cancelling the caller's token cancels it too), then cancel the external CTS after `handlerStarted` fires. You don't need to worry about racing with the *other* `catch (OperationCanceledException) when (...)` around `completion.Task.WaitAsync(cancellationToken)` in the outer method — both converge on `Status = Interrupted`, and awaiting `consumeTask` naturally waits for the handler's own catch to run (via the in-flight drain), so the line still gets covered.

## Testing `TryNackUnhandledDeliveryAsync` failure (nack-on-fault also throws)
`channelMock.Setup(it => it.BasicNackAsync(...)).Throws(new InvalidOperationException(...))` (synchronous `.Throws`, not `.ThrowsAsync`) works fine even though `BasicNackAsync` returns `ValueTask` — Moq throws before producing a return value, and the surrounding `try/await/catch` in the production code catches it exactly the same as an awaited-then-thrown exception.

## Gotchas
- Once `ConsumeAsync` returns and the successful-drain path disposes `localConcurrency`/`stopCts`, you **cannot** manually re-invoke `HandleBasicDeliverAsync` on the captured consumer to simulate late deliveries — the `ReceivedAsync` handler's first line is `await localConcurrency.WaitAsync(...)`, which throws `ObjectDisposedException` on a disposed `SemaphoreSlim`. Any "late delivery" test must happen *while the overall `ConsumeAsync` call is still in-flight* (i.e., before the semaphore/CTS get disposed).
- `AmqpContextAccessor.Current` is `[AsyncLocal]`-backed; setting it via `new AmqpContextAccessor { Current = ... }` before calling `ConsumeAsync` on the *same* logical async flow works fine (it flows into the awaited call).
- To exercise `ResolveConnectionAsync`'s "connection resolved from `IAmqpContextAccessor.Current.Connection`" branch, mock `IAmqpContext` directly (`Mock<IAmqpContext>().SetupGet(c => c.Connection)...`) rather than constructing a real `AmqpContext` (which requires all its `required` properties).
