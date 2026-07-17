---
title: Results and publishing
nextjs:
  metadata:
    title: Results and publishing
    description: Use AMQP results for ack, nack, reply, forward, and requeue-to-tail.
---

AMQP results make the final message action explicit. {% .lead %}

---

## Available results

| Result | Behavior |
| --- | --- |
| `AmqpResults.Ack()` | Acknowledges the current delivery |
| `AmqpResults.Nack(requeue)` | Negative acknowledges the current delivery |
| `AmqpResults.Reject(requeue)` | Rejects the current delivery |
| `AmqpResults.Reply(response)` | Publishes to `ReplyTo` |
| `AmqpResults.Forward(...)` | Publishes a new message |
| `AmqpResults.ForwardAndAck(...)` | Publishes and then acks |
| `AmqpResults.RequeueToTail()` | Republishes the current body to the tail |
| `AmqpResults.Compose(...)` | Executes several results in order |

## Publish channels

`Reply`, `Forward`, and `RequeueToTail` use dedicated publish channels with publisher confirmations enabled. This avoids concurrent use of the consumer channel. `RequeueToTail` does not acknowledge the original delivery by itself.

## Requeue to tail

```csharp
app.MapQueue("orders", (OrderCreated message) =>
{
    if (message.ShouldDelay)
    {
        return AmqpResults.Compose(
            AmqpResults.RequeueToTail(),
            AmqpResults.Ack());
    }

    return AmqpResults.Ack();
});
```

By default `RequeueToTail` republishes a full copy of the original message — same properties (`MessageId`, `Timestamp`, `Expiration`, `AppId`, `ClusterId`, content metadata, delivery mode, priority, correlation id, reply metadata, type) and same headers, including the dead-letter history (`x-death`, `x-first-death-*`, `x-last-death-*`), so the message keeps telling its whole story. Exactly two exceptions apply: `UserId` is not copied, because RabbitMQ validated-user-id requires it to match the publishing connection's user and copying it from another producer fails the publish; and the `x-delivery-count` header is not copied, because it is quorum-queue delivery state that feeds the broker delivery limit and `AmqpRetryPolicy.ByDeliveryCount` — carrying it into a fresh publish would fake failed deliveries. Note that copying `Expiration` preserves the TTL value but the clock restarts when the new copy enters the queue. The result publishes on a dedicated channel with publisher confirmations, so the publish completes only after broker receipt; routing is not verified (`mandatory: false`). It leaves the original delivery for another result, usually `Ack()`, to settle.

Use options when the republished message should copy only selected metadata:

```csharp
return AmqpResults.RequeueToTail(options =>
{
    options.CopyProperties = AmqpPropertyCopy.Priority
                           | AmqpPropertyCopy.Headers
                           | AmqpPropertyCopy.MessageIdentity;
    options.ConfigureProperties = (input, output) =>
    {
        output.AppId = "orders-worker";
    };
});
```

`AmqpPropertyCopy.Headers` copies all headers except `x-delivery-count`, which is broker-owned delivery state rather than message data. Dead-letter history headers (`x-death`, `x-first-death-*`, `x-last-death-*`) are preserved: the broker itself accumulates that history across cycles, and stripping it would erase the message's past.

Use `AmqpPropertyCopy.AllApplicationProperties` when the flow also needs `UserId` — only valid when the publishing connection uses the same user as the original producer (or has the `impersonator` tag).
