# Oragon.RabbitMQ demos

This directory is the executable companion to the documentation roadmap.

The implementation follows `spec/demo-cases-roadmap.md`. Phase 0 is the shared
demo infrastructure: a reproducible RabbitMQ container, a demo catalog, shared
helpers, and one host command that lists the 17 planned cases.

## Status

- Phase 0 infrastructure: implemented in this directory.
- Case runners 01-07, 09 and 10: implemented and smoke-tested against the local RabbitMQ compose file.
- Case runner 08: source verification implemented; AppHost smoke test pending.
- Case runners 11, 12, 13, 14, 15, 16 and 17: implemented and build-verified; broker smoke tests pending.
- All 17 cases have README coverage and a concrete command.
- The command `list` is intentionally broker-free and should work after build.

## Prerequisites

- .NET SDK capable of `net9.0` and `net10.0`.
- Docker, when running cases that need RabbitMQ.

Default connection:

```bash
export AMQP_URI=amqp://guest:guest@localhost:5672/
```

Default broker resource prefix:

```bash
export ORAGON_DEMO_PREFIX=oragon.demo
```

## Build

```bash
dotnet build samples/Demos/Oragon.RabbitMQ.Demos.slnx
```

## Start RabbitMQ

```bash
docker compose -f samples/Demos/docker-compose.yml up -d
```

RabbitMQ Management:

```text
http://localhost:15672
guest / guest
```

## Commands

List all cases:

```bash
dotnet run --project samples/Demos/src/DemoHost -- list
```

Describe one case:

```bash
dotnet run --project samples/Demos/src/DemoHost -- describe 13-dynamic-queue-consumer
```

Implemented case command shape:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 01-minimal-consumer
```

For cases that are not implemented yet, direct case execution returns a clear
`runner pending` message instead of pretending the case is complete.

## Cases

| ID | Case | Phase | Requires broker |
| --- | --- | --- | --- |
| 01 | `minimal-consumer` | Basic usage | yes |
| 02 | `standalone-topology-dlq` | Basic usage | yes |
| 03 | `model-binding-lab` | Basic usage | yes |
| 04 | `flow-control-results` | Basic usage | yes |
| 05 | `rpc-request-reply` | Basic usage | yes |
| 06 | `concurrency-prefetch` | Basic usage | yes |
| 07 | `serializers` | Basic usage | yes |
| 08 | `aspire-worker` | Aspire | yes |
| 09 | `keyed-rabbitmq` | Aspire/keyed connections | yes |
| 10 | `retry-quorum-delivery-count` | Reliability | yes |
| 11 | `graceful-shutdown` | Attention primitives | yes |
| 12 | `requeue-to-tail` | Attention primitives | yes |
| 13 | `dynamic-queue-consumer` | Attention primitives | yes |
| 14 | `attention-with-primitives` | Attention primitives | yes |
| 15 | `observability-dashboard` | Operations | yes |
| 16 | `application-gates` | Extension points | yes |
| 17 | `publish-consistency-work-attention` | Consistency | yes |

## Approval gate

The demo host baseline is approved when these commands pass:

```bash
dotnet build samples/Demos/Oragon.RabbitMQ.Demos.slnx
dotnet run --project samples/Demos/src/DemoHost -- list
```

Implemented case runners should also pass with the local broker:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 01-minimal-consumer
dotnet run --project samples/Demos/src/DemoHost -- 02-standalone-topology-dlq
dotnet run --project samples/Demos/src/DemoHost -- 03-model-binding-lab
dotnet run --project samples/Demos/src/DemoHost -- 04-flow-control-results
dotnet run --project samples/Demos/src/DemoHost -- 05-rpc-request-reply
dotnet run --project samples/Demos/src/DemoHost -- 06-concurrency-prefetch
dotnet run --project samples/Demos/src/DemoHost -- 07-serializers
dotnet run --project samples/Demos/src/DemoHost -- 09-keyed-rabbitmq
dotnet run --project samples/Demos/src/DemoHost -- 10-retry-quorum-delivery-count
```

Source-verification case:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 08-aspire-worker
```

Build-verified but still pending broker smoke test:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 11-graceful-shutdown
dotnet run --project samples/Demos/src/DemoHost -- 12-requeue-to-tail
dotnet run --project samples/Demos/src/DemoHost -- 13-dynamic-queue-consumer
dotnet run --project samples/Demos/src/DemoHost -- 14-attention-with-primitives
dotnet run --project samples/Demos/src/DemoHost -- 15-observability-dashboard
dotnet run --project samples/Demos/src/DemoHost -- 16-application-gates
dotnet run --project samples/Demos/src/DemoHost -- 17-publish-consistency-work-attention
```

The Docker command should also start a local broker when Docker is available:

```bash
docker compose -f samples/Demos/docker-compose.yml up -d
```
