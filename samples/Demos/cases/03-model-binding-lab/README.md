# 03 - model-binding-lab

Status: implemented.

Purpose: exercise the supported AMQP model binding surface in one executable case.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 03-model-binding-lab
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
- Queue `{ORAGON_DEMO_PREFIX}.03.input` and exchange `{ORAGON_DEMO_PREFIX}.03.exchange` are absent or can be safely redeclared.
- The runner declares and purges the queue inside `WithTopology`.
- The demo publisher sends the BasicProperties and headers required by the handler.

Scenarios:

- Resolve `[FromBody] ModelBindingLabMessage`.
- Resolve `[FromServices] ModelBindingProbeService`.
- Resolve `[FromServices("keyed-service")] ModelBindingProbeService`.
- Resolve `[FromAmqpHeader]` values:
  - `x-string` as `string`;
  - `x-number` as `int`;
  - `x-enabled` as `bool`.
- Resolve infrastructure types:
  - `IConnection`;
  - `IChannel`;
  - `BasicDeliverEventArgs`;
  - `IReadOnlyBasicProperties`;
  - `IServiceProvider`;
  - `IAmqpContext`;
  - `CancellationToken`;
  - `DeliveryModes`;
  - `IDictionary<string, object>`;
  - `AmqpTimestamp`.
- Resolve routing metadata by convention:
  - `queueName`;
  - `exchangeName`;
  - `routingKey`;
  - `consumerTag`.
- Resolve numeric metadata by convention:
  - `priority`;
  - `deliveryCount`;
  - `attempts`.
- Resolve BasicProperties fields by convention:
  - `contentType`;
  - `contentEncoding`;
  - `correlationId`;
  - `replyTo`;
  - `expiration`;
  - `messageId`;
  - `type`;
  - `userId`;
  - `appId`;
  - `clusterId`.

Expected values:

- Body: `Id=binding-message`, `Value=42`.
- Default service: `default-service`.
- Keyed service: `keyed-service`.
- Headers: `x-string=header-value`, `x-number=123`, `x-enabled=true`, `x-delivery-count=3`.
- `deliveryMode=Persistent`.
- `timestamp=1700000000`.
- `exchangeName={ORAGON_DEMO_PREFIX}.03.exchange`.
- `queueName={ORAGON_DEMO_PREFIX}.03.input`.
- `routingKey=bindings`.
- `consumerTag=oragon-demo-03-model-binding-lab`.
- `priority=7`.
- `deliveryCount=3`.
- `attempts=3`.
- `contentType=application/json`.
- `contentEncoding=utf-8`.
- `correlationId=binding-correlation-id`.
- `replyTo=binding-reply-to`.
- `expiration=60000`.
- `messageId=binding-message-id`.
- `type=model-binding-lab`.
- `userId` matches the user from `AMQP_URI`, defaulting to `guest`.
- `appId=oragon-rabbitmq-demos`.
- `clusterId=oragon-demo-cluster`.

Acceptance:

- The command exits with code `0`.
- The output contains `Demo 03 succeeded.`
- The output contains `Binding failures: 0`.
- The queue ends with `ready=0`.
- The README lists expected values for every bound parameter.
- The demo fails clearly if a binder cannot be resolved.
- The handlers do not use ASP.NET MVC binding attributes.
- The runner uses only attributes from `Oragon.RabbitMQ.Consumer.Dispatch.Attributes`.

Behavior added to the demo suite:

- `03-model-binding-lab` is an executable runner.
- The runner turns model binding into a self-checking contract example.
- The runner documents the difference between AMQP binding attributes and prohibited ASP.NET MVC binding attributes.
