# 09 - keyed-rabbitmq

Status: implemented.

Purpose: demonstrate multiple keyed RabbitMQ clients and per-consumer connection selection.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 09-keyed-rabbitmq
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
- Two keyed RabbitMQ clients can point to the same broker, different brokers, or different virtual hosts.
- The runner uses the same `AMQP_URI` for both keys to stay local and reproducible.
- The runner differentiates the connections with `ClientProvidedName`.
- Queues `{ORAGON_DEMO_PREFIX}.09.primary` and `{ORAGON_DEMO_PREFIX}.09.secondary` are absent or can be safely redeclared.

Scenarios:

- Register keyed client `primary` using `AddKeyedRabbitMQClient("primary", ...)`.
- Register keyed client `secondary` using `AddKeyedRabbitMQClient("secondary", ...)`.
- Consumer A uses:

```csharp
.WithConnection((services, cancellationToken) =>
    Task.FromResult(services.GetRequiredKeyedService<IConnection>("primary")))
```

- Consumer B uses the same pattern with key `secondary`.
- A publish channel is opened from each keyed connection.
- Each queue receives one message.
- A separate host intentionally asks for missing key `missing` and verifies startup fails clearly.

Expected values:

- Primary handler receives route `primary`.
- Primary handler sees `ClientProvidedName=oragon-demo-09-primary`.
- Secondary handler receives route `secondary`.
- Secondary handler sees `ClientProvidedName=oragon-demo-09-secondary`.
- Both queues end with `ready=0`.
- Missing keyed connection produces an `InvalidOperationException`.

Acceptance:

- The command exits with code `0`.
- The output contains `Demo 09 succeeded.`
- The output contains `Misconfigured key failed clearly: True`.
- README explains when keyed connections are appropriate.
- The setup uses application configuration rather than library-owned global state.

When to use keyed connections:

- Different virtual hosts for isolation.
- Different RabbitMQ clusters for traffic classes.
- A dedicated connection for attention/dynamic consumption while regular work uses another connection.
- Explicit client names for broker-side observability.

Behavior added to the demo suite:

- `09-keyed-rabbitmq` is an executable runner.
- The runner demonstrates the Aspire keyed client registration and the library descriptor selection point.
- The runner keeps ownership in the application: keys, broker URIs and client names are app configuration, not global library state.
