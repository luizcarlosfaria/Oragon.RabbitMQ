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
| `priority` | `byte`, `int`, `long` | Message priority |
| `deliveryCount`, `attempts` | `int`, `int?`, `long`, `long?` | `x-delivery-count` header |

{% callout title="Fail fast" %}
Numeric parameters without an attribute and without a recognized name fail during startup. This prevents handlers that look valid but cannot be resolved.
{% /callout %}
