# Attention support in Oragon.RabbitMQ

Este documento consolida a conversa sobre a evolucao necessaria no Oragon.RabbitMQ para suportar o padrao de Attention Queue usado pelo MessageFy e implementacoes parecidas.

O objetivo nao e transformar a library em uma implementacao acoplada ao MessageFy. O objetivo e identificar as primitivas que faltam para que clientes consigam implementar esse padrao com menos codigo manual, menos risco operacional e comportamento previsivel em shutdown, retry, headers, publish e consumo sob demanda.

## Contexto

O MessageFy usa o Oragon.RabbitMQ como uma das libraries de mensageria. No fluxo analisado, a aplicacao publica a mensagem real em uma fila dinamica de trabalho e tambem publica uma mensagem pequena de atencao em uma fila agregada.

Em termos simples:

- a fila de trabalho contem o payload real;
- a fila de atencao contem apenas um sinal de que uma fila de trabalho precisa ser observada;
- o handler da fila de atencao abre um consumo temporario na fila de trabalho, usando primitivas genericas da library;
- o consumo para por quantidade, tempo total de processamento, tempo ocioso, total inicial da fila, fila vazia, fila ausente ou shutdown;
- se ainda existir trabalho, a atencao e republicada ao final da fila de atencao.

Esse desenho evita manter consumidores permanentes para milhares de filas dinamicas e preserva isolamento por canal, tenant, loja, conta ou outro recurso granular.

## Fluxo observado no MessageFy

O fluxo relevante pode ser resumido assim:

1. A API recebe uma solicitacao e cria uma mensagem de trabalho.
2. A mensagem de trabalho e publicada em uma fila dinamica, por exemplo `messagefy.process.{channelType}.channel_{channelId}.work`.
3. Uma `AttentionRequest` e publicada na fila de atencao.
4. O worker de atencao consome a `AttentionRequest`.
5. O worker valida se o recurso ainda pode receber processamento.
6. O worker calcula limites de consumo: tempo maximo, quantidade maxima e concorrencia.
7. O worker adquire lock ou permissao de concorrencia por recurso.
8. O worker abre manualmente um `AsyncEventingBasicConsumer` na fila dinamica.
9. Cada mensagem interna e processada com ack, reject ou nack conforme o resultado.
10. Em shutdown, o worker para de aceitar novas mensagens, cancela o consumer, espera in-flight drenar e reprograma a atencao quando necessario.
11. Se o ciclo terminar sem backlog, a atencao e confirmada.
12. Se o ciclo precisar continuar depois, a atencao e republicada no final da fila e a mensagem original de atencao recebe ack.

O commit `db847c59ab3138fda5c8439ae131b22721cd89df` no MessageFy e importante porque adiciona comportamento de graceful shutdown nesse consumo temporario: parar novas entregas, lidar com mensagens tardias, cancelar o consumer, esperar processamento em andamento e devolver `NeedMoreAttention` quando o encerramento interrompe o ciclo.

Esse commit tambem inclui necessidades operacionais especificas do MessageFy, como migracao para `x-single-active-consumer`, locks de ciclo de vida de canal e reconciliacao de topologia. Esses pontos ajudaram a revelar extensibility points necessarios, mas nao devem ser assumidos como responsabilidade direta do Oragon.RabbitMQ.

## Gaps originais identificados no Oragon.RabbitMQ

O Oragon.RabbitMQ ja cobre bem o consumo fixo via `MapQueue`, dispatch por delegate, binders, `Ack`, `Nack`, `Reject`, `Forward`, `Reply`, `Compose`, serializer e `WithTopology`.

Para o padrao de attention, os gaps principais estavam em outra camada. A lista
abaixo registra a motivacao do milestone; o estado executado fica detalhado em
`spec/attention/milestone-roadmap.md`.

- `QueueConsumer.StopAsync` cancelava o consumer no RabbitMQ, mas o cancelamento do token do handler acontecia apenas no dispose. O handler nao tinha uma sinalizacao cooperativa clara durante `StopAsync`.
- Nao existe tracking publico/configuravel de mensagens in-flight nem espera de drain no shutdown.
- A falha durante `IAmqpResult.ExecuteAsync` cai em fallback fixo `Nack(false)`, sem politica configuravel.
- `[FromAmqpHeader]` foi desenhado como string, mas cenarios reais precisam ler headers numericos e nullable, especialmente `x-delivery-count`.
- Existem binders por convencao para `priority` e `deliveryCount/attempts`, mas isso precisa virar uma superficie documentada e estavel.
- O padrao de "republicar no fim da mesma fila e dar ack na mensagem atual" precisava ser montado com `Forward("", queue) + Ack`.
- `Forward` ja permite configurar `BasicProperties`, mas clientes com conexoes/canais especificos ainda precisam criar resultados customizados.
- Nao existe uma primitiva para consumir sob demanda uma fila dinamica dentro de um handler com limites, ack/nack por mensagem e shutdown cooperativo.
- Topologias como quorum, DLQ, priority e `x-single-active-consumer` ainda exigem boilerplate e nao tem helpers/diagnostico comum. Migracoes destrutivas de topologia continuam sendo responsabilidade da aplicacao.
- O consumo temporario precisa de regras de parada combinaveis, incluindo tempo ocioso. Sem isso, um consumer pode ficar aberto esperando novas mensagens mesmo depois de processar a fatia que motivou a atencao.
- Fluxos que publicam uma mensagem de trabalho e depois uma mensagem de atencao precisam de primitives de publish confiavel e documentacao de consistencia. A library nao deve prometer atomicidade com banco de dados, mas deve oferecer APIs que nao dificultem outbox, publisher confirms e reconciliacao feita pela aplicacao.

## Por que nao chamar a primitiva de Attention

Durante a discussao, o nome `Queue Drain` gerou duvida porque parece significar "esvaziar a fila inteira". O comportamento desejado nao e esse.

Tambem decidimos nao chamar essa primitiva de `Attention`, porque attention e o padrao completo:

- existe uma mensagem-sinal;
- essa mensagem aponta para uma fila de trabalho;
- ha validacao de estado do recurso;
- ha lock ou limite de concorrencia;
- ha consumo temporario da fila dinamica;
- ha decisao entre concluir ou pedir nova atencao;
- ha regras de prioridade, retry, shutdown e topologia.

A primitiva que falta no Oragon e menor e mais generica: consumir uma fila escolhida em runtime por uma janela controlada.

Nome recomendado para essa primitiva:

```text
Dynamic Queue Consumer Primitive
```

Em portugues:

```text
Primitiva de consumo dinamico sob demanda
```

Ela e a base tecnica que permite implementar attention com `MapQueue`, mas tambem pode servir para outros casos:

- processamento por tenant;
- filas dinamicas por canal;
- backlogs auxiliares;
- workers de retry;
- consumo por lote ou janela de tempo;
- manutencao controlada de filas.

## Decisoes tomadas

As decisoes de escopo para este milestone sao:

1. Entregar primitivas reutilizaveis, nao um modulo `Attention` opinativo.
2. Fechar lacunas praticas de shutdown, headers, retry, publish e consumo dinamico.
3. Nao assumir que um modulo `Attention` opinativo, como `MapAttentionQueue(...)`, seja necessario. Ele pode existir no futuro, mas nao e objetivo nem premissa deste milestone.
4. Fazer graceful shutdown como opt-in para preservar compatibilidade.
5. Incluir helpers e diagnostico de topologia, mas nao migracao automatica destrutiva de filas.
6. Publicar as novas interfaces e contratos em `Oragon.RabbitMQ.Abstractions`.
7. Nao implementar pacote Redis neste milestone. Redis pode aparecer em exemplos como implementacao do cliente, mas o core nao deve depender de Redis nem controlar versao de provider Redis.
8. Oferecer pontos de extensao com `IServiceProvider` para o usuario resolver Redis, banco ou outra tecnologia e executar validacoes, locks, observabilidade ou regras de negocio antes/depois do consumo dinamico, sem a library assumir essas responsabilidades.
9. Nao implementar locks de ciclo de vida de dominio, como `channel-lifecycle:{channelId}`.

## Comportamentos esperados apos o milestone

Depois do milestone, uma aplicacao deve conseguir implementar o padrao de attention assim:

1. Registrar um consumer fixo na fila de atencao com `MapQueue`.
2. Resolver a fila dinamica a partir da mensagem de atencao.
3. Usar a primitiva de consumo dinamico sob demanda para processar uma fatia da fila.
4. Respeitar `CancellationToken` quando a aplicacao estiver parando.
5. Decidir ack, reject, nack/requeue ou DLQ por mensagem interna.
6. Receber um status final dizendo se a fila acabou, estava vazia, nao existia, bateu limite de quantidade, bateu tempo total, ficou ociosa, atingiu o total inicial da fila ou foi interrompida.
7. Retornar `Ack` para concluir a atencao ou `RequeueToTail` para pedir nova fatia.

O controle de concorrencia distribuido pode ser aplicado antes do consumo dinamico, mas a chave, a tecnologia e a decisao sao da aplicacao. O core deve expor o ponto de extensibilidade e o `IServiceProvider`; Redis aparece apenas como exemplo de implementacao do cliente:

```csharp
Func<DynamicQueueStartContext, CancellationToken, ValueTask<DynamicQueueStartDecision>> beforeStart =
    async (startContext, cancellationToken) =>
{
    var gate = startContext.Services.GetRequiredService<IAmqpConcurrencyGate>();

    await using var lease = await gate.TryAcquireAsync(
        new AmqpConcurrencyGateRequest(
            Key: $"attention:{attention.ChannelTypeKey}:{attention.ChannelId:D}",
            LeaseTime: TimeSpan.FromSeconds(30),
            Metadata: null),
        cancellationToken);

    return lease.Acquired
        ? DynamicQueueStartDecision.Allow()
        : DynamicQueueStartDecision.Defer(TimeSpan.FromSeconds(5));
};
```

Esse exemplo usa uma chave parecida com a do MessageFy, mas a library nao conhece nem exige esse formato. Locks de negocio, como lifecycle de canal, devem continuar fora da library.

Sobre `IServiceProvider` nos pontos antigos de extensibilidade:

- `WithConnection` e `WithSerializer` ja recebem `IServiceProvider`.
- `WhenSerializationFail` e `WhenProcessFail` ja recebem `IAmqpContext`, que expoe `ServiceProvider` escopado da mensagem.
- `WhenResultExecutionFail` segue a mesma regra e recebe `IAmqpContext`.
- `WithChannel` e `WithTopology` expoem overloads non-breaking com `IServiceProvider`, porque rodam fora de mensagem e nao recebem contexto.

Um exemplo conceitual:

```csharp
public async Task<IAmqpResult> HandleAttention(
    AttentionRequest attention,
    IAmqpContext context,
    IAmqpDynamicQueueConsumer dynamicConsumer,
    CancellationToken cancellationToken)
{
    var queueName = ResolveWorkQueue(attention);

    var result = await dynamicConsumer.ConsumeAsync<Container>(
        new DynamicQueueConsumeRequest<Container>
        {
            QueueName = queueName,
            MaxMessages = 100,
            MaxDuration = TimeSpan.FromSeconds(20),
            IdleTimeout = TimeSpan.FromSeconds(3),
            OnMessageAsync = async (message, messageContext) =>
            {
                await ProcessMessageAsync(message, cancellationToken);
                return DynamicQueueMessageResult.Ack();
            }
        },
        cancellationToken);

    return result.Status switch
    {
        DynamicQueueConsumeStatus.QueueMissing => AmqpResults.Ack(),
        DynamicQueueConsumeStatus.Empty => AmqpResults.Ack(),
        DynamicQueueConsumeStatus.Completed => AmqpResults.Ack(),
        DynamicQueueConsumeStatus.IdleTimeoutReached => AmqpResults.Ack(),
        DynamicQueueConsumeStatus.MaxMessagesReached => AmqpResults.Compose(AmqpResults.RequeueToTail(), AmqpResults.Ack()),
        DynamicQueueConsumeStatus.MaxDurationReached => AmqpResults.Compose(AmqpResults.RequeueToTail(), AmqpResults.Ack()),
        DynamicQueueConsumeStatus.InitialQueueLengthReached => AmqpResults.Compose(AmqpResults.RequeueToTail(), AmqpResults.Ack()),
        DynamicQueueConsumeStatus.Interrupted => AmqpResults.Compose(AmqpResults.RequeueToTail(), AmqpResults.Ack()),
        _ => AmqpResults.Nack(false)
    };
}
```

## Relacao com specs existentes

Os arquivos `spec/attention-queue-PTBR.md`, `spec/attention-queue-ENUS.md` e `spec/attention-queue-tech.md` descrevem o padrao Attention Queue em nivel conceitual e tecnico. Eles devem ser lidos como material historico/conceitual, porque ainda contem uma proposta mais opinativa com `MapAttentionQueue(...)`.

Esta pasta documenta o recorte especifico da evolucao do Oragon.RabbitMQ:

- por que a library precisa mudar;
- quais gaps foram identificados a partir do MessageFy;
- quais comportamentos devem ser adicionados;
- qual milestone implementa as primitivas necessarias;
- quais decisoes ficam explicitamente fora deste ciclo.
