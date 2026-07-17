---
title: Model binding
nextjs:
  metadata:
    title: Model binding
    description: Understand how handler parameters are resolved.
---

`MapQueue` handlers can receive the message, services, AMQP metadata, and infrastructure objects directly as parameters. {% .lead %}

---

## Attributes

| Attribute | Source |
| --- | --- |
| `[FromServices]` | DI container, including keyed services |
| `[FromBody]` | Deserialized message body |
| `[FromAmqpHeader("key")]` | AMQP header by name |

```csharp
app.MapQueue("orders", (
    [FromServices] OrderService svc,
    [FromBody] OrderCreated body,
    [FromAmqpHeader("tenant-id")] string tenantId) =>
{
    return svc.HandleAsync(tenantId, body);
});
```

## Auto-bound types

Some types are resolved automatically without an attribute.

| Type | Value |
| --- | --- |
| `IConnection` | Current RabbitMQ connection |
| `IChannel` | Current RabbitMQ channel |
| `BasicDeliverEventArgs` | Raw delivery event |
| `IReadOnlyBasicProperties` | Message properties |
| `IDictionary<string, object>` | Message headers |
| `IReadOnlyDictionary<string, object>` | Message headers |
| `DeliveryModes?` | AMQP delivery mode |
| `AmqpTimestamp?` | AMQP timestamp |
| `IServiceProvider` | Message service scope |
| `IAmqpContext` | Full AMQP context |
| `CancellationToken` | Cancellation token |

## Name conventions

Parameters can also be resolved by name and type.

| Names | Types | Value |
| --- | --- | --- |
| `queue`, `queueName` | `string` | Consumed queue name |
| `routing`, `routingKey` | `string` | Routing key |
| `exchange`, `exchangeName` | `string` | Source exchange |
| `consumer`, `consumerTag` | `string` | Consumer tag |
| `priority` | `byte?`, `int?`, `long?` | Message priority |
| `deliveryCount`, `attempts` | `int?`, `long?` | `x-delivery-count` header |
| `contentType` | `string` | AMQP content type |
| `contentEncoding` | `string` | AMQP content encoding |
| `headers` | `IDictionary<string, object>`, `IReadOnlyDictionary<string, object>` | AMQP headers |
| `deliveryMode` | `DeliveryModes?`, `byte?`, `int?`, `long?` | AMQP delivery mode |
| `correlationId` | `string` | AMQP correlation id |
| `replyTo` | `string` | AMQP reply-to |
| `expiration` | `string` | AMQP expiration |
| `messageId` | `string`, `Guid?` | AMQP message id |
| `timestamp` | `AmqpTimestamp?`, `long?`, `DateTimeOffset?` | AMQP timestamp |
| `type`, `messageType` | `string` | AMQP type |
| `userId` | `string` | AMQP user id |
| `appId` | `string` | AMQP app id |
| `clusterId` | `string` | AMQP cluster id |

{% callout title="Fail fast" %}
Numeric parameters without an attribute and without a recognized name fail during startup. Optional AMQP metadata such as `priority`, `deliveryMode`, `timestamp`, `deliveryCount`, and `attempts` must use nullable types.
{% /callout %}

## Typed headers

`[FromAmqpHeader]` can bind strings, booleans, numeric types, nullable types, byte arrays, and enums when the header value can be converted.

```csharp
app.MapQueue("orders", (
    [FromAmqpHeader("tenant-id")] string tenantId,
    [FromAmqpHeader("enabled")] bool enabled,
    [FromAmqpHeader("attempt")] int? attempt) =>
{
    return Task.CompletedTask;
});
```
