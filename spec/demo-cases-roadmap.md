# Roadmap de cases e projetos de demonstracao

Este roadmap separa os projetos de demonstracao do milestone tecnico de
attention/primitives.

O objetivo e transformar os cases listados em `spec/docs-roadmap.md` em uma
suite de exemplos executaveis, verificaveis e navegavel pela documentacao.

## Status atual da implementacao

Atualizado em 2026-06-14.

- `samples/Demos` existe com `DemoHost`, `DemoShared`, `docker-compose.yml`,
  `Oragon.RabbitMQ.Demos.slnx` e README por case.
- Cases 01-07, 09 e 10 tem runners implementados e foram smoke-tested contra
  RabbitMQ local nesta iteracao de trabalho.
- Case 08 tem runner de verificacao de origem: ele valida que
  `samples/Aspire` contem AppHost, API, Worker, Web, `AddRabbitMQClient`,
  `AddRabbitMQConsumer`, `MapQueue` e instrumentacao RabbitMQ. O smoke real do
  AppHost continua dependente de Aspire/Docker.
- Cases 11-17 tem runners implementados e foram smoke-tested contra RabbitMQ
  local nesta iteracao de trabalho.
- O case 12 documenta `RequeueToTail` com cópia integral por default (exceto
  `UserId` e o header `x-delivery-count`) e controle explícito de cópia via
  `AmqpPropertyCopy`.
- Validacoes executadas sem Docker:
  - `dotnet build samples/Demos/Oragon.RabbitMQ.Demos.slnx --no-restore`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- list`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 08-aspire-worker`;
  - `dotnet test tests/Oragon.RabbitMQ.UnitTests/Oragon.RabbitMQ.UnitTests.csproj --no-restore`;
  - `dotnet build Oragon.RabbitMQ.slnx --no-restore`;
  - `yarn build` em `docs/`.
- Validacoes executadas com RabbitMQ local em `localhost:5672`:
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 01-minimal-consumer`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 02-standalone-topology-dlq`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 03-model-binding-lab`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 04-flow-control-results`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 05-rpc-request-reply`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 06-concurrency-prefetch`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 07-serializers`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 09-keyed-rabbitmq`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 10-retry-quorum-delivery-count`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 11-graceful-shutdown`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 12-requeue-to-tail`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 13-dynamic-queue-consumer`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 14-attention-with-primitives`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 15-observability-dashboard`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 16-application-gates`;
  - `dotnet run --project samples/Demos/src/DemoHost --no-build --no-restore -- 17-publish-consistency-work-attention`.
- Validacao integrada com Testcontainers executada:
  - `dotnet test tests/Oragon.RabbitMQ.IntegratedTests/Oragon.RabbitMQ.IntegratedTests.csproj --no-restore --filter FullyQualifiedName~AttentionPrimitivesIntegratedTests`;
  - resultado: 6 testes passaram em `net9.0` e 6 testes passaram em `net10.0`.
- A suite unitaria validada nesta iteracao passou com 196 testes em `net9.0`
  e 196 testes em `net10.0` naquela iteracao (208 por TFM apos as revisoes de
  2026-07-05), incluindo regressao para garantir que graceful shutdown vincula
  o token do host ao token de drain e
  regressoes para o default de copia integral de `RequeueToTail`, o cancelamento
  cooperativo do handler no consumer dinamico quando `MaxDuration` e atingido
  a separacao entre token do handler e token de execucao de `IAmqpResult`
  durante graceful shutdown, e a independencia entre `MaxMessages` e snapshot
  inicial vazio no consumer dinamico.
- `dotnet build Oragon.RabbitMQ.slnx --no-restore` passa, mas reporta
  `NU1903` para `MessagePack 2.5.192` via `samples/Aspire/DotNetAspireApp.AppHost`.
- Smoke pendente quando Docker voltar:
  - `docker compose -f samples/Demos/docker-compose.yml up -d`;
  - `dotnet run --project samples/Aspire/DotNetAspireApp.AppHost`.

## Motivacao

O roadmap tecnico da library entrega APIs, comportamento e testes. Os demos
tem outro objetivo: ensinar usos reais, reduzir duvidas do dia-a-dia e provar
que a documentacao esta fiel a API publica.

Manter esses cases em um roadmap separado evita que a evolucao da library fique
bloqueada por volume de material didatico, mas ainda preserva a exigencia de
ter exemplos substanciais.

## Decisoes

1. Os demos devem ser tratados como uma suite de aprendizado, nao como samples
   soltos.
2. Cada demo deve ter README proprio com objetivo, comandos, topologia,
   mensagens de teste e resultado esperado.
3. Os snippets principais da documentacao devem existir em algum demo ou teste.
4. Redis nao deve virar dependencia da library. Quando aparecer em demo, sera
   dependencia do proprio demo ou substituivel por provider fake/in-memory.
5. Demos de attention devem usar `MapQueue`, `IAmqpDynamicQueueConsumer`,
   hooks e gates genericos. Nao devem introduzir `MapAttentionQueue(...)`.
6. Demos de topologia podem mostrar SAC/quorum/DLQ quando a fila nasce com
   esses argumentos, mas nao devem prometer migracao automatica de filas.
7. Os demos devem compilar em CI. Demos que dependem de broker devem ter
   verificacao manual ou teste integrado separado.

## Estrutura recomendada

```text
samples/
  Demos/
    README.md
    docker-compose.yml
    Oragon.RabbitMQ.Demos.slnx
    src/
      DemoHost/
      DemoShared/
    cases/
      01-minimal-consumer/
      02-standalone-topology-dlq/
      ...
      17-publish-consistency-work-attention/
```

### Modelo de execucao

Preferir um host compartilhado com comandos por case:

```bash
dotnet run --project samples/Demos/src/DemoHost -- 01-minimal-consumer
dotnet run --project samples/Demos/src/DemoHost -- 13-dynamic-queue-consumer
```

Cada case ainda deve ter README proprio. O host compartilhado evita 17 copias
de setup de DI, conexao, serializer e publicacao.

Se um case exigir uma aplicacao isolada, ele pode ganhar projeto proprio, mas
isso deve ser excecao.

## Fases

### Fase 0: infraestrutura da suite

Entregas:

- `samples/Demos/README.md` como indice oficial;
- `docker-compose.yml` com RabbitMQ management;
- `DemoShared` com helpers de conexao, publish, declaracao e waits;
- `DemoHost` com roteamento por comando;
- convencao de nomes de filas e exchanges;
- comando `list` para listar todos os cases;
- build da suite em CI ou em comando documentado.

Aceite:

- `dotnet build samples/Demos/Oragon.RabbitMQ.Demos.slnx`;
- `docker compose -f samples/Demos/docker-compose.yml up -d`;
- `dotnet run --project samples/Demos/src/DemoHost -- list`.

### Fase 1: demos de uso basico e features atuais

Esta fase cobre a superficie que ja existia ou que e basica para qualquer
usuario.

#### Demo 01: `minimal-consumer`

Objetivo: primeiro consumer funcional.

Cobre:

- `AddRabbitMQConsumer`;
- serializer System.Text.Json;
- `IConnection` singleton;
- `MapQueue`;
- `Ack` default.

Aceite:

- publica uma mensagem;
- handler processa;
- fila fica sem mensagens ready/unacked.

#### Demo 02: `standalone-topology-dlq`

Objetivo: app standalone declarando topologia propria.

Cobre:

- `WithTopology`;
- queue declare;
- exchange declare;
- DLQ;
- `WhenSerializationFail`;
- `WhenProcessFail`;
- `WaitRabbitMQAsync`.

Aceite:

- mensagem invalida vai para DLQ;
- excecao no handler vai para DLQ;
- README mostra como inspecionar no RabbitMQ Management.

#### Demo 03: `model-binding-lab`

Objetivo: demonstrar todos os binders relevantes.

Cobre:

- `[FromServices]`;
- keyed services;
- `[FromBody]`;
- `[FromAmqpHeader]` tipado;
- `IConnection`;
- `IChannel`;
- `BasicDeliverEventArgs`;
- `IReadOnlyBasicProperties`;
- `IServiceProvider`;
- `IAmqpContext`;
- `CancellationToken`;
- `DeliveryModes`;
- `queueName`, `routingKey`, `exchangeName`, `consumerTag`;
- `priority`;
- `deliveryCount/attempts`;
- BasicProperties por convencao.

Aceite:

- cada binder aparece em handler pequeno;
- publicador do demo envia properties/headers necessarios;
- README lista valor esperado de cada parametro.

#### Demo 04: `flow-control-results`

Objetivo: demonstrar resultados e semantica de ack.

Cobre:

- `Ack`;
- `Nack`;
- `Reject`;
- `Reply`;
- `ReplyAndAck`;
- `Forward`;
- `ForwardAndAck`;
- `Compose`;
- `WhenResultExecutionFail`;
- configuracao de `BasicProperties` no `Forward`.

Aceite:

- cada result tem uma fila de entrada ou um modo de execucao;
- README explica o estado esperado no broker depois de cada publish.

#### Demo 05: `rpc-request-reply`

Objetivo: request/reply completo.

Cobre:

- `ReplyTo`;
- `CorrelationId`;
- `Reply`;
- `ReplyAndAck`;
- timeout no cliente;
- correlacao da response.

Aceite:

- client envia request;
- consumer responde;
- client recebe response com correlation correta.

#### Demo 06: `concurrency-prefetch`

Objetivo: demonstrar relacao entre prefetch e dispatch concurrency.

Cobre:

- `WithPrefetch`;
- `WithDispatchConcurrency`;
- ordering;
- idempotencia;
- I/O-bound versus CPU-bound.

Aceite:

- modo concorrencia 1 preserva ordem observada;
- modo concorrencia maior mostra paralelismo e possivel mudanca de ordem.

#### Demo 07: `serializers`

Objetivo: comparar serializers.

Cobre:

- `AddSystemTextJsonAmqpSerializer`;
- `AddNewtonsoftAmqpSerializer`;
- keyed `IAmqpSerializer`;
- `WithSerializer`.

Aceite:

- duas filas usam serializers diferentes;
- payload com casing/conversores demonstra diferenca.

#### Demo 08: `aspire-worker`

Objetivo: sample Aspire oficial e limpo.

Cobre:

- `AddRabbitMQClient`;
- health checks;
- tracing;
- AppHost;
- replicas de worker;
- `ClientProvidedName`.

Aceite:

- roda pelo AppHost;
- RabbitMQ aparece no dashboard Aspire;
- health check fica healthy;
- workers consomem mensagens.

#### Demo 09: `keyed-rabbitmq`

Objetivo: multiplas conexoes RabbitMQ.

Cobre:

- `AddKeyedRabbitMQClient`;
- keyed `IConnection`;
- `WithConnection`;
- keyed serializer opcional.

Aceite:

- duas filas consomem de conexoes ou virtual hosts diferentes;
- README explica quando usar.

#### Demo 10: `retry-quorum-delivery-count`

Objetivo: retry com quorum queue e `x-delivery-count`.

Cobre:

- quorum queue;
- DLQ;
- `deliveryCount/attempts`;
- `Reject(requeue:true)` versus terminal;
- poison message.

Aceite:

- mensagem falha N vezes;
- depois vai para DLQ;
- logs mostram attempts.

### Fase 2: demos das primitives de attention

Esta fase so deve ser considerada completa depois que as primitives tecnicas
estiverem implementadas no pacote principal.

#### Demo 11: `graceful-shutdown`

Objetivo: demonstrar shutdown cooperativo.

Cobre:

- `WithGracefulShutdown`;
- token cancelado;
- in-flight drain;
- timeout;
- handler que respeita token;
- handler que ignora token.

Aceite:

- SIGTERM ou Ctrl+C para o app;
- logs mostram `BasicCancel`, drain completo ou timeout.

#### Demo 12: `requeue-to-tail`

Objetivo: demonstrar fairness de republicacao no fim.

Cobre:

- `RequeueToTail`;
- prioridade preservada;
- correlation preservada;
- headers de broker removidos;
- diferenca para `Nack(requeue:true)`.

Aceite:

- publica A, B, C;
- A e reprogramada para o fim;
- ordem observada comprova A, B, C, A.

#### Demo 13: `dynamic-queue-consumer`

Objetivo: demonstrar consumo dinamico sob demanda.

Cobre:

- `IAmqpDynamicQueueConsumer`;
- fila definida em runtime;
- `MaxMessages`;
- `MaxDuration`;
- `IdleTimeout`;
- `StopAfterInitialQueueLength`;
- `BeforeStartAsync`;
- `AfterStopAsync`;
- status e contadores.

Aceite:

- cada regra de parada tem um cenario;
- combinacao de regras encerra na primeira atingida;
- fila ausente e vazia nao sao erro fatal;
- idle nao encerra enquanto houver mensagem in-flight.

#### Demo 14: `attention-with-primitives`

Objetivo: demonstrar o padrao completo sem API opinativa.

Cobre:

- fila de trabalho granular;
- fila de atencao agregada;
- gate por recurso com chave definida pela aplicacao;
- consumo dinamico sob demanda;
- `RequeueToTail`;
- retry por attempts;
- graceful shutdown;
- topologia com quorum/DLQ/SAC quando criada desde o inicio;
- ausencia de `MapAttentionQueue(...)`.

Aceite:

- duas entidades pequenas e uma ruidosa;
- processamento justo por fatias;
- entidade ruidosa nao monopoliza workers;
- shutdown gera nova atencao quando interrompe;
- README separa responsabilidades da library e da aplicacao.

### Fase 3: demos operacionais e extensibilidade

#### Demo 15: `observability-dashboard`

Objetivo: demonstrar operacao.

Cobre:

- logs estruturados;
- health checks;
- tracing Aspire;
- RabbitMQ Management;
- DLQ inspection;
- connection blocked/unblocked quando possivel simular.

Aceite:

- README orienta quais sinais observar;
- falhas simuladas aparecem em logs, health ou DLQ.

#### Demo 16: `application-gates`

Objetivo: demonstrar gates distribuidos extensveis sem semantica de dominio
dentro da library.

Cobre:

- `IAmqpConcurrencyGate`;
- implementacao in-memory para execucao local;
- exemplo opcional com Redis como dependencia do demo, nao da library;
- chave informada pela aplicacao;
- TTL/lease;
- `BeforeStartAsync`;
- `IServiceProvider` nos hooks;
- `RequeueToTail` quando bloqueado;
- nenhuma dependencia de lock `channel-lifecycle`.

Aceite:

- dois workers tentam consumir a mesma fila dinamica;
- apenas um adquire o lease;
- o segundo reprograma a atencao ou encerra conforme politica do sample;
- README mostra onde a aplicacao monta a chave.

#### Demo 17: `publish-consistency-work-attention`

Objetivo: demonstrar publicacao de work e attention com garantias explicitas.

Cobre:

- channel com publisher confirmations;
- `mandatory`;
- ordem `publish work -> confirm -> publish attention -> confirm`;
- falha antes de ack;
- reconciliacao simples feita pela aplicacao;
- outbox como recomendacao quando houver banco de dados.

Aceite:

- cenario feliz publica work e attention;
- falha de routing e detectada;
- README explica quando usar outbox fora da library.

## Ordem recomendada de implementacao

1. Fase 0: infraestrutura da suite.
2. Demos 01, 02 e 03 para cobrir onboarding e binding.
3. Demos 04, 05, 06 e 07 para cobrir resultados, RPC, concorrencia e serializer.
4. Demos 08 e 09 para Aspire/keyed connections.
5. Demo 10 para retry/quorum.
6. Demos 11, 12 e 13 para primitives tecnicas novas.
7. Demo 14 para o fluxo attention completo.
8. Demos 15, 16 e 17 para operacao, extensibilidade e consistencia.

## Gates de verificacao

Cada fase deve ter evidencias objetivas:

- build dos projetos de demo;
- README de cada case;
- comandos de execucao testados manualmente ou por teste integrado;
- links do website apontando para os cases;
- snippets centrais duplicados em testes ou codigo compilavel.

Comandos minimos esperados quando a suite existir:

```bash
dotnet build samples/Demos/Oragon.RabbitMQ.Demos.slnx
yarn --cwd docs build
```

Demos que usam RabbitMQ devem documentar tambem:

```bash
docker compose -f samples/Demos/docker-compose.yml up -d
```

## Relacao com os outros roadmaps

- `spec/attention/milestone-roadmap.md` continua sendo o roadmap tecnico da
  library.
- `spec/docs-roadmap.md` continua sendo o roadmap do website.
- Este arquivo e o roadmap de cases executaveis e deve ser usado para quebrar
  a implementacao dos demos em milestones menores.

## Fora de escopo

- Criar pacote Redis oficial.
- Implementar `MapAttentionQueue(...)`.
- Migrar filas existentes para SAC/quorum/DLQ.
- Prometer atomicidade entre banco de dados e RabbitMQ sem outbox da aplicacao.

## Requisitos gerais rastreaveis

Estes requisitos valem para todos os demos. Eles existem para que um novo
contexto consiga auditar o trabalho sem depender do historico da conversa.

| ID | Requisito | Evidencia obrigatoria |
| --- | --- | --- |
| DEMO-GEN-001 | O roadmap de demos deve ser autocontido. | Este arquivo descreve objetivo, fases, requisitos, cenarios, condicoes iniciais e aceite. |
| DEMO-GEN-002 | Cada demo deve ter README proprio. | `samples/Demos/cases/<id>/README.md`. |
| DEMO-GEN-003 | Cada demo deve ter comando executavel documentado. | README contem `dotnet run --project ... -- <case-id>`. |
| DEMO-GEN-004 | Cada demo deve declarar ou documentar sua topologia. | README e/ou codigo do case declara filas, exchanges, bindings e argumentos. |
| DEMO-GEN-005 | A suite deve compilar sem broker. | `dotnet build samples/Demos/Oragon.RabbitMQ.Demos.slnx`. |
| DEMO-GEN-006 | Demos que precisam de broker devem usar RabbitMQ local reproduzivel. | `samples/Demos/docker-compose.yml` e README com comando. |
| DEMO-GEN-007 | Demos nao devem adicionar dependencias ao core da library. | `src/Oragon.RabbitMQ*` nao referencia Redis ou providers de demo. |
| DEMO-GEN-008 | Redis, quando existir, deve ser dependencia do demo ou substituivel por fake/in-memory. | Projeto do demo ou README mostra provider in-memory e Redis opcional. |
| DEMO-GEN-009 | Demos de attention nao devem usar `MapAttentionQueue(...)`. | Codigo usa `MapQueue` + `IAmqpDynamicQueueConsumer`. |
| DEMO-GEN-010 | Demos nao devem prometer migracao automatica SAC/quorum/DLQ. | README separa topologia nova de migracao de filas existentes. |
| DEMO-GEN-011 | Os exemplos devem usar a API real da branch corrente. | Build da suite e snippets compilaveis. |
| DEMO-GEN-012 | O website deve apontar para os demos. | `docs/src/app/docs/samples/page.md` e paginas especificas linkam os cases. |
| DEMO-GEN-013 | Cada demo deve ter validacao observavel. | README descreve logs, estado da fila, response ou DLQ esperada. |
| DEMO-GEN-014 | Demos que deixam mensagens no broker devem ter cleanup documentado. | README inclui comando de purge/delete ou usa prefixo isolado. |
| DEMO-GEN-015 | A aprovacao de fase deve registrar comandos executados. | Resultado final da fase lista build, smoke tests e docs build. |

## Condicoes iniciais globais

Estas condicoes devem ser verdadeiras antes de validar qualquer fase.

### Ambiente local

- Repositorio em `/mnt/p/_dev/Oragon.RabbitMQ`.
- SDK .NET capaz de compilar `net9.0` e `net10.0`.
- Node/Yarn disponivel para build do website em `docs/`.
- Docker disponivel quando o demo exigir RabbitMQ real.
- Portas locais esperadas para RabbitMQ:
  - AMQP: `5672`;
  - management UI: `15672`.
- Variavel opcional:

```bash
export AMQP_URI=amqp://guest:guest@localhost:5672/
```

Se `AMQP_URI` nao for definida, os demos devem usar esse valor default.

### Estado do broker

- Os demos nao devem depender de filas criadas manualmente antes da execucao.
- Cada demo deve declarar sua propria topologia ou documentar claramente quando
  usa topologia externa.
- Nomes de filas/exchanges devem usar prefixo estavel:

```text
oragon.demo.<case-id>.<resource>
```

- O prefixo pode ser sobrescrito por variavel:

```bash
export ORAGON_DEMO_PREFIX=my-local-test
```

- Cada README deve explicar como limpar recursos do case.

### Estado da library

- O pacote principal deve compilar antes dos demos:

```bash
dotnet build Oragon.RabbitMQ.slnx --no-restore
```

- Os demos devem usar `ProjectReference` para os projetos da branch corrente,
  nao pacotes NuGet publicados, salvo quando o objetivo do demo for mostrar
  consumo externo da versao publicada.

### Estado da documentacao

- O website deve compilar depois que os links dos demos forem adicionados:

```bash
yarn --cwd docs build
```

## Cenarios de teste por demo

Cada demo deve ter pelo menos os cenarios abaixo. Quando um cenario nao for
automatizado, o README deve explicar a verificacao manual.

### Demo 01: `minimal-consumer`

Condicoes iniciais:

- RabbitMQ local ativo.
- Fila `oragon.demo.01.input` ausente ou vazia.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D01-S01 | O host esta configurado com `AddRabbitMQConsumer`, serializer e `MapQueue`. | Uma mensagem JSON valida e publicada. | O handler recebe a mensagem e o resultado default e `Ack`. |
| D01-S02 | A mensagem foi processada. | A fila e inspecionada. | `ready=0` e `unacked=0`. |

Aceite:

- comando do README processa uma mensagem fim a fim;
- logs mostram o payload recebido;
- nao ha configuracao avancada escondida.

### Demo 02: `standalone-topology-dlq`

Condicoes iniciais:

- RabbitMQ local ativo.
- Exchanges/filas do case ausentes ou removidas.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D02-S01 | `WithTopology` declara main queue, DLX e DLQ. | O host inicia. | A topologia existe sem operacao manual. |
| D02-S02 | `WhenSerializationFail` retorna resultado terminal para DLQ. | Payload invalido e publicado. | Mensagem vai para DLQ. |
| D02-S03 | `WhenProcessFail` retorna resultado terminal para DLQ. | Handler lanca excecao. | Mensagem vai para DLQ. |

Aceite:

- README mostra como publicar payload valido e invalido;
- README mostra como conferir DLQ no management UI;
- nenhuma fila precisa ser criada fora do app.

### Demo 03: `model-binding-lab`

Condicoes iniciais:

- RabbitMQ local ativo.
- Fila de binding declarada pelo demo.
- Mensagem publicada com headers e BasicProperties conhecidas.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D03-S01 | Handler possui um parametro de body e varios parametros de contexto. | Mensagem valida e entregue. | Todos os binders por tipo sao resolvidos. |
| D03-S02 | Mensagem possui `priority`, `correlationId`, `messageId` e headers. | Handler roda. | BasicProperties e headers sao recebidos pelos parametros esperados. |
| D03-S03 | Header numerico/string/bool esta presente. | `[FromAmqpHeader]` e usado. | Conversao tipada funciona ou erro esperado e documentado. |
| D03-S04 | Handler usa keyed service. | Handler roda. | Service correto e resolvido pelo DI. |

Aceite:

- README lista cada parametro e valor esperado;
- demo falha de forma clara se algum binder nao resolver;
- nao usa atributos MVC.

### Demo 04: `flow-control-results`

Condicoes iniciais:

- RabbitMQ local ativo.
- Filas para ack/nack/reject/reply/forward declaradas.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D04-S01 | Handler retorna `Ack`. | Mensagem e publicada. | Mensagem sai da fila. |
| D04-S02 | Handler retorna `Nack(false)`. | Mensagem e publicada. | Mensagem e descartada ou vai para DLQ conforme topologia. |
| D04-S03 | Handler retorna `Reject(false)`. | Mensagem e publicada. | Mensagem e descartada ou vai para DLQ conforme topologia. |
| D04-S04 | Handler retorna `ForwardAndAck`. | Mensagem e publicada. | Mensagem aparece na fila destino e original e acked. |
| D04-S05 | Handler retorna `ReplyAndAck`. | Request com `ReplyTo` e publicado. | Response chega na fila de reply. |
| D04-S06 | Result customizado falha. | `WhenResultExecutionFail` esta configurado. | Politica configurada e aplicada. |

Aceite:

- README explica estado esperado de cada fila;
- publish-before-ack fica documentado nos results que publicam.

### Demo 05: `rpc-request-reply`

Condicoes iniciais:

- RabbitMQ local ativo.
- Fila RPC declarada.
- Cliente cria fila temporaria de reply.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D05-S01 | Request possui `ReplyTo` e `CorrelationId`. | Consumer responde com `ReplyAndAck`. | Cliente recebe response com mesma correlation. |
| D05-S02 | Consumer nao responde no tempo esperado. | Cliente aguarda ate timeout. | Timeout e reportado sem travar o processo. |

Aceite:

- README mostra request e response;
- correlation id e validada no codigo.

### Demo 06: `concurrency-prefetch`

Condicoes iniciais:

- RabbitMQ local ativo.
- Fila do case vazia.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D06-S01 | `WithPrefetch(1)` e `WithDispatchConcurrency(1)`. | Oito mensagens sao publicadas. | Processamento observado e sequencial. |
| D06-S02 | `WithPrefetch(8)` e `WithDispatchConcurrency(4)`. | Oito mensagens sao publicadas. | Logs mostram paralelismo e possivel mudanca de ordem. |

Aceite:

- README explica que concorrencia pode quebrar ordering;
- README orienta escolha para I/O-bound e CPU-bound.

### Demo 07: `serializers`

Condicoes iniciais:

- RabbitMQ local ativo.
- Duas filas declaradas, uma por serializer.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D07-S01 | Consumer usa System.Text.Json default. | Payload compativel e publicado. | Handler recebe body corretamente. |
| D07-S02 | Consumer usa Newtonsoft keyed via `WithSerializer`. | Payload compativel e publicado. | Handler recebe body corretamente. |
| D07-S03 | Payload explora casing/conversor. | Mensagem e processada. | Diferenca entre serializers e demonstrada ou documentada. |

Aceite:

- README mostra como registrar os dois serializers sem ambiguidade;
- keyed serializer aparece em codigo compilavel.

### Demo 08: `aspire-worker`

Condicoes iniciais:

- .NET Aspire disponivel.
- RabbitMQ resource configurado pelo AppHost.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D08-S01 | AppHost sobe RabbitMQ, API, Worker e Web. | AppHost executa. | Dashboard mostra recursos healthy. |
| D08-S02 | Worker usa `AddRabbitMQClient`. | Mensagem e publicada. | Worker consome usando connection gerenciada pelo Aspire client. |
| D08-S03 | Tracing/health estao habilitados. | Consumer processa mensagem. | Sinais aparecem no dashboard/logs. |

Aceite:

- README aponta para `samples/Aspire`;
- comandos de execucao e validacao estao documentados.

### Demo 09: `keyed-rabbitmq`

Condicoes iniciais:

- RabbitMQ local ativo com dois virtual hosts ou duas connections simuladas.
- Connections keyed registradas.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D09-S01 | Consumer A usa connection keyed `primary`. | Mensagem e publicada na fila A. | Consumer A processa. |
| D09-S02 | Consumer B usa connection keyed `secondary`. | Mensagem e publicada na fila B. | Consumer B processa. |
| D09-S03 | Key incorreta e configurada. | Host inicia. | Erro e claro e documentado. |

Aceite:

- README explica quando keyed connection e util;
- `WithConnection` aparece em codigo compilavel.

### Demo 10: `retry-quorum-delivery-count`

Condicoes iniciais:

- RabbitMQ local ativo.
- Queue principal e quorum.
- DLQ configurada.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D10-S01 | Handler falha e usa `Reject(requeue:true)`. | Mensagem e redelivered. | `deliveryCount/attempts` aumenta. |
| D10-S02 | Attempts fica abaixo do limite. | Handler decide retry. | Mensagem volta para a fila. |
| D10-S03 | Attempts atinge limite. | Handler decide terminal. | Mensagem vai para DLQ. |

Aceite:

- README explica dependencia de quorum queue para `x-delivery-count`;
- logs mostram attempts.

### Demo 11: `graceful-shutdown`

Condicoes iniciais:

- RabbitMQ local ativo.
- Consumer configurado com `WithGracefulShutdown`.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D11-S01 | Handler recebe `CancellationToken`. | Host recebe SIGTERM/Ctrl+C. | Token do contexto e cancelado. |
| D11-S02 | Handler termina antes de `DrainTimeout`. | Stop e chamado. | Drain completo e logado. |
| D11-S03 | Handler ignora token e excede timeout. | Stop e chamado. | Timeout de drain e logado sem ack inventado pela library. |

Aceite:

- README orienta como simular SIGTERM;
- logs de drain completo/timeout sao observaveis.

### Demo 12: `requeue-to-tail`

Condicoes iniciais:

- RabbitMQ local ativo.
- Fila contem mensagens A, B e C.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D12-S01 | Handler recebe A e retorna `Compose(RequeueToTail(), Ack())`. | A e republicada antes do ack original. | Ordem observada fica A, B, C, A. |
| D12-S02 | A possui correlation/priority/headers. | `RequeueToTail` executa. | A copia integral e preservada (incluindo `x-death`) e o header `x-delivery-count` e removido. |
| D12-S03 | Publish nao recebe confirm do broker. | Publish falha antes do `Ack` composto. | Original nao e acked sem confirmacao de publish. |
| D12-S04 | Flow precisa controlar metadados republicados. | Handler usa `RequeueToTail(options => options.CopyProperties = ...)`. | Apenas os grupos selecionados sao copiados e `ConfigureProperties` pode sobrescrever campos. |

Aceite:

- README compara com `Nack(requeue:true)`;
- README documenta a copia integral por default e a copia explicita por `AmqpPropertyCopy`;
- teste ou comando prova a ordem;
- teste unitario cobre copy policy, o filtro de `x-delivery-count` e override via `ConfigureProperties`.

### Demo 13: `dynamic-queue-consumer`

Condicoes iniciais:

- RabbitMQ local ativo.
- Filas dinamicas criadas pelo demo.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D13-S01 | Request usa `MaxMessages`. | Fila tem mais mensagens que o limite. | Consumer para em `MaxMessagesReached` e deixa restante ready. |
| D13-S02 | Request usa `MaxDuration`. | Handler demora mais que a janela. | Consumer para em `MaxDurationReached`. |
| D13-S03 | Request usa `IdleTimeout`. | Fila fica sem novas entregas. | Consumer para em `IdleTimeoutReached`. |
| D13-S04 | Request usa `StopAfterInitialQueueLength`. | Mensagem nova entra depois do inicio. | Consumer processa apenas snapshot inicial. |
| D13-S05 | Request usa `StopAfterInitialQueueLength`. | Snapshot inicial da fila e zero. | Resultado e `Empty`, sem abrir consumer. |
| D13-S06 | Fila nao existe. | Consumer e chamado. | Resultado e `QueueMissing`, nao excecao fatal. |
| D13-S07 | Nenhuma regra de parada e configurada. | Consumer e chamado. | Erro de configuracao claro. |

Aceite:

- README explica todos os status;
- logs mostram contadores `MessagesReceived`, `Acked`, `RemainingReadyCount`.

### Demo 14: `attention-with-primitives`

Condicoes iniciais:

- RabbitMQ local ativo.
- Fila de atencao criada.
- Filas de trabalho por entidade criadas.
- Gate in-memory habilitado.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D14-S01 | Attention chega para entidade pequena. | Handler consome fila dinamica. | Work e processado e attention e acked. |
| D14-S02 | Attention chega para entidade ruidosa com backlog alto. | Consumer dinamico usa fatia limitada. | Parte do work fica ready e nova attention e reprogramada. |
| D14-S03 | Dois workers tentam mesma entidade. | Gate usa chave `attention:{type}:{channelId}`. | Apenas um consome, outro reprograma ou encerra conforme politica. |
| D14-S04 | Shutdown ocorre durante consumo dinamico. | Token do contexto e cancelado. | Handler para cooperativamente e decide ack/requeue conforme resultado. |

Aceite:

- README deixa claro que locks de lifecycle sao da aplicacao;
- nao existe `MapAttentionQueue(...)`;
- Redis nao e dependencia obrigatoria.

### Demo 15: `observability-dashboard`

Condicoes iniciais:

- RabbitMQ local ou Aspire ativo.
- Logs em console habilitados.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D15-S01 | Mensagem feliz e processada. | Logs sao observados. | Registro mostra consumer e processamento. |
| D15-S02 | Handler falha. | Mensagem vai para DLQ. | Logs e RabbitMQ Management mostram falha. |
| D15-S03 | Health checks Aspire estao habilitados. | Broker esta healthy/unhealthy. | Health reflete estado da connection. |

Aceite:

- README inclui checklist operacional;
- sinais de ready, unacked e DLQ sao explicados.

### Demo 16: `application-gates`

Condicoes iniciais:

- RabbitMQ local ativo.
- Gate in-memory registrado.
- Opcional: provider Redis configurado pelo proprio demo.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D16-S01 | Dois consumidores disputam mesma chave. | Ambos tentam `TryAcquireAsync`. | Um lease e adquirido e outro e negado. |
| D16-S02 | Lease e negado. | Attention esta em processamento. | Handler reprograma via `Compose(RequeueToTail(), Ack())` ou ack conforme politica documentada. |
| D16-S03 | Provider externo e usado. | Hook recebe `IServiceProvider`. | Provider e resolvido pela aplicacao, nao pela library. |

Aceite:

- README mostra onde a chave e montada;
- README reforca ausencia de lock `channel-lifecycle` na library.

### Demo 17: `publish-consistency-work-attention`

Condicoes iniciais:

- RabbitMQ local ativo.
- Work queue e attention queue declaradas.
- Channel de publish com confirmations habilitado.

Cenarios:

| ID | Dado | Quando | Entao |
| --- | --- | --- | --- |
| D17-S01 | Work e attention sao roteaveis. | App publica work e aguarda confirm, depois attention e confirm. | Ambas mensagens chegam nas filas esperadas. |
| D17-S02 | Work nao e roteavel com `mandatory=true`. | Publish e executado. | Falha e detectada e attention nao e publicada. |
| D17-S03 | Attention falha depois do work confirmado. | App detecta falha. | README orienta reconciliacao/outbox como responsabilidade da aplicacao. |

Aceite:

- README explica limites de atomicidade;
- ordem de publish e confirmacao aparece em codigo compilavel.

## Criterios de aceite por fase

### Aprovacao da Fase 0

Para aprovar a infraestrutura:

- `samples/Demos/README.md` existe;
- `samples/Demos/docker-compose.yml` existe;
- projeto ou solucao dos demos compila;
- comando `list` mostra os 17 cases;
- README raiz explica variaveis `AMQP_URI` e `ORAGON_DEMO_PREFIX`.

### Aprovacao da Fase 1

Para aprovar os demos 01 a 10:

- todos os READMEs 01-10 existem;
- cada comando 01-10 roda contra RabbitMQ local ou documenta pre-requisito;
- `dotnet build` da suite passa;
- pelo menos um smoke test manual por demo foi registrado no resumo da entrega;
- docs do website linkam os demos basicos.

### Aprovacao da Fase 2

Para aprovar os demos 11 a 14:

- todos usam primitives reais da library;
- `graceful-shutdown` demonstra drain completo e timeout;
- `requeue-to-tail` demonstra ordem;
- `requeue-to-tail` documenta copy policy explicita de `BasicProperties`;
- `dynamic-queue-consumer` cobre todos os motivos de parada;
- `attention-with-primitives` nao adiciona API opinativa nem dependencia Redis.

### Aprovacao da Fase 3

Para aprovar os demos 15 a 17:

- observabilidade tem checklist operacional;
- gates usam contratos genericos e provider da aplicacao;
- publish consistency demonstra `mandatory` e publisher confirmations;
- limites de responsabilidade da library estao documentados.

### Aprovacao final do roadmap de demos

O roadmap de demos so deve ser considerado concluido quando:

- todos os 17 cases possuem README proprio;
- a suite compila;
- os comandos documentados foram testados ou marcados com verificacao manual
  pendente justificada;
- o website aponta para a suite;
- nenhum README promete `MapAttentionQueue(...)`, Redis oficial ou migracao
  automatica de topologia;
- `spec/docs-roadmap.md` referencia este roadmap em vez de carregar todo o
  detalhamento dos demos.
