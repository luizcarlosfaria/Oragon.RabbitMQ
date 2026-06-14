---
title: Introduction
nextjs:
  metadata:
    title: Oragon.RabbitMQ
    description: Minimal APIs for RabbitMQ in .NET.
---

Oragon.RabbitMQ brings the Minimal APIs style to RabbitMQ consumers in .NET. If you already know `MapPost()` in ASP.NET Core, `MapQueue()` should feel familiar: map a queue to a handler, with dependency injection, parameter binding, serialization, and explicit ACK/NACK control when you need it. {% .lead %}

{% quick-links %}

{% quick-link title="Installation" icon="installation" href="/docs/installation" description="Install the core packages and configure RabbitMQ in a .NET application." /%}

{% quick-link title="Quick start" icon="presets" href="/docs/quick-start" description="Build a minimal worker with serializer, connection, and first consumer." /%}

{% quick-link title="MapQueue" icon="plugins" href="/docs/map-queue" description="Understand consumption, concurrency, prefetch, and topology." /%}

{% quick-link title="Flow control" icon="theming" href="/docs/flow-control" description="Control ACK, NACK, Reject, Forward, Reply, and composed results." /%}

{% /quick-links %}

---

## What it is

Oragon.RabbitMQ is a library for building resilient RabbitMQ consumers without manually writing all the consumption infrastructure, DI scoping, deserialization, error handling, and message acknowledgment code.

```csharp
// ASP.NET Core - HTTP
app.MapPost("/orders", ([FromServices] OrderService svc, [FromBody] OrderCreated msg) =>
    svc.HandleAsync(msg));

// Oragon.RabbitMQ - AMQP
app.MapQueue("orders", ([FromServices] OrderService svc, [FromBody] OrderCreated msg) =>
    svc.HandleAsync(msg));
```

It does not use HTTP or Kestrel to consume messages. The implementation is built directly on `RabbitMQ.Client` 7.x.

## Why use it

- Familiar API for ASP.NET Core developers.
- Native AMQP consumption with manual acknowledgment by default.
- Parameter binding with `[FromServices]`, `[FromBody]`, and `[FromAmqpHeader]`.
- Pluggable serialization with System.Text.Json or Newtonsoft.Json.
- Composable results with `Ack`, `Nack`, `Reject`, `Reply`, `Forward`, and `Compose`.
- .NET Aspire integration through `Oragon.RabbitMQ.AspireClient`.

## Packages

| Package | Purpose |
| --- | --- |
| `Oragon.RabbitMQ` | Core consumer infrastructure, `MapQueue`, and flow control |
| `Oragon.RabbitMQ.Abstractions` | Contracts such as `IAmqpResult`, `IAmqpSerializer`, and `IAmqpContext` |
| `Oragon.RabbitMQ.Serializer.SystemTextJson` | System.Text.Json serializer |
| `Oragon.RabbitMQ.Serializer.NewtonsoftJson` | Newtonsoft.Json serializer |
| `Oragon.RabbitMQ.AspireClient` | .NET Aspire integration with RabbitMQ.Client 7.x |

## Next steps

Start with installation, then follow the quick start. The next pages cover `MapQueue`, AMQP results, serialization, model binding, concepts, and benchmarks.
