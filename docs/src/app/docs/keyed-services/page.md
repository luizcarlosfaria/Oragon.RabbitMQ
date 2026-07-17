---
title: Serializers and keyed services
nextjs:
  metadata:
    title: Serializers and keyed services
    description: Select serializers, connections, and application providers.
---

Extension points receive `IServiceProvider` where selection needs application context. {% .lead %}

---

## Serializer selection

```csharp
app.MapQueue("orders", handler)
    .WithSerializer(services =>
        services.GetRequiredKeyedService<IAmqpSerializer>("orders-json"));
```

## Connection selection

```csharp
app.MapQueue("attention", handler)
    .WithConnection((services, cancellationToken) =>
        Task.FromResult(services.GetRequiredKeyedService<IConnection>("attention")));
```

## Dynamic queue hooks

`BeforeStartAsync`, `AfterStopAsync`, connection factories, and channel factories in the dynamic consumer expose `IServiceProvider`. Use that to resolve Redis, SQL, feature flags, metrics, or client-owned gates without adding those dependencies to Oragon.RabbitMQ.
