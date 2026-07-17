# 08 - aspire-worker

Status: source verification runner implemented; AppHost smoke test pending.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 08-aspire-worker
```

Objective: official Aspire worker sample path.

Initial conditions:

- .NET Aspire workload is available.
- Existing `samples/Aspire` app can be used as the implementation source.
- The verification command is run from inside this repository.
- Running the real AppHost requires Aspire/Docker availability:

```bash
dotnet run --project samples/Aspire/DotNetAspireApp.AppHost
```

Scenarios:

- AppHost starts RabbitMQ, API, Worker and Web resources.
- Worker uses `AddRabbitMQClient`.
- Health checks and tracing are visible.
- Workers consume messages from RabbitMQ.
- `DemoHost` source verification confirms:
  - `samples/Aspire/DotNetAspireApp.AppHost/Program.cs` declares RabbitMQ, API,
    Worker and Web resources;
  - AppHost references RabbitMQ through `WithReference(rabbitmq)`;
  - Worker registers `AddRabbitMQClient("rabbitmq")`;
  - Worker registers `AddRabbitMQConsumer`;
  - Worker waits for RabbitMQ and configures mapped consumers;
  - managed consumer extensions use `MapQueue`;
  - API also registers `AddRabbitMQClient("rabbitmq")`;
  - ServiceDefaults includes RabbitMQ OpenTelemetry instrumentation.

Acceptance:

- README points to the concrete Aspire project.
- RabbitMQ appears healthy in the Aspire dashboard.
- Source verification command exits with code `0`.
- AppHost smoke is approved only when the Aspire dashboard shows RabbitMQ, API,
  Worker and Web healthy and the Worker consumes messages published by the API
  or Web path.

Approval:

```bash
dotnet build samples/Demos/Oragon.RabbitMQ.Demos.slnx
dotnet run --project samples/Demos/src/DemoHost -- list
dotnet run --project samples/Demos/src/DemoHost -- 08-aspire-worker
dotnet run --project samples/Aspire/DotNetAspireApp.AppHost
```

Current verification:

- Source verification can be run without Docker and has its own runner.
- AppHost smoke test is pending while Docker/Aspire runtime execution is
  unavailable in the current WSL session.
