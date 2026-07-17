# 04 - flow-control-results

Status: implemented.

Purpose: demonstrate `IAmqpResult` behavior, acknowledgement semantics and publish-before-ack composition.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 04-flow-control-results
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
- Input, forward, reply and DLQ queues are absent or can be safely redeclared.
- The runner declares and purges all case queues inside `WithTopology` before consumption starts.

Scenarios:

- `ack`: handler returns `AmqpResults.Ack()`, removing the original message.
- `nack`: handler returns `AmqpResults.Nack(false)`, routing the message to the DLQ through the queue dead-letter configuration.
- `reject`: handler returns `AmqpResults.Reject(false)`, routing the message to the DLQ.
- `forward`: handler returns `AmqpResults.ForwardAndAck(...)`, publishing one message to `{ORAGON_DEMO_PREFIX}.04.forward` before acking the original.
- `reply`: handler returns `AmqpResults.ReplyAndAck(...)`, publishing one message to `{ORAGON_DEMO_PREFIX}.04.reply` using the original `ReplyTo`.
- `result-fail`: handler returns `ReplyAndAck` without `ReplyTo`; `ReplyResult` fails, `WhenResultExecutionFail` runs, and the fallback result rejects the original to the DLQ.

Expected broker state after processing:

- `{ORAGON_DEMO_PREFIX}.04.input`: `ready=0`.
- `{ORAGON_DEMO_PREFIX}.04.forward`: `ready=1`.
- `{ORAGON_DEMO_PREFIX}.04.reply`: `ready=1`.
- `{ORAGON_DEMO_PREFIX}.04.dlq`: `ready=3`.

Expected metadata:

- The forwarded message has `CorrelationId=flow-forward`.
- The reply message has `CorrelationId=flow-reply`.
- `WhenResultExecutionFail` is observed exactly once.

Acceptance:

- The command exits with code `0`.
- The output contains `Demo 04 succeeded.`
- The output contains `Handled messages: 6`.
- The output contains `Result execution failures: 1`.
- README explains expected broker state after each scenario.
- Publish-before-ack ordering is explicit for results that publish.
- The runner verifies forwarded and replied message correlation IDs.

Behavior added to the demo suite:

- `04-flow-control-results` is an executable runner.
- The runner demonstrates that publish results are composed before `Ack` by `ForwardAndAck` and `ReplyAndAck`.
- The runner demonstrates the result-execution failure policy separately from handler failure policy.
