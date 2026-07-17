# 16 - application-gates

Status: runner implemented; broker smoke test pending.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 16-application-gates
```

Objective: demonstrate application-owned gates and leases.

Initial conditions:

- RabbitMQ is running.
- In-memory gate provider is registered.
- `AMQP_URI` points to the broker, or defaults to
  `amqp://guest:guest@localhost:5672/`.
- Queue `{ORAGON_DEMO_PREFIX}.16.work` can be declared and purged.
- The demo registers `IAmqpConcurrencyGate` in the application container.
- The library provides the extension point and passes `IServiceProvider`; the
  application owns the gate implementation.
- Optional Redis belongs to an application/demo provider, not to the library.
- The demo key format is `attention:{type}:{channelId}` and the concrete key is
  `attention:orders:channel-42`.

Scenarios:

- The runner publishes two work messages.
- `worker-a` starts a dynamic consumption cycle, resolves
  `IAmqpConcurrencyGate` from `DynamicQueueStartContext.Services`, acquires
  `attention:orders:channel-42` and processes one message.
- `worker-b` starts while `worker-a` still holds the lease, resolves the same
  gate through `IServiceProvider`, fails to acquire the same key and returns
  `DynamicQueueStartDecision.Defer(...)`.
- `worker-c` starts after `worker-a` releases the lease, acquires the same key
  and processes the remaining message.
- `AfterStopAsync` receives `IServiceProvider`, records stop status and releases
  any active lease.
- No library code assumes or creates a `channel-lifecycle:{channelId}` lock.

Acceptance:

- README shows key format `attention:{type}:{channelId}`.
- No `channel-lifecycle` lock appears in the library.
- Redis remains optional and demo-owned.
- First worker ends with `MaxMessagesReached`.
- Second worker ends with `Deferred`.
- Third worker ends with `MaxMessagesReached`.
- Exactly two allowed leases and one denied lease are observed.
- Both work messages are ACKed and the work queue ends with `ready=0`.
- Hook observations confirm that `BeforeStartAsync` and `AfterStopAsync`
  received the expected scoped `IServiceProvider`.
- The runner exits with code `0`.

Approval:

```bash
dotnet build samples/Demos/Oragon.RabbitMQ.Demos.slnx
dotnet run --project samples/Demos/src/DemoHost -- list
dotnet run --project samples/Demos/src/DemoHost -- 16-application-gates
```

Current verification:

- Build/list can be verified without Docker.
- Broker smoke test is pending while Docker is unavailable in the current WSL
  session.
