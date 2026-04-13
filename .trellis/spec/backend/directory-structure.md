# Directory Structure

> How backend code is organized in this project.

---

## Overview

This repository is a .NET 8 solution split by responsibility, not by a single monolith folder:

- `src/OpenAnima.Core/`: main runtime, Blazor Server host, persistence, modules, tools, and backend services
- `src/OpenAnima.Contracts/`: cross-assembly interfaces, records, and port metadata used by both runtime and generated modules
- `src/OpenAnima.Cli/`: developer CLI for module scaffolding, validation, and packaging
- `tests/`: xUnit projects for runtime and CLI coverage

For backend work, most changes land in `OpenAnima.Core`, but cross-boundary contracts belong in `OpenAnima.Contracts`.

---

## Directory Layout

```text
src/
├── OpenAnima.Core/
│   ├── Anima/                 # Runtime lifecycle and active Anima context
│   ├── ChatPersistence/       # Durable chat SQLite storage
│   ├── Components/            # Blazor UI (frontend, but often wired to backend services)
│   ├── DependencyInjection/   # IServiceCollection extension-based registration
│   ├── Events/                # Event bus primitives and payload records
│   ├── Hosting/               # Startup/recovery hosted services
│   ├── LLM/                   # OpenAI client integration and token counting
│   ├── Memory/                # Memory graph domain and sedimentation logic
│   ├── Modules/               # Runtime modules executed inside wiring graphs
│   ├── Ports/                 # Port discovery and validation
│   ├── Providers/             # Persistent LLM provider registry
│   ├── RunPersistence/        # Durable run SQLite schema and repositories
│   ├── Runs/                  # Run domain models and orchestration
│   ├── Services/              # Application services and stateful coordinators
│   ├── Tools/                 # Workspace tool implementations
│   ├── ViewportPersistence/   # Per-Anima viewport persistence
│   ├── Wiring/                # Wiring config loading and execution graph helpers
│   └── Workflows/             # Preset discovery and workflow metadata
├── OpenAnima.Contracts/       # Shared interfaces / records / attributes
└── OpenAnima.Cli/             # CLI commands, models, services, templates
```

---

## Module Organization

Use the existing feature folders instead of creating generic `Helpers` or `Utils` buckets.

- Put runtime orchestration and long-lived services in `Services/`, `Runs/`, `Memory/`, or another domain folder that matches the feature.
- Put persistence code beside its data store, for example `RunPersistence/RunRepository.cs` and `ChatPersistence/ChatHistoryService.cs`.
- Put DI wiring in `DependencyInjection/*ServiceExtensions.cs`, not inline in `Program.cs`, unless it is truly app-host-only setup.
- Put contracts shared across assemblies in `OpenAnima.Contracts`, not duplicated in `OpenAnima.Core`.
- Keep CLI-specific logic in `OpenAnima.Cli`; do not leak CLI models into runtime assemblies.

Representative examples:

- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs`: groups all run-related registrations in one place
- `src/OpenAnima.Core/RunPersistence/RunRepository.cs`: persistence code stays near the `Runs` domain
- `src/OpenAnima.Contracts/Ports/PortMetadata.cs`: shared contract lives outside runtime

---

## Naming Conventions

- Use `PascalCase` for files, classes, records, and folders.
- Interface names use the `I*` prefix, for example `IRunRepository`, `IModuleService`, `IWorkspaceTool`.
- Registration helpers use the `*ServiceExtensions` suffix.
- Durable storage helpers use `*ConnectionFactory`, `*Initializer`, and `*Repository`.
- Immutable payloads and DTO-like types are usually `record`s, for example `RunDescriptor`, `ToolCallStartedPayload`, and `ValidationResult`.

Avoid adding new files with vague names such as `Common.cs`, `Utils.cs`, or `Helpers.cs` unless the abstraction is already an established project-level concept.

---

## Examples

- `src/OpenAnima.Core/Program.cs`: host bootstrap is intentionally thin and delegates most registration to extension classes
- `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs`: feature registration sits next to the feature
- `src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs`: domain orchestration lives under its own feature folder
- `src/OpenAnima.Cli/Services/TemplateEngine.cs`: CLI service code stays in the CLI assembly

---

## Common Mistakes

- Adding new cross-cutting logic directly to `Program.cs` instead of extending the matching `*ServiceExtensions.cs`
- Duplicating a contract in `OpenAnima.Core` when it is actually consumed by both runtime and CLI or plugin-facing code
- Creating a catch-all utility file instead of placing code under the owning feature directory
- Mixing persistence concerns into UI components when a service or repository already owns that boundary
