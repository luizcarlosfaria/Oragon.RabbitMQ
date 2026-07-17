---
title: Handlers and return types
nextjs:
  metadata:
    title: Handlers and return types
    description: Handler shapes supported by MapQueue.
---

A `MapQueue` handler is a delegate. The dispatcher binds parameters, invokes the delegate, and converts the return value into an AMQP result. {% .lead %}

---

## Common shapes

```csharp
app.MapQueue("orders", (OrderCreated message) => Task.CompletedTask);

app.MapQueue("orders", async ([FromServices] OrderService service, OrderCreated message) =>
{
    await service.HandleAsync(message);
    return AmqpResults.Ack();
});
```

## Return behavior

| Return type | Result |
| --- | --- |
| `void` / `Task` | `Ack()` on success |
| `IAmqpResult` | Executes the returned result |
| `Task<IAmqpResult>` | Awaits and executes the returned result |
| Any object | Treated as a successful handler result and acked |

Use explicit `IAmqpResult` when the handler must nack, reject, reply, forward, or compose several AMQP actions.
