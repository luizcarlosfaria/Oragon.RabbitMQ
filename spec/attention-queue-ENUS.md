# Attention Queue: on-demand processing for granular queues

> Status: conceptual article.
>
> This document explains the Attention Queue pattern, but it is not the implementation contract for the current Oragon.RabbitMQ milestone. The active plan lives in `spec/attention/README.md` and `spec/attention/milestone-roadmap.md`.
>
> Current direction: implement reusable primitives first: `MapQueue` graceful shutdown, dynamic queue consumption, reliable publishing, `RequeueToTail`, stable bindings, and optional application-defined gates. There will be no official Redis package in this milestone; Redis may appear only in examples as a client-owned implementation. The core must allow user-provided implementations through extension points with `IServiceProvider`. Domain locks, lifecycle locks, topology migration, and a high-level `MapAttentionQueue(...)` API are not part of this milestone.

Consume thousands of specific queues without keeping thousands of active consumers.

## The Problem

Imagine an e-commerce platform called MarketHub.

It integrates thousands of stores with marketplaces such as Amazon, eBay, Walmart Marketplace, and other sales channels. Each store needs to synchronize orders, inventory, prices, catalog data, shipping updates, and post-sale events. Most of the time, a small store generates little work. But when a large store performs a massive catalog update or receives a burst of orders during a campaign, it can generate thousands of tasks within minutes.

In MarketHub, each store has its own work queue. In simple terms, a queue is a persistent list of messages waiting to be processed. A producer puts messages into the queue; a consumer reads those messages and performs some action. In this scenario, the producer is MarketHub's own API, not the external integrator.

This decision is not about aesthetic preference for granularity. It exists because different stores have different behaviors, priorities, and risks. A small store should not sit behind thousands of catalog updates from a very large store. A store with an unstable integration should not congest processing for the others. Also, a dedicated queue allows store-specific policies: consumption limits, operational pauses, maintenance, controlled discard, isolated reprocessing, respect for marketplace API limits, and direct observability into that store's backlog.

In other words, a queue per store buys isolation, predictability, and operational control.

But it also creates a new architectural tension: if every store has its own queue, who consumes those queues?

The first solution seems obvious: create permanent consumers for every store queue. A permanent consumer would be a worker registered on that queue all the time, waiting for new messages.

But that creates another problem.

If there are 50,000 stores, it does not make sense to keep 50,000 active consumers, open connections, configured prefetch, and reserved resources for queues that are empty most of the time. The system starts spending compute capacity waiting for work that may not exist.

The real problem is not just processing messages.

The problem is finding an intermediate model between two bad extremes: too many permanent consumers or an overly centralized queue. The system needs to start consumers on demand for specific queues, but only when there is some indication that work is pending in that queue. It also needs to decide how long each temporary consumer should work, how many messages it should try to consume, and how to prevent multiple workers from processing the same queue in an uncontrolled way.

## Considered Alternatives

Several solutions appear quickly.

The first is to use permanent consumers per queue. It is simple to understand, but it scales poorly when there are many granular queues and low average volume per queue.

The second is to use a single central queue for all stores. This reduces the number of consumers, but loses isolation. A noisy store can delay smaller stores, and store-specific policies become harder.

The third is to create grouped centralized queues. Instead of one queue for all stores, the system could create smaller groups, for example `process.marketplace.group_01.work`, `process.marketplace.group_02.work`, and so on. Each group would receive tasks from some stores. This approach looks like a middle ground: it reduces the number of queues and avoids making all stores compete in one global queue.

The problem is that isolation remains partial and accidental. Imagine `group_01` has 100 stores. If one large store inside that group generates 1 million jobs during a campaign, the other 99 stores are stuck behind it or forced to compete with it inside the same queue. The problem is no longer global, but it became local: instead of one store affecting the whole platform, it affects every store in the group.

If group distribution becomes poor, teams need to rebalance stores across queues, which adds operations, migration, and risk. Also, store-specific policies remain difficult: API limits, pausing one integration, commercial priority, and isolated reprocessing need to be rebuilt inside the consumer, because the queue no longer represents a single store. Grouping improves the single-queue approach, but does not provide real isolation per store.

The fourth is to periodically poll all queues. A scheduler scans queues and processes the ones that have messages. It works, but it introduces artificial latency, increases empty calls to the broker, and wastes cycles when many queues are inactive. Here, broker means the messaging server, such as RabbitMQ, responsible for storing and delivering messages.

The fifth is to make the API publish messages directly to specific queues and, along with them, publish a small internal signal saying: this queue needs attention.

That is the solution we will call Attention Queue.

The core idea is to produce two types of messages inside the system. For the external integrator, there is still a single operation: an HTTP call to the API, for example to send an inventory update or synchronization task. After the API receives and validates that request, it internally publishes two messages. The first is the work message: it contains the real data that needs to be processed. The second is the attention message: it is smaller and only serves to tell the system itself that a specific queue has pending demand and needs to be consumed.

| Type | Where it lives | What it contains | What it is for |
| --- | --- | --- | --- |
| Work message | Store-specific queue | Real task payload, such as inventory update, order, or catalog data | To be processed by business logic |
| Attention request | Shared attention queue | Minimal pointers, such as tenant, store, marketplace, and priority | To trigger consumption of the correct work queue |

The real message enters the resource-specific queue. That queue is where the data that actually needs to be processed waits. For example:

```text
process.marketplace.store_873.work
```

This queue name is only a didactic convention. An implementation can choose another format. The essential point is that the attention request has enough data to locate, derive, or query which work queue needs to be consumed.

The signal enters a shared attention queue. This queue does not store the complete work; it only stores notices that some specific queue needs to be inspected. For example:

```text
attention.marketplace.work
```

That signal does not contain the full payload. It contains only what is needed to locate the queue that needs attention, meaning the queue that needs to be consumed: tenant, resource type, resource id, and some routing keys. For a .NET developer, this signal can be seen as a small DTO, serialized as JSON and published internally by the system itself.

With this, the system does not need to keep permanent consumers for every work queue. It keeps consumers on the attention queue. When an attention message arrives, a worker uses that notice to locate the specific queue, starts a temporary consumption cycle, consumes for a limited time or up to a maximum number of messages, and then decides whether the work is done or whether it needs to republish another attention request.

This decision to stop before consuming everything is intentional. The inspiration comes from process scheduling in operating systems, especially the concept of time sharing. The CPU keeps processes ready to run and does not allow a single process to monopolize the processor indefinitely. It gives one process a time slice, interrupts it, and then gives other processes a chance to advance. In Attention Queue, the attention queue plays a similar role to a ready queue: each attention message represents a work queue asking for a processing slice. If there are still messages after that slice, the queue enters the processing competition again through a new attention message.

The pattern is not only trying to be efficient; it is trying to be fair. Each store receives processing slices, preventing a large store from capturing most workers and turning volume into unintended operational privilege.

## How It Works

The pattern works like an internal dispatcher.

It does not process the work directly. It points to where work exists. In a .NET application, think of it as a `BackgroundService` or worker that receives a small command and, from that command, decides which work queue should be consumed.

Instead of constantly asking whether each queue has messages, the system receives a signal when something new arrives. Instead of leaving an eternal consumer sitting on an empty queue, it starts a temporary consumer only when there is a reason to do so.

A typical flow would be:

1. An integrator makes an HTTP call to the MarketHub API requesting a synchronization or sending an update.
2. The API validates the request and internally publishes the task to `process.marketplace.store_873.work`.
3. In the same internal operation, the API publishes a signal to `attention.marketplace.work`.
4. A worker consumes that attention signal.
5. The worker checks the state of the related store or integration.
6. If the store is disabled, under maintenance, blocked due to credential errors, or removed, the attention is discarded.
7. If the store is valid, the worker calculates the consumption limits: maximum time, maximum number of messages, and allowed concurrency.
8. The worker tries to acquire rate-limit permission to avoid too many simultaneous consumers on the same queue. In this context, rate limit is just a capacity gate: how many consumers can work on that queue at the same time. This limit can be 1 when the queue must be consumed serially, or it can be higher, such as 5 or 10, when the domain allows safe parallelism.
9. If allowed, it starts a temporary consumer on the store-specific queue.
10. It processes messages until one of the stop conditions is reached: maximum time, maximum message count, operational error, or empty queue.
11. At the end, it checks whether there is still backlog. If there is, it republishes a new attention request to the attention queue. If not, it stops.
12. The new attention returns to the same shared attention queue and will be consumed in the next cycle by some available worker. It may be the same worker or another one. The point is that one large store's queue does not monopolize processing indefinitely.

Finding the work queue empty is not an error. An attention request can arrive late: when the worker checks the work queue, it may already have been consumed by another cycle. In that case, the worker simply acknowledges the attention and stops.

The important point is that the attention signal is cheap, small, and repeatable.

It does not need to represent exactly one work message. It represents an intention: this queue deserves to be observed and probably needs to be consumed.

For that reason, the attention request must be idempotent. The system must tolerate receiving two or more attention requests for the same store without causing improper duplicate processing. In the worst case, an extra attention starts an attempt that finds the queue empty, is blocked by the concurrency limit, or notices that the backlog was already processed by another cycle.

This changes how we think about the design. The attention queue is not the work queue. It is the coordination queue. It works like a scheduling queue: it decides which work queue receives the next processing slice.

## Complete Example

In MarketHub, each store has its own queue:

```text
process.marketplace.store_{storeId}.work
```

The marketplace integration set has an attention queue:

```text
attention.marketplace.work
```

When store `store-873` needs to synchronize orders or update inventory in a marketplace, the integrator makes a single HTTP call to the API. For the integrator, the operation ends there: it sent the request to MarketHub. The API itself knows that it also needs to generate an attention request.

After receiving the HTTP call, the API performs two internal publications to the broker. This double publication must be treated as one operational unit: it is not enough to publish the work message and hope the attention request is also published. If the first publication succeeds and the second one fails, the store queue may contain pending work with no signal to trigger consumption.

There are several ways to protect this point, depending on the level of guarantee required by the application: use broker publish confirmations, apply the outbox pattern, perform idempotent retry of the attention publication, or keep a periodic reconciliation process that finds queues with backlog and no recent attention. The specific technique may vary, but the architectural decision is the same: the work message and the attention request are part of the same operational intent.

The examples below use three common RabbitMQ terms. The `exchange` is the point where the application publishes the message. The `routingKey` is the key used to decide the message path. The `queue` is where the message stays stored until some consumer processes it.

The first publication carries the real work:

```text
exchange: process.marketplace
routingKey: store.store-873
body: complete synchronization task
```

The second publication carries only the attention request:

```json
{
  "tenantId": "seller-group-a",
  "storeId": "store-873",
  "marketplace": "amazon",
  "priority": "normal"
}
```

The attention worker receives this second event and builds the real queue:

```text
process.marketplace.store_873.work
```

Before consuming, it applies a policy:

```text
maxConsumptionTimeSeconds = 20
maxMessages = 100
maxConcurrentConsumers = 2
```

In this example, `maxConcurrentConsumers = 2` means at most two consumers can process that store queue at the same time. In another scenario, this value could be `1`, guaranteeing a single active consumer per work queue. That is useful when event order matters, when there is risk of conflict in inventory updates, or when the marketplace API requires more controlled calls. If the store has a higher plan, stable integration, and independent operations, the limit could be `10`.

Then it tries to acquire a concurrency token for that store. If enough consumers are already processing that queue, the attention is republished for a future attempt.

If allowed, the worker consumes up to 100 messages or up to 20 seconds. It does not continue until the queue is empty because that would allow one noisy store to occupy the worker for too long. If messages remain, it republishes a new attention request. That new attention returns to `attention.marketplace.work` and will be competed for by the consumers of that attention queue. If the queue is empty, the worker stops.

## Implementation

The pattern can be implemented with five components.

It does not require a generic messaging abstraction. The examples below use common .NET and RabbitMQ vocabulary, but the most important part is the explicit domain contract: a work message, an attention message, a worker that understands that contract, and a clear consumption policy.

The first is the granular work queue. It stores real messages per entity, customer, store, account, or any unit that needs isolation. This is where the payload that business logic will process lives.

The second is the aggregated attention queue. It receives small signals grouped by type. This queue is consumed by a small number of permanent workers.

The third is the attention envelope. It contains the minimum identifiers needed to locate the work queue that needs attention. This envelope must be safe to repeat: publishing or consuming the same request more than once must not corrupt system state. In C#, it could be a simple class, for example:

```csharp
public sealed class AttentionRequest
{
    public required string TenantId { get; init; }
    public required string StoreId { get; init; }
    public required string Marketplace { get; init; }
}
```

The fourth is the attention worker. It validates entity state, starts a temporary consumer, processes a controlled batch, and decides whether more attention is needed. This worker does not need to hide RabbitMQ behind a generic abstraction; it can call explicit broker APIs or internal services.

The fifth is concurrency control. It is usually backed by Redis, a database, or a distributed lock, to prevent multiple workers from processing the same queue beyond the allowed limit. This limit does not need to be greater than 1. In many cases, the correct value is exactly 1 active consumer per work queue. In others, the limit can be 10 or more, as long as processing is independent, idempotent, and safe for parallelism.

In a .NET codebase, these components could appear as domain-specific contracts:

```csharp
public interface IAttentionPublisher
{
    Task PublishAsync(AttentionRequest request, CancellationToken cancellationToken);
}

public interface IAttentionWorker
{
    Task<AttentionResult> ProcessAsync(AttentionRequest request, CancellationToken cancellationToken);
}

public enum AttentionResult
{
    Done,
    NeedMoreAttention
}
```

These interfaces do not need to promise that they work for any broker or every messaging use case. They exist to represent a specific architectural decision: publish attention requests and consume work queues on demand.

One possible pseudocode:

In the pseudocode, `ack attention` means confirming to the broker that the attention message was handled and can leave the queue. If the worker fails before the `ack`, the broker may try to deliver the same attention again, depending on configuration. This is another reason why the attention request must be idempotent. The opposite of `ack` is usually called `nack`, used when the message was not successfully processed and should follow the error or retry policy.

```text
on attention_received(attention):
    resource = load_resource(attention.resource_id)

    if resource cannot receive processing:
        ack attention
        return

    queue_name = build_work_queue_name(resource)

    if rate_limit_blocked(queue_name):
        republish attention
        ack attention
        return

    if queue_does_not_exist(queue_name):
        ack attention
        return

    if queue_is_empty(queue_name):
        ack attention
        return

    consume_until(
        queue = queue_name,
        max_messages = resource.max_messages,
        max_time = resource.max_time
    )

    if queue_has_remaining_messages(queue_name):
        republish attention

    ack attention
```

The `republish attention` step is not recursion and not an immediate call to the same worker. It places a new request at the end of the attention queue. After that, the current worker finishes the cycle and becomes free to pick up the next available request. The broker delivers the new attention when its turn comes, respecting concurrency and the operational order of the queue.

In RabbitMQ, a concrete implementation can use a topology like this. The `binding` is the rule that connects an exchange to a queue.

```text
exchange: attention.marketplace
queue: attention.marketplace.work
binding: store.*

exchange: process.marketplace
queue: process.marketplace.store_873.work
binding: store.store-873
```

The API publishes the full payload to the work queue and publishes a small envelope to the attention queue:

```text
HTTP request from integrator
API validates request
API publishes process event
API publishes attention event
```

In a real implementation, these last two steps must have an explicit consistency strategy. If there is no single transaction covering everything, the application needs confirmations, retry, outbox, or reconciliation to avoid leaving work without attention.

The attention worker, in turn, does not need to know every store ahead of time. It only needs to know how to transform the attention envelope into a queue name, consumption policy, and concurrency-control key.

## Observability

Attention Queue is comfortable to operate only when the system clearly shows where work is stuck, where there is too much attention, and where policy is blocking consumption.

Useful metrics include:

| Metric | What it reveals |
| --- | --- |
| Message count per store queue | Which stores are accumulating backlog |
| Age of the oldest message per queue | How long the most delayed store has been waiting for processing |
| Number of republished attentions | Which queues need many cycles to empty |
| Attentions discarded because the store is disabled or invalid | How much work is being ignored due to operational state |
| Rate-limit blocks | Which stores are hitting concurrency or SLA limits |
| Average time to clear backlog | How long the system takes to recover a queue with pending work |

These metrics help separate different problems. A store may be slow because it has real backlog, because it is limited by plan, because the integration is blocked, because there are too many repeated attentions, or because available workers are not enough. Without these measurements, the pattern still works, but explaining its behavior in production becomes difficult.

## When Not To Use It

Attention Queue should not be treated as the default solution for every asynchronous processing problem.

If the system has few queues, predictable volume, and permanent consumers that are cheap to maintain, the pattern may add unnecessary complexity. If a single queue already works well, with acceptable latency and no isolation problems, there may not be enough pain to justify granular queues and attention requests.

It is also not a good choice when processing must obey a strictly global order across all messages. The pattern favors isolation and fairness across queues, not one single ordering for the whole system.

Another point is operational cost. Dynamic queues require naming conventions, creation, removal, monitoring, and diagnostic capability. If the broker or the team cannot yet operate many queues safely, it is better to mature that foundation before adopting the pattern.

## Benefits

The main benefit of the pattern is aligning consumption with real demand.

It allows the system to maintain thousands or millions of logical queues without requiring thousands or millions of active consumers. The system becomes more elastic because consumers appear when there is backlog and disappear when work is done.

It also improves isolation. A noisy entity does not need to contaminate the flow of others, because each entity can have its own queue, limits, and consumption policy.

Another benefit is operational fairness between stores. The pattern distributes processing in slices and prevents a large store from capturing most workers simply because it has more volume. It can receive more attention if policy allows it, but that becomes an explicit system decision, not a side effect of backlog.

Another benefit is operational governance. Because the attention worker goes through an enrichment step before consuming, it can check state, permissions, locks, maintenance, priority, and limits before spending effort processing messages.

The pattern also opens space for different commercial agreements. Because attention goes through an internal policy before becoming real consumption, the system can treat stores in different plans differently: more simultaneous consumers, larger consumption windows, more messages per cycle, higher priority when republishing attention, or specific rules for campaigns and seasonal dates.

A simple example:

| Plan | Simultaneous consumers per store | Messages per cycle |
| --- | ---: | ---: |
| Basic | 1 | 50 |
| Pro | 3 | 200 |
| Enterprise | 10 | 1000 |

This allows processing capacity to become a commercial SLA without exposing queue complexity to the integrator.

There is also a resilience gain: if processing does not finish in one cycle, attention itself can be republished. Work progresses in slices, as in CPU time sharing. The system does not need to clear the entire backlog at once, and a very full queue does not hold a worker indefinitely.

In summary, the Attention Queue pattern is useful when there are many specific queues, irregular volume, a need for isolation, and a high cost to keep permanent consumers.

It transforms continuous processing into on-demand processing.

The attention queue does not carry the weight of the work. It carries the awareness that work exists.
