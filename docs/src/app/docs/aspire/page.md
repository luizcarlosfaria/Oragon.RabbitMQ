---
title: Aspire integration
nextjs:
  metadata:
    title: Aspire integration
    description: Use Oragon.RabbitMQ with .NET Aspire.
---

`Oragon.RabbitMQ.AspireClient` registers RabbitMQ.Client 7.x services for Aspire applications. {% .lead %}

---

## AppHost

```csharp
var rabbitmq = builder.AddRabbitMQ("rabbitmq");

builder.AddProject<Projects.Worker>("worker")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq);
```

## Worker

```csharp
builder.AddRabbitMQClient("rabbitmq");
builder.Services.AddRabbitMQConsumer();
builder.Services.AddSystemTextJsonAmqpSerializer();
```

The Aspire package contributes `IConnectionFactory`, `IConnection`, health checks, and client configuration. It exists because the built-in Aspire package may lag RabbitMQ.Client major versions.

## Keyed clients

When an app has more than one RabbitMQ resource, register keyed clients and select them in `WithConnection(...)` or dynamic queue requests.

```csharp
builder.AddRabbitMQClient("rabbitmq-attention", settings => settings.DisableHealthChecks = false);
builder.AddRabbitMQClient("rabbitmq-work");
```

The sample under `samples/Aspire` demonstrates AppHost, API, Worker, and Web projects.
