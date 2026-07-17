# Attention Queue: processamento sob demanda para filas granulares

> Status: artigo conceitual.
>
> Este documento explica o padrão Attention Queue, mas não é o contrato de implementação do milestone atual do Oragon.RabbitMQ. O plano ativo está em `spec/attention/README.md` e `spec/attention/milestone-roadmap.md`.
>
> Direção atual: implementar primeiro primitivas reutilizáveis: graceful shutdown no `MapQueue`, consumo dinâmico de filas, publish confiável, `RequeueToTail`, bindings estáveis e gates opcionais definidos pela aplicação. Não haverá pacote Redis oficial neste milestone; Redis pode aparecer apenas em exemplos como implementação do cliente. O core deve permitir implementações do usuário por pontos de extensibilidade com `IServiceProvider`. Locks de domínio, locks de lifecycle, migração de topologia e uma API de alto nível `MapAttentionQueue(...)` não fazem parte deste milestone.

Um padrão para consumir milhares de filas específicas sem manter milhares de consumidores ativos.

## O problema

Imagine uma plataforma de e-commerce chamada MarketHub.

Ela integra milhares de lojas com marketplaces como Amazon, Mercado Livre, Shopee e outros canais de venda. Cada loja precisa sincronizar pedidos, estoque, preço, catálogo, notas de envio e eventos de pós-venda. Na maior parte do tempo, uma loja pequena gera pouco trabalho. Mas, quando uma loja grande faz uma atualização massiva de catálogo ou recebe uma rajada de pedidos em uma campanha, ela pode gerar milhares de tarefas em poucos minutos.

No MarketHub, cada loja tem sua própria fila de trabalho. Em termos simples, uma fila é uma lista persistente de mensagens esperando processamento. Um produtor coloca mensagens na fila; um consumidor lê essas mensagens e executa alguma ação. No nosso cenário, o produtor é a própria API do MarketHub, não o integrador externo.

Essa decisão não nasce de preferência estética pela granularidade. Ela existe porque lojas diferentes têm comportamentos, prioridades e riscos diferentes. Uma loja pequena não pode ficar atrás de milhares de atualizações de catálogo de uma loja muito grande. Uma loja com integração instável também não pode congestionar o processamento das demais. Além disso, a fila própria permite aplicar políticas específicas por loja: limite de consumo, pausa operacional, manutenção, descarte controlado, reprocessamento isolado, respeito a limites de API do marketplace e observabilidade direta do backlog daquela loja.

Em outras palavras, a fila por loja compra isolamento, previsibilidade e controle operacional.

Mas ela também cria uma nova tensão arquitetural: se cada loja tem sua fila, quem consome essas filas?

A primeira solução parece óbvia: criar consumidores permanentes para todas as filas de lojas. Um consumidor permanente seria um worker registrado o tempo todo naquela fila, aguardando novas mensagens.

Mas isso cria outro problema.

Se existem 50 mil lojas, não faz sentido manter 50 mil consumidores ativos, conexões abertas, prefetch configurado e recursos reservados para filas que, na maior parte do tempo, estão vazias. O sistema passa a gastar capacidade computacional esperando por trabalho que talvez nem exista.

O problema real não é apenas processar mensagens.

O problema é encontrar um modelo intermediário entre dois extremos ruins: consumidores permanentes demais ou uma fila centralizada demais. O sistema precisa iniciar consumidores sob demanda para filas específicas, mas apenas quando houver algum indício de que existe trabalho pendente naquela fila. Também precisa decidir por quanto tempo cada consumidor temporário deve trabalhar, quantas mensagens ele deve tentar consumir, e como evitar que múltiplos workers processem a mesma fila de forma descontrolada.

## Alternativas consideradas

Algumas soluções aparecem rapidamente.

A primeira é usar consumidores permanentes por fila. É simples de entender, mas escala mal quando há muitas filas granulares e baixo volume médio por fila.

A segunda é usar uma fila central única para todas as lojas. Isso reduz a quantidade de consumidores, mas perde isolamento. Uma loja ruidosa pode atrasar lojas menores, e políticas por loja ficam mais difíceis.

A terceira é criar agrupadores de filas centralizadas. Em vez de uma fila única para todas as lojas, o sistema poderia criar grupos menores, por exemplo `process.marketplace.group_01.work`, `process.marketplace.group_02.work` e assim por diante. Cada grupo receberia tarefas de algumas lojas. Essa abordagem parece um meio-termo: reduz a quantidade de filas e evita que todas as lojas disputem uma única fila global.

O problema é que o isolamento continua sendo parcial e acidental. Imagine que `group_01` tenha 100 lojas. Se uma loja grande dentro desse grupo gera 1 milhão de jobs durante uma campanha, as outras 99 lojas ficam presas atrás dela ou passam a competir com ela dentro da mesma fila. O problema deixou de ser global, mas virou local: em vez de uma loja afetar a plataforma inteira, ela afeta todas as lojas do grupo.

Se a distribuição por grupo fica ruim, surge a necessidade de rebalancear lojas entre filas, o que adiciona operação, migração e risco. Além disso, políticas por loja continuam difíceis: limite de API, pausa de uma integração, prioridade comercial e reprocessamento isolado precisam ser reconstruídos dentro do consumidor, porque a fila já não representa uma única loja. O grupo melhora a fila única, mas não entrega isolamento real por loja.

A quarta é fazer polling periódico em todas as filas. Um scheduler varre as filas e processa as que têm mensagens. Funciona, mas introduz latência artificial, aumenta chamadas vazias ao broker e desperdiça ciclos quando há muitas filas inativas. Aqui, broker é o servidor de mensageria, como RabbitMQ, responsável por armazenar e entregar mensagens.

A quinta é fazer a API publicar mensagens diretamente em filas específicas e, junto com elas, publicar um pequeno sinal interno dizendo: esta fila precisa de atenção.

Essa é a solução que chamaremos de Attention Queue.

A ideia central é produzir dois tipos de mensagem dentro do sistema. Para o integrador externo, continua existindo uma única operação: uma chamada HTTP para a API, por exemplo para enviar uma atualização de estoque ou uma tarefa de sincronização. Depois que a API recebe e valida essa requisição, ela publica internamente duas mensagens. A primeira é a mensagem de trabalho: ela contém o dado real que precisa ser processado. A segunda é a mensagem de atenção: ela é menor e serve apenas para avisar ao próprio sistema que uma fila específica tem demanda pendente e precisa ser consumida.

| Tipo | Onde fica | O que contém | Para que serve |
| --- | --- | --- | --- |
| Mensagem de trabalho | Fila específica da loja | Payload real da tarefa, como atualização de estoque, pedido ou catálogo | Ser processada pela regra de negócio |
| Pedido de atenção | Fila compartilhada de atenção | Ponteiros mínimos, como tenant, loja, marketplace e prioridade | Acionar o consumo da fila de trabalho correta |

A mensagem real entra na fila específica do recurso. Essa fila é onde ficam os dados que efetivamente precisam ser processados. Por exemplo:

```text
process.marketplace.store_873.work
```

Esse nome de fila é apenas uma convenção didática. A implementação pode escolher outro formato. O ponto essencial é que o pedido de atenção tenha dados suficientes para localizar, derivar ou consultar qual fila de trabalho precisa ser consumida.

O sinal entra em uma fila compartilhada de atenção. Essa fila não guarda o trabalho completo; ela guarda apenas avisos de que alguma fila específica precisa ser olhada. Por exemplo:

```text
attention.marketplace.work
```

Esse sinal não contém o payload completo. Ele contém apenas o necessário para localizar a fila que precisa de atenção, ou seja, a fila que precisa ser consumida: tenant, tipo do recurso, id do recurso e algumas chaves de roteamento. Para um desenvolvedor .NET, esse sinal pode ser visto como um DTO pequeno, serializado em JSON e publicado internamente pelo próprio sistema.

Com isso, o sistema não precisa manter consumidores permanentes para todas as filas de trabalho. Ele mantém consumidores na fila de atenção. Quando uma mensagem de atenção chega, um worker usa esse aviso para localizar a fila específica, inicia um consumo temporário, consome por um tempo limitado ou até uma quantidade máxima de mensagens, e depois decide se o trabalho acabou ou se precisa republicar outra atenção.

Essa decisão de parar antes de consumir tudo é intencional. A inspiração vem do escalonamento de processos em sistemas operacionais, especialmente do conceito de time sharing. A CPU mantém processos prontos para executar e não deixa um único processo monopolizar o processador indefinidamente. Ela entrega uma fatia de tempo para um processo, interrompe, e depois dá oportunidade para outros processos avançarem. No Attention Queue, a fila de atenção faz um papel parecido com uma fila de processos prontos: cada mensagem de atenção representa uma fila de trabalho que quer uma fatia de processamento. Se ainda houver mensagens depois dessa fatia, a fila entra novamente na disputa por processamento por meio de uma nova mensagem de atenção.

O padrão não tenta ser apenas eficiente; ele tenta ser justo. Cada loja recebe fatias de processamento, evitando que uma loja grande capture a maior parte dos workers e transforme volume em privilégio operacional involuntário.

## Como funciona

O padrão funciona como um despachante interno.

Ele não processa o trabalho diretamente. Ele aponta onde existe trabalho. Em uma aplicação .NET, pense nele como um `BackgroundService` ou worker que recebe um comando pequeno e, a partir dele, decide qual fila de trabalho deve ser consumida.

Em vez de perguntar o tempo todo se há mensagens em cada fila, o sistema recebe um sinal quando algo novo chega. Em vez de deixar um consumidor eterno parado em uma fila vazia, ele inicia um consumidor temporário apenas quando há motivo para isso.

Um fluxo típico seria:

1. Um integrador faz uma chamada HTTP para a API do MarketHub solicitando uma sincronização ou enviando uma atualização.
2. A API valida a requisição e publica internamente a tarefa na fila `process.marketplace.store_873.work`.
3. Na mesma operação interna, a API publica um sinal em `attention.marketplace.work`.
4. Um worker consome esse sinal de atenção.
5. O worker consulta o estado da loja ou integração relacionada.
6. Se a loja está desativada, em manutenção, bloqueada por erro de credencial ou removida, a atenção é descartada.
7. Se a loja está válida, o worker calcula os limites de consumo: tempo máximo, quantidade máxima de mensagens e concorrência permitida.
8. O worker tenta adquirir permissão de rate limit para evitar excesso de consumidores simultâneos na mesma fila. Nesse contexto, rate limit é apenas uma trava de capacidade: quantos consumidores podem trabalhar naquela fila ao mesmo tempo. Esse limite pode ser 1, quando a fila precisa ser consumida de forma serial, ou pode ser maior, como 5 ou 10, quando o domínio permite paralelismo seguro.
9. Se permitido, ele inicia um consumidor temporário na fila específica da loja.
10. Ele processa mensagens até atingir uma das condições de parada: tempo máximo, quantidade máxima, erro operacional ou fila vazia.
11. Ao final, ele verifica se ainda há backlog. Se houver, republica uma nova atenção na fila de atenção. Se não houver, encerra.
12. A nova atenção volta para a mesma fila compartilhada de atenção e será consumida no próximo ciclo por algum worker disponível. Pode ser o mesmo worker ou outro. O ponto é que a fila de uma loja grande não monopoliza o processamento indefinidamente.

Encontrar a fila vazia não é erro. Uma atenção pode chegar atrasada: quando o worker olha a fila de trabalho, ela já pode ter sido consumida por outro ciclo. Nesse caso, ele apenas confirma a atenção e encerra.

O ponto importante é que o sinal de atenção é barato, pequeno e repetível.

Ele não precisa representar exatamente uma mensagem de trabalho. Ele representa uma intenção: essa fila merece ser observada e provavelmente precisa ser consumida.

Por isso, o pedido de atenção deve ser idempotente. O sistema precisa aceitar a possibilidade de receber duas ou mais atenções para a mesma loja sem duplicar processamento indevido. No pior caso, uma atenção extra inicia uma tentativa que encontra a fila vazia, é bloqueada pelo limite de concorrência, ou percebe que o backlog já foi processado por outro ciclo.

Isso muda a forma de pensar. A fila de atenção não é a fila de trabalho. Ela é a fila de coordenação. Ela funciona como uma fila de escalonamento: decide qual fila de trabalho recebe a próxima fatia de processamento.

## Exemplo completo

No MarketHub, cada loja possui uma fila própria:

```text
process.marketplace.store_{storeId}.work
```

O conjunto de integrações com marketplaces possui uma fila de atenção:

```text
attention.marketplace.work
```

Quando a loja `store-873` precisa sincronizar pedidos ou atualizar estoque em um marketplace, o integrador faz uma única chamada HTTP para a API. Para quem integra, a operação termina aí: ele enviou a solicitação para o MarketHub. Quem sabe que também precisa gerar um pedido de atenção é a própria API.

Depois de receber a chamada HTTP, a API faz duas publicações internas no broker. Essa dupla publicação precisa ser tratada como uma unidade operacional: não basta publicar a mensagem de trabalho e torcer para que o pedido de atenção também seja publicado. Se a primeira publicação funcionar e a segunda falhar, a fila da loja pode ficar com trabalho pendente sem nenhum sinal para acionar o consumo.

Existem várias formas de proteger esse ponto, dependendo do nível de garantia exigido pela aplicação: usar confirmação de publicação do broker, aplicar outbox pattern, fazer retry idempotente da publicação de atenção ou manter uma reconciliação periódica que encontre filas com backlog sem atenção recente. O detalhe da técnica pode variar, mas a decisão arquitetural é a mesma: mensagem de trabalho e pedido de atenção fazem parte da mesma intenção operacional.

Nos exemplos abaixo, aparecem três termos comuns em RabbitMQ. O `exchange` é o ponto onde a aplicação publica a mensagem. A `routingKey` é a chave usada para decidir o caminho da mensagem. A `queue` é a fila onde a mensagem fica armazenada até algum consumidor processá-la.

A primeira publicação carrega o trabalho real:

```text
exchange: process.marketplace
routingKey: store.store-873
body: tarefa completa de sincronização
```

A segunda publicação carrega apenas o pedido de atenção:

```json
{
  "tenantId": "seller-group-a",
  "storeId": "store-873",
  "marketplace": "mercado-livre",
  "priority": "normal"
}
```

O worker de atenção recebe esse segundo evento e monta a fila real:

```text
process.marketplace.store_873.work
```

Antes de consumir, aplica uma política:

```text
maxConsumptionTimeSeconds = 20
maxMessages = 100
maxConcurrentConsumers = 2
```

Nesse exemplo, `maxConcurrentConsumers = 2` significa que no máximo dois consumidores podem processar a fila dessa loja ao mesmo tempo. Em outro cenário, esse valor poderia ser `1`, garantindo um único consumidor ativo por fila de trabalho. Isso é útil quando a ordem dos eventos importa, quando há risco de conflito em atualizações de estoque, ou quando a API do marketplace exige chamadas mais controladas. Se a loja tiver um plano maior, integração estável e operações independentes, o limite poderia ser `10`.

Então ele tenta adquirir um token de concorrência para essa loja. Se já existem consumidores suficientes processando essa fila, a atenção é republicada para uma tentativa futura.

Se houver permissão, o worker consome até 100 mensagens ou até 20 segundos. Ele não continua até o fim da fila porque isso permitiria que uma única loja ruidosa ocupasse o worker por tempo demais. Se ainda restarem mensagens, ele republica uma nova atenção. Essa nova atenção volta para `attention.marketplace.work` e será disputada pelos consumidores dessa fila de atenção. Se a fila estiver vazia, ele encerra.

## Implementação

O padrão pode ser implementado com cinco componentes.

Ele não exige uma abstração genérica de mensageria. Os exemplos a seguir usam vocabulário comum em aplicações .NET e RabbitMQ, mas a parte mais importante é o contrato explícito do domínio: uma mensagem de trabalho, uma mensagem de atenção, um worker que entende esse contrato e uma política clara de consumo.

O primeiro é a fila de trabalho granular. Ela guarda mensagens reais por entidade, cliente, loja, conta ou qualquer unidade que precise de isolamento. É nela que está o payload que a regra de negócio vai processar.

O segundo é a fila de atenção agregada. Ela recebe sinais pequenos agrupados por tipo. Essa fila é consumida por poucos workers permanentes.

O terceiro é o envelope de atenção. Ele contém os identificadores mínimos para localizar a fila de trabalho que precisa de atenção. Esse envelope deve ser seguro para repetição: publicar ou consumir o mesmo pedido mais de uma vez não pode corromper o estado do sistema. Em C#, ele seria uma classe simples, por exemplo:

```csharp
public sealed class AttentionRequest
{
    public required string TenantId { get; init; }
    public required string StoreId { get; init; }
    public required string Marketplace { get; init; }
}
```

O quarto é o worker de atenção. Ele valida o estado da entidade, inicia um consumidor temporário, processa em lote controlado e decide se precisa de nova atenção. Esse worker não precisa esconder RabbitMQ atrás de uma abstração genérica; ele pode chamar APIs explícitas do broker ou serviços internos próprios.

O quinto é o controle de concorrência. Ele normalmente é apoiado por Redis, banco de dados ou lock distribuído, para impedir que múltiplos workers processem a mesma fila além do limite permitido. Esse limite não precisa ser maior que 1. Em muitos casos, o valor correto é exatamente 1 consumidor ativo por fila de trabalho. Em outros, o limite pode ser 10 ou mais, desde que o processamento seja independente, idempotente e seguro para paralelismo.

Em uma base .NET, esses componentes poderiam aparecer como contratos próprios do sistema:

```csharp
public interface IAttentionPublisher
{
    Task PublishAsync(AttentionRequest request, CancellationToken cancellationToken);
}

public interface IAttentionWorker
{
    Task<AttentionResult> ProcessAsync(AttentionRequest request, CancellationToken cancellationToken);
}

public enum AttentionResult
{
    Done,
    NeedMoreAttention
}
```

Essas interfaces não precisam prometer que servem para qualquer broker ou qualquer caso de mensageria. Elas existem para representar uma decisão arquitetural específica: publicar pedidos de atenção e consumir filas de trabalho sob demanda.

Um pseudocódigo possível:

No pseudocódigo, `ack attention` significa confirmar ao broker que aquela mensagem de atenção foi tratada e pode sair da fila. Se o worker falhar antes do `ack`, o broker pode tentar entregar a mesma atenção de novo, dependendo da configuração. Esse é mais um motivo para o pedido de atenção ser idempotente. O oposto do `ack` costuma ser chamado de `nack`, usado quando a mensagem não foi processada com sucesso e deve seguir a política de erro ou retentativa.

```text
on attention_received(attention):
    resource = load_resource(attention.resource_id)

    if resource cannot receive processing:
        ack attention
        return

    queue_name = build_work_queue_name(resource)

    if rate_limit_blocked(queue_name):
        republish attention
        ack attention
        return

    if queue_does_not_exist(queue_name):
        ack attention
        return

    if queue_is_empty(queue_name):
        ack attention
        return

    consume_until(
        queue = queue_name,
        max_messages = resource.max_messages,
        max_time = resource.max_time
    )

    if queue_has_remaining_messages(queue_name):
        republish attention

    ack attention
```

O `republish attention` não é uma recursão nem uma chamada imediata ao mesmo worker. Ele coloca um novo pedido no fim da fila de atenção. Depois disso, o worker atual finaliza o ciclo e fica livre para pegar o próximo pedido disponível. O broker entrega a nova atenção quando ela chegar à vez dela, respeitando a concorrência e a ordem operacional da fila.

Em RabbitMQ, uma implementação concreta pode usar uma topologia semelhante. O `binding` é a regra que liga um exchange a uma queue.

```text
exchange: attention.marketplace
queue: attention.marketplace.work
binding: store.*

exchange: process.marketplace
queue: process.marketplace.store_873.work
binding: store.store-873
```

A API publica o payload completo na fila de trabalho e publica um envelope pequeno na fila de atenção:

```text
HTTP request from integrator
API validates request
API publishes process event
API publishes attention event
```

Na implementação real, esses dois últimos passos devem ter uma estratégia explícita de consistência. Se não houver uma transação única envolvendo tudo, a aplicação precisa de confirmação, retry, outbox ou reconciliação para não deixar trabalho sem atenção.

O worker de atenção, por sua vez, não precisa conhecer todas as lojas antecipadamente. Ele só precisa saber transformar o envelope de atenção em nome de fila, política de consumo e chave de controle de concorrência.

## Observabilidade

Attention Queue só é confortável de operar quando o sistema mostra claramente onde há trabalho parado, onde há excesso de atenção e onde há bloqueio por política.

Algumas métricas úteis:

| Métrica | O que revela |
| --- | --- |
| Quantidade de mensagens por fila de loja | Quais lojas estão acumulando backlog |
| Idade da mensagem mais antiga por fila | Há quanto tempo a loja mais atrasada espera processamento |
| Número de atenções republicadas | Quais filas precisam de muitos ciclos para esvaziar |
| Atenções descartadas por loja desativada ou inválida | Quanto trabalho está sendo ignorado por estado operacional |
| Bloqueios por rate limit | Quais lojas estão batendo no limite de concorrência ou SLA |
| Tempo médio para zerar backlog | Quanto tempo o sistema leva para recuperar uma fila com trabalho pendente |

Essas métricas ajudam a separar problemas diferentes. Uma loja pode estar lenta porque tem backlog real, porque está limitada por plano, porque a integração está bloqueada, porque há muitas atenções repetidas, ou porque os workers disponíveis não são suficientes. Sem essas medidas, o padrão continua funcionando, mas fica difícil explicar seu comportamento em produção.

## Quando não usar

Attention Queue não deve ser tratado como solução padrão para qualquer processamento assíncrono.

Se o sistema tem poucas filas, volume previsível e consumidores permanentes baratos de manter, o padrão pode adicionar complexidade desnecessária. Se uma fila única já atende bem, com latência aceitável e sem problemas de isolamento, talvez não exista dor suficiente para justificar filas granulares e pedidos de atenção.

Também não é uma boa escolha quando o processamento precisa obedecer uma ordem estritamente global entre todas as mensagens. O padrão favorece isolamento e justiça entre filas, não uma ordenação única do sistema inteiro.

Outro ponto é custo operacional. Filas dinâmicas exigem convenção de nomes, criação, remoção, monitoramento e capacidade de diagnóstico. Se o broker ou o time ainda não consegue operar muitas filas com segurança, é melhor amadurecer essa base antes de adotar o padrão.

## Benefícios

O benefício principal do padrão é alinhar consumo com necessidade real.

Ele permite manter milhares ou milhões de filas lógicas sem exigir milhares ou milhões de consumidores ativos. O sistema fica mais elástico porque consumidores aparecem quando há backlog e desaparecem quando o trabalho acaba.

Também melhora o isolamento. Uma entidade ruidosa não precisa contaminar o fluxo das demais, porque cada entidade pode ter sua fila, seus limites e sua política de consumo.

Outro benefício é justiça operacional, ou fairness, entre lojas. O padrão distribui o processamento em fatias e evita que uma loja grande capture a maior parte dos workers apenas por ter mais volume. Ela pode receber mais atenção se a política permitir, mas isso passa a ser uma decisão explícita do sistema, não um efeito colateral do backlog.

Outro benefício é a governança operacional. Como o worker de atenção passa por uma etapa de enriquecimento antes de consumir, ele pode verificar estado, permissões, locks, manutenção, prioridade e limites antes de gastar esforço processando mensagens.

O padrão também abre espaço para acordos comerciais diferentes. Como a atenção passa por uma política interna antes de virar consumo real, o sistema pode dar tratamento diferente para lojas em planos diferentes: mais consumidores simultâneos, janelas de consumo maiores, mais mensagens por ciclo, prioridade maior na republicação da atenção ou regras específicas para campanhas e datas sazonais.

Um exemplo simples:

| Plano | Consumidores simultâneos por loja | Mensagens por ciclo |
| --- | ---: | ---: |
| Basic | 1 | 50 |
| Pro | 3 | 200 |
| Enterprise | 10 | 1000 |

Isso permite transformar capacidade de processamento em SLA comercial sem expor a complexidade de filas para o integrador.

Há também um ganho de resiliência: se o processamento não terminar em um ciclo, a própria atenção pode ser republicada. O trabalho progride em fatias, como no time sharing da CPU. O sistema não precisa resolver todo o backlog de uma vez, e uma fila muito cheia não prende o worker indefinidamente.

Em resumo, o padrão Attention Queue é útil quando há muitas filas específicas, volume irregular, necessidade de isolamento e custo alto para manter consumidores permanentes.

Ele transforma processamento contínuo em processamento sob demanda.

A fila de atenção não carrega o peso do trabalho. Ela carrega a consciência de que existe trabalho.
