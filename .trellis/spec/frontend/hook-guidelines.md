# Hook Guidelines

> How hook-like patterns are used in this project.

---

## Overview

This project is Blazor Server, so there are no React hooks. The equivalent mechanisms are:

- component lifecycle methods such as `OnInitialized`, `OnInitializedAsync`, `OnAfterRenderAsync`, and `OnParametersSet`
- injected state/services
- event subscriptions with explicit cleanup
- optional `.razor.cs` partial classes for large lifecycle-heavy components

When translating frontend patterns into this codebase, think in Blazor lifecycle and DI terms, not `useState` / `useEffect`.

---

## Custom Hook Patterns

Reusable stateful logic usually lives in an injected service, not in a hook function.

Representative examples:

- `src/OpenAnima.Core/Services/EditorStateService.cs`: shared editor state and commands
- `src/OpenAnima.Core/Services/ChatSessionState.cs`: per-session chat message container
- `src/OpenAnima.Core/Services/LanguageService.cs`: shared language change notifications

Component responsibilities:

- subscribe to service events in initialization
- call `InvokeAsync(StateHasChanged)` when async/service state changes
- unsubscribe during disposal

Representative component examples:

- `src/OpenAnima.Core/Components/Pages/Editor.razor`
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`
- `src/OpenAnima.Core/Components/Pages/Monitor.razor.cs`

---

## Data Fetching

There is no React Query, SWR, or client-side cache layer here. Data fetching happens through injected services and async lifecycle methods.

Patterns to follow:

- fetch on `OnInitializedAsync` or from explicit user actions
- read durable state through services such as `ChatHistoryService` or `ViewportStateService`
- use SignalR callbacks for streaming/live updates
- keep JS interop limited to DOM concerns, not data fetching

Representative examples:

- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`: restore chat history via `ChatHistoryService`
- `src/OpenAnima.Core/Components/Pages/Editor.razor`: restore viewport state from `ViewportStateService`
- `src/OpenAnima.Core/Components/Pages/Monitor.razor.cs`: subscribe to live runtime updates via SignalR

---

## Naming Conventions

There are no `use*` naming rules here. Instead:

- services use descriptive nouns such as `EditorStateService`, `LanguageService`, `ChatSessionState`
- lifecycle handlers are typically named `OnXChanged`, `HandleX`, `RestoreXAsync`, or `LoadXAsync`
- event-like members exposed by services commonly use `OnStateChanged` or `LanguageChanged`

Representative examples:

- `src/OpenAnima.Core/Services/EditorStateService.cs`
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`
- `src/OpenAnima.Core/Components/Shared/ChatInput.razor`

---

## Common Mistakes

- Porting React mental models directly into Blazor and inventing pseudo-hooks
- Duplicating shared logic across multiple components instead of moving it into a service
- Subscribing to events without cleaning them up
- Fetching/persisting through JS interop when a C# service already owns the data
