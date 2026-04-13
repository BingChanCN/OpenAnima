# State Management

> How state is managed in this project.

---

## Overview

State is managed primarily through Blazor DI services plus local component fields. There is no separate frontend state library.

The main categories are:

- singleton runtime/application state
- scoped per-circuit UI state
- component-local transient state
- durable persisted state loaded through services

---

## State Categories

### Singleton state

Use singleton services for app-wide or runtime-wide state that must survive across components.

Representative examples:

- `src/OpenAnima.Core/Anima/AnimaContext.cs`
- `src/OpenAnima.Core/Providers/LLMProviderRegistryService.cs`
- `src/OpenAnima.Core/Services/LanguageService.cs`

### Scoped state

Use scoped services for UI/session state that should be shared within a circuit but not treated as global runtime state.

Representative examples:

- `src/OpenAnima.Core/Services/ChatSessionState.cs`
- `src/OpenAnima.Core/Services/EditorStateService.cs`
- `src/OpenAnima.Core/Services/ChatBackgroundExecutionService.cs`

### Component-local state

Use private fields for ephemeral UI details such as modal input buffers, temporary validation messages, loading flags, and drag state local to one component.

Representative examples:

- `src/OpenAnima.Core/Components/Shared/ProviderDialog.razor`
- `src/OpenAnima.Core/Components/Shared/ChatInput.razor`
- `src/OpenAnima.Core/Components/Pages/Monitor.razor.cs`

### Durable state

Persisted state is owned by services, then loaded into components on demand.

Representative examples:

- `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs`
- `src/OpenAnima.Core/ViewportPersistence/ViewportStateService.cs`
- `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs`

---

## When to Use Global State

Promote state into an injected service when:

- multiple components need to read or mutate it
- the state needs persistence or recovery
- the state mirrors runtime/backend state instead of one UI widget
- the state needs event notifications across components

Keep state local when:

- only one component owns it
- it is a short-lived form buffer or spinner flag
- persisting it would add complexity without reuse

Representative examples:

- `EditorStateService` owns editor graph state because `Editor`, `EditorCanvas`, `NodeCard`, and sidebars all depend on it
- `ProviderDialog` keeps `_displayName`, `_baseUrl`, and validation errors local because they are modal-local input state

---

## Server State

Server state is not wrapped in a client cache abstraction. Components fetch directly from injected services and subscribe to updates from service events or SignalR.

Patterns to follow:

- load once in lifecycle methods
- refresh on explicit domain events
- keep the service as the source of truth

Representative examples:

- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`
- `src/OpenAnima.Core/Components/Pages/Editor.razor`
- `src/OpenAnima.Core/Components/Pages/Monitor.razor.cs`

For chat specifically:

- `ChatPanel.razor` is a rendering and interaction layer only.
- `ChatBackgroundExecutionService.cs` owns in-flight chat generation, cancellation, tool-event projection, and history restore.
- Do not tie long-running chat execution ownership to component mount/unmount lifecycle. Navigation away from chat must not cancel background execution by itself.

---

## Common Mistakes

- Copying service-owned canonical state into multiple components and letting it drift
- Using singleton lifetime for per-session UI state
- Forgetting to trigger `StateHasChanged` after service or SignalR callbacks
- Persisting modal-local input state in a global service without a real cross-component need
