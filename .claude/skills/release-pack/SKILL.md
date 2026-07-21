---
name: release-pack
description: Release checklist for Oragon.RabbitMQ — versioning, changelog, PublicAPI promotion, pack, tag conventions, publish targets. Use when preparing a release, alpha/beta, or NuGet pack.
---

# Release / Pack

## Convenção de tags (Jenkins publica; local só valida)

- `vX.Y.Z-alpha` → pacote debug + symbols no **MyGet**
- `vX.Y.Z-beta` e release final → **MyGet + NuGet**

## Checklist (ordem)

1. **CHANGELOG.md** atualizado com as mudanças da versão.
2. **PublicAPI**: promover conteúdo de `PublicAPI.Unshipped.txt` → `PublicAPI.Shipped.txt` (ver skill `public-api-check`). Só no release — nunca durante desenvolvimento.
3. **Build limpo nos 3 TFMs** (net8/9/10): `dotnet build ./Oragon.RabbitMQ.slnx`.
4. **Suite completa** (unit + integrated) — delegar ao agente `ci-runner`; integração exige Docker.
5. **Pack** de cada projeto src:
   ```bash
   dotnet pack ./src/<Projeto>/<Projeto>.csproj --configuration Release -p:PackageVersion=X.Y.Z
   ```
   Projetos: Oragon.RabbitMQ, .Abstractions, .AspireClient, .Serializer.SystemTextJson, .Serializer.NewtonsoftJson.
6. **Inspecionar o .nupkg**: dependências corretas por TFM, readme, ícone (src/Assets), símbolos quando alpha.
7. **Tag + push** — a tag dispara o pipeline Jenkins que publica.

## Regra

Decisão de versão (major/minor/patch) é do usuário — apresente as mudanças e pergunte; a skill não decide semver sozinha.
