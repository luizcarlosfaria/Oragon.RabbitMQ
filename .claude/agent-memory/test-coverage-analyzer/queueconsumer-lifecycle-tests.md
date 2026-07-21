---
name: queueconsumer-lifecycle-tests
description: Patterns for unit-testing Oragon.RabbitMQ.Consumer.QueueConsumer edge cases (init retry, keyed DI failures, connection ownership probe, outer catch, graceful-shutdown timeouts, ObjectDisposedException swallowing)
metadata:
  type: reference
---

Source: `src/Oragon.RabbitMQ/Consumer/QueueConsumer.cs` (~640 lines).
Happy-path/graceful-shutdown coverage: `tests/Oragon.RabbitMQ.UnitTests/Oragon_RabbitMQ/QueueConsumerExtendedTests.cs`, `FlowTests.cs`, `GracefulShutdownTests.cs`.
Edge-case tests added 2026-07 (release/1.10): `QueueConsumerEdgeCasesTests.cs` (11 facts) + `QueueConsumerEventHandlersTests.cs` (5 facts).

## `[FromServices]` vs Microsoft's `[FromKeyedServices]` — do NOT confuse them
This codebase's own `Oragon.RabbitMQ.Consumer.Dispatch.Attributes.FromServicesAttribute` has a `string serviceKey` ctor overload — usage is `[FromServices("my-key")] IFoo svc`, NOT `[FromKeyedServices("my-key")]` (the Microsoft.Extensions.DependencyInjection attribute). Argument-binder discovery in `ArgumentBinderExtensions.GetAmqpArgumentBinderParameter` only recognizes parameter attributes implementing `IAmqpArgumentBinderParameter`; `FromServicesAttribute` is the only one that supports keys, and Microsoft's `[FromKeyedServices]` does **not** implement that interface so it would silently fall through to `DiscoveryArgumentBinder` (wrong binder, or an exception if the type doesn't match any inference rule). If a task brief says `[FromKeyedServices(...)]`, translate it to `[FromServices("...")]`.

## Overriding `IAmqpContextAccessor` with a throwing mock
`Extensions.DependencyInjection.cs` registers the accessor via `services.TryAddSingleton<IAmqpContextAccessor, AmqpContextAccessor>();` inside `AddRabbitMQConsumer()`. To inject `Mock<IAmqpContextAccessor>` instead, you must call `services.AddSingleton<IAmqpContextAccessor>(mock.Object)` **before** `services.AddRabbitMQConsumer()` — `TryAddSingleton` no-ops once any registration for that service type exists. Useful to force `QueueConsumer.ReceiveAsync`'s outer `catch (Exception ex)` (the one that calls `TryNackMessageAsync`) without needing a broken serializer/handler: `accessorMock.SetupSet(a => a.Current = It.IsAny<IAmqpContext>()).Throws(new InvalidOperationException(...));` — the context is already built (non-null) by the time the setter throws, so `TryNackMessageAsync` receives a real context/channel.

## Raising `IConnection`'s custom `AsyncEventHandler<T>` events directly on the mock
`IConnection.ConnectionShutdownAsync`/`ConnectionBlockedAsync`/`ConnectionUnblockedAsync` are `event AsyncEventHandler<TArgs>` (a `Task`-returning delegate shaped `(object sender, TArgs args)`, not the BCL `EventHandler<T>`). Moq's `Mock<T>.Raise(Action<T> eventExpression, params object[] args)` overload works fine with them regardless of the custom delegate name — confirmed via decompile of `Moq.dll` 4.20.72 (`Mock<T>.Raise` has both an `EventArgs`-typed overload and a generic `params object[]` one; the latter is picked automatically since `ShutdownEventArgs`/`ConnectionBlockedEventArgs` derive from `RabbitMQ.Client.Events.AsyncEventArgs`, not `System.EventArgs`):
```csharp
connectionMock.Raise(c => c.ConnectionShutdownAsync += null, sender, new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED"));
```
This directly invokes `QueueConsumer`'s **private** handler methods (`ConnectionShutdownAsync`, `ConnectionBlockedAsync`, `ConnectionUnblockedAsync`) without going through `AsyncEventingBasicConsumer`'s own base-class plumbing — safe to pass `null` as the event args this way to exercise the handlers' `eventArgs?.X ?? default` null-guards, since it's *our* handler code being invoked, not RabbitMQ.Client's (which is not guaranteed null-safe). Do **not** pass `null` to `AsyncEventingBasicConsumer.HandleChannelShutdownAsync(object, ShutdownEventArgs)` for the same reason — that goes through the base class first.

`ConnectionBlockedEventArgs(string reason, CancellationToken ct = default)` and `AsyncEventArgs.Empty` (a static singleton) / `new AsyncEventArgs(CancellationToken ct = default)` are both in `RabbitMQ.Client.Events`, confirmed via decompile.

## `IConnection.CloseAsync(CancellationToken)` resolves to a 5-arg interface method
`IConnectionExtensions.CloseAsync(this IConnection, CancellationToken ct=default)` (used internally by `QueueConsumer.DetermineConnectionOwnershipAsync` for the probe connection) forwards to `IConnection.CloseAsync(ushort reasonCode, string reasonText, TimeSpan timeout, bool abort, CancellationToken ct)`. When asserting a probe connection was closed, `Verify` against the full 5-arg signature with `It.IsAny<...>()` for each — verifying a simpler overload will silently report zero matching invocations (false negative, not a compile error).

## Forcing the `WaitQueueCreationAsync` Polly retry (queue-not-found-yet) path
```csharp
channelMock.SetupSequence(it => it.QueueDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ThrowsAsync(new OperationInterruptedException())      // OperationInterruptedException() has a public parameterless ctor
    .ReturnsAsync(new QueueDeclareOk("queue", 0, 0));       // ctor(string queueName, uint messageCount, uint consumerCount)
```
The connection mock's `CreateChannelAsync(...)` returning the *same* channel mock for every call (as `BuildTestInfrastructure`-style helpers already do) means both the "real" channel and every retry's probe channel share one `QueueDeclarePassiveAsync` setup sequence — no extra wiring needed. First retry backoff is `Math.Pow(2, 1) = 2s` (Polly `WaitAndRetryAsync(5, retryAttempt => 2^retryAttempt)`), so a test exercising exactly one retry-then-succeed takes ~2 real wall-clock seconds — unavoidable, budget for it.

## `ExecuteResultAsync` double-failure (both primary and configured failure result throw)
`ConsumerDescriptor.WhenResultExecutionFail(...)` is the fluent method (not `ResultForResultExecutionFailure` — that's just the backing property name). Map a handler returning a fake `IAmqpResult` whose `ExecuteAsync` throws, and also configure `WhenResultExecutionFail((ctx, ex) => new ThrowingResult())` — `QueueConsumer.ExecuteResultAsync` catches both failures internally and only logs; nothing propagates out of `HandleBasicDeliverAsync`. Natural-typed lambda syntax needed for a `Task<IAmqpResult>`-returning handler passed to `Delegate`-typed `MapQueue`: `Task<IAmqpResult> (TestMessage msg) => Task.FromResult<IAmqpResult>(new ThrowingResult())`.

## Graceful shutdown timeout branches
- **`BasicCancelAsync` exceeding `DrainTimeout`**: `channelMock.Setup(it => it.BasicCancelAsync(...)).Returns((tag, noWait, ct) => Task.Delay(Timeout.Infinite, ct));` + `descriptor.WithGracefulShutdown(o => o.DrainTimeout = TimeSpan.FromMilliseconds(100))` (called *before* `consumerServer.StartAsync`, since `BuildConsumerAsync` locks the descriptor). `StopAsync`'s `CreateShutdownToken` cancels the linked token after `DrainTimeout`, so the infinite delay throws `OperationCanceledException`, caught by the `when (gracefulOptions != null && shutdownToken.IsCancellationRequested)` filter. `StopAsync` returns cleanly in ~`DrainTimeout`.
- **Drain never completes (`WaitForInFlightMessages`)**: deliver a message whose handler blocks on an ungated `TaskCompletionSource`, set a short `DrainTimeout`, then call `StopAsync` — `WaitInFlightMessagesAsync`'s poll loop (25ms `Task.Delay`) exits via `OperationCanceledException` once the shutdown token's `CancelAfter(DrainTimeout)` fires, returning `drained=false`. `StopAsync` still completes (doesn't hang forever). Release the handler's TCS *after* asserting, then await the original `HandleBasicDeliverAsync` task too, or the test process may tear down mid-flight.

## `ObjectDisposedException` swallowing in `DisposeAsync` / `DetachConnectionHandlers`
- `DisposeAsync`: with `ownsConnection=true` (distinct connections via `.WithConnection(...)` returning different mocks per call), set `runtimeConnectionMock.Setup(it => it.IsOpen).Throws(new ObjectDisposedException(...))` **and** `.Setup(it => it.Dispose()).Throws(...)` — both are in separate `try/catch (ObjectDisposedException)` blocks in the source, so both get individually swallowed. `Verify(it => it.Dispose(), Times.Once)` still passes even though the call threw — Moq records the invocation before executing the configured `Throws` behavior.
- `DetachConnectionHandlers`: `connectionMock.SetupRemove(c => c.ConnectionShutdownAsync -= It.IsAny<AsyncEventHandler<ShutdownEventArgs>>()).Throws(new ObjectDisposedException(...))` — all three `-=` unsubscriptions live in one `try` block, so the exception on the *first* one skips the other two silently; that's fine, the test only needs `StopAsync`/`DisposeAsync` to not propagate.

See also [[dynamic-queue-consumer-tests]] (sibling consumer, similar patterns), [[rabbitmq-client-moq-signatures]] (IChannel/ShutdownEventArgs base signatures), [[build-strictness]] (why an unused `using` here is a suggestion, not a build error).
