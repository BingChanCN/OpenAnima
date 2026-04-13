# Logging Guidelines

> How logging is done in this project.

---

## Overview

Backend logging uses `Microsoft.Extensions.Logging` with `ILogger<T>`. Logs are generally structured and scoped to the owning class. The CLI is different: it uses `Console.WriteLine` for user-facing output rather than application diagnostics.

Representative examples:

- `src/OpenAnima.Core/Services/ModuleService.cs`
- `src/OpenAnima.Core/Routing/CrossAnimaRouter.cs`
- `src/OpenAnima.Core/Memory/SedimentationService.cs`
- `src/OpenAnima.Cli/Program.cs`

---

## Log Levels

- `LogDebug`: high-frequency internal state that is useful during investigation but too noisy for normal runs
  Examples:
  `EditorStateService`, `ChatContextManager`, `HttpRequestModule`
- `LogInformation`: lifecycle transitions, successful registrations, startup milestones, and durable state changes
  Examples:
  `RunDbInitializer`, `CrossAnimaRouter`, `WiringEngine`
- `LogWarning`: recoverable failures, rejected requests, missing optional configuration, or degraded behavior
  Examples:
  `SedimentationService`, `HttpRequestModule`, `ModuleService`
- `LogError`: unexpected failures that should be investigated
  Examples:
  `LLMModule`, `ModuleService`, `EditorStateService`

---

## Structured Logging

Use message templates with named placeholders, not string concatenation.

Good examples:

- `src/OpenAnima.Core/Services/ModuleService.cs`: `"Loaded module: {Name} v{Version}"`
- `src/OpenAnima.Core/Modules/HttpRequestModule.cs`: `"HttpRequestModule: request completed — status {Status}"`
- `src/OpenAnima.Core/Memory/SedimentationService.cs`: `"Sedimentation failed for anima {AnimaId} — skipping without propagating"`

Conventions:

- Include the domain identity being acted on, such as `AnimaId`, `RunId`, `ModuleId`, `Slug`, or `Url`
- Pass the exception as the first argument to `LogWarning(ex, ...)` or `LogError(ex, ...)`
- Keep messages short and searchable

---

## What to Log

- startup initialization and service registration milestones
- wiring/configuration load and unload events
- module lifecycle changes
- external request outcomes
- persistence initialization and migrations
- recoverable runtime degradation that changes behavior

Representative examples:

- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs`: schema migration and backup events
- `src/OpenAnima.Core/Routing/CrossAnimaRouter.cs`: route registration and request completion
- `src/OpenAnima.Core/Modules/LLMModule.cs`: provider/model execution failures

---

## What NOT to Log

- plaintext secrets, API keys, or encrypted provider blobs
- full prompt or user content unless there is an explicit feature need
- repeated success noise in very hot paths at `Information` level

Current codebase note:

- `src/OpenAnima.Core/Modules/LLMModule.cs` masks the per-Anima API key before logging it
- `src/OpenAnima.Core/Providers/ApiKeyProtector.cs` explicitly documents that decrypted keys must not be logged
- `src/OpenAnima.Core/Components/Shared/ProviderDialog.razor` never pre-populates stored API keys back into the UI

New code should preserve that security posture.

---

## Common Mistakes

- Logging with interpolated strings instead of structured placeholders
- Omitting the exception object when logging a failure
- Logging routine hot-path events at `Information` and flooding the output
- Emitting secrets or raw credentials instead of masked identifiers
