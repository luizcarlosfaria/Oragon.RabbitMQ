---
title: Installation
nextjs:
  metadata:
    title: Installation
    description: Install Oragon.RabbitMQ packages.
---

Install the core package and a serializer. Serializers are separate so you can choose System.Text.Json or Newtonsoft.Json. {% .lead %}

---

## Core packages

For the common System.Text.Json setup:

```shell
dotnet add package Oragon.RabbitMQ
dotnet add package Oragon.RabbitMQ.Serializer.SystemTextJson
```

For Newtonsoft.Json:

```shell
dotnet add package Oragon.RabbitMQ
dotnet add package Oragon.RabbitMQ.Serializer.NewtonsoftJson
```

## Service registration

In a Generic Host application, register the consumer infrastructure, serializer, and RabbitMQ connection.

```csharp
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oragon.RabbitMQ;
using RabbitMQ.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.AddRabbitMQConsumer();
builder.Services.AddAmqpSerializer(options: JsonSerializerOptions.Default);

builder.Services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
{
    Uri = new Uri("amqp://guest:guest@localhost:5672"),
});

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IConnectionFactory>()
        .CreateConnectionAsync()
        .GetAwaiter()
        .GetResult());
```

## Aspire

In .NET Aspire applications, use `Oragon.RabbitMQ.AspireClient` to register `IConnectionFactory`, `IConnection`, and health checks with RabbitMQ.Client 7.x.

```shell
dotnet add package Oragon.RabbitMQ.AspireClient
```

```csharp
builder.AddRabbitMQClient("rabbitmq");
```

{% callout title="Aspire note" %}
The Aspire package exists to cover RabbitMQ.Client 7.x. When official Aspire support reaches that client version, this package can be retired.
{% /callout %}

## Next step

After installation, create the first handler with `MapQueue`.

## Wait for RabbitMQ

For startup flows that need to wait until the broker is reachable before declaring topology or publishing seed messages, call `WaitRabbitMQAsync()`.

```csharp
await app.Services.WaitRabbitMQAsync();
```

When using keyed connection factories, pass the key:

```csharp
await app.Services.WaitRabbitMQAsync("orders");
```
