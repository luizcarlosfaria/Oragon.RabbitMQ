---
name: verifying-without-building
description: How to gain confidence a hand-written test file compiles when explicitly told not to run dotnet build/test (parallel agents sharing the same project)
metadata:
  type: feedback
---

When a task explicitly forbids running `dotnet build`/`dotnet test` (typically because other agents are writing to the same test project in parallel and a centralized validation pass will run later), do the following instead of skipping verification:

1. Read the actual production types being tested (request/result/decision records, interfaces) directly from `src/` — don't guess field names or method signatures.
2. For third-party API surfaces you're not 100% sure about (exact overload, parameter defaults, which namespace a type lives in), don't guess — use a throwaway reflection console app against the exact NuGet package version referenced by the project (`~/.nuget/packages/<pkg>/<version>/lib/<tfm>/*.dll`) to print real constructor/method signatures. This caught, for example, that `ShutdownEventArgs` lives in `RabbitMQ.Client.Events` (not `RabbitMQ.Client`) and that its 3-arg constructor form is valid because `cause`/`cancellationToken` are defaulted.
3. Check `Global.Build.props` / the target `.csproj` / any `GlobalUsings.g.cs` under `obj/` for implicit global usings before assuming a namespace needs an explicit `using` — the Oragon.RabbitMQ test projects globally use `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Net.Http`, `System.Threading`, `System.Threading.Tasks`, `Xunit`.
4. After writing the file, do a final manual read-through (or a brace/paren balance check via a quick script) rather than skipping review entirely just because you can't compile.
5. Record in your final report that build/test validation was intentionally skipped per instructions and should be run by whoever does the centralized pass (e.g., the `ci-runner` agent) — don't imply the tests were verified to pass when they weren't actually compiled.

**Why:** guessing at third-party API shapes (especially RabbitMQ.Client 7.x's async-suffixed methods and ValueTask-vs-Task return types, or Moq's ThrowsAsync/ValueTask support) is a common source of subtle compile failures that a later centralized build pass would have to debug blind, wasting more tokens than the upfront reflection check.
