# 15 - observability-dashboard

Status: runner implemented; broker smoke test pending.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 15-observability-dashboard
```

Objective: demonstrate operational signals that a team can inspect while a real
consumer is running.

Initial conditions:

- RabbitMQ or Aspire environment is running.
- Console logs are enabled.
- `AMQP_URI` points to the broker, or defaults to
  `amqp://guest:guest@localhost:5672/`.
- RabbitMQ Management is available for the same broker, usually
  `http://localhost:15672`.
- Queues named from `{ORAGON_DEMO_PREFIX}.15.*` can be declared and purged.
- The runner uses `AddRabbitMQClient("messaging")` with health checks enabled.
- The runner configures:
  - input queue `{ORAGON_DEMO_PREFIX}.15.input`;
  - DLQ `{ORAGON_DEMO_PREFIX}.15.dlq`;
  - DLX `{ORAGON_DEMO_PREFIX}.15.dlx`;
  - consumer tag `oragon-demo-15-observability`;
  - client-provided connection name `oragon-demo-15-observability`.

Scenarios:

- Happy-path processing appears in console logs and is ACKed.
- A poison message throws inside the handler.
- `WhenProcessFail` logs the exception category and returns `Reject(false)`.
- RabbitMQ routes the rejected poison message to the DLQ through the configured
  dead-letter exchange.
- The Aspire client health check reports the RabbitMQ connection as healthy
  while the host is running.
- RabbitMQ Management can show:
  - the input queue with `ready=0`;
  - the DLQ with `ready=1`;
  - the consumer tag on the input queue while the runner is active;
  - the connection name `oragon-demo-15-observability`;
  - ready/unacked counters changing during processing.

Acceptance:

- Console output prints the AMQP URI, Management URL, input queue, DLQ, message
  counters, connection state and health status.
- `handledMessages=1`.
- `processFailures=1`.
- Input queue ends with `ready=0`.
- DLQ ends with `ready=1`.
- Health report status is `Healthy`.
- The runner exits with code `0`.

Operational checklist:

- Confirm the runner prints `message.received` for `happy-path`.
- Confirm the runner prints `message.failed ... result=Reject(false)` for the
  poison message.
- Confirm RabbitMQ Management shows `oragon-demo-15-observability` as the
  connection name while the runner is active.
- Confirm the input queue has no ready messages after processing.
- Confirm the DLQ contains one ready message after processing.
- Confirm the health output includes `Health status: Healthy`.

Approval:

```bash
dotnet build samples/Demos/Oragon.RabbitMQ.Demos.slnx
dotnet run --project samples/Demos/src/DemoHost -- list
dotnet run --project samples/Demos/src/DemoHost -- 15-observability-dashboard
```

Current verification:

- Build/list can be verified without Docker.
- Broker smoke test is pending while Docker is unavailable in the current WSL
  session.
