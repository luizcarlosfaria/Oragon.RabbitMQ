# 02 - standalone-topology-dlq

Status: implemented.

Purpose: demonstrate a standalone consumer that owns its RabbitMQ topology through `WithTopology` and routes both serialization and handler failures to a DLQ.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 02-standalone-topology-dlq
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
- Demo exchanges and queues are absent or can be safely redeclared.
- The runner purges `{ORAGON_DEMO_PREFIX}.02.input` and `{ORAGON_DEMO_PREFIX}.02.dlq` inside `WithTopology` before consumption starts.
- `IConnectionFactory` is registered in DI so `WaitRabbitMQAsync` can probe the broker and the descriptor can create its consumer connection.

Scenarios:

- `WithTopology` declares:
  - direct exchange `{ORAGON_DEMO_PREFIX}.02.exchange`;
  - durable queue `{ORAGON_DEMO_PREFIX}.02.input`;
  - direct dead-letter exchange `{ORAGON_DEMO_PREFIX}.02.dlx`;
  - durable dead-letter queue `{ORAGON_DEMO_PREFIX}.02.dlq`;
  - queue binding from the main exchange with routing key `work`;
  - DLQ binding from the DLX with routing key `failed`;
  - queue dead-letter arguments via `QueueArguments.WithDeadLetter(...)`.
- The host calls `WaitRabbitMQAsync` before starting the consumer.
- An invalid JSON payload triggers `WhenSerializationFail`, which returns `AmqpResults.Reject(false)`.
- A valid JSON payload with `mode=throw` triggers a handler exception and `WhenProcessFail`, which returns `AmqpResults.Nack(false)`.
- Both failed messages reach the DLQ.

Acceptance:

- No manual topology creation is required.
- The command exits with code `0`.
- The output contains `Demo 02 succeeded.`
- The output shows exactly one serialization failure and one process failure.
- The DLQ ends with `ready=2`.
- `WaitRabbitMQAsync` usage is visible in the runner setup.
- `WithTopology`, `WhenSerializationFail` and `WhenProcessFail` are visible in the runner code.

RabbitMQ Management inspection:

- Open `http://localhost:15672` when using the local compose file.
- Login with `guest` / `guest`.
- Inspect queue `{ORAGON_DEMO_PREFIX}.02.dlq`.
- The queue should contain two ready messages after a successful run.

Behavior added to the demo suite:

- `02-standalone-topology-dlq` is an executable runner.
- The runner proves that topology creation can be colocated with the consumer descriptor.
- The runner separates deserialization failure policy from handler failure policy while sending both outcomes to RabbitMQ dead-lettering.
