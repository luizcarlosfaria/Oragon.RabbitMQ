# 05 - rpc-request-reply

Status: implemented.

Purpose: demonstrate a complete request/reply flow with a client-created reply queue, correlation validation and bounded client timeout.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 05-rpc-request-reply
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
- The RPC server queue is absent or can be safely redeclared.
- The runner declares and purges `{ORAGON_DEMO_PREFIX}.05.server` inside `WithTopology`.
- The client creates an exclusive, auto-delete reply queue.

Scenarios:

- Client publishes request payload `RpcRequest(Left=13, Right=29)`.
- Client sets:
  - `ReplyTo=<exclusive reply queue>`;
  - `CorrelationId=rpc-<guid>`;
  - no `MessageId`, so `ReplyResult` uses the original `CorrelationId`.
- Consumer resolves `RpcCalculatorService` using `[FromServices]`.
- Consumer returns `AmqpResults.ReplyAndAck(new RpcResponse(42))`.
- Client polls the reply queue with a bounded timeout.
- Client validates response payload and response `CorrelationId`.
- Client runs a second wait for a missing correlation id and verifies the timeout returns `null`.

Important behavior:

- `ReplyResult` publishes to the original `ReplyTo`.
- `ReplyResult` sets response `CorrelationId` to `MessageId ?? CorrelationId` from the request.
- `ReplyAndAck` executes reply publish before acking the original message.
- A client that wants response correlation to equal request `CorrelationId` should either omit `MessageId` or set `MessageId` to the same value.

Expected values:

- Request: `Left=13`, `Right=29`.
- Response: `Result=42`.
- Server queue after processing: `ready=0`.
- Timeout probe: no message, returns `null`.

Acceptance:

- The command exits with code `0`.
- The output contains `Demo 05 succeeded.`
- The output contains `Response result: 42`.
- The response correlation equals the request correlation id.
- The timeout probe prints `Timeout returned null: True`.
- README shows request and response payloads.
- The correlation id is validated in code.

Behavior added to the demo suite:

- `05-rpc-request-reply` is an executable runner.
- The runner documents the exact correlation rule implemented by `ReplyResult`.
- The runner demonstrates a finite client wait, avoiding examples that can hang forever.
