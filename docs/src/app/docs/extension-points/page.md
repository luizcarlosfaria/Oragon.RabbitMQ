---
title: Extension points
nextjs:
  metadata:
    title: Extension points
    description: Customize Oragon.RabbitMQ behavior safely.
---

Extension points are designed to let the application own infrastructure and business decisions. {% .lead %}

---

## Provider-aware hooks

These extension points receive `IServiceProvider` directly or through `IAmqpContext`:

- `WithConnection((services, ct) => ...)`
- `WithChannel((services, connection, ct) => ...)`
- `WithTopology((services, channel, ct) => ...)`
- `WithSerializer(services => ...)`
- `WhenSerializationFail((context, ex) => ...)`
- `WhenProcessFail((context, ex) => ...)`
- `WhenResultExecutionFail((context, ex) => ...)`
- dynamic queue connection/channel factories and start/stop hooks

Use them to resolve keyed services, metrics, application gates, feature flags, and client-owned technology providers.

## Responsibility boundary

The library exposes hooks; the application owns decisions. Domain locks, lifecycle locks, topology migration, tenant validation, rate-limit policy, and provider selection belong in application services resolved through these hooks.

Good extension-point uses:

- choose a keyed RabbitMQ connection for one consumer;
- resolve an application gate before dynamic consumption;
- record metrics after a dynamic queue cycle;
- declare topology through an application-owned topology service;
- choose a serializer for one queue.

Out of scope for the core library:

- Redis provider version management;
- Distributed locks;
- automatic migration to quorum, DLQ, priority or single-active-consumer;
- an opinionated `MapAttentionQueue(...)` API in this milestone.

## Related demos

Use `09-keyed-rabbitmq`, `14-attention-with-primitives`, and `16-application-gates` from `samples/Demos` to see provider-aware hooks in executable code.
