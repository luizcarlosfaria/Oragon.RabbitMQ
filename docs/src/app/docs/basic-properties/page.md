---
title: BasicProperties and headers
nextjs:
  metadata:
    title: BasicProperties and headers
    description: Bind AMQP properties and typed headers.
---

AMQP metadata is part of the handler surface. Handlers can receive raw properties, individual properties, priority, attempts, and typed headers. {% .lead %}

---

## Raw properties

```csharp
app.MapQueue("orders", (OrderCreated message, IReadOnlyBasicProperties properties) =>
{
    string correlationId = properties.CorrelationId;
});
```

## Convention bindings

```csharp
app.MapQueue("orders", (
    OrderCreated message,
    string contentType,
    string correlationId,
    string messageId,
    byte? priority,
    long? attempts) =>
{
    return Task.CompletedTask;
});
```

Recognized AMQP property names include `contentType`, `contentEncoding`, `headers`, `deliveryMode`, `priority`, `correlationId`, `replyTo`, `expiration`, `messageId`, `timestamp`, `type`, `messageType`, `userId`, `appId`, and `clusterId`.

Supported convenience types include `DeliveryModes?`, `byte?`, `int?` and `long?` for `deliveryMode`; `byte?`, `int?` and `long?` for `priority`; `string` and `Guid?` for `messageId`; and `AmqpTimestamp?`, `long?` and `DateTimeOffset?` for `timestamp`.

Optional AMQP metadata must be nullable. Non-nullable convention parameters for `priority`, `deliveryMode`, `timestamp`, `deliveryCount`, and `attempts` fail during startup instead of receiving misleading defaults such as `0` or Unix epoch.

## Headers

```csharp
app.MapQueue("orders", (
    [FromAmqpHeader("tenant-id")] string tenantId,
    [FromAmqpHeader("enabled")] bool enabled,
    [FromAmqpHeader("retry-after")] int retryAfter) =>
{
    return Task.CompletedTask;
});
```

`[FromAmqpHeader]` uses `AmqpHeaders.Get(...)` and supports common conversions such as UTF-8 `byte[]` to `string`, numeric values, booleans, nullable types, and enums.

## Attempts

`deliveryCount` and `attempts` bind to RabbitMQ quorum queue header `x-delivery-count`. Classic queues do not set this header, so nullable parameters receive `null`.
