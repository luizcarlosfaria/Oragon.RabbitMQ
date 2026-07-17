# 13 - dynamic-queue-consumer

Status: implemented; broker smoke test pending.

Purpose: demonstrate runtime-selected queue consumption windows with `IAmqpDynamicQueueConsumer`.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 13-dynamic-queue-consumer
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
- Dynamic queues are created and purged by the demo.
- The runner resolves `IAmqpDynamicQueueConsumer` from DI.
- The runner uses an explicit `IConnection` in the request to show that execution can target a selected connection.

Scenarios:

- `MaxMessages`:
  - queue starts with 3 messages;
  - request has `MaxMessages=2`;
  - status is `MaxMessagesReached`;
  - `MessagesReceived=2`;
  - `MessagesAcked=2`;
  - remaining ready count is `1`.
- `MaxDuration`:
  - queue starts with several messages;
  - request has `MaxDuration=250ms`;
  - handler simulates short work;
  - status is `MaxDurationReached`.
- `IdleTimeout`:
  - queue starts empty;
  - request has `IdleTimeout=150ms`;
  - no message is delivered;
  - status is `IdleTimeoutReached`.
- `StopAfterInitialQueueLength`:
  - queue starts with 2 messages;
  - first handler execution publishes one late message;
  - request has `StopAfterInitialQueueLength=true`;
  - status is `InitialQueueLengthReached`;
  - only the initial 2 messages are processed;
  - late message remains ready.
- Empty snapshot:
  - queue starts empty;
  - request has `StopAfterInitialQueueLength=true`;
  - no consumer is opened;
  - status is `Empty`.
- Missing queue:
  - request targets a queue name that the demo does not declare;
  - status is `QueueMissing`.
- Missing stop rule:
  - request omits `MaxMessages`, `MaxDuration`, `IdleTimeout`, `StopAfterInitialQueueLength` and external cancellation;
  - `ConsumeAsync` throws `InvalidOperationException`;
  - the demo treats that as expected and prints a clear error.

Hook behavior:

- `BeforeStartAsync` receives:
  - queue name;
  - initial ready count;
  - `IServiceProvider`;
  - request metadata.
- `AfterStopAsync` receives:
  - final `DynamicQueueConsumeResult`;
  - `IServiceProvider`;
  - request metadata.
- The runner validates both hooks in the `MaxMessages` scenario.

Statuses shown by the demo:

- `MaxMessagesReached`;
- `MaxDurationReached`;
- `IdleTimeoutReached`;
- `InitialQueueLengthReached`;
- `Empty`;
- `QueueMissing`.

Important behavior:

- At least one stop rule is required.
- Stop rules are independent and the first reached rule wins.
- `IdleTimeout` is evaluated only when there is no in-flight message.
- `StopAfterInitialQueueLength` uses the ready count observed at the beginning of consumption; messages published after start are not included in that processing window. When the initial snapshot is zero, the result status is `Empty`.
- `MaxMessages` alone waits until the requested number of deliveries finishes; add `IdleTimeout`, `MaxDuration` or `StopAfterInitialQueueLength` when a quiet queue must stop deterministically.
- The library does not own attention/business keys. The app passes queue name, metadata and optional connection/channel factories.

Acceptance:

- The command exits with code `0`.
- The output contains `Demo 13 succeeded.`
- The output prints status, initial count, received, acked and remaining count for each scenario.
- README explains all statuses.
- Logs show received, acked and remaining counts.

Behavior added to the demo suite:

- `13-dynamic-queue-consumer` is an executable runner.
- The runner demonstrates dynamic consumption directly through `IAmqpDynamicQueueConsumer`.
- The runner proves the stop-rule model without introducing `MapAttentionQueue(...)`.
