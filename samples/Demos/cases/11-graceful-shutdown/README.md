# 11 - graceful-shutdown

Status: implemented; broker smoke test pending.

Purpose: demonstrate cooperative shutdown, in-flight drain and drain timeout behavior for `MapQueue`.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 11-graceful-shutdown
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
- Queues `{ORAGON_DEMO_PREFIX}.11.cooperative` and `{ORAGON_DEMO_PREFIX}.11.timeout` are absent or can be safely redeclared.
- Both consumers are configured with `WithGracefulShutdown`.
- The runner declares and purges both queues inside `WithTopology`.

Scenarios:

- Cooperative path:
  - handler receives `CancellationToken`;
  - handler starts a long delay with that token;
  - runner calls `host.StopAsync()`;
  - `CancelContextTokenOnStop=true` cancels the handler token;
  - handler exits before `DrainTimeout`;
  - drain completes.
- Timeout path:
  - handler intentionally ignores the token by using `CancellationToken.None`;
  - runner calls `host.StopAsync()`;
  - `WaitForInFlightMessages=true` waits only until `DrainTimeout`;
  - `StopAsync` returns while the handler is still running;
  - handler completes afterward and the queue ends empty.

Configured options:

```csharp
.WithGracefulShutdown(options =>
{
    options.CancelContextTokenOnStop = true;
    options.WaitForInFlightMessages = true;
    options.DrainTimeout = TimeSpan.FromSeconds(2);
});
```

The timeout scenario uses the same flags with `DrainTimeout = 200ms`.

Acceptance:

- The command exits with code `0`.
- The output contains `Demo 11 succeeded.`
- The output contains `Cooperative canceled: True`.
- The output contains `Timeout handler was still running after StopAsync: True`.
- Both queues end with `ready=0`.
- README shows how to trigger shutdown.
- Logs show drain completed and timeout paths.

Manual Ctrl+C/SIGTERM check:

1. Start the broker with the compose command above.
2. Run the case command.
3. While handlers are active, press Ctrl+C or send SIGTERM to the process.
4. Confirm the cooperative handler observes cancellation and the timeout path logs a drain timeout.

Behavior added to the demo suite:

- `11-graceful-shutdown` is an executable runner.
- The runner turns graceful shutdown into an automated `StopAsync` scenario instead of requiring only manual Ctrl+C.
- The runner documents that the library cancels and waits, but does not force-complete a handler that ignores cancellation.
