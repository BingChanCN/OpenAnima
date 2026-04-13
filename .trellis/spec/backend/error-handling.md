# Error Handling

> How errors are handled in this project.

---

## Overview

This codebase does not use a single custom error hierarchy. Instead, it relies on a few consistent patterns:

- constructor guard clauses throw `ArgumentNullException` or `ArgumentException`
- configuration and contract violations throw `InvalidOperationException`, `ArgumentException`, or `FileNotFoundException`
- long-running modules catch operational failures, log them, and publish a failure payload instead of crashing the entire runtime
- cancellations are handled separately from actual failures
- workspace tools return `ToolResult.Failed(...)` for user-facing validation errors instead of throwing

---

## Error Types

Use standard .NET exception types unless a feature boundary already defines a richer result object.

Representative examples:

- `src/OpenAnima.Core/Wiring/ConfigurationLoader.cs`: throws `FileNotFoundException` and `InvalidOperationException` for invalid wiring configs
- `src/OpenAnima.Core/Services/ModuleStorageService.cs`: throws `ArgumentException` and `InvalidOperationException` for invalid module storage requests
- `src/OpenAnima.Core/Tools/FileWriteTool.cs`: returns `ToolResult.Failed(...)` for missing required parameters instead of throwing

For runtime module execution, prefer returning or publishing structured failure data when the consumer can continue.

---

## Error Handling Patterns

### 1. Guard early on invalid inputs

Use guard clauses in constructors and public entry points.

Examples:

- `src/OpenAnima.Core/Memory/SedimentationService.cs`
- `src/OpenAnima.Core/Routing/CrossAnimaRouter.cs`
- `src/OpenAnima.Core/Modules/AnimaInputPortModule.cs`

### 2. Separate cancellation from failure

`OperationCanceledException` is treated as control flow, not as an error.

Examples:

- `src/OpenAnima.Core/Modules/HttpRequestModule.cs`: timeout and pipeline cancellation have dedicated `catch` branches
- `src/OpenAnima.Core/Services/EditorStateService.cs`: autosave ignores cancellation and logs only real failures
- `src/OpenAnima.Core/Components/Shared/ProviderDialog.razor`: test connection cancellation is handled separately

### 3. Log and degrade gracefully in background or recoverable flows

If a module or background process can fail without taking down the app, catch, log, and continue with a safe fallback.

Examples:

- `src/OpenAnima.Core/Memory/SedimentationService.cs`: skips sedimentation on failure after logging a warning
- `src/OpenAnima.Core/Modules/MemoryModule.cs`: logs and emits error payloads for malformed requests
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`: logs restore failures instead of breaking the page
- `src/OpenAnima.Core/Modules/LLMModule.cs` and `src/OpenAnima.Core/LLM/LLMService.cs`: when a custom OpenAI-compatible endpoint rejects `system` messages with a provider-specific 400, retry once with a systemless instruction mapping instead of surfacing the raw provider error immediately

### 4. Prefer explicit result objects at tool and runtime boundaries

Examples:

- `src/OpenAnima.Core/Tools/ToolResult.cs` consumers such as `MemoryWriteTool` and `GitCheckoutTool`
- `src/OpenAnima.Core/Runs/RunResult.cs`

---

## API Error Responses

There is no conventional JSON controller API layer in this project. Error propagation depends on the boundary:

- Blazor components typically catch exceptions locally and keep rendering
- modules publish failure messages to ports or event bus channels
- workspace tools return `ToolResult.Failed(...)`
- CLI commands write a user-facing message and return an exit code

Provider-compatibility note:

- Some OpenAI-compatible third-party endpoints reject `system` role input entirely.
- For custom provider/manual endpoint calls, prefer degrading to a systemless instruction payload over failing the request outright when the upstream error explicitly says system messages are not allowed.
- Keep the raw provider message in logs, but return a stable user-facing failure only if the compatibility retry also fails.

Representative examples:

- `src/OpenAnima.Core/Modules/HttpRequestModule.cs`: publishes serialized error output
- `src/OpenAnima.Core/Modules/AnimaRouteModule.cs`: returns structured route failure information
- `src/OpenAnima.Cli/Commands/ValidateCommand.cs`: converts exceptions into CLI-visible output and exit codes

---

## Common Mistakes

- Catching `Exception` without logging enough context to debug the failure
- Logging cancellations as errors
- Throwing directly from a recoverable module path when a failure payload would preserve runtime continuity
- Returning unstructured string errors at a boundary that already has a typed result object
- Swallowing exceptions silently in UI or background code
