---
title: Serialization
nextjs:
  metadata:
    title: Serialization
    description: Configure AMQP serializers in Oragon.RabbitMQ.
---

Oragon.RabbitMQ uses `IAmqpSerializer` to transform the AMQP body into .NET objects and to publish objects in results such as `Forward` and `Reply`. {% .lead %}

---

## System.Text.Json

```shell
dotnet add package Oragon.RabbitMQ.Serializer.SystemTextJson
```

```csharp
using System.Text.Json;
using Oragon.RabbitMQ;

builder.Services.AddAmqpSerializer(options: JsonSerializerOptions.Web);
```

The package also exposes `AddSystemTextJsonAmqpSerializer`.

```csharp
builder.Services.AddSystemTextJsonAmqpSerializer(options: JsonSerializerOptions.Default);
```

## Newtonsoft.Json

```shell
dotnet add package Oragon.RabbitMQ.Serializer.NewtonsoftJson
```

```csharp
using Newtonsoft.Json;
using Oragon.RabbitMQ;

builder.Services.AddNewtonsoftAmqpSerializer(
    options: new JsonSerializerSettings());
```

## Named serializers

Serializer packages accept a key to register keyed serializers in the container.

```csharp
builder.Services.AddSystemTextJsonAmqpSerializer(
    key: "orders",
    options: JsonSerializerOptions.Web);

app.MapQueue("orders", handler)
    .WithSerializer(sp => sp.GetRequiredKeyedService<IAmqpSerializer>("orders"));
```

## Deserialization failures

When a message cannot be deserialized, the default behavior is `Reject(requeue: false)`. Configure a custom policy with `WhenSerializationFail`.

```csharp
app.MapQueue("orders", handler)
    .WhenSerializationFail((ctx, ex) => AmqpResults.Reject(requeue: false));
```
