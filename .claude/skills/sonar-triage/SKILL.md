---
name: sonar-triage
description: Fetch, classify and route SonarCloud/DeepSource issues for Oragon.RabbitMQ. Use when the quality gate fails or when asked to clean up static-analysis issues.
---

# Sonar Triage

## Fetch barato (API, nunca browsing)

Projeto público, sem auth:

```bash
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=Oragon.RabbitMQ&branch=<branch>&resolved=false&ps=500" \
  | jq -r '.issues[] | [.rule, .severity, .component, (.line // "-"), .message] | @tsv' | column -t -s$'\t'

# Status do quality gate
curl -s "https://sonarcloud.io/api/qualitygates/project_status?projectKey=Oragon.RabbitMQ&branch=<branch>"
```

**Nunca cole o JSON bruto do Sonar no contexto principal** — resuma em tabela (regra / severidade / arquivo / linha / contagem).

## Roteamento por custo

| Categoria | Rota |
|---|---|
| Code smells triviais (S1481, S1135, S1066, naming) e Minor | Lote único → agente `sonar-issue-resolver` (Sonnet) |
| Bugs / Vulnerabilities fora de `Consumer/` | `sonar-issue-resolver`, um a um |
| Qualquer issue em `src/Oragon.RabbitMQ/Consumer/` envolvendo Interlocked / SemaphoreSlim / CTS / shutdown | Agente `concurrency-specialist` |
| Falso positivo do source generator AutomaticInterface | Suprimir com justificativa em comentário |

## Validação final

Build + unit tests via agente `ci-runner` (não inline). Se o quality gate media cobertura, confira também o percentual retornado pela API antes de declarar concluído.
