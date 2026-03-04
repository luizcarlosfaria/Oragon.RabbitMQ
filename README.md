![Card](https://raw.githubusercontent.com/luizcarlosfaria/Oragon.RabbitMQ/master/src/Assets/opengraph-card.png)

# Oragon.RabbitMQ

**Minimal APIs for RabbitMQ in .NET** — Consume queues with the same `MapQueue()` pattern you already know from `MapPost()`.

> Oragon.RabbitMQ is not HTTP/Kestrel-based. It is a fully custom implementation built on **RabbitMQ.Client 7.x** natively.

---

**Quality**

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=alert_status)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=bugs)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=code_smells)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=coverage)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=duplicated_lines_density)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=reliability_rating)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=security_rating)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=sqale_index)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=sqale_rating)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=vulnerabilities)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)

**Releases**

[![NuGet Version](https://img.shields.io/nuget/v/Oragon.RabbitMQ?logo=nuget&label=nuget)](https://www.nuget.org/packages?q=Oragon.RabbitMQ&includeComputedFrameworks=true&prerel=true&sortby=created-desc)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Oragon.RabbitMQ)](https://www.nuget.org/packages/Oragon.RabbitMQ/)
[![GitHub Tag](https://img.shields.io/github/v/tag/luizcarlosfaria/Oragon.RabbitMQ)](https://github.com/luizcarlosfaria/Oragon.RabbitMQ/tags)
[![GitHub Release](https://img.shields.io/github/v/release/luizcarlosfaria/Oragon.RabbitMQ)](https://github.com/luizcarlosfaria/Oragon.RabbitMQ/releases)
[![MyGet Version](https://img.shields.io/myget/oragon/vpre/Oragon.RabbitMQ?logo=myget&label=myget)](https://www.myget.org/feed/Packages/oragon)

**Project**

[![GitHub Repo stars](https://img.shields.io/github/stars/luizcarlosfaria/Oragon.RabbitMQ)](https://github.com/luizcarlosfaria/Oragon.RabbitMQ)
[![GitHub last commit](https://img.shields.io/github/last-commit/luizcarlosfaria/Oragon.RabbitMQ)](https://github.com/luizcarlosfaria/Oragon.RabbitMQ/commits/)
[![Roadmap](https://img.shields.io/badge/Roadmap-%23ff6600?logo=github&logoColor=%23000000&label=GitHub&labelColor=%23f0f0f0)](https://github.com/users/luizcarlosfaria/projects/3/views/3)
![.NET 8](https://img.shields.io/badge/.NET_8-5C2D91?style=flat&logo=dotnet&label=target)
![.NET 9](https://img.shields.io/badge/.NET_9-5C2D91?style=flat&logo=dotnet&label=target)
![.NET 10](https://img.shields.io/badge/.NET_10-5C2D91?style=flat&logo=dotnet&label=target)

**Tech**

![C#](https://img.shields.io/badge/c%23-%23239120.svg?style=for-the-badge&logo=csharp&logoColor=white)
![.Net](https://img.shields.io/badge/.NET-5C2D91?style=for-the-badge&logo=.net&logoColor=white)
![Visual Studio](https://img.shields.io/badge/Visual%20Studio-5C2D91.svg?style=for-the-badge&logo=visual-studio&logoColor=white)
[![Jenkins](https://img.shields.io/badge/jenkins-%232C5263.svg?style=for-the-badge&logo=jenkins&logoColor=white)](https://jenkins.oragon.io/job/oragon/job/Oragon.RabbitMQ/)
[![Telegram](https://img.shields.io/badge/Telegram-2CA5E0?style=for-the-badge&logo=telegram&logoColor=white)](https://t.me/luizcarlosfaria)

---

## What is Oragon.RabbitMQ?

If you know ASP.NET Core Minimal APIs, you already know Oragon.RabbitMQ:

```csharp
// ASP.NET Core — HTTP
app.MapPost("/orders", ([FromServices] OrderService svc, [FromBody] OrderCreated msg) => svc.HandleAsync(msg));

// Oragon.RabbitMQ — AMQP
app.MapQueue("orders", ([FromServices] OrderService svc, [FromBody] OrderCreated msg) => svc.HandleAsync(msg));
```

It provides everything you need to create resilient RabbitMQ consumers without the need to study numerous books and articles or introduce unknown risks to your environment. All queue consumption settings are configurable through a friendly, fluent, and consistent API.

## Why Oragon.RabbitMQ?

- **Minimal API design** — familiar `MapQueue()` pattern, zero learning curve for ASP.NET Core developers
- **RabbitMQ.Client 7.x native** — no HTTP, no Kestrel, pure AMQP
- **Near-zero overhead** — benchmarks prove 0-1% overhead for I/O-bound workloads
- **Built-in resilience** — automatic Ack/Nack/Reject with manual acknowledgment by default
- **DI-first** — `[FromServices]`, `[FromBody]`, `[FromAmqpHeader]` attribute binding
- **Pluggable serialization** — System.Text.Json or Newtonsoft.Json out of the box
- **Composable flow control** — Ack, Nack, Reject, Reply, Forward, and Compose results
- **.NET Aspire integration** — first-class support via `Oragon.RabbitMQ.AspireClient`
- **OpenTelemetry native** — via RabbitMQ.Client 7.x built-in instrumentation
- **Multi-framework** — targets .NET 8, 9, and 10

## Quick Start

### Standalone

```bash
dotnet add package Oragon.RabbitMQ
dotnet add package Oragon.RabbitMQ.Serializer.SystemTextJson
```

```csharp
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Serializer;
using RabbitMQ.Client;

var builder = Host.CreateApplicationBuilder(args);

// 1. Register consumer infrastructure
builder.AddRabbitMQConsumer();

// 2. Register serializer
builder.Services.AddAmqpSerializer(options: JsonSerializerOptions.Default);

// 3. Register RabbitMQ connection
builder.Services.AddSingleton<IConnectionFactory>(sp => new ConnectionFactory()
{
    Uri = new Uri("amqp://guest:guest@localhost:5672"),
    DispatchConsumersAsync = true
});
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IConnectionFactory>().CreateConnectionAsync().GetAwaiter().GetResult());

// 4. Register your service
builder.Services.AddSingleton<OrderService>(); // singleton, scoped or transient

var app = builder.Build();
```

### Example 1

```csharp
// 5. Map queue to handler
app.MapQueue("orders", ([FromServices] OrderService svc, OrderCreated msg) =>
    svc.HandleAsync(msg));

app.Run();
```

### Example 2

Asume the controller has a method `CanProcess` that returns a boolean.

```csharp
app.MapQueue("orders", async ([FromServices] OrderService svc, OrderCreated msg) =>
{
    if (svc.CanProcess(msg))
    {
        await svc.HandleAsync(msg);
        return AmqpResults.Ack();
    }
    return AmqpResults.Nack(requeue: true);
});
```

### Example 3

You can also handle exceptions yourself and return a valid `IAmqpResult`:

```csharp
app.MapQueue("orders", async ([FromServices] OrderService svc, OrderCreated msg) =>
{
    try
    {
        await svc.HandleAsync(msg);
        return AmqpResults.Ack();
    }
    catch (Exception ex)
    {
        // Log the exception
        return AmqpResults.Nack(requeue: true);
    }
});
```

### With .NET Aspire

Replace `Aspire.RabbitMQ.Client` with `Oragon.RabbitMQ.AspireClient` to get RabbitMQ.Client 7.x support:

```bash
dotnet add package Oragon.RabbitMQ.AspireClient
```

```csharp
builder.AddRabbitMQClient("rabbitmq");
```

> After `Aspire.RabbitMQ.Client` gains RabbitMQ.Client 7.x support, the `Oragon.RabbitMQ.AspireClient` package will be deprecated.

## Packages

| Package                                                                                                                  | Purpose                                                        |
| ------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------- |
| [`Oragon.RabbitMQ`](https://www.nuget.org/packages/Oragon.RabbitMQ/)                                                     | Core library — consumer infrastructure, MapQueue, flow control |
| [`Oragon.RabbitMQ.Abstractions`](https://www.nuget.org/packages/Oragon.RabbitMQ.Abstractions/)                           | Interfaces (`IAmqpResult`, `IAmqpSerializer`, `IAmqpContext`)  |
| [`Oragon.RabbitMQ.Serializer.SystemTextJson`](https://www.nuget.org/packages/Oragon.RabbitMQ.Serializer.SystemTextJson/) | System.Text.Json serializer                                    |
| [`Oragon.RabbitMQ.Serializer.NewtonsoftJson`](https://www.nuget.org/packages/Oragon.RabbitMQ.Serializer.NewtonsoftJson/) | Newtonsoft.Json serializer                                     |
| [`Oragon.RabbitMQ.AspireClient`](https://www.nuget.org/packages/Oragon.RabbitMQ.AspireClient/)                           | .NET Aspire integration (RabbitMQ.Client 7.x)                  |

## Configuration Reference

All configuration is done through fluent methods on the `ConsumerDescriptor` returned by `MapQueue()`:

| Method                                                                          | Description                                   | Default                   |
| ------------------------------------------------------------------------------- | --------------------------------------------- | ------------------------- |
| `.WithPrefetch(ushort)`                                                         | Number of messages prefetched from the broker | `1`                       |
| `.WithDispatchConcurrency(ushort)`                                              | Concurrent message processing slots           | `1`                       |
| `.WithConsumerTag(string)`                                                      | Custom consumer tag                           | auto-generated            |
| `.WithExclusive(bool)`                                                          | Exclusive consumer on the queue               | `false`                   |
| `.WithTopology(Func<IChannel, CancellationToken, Task>)`                        | Declare exchanges/queues/bindings on startup  | none                      |
| `.WithConnection(Func<IServiceProvider, CancellationToken, Task<IConnection>>)` | Custom connection factory                     | `IConnection` from DI     |
| `.WithSerializer(Func<IServiceProvider, IAmqpSerializer>)`                      | Custom serializer factory                     | `IAmqpSerializer` from DI |
| `.WithChannel(Func<IConnection, CancellationToken, Task<IChannel>>)`            | Custom channel factory                        | auto-created              |
| `.WhenSerializationFail(Func<IAmqpContext, Exception, IAmqpResult>)`            | Behavior on deserialization errors            | `Reject(requeue: false)`  |
| `.WhenProcessFail(Func<IAmqpContext, Exception, IAmqpResult>)`                  | Behavior on handler exceptions                | `Nack(requeue: false)`    |

```csharp
app.MapQueue("orders", ([FromServices] OrderService svc, OrderCreated msg) =>
    svc.HandleAsync(msg))
    .WithPrefetch(100)
    .WithDispatchConcurrency(8)
    .WhenProcessFail((ctx, ex) => AmqpResults.Nack(requeue: true));
```

## Flow Control

By default, Oragon handles acknowledgments automatically:

- **Success** → `BasicAck`
- **Serialization failure** → `BasicReject` (no requeue) — use dead-lettering
- **Processing failure** → `BasicNack` (no requeue) — use dead-lettering

For explicit control, return an `IAmqpResult` from your handler:

| Group           | Method                                                                      | Description                 |
| --------------- | --------------------------------------------------------------------------- | --------------------------- |
| **Basic**       | `AmqpResults.Ack()`                                                         | Acknowledge the message     |
|                 | `AmqpResults.Nack(requeue)`                                                 | Negative acknowledge        |
|                 | `AmqpResults.Reject(requeue)`                                               | Reject the message          |
| **RPC**         | `AmqpResults.Reply<T>(T)`                                                   | Reply to the caller         |
|                 | `AmqpResults.ReplyAndAck<T>(T)`                                             | Reply and acknowledge       |
| **Routing**     | `AmqpResults.Forward<T>(exchange, routingKey, mandatory, params T[])`       | Forward to another exchange |
|                 | `AmqpResults.ForwardAndAck<T>(exchange, routingKey, mandatory, params T[])` | Forward and acknowledge     |
| **Composition** | `AmqpResults.Compose(params IAmqpResult[])`                                 | Combine multiple results    |

## Model Binding

### Attributes

| Attribute                 | Resolves from                          |
| ------------------------- | -------------------------------------- |
| `[FromServices]`          | DI container (supports keyed services) |
| `[FromBody]`              | Deserialized message body              |
| `[FromAmqpHeader("key")]` | AMQP message header by key             |

### Auto-bound Types

These types are resolved automatically by the model binder without any attribute:

| Type                       | Value                       |
| -------------------------- | --------------------------- |
| `IConnection`              | Current RabbitMQ connection |
| `IChannel`                 | Current RabbitMQ channel    |
| `BasicDeliverEventArgs`    | Raw delivery event          |
| `DeliveryModes`            | Message delivery mode       |
| `IReadOnlyBasicProperties` | Message properties          |
| `IServiceProvider`         | Scoped service provider     |
| `IAmqpContext`             | Full AMQP context           |
| `CancellationToken`        | Cancellation token          |

### Auto-bound String Parameters

String parameters are matched by name convention:

| Parameter names            | Value                      |
| -------------------------- | -------------------------- |
| `queue`, `queueName`       | Name of the consumed queue |
| `routing`, `routingKey`    | Message routing key        |
| `exchange`, `exchangeName` | Source exchange name       |
| `consumer`, `consumerTag`  | Consumer tag               |

## Telemetry

RabbitMQ.Client 7.x implements native OpenTelemetry instrumentation via `System.Diagnostics.ActivitySource`. Your existing OpenTelemetry collectors will capture AMQP operations automatically without any additional configuration in this library.

## Benchmarks

All benchmarks compare **Oragon.RabbitMQ** against **hand-written native RabbitMQ.Client code** performing the same DI scoping, serialization, try/catch, and ack/nack logic.

**Environment:** AMD Ryzen 9 9950X3D (16 cores / 32 threads), .NET 9.0.12, Windows 11, GC Server=True, BenchmarkDotNet v0.14.0.

### Performance Summary

| Benchmark           | Scenario                             | Oragon Overhead        | Verdict                       |
| ------------------- | ------------------------------------ | ---------------------- | ----------------------------- |
| Concurrency Scaling | I/O-Bound (1000 msgs, Task.Delay)    | 0 - 1%                 | **Excellent** - Zero overhead |
| Concurrency Scaling | CPU-Bound (1000 msgs, HashCode loop) | 2 - 8%                 | **Very Good**                 |
| Throughput          | NoOp handler (1000-5000 msgs)        | 0 - 11%                | **Good**                      |
| Throughput          | CPU-Bound handler                    | 0 - 14%                | **Good**                      |
| Latency             | Single message (all handlers)        | 5 - 7% (~3.5 ms fixed) | **Good**                      |
| Allocation          | Large messages (100 msgs)            | 9% time, 1% memory     | **Excellent**                 |
| RPC                 | ReplyAndAck vs native dedicated      | **-7% (Oragon wins)**  | **Excellent**                 |

### RPC Performance

| Size   | Native Dedicated (ms) | Oragon ReplyAndAck (ms) | Ratio    |
| ------ | --------------------- | ----------------------- | -------- |
| Small  | 50.1                  | **46.8**                | **0.93** |
| Medium | 50.4                  | **47.1**                | **0.93** |

Oragon is **7% faster** and allocates **17% less memory** for RPC by reusing pre-warmed infrastructure.

### Memory Allocation Summary

| Scenario               | Oragon Overhead | Context                          |
| ---------------------- | --------------- | -------------------------------- |
| Large messages         | ~1%             | Message body dominates           |
| Small messages (bulk)  | ~20%            | Fixed DI scope + pipeline cost   |
| Single message latency | 2 - 3x          | Fixed overhead dominates         |
| RPC                    | **17% less**    | Reuses pre-warmed infrastructure |

### Concurrency Scaling (I/O-Bound)

1000 messages with `Task.Delay(5)` handler. This is the most representative scenario for real-world workloads.

| Prefetch | Concurrency | Native (ms) | Oragon (ms) | Ratio |
| -------- | ----------- | ----------- | ----------- | ----- |
| 10       | 2           | 2,700       | 2,706       | 1.00  |
| 10       | 4           | 1,380       | 1,390       | 1.01  |
| 10       | 8           | 728         | 731         | 1.00  |
| 50       | 4           | 1,351       | 1,357       | 1.00  |
| 50       | 8           | 694         | 694         | 1.00  |
| 100      | 4           | 1,347       | 1,352       | 1.00  |
| 100      | 8           | 677         | 681         | 1.01  |

**Conclusion:** Ratio consistently between 0.98 - 1.01. **Zero latency overhead** for I/O-bound workloads.

### Key Takeaways

1. **I/O-bound workloads** (the most common real-world scenario) — **zero measurable overhead**.
2. **CPU-bound with small messages** (worst case) — 5-10% overhead from DI scope + dispatch pipeline.
3. **RPC** — Oragon is **faster and more memory-efficient** than hand-written code.
4. **The overhead is predictable and constant:** ~3.5 ms fixed per message + ~2.5 KB of fixed allocation. In production workloads with larger messages and heavier handlers, this fixed cost becomes irrelevant.
5. **Fair trade-off:** For ~5% overhead in the worst case, Oragon provides: integrated DI, pluggable serialization, automatic error handling, declarative flow control, and a clean API.

> Full benchmark results are available in `benchmarks/Oragon.RabbitMQ.Benchmarks/BenchmarkDotNet.Artifacts/results/`.

<details>
<summary><strong>Design Philosophy</strong></summary>

### Decoupling Business Logic from Infrastructure

Oragon.RabbitMQ is designed to decouple RabbitMQ consumers from business logic. Your business code remains completely unaware of the queue consumption context — resulting in simple, decoupled, agnostic, reusable, and highly testable code.

### Manual Acknowledgment by Default

This consumer is focused on creating resilient consumers using manual acknowledgments (`autoAck: false`). The automatic flow handles Ack/Nack/Reject so you don't have to, but you can take control at any time by returning `IAmqpResult`.

### Dead-Lettering as Recommended Pattern

Both serialization failures (`Reject`) and processing failures (`Nack`) default to no requeue. This is intentional — configure dead-letter exchanges on your queues to capture failed messages for inspection, replay, or alerting.

</details>

## Samples

- [Standalone sample](samples/Standalone/) — minimal console worker
- [.NET Aspire sample](samples/Aspire/) — full Aspire integration

## Contributing

Contributions are welcome! Please open an [issue](https://github.com/luizcarlosfaria/Oragon.RabbitMQ/issues) or submit a pull request.

## License

[MIT](LICENSE) — LUIZ CARLOS FARIA - ACADEMIA.DEV - MENSAGERIA.NET
