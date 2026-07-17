# Attention Queue: especificação técnica para Oragon.RabbitMQ

> Status: documento conceitual/histórico.
>
> Este arquivo descreve uma proposta opinativa de Attention Queue e contém ideias úteis, mas não é mais o contrato do milestone atual. O roadmap ativo está em `spec/attention/README.md` e `spec/attention/milestone-roadmap.md`.
>
> Decisão atual: entregar primitivas genéricas (`MapQueue` com graceful shutdown, `IAmqpDynamicQueueConsumer`, publish confiável, `RequeueToTail`, bindings e gates opcionais) em vez de implementar `MapAttentionQueue(...)` neste milestone. Não haverá pacote Redis oficial neste milestone; Redis pode aparecer apenas em exemplos como implementação do cliente. O core deve permitir implementação do usuário via pontos de extensibilidade com `IServiceProvider`. Locks de domínio/lifecycle, migração para SAC/quorum/DLQ e orquestração de regras de negócio pertencem à aplicação.
>
> As APIs descritas no corpo deste documento (`MapAttentionQueue`, `IAttentionConcurrencyGate`, `AttentionConsumptionPolicy` etc.) foram substituídas pelas primitivas genéricas (`IAmqpDynamicQueueConsumer`, `IAmqpConcurrencyGate`, `AmqpResults.RequeueToTail`) e permanecem aqui apenas como registro histórico.

Este documento descreve o mecanismo Attention Queue em nível técnico. Ele foi
mantido como registro de desenho e fonte conceitual, não como plano de
implementação do milestone atual.

O foco aqui não é o artigo conceitual. O foco é transformar o padrão em um componente reutilizável, com contratos claros, pontos de extensão, comportamento previsível, observabilidade e compatibilidade com a forma como `Oragon.RabbitMQ` já trabalha com `MapQueue`, `IAmqpResult`, `AmqpResults`, `Forward`, `Ack`, `Nack`, `Reject`, `WithDispatchConcurrency` e `WithPrefetch`.

## Objetivo

O pacote deve permitir que uma aplicação publique mensagens reais em filas granulares e publique, de forma coordenada, um pedido de atenção em uma fila agregada. A fila de atenção será consumida por poucos workers permanentes. Cada pedido de atenção indica que uma fila granular precisa ser consumida por uma janela controlada de tempo, quantidade e concorrência.

O pacote deve resolver o problema de consumir muitas filas específicas sem manter um consumidor permanente para cada fila.

## Ideia central

O padrão separa duas responsabilidades:

| Conceito | Responsabilidade |
| --- | --- |
| Work queue | Armazena as mensagens reais de trabalho de uma entidade específica |
| Attention queue | Armazena pedidos pequenos indicando que alguma work queue precisa ser consumida |
| Attention worker | Consome a attention queue, localiza a work queue e abre consumo temporário |
| Consumption policy | Define tempo máximo, quantidade máxima, concorrência e comportamento de repetição |
| Concurrency gate | Impede que mais consumidores do que o permitido processem a mesma work queue |

Exemplo de nomes didáticos:

```text
process.marketplace.store_873.work
process.marketplace.store_873.error
attention.marketplace.work
attention.marketplace.error
```

Esses nomes não devem ser fixos no pacote. O pacote deve expor uma estratégia de nomes.

## Escopo do pacote

O pacote adicional de `Oragon.RabbitMQ` deve prover:

1. Contratos para representar pedidos de atenção.
2. Contratos para resolver a fila de trabalho a partir de um pedido de atenção.
3. Contratos para calcular política de consumo por pedido.
4. Infraestrutura para registrar um consumidor de attention queue.
5. Infraestrutura para abrir um consumidor temporário em uma work queue.
6. Resultado padronizado para decidir se a atenção terminou ou deve ser republicada.
7. Pontos de extensão para rate limit, locks, validação de estado e observabilidade.
8. Helpers para publicar mensagem de trabalho + pedido de atenção.
9. Helpers para declarar topologia, quando desejado.

O pacote não deve tentar resolver o domínio da aplicação. Ele deve orquestrar filas.

## Conceitos

### Work message

É a mensagem real da aplicação.

Exemplo:

```json
{
  "productId": "SKU-123",
  "storeId": "store-873",
  "quantity": 10,
  "operation": "inventory-update"
}
```

Essa mensagem fica na work queue.

### Attention request

É o envelope pequeno que informa que uma work queue precisa ser consumida.

Exemplo:

```json
{
  "tenantId": "seller-group-a",
  "storeId": "store-873",
  "resourceType": "marketplace-store",
  "priority": "normal"
}
```

Esse pedido fica na attention queue.

O pedido de atenção deve ser idempotente. Receber dois pedidos de atenção para a mesma fila não pode duplicar indevidamente o processamento. No pior caso, o segundo pedido encontra a fila vazia, é bloqueado por concorrência ou descobre que o backlog já foi processado.

### Attention cycle

É uma execução do worker de atenção para uma work queue específica.

Um ciclo:

1. Recebe um `AttentionRequest`.
2. Resolve a work queue correspondente.
3. Verifica se o recurso pode ser processado.
4. Aplica política de consumo.
5. Tenta adquirir permissão de concorrência.
6. Verifica se a work queue existe.
7. Verifica se há mensagens.
8. Abre um consumidor temporário.
9. Consome até bater limite de tempo, limite de quantidade ou fila vazia.
10. Fecha o consumidor.
11. Verifica se ainda há backlog.
12. Retorna `Done` ou `NeedMoreAttention`.

### Processing slice

O ciclo de atenção é uma fatia de processamento, inspirada em time sharing. O worker não deve tentar consumir a fila inteira indefinidamente. Ele deve consumir por uma janela limitada e devolver a fila para a disputa, se ainda houver backlog.

## Topologia

### Attention exchange

Exchange que recebe pedidos de atenção.

Exemplo:

```text
exchange: attention.marketplace
type: topic
durable: true
```

### Attention work queue

Fila agregada consumida por workers permanentes.

Exemplo:

```text
queue: attention.marketplace.work
durable: true
type: quorum
dead-letter: attention.marketplace.error
```

### Attention error queue

Fila de erro dos pedidos de atenção.

Exemplo:

```text
queue: attention.marketplace.error
durable: true
type: quorum
```

### Work exchange

Exchange que recebe mensagens reais.

Exemplo:

```text
exchange: process.marketplace
type: topic
durable: true
```

### Dynamic work queue

Fila específica por entidade/recurso.

Exemplo:

```text
queue: process.marketplace.store_873.work
durable: true
type: quorum
dead-letter: process.marketplace.store_873.error
```

### Dynamic error queue

Fila de erro específica por entidade/recurso.

Exemplo:

```text
queue: process.marketplace.store_873.error
durable: true
type: quorum
```

### Bindings

Exemplo:

```text
exchange: process.marketplace
queue: process.marketplace.store_873.work
routingKey: store.store-873

exchange: attention.marketplace
queue: attention.marketplace.work
routingKey: store.*
```

O pacote deve permitir que a aplicação defina:

1. Nome do exchange de trabalho.
2. Nome da work queue.
3. Nome da error queue.
4. Routing key de trabalho.
5. Nome do exchange de atenção.
6. Nome da attention queue.
7. Routing key de atenção.

## Contratos recomendados

### Attention request base

O pacote pode oferecer uma interface mínima em vez de classe base rígida.

```csharp
public interface IAttentionRequest
{
    string ResourceKey { get; }
}
```

`ResourceKey` deve identificar a unidade de isolamento. Exemplos:

```text
store:store-873
channel:7f5b5c90-74b7-4d98-82b2-3f4a2b4d9e44
account:acc-123
tenant:tenant-a
```

O pacote não deve exigir que a aplicação use esses nomes. Eles são convenções.

### Queue resolution

Responsável por transformar um pedido de atenção em nomes AMQP.

```csharp
public interface IAttentionQueueResolver<TAttention>
{
    ValueTask<AttentionQueueResolution> ResolveAsync(
        TAttention attention,
        CancellationToken cancellationToken);
}
```

```csharp
public sealed record AttentionQueueResolution
{
    public required string WorkQueueName { get; init; }
    public required string AttentionQueueName { get; init; }
    public string? WorkExchange { get; init; }
    public string? WorkRoutingKey { get; init; }
    public string? AttentionExchange { get; init; }
    public string? AttentionRoutingKey { get; init; }
    public string? ConcurrencyKey { get; init; }
    public string? ResourceKey { get; init; }
}
```

`ConcurrencyKey` deve permitir controlar concorrência por fila. Se não for informado, o pacote pode usar `WorkQueueName`.

### Policy provider

Responsável por calcular limites por pedido.

```csharp
public interface IAttentionConsumptionPolicyProvider<TAttention>
{
    ValueTask<AttentionConsumptionPolicy> GetPolicyAsync(
        TAttention attention,
        AttentionQueueResolution resolution,
        CancellationToken cancellationToken);
}
```

```csharp
public sealed record AttentionConsumptionPolicy
{
    public TimeSpan MaxConsumptionTime { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxMessages { get; init; } = 50;
    public int MaxConcurrentConsumers { get; init; } = 1;
    public TimeSpan DrainDelay { get; init; } = TimeSpan.FromSeconds(5);
    public ushort? PrefetchCount { get; init; }
    public bool RequeueOnMessageProcessingError { get; init; } = true;
    public bool RejectInvalidMessage { get; init; } = true;
}
```

Regras:

1. `MaxConsumptionTime` deve ser maior que zero.
2. `MaxMessages` deve ser maior que zero.
3. `MaxConcurrentConsumers` deve ser maior que zero.
4. Se `PrefetchCount` não for informado, pode usar `MaxMessages`.
5. `MaxConcurrentConsumers = 1` é caso válido e importante.

### Attention state gate

Permite descartar atenção quando o recurso não pode mais processar.

```csharp
public interface IAttentionReadinessGate<TAttention>
{
    ValueTask<AttentionReadinessResult> CanProcessAsync(
        TAttention attention,
        AttentionQueueResolution resolution,
        CancellationToken cancellationToken);
}
```

```csharp
public sealed record AttentionReadinessResult
{
    public bool CanProcess { get; init; }
    public string? Reason { get; init; }

    public static AttentionReadinessResult Allow() => new() { CanProcess = true };
    public static AttentionReadinessResult Deny(string reason) => new() { CanProcess = false, Reason = reason };
}
```

Exemplos de negação:

1. Loja desativada.
2. Canal removido.
3. Integração sem credencial.
4. Recurso em manutenção.
5. Tenant bloqueado.

Se `CanProcess = false`, o pacote deve retornar `Ack` para a attention message, não `Nack`, porque a atenção foi tratada e deve sair da fila.

### Concurrency gate

Controla quantos ciclos simultâneos podem processar a mesma work queue.

```csharp
public interface IAttentionConcurrencyGate
{
    ValueTask<AttentionConcurrencyLease> TryAcquireAsync(
        AttentionConcurrencyRequest request,
        CancellationToken cancellationToken);
}
```

```csharp
public sealed record AttentionConcurrencyRequest
{
    public required string Key { get; init; }
    public required int MaxConcurrentConsumers { get; init; }
    public required TimeSpan Window { get; init; }
    public int Cost { get; init; } = 1;
}
```

```csharp
public sealed class AttentionConcurrencyLease : IAsyncDisposable
{
    public required bool Acquired { get; init; }
    public TimeSpan? RetryAfter { get; init; }
    public long? Remaining { get; init; }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

Implementações possíveis:

1. Redis Cell com `CL.THROTTLE`.
2. Redis lock com contador.
3. Banco relacional com lease e expiração.
4. In-memory, apenas para testes.
5. No-op, quando a aplicação não precisa de limite.

Semântica:

1. Se `Acquired = false`, o pacote deve republicar a atenção e dar `Ack` na atual.
2. Não deve abrir consumidor temporário quando não houver permissão.
3. O tempo de janela pode ser igual ao `MaxConsumptionTime`.
4. O limite pode ser 1.

### Work message handler

Responsável por processar cada mensagem da work queue.

```csharp
public interface IAttentionWorkMessageHandler<TWork, TAttention>
{
    ValueTask HandleAsync(
        TWork message,
        TAttention attention,
        AttentionWorkMessageContext context,
        CancellationToken cancellationToken);
}
```

```csharp
public sealed record AttentionWorkMessageContext
{
    public required string WorkQueueName { get; init; }
    public required string ConsumerTag { get; init; }
    public required int MessagesProcessedInCycle { get; init; }
    public required DateTimeOffset CycleStartedAt { get; init; }
}
```

O pacote deve deixar o handler de domínio processar a mensagem real. O pacote controla consumo, ack/nack, encerramento e republicação.

### Resultados

```csharp
public enum AttentionCycleResult
{
    Done,
    NeedMoreAttention
}
```

`Done` significa:

1. Fila não existe.
2. Fila vazia.
3. Recurso não pode processar e atenção foi descartada.
4. Ciclo processou mensagens e não detectou backlog remanescente.

`NeedMoreAttention` significa:

1. Ainda há mensagens na fila.
2. O recurso está temporariamente bloqueado.
3. A concorrência está indisponível.
4. O ciclo foi interrompido por limite e há indício de backlog.

## API sugerida para Oragon.RabbitMQ

> Nota: esta API e historica/conceitual. O milestone atual descartou
> `MapAttentionQueue(...)` como entrega obrigatoria e optou por primitivas
> genericas: `MapQueue`, `IAmqpDynamicQueueConsumer`, `RequeueToTail`,
> graceful shutdown, bindings, hooks e gates definidos pela aplicacao.

### Registro de consumidor de atenção

Possível extensão:

```csharp
app.MapAttentionQueue<TAttention, TWork>(
    attentionQueueName,
    configure);
```

Exemplo de uso:

```csharp
app.MapAttentionQueue<MarketplaceAttentionRequest, MarketplaceWorkMessage>(
    "attention.marketplace.work",
    options =>
    {
        options.ConnectionKey = "rabbitmq_messagefy";
        options.DispatchConcurrency = 2;
        options.Prefetch = 2;
        options.WorkQueueResolver = typeof(MarketplaceQueueResolver);
        options.PolicyProvider = typeof(MarketplaceAttentionPolicyProvider);
        options.ReadinessGate = typeof(MarketplaceReadinessGate);
        options.WorkMessageHandler = typeof(MarketplaceWorkMessageHandler);
    });
```

Ou em estilo compatível com `MapQueue`:

```csharp
app.MapAttentionQueue<MarketplaceAttentionRequest, MarketplaceWorkMessage>(
    "attention.marketplace.work")
    .WithSafeRunnerConnection("rabbitmq_messagefy")
    .WithDispatchConcurrency(2)
    .WithPrefetch(2)
    .WithQueueResolver<MarketplaceQueueResolver>()
    .WithPolicyProvider<MarketplaceAttentionPolicyProvider>()
    .WithReadinessGate<MarketplaceReadinessGate>()
    .WithWorkMessageHandler<MarketplaceWorkMessageHandler>();
```

### Publicação coordenada

O pacote pode oferecer helper para publicar trabalho + atenção:

```csharp
public interface IAttentionPublisher<TWork, TAttention>
{
    Task PublishAsync(
        TWork workMessage,
        TAttention attention,
        AttentionPublishOptions options,
        CancellationToken cancellationToken);
}
```

```csharp
public sealed record AttentionPublishOptions
{
    public required string WorkExchange { get; init; }
    public required string WorkRoutingKey { get; init; }
    public required string AttentionExchange { get; init; }
    public required string AttentionRoutingKey { get; init; }
    public bool Mandatory { get; init; } = true;
    public bool Persistent { get; init; } = true;
}
```

O pacote deve documentar que essas duas publicações precisam de estratégia de consistência.

Opções:

1. Publisher confirms nas duas publicações.
2. Outbox pattern.
3. Retry idempotente.
4. Reconciliação periódica.
5. Transação externa, se existir.

O pacote pode prover um publisher simples, mas não deve prometer atomicidade distribuída se ela não existir.

## Algoritmo do consumidor de atenção

Pseudocódigo completo:

```text
on attention message received:
    deserialize TAttention

    resolution = queueResolver.Resolve(attention)
    policy = policyProvider.GetPolicy(attention, resolution)

    readiness = readinessGate.CanProcess(attention, resolution)
    if readiness.CanProcess == false:
        log attention discarded
        ack attention
        return

    lease = concurrencyGate.TryAcquire(
        key = resolution.ConcurrencyKey ?? resolution.WorkQueueName,
        max = policy.MaxConcurrentConsumers,
        window = policy.MaxConsumptionTime)

    if lease.Acquired == false:
        forward attention to attention queue
        ack attention
        return

    result = process work queue temporarily

    if result == Done:
        ack attention
        return

    if result == NeedMoreAttention:
        forward attention to attention queue
        ack attention
        return
```

Observação importante: se a atenção for republicada, deve-se dar `Ack` na mensagem atual. A nova atenção representa o próximo ciclo. Não deve ser usado `Nack(requeue: true)` para esse caso, porque isso mistura retentativa de erro com escalonamento normal.

## Algoritmo do consumo temporário

```text
process work queue temporarily:
    create AMQP channel

    try passive declare work queue
    if queue not found:
        close channel
        return Done

    if message count == 0:
        close channel
        return Done

    configure qos:
        prefetch = policy.PrefetchCount ?? policy.MaxMessages

    create AsyncEventingBasicConsumer
    start timer = policy.MaxConsumptionTime
    processed = 0
    stopRequested = false

    on received message:
        if stopRequested:
            nack message with requeue = true
            return

        try:
            deserialize TWork
            if invalid:
                reject message according to policy
                return

            call work handler
            ack work message
            processed++

            if processed >= policy.MaxMessages:
                stopRequested = true

        catch exception:
            nack or reject work message according to policy

    start consuming
    wait until:
        timer elapsed
        processed >= policy.MaxMessages
        cancellation requested
        queue canceled by broker

    cancel consumer
    wait policy.DrainDelay

    passive declare work queue again
    if queue not found:
        close channel
        return Done

    close channel

    if message count > 0:
        return NeedMoreAttention

    return Done
```

## ACK/NACK semantics

### Attention message

| Situação | Resultado |
| --- | --- |
| Atenção processada e fila sem backlog | `Ack` |
| Recurso não pode processar | `Ack` |
| Work queue não existe | `Ack` |
| Work queue vazia | `Ack` |
| Rate limit bloqueado | `Forward attention` + `Ack` |
| Ainda há backlog | `Forward attention` + `Ack` |
| Erro inesperado no handler de atenção | deixar Oragon aplicar política de erro ou retornar `Nack` conforme configuração |

### Work message

| Situação | Resultado |
| --- | --- |
| Mensagem processada com sucesso | `BasicAck` |
| Desserialização retorna null/inválida | `BasicReject(requeue: false)` por padrão |
| Handler lança exceção transitória | `BasicNack(requeue: true)` por padrão |
| Stop já solicitado e mensagem chegou depois | `BasicNack(requeue: true)` |

O pacote deve permitir configurar comportamento para mensagem inválida e erro transitório.

## Rate limit e concorrência

O objetivo do gate não é limitar mensagens por segundo. O objetivo principal é limitar ciclos simultâneos por work queue.

Exemplo:

```text
key = attention:concurrency:process.marketplace.store_873.work
max = 1
window = 30s
```

Com `max = 1`, apenas um ciclo de atenção pode consumir a fila da loja por vez.

Exemplo com mais paralelismo:

```text
key = attention:concurrency:process.marketplace.store_873.work
max = 10
window = 30s
```

Com `max = 10`, até dez ciclos simultâneos podem consumir a mesma fila.

Recomendações:

1. Usar `max = 1` quando ordem importa.
2. Usar `max = 1` quando há risco de conflito.
3. Usar `max > 1` apenas quando o handler é idempotente e seguro para paralelismo.
4. A janela deve cobrir a duração esperada do ciclo.
5. Se o ciclo pode durar 30s, a janela do gate não deve expirar muito antes disso.

## Locks externos

Além do gate de concorrência, uma aplicação pode precisar de locks de domínio.

Exemplos:

1. Loja em migração.
2. Canal sendo movido.
3. Recurso em manutenção.
4. Tenant em operação crítica.

O pacote deve permitir um hook antes do consumo:

```csharp
public interface IAttentionResourceLockGate<TAttention>
{
    ValueTask<AttentionLockResult> TryEnterAsync(
        TAttention attention,
        AttentionQueueResolution resolution,
        CancellationToken cancellationToken);
}
```

Se o lock não puder ser adquirido, o resultado recomendado é `NeedMoreAttention`, com republicação da atenção.

## Topology bootstrap

O pacote pode ter um serviço opcional para declarar topologia.

```csharp
public interface IAttentionTopologyBuilder
{
    Task EnsureAttentionTopologyAsync(
        AttentionTopologyDefinition definition,
        CancellationToken cancellationToken);

    Task EnsureWorkQueueTopologyAsync(
        WorkQueueTopologyDefinition definition,
        CancellationToken cancellationToken);
}
```

```csharp
public sealed record AttentionTopologyDefinition
{
    public required string Exchange { get; init; }
    public required string WorkQueue { get; init; }
    public required string ErrorQueue { get; init; }
    public required string RoutingKey { get; init; }
    public string ExchangeType { get; init; } = "topic";
    public bool Durable { get; init; } = true;
    public string QueueType { get; init; } = "quorum";
}
```

```csharp
public sealed record WorkQueueTopologyDefinition
{
    public required string Exchange { get; init; }
    public required string WorkQueue { get; init; }
    public required string ErrorQueue { get; init; }
    public required string RoutingKey { get; init; }
    public string ExchangeType { get; init; } = "topic";
    public bool Durable { get; init; } = true;
    public string QueueType { get; init; } = "quorum";
}
```

O pacote deve deixar claro se declara topologia automaticamente ou se apenas consome topologia existente.

## Publicação de trabalho + atenção

Fluxo recomendado na API:

```text
receive HTTP request
validate request
build work message
build attention request
publish work message
publish attention request
return HTTP response
```

Ponto crítico: publicar trabalho e atenção são duas ações relacionadas.

Cenários de falha:

| Falha | Consequência | Mitigação |
| --- | --- | --- |
| Publicou trabalho, falhou atenção | Work queue fica com backlog sem consumo | Outbox, retry, reconciliação |
| Falhou trabalho, publicou atenção | Attention cycle encontra fila vazia | Idempotência; não é grave |
| Publicou ambos, resposta HTTP falhou | Integrador pode reenviar | Idempotência da work message |
| Publicou atenção duplicada | Ciclo extra pode ocorrer | Idempotência da atenção |

## Consistência

O pacote não deve prometer atomicidade entre RabbitMQ e banco de dados se isso não for implementado.

Deve documentar estratégias:

1. Outbox em banco relacional.
2. Publisher confirms do RabbitMQ.
3. Mensagens idempotentes.
4. Chaves de deduplicação no domínio.
5. Job de reconciliação.

### Outbox recomendada

Quando a API persiste estado no banco e precisa publicar no RabbitMQ, a opção mais robusta costuma ser:

```text
transaction:
    save domain state
    insert outbox work message
    insert outbox attention message

background publisher:
    read pending outbox
    publish with confirmation
    mark as published
```

## Reconciliação

Mesmo com publisher confirms, pode ser útil ter reconciliação.

Ideia:

```text
periodically:
    list work queues
    for each queue with messages > 0:
        if no recent attention seen:
            publish attention request
```

O pacote pode expor interface para reconciliação, mas talvez não deva implementar uma reconciliação genérica porque reconstruir `TAttention` pode exigir domínio.

## Observabilidade

O pacote deve emitir logs, métricas e eventos.

### Métricas mínimas

| Métrica | Dimensões sugeridas |
| --- | --- |
| attention.received.count | attentionQueue, resourceType |
| attention.discarded.count | reason, resourceType |
| attention.republished.count | reason, resourceType |
| attention.done.count | resourceType |
| attention.rate_limited.count | key, resourceType |
| attention.cycle.duration | workQueue, resourceType |
| attention.cycle.messages_processed | workQueue, resourceType |
| attention.work_queue.empty.count | workQueue |
| attention.work_queue.missing.count | workQueue |
| attention.work_message.ack.count | workQueue |
| attention.work_message.nack.count | workQueue |
| attention.work_message.reject.count | workQueue |
| attention.backlog.remaining.count | workQueue |

### Logs recomendados

1. Attention received.
2. Attention discarded.
3. Rate limit blocked.
4. Work queue missing.
5. Work queue empty.
6. Temporary consumer started.
7. Temporary consumer stopped.
8. Work message processed.
9. Work message failed.
10. Attention republished.
11. Consumer canceled by broker.
12. Channel closed with error.

### Tags

Tags úteis:

```text
attention_queue
work_queue
resource_key
worker_id
consumer_tag
max_messages
max_consumption_time_seconds
max_concurrent_consumers
result
reason
```

## Edge cases

### Work queue não existe

Pode acontecer quando:

1. O recurso foi deletado.
2. A fila ainda não foi declarada.
3. Houve erro de topologia.

Tratamento recomendado:

1. Se o domínio indica recurso deletado, retornar `Done`.
2. Se a topologia deveria existir, logar warning ou error.
3. Não entrar em loop infinito de `Nack`.

### Work queue vazia

Não é erro. Pode haver atenção atrasada ou duplicada.

Tratamento:

```text
Ack attention
Return Done
```

### Rate limit bloqueado

Não é erro de processamento.

Tratamento:

```text
Forward attention
Ack current attention
Return
```

### Canal AMQP fechado pelo broker

Pode acontecer se a queue for deletada durante o consumo.

Tratamento:

1. Diferenciar cancelamento iniciado pelo cliente de cancelamento iniciado pelo broker.
2. Logar broker-initiated como warning.
3. Se a fila sumiu, retornar `Done`.

### Mensagem de trabalho chega depois do stop

Se o ciclo já atingiu limite, uma mensagem entregue depois deve voltar para a fila.

Tratamento:

```text
BasicNack(requeue: true)
```

### Erro no handler de trabalho

Padrão:

```text
BasicNack(requeue: true)
```

Mas deve ser configurável. Alguns domínios preferem enviar para DLQ após erro não transitório.

### CancellationToken

Se a aplicação estiver encerrando:

1. Parar de aceitar novas mensagens.
2. Cancelar consumer temporário.
3. Nack/requeue mensagens não finalizadas.
4. Não ackar attention se o ciclo não chegou a uma decisão segura, a menos que a aplicação tenha republicado atenção.

## Regras de segurança

1. Não consumir work queue se atenção foi bloqueada por rate limit.
2. Não usar `Nack(requeue: true)` como mecanismo normal de escalonamento da atenção.
3. Republicar atenção e ackar a atual quando o ciclo precisa continuar futuramente.
4. Tratar attention request como idempotente.
5. Tratar work message como idempotente sempre que houver retry.
6. Fechar canal explicitamente antes de sair, quando possível.
7. Distinguir fila inexistente de erro de permissão.
8. Não engolir erros de broker que não sejam esperados.

## Integração com Oragon.RabbitMQ

### Base atual

O projeto já usa padrões como:

```csharp
app.MapQueue("queue.work", async ([FromBody] Message body) =>
{
    return AmqpResults.Ack();
})
.WithSafeRunnerConnection("rabbitmq_messagefy")
.WithDispatchConcurrency(2)
.WithPrefetch(2);
```

O pacote de Attention Queue deve ser idiomático com isso.

### Resultado de attention handler

Um handler de attention queue pode retornar:

```csharp
return AmqpResults.Ack();
```

ou:

```csharp
return AmqpResults.Compose(
    AmqpResults.Forward(exchange: "", routingKey: attentionQueueName, mandatory: false, attention),
    AmqpResults.Ack());
```

O pacote pode esconder essa composição atrás de um `AttentionCycleResult`.

### Descriptor

Uma API fluente pode expor:

```csharp
public interface IAttentionConsumerDescriptor<TAttention, TWork> : IConsumerDescriptor
{
    IAttentionConsumerDescriptor<TAttention, TWork> WithQueueResolver<TResolver>();
    IAttentionConsumerDescriptor<TAttention, TWork> WithPolicyProvider<TPolicyProvider>();
    IAttentionConsumerDescriptor<TAttention, TWork> WithReadinessGate<TGate>();
    IAttentionConsumerDescriptor<TAttention, TWork> WithConcurrencyGate<TGate>();
    IAttentionConsumerDescriptor<TAttention, TWork> WithWorkMessageHandler<THandler>();
}
```

## Testes recomendados

### Unitários

1. Attention descartada quando readiness gate nega.
2. Attention republicada quando concurrency gate bloqueia.
3. `QueueDeclarePassive` com 404 retorna `Done`.
4. Fila vazia retorna `Done`.
5. Fila com mensagens inicia consumer temporário.
6. MaxMessages encerra ciclo.
7. MaxConsumptionTime encerra ciclo.
8. Mensagem inválida é rejeitada.
9. Handler com exceção faz nack/requeue.
10. Backlog remanescente retorna `NeedMoreAttention`.
11. Sem backlog retorna `Done`.
12. Attention duplicada não quebra fluxo.

### Integração com RabbitMQ real

1. Publica trabalho + atenção e verifica processamento.
2. Verifica republicação quando backlog excede `MaxMessages`.
3. Verifica `MaxConcurrentConsumers = 1`.
4. Verifica `MaxConcurrentConsumers = 10`.
5. Deleta work queue antes do consumo e valida `Done`.
6. Deleta work queue durante consumo e valida encerramento seguro.
7. Simula erro no handler e valida nack/requeue ou DLQ.
8. Verifica publisher confirms no helper de publicação.

### Testes de carga

1. Muitas filas vazias com atenções duplicadas.
2. Uma fila com backlog muito alto.
3. Muitas filas com backlog pequeno.
4. Uma fila ruidosa e várias filas pequenas.
5. Diferentes políticas por plano.

## Configuração sugerida

```csharp
services.AddOragonRabbitMqAttentionQueue(options =>
{
    options.DefaultMaxConsumptionTime = TimeSpan.FromSeconds(30);
    options.DefaultMaxMessages = 50;
    options.DefaultMaxConcurrentConsumers = 1;
    options.DefaultDrainDelay = TimeSpan.FromSeconds(5);
    options.DefaultQueueType = "quorum";
});
```

Registro de componentes:

```csharp
services.AddScoped<IAttentionQueueResolver<MarketplaceAttentionRequest>, MarketplaceQueueResolver>();
services.AddScoped<IAttentionConsumptionPolicyProvider<MarketplaceAttentionRequest>, MarketplacePolicyProvider>();
services.AddScoped<IAttentionReadinessGate<MarketplaceAttentionRequest>, MarketplaceReadinessGate>();
services.AddScoped<IAttentionWorkMessageHandler<MarketplaceWorkMessage, MarketplaceAttentionRequest>, MarketplaceWorkHandler>();
services.AddSingleton<IAttentionConcurrencyGate, RedisAttentionConcurrencyGate>();
```

Mapeamento:

```csharp
app.MapAttentionQueue<MarketplaceAttentionRequest, MarketplaceWorkMessage>("attention.marketplace.work")
    .WithSafeRunnerConnection("rabbitmq_messagefy")
    .WithDispatchConcurrency(2)
    .WithPrefetch(2);
```

## Exemplo de política por plano

```csharp
public sealed class MarketplacePolicyProvider
    : IAttentionConsumptionPolicyProvider<MarketplaceAttentionRequest>
{
    public async ValueTask<AttentionConsumptionPolicy> GetPolicyAsync(
        MarketplaceAttentionRequest attention,
        AttentionQueueResolution resolution,
        CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(attention.StoreId, cancellationToken);

        return plan switch
        {
            "Basic" => new AttentionConsumptionPolicy
            {
                MaxConcurrentConsumers = 1,
                MaxMessages = 50,
                MaxConsumptionTime = TimeSpan.FromSeconds(20)
            },
            "Pro" => new AttentionConsumptionPolicy
            {
                MaxConcurrentConsumers = 3,
                MaxMessages = 200,
                MaxConsumptionTime = TimeSpan.FromSeconds(30)
            },
            "Enterprise" => new AttentionConsumptionPolicy
            {
                MaxConcurrentConsumers = 10,
                MaxMessages = 1000,
                MaxConsumptionTime = TimeSpan.FromSeconds(60)
            },
            _ => new AttentionConsumptionPolicy()
        };
    }
}
```

## Redis concurrency gate com CL.THROTTLE

Semântica inspirada no uso atual:

```text
CL.THROTTLE key max_burst limit period cost
```

Mapeamento:

```text
key = attention:concurrency:{workQueueName}
max_burst = MaxConcurrentConsumers - 1
limit = MaxConcurrentConsumers
period = ceil(MaxConsumptionTime.TotalSeconds)
cost = 1
```

Resposta:

```text
0 = allowed
1 = blocked
```

Campos úteis:

```text
limit
remaining
retry_after
reset_after
```

Cuidados:

1. `MaxConcurrentConsumers = 1` deve resultar em `max_burst = 0`.
2. `MaxConcurrentConsumers` não pode ser menor que 1.
3. `period` não pode ser zero.
4. Se Redis estiver indisponível, decidir política: fail-open ou fail-closed.

Recomendação de pacote: padrão conservador `fail-closed` ou configurável.

## Ordem, idempotência e paralelismo

Se `MaxConcurrentConsumers = 1`, o pacote reduz chance de concorrência na mesma fila, mas não garante ordenação global do sistema. Garante apenas que um ciclo por work queue deve ocorrer por vez, desde que o gate seja correto.

Se `MaxConcurrentConsumers > 1`, a aplicação assume que:

1. Work messages são independentes.
2. Handler é idempotente.
3. Ordem estrita não é exigida.
4. Conflitos de atualização são tratados pelo domínio.

## Checklist historico da proposta opinativa

> Nao usar este checklist como plano ativo do milestone atual. Ele registra o que
> seria necessario para um pacote `Attention` opinativo caso um milestone futuro
> decida implementa-lo.

1. Definir contratos públicos.
2. Definir opções globais.
3. Avaliar se um descriptor `MapAttentionQueue` ainda e necessario.
4. Integrar com DI.
5. Implementar attention handler interno.
6. Implementar temporary work queue consumer.
7. Implementar queue resolver.
8. Implementar policy provider default.
9. Implementar readiness gate default allow.
10. Implementar concurrency gate no-op.
11. Avaliar gate Redis apenas como implementacao externa ao core.
12. Implementar topology builder opcional.
13. Implementar publisher helper opcional.
14. Implementar métricas.
15. Implementar logs estruturados.
16. Implementar testes unitários.
17. Implementar testes com RabbitMQ real.
18. Documentar falhas e garantias.

## Garantias que o pacote pode oferecer

O pacote pode oferecer:

1. Consumo sob demanda de work queues.
2. Limite de tempo por ciclo.
3. Limite de mensagens por ciclo.
4. Limite de concorrência por fila.
5. Republicação de atenção quando há backlog.
6. Ack seguro da atenção tratada.
7. Encerramento de ciclo quando fila está vazia.
8. Hooks para estado, política, lock, rate limit e observabilidade.

O pacote não deve prometer sozinho:

1. Atomicidade entre HTTP, banco e RabbitMQ.
2. Deduplicação perfeita de mensagens de domínio.
3. Ordenação global.
4. Correção de handlers não idempotentes.
5. Operação segura de infinitas filas sem capacidade no broker.

## Resumo do mecanismo

Attention Queue transforma uma fila agregada de pedidos pequenos em um escalonador de filas granulares. Cada pedido de atenção aponta para uma fila real, recebe uma política de consumo, disputa permissão de concorrência e abre um consumidor temporário. O consumidor trabalha por uma fatia limitada. Se ainda houver backlog, a atenção é republicada para um ciclo futuro. Se não houver, o ciclo termina.

O padrão é útil quando existem muitas filas específicas, volume irregular, necessidade de isolamento, políticas diferentes por entidade e custo alto para manter consumidores permanentes.
