# 17 - publish-consistency-work-attention

Status: runner implemented; broker smoke test pending.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 17-publish-consistency-work-attention
```

Objective: publish work and attention with explicit confirms.

Initial conditions:

- RabbitMQ is running.
- Work queue and attention queue are declared.
- Publish channel has publisher confirmations enabled.
- `AMQP_URI` points to the broker, or defaults to
  `amqp://guest:guest@localhost:5672/`.
- Queues named `{ORAGON_DEMO_PREFIX}.17.work` and
  `{ORAGON_DEMO_PREFIX}.17.attention` can be declared and purged.
- The runner uses a channel created by `RabbitMqDemoClient.CreatePublishChannelAsync`,
  with `publisherConfirmationsEnabled=true` and
  `publisherConfirmationTrackingEnabled=true`.

Scenarios:

- Publish work with `mandatory=true` and wait for confirm.
- Publish attention with `mandatory=true` and wait for confirm.
- If work routing fails, attention is not published.
- If attention fails after work confirm, application reconciliation or outbox is required.
- Happy path:
  - publish work to `{ORAGON_DEMO_PREFIX}.17.work`;
  - await publisher confirm;
  - publish attention to `{ORAGON_DEMO_PREFIX}.17.attention`;
  - await publisher confirm.
- Work routing failure:
  - publish work to a missing queue through the default exchange with
    `mandatory=true`;
  - RabbitMQ.Client raises `PublishException`;
  - attention publish is skipped.
- Attention routing failure:
  - publish work successfully;
  - publish attention to a missing queue with `mandatory=true`;
  - RabbitMQ.Client raises `PublishException`;
  - demo publishes a compensating/reconciled attention message to the real
    attention queue.

Acceptance:

- README explains the limit of atomicity between broker and database.
- Code makes publish order explicit.
- Happy path confirms both messages.
- Work routing failure confirms no attention was attempted.
- Attention routing failure leaves confirmed work and then records a reconciled
  attention signal.
- Final work queue has `ready=2`: happy path work plus work whose first
  attention publish failed.
- Final attention queue has `ready=2`: happy path attention plus reconciled
  attention.
- The runner exits with code `0`.

Atomicity guidance:

- Publisher confirmations prove that RabbitMQ accepted or returned a publish on
  that channel; they do not create an atomic transaction with an application
  database.
- `publish work -> confirm -> publish attention -> confirm` prevents publishing
  attention for work that was not accepted by the broker.
- If the process crashes after work confirm and before attention confirm, the
  application needs an outbox, retry journal or reconciliation process.
- The library should provide reliable publishing primitives and should not own
  business outbox semantics.

Approval:

```bash
dotnet build samples/Demos/Oragon.RabbitMQ.Demos.slnx
dotnet run --project samples/Demos/src/DemoHost -- list
dotnet run --project samples/Demos/src/DemoHost -- 17-publish-consistency-work-attention
```

Current verification:

- Build/list can be verified without Docker.
- Broker smoke test is pending while Docker is unavailable in the current WSL
  session.
