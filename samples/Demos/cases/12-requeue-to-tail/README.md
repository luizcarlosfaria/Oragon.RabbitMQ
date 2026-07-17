# 12 - requeue-to-tail

Status: implemented; broker smoke test pending.

Purpose: demonstrate fair requeue by republishing the current delivery to the queue tail before acknowledging the original delivery.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 12-requeue-to-tail
```

Optional environment:

```bash
AMQP_URI=amqp://guest:guest@localhost:5672/
ORAGON_DEMO_PREFIX=oragon.demo
```

Broker helper:

```bash
docker compose -f samples/Demos/docker-compose.yml up -d
```

Initial conditions:

- RabbitMQ is running.
- Queue `{ORAGON_DEMO_PREFIX}.12.input` is absent or can be safely redeclared.
- The runner declares and purges the queue inside `WithTopology`.
- The runner publishes A, B and C in that order.

Scenarios:

- First delivery of A returns `AmqpResults.RequeueToTail()`.
- `RequeueToTail` republishes A to the same queue before acknowledging the original delivery.
- B and C are then processed before the new copy of A.
- Observed delivery order is `A,B,C,A`.
- By default, the republished copy carries the full original message — same
  properties (`Priority`, `CorrelationId`, `MessageId`, `Timestamp`, `Expiration`,
  `AppId`, `ClusterId`) and same headers, including dead-letter history
  (`x-death`, `x-first-death-*`, `x-last-death-*`) — so the message keeps telling
  its whole story. Exactly two exceptions apply:
  - `UserId` is not copied: RabbitMQ validated-user-id requires it to match the
    publishing connection's user, so copying it from another producer fails the
    publish. Opt in with `AmqpPropertyCopy.UserId` (or `AllApplicationProperties`)
    when the connection user matches.
  - Header `x-delivery-count` is not copied: it is quorum-queue delivery state
    that feeds the broker delivery limit and `AmqpRetryPolicy.ByDeliveryCount`;
    carrying it into a fresh publish would fake failed deliveries.
- `Expiration` caveat: the TTL value is preserved, but the clock restarts when
  the new copy enters the queue (per-message TTL counts from queue entry).
- When a flow needs stricter control, `RequeueToTail` can receive explicit copy options:

```csharp
return AmqpResults.RequeueToTail(options =>
{
    options.CopyProperties = AmqpPropertyCopy.Priority
                           | AmqpPropertyCopy.Headers
                           | AmqpPropertyCopy.MessageIdentity;

    options.ConfigureProperties = (input, output) =>
    {
        output.Expiration = "30000";
    };
});
```

`ConfigureProperties` runs after the selected copy groups are applied. This lets
the application preserve only the fields that matter for its flow and override
or add publish metadata intentionally.

Expected values:

- Delivery order: `A,B,C,A`.
- Final A priority: `7`.
- Final A correlation: `correlation-A`.
- Final A keeps `x-app`, `x-death` and `x-first-death-queue`.
- Final A does not contain `x-delivery-count`.
- Queue ends with `ready=0`.

Comparison with `Nack(requeue:true)`:

- `Nack(requeue:true)` asks RabbitMQ to redeliver the same message and can return it near the head of the queue.
- `RequeueToTail` publishes a new copy to the queue tail and then acks the current delivery.
- `RequeueToTail` is useful when a temporarily blocked item should not monopolize the consumer while later items are ready.
- `RequeueToTail` requires publish success before ack; if publishing fails, the original delivery is not acked by this result.

Acceptance:

- The command exits with code `0`.
- The output contains `Demo 12 succeeded.`
- The output contains `Delivery order: A,B,C,A`.
- README compares behavior with `Nack(requeue:true)`.
- README documents explicit `AmqpPropertyCopy` usage and the full-copy default behavior.
- The final order is visible in logs or assertions.

Behavior added to the demo suite:

- `12-requeue-to-tail` is an executable runner.
- The runner verifies order, full property preservation and the `x-delivery-count` filter.
- The runner demonstrates the fairness primitive used by attention-style flows without adding a `MapAttentionQueue(...)` API.
