---
name: benchmark-run
description: Run and compare BenchmarkDotNet benchmarks for Oragon.RabbitMQ. Use when asked to measure performance or validate a performance-sensitive change.
---

# Benchmark Run

## Regra de roteamento

**Execução sempre via agente `ci-runner` (Haiku)** — benchmarks são demorados e o log de warmup é o pior poluidor de contexto que existe. Peça ao ci-runner apenas a tabela final do BenchmarkDotNet.

## Execução

```bash
dotnet run -c Release --project ./benchmarks/Oragon.RabbitMQ.Benchmarks/
```

Resultados ficam em `BenchmarkDotNet.Artifacts/results/` (md/html/csv por benchmark).

## Comparação

1. Antes de mudar código de hot path, rode o baseline e preserve o diretório de artifacts (copie para o scratchpad com timestamp).
2. Após a mudança, rode de novo e compare Mean/Allocated por benchmark.
3. Regra do projeto: mudanças em hot path (loop de dispatch em `Consumer/Dispatch/`, serializers) pedem benchmark antes/depois no PR.

## Interpretação

- Diferenças < ~3% em Mean geralmente são ruído — repita a execução antes de concluir regressão.
- `Allocated` é determinístico: qualquer aumento de alocação em hot path é real e deve ser justificado.
