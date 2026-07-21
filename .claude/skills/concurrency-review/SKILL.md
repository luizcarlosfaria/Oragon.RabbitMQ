---
name: concurrency-review
description: Checklist and routing for any change touching src/Oragon.RabbitMQ/Consumer/ (QueueConsumer, DynamicQueueConsumer, ConsumerServer, graceful shutdown, semaphores, CTS). Use before implementing or reviewing concurrency-sensitive code.
---

# Concurrency Review — Consumer Pipeline

## Passo 0 (obrigatório)

Leia `.claude/agent-memory/dotnet-messaging-reviewer/known-issues.md` antes de tocar em qualquer arquivo de `src/Oragon.RabbitMQ/Consumer/`. Ele registra races reais já encontradas.

## Invariantes do projeto (não negociáveis)

1. **Ack/nack exatamente uma vez** por delivery — nunca perder, nunca duplicar settlement.
2. **Drain antes de fechar**: mensagens in-flight devem completar (ou expirar no DrainTimeout) antes de fechar o canal.
3. **Ordem de disposal**: linked CTS → cancelamento do consumer → channel → connection.
4. **Semáforo liberado em todo caminho de exceção** — todo `WaitAsync` tem um `Release` garantido em `finally` ou equivalente provado.
5. **Check-then-act é suspeito por padrão** — especialmente sobre `completion.Task.IsCompleted` e contadores lidos com `Volatile.Read`.
6. **Dois tokens distintos** no dynamic consumer: handler usa stopCts; settlement (ack/nack) usa o token externo — não misturar.

## Matriz de decisão (roteamento por custo)

| Tipo de mudança | Rota |
|---|---|
| Superfície: rename, docs, log message, XML doc | `dotnet-implementer` (Sonnet) ou inline |
| Caminho de sincronização, shutdown, stop rules, contadores, semáforo, CTS | **OBRIGATÓRIO: agente `concurrency-specialist`** (análise antes de implementar) |
| Pós-implementação de qualquer mudança em Consumer/ | Review com `dotnet-messaging-reviewer` (Opus) |
| Execução de testes de validação | `ci-runner` (Haiku) |

## Regras de entrega

- Todo fix de race exige a descrição do interleaving no commit message.
- Teste de regressão determinístico quando viável; senão, cenário descrito para teste integrado.
- Arquivos críticos: `Consumer/QueueConsumer.cs`, `Consumer/DynamicQueues/DynamicQueueConsumer.cs`, `Consumer/ConsumerServer.cs`, `Abstractions/Consumer/DynamicQueues/IAmqpConcurrencyGate.cs`.
