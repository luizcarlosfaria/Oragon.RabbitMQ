# Milestone: suporte a Attention, consumo dinamico e shutdown cooperativo

> Status (2026-07-05): implementado na branch `release/1.10`.
>
> As primitivas descritas neste roadmap existem no código: `WithGracefulShutdown`, `WhenResultExecutionFail`, bindings estáveis (`AmqpHeaders`, `[FromAmqpHeader]` tipado), `AmqpResults.RequeueToTail`, publish com publisher confirms nos results, `IAmqpDynamicQueueConsumer`, `IAmqpConcurrencyGate` (contrato), `AmqpRetryPolicy.ByDeliveryCount` e `QueueArguments`/`QueueArgumentDiagnostics`. Divergências do contrato final em relação ao corpo deste documento:
>
> - `DynamicQueueConsumeRequest<T>` ganhou `Metadata` (`IReadOnlyDictionary<string, object>`) e `InFlightDrainTimeout`, e `OnMessageAsync` usa `Func<T, IAmqpContext, Task<IAmqpResult>>`; os tipos `DynamicQueueMessageContext`/`DynamicQueueMessageResult` não existem.
> - O gate usa `TryAcquireAsync(AmqpConcurrencyGateRequest, CancellationToken)` com o record `AmqpConcurrencyGateRequest(Key, LeaseTime, Metadata)`.
> - Garantia de publish nos results: recebimento pelo broker via publisher confirms em canal dedicado. Verificação de roteamento via `mandatory`/`basic.return` não faz parte do contrato — fila de destino ausente não é falha do publish; menções a `mandatory`/unrouted no corpo deste documento estão superadas.
> - Default de `WhenResultExecutionFail`: `Nack(requeue: false)`.
> - Default de cópia no `RequeueToTail`: cópia integral da mensagem original (incluindo `x-death` e histórico de dead-letter), exceto `UserId` (validated-user-id) e o header `x-delivery-count` (estado de quorum queue). Ver seção "Copia de BasicProperties em publish/requeue".

Este roadmap descreve um milestone inteiro para evoluir o Oragon.RabbitMQ com primitivas que suportem o padrao de Attention Queue e implementacoes parecidas.

O milestone deve ser implementado como mudanca additive/minor sempre que possivel. Comportamentos que possam afetar clientes existentes devem ser opt-in.

## Objetivo

Permitir que clientes implementem um fluxo do tipo:

1. uma mensagem de atencao chega em uma fila fixa;
2. o handler resolve uma fila dinamica de trabalho;
3. a library consome uma fatia controlada dessa fila dinamica;
4. o processamento respeita shutdown, limites e retry;
5. o handler confirma a atencao ou republica a atencao para continuar depois.

O milestone nao deve implementar um modulo `Attention` opinativo. Ele deve entregar primitivas suficientes para que a aplicacao implemente esse padrao com `MapQueue`, `IAmqpDynamicQueueConsumer`, publish confiavel e `RequeueToTail`.

Um modulo futuro como `MapAttentionQueue(...)` pode ser reavaliado depois, mas nao e premissa do desenho. Se as primitivas ficarem boas, esse modulo pode ser desnecessario.

## Decisoes fechadas antes da execucao

1. As interfaces e contratos publicos novos devem ficar em `Oragon.RabbitMQ.Abstractions`.
2. O core `Oragon.RabbitMQ` nao deve depender de Redis.
3. Nao havera pacote Redis oficial neste milestone. Redis pode aparecer em exemplos como implementacao do cliente.
4. Gates, locks, rate limits e validacoes de negocio sao da aplicacao. A library fornece contratos, contexto e hooks.
5. `MapAttentionQueue(...)` esta descartado neste milestone.
6. Migracao automatica para SAC/quorum/DLQ esta fora de escopo.
7. Results que publicam antes de confirmar a mensagem original devem seguir a ordem `publish -> confirm -> ack`.

### `IServiceProvider` nos pontos de extensibilidade

Nem todo delegate precisa receber `IServiceProvider` diretamente. A regra de evolucao deve ser:

- `WithConnection` e `WithSerializer` ja recebem `IServiceProvider`; manter assinatura atual.
- `WhenSerializationFail` e `WhenProcessFail` ja recebem `IAmqpContext`; usar `context.ServiceProvider`.
- `WhenResultExecutionFail` recebe `IAmqpContext`, nao um provider separado.
- `IAmqpResult.ExecuteAsync(IAmqpContext)` ja tem acesso a `context.ServiceProvider`.
- `WithChannel` expoe overload non-breaking com `IServiceProvider`.
- `WithTopology` expoe overload non-breaking com `IServiceProvider`.
- os hooks novos do consumer dinamico devem receber contextos com `IServiceProvider`.

Overloads conceituais:

```csharp
descriptor.WithChannel((services, connection, cancellationToken) =>
    connection.CreateChannelAsync(cancellationToken: cancellationToken));

descriptor.WithTopology(async (services, channel, cancellationToken) =>
{
    var topology = services.GetRequiredService<IMyTopologyService>();
    await topology.DeclareAsync(channel, cancellationToken);
});
```

Esses providers sao para extensibilidade da aplicacao. A library nao deve resolver provider Redis ou provider de lock especifico por conta propria.

## 1. Graceful shutdown opt-in

### Implementacao

Adicionar uma configuracao no `ConsumerDescriptor`, por exemplo:

```csharp
descriptor.WithGracefulShutdown(options =>
{
    options.CancelContextTokenOnStop = true;
    options.WaitForInFlightMessages = true;
    options.DrainTimeout = TimeSpan.FromSeconds(30);
});
```

Alterar `QueueConsumer` para, quando essa opcao estiver habilitada:

- rastrear quantidade de mensagens em processamento;
- chamar `BasicCancelAsync` em `StopAsync`;
- cancelar o `CancellationTokenSource` usado pelo `IAmqpContext`;
- aguardar as mensagens in-flight terminarem ate `DrainTimeout`;
- usar um token interno de shutdown para operacoes AMQP criticas, evitando depender apenas do token recebido de `IHostedService.StopAsync`;
- registrar logs estruturados quando o drain terminar ou estourar timeout.

O comportamento deve ser modelado como uma state machine simples:

```text
Running
  -> StopRequested
  -> CancelingBrokerConsumer
  -> ContextTokenCanceled
  -> DrainingInFlight
  -> Stopped
```

Se `DrainTimeout` expirar, o estado final ainda deve ser `Stopped`, mas com timeout registrado em log/metrica. A library nao deve tentar finalizar uma mensagem em nome de um handler que ainda esta executando.

### Semantica das opcoes

`CancelContextTokenOnStop` define se o token entregue ao handler deve ser cancelado quando o host chamar `StopAsync`.

Quando habilitado, handlers que recebem `CancellationToken` por parametro ou acessam `IAmqpContext.CancellationToken` passam a receber um sinal cooperativo de encerramento. Esse sinal permite parar loops, cancelar chamadas externas, interromper waits e encerrar consumidores internos criados pelo proprio handler.

Exemplo:

```csharp
Task<IAmqpResult> Handle(MyMessage message, CancellationToken cancellationToken)
```

Com `CancelContextTokenOnStop = true`, esse token e cancelado durante o shutdown do consumer.

Esse cancelamento sinaliza o handler, nao deve impedir automaticamente a
execucao do `IAmqpResult` retornado pelo handler. Se o handler observar o
cancelamento e ainda assim retornar um resultado terminal, como `Ack`,
`Reject`, `Forward` ou `RequeueToTail`, a library deve tentar executar esse
resultado dentro da janela de `DrainTimeout`. Isso evita perder a oportunidade
de confirmar, rejeitar ou republicar uma mensagem apenas porque o token
cooperativo do handler ja foi cancelado.

`WaitForInFlightMessages` define se `StopAsync` deve aguardar mensagens que ja foram entregues ao handler, mas ainda nao chegaram a um resultado final.

Mensagem in-flight significa uma entrega que ja saiu do RabbitMQ para o consumer e esta em processamento dentro do pipeline do Oragon. Ela ainda nao recebeu `Ack`, `Nack`, `Reject`, `Forward + Ack` ou outro `IAmqpResult` terminal.

Quando habilitado, `StopAsync` para novas entregas e espera as execucoes em andamento terminarem antes de seguir.

`DrainTimeout` define o tempo maximo dessa espera.

Se todas as mensagens in-flight terminarem antes do timeout, o shutdown segue normalmente. Se o timeout expirar, a library deve registrar log estruturado e continuar o encerramento. A library nao deve tentar matar a task do handler a forca e tambem nao deve inventar `Ack` ou `Nack` para uma mensagem cujo handler ainda esta executando.

Fluxo esperado com as tres opcoes habilitadas:

```text
Host chama StopAsync
  -> Oragon chama BasicCancelAsync
  -> RabbitMQ para de entregar novas mensagens para esse consumer
  -> Oragon cancela o token do contexto
  -> handlers em execucao recebem sinal cooperativo de shutdown
  -> resultados retornados pelos handlers ainda podem executar dentro do DrainTimeout
  -> Oragon espera mensagens in-flight terminarem ate DrainTimeout
  -> se terminarem: shutdown segue limpo
  -> se nao terminarem: Oragon loga timeout e continua encerramento
```

### Comportamento adicionado

Handlers passam a receber sinal cooperativo de shutdown pelo `CancellationToken` do `IAmqpContext`.

Ao parar a aplicacao, o consumer deixa de aceitar novas mensagens e aguarda o processamento em andamento dentro do limite configurado.

### Efeito na library

O comportamento atual continua sendo o default. Clientes existentes nao passam a receber cancelamento em `StopAsync` sem opt-in.

Clientes que precisam de shutdown controlado, como o MessageFy, deixam de depender apenas de `IHostApplicationLifetime.ApplicationStopping` dentro do codigo de negocio.

O objetivo e melhorar o graceful shutdown do `MapQueue` em si. Consumers dinamicos criados dentro de handlers podem aproveitar esse token, mas o desenho nao deve depender de tipos ou servicos especificos do MessageFy.

## 2. Politica configuravel para falha em result execution

### Implementacao

Adicionar uma configuracao no `ConsumerDescriptor`:

```csharp
descriptor.WhenResultExecutionFail((context, exception) => AmqpResults.Nack(false));
```

Antes desta mudanca, quando `IAmqpResult.ExecuteAsync(context)` falhava, o `QueueConsumer` tentava `BasicNack(requeue:false)` como fallback fixo. A mudanca substitui esse fallback por uma politica configuravel, mantendo `Nack(false)` como default.

### Comportamento adicionado

Clientes podem escolher o resultado aplicado quando um `Ack`, `Forward`, `Reply`, `Compose` ou outro result falhar durante execucao.

Exemplos:

- `Nack(false)` para enviar para DLQ;
- `Nack(true)` para retry;
- `Reject(false)` para descarte/DLQ;
- result customizado para observabilidade e decisao por excecao.

### Efeito na library

Remove uma decisao hardcoded em um ponto sensivel do pipeline. Isso e importante para fluxos de attention porque uma falha ao republicar atencao nao deve necessariamente ter a mesma politica de uma falha de processamento comum.

## 3. BasicProperties, headers, priority e attempts como superficie estavel

### Implementacao

Promover as propriedades AMQP conhecidas de `BasicProperties` para binding por convencao, alem do binding do objeto inteiro.

Essas propriedades devem poder ser recebidas diretamente no handler quando o nome e o tipo do parametro forem reconhecidos:

```csharp
public IAmqpResult Handle(
    MyMessage message,
    string? contentType,
    string? contentEncoding,
    IReadOnlyDictionary<string, object>? headers,
    DeliveryModes? deliveryMode,
    byte? priority,
    string? correlationId,
    string? replyTo,
    string? expiration,
    string? messageId,
    AmqpTimestamp? timestamp,
    string? type,
    string? userId,
    string? appId,
    string? clusterId)
{
    return AmqpResults.Ack();
}
```

O binding do objeto inteiro deve continuar existindo para cenarios avancados:

```csharp
public IAmqpResult Handle(
    MyMessage message,
    IReadOnlyBasicProperties properties)
{
    return AmqpResults.Ack();
}
```

Expandir `[FromAmqpHeader]` para converter o valor do header para o tipo do parametro.

Tipos minimos:

- `string`;
- `byte[]`;
- `byte`;
- `int`;
- `long`;
- `bool`;
- nullable equivalents.

Criar helper publico:

```csharp
AmqpHeaders.Get<T>(IReadOnlyBasicProperties properties, string key);
AmqpHeaders.GetDeliveryCount(IReadOnlyBasicProperties properties);
AmqpHeaders.GetPriority(IReadOnlyBasicProperties properties);
```

Manter e documentar os binders por convencao ja existentes:

```csharp
byte? priority
int? priority
long? priority
int? deliveryCount
long? deliveryCount
int? attempts
long? attempts
```

### Problema que esta sendo resolvido

RabbitMQ entrega metadados de mensagem em `BasicProperties`.

Dentro dessas propriedades existem campos AMQP conhecidos, como `ContentType`, `DeliveryMode`, `Priority`, `CorrelationId` e `MessageId`. Tambem existe `Headers`, que e um dicionario para metadados arbitrarios.

Essas duas coisas precisam ser tratadas de forma diferente:

- propriedades AMQP conhecidas devem ter binding por convencao;
- headers arbitrarios devem continuar usando `[FromAmqpHeader]` ou helpers de headers.

Na pratica, valores dentro de `Headers` podem chegar como tipos diferentes dependendo da origem da mensagem, do client, da fila e da propriedade.

Exemplos comuns:

- `x-delivery-count` pode ser lido como `long` em quorum queues;
- alguns produtores podem publicar numeros como `int`, `long`, `byte` ou string numerica;
- headers textuais podem chegar como `string` ou `byte[]`;
- headers ausentes podem significar "primeira entrega" ou "fila que nao suporta esse header".

Sem uma superficie estavel, cada cliente precisa fazer casts manuais:

```csharp
var attempts = (long)context.Request.BasicProperties.Headers["x-delivery-count"];
```

Esse padrao e fragil. Ele quebra quando o header nao existe, quando o tipo real nao e exatamente `long`, ou quando a mensagem vem de outro produtor.

Para attention, essa fragilidade impacta diretamente retry, DLQ e prioridade. O worker precisa decidir se uma mensagem interna deve voltar para fila, ir para DLQ ou preservar prioridade ao republicar atencao.

### API esperada

O handler deve conseguir receber metadados de quatro formas.

Por binding de propriedade AMQP conhecida:

```csharp
public IAmqpResult Handle(
    MyMessage message,
    byte? priority,
    string? correlationId,
    string? messageId,
    string? replyTo)
{
    return AmqpResults.Ack();
}
```

Por atributo explicito:

```csharp
public IAmqpResult Handle(
    MyMessage message,
    [FromAmqpHeader("x-delivery-count")] long? attempts,
    [FromAmqpHeader("source")] string source)
{
    return attempts >= 3
        ? AmqpResults.Nack(false)
        : AmqpResults.Reject(true);
}
```

Por convencao de nome para campos conhecidos:

```csharp
public IAmqpResult Handle(
    MyMessage message,
    long? attempts,
    byte? priority)
{
    return AmqpResults.Ack();
}
```

Por helper quando o handler precisar tomar decisoes dinamicas:

```csharp
long? attempts = AmqpHeaders.GetDeliveryCount(context.Request.BasicProperties);
byte? priority = AmqpHeaders.GetPriority(context.Request.BasicProperties);
string? source = AmqpHeaders.Get<string>(context.Request.BasicProperties, "source");
```

Pelo objeto inteiro:

```csharp
IReadOnlyBasicProperties properties = context.Request.BasicProperties;
```

### Propriedades AMQP com binding por convencao

Os campos abaixo devem virar superficie estavel de binding:

| Parametro | Origem |
| --- | --- |
| `contentType` | `BasicProperties.ContentType` |
| `contentEncoding` | `BasicProperties.ContentEncoding` |
| `headers` | `BasicProperties.Headers` |
| `deliveryMode` | `BasicProperties.DeliveryMode` |
| `priority` | `BasicProperties.Priority` |
| `correlationId` | `BasicProperties.CorrelationId` |
| `replyTo` | `BasicProperties.ReplyTo` |
| `expiration` | `BasicProperties.Expiration` |
| `messageId` | `BasicProperties.MessageId` |
| `timestamp` | `BasicProperties.Timestamp` |
| `type` | `BasicProperties.Type` |
| `messageType` | `BasicProperties.Type` |
| `userId` | `BasicProperties.UserId` |
| `appId` | `BasicProperties.AppId` |
| `clusterId` | `BasicProperties.ClusterId` |

`messageType` deve existir como alias recomendado para `Type`, porque `type` e um nome generico e pode colidir com intencao de dominio. `type` ainda pode ser suportado por ergonomia.

### Tipos aceitos por propriedade

Os tipos aceitos devem ser explicitos para evitar comportamento magico demais.

| Propriedade | Tipos aceitos |
| --- | --- |
| `ContentType` | `string?` |
| `ContentEncoding` | `string?` |
| `Headers` | `IDictionary<string, object>?`, `IReadOnlyDictionary<string, object>?` |
| `DeliveryMode` | `DeliveryModes`, `byte`, `int`, `long` |
| `Priority` | `byte`, `int`, `long` |
| `CorrelationId` | `string?` |
| `ReplyTo` | `string?` |
| `Expiration` | `string?` |
| `MessageId` | `string?`, `Guid?` |
| `Timestamp` | `AmqpTimestamp`, `DateTimeOffset?`, `long` |
| `Type` | `string?` |
| `UserId` | `string?` |
| `AppId` | `string?` |
| `ClusterId` | `string?` |

Conversoes de conveniencia precisam ser documentadas e testadas:

- `Expiration` e nativo como string em milissegundos no RabbitMQ. Suporte a `TimeSpan` pode ser adicionado, mas deve converter explicitamente para/desde milissegundos.
- `Timestamp` e nativo como AMQP timestamp. Suporte a `DateTimeOffset` deve converter usando Unix time em segundos.
- `MessageId` pode ser `string` quando o valor livre AMQP precisa ser preservado, ou `Guid?` quando o handler so quer valores GUID; nesse caso valor ausente ou nao-GUID retorna `null`.

### Regras de conversao

`[FromAmqpHeader]` e `AmqpHeaders.Get<T>` devem aplicar regras previsiveis:

- se o header nao existe e o tipo e nullable, retornar `null`;
- se o header nao existe e o tipo nao e nullable, retornar o default do tipo ou falhar de forma documentada conforme decisao de implementacao;
- se o valor ja e do tipo esperado, retornar diretamente;
- se o destino e `string` e o valor e `byte[]`, converter usando UTF-8;
- se o destino e `byte[]` e o valor e `string`, converter usando UTF-8;
- se o destino e numerico, aceitar valores numericos compativeis e strings numericas;
- se o destino e nullable, converter para o tipo interno;
- se a conversao nao for possivel, falhar com mensagem clara indicando header, tipo real e tipo esperado.

Para evitar comportamento surpreendente, a decisao recomendada e:

- atributo explicito com tipo nao nullable e header ausente deve falhar na validacao/execucao do binder;
- helper `Get<T>` pode ter overload com default:

```csharp
AmqpHeaders.Get<T>(properties, key);
AmqpHeaders.GetOrDefault<T>(properties, key, defaultValue);
```

### Priority

Priority deve ser tratada como propriedade AMQP conhecida, nao como header arbitrario.

O Oragon permite bind por convencao para parametros chamados `priority` em `byte?`, `int?` ou `long?`. O milestone deve documentar isso como contrato publico e adicionar helper equivalente.

Comportamento esperado:

- `byte? priority` recebe `BasicProperties.Priority` quando a propriedade existe;
- `int? priority` recebe o mesmo valor convertido para `int` quando a propriedade existe;
- `long? priority` recebe o mesmo valor convertido para `long` quando a propriedade existe;
- quando a propriedade nao existe, o binding retorna `null`;
- parametros nao-nullable para `priority` falham no startup para evitar `0` como falso sinal de presenca;
- quando a mensagem for republicada por `RequeueToTail` ou `Forward`, a prioridade deve poder ser preservada ou sobrescrita explicitamente.

### Copia de BasicProperties em publish/requeue

Os novos results de republicacao devem permitir preservar propriedades da mensagem original de forma explicita.

Exemplo conceitual:

```csharp
return AmqpResults.RequeueToTail(options =>
{
    options.CopyProperties = AmqpPropertyCopy.MessageIdentity
                           | AmqpPropertyCopy.Priority
                           | AmqpPropertyCopy.Headers
                           | AmqpPropertyCopy.Reply;
});
```

Grupos recomendados:

| Grupo | Propriedades |
| --- | --- |
| `MessageId` | `MessageId` |
| `CorrelationId` | `CorrelationId` |
| `Type` | `Type` |
| `MessageIdentity` | `MessageId`, `CorrelationId`, `Type` |
| `Content` | `ContentType`, `ContentEncoding` |
| `Headers` | `Headers` |
| `Priority` | `Priority` |
| `Reply` | `ReplyTo` |
| `Expiration` | `Expiration` |
| `Timestamp` | `Timestamp` |
| `UserId` | `UserId` |
| `AppId` | `AppId` |
| `ClusterId` | `ClusterId` |
| `Producer` | `UserId`, `AppId`, `ClusterId` |
| `RequeueToTailDefault` | Cópia integral: `MessageIdentity`, `Content`, `Headers`, `DeliveryMode`, `Priority`, `Reply`, `Expiration`, `Timestamp`, `AppId`, `ClusterId` (tudo, exceto `UserId`) |

Decisão final (2026-07-05): o requeue republica a mesma mensagem lógica, então o default é **cópia integral** — a mensagem deve continuar contando a história inteira, incluindo o histórico de dead-letter (`x-death`, `x-first-death-*`, `x-last-death-*`), que o próprio broker acumula por `{Queue, Reason}` através de ciclos. Precedente: Spring AMQP `RepublishMessageRecoverer` copia tudo e apenas adiciona headers. Existem exatamente duas exceções, ambas técnicas:

- `UserId` não é copiado por default: o validated-user-id do RabbitMQ exige que o valor seja igual ao usuário da conexão publicadora; copiar de outro produtor falha o publish (exceto com a tag `impersonator`). Opt-in via flag `UserId` ou `AllApplicationProperties`.
- O header `x-delivery-count` não é copiado: é estado de entrega de quorum queue (alimenta o `delivery-limit` do broker e o `AmqpRetryPolicy.ByDeliveryCount`); carregá-lo num publish novo simularia falhas de entrega que não ocorreram.

Caveat documentado: `Expiration` é copiado, mas o relógio de TTL reinicia quando a nova cópia entra na fila (TTL por mensagem conta a partir da entrada na fila). Flags mais restritos permanecem disponíveis como opt-out para fluxos que queiram resetar partes da mensagem deliberadamente.

### Attempts / delivery count

Attempts deve ser um alias de leitura para o header RabbitMQ `x-delivery-count`.

O Oragon ja reconhece parametros chamados `deliveryCount` e `attempts`. O milestone deve estabilizar essa convencao e expor helper publico.

Comportamento esperado:

- `long? attempts` retorna `null` quando o header nao existe;
- `int? attempts` retorna `null` quando o header nao existe;
- parametros nao-nullable para `attempts` ou `deliveryCount` falham no startup para evitar `0` como falso sinal de presenca;
- valores `int`, `long` e strings numericas devem ser aceitos quando seguros.

Essa decisao de exigir nullable no binding por convencao e importante:

- nullable permite diferenciar "sem header" de "header com zero";
- nao nullable mascara ausencia com `0` e pode transformar classic queues em loops ou decisoes incorretas.

### Relacao com retry

Retry policy deve usar a mesma leitura estabilizada de attempts.

Exemplo:

```csharp
public IAmqpResult Handle(MyMessage message, long? attempts)
{
    return attempts >= 3
        ? AmqpResults.Nack(false)
        : AmqpResults.Reject(true);
}
```

Para quorum queues, a documentacao deve explicar que `x-delivery-count` e gerenciado pelo broker e nao deve ser incrementado manualmente pela aplicacao. A aplicacao deve apenas le-lo para decidir o resultado.

### Comportamento adicionado

Handlers podem receber headers tipados sem acessar manualmente `BasicProperties.Headers`.

Exemplo:

```csharp
public IAmqpResult Handle(
    MyMessage message,
    [FromAmqpHeader("x-delivery-count")] long? attempts)
{
    return attempts >= 3 ? AmqpResults.Nack(false) : AmqpResults.Reject(true);
}
```

### Efeito na library

Diminui casts frageis e erros de runtime em headers vindos do RabbitMQ. Isso e especialmente importante para quorum queues, onde `x-delivery-count` orienta retry e DLQ.

## 4. Resultado explicito para reprogramar no fim da fila

### Implementacao

Adicionar um result explicito:

```csharp
AmqpResults.RequeueToTail();
AmqpResults.RequeueToTail(options => { ... });
AmqpResults.RequeueToTail(queueName);
AmqpResults.RequeueToTail(queueName, options => { ... });
```

O comportamento deve ser:

1. publicar uma nova mensagem no exchange default `""`;
2. usar como routing key a fila atual (`context.QueueName`) ou fila informada;
3. preservar propriedades relevantes quando fizer sentido, como `CorrelationId`, `Priority` e headers;
4. nao executar `Ack`, `Nack` ou `Reject` na entrega original.

O milestone atual republica o corpo bruto da entrega atual. Um overload generico
para publicar outro payload pode ser reavaliado depois, mas nao e requisito para
attention porque o fluxo normal precisa reprogramar a propria mensagem de
atencao. Quando a aplicacao precisa publicar outro payload, `Forward`,
`ForwardAndAck` ou `Compose(Forward(...), Ack())` continuam sendo o caminho
explicito.

### Comportamento adicionado

Clientes passam a ter uma forma declarativa para "nao processar agora, voltar ao final da fila".

Isso difere de `Nack(requeue:true)`, que devolve a mensagem ao broker conforme a semantica do RabbitMQ e pode causar redelivery imediato ou unfairness.

O nome `RequeueToTail` deve ser documentado com cuidado: tecnicamente ele nao usa `basic.nack requeue`. Ele faz apenas `publish` de uma nova mensagem. Quando o fluxo tambem deve confirmar a entrega original, o usuario deve compor explicitamente `Compose(RequeueToTail(), Ack())`.

### Efeito na library

O padrao usado pelo MessageFy com `Compose(Forward("", mesmaFila), Ack())` ganha uma primitiva nomeada para a etapa de republicacao. A confirmacao da entrega original continua explicita via `Ack`.

## 5. Publish confiavel e flexivel para results

### Implementacao

Preservar `Forward` atual e garantir que results de publish usados antes de um
ack composto usem um caminho confiavel: channel dedicado, publisher confirmations
quando o result depende do publish, `mandatory` quando aplicavel e falha antes
do ack original.

Uma primitiva publica compartilhada de publish pode ser avaliada em milestone
futuro, mas nao e obrigatoria para este milestone. A necessidade atual e o
comportamento operacional seguro dos results existentes.

Possivel contrato futuro:

```csharp
public interface IAmqpPublisher
{
    Task PublishAsync(AmqpPublishRequest request, CancellationToken cancellationToken);
}
```

Ou uma opcao no descriptor/result:

```csharp
descriptor.WithPublisher(...);
```

Requisitos do comportamento entregue nos results:

- manter canal dedicado para publish;
- permitir publisher confirmations quando o result precisa publicar antes de ack;
- tratar `mandatory`/unrouted como falha quando configurado;
- devolver erro claro quando publish falhar antes do ack da mensagem original;
- respeitar cancellation token quando disponivel;
- permitir configuracao completa de `BasicProperties` onde a API do result ja
  expoe configuracao;
- permitir politica explicita de copia de `IReadOnlyBasicProperties` para
  `BasicProperties` no `RequeueToTail`;
- preservar defaults atuais de `DeliveryMode`, `MessageId` e `CorrelationId`.

Para results compostos como `Forward + Ack` ou `RequeueToTail + Ack`, a ordem operacional deve ser:

```text
publish nova mensagem
  -> confirmar sucesso ou tratar falha
  -> somente entao ackar mensagem original
```

Se o publish falhar dentro de uma composicao como `Compose(RequeueToTail(), Ack())`, o ack da mensagem original nao deve acontecer por acidente. A politica de falha deve passar por `WhenResultExecutionFail`.

### Comportamento adicionado

Clientes com multiplas conexoes ou keyed services continuam usando a connection
do `IAmqpContext` para os results padrao ou results customizados quando precisam
de um caminho de publish especifico.

Clientes tambem passam a ter um caminho seguro para padroes que dependem de "publish antes de ack", como reprogramar atencao ao final da fila.

### Efeito na library

Cobre a necessidade que levou o MessageFy a ter uma variacao de `Forward` mais avancada. O caminho simples continua igual para quem usa uma unica conexao.

## 6. Dynamic Queue Consumer Primitive

### Implementacao

Criar uma primitiva de consumo dinamico sob demanda.

Nome recomendado:

```csharp
IAmqpDynamicQueueConsumer
```

O contrato publico deve ficar em `Oragon.RabbitMQ.Abstractions`. A implementacao default deve ficar no pacote `Oragon.RabbitMQ`.

Contrato conceitual:

```csharp
public interface IAmqpDynamicQueueConsumer
{
    Task<DynamicQueueConsumeResult> ConsumeAsync<T>(
        DynamicQueueConsumeRequest<T> request,
        CancellationToken cancellationToken);
}
```

Request conceitual (contrato final implementado):

```csharp
public sealed class DynamicQueueConsumeRequest<T>
{
    public required string QueueName { get; init; }
    public IConnection Connection { get; init; }
    public Func<IServiceProvider, CancellationToken, ValueTask<IConnection>> ConnectionFactory { get; init; }
    public Func<IServiceProvider, IConnection, CancellationToken, ValueTask<IChannel>> ChannelFactory { get; init; }
    public ushort PrefetchCount { get; init; } = 1;
    public int? MaxMessages { get; init; }
    public TimeSpan? MaxDuration { get; init; }
    public TimeSpan? IdleTimeout { get; init; }
    public bool StopAfterInitialQueueLength { get; init; }
    public ushort MaxLocalConcurrency { get; init; } = 1;
    public TimeSpan InFlightDrainTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public IReadOnlyDictionary<string, object> Metadata { get; init; }
    public Func<DynamicQueueStartContext, CancellationToken, ValueTask<DynamicQueueStartDecision>> BeforeStartAsync { get; init; }
    public Func<DynamicQueueStopContext, CancellationToken, ValueTask> AfterStopAsync { get; init; }
    public required Func<T, IAmqpContext, Task<IAmqpResult>> OnMessageAsync { get; init; }
}
```

Contextos e decisao de extensibilidade conceituais:

```csharp
public sealed record DynamicQueueStartContext(
    string QueueName,
    long? InitialReadyCount,
    IServiceProvider Services,
    IReadOnlyDictionary<string, object?> Metadata);

public enum DynamicQueueStartDecisionType
{
    Allow,
    Skip,
    Defer,
    Fail
}

public sealed record DynamicQueueStartDecision(
    DynamicQueueStartDecisionType Type,
    TimeSpan? SuggestedDelay = null,
    Exception? Exception = null)
{
    public static DynamicQueueStartDecision Allow() =>
        new(DynamicQueueStartDecisionType.Allow);

    public static DynamicQueueStartDecision Skip() =>
        new(DynamicQueueStartDecisionType.Skip);

    public static DynamicQueueStartDecision Defer(TimeSpan? suggestedDelay = null) =>
        new(DynamicQueueStartDecisionType.Defer, suggestedDelay);

    public static DynamicQueueStartDecision Fail(Exception exception) =>
        new(DynamicQueueStartDecisionType.Fail, Exception: exception);
}

public sealed record DynamicQueueStopContext(
    DynamicQueueConsumeResult Result,
    IServiceProvider Services,
    IReadOnlyDictionary<string, object?> Metadata);
```

Status conceitual:

```csharp
public enum DynamicQueueConsumeStatus
{
    Completed,
    Empty,
    Skipped,
    Deferred,
    QueueMissing,
    MaxMessagesReached,
    MaxDurationReached,
    IdleTimeoutReached,
    InitialQueueLengthReached,
    Interrupted,
    Faulted
}
```

Resultado conceitual:

```csharp
public sealed record DynamicQueueConsumeResult
{
    public required DynamicQueueConsumeStatus Status { get; init; }
    public required string QueueName { get; init; }
    public long? InitialReadyCount { get; init; }
    public long? RemainingReadyCount { get; init; }
    public int MessagesReceived { get; init; }
    public int MessagesAcked { get; init; }
    public int MessagesNacked { get; init; }
    public int MessagesRejected { get; init; }
    public TimeSpan Elapsed { get; init; }
    public bool BrokerCanceledConsumer { get; init; }
    public bool InFlightDrainTimedOut { get; init; }
    public Exception? Exception { get; init; }
}
```

O implementation deve:

- abrir canal dedicado;
- fazer `QueueDeclarePassiveAsync`;
- retornar `QueueMissing` quando a fila nao existir;
- capturar a quantidade inicial da fila quando a regra de total inicial estiver habilitada;
- detectar fila vazia sem tratar como erro;
- criar `AsyncEventingBasicConsumer`;
- aplicar `BasicQos`;
- processar ate bater qualquer regra de parada habilitada: quantidade de mensagens, tempo total, tempo ocioso, total inicial da fila ou cancelamento;
- permitir ack, reject, nack requeue e nack terminal por mensagem;
- executar hooks opcionais como `BeforeStartAsync` e `AfterStopAsync`, sem assumir regra de negocio;
- respeitar a decisao de `BeforeStartAsync`: `Allow` continua, `Skip` encerra sem abrir consumo, `Defer` encerra indicando adiamento, `Fail` encerra como falha;
- chamar `BasicCancelAsync` ao encerrar;
- aguardar mensagens in-flight locais;
- fechar canal com seguranca.

Regras de parada:

- `MaxMessages`: encerra depois de processar N mensagens com decisao terminal para cada mensagem recebida.
- `MaxDuration`: encerra depois de X tempo total desde o inicio do ciclo de consumo.
- `IdleTimeout`: encerra quando o consumer fica sem receber novas entregas por X tempo. A implementacao deve documentar se o tempo e contado desde a ultima entrega recebida ou desde a ultima finalizacao de mensagem; a recomendacao e nao disparar idle enquanto houver in-flight local.
- `StopAfterInitialQueueLength`: encerra depois de processar a quantidade de mensagens ready informada pelo passive declare inicial. Esse numero e um snapshot de mensagens ready no inicio, nao inclui unacked e pode mudar com producers concorrentes. Se o snapshot inicial for zero, o status final deve ser `Empty`.
- `CancellationToken`: sempre pode interromper o consumo por shutdown ou decisao externa.

As regras devem ser combinaveis. Nenhuma regra especifica e obrigatoria, mas a request deve ter ao menos um mecanismo de parada efetivo: `MaxMessages`, `MaxDuration`, `IdleTimeout`, `StopAfterInitialQueueLength` ou um `CancellationToken` que possa ser cancelado pelo chamador. Se nenhuma regra de parada for configurada e o token nao puder ser cancelado, a library deve rejeitar a configuracao com erro de validacao.

`MaxMessages` sozinho e uma regra valida, mas ela so encerra quando N entregas
chegam e terminam. Se a fila pode ficar quieta antes de atingir N, o usuario
deve combinar com `IdleTimeout`, `MaxDuration` ou `StopAfterInitialQueueLength`.
Esse detalhe existe justamente para que o tempo ocioso seja uma decisao
explicita, nao um timeout escondido.

O `CancellationToken` entregue em `DynamicQueueMessageContext` deve ser cancelado
quando o token externo for cancelado ou quando uma regra interna de parada
encerrar o ciclo. Assim, handlers cooperativos conseguem sair de chamadas longas
quando `MaxDuration`, shutdown ou outra decisao de parada for atingida.

O consumer dinamico fecha o canal criado para o ciclo. Conexoes recebidas via
`Connection`, `ConnectionFactory`, contexto AMQP atual ou DI continuam sendo da
aplicacao e nao devem ser fechadas pela primitiva.

### Comportamento adicionado

Um handler pode consumir uma fila escolhida em runtime por uma fatia controlada.

Isso e o miolo tecnico do attention worker, mas nao contem semantica de attention. A aplicacao ainda decide:

- como resolver a fila;
- como validar o recurso;
- como aplicar lock, rate limit ou regra de negocio antes do consumo;
- quando retornar `Ack`;
- quando retornar `RequeueToTail`.

### Efeito na library

Evita que cada cliente implemente manualmente `AsyncEventingBasicConsumer`, passive declare, ack/nack, cancelamento e drain local.

Esse bloco permite implementar Attention com `MapQueue`, mas permanece util fora de attention. Um futuro `MapAttentionQueue(...)` deixa de ser uma meta natural do milestone e deve ser reavaliado somente se as primitivas nao forem suficientes.

## 7. Gate generico opcional para concorrencia

### Implementacao

Adicionar um ponto de extensao generico para a aplicacao controlar se um ciclo dinamico pode iniciar. Esse ponto nao deve conhecer attention, canal, tenant, loja ou lifecycle de dominio.

Contrato conceitual:

```csharp
public interface IAmqpConcurrencyGate
{
    ValueTask<IAmqpConcurrencyLease> TryAcquireAsync(
        AmqpConcurrencyGateRequest request,
        CancellationToken cancellationToken);
}

public sealed record AmqpConcurrencyGateRequest(
    string Key,
    TimeSpan LeaseTime,
    IReadOnlyDictionary<string, object?> Metadata);

public interface IAmqpConcurrencyLease : IAsyncDisposable
{
    bool Acquired { get; }
    string Key { get; }
}
```

Esses contratos devem ficar em `Oragon.RabbitMQ.Abstractions`. O core nao deve implementar Redis nem qualquer provider distribuido oficial neste milestone.

Uma implementacao Redis pode existir no codigo da aplicacao ou em exemplos, mas nao como pacote oficial deste milestone. A chave deve ser sempre informada pelo usuario:

```csharp
await using var lease = await gate.TryAcquireAsync(
    new AmqpConcurrencyGateRequest(
        Key: $"attention:{attention.Type}:{attention.ChannelId:D}",
        LeaseTime: TimeSpan.FromSeconds(30),
        Metadata: new Dictionary<string, object?>
        {
            ["queue"] = queueName,
            ["attentionId"] = attention.Id
        }),
    cancellationToken);
```

Essa API tambem pode ser chamada dentro de `BeforeStartAsync` do `IAmqpDynamicQueueConsumer`, permitindo que a aplicacao decida `Allow`, `Skip`, `Defer` ou `Fail` antes de abrir o consumer.

O contexto do hook deve expor `IServiceProvider` para que a aplicacao resolva a tecnologia escolhida sem criar dependencia no core:

```csharp
BeforeStartAsync = async (startContext, cancellationToken) =>
{
    var gate = startContext.Services.GetRequiredService<IAmqpConcurrencyGate>();

    await using var lease = await gate.TryAcquireAsync(
        new AmqpConcurrencyGateRequest(
            Key: $"attention:{attention.Type}:{attention.ChannelId:D}",
            LeaseTime: TimeSpan.FromSeconds(30),
            Metadata: startContext.Metadata),
        cancellationToken);

    return lease.Acquired
        ? DynamicQueueStartDecision.Allow()
        : DynamicQueueStartDecision.Defer(TimeSpan.FromSeconds(5));
}
```

O pacote core deve expor apenas contratos e hooks. Controlar a versao do provider Redis fica fora da responsabilidade do Oragon.RabbitMQ.

### Limites explicitos

A library nao deve implementar locks de ciclo de vida ou semantica de negocio como:

```text
channel-lifecycle:{channelId}
```

Esse tipo de lock pertence a aplicacao. A library pode prover o mecanismo generico no core e implementacoes tecnologicas em pacotes separados; a decisao de chave, TTL, renovacao, fallback, prioridade e acao quando bloqueado continua sendo da aplicacao.

### Comportamento adicionado

Clientes ganham uma forma padronizada de plugar Redis, banco ou outro controle de concorrencia sem que o Oragon.RabbitMQ core assuma dependencia tecnologica ou semantica de dominio.

### Efeito na library

O suporte a attention fica mais simples para usuarios que precisam limitar consumo por recurso, mas o core continua util para outros padroes de consumo dinamico e continua livre de dependencia direta de Redis.

## 8. Retry policy por attempts

### Implementacao

Adicionar helper/policy baseada em delivery count:

```csharp
var policy = AmqpRetryPolicy.ByDeliveryCount(maxAttempts: 3);
```

A policy deve ler `x-delivery-count` quando existir e retornar um `IAmqpResult` ou uma decisao reutilizavel.

Deve documentar explicitamente a diferenca entre `Reject(requeue:true)` e `Nack(requeue:true)` para quorum queues, pois o delivery count pode se comportar de forma diferente conforme a operacao.

### Comportamento adicionado

Clientes conseguem expressar "tentar ate N vezes, depois DLQ" sem reimplementar leitura de headers e regras de fallback.

### Efeito na library

Reduz risco de loop infinito e padroniza retry para filas dinamicas e consumers fixos.

## 9. Helpers e diagnostico de topologia

### Implementacao

Adicionar helpers combinaveis para argumentos de fila:

```csharp
QueueArguments.Quorum();
QueueArguments.SingleActiveConsumer();
QueueArguments.WithDeadLetter(exchange, routingKey);
QueueArguments.WithMaxPriority(10);
```

Adicionar diagnostico nao destrutivo quando possivel:

- argumento esperado ausente;
- tipo de fila diferente;
- DLQ diferente;
- SAC esperado mas ausente;
- priority esperado mas ausente.

O diagnostico deve apenas reportar inconsistencias. A library nao deve migrar automaticamente filas existentes para SAC, quorum, DLQ ou priority, porque argumentos imutaveis podem exigir recriacao de fila, drain operacional, janela de manutencao ou regra de negocio.

Pontos de extensao podem permitir que a aplicacao execute uma acao propria quando uma inconsistencia for encontrada, mas essa acao deve ser opt-in e fornecida pelo usuario.

### Comportamento adicionado

Clientes declaram topologias comuns com menos boilerplate e conseguem detectar inconsistencias.

### Efeito na library

Ajuda cenarios como MessageFy, que usa quorum, DLQ e `x-single-active-consumer`, sem colocar migracao automatica, locks de lifecycle ou decisao de negocio dentro do core.

## Sequencia recomendada de implementacao

1. Criar testes de caracterizacao para o comportamento atual de `QueueConsumer`, `ConsumerServer`, binders e results.
2. Implementar graceful shutdown opt-in.
3. Implementar `WhenResultExecutionFail`.
4. Expandir headers tipados e helpers publicos.
5. Implementar `RequeueToTail`.
6. Introduzir publish confiavel e flexivel sem quebrar `Forward`.
7. Implementar `IAmqpDynamicQueueConsumer`.
8. Adicionar contratos de gate em `Oragon.RabbitMQ.Abstractions` e hooks com `IServiceProvider`; Redis apenas em exemplos ou implementacao do cliente.
9. Implementar retry policy por attempts.
10. Implementar helpers/diagnostico de topologia, sem migracao automatica.
11. Criar exemplo documentado de attention usando as primitivas, sem criar modulo opinativo.

## Test plan

### Unit tests

- `StopAsync` sem graceful mantem comportamento atual.
- `StopAsync` com graceful cancela token do contexto e espera in-flight.
- `StopAsync` com graceful usa token interno para `BasicCancelAsync` e drain, mesmo quando o token do host e cancelado antes do timeout configurado.
- Timeout de graceful shutdown registra log e retorna.
- `WhenResultExecutionFail` usa default `Nack(false)`.
- `WhenResultExecutionFail` permite politica custom.
- `[FromAmqpHeader]` converte string, byte array, inteiros, long, bool e nullable.
- Helpers de `AmqpHeaders` retornam valores corretos para headers ausentes e presentes.
- `RequeueToTail` publica sem ack implicito.
- `RequeueToTail` preserva priority, correlation e headers configurados.
- Publish primitive falha antes do ack quando confirmacao de publish ou `mandatory`/unrouted falhar.
- Copy policy de BasicProperties nao copia headers de broker por default.
- Contrato/hook de gate repassa a chave fornecida pela aplicacao e nao conhece nomes de dominio.
- Hook de extensibilidade expoe `IServiceProvider` para resolver provider Redis, banco ou outra tecnologia escolhida pelo cliente.
- Hooks `BeforeStartAsync` e `AfterStopAsync` sao chamados nos pontos esperados do consumer dinamico.
- Retry policy retorna requeue antes do limite e terminal depois do limite.
- Queue arguments combinam quorum, SAC, DLQ e priority.

### Integration tests

- Consumer graceful para de receber novas mensagens apos `StopAsync`.
- Mensagens in-flight terminam antes do shutdown quando possivel.
- Dynamic queue consumer retorna `QueueMissing` para fila ausente.
- Dynamic queue consumer retorna `Empty` para fila vazia.
- Dynamic queue consumer respeita `MaxMessages`.
- Dynamic queue consumer respeita `MaxDuration`.
- Dynamic queue consumer respeita `IdleTimeout`.
- Dynamic queue consumer nao dispara `IdleTimeout` enquanto houver mensagem local in-flight.
- Dynamic queue consumer respeita `StopAfterInitialQueueLength`.
- Dynamic queue consumer trata `StopAfterInitialQueueLength` como snapshot de ready count inicial.
- Dynamic queue consumer permite operar com apenas uma regra de parada habilitada.
- Dynamic queue consumer permite combinar varias regras e encerra na primeira regra atingida.
- Dynamic queue consumer rejeita configuracao sem nenhum mecanismo efetivo de parada.
- Dynamic queue consumer retorna `Interrupted` quando cancellation token e cancelado.
- Dynamic queue consumer retorna contadores e motivo final coerentes em `DynamicQueueConsumeResult`.
- `BeforeStartAsync` consegue permitir, adiar, pular ou falhar o ciclo antes de abrir consumo.
- Dynamic queue consumer retorna `Skipped` ou `Deferred` quando `BeforeStartAsync` pedir esse encerramento.
- `AfterStopAsync` recebe o resultado final mesmo quando o ciclo encerra por timeout, cancelamento ou fila vazia.
- Hook com gate fake ou implementacao de exemplo limita concorrencia usando chave definida pela aplicacao.
- Nao ha implementacao Redis oficial neste milestone; exemplos podem demonstrar uma implementacao do cliente.
- Retry por `x-delivery-count` funciona em quorum queue.
- `Compose(RequeueToTail(), Ack())` coloca a mensagem no final da fila e confirma a entrega original.
- Diagnostico de topologia reporta diferencas sem deletar, recriar ou migrar filas.

## Fora do escopo deste milestone

- Implementar `MapAttentionQueue(...)`.
- Definir contratos de dominio como `AttentionRequest` obrigatorio.
- Implementar locks de dominio ou lifecycle dentro da library.
- Definir chaves de lock/rate limit pela library.
- Implementar rate limit distribuido opinativo dentro da library.
- Implementar pacote Redis oficial.
- Implementar provider Redis no core.
- Migrar filas existentes para SAC/quorum/DLQ.
- Orquestrar migracao para Single Active Consumer como comportamento da library.
- Deletar e recriar filas com argumentos imutaveis.
- Acoplar APIs aos tipos do MessageFy.

## Resultado esperado

Ao final do milestone, o Oragon.RabbitMQ deve permitir que o MessageFy remova parte significativa do codigo manual de consumo temporario e shutdown cooperativo, enquanto outros clientes ganham primitivas genericas para filas dinamicas, retry, headers e publish controlado.

Um milestone futuro so deve considerar um modulo `Attention` opinativo, como `MapAttentionQueue(...)`, se as primitivas deste milestone se mostrarem insuficientes para casos reais.
