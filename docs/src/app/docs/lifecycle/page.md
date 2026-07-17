---
title: Lifecycle and shutdown
nextjs:
  metadata:
    title: Lifecycle and shutdown
    description: Understand startup, stop, and graceful shutdown behavior.
---

Consumer lifecycle is tied to the .NET host. Graceful shutdown is opt-in. {% .lead %}

---

## Startup

On startup, the consumer resolves connection and serializer services, creates a channel, runs optional topology initialization, checks queue availability, and starts consuming.

The consumer validates handler bindings during startup. Missing services, ambiguous message parameters, unsupported MVC binding attributes, or invalid descriptor configuration fail early instead of surfacing as silent message loss.

## Graceful shutdown

```csharp
app.MapQueue("orders", handler)
    .WithGracefulShutdown(options =>
    {
        options.CancelContextTokenOnStop = true;
        options.WaitForInFlightMessages = true;
        options.DrainTimeout = TimeSpan.FromSeconds(30);
    });
```

`CancelContextTokenOnStop` cancels the token exposed through `IAmqpContext`. `WaitForInFlightMessages` waits for active deliveries to finish. `DrainTimeout` limits `BasicCancelAsync` and drain wait.

When graceful shutdown is enabled, Oragon.RabbitMQ uses an internal shutdown token for broker cancel and drain operations. That token is bounded by `DrainTimeout` and is not already canceled just because the host stop token was canceled before `StopAsync` reached the consumer.

The canceled context token is a cooperative signal for the handler. If the handler observes that signal and still returns a terminal `IAmqpResult`, Oragon.RabbitMQ executes that result with a separate operation token bounded by `DrainTimeout`. This lets `Ack`, `Reject`, `Forward`, or `RequeueToTail` complete broker operations during the drain window.

With all options enabled, shutdown follows this order:

1. stop accepting new deliveries by canceling the broker consumer;
2. cancel the handler context token;
3. let terminal results complete broker operations inside the drain window;
4. wait for in-flight handlers to produce terminal results;
5. continue shutdown when all in-flight work finishes or `DrainTimeout` expires.

If the timeout expires, Oragon.RabbitMQ logs the timeout and continues shutdown. It does not kill handler tasks and does not invent an `Ack` or `Nack` for a message that is still running.

## Default behavior

Without `WithGracefulShutdown(...)`, stop keeps the historical behavior: cancel the broker consumer and return according to the host stop token.

## Ownership

When `WithConnection(...)` returns an application-owned singleton connection from DI, the consumer detaches event handlers but does not own the application lifecycle. When a descriptor creates its own connection, that descriptor is responsible for closing and disposing it.

Use `WithChannel((services, connection, ct) => ...)` when the application needs custom channel options, client-owned telemetry, or provider-specific setup before consumption starts.

## Related demo

Run `11-graceful-shutdown` from `samples/Demos` to inspect cooperative token cancellation and in-flight drain behavior.
