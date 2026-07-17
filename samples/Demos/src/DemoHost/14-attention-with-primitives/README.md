# 14 - attention-with-primitives

Status: implemented; broker smoke test pending.

Purpose: compose the attention pattern from generic primitives without adding an opinionated `MapAttentionQueue(...)` API.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 14-attention-with-primitives
```

Optional environment:

```bash
AMQP_URI=amqp://guest:guest@localhost:5672/
ORAGON_DEMO_PREFIX=oragon.demo
```

Broker helper:

```bash
docker compose -f samples/Demos/docker-compose.yml up -d
```

Initial conditions:

- RabbitMQ is running.
- Attention queue `{ORAGON_DEMO_PREFIX}.14.attention` is declared by the demo.
- Work queues are declared by the demo:
  - `{ORAGON_DEMO_PREFIX}.14.work.noisy`;
  - `{ORAGON_DEMO_PREFIX}.14.work.small`.
- Application-owned in-memory `IAmqpConcurrencyGate` is registered.
- Redis is not required.

Scenarios:

- Publish 5 work messages for entity `noisy`.
- Publish 1 work message for entity `small`.
- Publish two attention messages for `noisy` to simulate competing workers for the same entity.
- Publish one attention message for `small`.
- The attention consumer is a regular `MapQueue` handler.
- The handler receives `IAmqpDynamicQueueConsumer` and `IAmqpConcurrencyGate` from DI using `[FromServices]`.
- The application builds the gate key as `attention:{entityId}`.
- If the gate is already held, the app policy records the blocked attempt and returns `Ack`.
- If the gate is acquired, the handler opens a dynamic consumption window over the entity work queue.
- `noisy` uses `MaxMessages=2`, so it processes a slice and returns `RequeueToTail` while work remains.
- `small` drains in one cycle and returns `Ack`.
- `BeforeStartAsync` and `AfterStopAsync` receive `IServiceProvider` and metadata and record diagnostics.

Expected values:

- Total work processed: `6`.
- Noisy work processed: `5`.
- Small work processed: `1`.
- Gate blocked attempts: at least `1`.
- Attention requeues: at least `1`.
- Attention queue ends with `ready=0`.
- Dynamic hook diagnostics are printed.

Acceptance:

- The code uses `MapQueue` and `IAmqpDynamicQueueConsumer`.
- No `MapAttentionQueue(...)` exists.
- Redis is not required.
- README separates library responsibilities from application responsibilities.

Library responsibilities shown:

- `MapQueue` consumes the aggregated attention queue.
- `IAmqpDynamicQueueConsumer` consumes a runtime-selected work queue for a bounded window.
- `RequeueToTail` republishes an attention message after the current one when more work remains.
- `WithGracefulShutdown` cancels handler tokens and drains in-flight attention messages.
- Hooks expose `IServiceProvider` and metadata without choosing a storage technology.

Application responsibilities shown:

- Choose the attention payload shape.
- Resolve the work queue name from the attention message.
- Choose stop rules for each entity.
- Choose the gate key format.
- Implement the gate storage/lease semantics.
- Decide what to do when the gate is unavailable.
- Decide whether remaining work should requeue attention or finish the cycle.
- Own Redis, SQL, in-memory or any other gate provider if needed.

Behavior added to the demo suite:

- `14-attention-with-primitives` is an executable runner.
- The runner demonstrates the complete attention composition without introducing `MapAttentionQueue(...)`.
- The runner keeps domain lifecycle, lock naming and provider ownership in application code.
