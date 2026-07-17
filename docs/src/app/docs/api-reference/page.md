---
title: API reference
nextjs:
  metadata:
    title: API reference
    description: Main Oragon.RabbitMQ APIs.
---

This page summarizes the main APIs. Use XML docs and source for the complete reference. {% .lead %}

---

| API | Purpose |
| --- | --- |
| `AddRabbitMQConsumer()` | Registers consumer hosting |
| `MapQueue(...)` | Maps a queue to a delegate |
| `ConsumerDescriptor` | Configures a mapped consumer |
| `AmqpResults` | Creates ACK/NACK/reply/forward results |
| `RequeueToTailOptions` | Controls target queue and copied properties for requeue-to-tail |
| `AmqpPropertyCopy` | Selects AMQP property groups copied during publish/requeue |
| `IAmqpDynamicQueueConsumer` | Consumes runtime-selected queues |
| `AmqpHeaders` | Reads typed AMQP headers |
| `QueueArguments` | Builds common queue arguments |
| `QueueArgumentDiagnostics` | Compares expected and actual queue arguments |
| `IAmqpConcurrencyGate` | Generic application-owned gate contract |

## Property copy groups

`AmqpPropertyCopy.RequeueToTailDefault` is a full copy of the original message for `RequeueToTail` — all properties and headers, including dead-letter history — except `UserId` (RabbitMQ validated-user-id would fail the publish on a different connection user) and the `x-delivery-count` header (broker-owned quorum-queue delivery state). Use `AllApplicationProperties` to also copy `UserId`, or narrower flag combinations to reset parts of the message deliberately.
