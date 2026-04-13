# Backend Development Guidelines

> Project-specific backend conventions for OpenAnima.

---

## Overview

The backend is a .NET 8 runtime centered on `OpenAnima.Core`, with shared contracts in `OpenAnima.Contracts` and developer tooling in `OpenAnima.Cli`.

The project does not follow a classic ASP.NET controller/service/repository split. Instead, it is organized by runtime capability:

- runtime orchestration in `Anima/`, `Runs/`, `Wiring/`, and `Modules/`
- persistence in dedicated feature folders such as `RunPersistence/` and `ChatPersistence/`
- DI composition in `DependencyInjection/`
- shared interfaces and immutable payloads in `OpenAnima.Contracts`

---

## Guidelines Index

| Guide | Description | Status |
|-------|-------------|--------|
| [Directory Structure](./directory-structure.md) | Module organization and file layout | Filled |
| [Database Guidelines](./database-guidelines.md) | Dapper, SQLite, schema initialization | Filled |
| [Error Handling](./error-handling.md) | Exceptions, result objects, recoverable failures | Filled |
| [Quality Guidelines](./quality-guidelines.md) | Code standards, testing expectations, review checklist | Filled |
| [Logging Guidelines](./logging-guidelines.md) | Structured logging and secret-safe diagnostics | Filled |

---

## Pre-Development Checklist

Read these documents before backend changes:

- any backend change: [Directory Structure](./directory-structure.md) and [Quality Guidelines](./quality-guidelines.md)
- persistence or schema work: [Database Guidelines](./database-guidelines.md)
- recoverability or runtime failure handling: [Error Handling](./error-handling.md)
- new diagnostics or operational changes: [Logging Guidelines](./logging-guidelines.md)

For cross-layer work that touches Blazor UI, runtime services, and persistence together, also read [Cross-Layer Thinking Guide](../guides/cross-layer-thinking-guide.md).

---

## Collaboration Language

- Use Chinese for agent-user conversation in this project.
- Keep backend spec documentation and guideline updates written in English.

---

## Core Backend Patterns

- Register features through `DependencyInjection/*ServiceExtensions.cs`, keeping `Program.cs` thin.
- Prefer explicit records and interfaces over dynamic payloads.
- Use startup-time, idempotent SQLite schema initialization instead of external migrations.
- Use `ILogger<T>` with structured templates for runtime code; reserve `Console.WriteLine` for CLI output.

Representative files:

- `src/OpenAnima.Core/Program.cs`
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs`
- `src/OpenAnima.Core/RunPersistence/RunRepository.cs`
- `src/OpenAnima.Core/Modules/HttpRequestModule.cs`

---

**Language**: Use Chinese for collaboration, but keep backend documentation in English.
