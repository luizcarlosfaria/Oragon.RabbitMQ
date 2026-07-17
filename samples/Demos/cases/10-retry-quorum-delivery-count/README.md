# 10 - retry-quorum-delivery-count

Status: implemented.

Purpose: demonstrate broker-counted retry with quorum queues and terminal dead-lettering after a fixed number of attempts.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 10-retry-quorum-delivery-count
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
- Main queue `{ORAGON_DEMO_PREFIX}.10.input` is declared as durable quorum.
- DLQ `{ORAGON_DEMO_PREFIX}.10.dlq` and DLX `{ORAGON_DEMO_PREFIX}.10.dlx` are configured by `WithTopology`.
- The runner purges input and DLQ before publishing the poison message.

Scenarios:

- Publish one poison message.
- Handler receives `deliveryCount` and `attempts` by convention.
- First delivery has no broker header:
  - `deliveryCount=0`;
  - `attempts=null`.
- While the attempt number is below `3`, handler returns `AmqpResults.Reject(requeue: true)`.
- RabbitMQ redelivers the quorum queue message and increments `x-delivery-count`.
- Third delivery returns `AmqpResults.Reject(requeue: false)`.
- RabbitMQ routes the message to DLQ through dead-letter configuration.

Expected values:

- Attempts observed: `0/null,1/1,2/2`.
- Input queue ends with `ready=0`.
- DLQ ends with `ready=1`.

Why quorum matters:

- RabbitMQ exposes `x-delivery-count` for quorum queue redeliveries.
- Classic queues do not provide the same broker-counted retry signal.
- In RabbitMQ 4.x, `basic.reject` with requeue is the demonstrated path used here to increment the quorum delivery count.

Acceptance:

- The command exits with code `0`.
- The output contains `Demo 10 succeeded.`
- The output contains `Attempts observed: 0/null,1/1,2/2`.
- README explains why quorum queue matters.
- Logs show attempts before DLQ.

Behavior added to the demo suite:

- `10-retry-quorum-delivery-count` is an executable runner.
- The runner demonstrates delivery-count binding and terminal DLQ behavior.
- The runner keeps retry policy visible in application code instead of hiding it in library-owned global state.
