# 06 - concurrency-prefetch

Status: implemented.

Purpose: show how `WithPrefetch` and `WithDispatchConcurrency` affect throughput, in-flight work and observed ordering.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 06-concurrency-prefetch
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
- Queues `{ORAGON_DEMO_PREFIX}.06.sequential` and `{ORAGON_DEMO_PREFIX}.06.parallel` are absent or can be safely redeclared.
- The runner declares and purges both queues inside `WithTopology`.

Scenarios:

- Sequential queue:
  - `WithPrefetch(1)`;
  - `WithDispatchConcurrency(1)`;
  - four messages with decreasing simulated work time;
  - expected max concurrency is `1`;
  - expected finish order remains `1,2,3,4`.
- Parallel queue:
  - `WithPrefetch(8)`;
  - `WithDispatchConcurrency(4)`;
  - four messages with decreasing simulated work time;
  - expected max concurrency is at least `2`;
  - expected finish order differs from `1,2,3,4`.

Expected output shape:

- Sequential start order: `1,2,3,4`.
- Sequential finish order: `1,2,3,4`.
- Sequential max concurrency: `1`.
- Parallel max concurrency: `>= 2`.
- Parallel finish order is intentionally not guaranteed.
- Both queues end with `ready=0`.

Acceptance:

- The command exits with code `0`.
- The output contains `Demo 06 succeeded.`
- The output shows sequential max concurrency equal to `1`.
- The output shows parallel max concurrency greater than or equal to `2`.
- README explains ordering tradeoffs.
- README gives practical guidance for I/O-bound and CPU-bound handlers.

Practical guidance:

- Use `WithPrefetch(1)` and `WithDispatchConcurrency(1)` when strict per-consumer ordering is more important than throughput.
- Increase `WithPrefetch` when the consumer can safely hold more unacked messages.
- Increase `WithDispatchConcurrency` for I/O-bound handlers that spend time waiting on databases, HTTP calls or storage.
- Be conservative for CPU-bound handlers; high dispatch concurrency can move bottlenecks to the thread pool or CPU.
- When concurrency is greater than `1`, handler code and injected services must be thread/concurrency safe.
- Higher concurrency changes completion order, even when delivery/start order looks ordered.

Behavior added to the demo suite:

- `06-concurrency-prefetch` is an executable runner.
- The runner measures actual observed concurrency instead of only printing configuration.
- The runner makes the ordering tradeoff visible through start and finish order logs.
