# Roadmap de documentacao do Oragon.RabbitMQ

Este documento registra a analise do website Next em `docs/` contra a superficie real da library em `src/`, `samples/` e `tests/`.

O objetivo e tornar a documentacao fiel ao comportamento atual da library e preparar espaco para as proximas evolucoes planejadas, incluindo attention implementado com primitivas genericas, consumo dinamico sob demanda, graceful shutdown, BasicProperties bindings e exemplos completos.

## Status atual da execucao

Atualizado em 2026-06-14.

- O website em `docs/` foi expandido com paginas dedicadas para lifecycle,
  dynamic queues, BasicProperties, results, publish consistency, extension
  points, application gates, topology, observability, samples e roadmap.
- A navegacao em `docs/src/lib/navigation.ts` foi reorganizada em mais niveis
  para separar start, daily usage, configuration, reliability, operations,
  advanced patterns, examples e reference.
- O build do website foi validado com `yarn build`, gerando 50 paginas
  estaticas.
- O detalhamento executivo dos demos foi movido para
  `spec/demo-cases-roadmap.md`; este documento preserva o diagnostico original
  e a estrutura de manutencao da documentacao.
- O roadmap ativo de attention permanece em `spec/attention/README.md` e
  `spec/attention/milestone-roadmap.md`.

## Diagnostico original

A documentacao analisada no inicio do milestone cobria o caminho feliz de
`MapQueue`, serializacao, flow control, model binding basico e benchmarks. Ela
era boa como introducao, mas ainda nao representava a library inteira.

Principais lacunas:

- nao ha documentacao suficiente sobre ciclo de vida do consumer;
- nao ha pagina dedicada a setup standalone versus Aspire;
- os samples existentes nao aparecem como trilha oficial;
- extensoes como `WaitRabbitMQAsync` e keyed Aspire clients nao estao explicadas;
- configuracoes avancadas do `ConsumerDescriptor` aparecem em tabela, mas sem comportamento e tradeoffs;
- result handlers e semantica de retorno de handlers nao estao detalhados;
- model binding precisa cobrir toda a superficie real, incluindo `BasicProperties`;
- flow control nao explica falha durante execucao de result;
- topology e declaracao de filas aparecem apenas como exemplos curtos;
- observabilidade, health checks, tracing, connection blocked/unblocked e shutdown praticamente nao aparecem;
- nao ha pagina para migracao/compatibilidade por versao;
- attention ainda esta apenas em `spec/`, nao no website;
- nao ha documentacao para extension points onde a aplicacao assume locks, gates, validacoes e regras de negocio;
- nao ha pagina explicando consistencia de publish quando a aplicacao publica mensagem de trabalho e sinal de atencao.

## Correcoes planejadas e criterios de manutencao

As subsecoes abaixo nasceram como backlog de documentacao. Quando uma pagina ja
existe, elas devem ser lidas como checklist de cobertura e manutencao, nao como
prova automatica de pendencia.

### Installation

Arquivo atual: `docs/src/app/docs/installation/page.md`.

Incrementos necessarios:

- explicar separadamente `builder.AddRabbitMQConsumer()` e `builder.Services.AddRabbitMQConsumer()`;
- documentar que a aplicacao deve registrar `IConnection` ou usar AspireClient para registrar `IConnectionFactory` e `IConnection`;
- explicar `WaitRabbitMQAsync()` como verificacao de disponibilidade do broker antes de criar topologia ou iniciar fluxo dependente;
- adicionar exemplo com keyed connection factory em `WaitRabbitMQAsync(keyedServiceKey)`;
- explicar lifecycle da connection singleton e quando usar `WithConnection(...)` para override por consumer;
- adicionar matriz de pacotes:
  - `Oragon.RabbitMQ`;
  - `Oragon.RabbitMQ.Abstractions`;
  - `Oragon.RabbitMQ.Serializer.SystemTextJson`;
  - `Oragon.RabbitMQ.Serializer.NewtonsoftJson`;
  - `Oragon.RabbitMQ.AspireClient`.

Possiveis erros/incompletudes:

- a pagina mostra o setup minimo, mas nao deixa claro que sem `IConnection` registrado o consumer falha em startup;
- a pagina menciona Aspire, mas nao documenta `AddKeyedRabbitMQClient`.

### Quick start

Arquivo atual: `docs/src/app/docs/quick-start/page.md`.

Incrementos necessarios:

- mostrar o programa completo, incluindo package, `AddRabbitMQConsumer`, serializer, connection e `MapQueue`;
- incluir uma versao standalone e uma versao Aspire;
- explicar que o `MapQueue` deve ser registrado antes de `Run`;
- explicar que o default de sucesso e `Ack`;
- mostrar `Task`, `Task<IAmqpResult>`, `void` e retorno sincronico;
- incluir um exemplo de publicacao de mensagem para testar o consumer.

Possiveis erros/incompletudes:

- o exemplo com `var app = builder.Build()` nao mostra o setup anterior de `AddRabbitMQConsumer`, serializer e connection no mesmo bloco, o que pode induzir um copy/paste incompleto.

### MapQueue

Arquivo atual: `docs/src/app/docs/map-queue/page.md`.

Incrementos necessarios:

- explicar o lifecycle real:
  - `MapQueue` cria `ConsumerDescriptor`;
  - `ConsumerServer` guarda descriptors ate o host iniciar;
  - startup valida bindings de services;
  - startup aguarda queue existir com retry;
  - `BasicQos` aplica `WithPrefetch`;
  - `BasicConsumeAsync` inicia consumo;
  - `StopAsync` cancela o consumer;
  - `DisposeAsync` libera channel e connection quando aplicavel.
- documentar `WithConnection`, `WithChannel`, `WithSerializer`, `WithTopology`, `WithConsumerTag`, `WithExclusive`, `WithPrefetch`, `WithDispatchConcurrency`, `WhenSerializationFail`, `WhenProcessFail`;
- explicar connection ownership:
  - quando a factory retorna a connection singleton do DI, o consumer nao deve fechar a connection;
  - quando a factory cria uma connection nova, o consumer passa a ser dono dela e deve fechar no dispose.
- explicar fail-fast:
  - service binding invalido falha no startup;
  - queue inexistente e aguardada com retries;
  - erro de configuracao nao deve virar erro silencioso em runtime.
- documentar `ConsumerDispatchConcurrency` versus `PrefetchCount` com exemplos de combinacoes seguras.

Assuntos errados/incompletos:

- a tabela lista metodos, mas nao mostra exemplos suficientes nem impacto operacional;
- nao ha orientacao sobre `ConsumerTag` unico, `Exclusive` ou custom channel options.

### Model binding

Arquivo atual: `docs/src/app/docs/model-binding/page.md`.

Incrementos necessarios:

- documentar que atributos MVC (`Microsoft.AspNetCore.Mvc.FromBody`, `FromHeader`, etc.) sao rejeitados para evitar ambiguidade;
- expandir `[FromServices]` com keyed services;
- documentar `[FromBody]` e a regra de apenas um parametro de mensagem;
- documentar `[FromAmqpHeader]` com conversao tipada e comportamento para valores ausentes;
- documentar headers tipados e BasicProperties bindings como comportamento implementado;
- listar todos os auto-bound types reais:
  - `IConnection`;
  - `IChannel`;
  - `BasicDeliverEventArgs`;
  - `IReadOnlyBasicProperties`;
  - `IServiceProvider`;
  - `IAmqpContext`;
  - `CancellationToken`;
  - `DeliveryModes`.
- documentar convencoes reais:
  - `queue`, `queueName`;
  - `exchange`, `exchangeName`;
  - `routing`, `routingKey`;
  - `consumer`, `consumerTag`;
  - `priority`;
  - `deliveryCount`, `attempts`.
- explicar comportamento de `deliveryCount/attempts`:
  - nullable retorna `null` quando o header esta ausente;
  - nao nullable falha no startup para metadados AMQP opcionais;
  - `x-delivery-count` e tipico de quorum queues.

Assuntos errados/incompletos:

- a pagina atual lista `IReadOnlyBasicProperties`, o que esta correto para a API atual, mas nao explica que o binding vem de `context.Request.BasicProperties`;
- a pagina deve deixar claro quais conversoes tipadas sao suportadas por `[FromAmqpHeader]`;
- a pagina deve cobrir BasicProperties como superficie estavel implementada.

### Serialization

Arquivo atual: `docs/src/app/docs/serialization/page.md`.

Incrementos necessarios:

- documentar exatamente os dois pacotes de serializer e aliases:
  - `AddAmqpSerializer(...)` no pacote System.Text.Json;
  - `AddSystemTextJsonAmqpSerializer(...)`;
  - `AddAmqpSerializer(...)` no pacote Newtonsoft;
  - `AddNewtonsoftAmqpSerializer(...)`.
- explicar conflito se ambos pacotes forem importados e ambos expuserem `AddAmqpSerializer` no namespace `Oragon.RabbitMQ`;
- documentar keyed serializers com exemplo completo;
- documentar que `Forward` e `Reply` usam o serializer do contexto;
- documentar comportamento em payload nulo ou body vazio;
- documentar como `WhenSerializationFail` e chamado e qual e o default.

Assuntos errados/incompletos:

- a pagina nao alerta sobre ambiguidade de extension method `AddAmqpSerializer` quando os dois serializers estao referenciados;
- nao ha matriz de features entre System.Text.Json e Newtonsoft.

### Flow control

Arquivo atual: `docs/src/app/docs/flow-control/page.md`.

Incrementos necessarios:

- documentar todos os formatos de retorno:
  - `void` => `Ack`;
  - valor qualquer nao `IAmqpResult` => `Ack`;
  - `Task` => aguarda e `Ack`;
  - `IAmqpResult` => executa result;
  - `Task<IAmqpResult>` => aguarda e executa result;
  - excecao no handler => `WhenProcessFail`;
  - `Task<IAmqpResult>` retornando null => erro.
- explicar que `Ack`, `Nack` e `Reject` usam delivery tag atual;
- explicar diferenca entre `Nack(requeue:true)` e `Reject(requeue:true)`, especialmente para quorum queues e `x-delivery-count`;
- explicar `Compose` e que a ordem importa;
- explicar `Forward`:
  - cria channel dedicado;
  - publica usando `context.Connection`;
  - permite configurar `BasicProperties`;
  - seta `DeliveryMode`, `MessageId`, `CorrelationId`;
  - fecha channel ao final.
- explicar `Reply` e `ReplyAndAck` com `ReplyTo` e `CorrelationId`;
- documentar `WhenResultExecutionFail` e como falhas durante `IAmqpResult.ExecuteAsync` sao tratadas.

Assuntos errados/incompletos:

- a pagina atual nao explica falhas durante `IAmqpResult.ExecuteAsync`;
- nao explica que `ForwardAndAck` e composicao publish + ack e, portanto, falhas de publish importam antes do ack.

### Concepts

Arquivo atual: `docs/src/app/docs/concepts/page.md`.

Incrementos necessarios:

- adicionar arquitetura do pipeline:
  - descriptor;
  - hosted service;
  - queue consumer;
  - dispatcher;
  - binders;
  - result handlers;
  - result execution.
- adicionar principios:
  - fail fast;
  - manual ack;
  - explicit results;
  - DI scope por mensagem;
  - serializer plugavel;
  - RabbitMQ.Client 7.x first.
- explicar limites:
  - nao e abstracao generica de mensageria;
  - nao faz topology recovery automatico;
  - nao substitui politicas de retry do dominio;
  - nao cria producers genericos ainda.

### Benchmarks

Arquivo atual: `docs/src/app/docs/benchmarks/page.md`.

Incrementos necessarios:

- linkar para `benchmarks/Oragon.RabbitMQ.Benchmarks`;
- documentar como executar smoke test e benchmarks;
- registrar commit/data/ambiente dos numeros publicados;
- separar resultados medidos de interpretacao;
- avisar que benchmarks dependem de RabbitMQ local/container e configuracao de GC.

Assuntos errados/incompletos:

- os numeros aparecem sem apontar para scripts ou comandos reproduziveis;
- nao ha criterio para atualizar os dados quando a pipeline mudar.

## Organizacao recomendada do website

### Estrutura recomendada de navegacao

A navegacao atual tem apenas dois grupos: `Getting started` e `Core concepts`. Isso e insuficiente para uma library que precisa servir como guia de aprendizado, referencia de uso diario, material operacional e base para padroes avancados.

A nova navegacao deve ter mais niveis conceituais. O objetivo nao e criar paginas longas demais, mas permitir que o usuario encontre rapidamente o que precisa no dia-a-dia.

Proposta para `docs/src/lib/navigation.ts`:

```ts
export const navigation = [
  {
    title: 'Start here',
    links: [
      { title: 'Introduction', href: '/' },
      { title: 'Installation', href: '/docs/installation' },
      { title: 'Quick start', href: '/docs/quick-start' },
      { title: 'Standalone hosting', href: '/docs/standalone' },
      { title: 'Aspire integration', href: '/docs/aspire' },
    ],
  },
  {
    title: 'Daily usage',
    links: [
      { title: 'MapQueue', href: '/docs/map-queue' },
      { title: 'Handlers and return types', href: '/docs/handlers' },
      { title: 'Model binding', href: '/docs/model-binding' },
      { title: 'BasicProperties and headers', href: '/docs/basic-properties' },
      { title: 'Serialization', href: '/docs/serialization' },
      { title: 'Results and publishing', href: '/docs/results' },
      { title: 'Flow control', href: '/docs/flow-control' },
    ],
  },
  {
    title: 'Configuration',
    links: [
      { title: 'Consumer descriptor', href: '/docs/consumer-descriptor' },
      { title: 'Connections and channels', href: '/docs/connections' },
      { title: 'Prefetch and concurrency', href: '/docs/concurrency' },
      { title: 'Topology', href: '/docs/topology' },
      { title: 'Serializers and keyed services', href: '/docs/keyed-services' },
    ],
  },
  {
    title: 'Reliability',
    links: [
      { title: 'Error handling', href: '/docs/error-handling' },
      { title: 'Retry and DLQ', href: '/docs/retry-dlq' },
      { title: 'Lifecycle and shutdown', href: '/docs/lifecycle' },
      { title: 'Publish consistency', href: '/docs/publish-consistency' },
      { title: 'Poison messages', href: '/docs/poison-messages' },
      { title: 'Operational pitfalls', href: '/docs/operational-pitfalls' },
    ],
  },
  {
    title: 'Operations',
    links: [
      { title: 'Observability', href: '/docs/observability' },
      { title: 'Health checks', href: '/docs/health-checks' },
      { title: 'RabbitMQ management', href: '/docs/rabbitmq-management' },
      { title: 'Benchmarks', href: '/docs/benchmarks' },
      { title: 'Troubleshooting', href: '/docs/troubleshooting' },
    ],
  },
  {
    title: 'Advanced patterns',
    links: [
      { title: 'Request/reply', href: '/docs/request-reply' },
      { title: 'Forwarding and fan-out', href: '/docs/forwarding' },
      { title: 'Dynamic queues', href: '/docs/dynamic-queues' },
      { title: 'Application-owned gates', href: '/docs/application-gates' },
      { title: 'Extension points', href: '/docs/extension-points' },
      { title: 'Attention with primitives', href: '/docs/attention' },
    ],
  },
  {
    title: 'Examples',
    links: [
      { title: 'Samples overview', href: '/docs/samples' },
      { title: 'Minimal consumer', href: '/docs/samples/minimal-consumer' },
      { title: 'Standalone with DLQ', href: '/docs/samples/standalone-dlq' },
      { title: 'Aspire worker', href: '/docs/samples/aspire-worker' },
      { title: 'Retry with quorum queues', href: '/docs/samples/retry-quorum' },
      { title: 'Attention with primitives', href: '/docs/samples/attention-with-primitives' },
    ],
  },
  {
    title: 'Reference',
    links: [
      { title: 'API reference', href: '/docs/api-reference' },
      { title: 'Package matrix', href: '/docs/packages' },
      { title: 'Compatibility', href: '/docs/compatibility' },
      { title: 'Roadmap', href: '/docs/roadmap' },
    ],
  },
]
```

### Principios para a nova estrutura

- `Start here` deve responder "como comeco sem conhecer o projeto?".
- `Daily usage` deve ser otimizado para tarefas frequentes de implementacao.
- `Configuration` deve reunir knobs e tradeoffs que aparecem depois do primeiro consumer.
- `Reliability` deve ensinar como nao perder mensagens, evitar loops e desligar corretamente.
- `Operations` deve ajudar quem esta rodando isso em producao.
- `Advanced patterns` deve cobrir ganhos reais de arquitetura, como request/reply, fan-out, filas dinamicas, gates definidos pela aplicacao e attention com primitivas.
- `Examples` deve apontar para projetos executaveis, nao apenas snippets.
- `Reference` deve ser lookup rapido para APIs, pacotes e compatibilidade.

### Paginas de ganho no dia-a-dia

Estas paginas devem ser priorizadas porque reduzem duvidas recorrentes durante implementacao:

- `Handlers and return types`: tabela direta de `void`, `Task`, valor comum, `IAmqpResult`, `Task<IAmqpResult>`, excecoes e null.
- `Consumer descriptor`: guia de cada metodo fluente com "quando usar", default, impacto e exemplo.
- `Connections and channels`: quando usar connection singleton, keyed connection, custom channel e ownership.
- `Prefetch and concurrency`: receitas praticas para I/O-bound, CPU-bound, ordering, SAC ja provisionado e handlers nao thread-safe.
- `Retry and DLQ`: receitas para poison messages, quorum queue, `x-delivery-count`, `Reject` versus `Nack`.
- `Operational pitfalls`: lista objetiva de problemas comuns, sintomas e correcao.
- `Troubleshooting`: erro de fila inexistente, serializer nao registrado, handler com binder ambiguo, connection fechada, DLQ crescendo, unacked alto.
- `API reference`: uma pagina curta por classe/metodo central, com links para guias mais ricos.

### Formato recomendado para paginas praticas

Paginas de uso diario devem seguir um padrao repetivel:

1. Quando usar.
2. Exemplo minimo.
3. Exemplo realista.
4. Defaults.
5. Tradeoffs.
6. Erros comuns.
7. Como testar localmente.
8. Links para samples relacionados.

Exemplo para `Prefetch and concurrency`:

- quando `WithPrefetch(1)` e correto;
- quando aumentar prefetch;
- quando aumentar `WithDispatchConcurrency`;
- por que concorrencia quebra ordering;
- como combinar com SAC quando a topologia ja foi provisionada;
- como diagnosticar `unacked`;
- sample `concurrency-prefetch`.

### Referencia cruzada com demos

Cada pagina operacional ou avancada deve apontar para pelo menos um demo executavel:

| Pagina | Demo principal |
| --- | --- |
| `MapQueue` | `minimal-consumer` |
| `Topology` | `standalone-topology-dlq` |
| `Model binding` | `model-binding-lab` |
| `Results and publishing` | `flow-control-results` |
| `Request/reply` | `rpc-request-reply` |
| `Prefetch and concurrency` | `concurrency-prefetch` |
| `Serialization` | `serializers` |
| `Aspire integration` | `aspire-worker` |
| `Retry and DLQ` | `retry-quorum-delivery-count` |
| `Lifecycle and shutdown` | `graceful-shutdown` |
| `Dynamic queues` | `dynamic-queue-consumer` |
| `Application-owned gates` | `application-gates` |
| `Attention with primitives` | `attention-with-primitives` |
| `Publish consistency` | `publish-consistency-work-attention` |
| `Observability` | `observability-dashboard` |

### Architecture

Nova pagina em `docs/src/app/docs/architecture/page.md`.

Deve explicar:

- `ConsumerServer`;
- `ConsumerDescriptor`;
- `QueueConsumer`;
- `Dispatcher`;
- `ArgumentBinderExtensions`;
- `ResultHandlerExtensions`;
- `IAmqpResult`;
- lifecycle start/stop/dispose;
- como as excecoes atravessam o pipeline.

### Consumer lifecycle and graceful shutdown

Nova pagina em `docs/src/app/docs/lifecycle/page.md`.

Deve cobrir comportamento atual e roadmap:

- startup;
- queue passive declare retry;
- start all consumers em paralelo;
- stop em ordem reversa;
- dispose;
- connection/channel ownership;
- connection shutdown/blocked/unblocked logs;
- graceful shutdown opt-in implementado;
- in-flight messages;
- cancelamento cooperativo.

### Aspire integration

Nova pagina em `docs/src/app/docs/aspire/page.md`.

Deve cobrir:

- `Oragon.RabbitMQ.AspireClient`;
- `AddRabbitMQClient`;
- `AddKeyedRabbitMQClient`;
- settings;
- connection string resolution;
- health checks;
- tracing;
- `ClientProvidedName`;
- `AutomaticRecoveryEnabled` e `TopologyRecoveryEnabled` quando relevante;
- exemplos com AppHost e Worker.

### Standalone hosting

Nova pagina em `docs/src/app/docs/standalone/page.md`.

Deve cobrir:

- console/worker service;
- ASP.NET Core minimal host;
- docker-compose com RabbitMQ;
- setup manual de connection factory;
- `WaitRabbitMQAsync`;
- declaracao de topologia antes de consumir.

### Topology

Nova pagina em `docs/src/app/docs/topology/page.md`.

Deve cobrir:

- `WithTopology`;
- queue declare;
- exchange declare;
- bindings;
- DLQ;
- quorum queue;
- priority queue;
- `x-single-active-consumer`;
- quando declarar no app versus infra externa;
- riscos de argumentos imutaveis no RabbitMQ.

### Error handling and retry

Nova pagina em `docs/src/app/docs/error-handling/page.md`.

Deve cobrir:

- deserialization failure;
- process failure;
- `WhenResultExecutionFail`;
- `Nack` versus `Reject`;
- DLQ;
- retry por `x-delivery-count`;
- poison messages;
- loop infinito de requeue;
- exemplos com quorum queue.

### Results and publishing

Nova pagina em `docs/src/app/docs/results/page.md`.

Deve detalhar:

- `Ack`;
- `Nack`;
- `Reject`;
- `Reply`;
- `ReplyAndAck`;
- `Forward`;
- `ForwardAndAck`;
- `Compose`;
- configuracao de `BasicProperties`;
- correlation e message id;
- publish com channel dedicado;
- copia explicita de BasicProperties;
- `RequeueToTail`.

### Publish consistency

Nova pagina em `docs/src/app/docs/publish-consistency/page.md`.

Deve cobrir:

- diferenca entre publicar uma mensagem e confirmar a entrega original;
- publisher confirmations;
- comportamento `mandatory`/unrouted;
- ordem segura `publish -> confirm -> ack`;
- outbox como responsabilidade da aplicacao quando houver banco de dados envolvido;
- reconciliacao de filas com backlog sem sinal de atencao como rotina da aplicacao;
- limites da library: ela ajuda a publicar com segurança no RabbitMQ, mas nao promete atomicidade entre banco, broker e regras de negocio.

### BasicProperties and headers

Nova pagina em `docs/src/app/docs/basic-properties/page.md`.

Deve cobrir:

- diferenca entre propriedades AMQP conhecidas e headers arbitrarios;
- binding atual de `IReadOnlyBasicProperties`;
- binding atual de `DeliveryModes`;
- binding atual de `priority`;
- binding atual de `deliveryCount/attempts`;
- `[FromAmqpHeader]` atual;
- roadmap de bindings para `contentType`, `correlationId`, `messageId`, `replyTo`, `timestamp`, `appId`, etc.

### Dynamic queues

Nova pagina em `docs/src/app/docs/dynamic-queues/page.md`.

Deve cobrir:

- `IAmqpDynamicQueueConsumer`;
- fila definida em runtime;
- `MaxMessages`;
- `MaxDuration`;
- `IdleTimeout`;
- `StopAfterInitialQueueLength`;
- regra de "ao menos um mecanismo efetivo de parada";
- significado de `InitialReadyCount` como snapshot de mensagens ready;
- comportamento de idle enquanto houver mensagem in-flight;
- `BeforeStartAsync` e `AfterStopAsync`;
- resultado final com motivo de parada e contadores;
- exemplos de uso fora de attention.

### Application-owned gates

Nova pagina em `docs/src/app/docs/application-gates/page.md`.

Deve cobrir:

- porque locks, rate limits e validacoes de negocio pertencem a aplicacao;
- como plugar um gate generico antes do consumo dinamico;
- chave de lock sempre definida pelo usuario, por exemplo `attention:{type}:{channelId}`;
- ausencia de pacote Redis oficial no milestone;
- Redis apenas como exemplo de implementacao do cliente, quando fizer sentido;
- alternativa em que o usuario implementa o gate usando a tecnologia que escolher;
- uso de `IServiceProvider` no contexto de extensibilidade para resolver `IConnectionMultiplexer`, provider SQL, provider de lock distribuido ou outro servico registrado pela aplicacao;
- o que fazer quando o gate bloqueia: ack, requeue to tail, skip, fail ou retry;
- diferenca entre gate generico da library e locks de lifecycle de dominio, como `channel-lifecycle:{channelId}`, que devem ficar fora da library.

### Extension points

Nova pagina em `docs/src/app/docs/extension-points/page.md`.

Deve cobrir:

- `WhenSerializationFail`;
- `WhenProcessFail`;
- `WhenResultExecutionFail`;
- `WithTopology`;
- publish confiavel usado pelos results;
- publish primitive publica compartilhada apenas se o milestone futuro justificar a API;
- hooks do consumer dinamico;
- `IServiceProvider` disponivel nos contextos de extensibilidade para resolver dependencias da aplicacao;
- quais pontos antigos ja tem provider direto ou via `IAmqpContext`;
- quais pontos expoem overload non-breaking com provider: `WithChannel` e `WithTopology`;
- como executar actions/functions da aplicacao sem acoplar o Oragon.RabbitMQ ao dominio;
- padrao recomendado para manter business rules nos services da aplicacao.

### Observability

Nova pagina em `docs/src/app/docs/observability/page.md`.

Deve cobrir:

- logs estruturados existentes no consumer;
- connection shutdown;
- connection blocked/unblocked;
- Aspire health checks;
- tracing do Aspire client;
- recomendacoes de dashboards:
  - consumer count;
  - ready/unacked;
  - DLQ depth;
  - redeliveries;
  - connection blocked.

### Samples and demos

Nova pagina em `docs/src/app/docs/samples/page.md`.

Deve mapear cada sample para o que ele demonstra e como executar.

### Attention with primitives

Nova pagina em `docs/src/app/docs/attention/page.md`.

Deve apontar para o conceito de fila de atencao, consumo dinamico sob demanda, `RequeueToTail`, graceful shutdown, publish confiavel e topologia recomendada.

A pagina deve deixar claro:

- o Oragon.RabbitMQ entrega primitivas, nao um `MapAttentionQueue(...)` neste milestone;
- o handler de atencao pode ser um `MapQueue` comum;
- a fila dinamica e consumida por `IAmqpDynamicQueueConsumer`;
- locks/gates e validacoes de recurso sao responsabilidade da aplicacao;
- SAC/quorum/DLQ podem aparecer como topologia recomendada, mas migracao automatica de filas existentes nao faz parte da library;
- `MapAttentionQueue(...)` so deve ser citado como possibilidade futura, nao como contrato prometido.

## Projetos de demonstracao

O detalhamento executivo dos cases foi extraido para
`spec/demo-cases-roadmap.md`.

Este trecho permanece como resumo historico e como origem da lista de demos,
mas requisitos, condicoes iniciais, cenarios de teste, aceite e aprovacao devem
ser mantidos no roadmap dedicado.

Os samples atuais sao uteis, mas nao cobrem todas as features nem parecem organizados como uma suite de aprendizado. A recomendacao e criar uma pasta `samples/` com projetos pequenos, nomeados por caso, cada um com README proprio, comandos de execucao e topologia reproduzivel.

### Demo 01: minimal-consumer

Objetivo: primeiro consumer funcional.

Features cobertas:

- `AddRabbitMQConsumer`;
- System.Text.Json serializer;
- `IConnection` singleton;
- `MapQueue`;
- default `Ack`.

Aceite:

- `docker compose up rabbitmq`;
- `dotnet run`;
- publish de uma mensagem via endpoint HTTP ou script;
- mensagem processada e acked.

### Demo 02: standalone-topology-dlq

Objetivo: app standalone declarando topologia propria.

Features cobertas:

- `WithTopology`;
- queue declare;
- DLQ;
- `WhenSerializationFail`;
- `WhenProcessFail`;
- `WaitRabbitMQAsync`.

Aceite:

- mensagem invalida vai para DLQ;
- excecao no handler vai para DLQ;
- README mostra como inspecionar a DLQ.

### Demo 03: model-binding-lab

Objetivo: demonstrar todos os binders atuais.

Features cobertas:

- `[FromServices]`;
- keyed services;
- `[FromBody]`;
- `[FromAmqpHeader]`;
- `IConnection`;
- `IChannel`;
- `BasicDeliverEventArgs`;
- `IReadOnlyBasicProperties`;
- `IServiceProvider`;
- `IAmqpContext`;
- `CancellationToken`;
- `DeliveryModes`;
- `queueName`;
- `routingKey`;
- `exchangeName`;
- `consumerTag`;
- `priority`;
- `deliveryCount/attempts`.

Aceite:

- cada binder tem um handler pequeno;
- testes ou endpoint publicador exercitam cada fila.

### Demo 04: flow-control-results

Objetivo: demonstrar todos os `AmqpResults`.

Features cobertas:

- `Ack`;
- `Nack`;
- `Reject`;
- `Reply`;
- `ReplyAndAck`;
- `Forward`;
- `ForwardAndAck`;
- `Compose`;
- configuracao de `BasicProperties` no `Forward`.

Aceite:

- cada result tem uma fila de entrada;
- README explica o estado esperado no RabbitMQ apos cada publish.

### Demo 05: rpc-request-reply

Objetivo: request/reply completo.

Features cobertas:

- `ReplyTo`;
- `CorrelationId`;
- `Reply`;
- `ReplyAndAck`;
- timeout do cliente;
- correlation no consumidor.

Aceite:

- client envia request;
- consumer responde;
- client correlaciona response corretamente.

### Demo 06: concurrency-prefetch

Objetivo: demonstrar relacao entre prefetch e dispatch concurrency.

Features cobertas:

- `WithPrefetch`;
- `WithDispatchConcurrency`;
- ordering;
- idempotencia;
- I/O-bound versus CPU-bound.

Aceite:

- rodar com concorrencia 1 e 8;
- logs mostram paralelismo e mudanca de ordem.

### Demo 07: serializers

Objetivo: comparar System.Text.Json, Newtonsoft e keyed serializers.

Features cobertas:

- `AddSystemTextJsonAmqpSerializer`;
- `AddNewtonsoftAmqpSerializer`;
- keyed `IAmqpSerializer`;
- `WithSerializer`.

Aceite:

- duas filas usam serializers diferentes;
- payload com casing/conversores demonstra diferenca.

### Demo 08: aspire-worker

Objetivo: sample Aspire oficial e limpo.

Features cobertas:

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

### Demo 09: keyed-rabbitmq

Objetivo: multiplas conexoes RabbitMQ.

Features cobertas:

- `AddKeyedRabbitMQClient`;
- keyed `IConnection`;
- `WithConnection`;
- keyed serializer opcional.

Aceite:

- duas filas consomem de conexoes/virtual hosts diferentes;
- README explica quando usar.

### Demo 10: retry-quorum-delivery-count

Objetivo: retry com quorum queue e `x-delivery-count`.

Features cobertas:

- quorum queue;
- DLQ;
- `deliveryCount/attempts`;
- `Reject(requeue:true)` versus terminal;
- poison message.

Aceite:

- mensagem falha N vezes;
- depois vai para DLQ;
- logs mostram attempts.

### Demo 11: graceful-shutdown

Objetivo: demonstrar shutdown cooperativo implementado pela library.

Features cobertas:

- `WithGracefulShutdown`;
- token cancelado;
- in-flight drain;
- timeout;
- handler que respeita token e handler que ignora token.

Aceite:

- SIGTERM para o app;
- logs mostram `BasicCancel`, token cancelado e drain.

### Demo 12: requeue-to-tail

Objetivo: demonstrar diferenca entre `Nack(requeue:true)` e republicar no fim da fila.

Features cobertas:

- `RequeueToTail`;
- prioridade preservada;
- correlation preservada;
- fairness.

Aceite:

- mensagens A, B, C;
- A e reprogramada para o fim;
- ordem observada comprova comportamento.

### Demo 13: dynamic-queue-consumer

Objetivo: demonstrar consumo dinamico sob demanda.

Features cobertas:

- `IAmqpDynamicQueueConsumer`;
- fila definida em runtime;
- `MaxMessages`;
- `MaxDuration`;
- `IdleTimeout`;
- `StopAfterInitialQueueLength`;
- `BeforeStartAsync`;
- `AfterStopAsync`;
- status de encerramento;
- contadores no resultado.

Aceite:

- cada regra de parada tem um cenario;
- combinacao de regras encerra na primeira atingida;
- fila ausente e vazia nao sao erro fatal;
- idle nao encerra enquanto houver mensagem in-flight.

### Demo 14: attention-with-primitives

Objetivo: demonstrar o padrao completo em cima das primitivas.

Features cobertas:

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
- README explica quais responsabilidades sao da aplicacao.

### Demo 15: observability-dashboard

Objetivo: demonstrar operacao.

Features cobertas:

- logs;
- health checks;
- tracing Aspire;
- RabbitMQ management;
- DLQ inspection.

Aceite:

- README orienta quais sinais observar;
- falhas simuladas aparecem em logs/health/DLQ.

### Demo 16: application-gates

Objetivo: demonstrar gates distribuidos extensiveis sem semantica de dominio dentro da library.

Features cobertas:

- gate generico;
- Redis como implementacao do proprio sample, sem pacote oficial da library;
- uma alternativa simples com banco ou provider fake em memoria para mostrar substituicao tecnologica;
- chave informada pela aplicacao;
- TTL/lease;
- bloqueio antes de abrir consumer dinamico;
- `BeforeStartAsync`;
- `IServiceProvider` no hook para resolver o provider Redis;
- `RequeueToTail` quando bloqueado;
- nenhuma dependencia de lock `channel-lifecycle`.

Aceite:

- dois workers tentam consumir a mesma fila dinamica;
- apenas um adquire o lease;
- o segundo reprograma a atencao ou encerra conforme politica do sample;
- README mostra onde a aplicacao monta a chave;
- README deixa claro que a implementacao do gate pertence ao cliente.

### Demo 17: publish-consistency-work-attention

Objetivo: demonstrar publicacao de mensagem de trabalho e sinal de atencao com garantias explicitas.

Features cobertas:

- publish consistency com channel dedicado, `mandatory` e publisher confirmations;
- publisher confirmations;
- `mandatory`/unrouted;
- ordem `publish work -> confirm -> publish attention -> confirm`;
- falha antes de ack;
- estrategia de reconciliacao simples feita pela aplicacao.

Aceite:

- cenario feliz publica work e attention;
- falha de routing e detectada;
- README explica quando usar outbox fora da library.

## Ordem recomendada para fechar o roadmap de docs

1. Corrigir paginas existentes para ficarem fieis a API atual.
2. Criar paginas novas de arquitetura, lifecycle, topology, error handling, results e Aspire.
3. Criar pagina `Samples and demos` como indice oficial.
4. Limpar ou recriar samples atuais para virarem demos intencionais.
5. Adicionar demos 01 a 10 cobrindo features atuais.
6. Manter demos 11 a 17 como cobertura das primitivas de attention.
7. Adicionar verificacao de snippets: sempre que possivel, snippets devem compilar em projetos de demo ou testes.
8. Atualizar `docs/src/lib/navigation.ts` para refletir a nova arquitetura da documentacao.

## Criterios de aceite da documentacao

- Todo metodo publico central tem pelo menos uma referencia navegavel.
- Todo comportamento default tem uma pagina explicando consequencias operacionais.
- Todo exemplo do website compila ou existe em sample/teste equivalente.
- Todo sample tem README com requisitos, como executar, como publicar mensagens e como validar resultado.
- A documentacao distingue claramente comportamento existente de roadmap/futuro.
- Attention aparece primeiro como conceito/spec e depois como guia pratico construido sobre as primitivas implementadas.
- A documentacao nao promete migracao SAC, locks de lifecycle ou `MapAttentionQueue(...)` como parte deste milestone.
