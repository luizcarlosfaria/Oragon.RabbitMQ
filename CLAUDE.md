# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Oragon.RabbitMQ is a Minimal API implementation for consuming RabbitMQ queues in .NET. It provides a fluent configuration API similar to ASP.NET Core's Minimal APIs, with built-in resilience features using RabbitMQ.Client 7.x natively (not HTTP/Kestrel-based).

## Build Commands

```bash
# Build all projects (uses solution XML format)
dotnet build ./Oragon.RabbitMQ.slnx

# Run unit tests (uses xUnit)
dotnet test ./tests/Oragon.RabbitMQ.UnitTests/Oragon.RabbitMQ.UnitTests.csproj

# Run integration tests (requires Docker for Testcontainers.RabbitMq)
dotnet test ./tests/Oragon.RabbitMQ.IntegratedTests/Oragon.RabbitMQ.IntegratedTests.csproj

# Run a single test
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Pack a specific project
dotnet pack ./src/Oragon.RabbitMQ/Oragon.RabbitMQ.csproj --configuration Release -p:PackageVersion=1.0.0
```

## Target Frameworks

Projects target `net8.0`, `net9.0`, and `net10.0`. The solution uses C# preview language features with nullable enabled by default (disabled in test projects).

## Architecture

### Core Projects (src/)

- **Oragon.RabbitMQ**: Main library with consumer infrastructure
- **Oragon.RabbitMQ.Abstractions**: Interfaces (`IAmqpResult`, `IAmqpSerializer`, `IAmqpContext`, `IHostedAmqpConsumer`)
- **Oragon.RabbitMQ.Serializer.SystemTextJson**: System.Text.Json serializer implementation
- **Oragon.RabbitMQ.Serializer.NewtonsoftJson**: Newtonsoft.Json serializer implementation
- **Oragon.RabbitMQ.AspireClient**: .NET Aspire integration (replaces Aspire.RabbitMQ.Client for RabbitMQ.Client 7.x support)

### Consumer Pipeline

1. **ConsumerServer** (`Consumer/ConsumerServer.cs`): `IHostedService` that manages all queue consumers
2. **ConsumerDescriptor** (`Consumer/ConsumerDescriptor.cs`): Fluent builder for configuring consumers (prefetch, concurrency, serializer, connection, failure handlers)
3. **QueueConsumer** (`Consumer/QueueConsumer.cs`): Individual queue consumer using `AsyncEventingBasicConsumer`, handles message lifecycle
4. **Dispatcher** (`Consumer/Dispatch/Dispatcher.cs`): Routes messages to handlers using argument binders and result handlers

### Key Extension Points

- **Argument Binders** (`Consumer/ArgumentBinders/`): Resolve handler parameters (`FromServicesArgumentBinder`, `MessageObjectArgumentBinder`, `DynamicArgumentBinder`)
- **Result Handlers** (`Consumer/ResultHandlers/`): Process handler return values (`VoidResultHandler`, `TaskResultHandler`, `TaskOfAmqpResultResultHandler`, `GenericResultHandler`)
- **AMQP Results** (`Consumer/Actions/`): Flow control results (`AckResult`, `NackResult`, `RejectResult`, `ReplyResult`, `ForwardResult`, `ComposableResult`)

### Registration Flow

```csharp
// 1. Register consumer server
builder.AddRabbitMQConsumer();  // Adds ConsumerServer as IHostedService

// 2. Register serializer
builder.Services.AddAmqpSerializer(options: JsonSerializerOptions.Default);

// 3. Map queues after app.Build()
app.MapQueue("queueName", (MyService svc, MyMessage msg) => svc.Handle(msg))
   .WithPrefetch(100)
   .WithDispatchConcurrency(4);
```

### Default Behaviors

- Serialization failures: `BasicReject` without requeue (configure with `WhenSerializationFail`)
- Processing failures: `BasicNack` without requeue (configure with `WhenProcessFail`)
- Successful processing: `BasicAck` (or return `IAmqpResult` for custom control)
- Uses manual acknowledgments (autoAck: false)
- Polly retry with exponential backoff for queue creation wait

## Testing

- **UnitTests**: Use Moq for mocking, xUnit framework
- **IntegratedTests**: Use Testcontainers.RabbitMq for real RabbitMQ instances
- **TestsExtensions**: Shared test utilities

## Local Build Pipeline

The CI/CD pipeline can be reproduced locally using Docker. The `Dockerfile` builds an environment with the .NET 10 SDK, Docker CLI, Java (for SonarScanner), and global tools (`dotnet-sonarscanner`, `dotnet-coverage`).

### 1. Build the builder image

```bash
docker build -t oragon-rabbitmq-builder .
```

### 2. Run the build environment

```bash
docker run --privileged -it --rm \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -w /projeto \
  -v ./:/projeto \
  oragon-rabbitmq-builder
```

The Docker socket mount (`-v /var/run/docker.sock:/var/run/docker.sock`) is required because integration tests use **Testcontainers** to spin up real RabbitMQ instances.

### 3. Inside the container, run the pipeline steps

```bash
# Restore workloads
dotnet workload restore ./Oragon.RabbitMQ.slnx

# Build
dotnet build ./Oragon.RabbitMQ.slnx

# Run all tests (unit + integration)
dotnet test ./Oragon.RabbitMQ.slnx

# Or run tests with coverage (as CI does)
dotnet-coverage collect "dotnet test --framework net10.0 -p:TargetFrameworks=net10.0" -f xml -o /output-coverage/coverage.xml
```

## CI/CD

Uses Jenkins pipeline (see `Jenkinsfile`). Tags ending with `-alpha` publish debug packages with symbols to MyGet. Tags ending with `-beta` or release tags publish to both MyGet and NuGet.

## Code Style

- Uses `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true`
- Generated interfaces via `AutomaticInterface` source generator (marked with `[GenerateAutomaticInterface]`)
- MIT licensed under ACADEMIA.DEV
