# Quality Guidelines

> Code quality standards for backend development.

---

## Overview

This codebase favors explicit types, small feature-local abstractions, structured logging, and startup-time wiring over reflection-heavy or framework-heavy patterns. The stack is pragmatic: Dapper, singleton/scoped DI services, and record-heavy domain models.

Project baselines visible today:

- `net8.0`, `Nullable` enabled, `ImplicitUsings` enabled in `src/OpenAnima.Core/OpenAnima.Core.csproj`
- xUnit tests in `tests/OpenAnima.Tests` and `tests/OpenAnima.Cli.Tests`
- many runtime features are integration-tested or manually verified rather than exhaustively unit-tested

---

## Forbidden Patterns

- Adding backend feature registrations inline in `Program.cs` when a feature extension class already exists
- Creating long-lived shared SQLite connections
- Returning untyped `object` or `Dictionary<string, object>` from internal domain APIs when a record or interface would be clearer
- Logging raw secrets or credentials
- Introducing a generic catch-all utility file instead of placing code in the owning feature folder

Representative counter-examples to follow instead:

- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs`
- `src/OpenAnima.Core/RunPersistence/RunRepository.cs`
- `src/OpenAnima.Core/Providers/ApiKeyProtector.cs`

---

## Required Patterns

- Use constructor guard clauses for required dependencies
- Keep DI registration in `DependencyInjection/*ServiceExtensions.cs`
- Use `record` for immutable payloads and row models where appropriate
- Propagate `CancellationToken` through async I/O paths
- Use structured `ILogger<T>` logging in runtime/backend services
- Alias SQL columns explicitly for Dapper row mapping

Representative examples:

- `src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs`
- `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs`
- `src/OpenAnima.Core/Modules/HttpRequestModule.cs`

---

## Testing Requirements

There is no evidence of mandatory test-per-change enforcement yet, but the current test style is clear:

- use xUnit
- prefer temp-directory integration tests for filesystem/plugin behavior
- use `NullLogger<T>.Instance` when logging is not under test
- disable parallelization when console or shared process state is involved

Representative examples:

- `tests/OpenAnima.Tests/PerformanceTests.cs`: integration-style runtime performance test
- `tests/OpenAnima.Tests/MemoryLeakTests.cs`: temp-directory plugin lifecycle test
- `tests/OpenAnima.Cli.Tests/CliFoundationTests.cs`: CLI process behavior and output capture

For new backend work:

- add or update automated tests when touching repositories, CLI behavior, persistence, or non-trivial service logic
- if UI-heavy runtime work is hard to automate, document manual verification clearly

---

## Code Review Checklist

- Does the change fit the existing folder and DI registration structure?
- Are contracts placed in `OpenAnima.Contracts` only when they truly cross assembly boundaries?
- Are logs structured and free of secrets?
- Are cancellation and recoverable failure paths handled separately?
- If SQL changed, are aliases, parameters, and startup migrations correct?
- If persistence behavior changed, is there at least one automated or manual verification path?
