---
title: Application-owned gates
nextjs:
  metadata:
    title: Application-owned gates
    description: Limit dynamic consumption without library-owned locks.
---

Oragon.RabbitMQ exposes generic gate contracts but does not implement Redis, SQL, lifecycle locks, or domain keys. {% .lead %}

---

## Contract

```csharp
public interface IAmqpConcurrencyGate
{
    ValueTask<IAmqpConcurrencyLease> TryAcquireAsync(
        AmqpConcurrencyGateRequest request,
        CancellationToken cancellationToken);
}
```

The application chooses the key:

```csharp
new AmqpConcurrencyGateRequest(
    Key: $"attention:{type}:{channelId}",
    LeaseTime: TimeSpan.FromMinutes(2),
    Metadata: metadata);
```

Redis may be used in an application or sample, but there is no official Redis package in this milestone.

## Where gates run

The usual place to use a gate is `BeforeStartAsync` on `DynamicQueueConsumeRequest<T>`.

```csharp
BeforeStartAsync = async (context, cancellationToken) =>
{
    var gate = context.Services.GetRequiredService<IAmqpConcurrencyGate>();
    await using var lease = await gate.TryAcquireAsync(
        new AmqpConcurrencyGateRequest(
            Key: $"attention:{type}:{channelId}",
            LeaseTime: TimeSpan.FromSeconds(30),
            Metadata: context.Metadata),
        cancellationToken);

    return lease.Acquired
        ? DynamicQueueStartDecision.Allow()
        : DynamicQueueStartDecision.Defer(TimeSpan.FromSeconds(5));
};
```

The key is always supplied by the application. A key like `attention:{type}:{channelId}` is only a convention used by the application, not a naming rule from Oragon.RabbitMQ.

## What the library does not do

Oragon.RabbitMQ does not create redis locks, does not decide whether a blocked resource should be acked or requeued, and does not renew distributed leases. Those rules depend on business semantics and operational policy.

## Related demo

Run `16-application-gates` from `samples/Demos` to see two dynamic consumers compete for an application-owned key.
