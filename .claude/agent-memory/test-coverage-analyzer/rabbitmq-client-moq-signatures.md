---
name: rabbitmq-client-moq-signatures
description: Confirmed method signatures for RabbitMQ.Client 7.2.1 IChannel/AsyncEventingBasicConsumer and Moq 4.20.72 async setup behavior, used to write mocks without guessing
metadata:
  type: reference
---

Confirmed via reflection against `/root/.nuget/packages/rabbitmq.client/7.2.1/lib/net8.0/RabbitMQ.Client.dll` (repo pins 7.2.1 — see [[../release-1-10-primitives-changeset]] context in the main project memory for package versions).

## IChannel (RabbitMQ.Client namespace) — implements IDisposable, IAsyncDisposable
- `QueueDeclarePassiveAsync(string queue, CancellationToken ct=default) : Task<QueueDeclareOk>`
- `BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global, CancellationToken ct=default) : Task`
- `BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string,object> arguments, IAsyncBasicConsumer consumer, CancellationToken ct=default) : Task<string>`
- `BasicCancelAsync(string consumerTag, bool noWait=false, CancellationToken ct=default) : Task`
- `BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue, CancellationToken ct=default) : ValueTask`
- `BasicRejectAsync(ulong deliveryTag, bool requeue, CancellationToken ct=default) : ValueTask`
- `BasicAckAsync(ulong deliveryTag, bool multiple, CancellationToken ct=default) : ValueTask`
- `CloseAsync(ushort replyCode, string replyText, bool abort, CancellationToken ct=default) : Task` (plus `ShutdownEventArgs`-based overloads)

## AsyncEventingBasicConsumer (RabbitMQ.Client.Events, base AsyncDefaultBasicConsumer)
- `HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason) : Task` — fires the `ShutdownAsync` event.
- Events: `ReceivedAsync<BasicDeliverEventArgs>`, `RegisteredAsync<ConsumerEventArgs>`, `ShutdownAsync<ShutdownEventArgs>`, `UnregisteredAsync<ConsumerEventArgs>`.

## ShutdownEventArgs (RabbitMQ.Client.Events namespace, NOT RabbitMQ.Client)
Three ctor overloads, all with `cause`/`cancellationToken` defaulted to `null`/`default`:
- `(ShutdownInitiator initiator, ushort replyCode, string replyText, object cause=null, CancellationToken ct=default)`
- `(ShutdownInitiator initiator, ushort replyCode, string replyText, ushort classId, ushort methodId, object cause=null, CancellationToken ct=default)`
- `(ShutdownInitiator initiator, ushort replyCode, string replyText, Exception exception, CancellationToken ct=default)`
So `new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED")` compiles fine with just 3 args.

## Moq 4.20.72
- `.ThrowsAsync(exception)` works on setups for `Task`, `Task<T>`, `ValueTask`, and `ValueTask<T>` return types (confirmed via `ValueTaskFactory`/`ValueTaskFactory<T>` types present in Moq.dll).
- `.Throws(exception)` (synchronous) also works on `ValueTask`-returning setups — it throws before a return value is ever produced, and gets caught by an enclosing `try/await/catch` exactly like an awaited fault would.
- `SetupSequence(...)` supports mixing `.ReturnsAsync(...)` then `.ThrowsAsync(...)` in the same chain (first call succeeds, second call throws) — useful for "the second QueueDeclarePassiveAsync call fails" scenarios.

## How these were confirmed without running a full build
Used `/usr/bin/dotnet` (the WSL system SDK, 10.0.110) to run a throwaway reflection console app in the scratchpad that does `Assembly.LoadFrom(...)` on the exact DLLs under `~/.nuget/packages/...` and prints constructor/method signatures via `GetConstructors()`/`GetMethods()`. Note: `~/.dotnet/tools/ilspycmd` shim is broken in this environment (points to a `~/.dotnet` install missing the `10.0.0` runtime) — either invoke it via `/usr/bin/dotnet ~/.dotnet/tools/.store/ilspycmd/<ver>/ilspycmd/<ver>/tools/net10.0/any/ilspycmd.dll` or just use plain reflection, which is simpler and doesn't need the type to resolve all dependencies (unlike ilspycmd's decompiler, which chokes on missing `Castle.Core` etc. when loading Moq.dll).
