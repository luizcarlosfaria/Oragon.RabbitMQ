# Test Coverage Analyzer — Oragon.RabbitMQ Memory

- [QueueConsumer lifecycle edge-case tests](queueconsumer-lifecycle-tests.md) — init retry (Polly), keyed-DI failures, connection-ownership probe, outer `ReceiveAsync` catch, graceful-shutdown timeouts, `ObjectDisposedException` swallowing, raising `IConnection`'s custom `AsyncEventHandler<T>` events via Moq.
- [DynamicQueueConsumer test patterns](dynamic-queue-consumer-tests.md) — how to capture the internal `AsyncEventingBasicConsumer`, drive deliveries, simulate broker shutdown, force concurrent-delivery races deterministically.
- [RabbitMQ.Client 7.2.1 / Moq 4.20.72 signature notes](rabbitmq-client-moq-signatures.md) — exact method signatures for mocking `IChannel`, `ShutdownEventArgs` ctor overloads, Moq ThrowsAsync/ValueTask behavior.
- [Repo build-strictness quirk](build-strictness.md) — `TreatWarningsAsErrors`/`Nullable` are NOT enabled for the unit test project, only for `src/`.
- [Verifying without building](verifying-without-building.md) — technique used when told not to run `dotnet build`/`test` (parallel agents on same project): reflection scripts against referenced NuGet DLLs.
