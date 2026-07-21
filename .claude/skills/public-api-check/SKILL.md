---
name: public-api-check
description: Procedure for Microsoft.CodeAnalysis.PublicApiAnalyzers in Oragon.RabbitMQ (RS0016/RS0017 errors, PublicAPI.Shipped/Unshipped.txt). Use when adding, changing or removing public API surface.
---

# Public API Check

## Arquivos

`PublicAPI.Shipped.txt` e `PublicAPI.Unshipped.txt` nos projetos src (localize com Glob `src/**/PublicAPI.*.txt`).

## Regras

- **Nova API** → adicionar linha em `Unshipped.txt` com a assinatura completa, incluindo anotações de nullability (`?`, `!`). Ex.: `Oragon.RabbitMQ.Consumer.ConsumerDescriptor.WithPrefetch(ushort prefetch) -> Oragon.RabbitMQ.Consumer.ConsumerDescriptor!`
- **Remoção de API** → linha com prefixo `*REMOVED*` em `Unshipped.txt`.
- **Interfaces geradas pelo AutomaticInterface** (`[GenerateAutomaticInterface]`) também contam na superfície pública — mudar a classe muda a interface gerada.
- **Multi-target net8/9/10**: a assinatura precisa valer nos 3 TFMs; APIs condicionais por TFM exigem entradas condicionais.
- `Shipped.txt` só é alterado no release (promoção Unshipped→Shipped — ver skill `release-pack`).

## Erros do analyzer

- **RS0016** (símbolo público não declarado): adicionar a assinatura em `Unshipped.txt`. O quick-fix do IDE gera a assinatura exata; sem IDE, copie o formato de linhas vizinhas.
- **RS0017** (declarado mas não existe): remover a linha órfã — geralmente sobra de rename/remoção.

## Validação

`dotnet build ./Oragon.RabbitMQ.slnx` — com TreatWarningsAsErrors, RS0016/RS0017 quebram o build, então build limpo = superfície consistente.
