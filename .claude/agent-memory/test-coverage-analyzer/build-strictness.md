---
name: build-strictness
description: TreatWarningsAsErrors and Nullable are only enabled for src/ projects, not the unit test project
metadata:
  type: project
---

`CLAUDE.md` and task briefs describe the repo as `TreatWarningsAsErrors=true` / nullable-enabled generally, but as of 2026-07 (release/1.10 branch) that's only literally true for `src/Oragon.RabbitMQ.Build.props` (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, imported by all `src/*` projects). The test projects import `tests/Oragon.RabbitMQ.Tests.props`, which only sets `<Nullable>disable</Nullable>` and does **not** set `TreatWarningsAsErrors`. Grep confirmed no `Directory.Build.props` exists anywhere in the repo, and `TreatWarningsAsErrors` only appears in `benchmarks/.../*.csproj` (explicitly `false`) and `src/Oragon.RabbitMQ.Build.props` (`true`).

**Why it matters:** when writing/reviewing unit tests, don't assume an unused parameter, a discarded lambda arg, or an IDE0058-style "expression value never used" pattern will fail the build — it won't, for the test project. Still worth matching the existing style (`_ = mock.Setup(...)`) for consistency and because a future tightening of the props could make it strict.

**How to apply:** when told not to run `dotnet build` (e.g., parallel-agent workspace conflicts), a missing `_ =` discard on a `Setup(...)` chain in a test file is not a compile-blocking risk here — but still match convention. See [[dynamic-queue-consumer-tests]] and [[verifying-without-building]].
