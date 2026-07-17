# 01 - minimal-consumer

Status: implemented.

Purpose: prove the smallest useful consumer setup with one queue, one strongly typed JSON body and the default `Ack` behavior.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 01-minimal-consumer
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
- `AMQP_URI` points to the broker or uses the default local URI.
- Queue `{ORAGON_DEMO_PREFIX}.01.input` is absent or can be purged by the demo.

Scenarios:

- Declare a durable demo queue.
- Purge the queue to isolate the run.
- Register `AddRabbitMQConsumer`.
- Register `AddSystemTextJsonAmqpSerializer`.
- Register a singleton `IConnection`.
- Map the queue with `MapQueue`.
- Publish one valid JSON message.
- `MapQueue` receives the body.
- The handler returns no explicit `IAmqpResult`.
- The default result is `Ack`.
- Stop the host after the message is observed.
- Passively declare the queue and verify the ready count.

Acceptance:

- The message is processed once.
- The command exits with code `0`.
- The output contains `Demo 01 succeeded.`
- The queue ends with `ready=0` after the host stop.
- If a delivery stayed unacked during channel shutdown, it would return to the queue; therefore `ready=0` after stop is the observable CLI acceptance condition for this runner.
- The README command and code show `AddRabbitMQConsumer`, serializer registration, singleton `IConnection`, `WithConnection`, `WithSerializer` and `MapQueue`.

Behavior added to the demo suite:

- `01-minimal-consumer` is an executable runner, not only a catalog entry.
- The runner validates the library's default result path: a handler that completes successfully without returning `IAmqpResult` is acknowledged.
- The runner intentionally uses the library mapping API instead of direct RabbitMQ consumption, so it exercises the same public surface documented for application users.
