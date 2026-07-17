# 07 - serializers

Status: implemented.

Purpose: compare serializer registration paths and show keyed serializer selection per queue descriptor.

Command:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 07-serializers
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
- Queues `{ORAGON_DEMO_PREFIX}.07.system-text-json` and `{ORAGON_DEMO_PREFIX}.07.newtonsoft` are absent or can be safely redeclared.
- The runner declares and purges both queues inside `WithTopology`.

Scenarios:

- Register keyed System.Text.Json serializer:
  - key: `stj`;
  - options: `PropertyNameCaseInsensitive = true`;
  - payload uses camelCase JSON: `{"source":"system-text-json","value":7}`.
- Register keyed Newtonsoft.Json serializer:
  - key: `newtonsoft`;
  - options: `StringEnumConverter`;
  - payload uses enum as string: `{"Source":"newtonsoft","Mode":"Fast"}`.
- Select the serializer per queue using:
  - `WithSerializer(services => services.GetRequiredKeyedService<IAmqpSerializer>("stj"))`;
  - `WithSerializer(services => services.GetRequiredKeyedService<IAmqpSerializer>("newtonsoft"))`.

Expected values:

- System.Text.Json handler receives `Source=system-text-json`, `Value=7`.
- Newtonsoft.Json handler receives `Source=newtonsoft`, `Mode=Fast`.
- Both queues end with `ready=0`.

Avoiding extension ambiguity:

- Both serializer packages expose an `AddAmqpSerializer(...)` convenience extension.
- When both packages are referenced in the same project, prefer explicit names:
  - `AddSystemTextJsonAmqpSerializer(...)`;
  - `AddNewtonsoftAmqpSerializer(...)`.
- Use keyed registrations when different queues need different serializer behavior in the same application.

Acceptance:

- The command exits with code `0`.
- The output contains `Demo 07 succeeded.`
- The output shows `System.Text.Json value: 7`.
- The output shows `Newtonsoft.Json mode: Fast`.
- README explains how to avoid `AddAmqpSerializer` extension ambiguity.
- Both serializer paths are present in compiling code.

Behavior added to the demo suite:

- `07-serializers` is an executable runner.
- The runner references both serializer packages in compiling code.
- The runner demonstrates descriptor-level serializer selection rather than global serializer replacement.
