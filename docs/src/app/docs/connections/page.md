---
title: Connections and channels
nextjs:
  metadata:
    title: Connections and channels
    description: Configure RabbitMQ connections and channels.
---

Connections are supplied by the application. Channels are usually created by the library. {% .lead %}

---

## Default connection

By default, `ConsumerDescriptor` resolves `IConnection` from DI.

```csharp
builder.Services.AddSingleton<IConnection>(sp =>
    sp.GetRequiredService<IConnectionFactory>()
        .CreateConnectionAsync()
        .GetAwaiter()
        .GetResult());
```

## Custom connection

```csharp
app.MapQueue("orders", handler)
    .WithConnection((services, cancellationToken) =>
        Task.FromResult(services.GetRequiredKeyedService<IConnection>("orders")));
```

## Custom channel

```csharp
app.MapQueue("orders", handler)
    .WithChannel((services, connection, cancellationToken) =>
        connection.CreateChannelAsync(cancellationToken: cancellationToken).AsTask());
```

Dynamic queue consumption can also receive an explicit `IConnection`. If absent, it uses a configured factory, then the current AMQP context connection when available, then DI `IConnection`.
