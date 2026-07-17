# Changelog

## [1.10.0] - Unreleased

The theme of this release is the **Attention milestone**: a set of small, composable primitives for on-demand queue consumption — a dynamic queue consumer, application-owned concurrency gates, requeue-to-tail, delivery-count-based retry, graceful shutdown and topology helpers — deliberately shipped as building blocks instead of an opinionated `MapAttentionQueue(...)` API. It accumulates everything since `1.8.0` (the `1.9.0-alpha` tag covered only the first convention-binding commit).

### Added

#### Dynamic Queue Consumer ("Attention Queues")

- `IAmqpDynamicQueueConsumer.ConsumeAsync<T>(DynamicQueueConsumeRequest<T>, CancellationToken)` consumes a queue chosen **at runtime** for a bounded window. Registered as a scoped service by `AddRabbitMQConsumer()`.
- `DynamicQueueConsumeRequest<T>` with combinable stop rules — `MaxMessages`, `MaxDuration`, `IdleTimeout`, `StopAfterInitialQueueLength`, `CancellationToken` (at least one is required) — plus `PrefetchCount`, `MaxLocalConcurrency`, `InFlightDrainTimeout`, and `BeforeStartAsync` / `AfterStopAsync` hooks. `BeforeStartAsync` returns a `DynamicQueueStartDecision` (`Allow` / `Skip` / `Defer(delay)` / `Fail(exception)`).
- `DynamicQueueConsumeResult` reports the outcome: status (`Completed`, `Empty`, `Skipped`, `Deferred`, `QueueMissing`, `MaxMessagesReached`, `MaxDurationReached`, `IdleTimeoutReached`, `InitialQueueLengthReached`, `Interrupted`, `Faulted`), initial/remaining ready counts, ack/nack/reject counters, elapsed time and drain flags.
- Connection resolution cascade: explicit `Connection` → `ConnectionFactory` → ambient connection of the currently executing handler (via `IAmqpContextAccessor`) → `IConnection` from DI.

#### Application-owned concurrency gates

- `IAmqpConcurrencyGate` / `IAmqpConcurrencyLease` / `AmqpConcurrencyGateRequest`: a generic gate contract for distributed concurrency control. The library ships **no implementation and no DI registration by design** — the application picks the store (Redis, SQL, in-process lock), the key and the lease semantics. Typical usage is inside `BeforeStartAsync`.

#### Flow control results

- `AmqpResults.RequeueToTail()` / `RequeueToTail(queueName)` / `RequeueToTail(configure)`: republishes the current message to the **tail** of the same (or another) queue through a dedicated channel with publisher confirms. Property copying is configurable via `RequeueToTailOptions` and the `AmqpPropertyCopy` flags; the `x-delivery-count` header is always stripped and `UserId` is not copied by default.
- `AmqpResults.LeaveUnsettled()`: leaves the delivery unsettled (no ack/nack/reject) for manual or deferred settlement.
- `AmqpRetryPolicy.ByDeliveryCount(maxAttempts)`: retry policy driven by the `x-delivery-count` header of quorum queues — `Reject(requeue: true)` below the limit, `Nack(requeue: false)` (dead-letter) at the limit.

#### Model binding

- Convention-based binding of AMQP metadata by parameter name and type: `priority` (`byte?`/`int?`/`long?`), `deliveryMode` (`DeliveryModes?`/`byte?`/`int?`/`long?`), `timestamp` (`DateTimeOffset?`/`long?`/`AmqpTimestamp?`), `deliveryCount`/`attempts` (`int?`/`long?`, from `x-delivery-count`), `messageId` (`string`/`Guid?`), `contentType`, `contentEncoding`, `correlationId`, `replyTo`, `expiration`, `type`/`messageType`, `userId`, `appId` and `clusterId` (`string`).
- Type-based binding for message headers (`IDictionary<string, object>` / `IReadOnlyDictionary<string, object>`) and `AmqpTimestamp?`.
- Fail-fast validation at startup: optional AMQP metadata declared with a non-nullable value type throws `InvalidOperationException` when the consumer pipeline is built.
- `[FromAmqpHeader]` is now typed: any parameter type is supported through the new `AmqpHeaders` conversion helper (enums, `byte[]` ↔ UTF-8 string, nullables). A missing header on a non-nullable value type throws; nullable/reference types receive `null`.

#### Lifecycle

- `WithGracefulShutdown(options)` on the consumer descriptor: cooperative shutdown with `CancelContextTokenOnStop`, `WaitForInFlightMessages` and `DrainTimeout` (default 30s). Disabled by default — existing stop behavior is preserved.
- `WhenResultExecutionFail(handler)`: dedicated failure policy for errors thrown while **executing a result** (ack/reply/forward/requeue). Default: `Nack(requeue: false)`.
- `IAmqpContextAccessor` (`AsyncLocal`-based, similar to `IHttpContextAccessor`), registered as a singleton; exposes the current `IAmqpContext` during handler execution.

#### Topology

- `QueueArguments`: fluent builder for queue `x-arguments` — `Quorum()`, `SingleActiveConsumer()`, `WithDeadLetter(exchange, routingKey?)`, `WithMaxPriority(n)`.
- `QueueArgumentDiagnostics.Compare(expected, actual)`: non-destructive diff of queue arguments (tolerates numeric-type and `string`/`byte[]` representation differences).

#### Configuration

- New `WithChannel` / `WithTopology` overloads receiving `IServiceProvider` (`Func<IServiceProvider, IConnection, CancellationToken, Task<IChannel>>` and `Func<IServiceProvider, IChannel, CancellationToken, Task>`). The previous overloads remain available.

#### Documentation & samples

- New documentation site (Next.js 14 + Markdoc + Tailwind CSS) with ~44 pages across 8 sections, covering all new features (`docs/`).
- New executable demo suite `samples/Demos` with 17 cases (own solution, Docker Compose RabbitMQ, `DemoHost` CLI with `list`/`describe`/run) and the `samples/Attention.Primitives` composition sample.

### Changed

- `ReplyResult` and `ForwardResult` now publish through a dedicated channel with **publisher confirms + tracking** enabled.
- All built-in results (`Ack`, `Nack`, `Reject`, `Reply`, `Forward`) now propagate `IAmqpContext.CancellationToken` to the broker operations.
- When graceful shutdown is enabled, result execution runs under a dedicated cancellation token bounded by `DrainTimeout`, giving settlement a grace window after stop.
- `QueueConsumer.DisposeAsync` hardened against `ObjectDisposedException` during connection close/detach.
- Dependency updates: Aspire 13.4.6, Microsoft.Extensions.* 9.0.17 / 10.0.9, OpenTelemetry 1.16.0, Polly 8.7.0, Testcontainers 4.13.0, Microsoft.CodeAnalysis.PublicApiAnalyzers 5.6.0, RabbitMQ.Client.OpenTelemetry 1.0.0-rc.2. `RabbitMQ.Client` stays at 7.2.1.

### Breaking Changes

- **`ConsumerDescriptor.ChannelFactory` and `TopologyInitializer` changed type** — both now take `IServiceProvider` as the first parameter. Since `IConsumerDescriptor` is source-generated from the class, the interface changed accordingly. The fluent `WithChannel(...)` / `WithTopology(...)` overloads without `IServiceProvider` were kept, so typical fluent configuration is unaffected; only code reading/assigning these properties directly (or implementing the interface manually) needs adjustment.
- **Handler parameters of type `DeliveryModes` (non-nullable) are no longer supported** — they now throw `InvalidOperationException` at startup. Use `DeliveryModes?` instead (optional AMQP metadata requires nullable types).
- **`ReplyResult` no longer publishes with `mandatory: true`** — replies are published with `mandatory: false` on a confirming channel. Delivery to the broker is guaranteed by publisher confirms; routing the reply to the `replyTo` queue is the requester's responsibility (an unroutable reply is no longer surfaced as an error).

[1.10.0]: https://github.com/luizcarlosfaria/Oragon.RabbitMQ/compare/1.8.0...release/1.10

## Version 1.0 Milestones

- [x] Migrate Demo to Library Project
- [x] Core: Queue Consumer
- [x] Core: Rpc Queue Consumer
- [x] Core: Support Keyed Services
- [x] Core: Support of new design of RabbitMQ.Client
- [x] Create Samples
- [x] Review All SuppressMessageAttribute
- [x] Create Docs
- [x] Benchmarks
- [x] Automate Badges
- [x] Add SonarCloud
- [x] Code Coverage > 80%
- [X] Add CI/CD
- [x] Add Unit Tests
- [x] Add Integrated Tests with TestContainers
- [x] Test CI/CD Flow: MyGet Alpha Packages with Symbols
- [x] Test CI/CD Flow: MyGet Packages without Symbols
- [x] Test CI/CD Flow: Nuget Packages without Symbols
- [x] Change original behavior based on lambda expressions to dynamic delegate.
