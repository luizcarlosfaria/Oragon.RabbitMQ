---
name: integration-test-run
description: Run Oragon.RabbitMQ integration tests (Testcontainers + Docker RabbitMQ) or reproduce the CI pipeline locally. Use when asked to run integrated tests, validate against real RabbitMQ, or debug Testcontainers/Docker issues.
---

# Integration Test Run

## Regra de roteamento (economia de tokens)

**Delegue a execução ao agente `ci-runner` (haiku) via Task e peça só o resumo de falhas — não rode `dotnet test` de integração inline no contexto principal.** O output de Testcontainers/xUnit é longo e não pode poluir o transcript da sessão.

## Pré-check

1. `docker info` deve responder. Testcontainers precisa do daemon.
2. Ambiente WSL: se `docker` não for encontrado, peça ao usuário para reiniciar o Docker Desktop (integração WSL cai às vezes). Não tente instalar Docker.

## Execução

```bash
# Suite completa (~24 arquivos, demorada — timeout ≥ 10 min)
dotnet test ./tests/Oragon.RabbitMQ.IntegratedTests/Oragon.RabbitMQ.IntegratedTests.csproj

# Um teste específico
dotnet test ./tests/Oragon.RabbitMQ.IntegratedTests/Oragon.RabbitMQ.IntegratedTests.csproj --filter "FullyQualifiedName~MapQueueFullFeaturedTest"
```

Testes que dependem de Testcontainers/RabbitMQ real: `MapQueueFullFeaturedTest`, `MapRpcQueueFullFeaturedTest`, `ConventionBindingTest`, `MultipleConsumersTest`, `AttentionPrimitivesIntegratedTests`, `TestContainersTest`. O bootstrap do container fica em `TestExtensions.cs`.

## Reprodução completa do CI (pipeline local)

```bash
docker build -t oragon-rabbitmq-builder .
docker run --privileged -it --rm \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -w /projeto -v ./:/projeto \
  oragon-rabbitmq-builder
# dentro do container:
dotnet workload restore ./Oragon.RabbitMQ.slnx
dotnet build ./Oragon.RabbitMQ.slnx
dotnet-coverage collect "dotnet test --framework net10.0 -p:TargetFrameworks=net10.0" -f xml -o /output-coverage/coverage.xml
```

O mount do docker.sock é obrigatório: Testcontainers dentro do builder usa o Docker do host.

## Troubleshooting

- **Permission denied no socket**: rodar com `--privileged` ou verificar grupo docker.
- **Containers órfãos**: `docker ps -a --filter "label=org.testcontainers"` → remover com `docker rm -f`.
- **Porta em uso**: Testcontainers usa portas aleatórias; conflito indica container órfão de execução anterior.
- **Timeout de startup do RabbitMQ**: verificar memória/CPU disponível no Docker Desktop.
