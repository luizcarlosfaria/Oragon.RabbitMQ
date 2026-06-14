---
title: Benchmarks
nextjs:
  metadata:
    title: Benchmarks
    description: Oragon.RabbitMQ performance results.
---

The benchmarks compare Oragon.RabbitMQ against hand-written native RabbitMQ.Client code running the same DI scoping, serialization, try/catch, and ack/nack logic. {% .lead %}

---

## Environment

- AMD Ryzen 9 9950X3D, 16 cores and 32 threads.
- .NET 9.0.12.
- Windows 11.
- Server GC enabled.
- BenchmarkDotNet v0.14.0.

## Performance summary

| Benchmark | Scenario | Oragon overhead | Verdict |
| --- | --- | --- | --- |
| Concurrency Scaling | I/O-bound, 1000 messages with `Task.Delay` | 0 - 1% | Excellent |
| Concurrency Scaling | CPU-bound, 1000 messages with hash loop | 2 - 8% | Very good |
| Throughput | NoOp handler, 1000-5000 messages | 0 - 11% | Good |
| Throughput | CPU-bound handler | 0 - 14% | Good |
| Latency | Single message | 5 - 7%, about 3.5 ms fixed | Good |
| Allocation | Large messages | 9% time, 1% memory | Excellent |
| RPC | `ReplyAndAck` against native dedicated | -7%, Oragon wins | Excellent |

## Concurrency scaling

1000 messages with a `Task.Delay(5)` handler, representative of I/O-bound workloads.

| Prefetch | Concurrency | Native (ms) | Oragon (ms) | Ratio |
| --- | --- | --- | --- | --- |
| 10 | 2 | 2,700 | 2,706 | 1.00 |
| 10 | 4 | 1,380 | 1,390 | 1.01 |
| 10 | 8 | 728 | 731 | 1.00 |
| 50 | 4 | 1,351 | 1,357 | 1.00 |
| 50 | 8 | 694 | 694 | 1.00 |
| 100 | 4 | 1,347 | 1,352 | 1.00 |
| 100 | 8 | 677 | 681 | 1.01 |

## RPC

| Size | Native dedicated (ms) | Oragon `ReplyAndAck` (ms) | Ratio |
| --- | --- | --- | --- |
| Small | 50.1 | 46.8 | 0.93 |
| Medium | 50.4 | 47.1 | 0.93 |

## Practical reading

For I/O-bound workloads, measured overhead is effectively zero. For small CPU-bound workloads, the cost comes from DI scoping, the dispatch pipeline, and pluggable serialization. In production, this fixed cost tends to become irrelevant as the handler or payload gets larger.
