# Quality Guidelines

> Code quality standards for frontend development.

---

## Overview

The frontend is Blazor Server UI with substantial service-driven behavior. Quality here is mostly about predictable component lifecycles, state ownership, localization, cleanup, and keeping JS interop thin.

Current reality:

- styling primarily uses CSS isolation
- shared state is mostly DI-service based
- localization exists through `SharedResources`, though some legacy hard-coded strings remain
- automated test coverage is stronger in backend/CLI code than in UI components, so manual verification still matters

---

## Forbidden Patterns

- Leaving event subscriptions active after a component is disposed
- Moving data/state logic into JS when a C# service should own it
- Using global CSS for component-local styling without a clear reason
- Introducing weakly typed parameters or callbacks in reusable components

Representative examples to follow instead:

- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`
- `src/OpenAnima.Core/Components/Shared/ChatInput.razor`
- `src/OpenAnima.Core/Components/Shared/NodeCard.razor`

---

## Required Patterns

- Use `.razor.css` for component-scoped styles by default
- Use typed `[Parameter]` and `EventCallback<T>` APIs
- Use `Dispose` / `DisposeAsync` when subscribing to services, SignalR, or JS resources
- Prefer `IStringLocalizer<SharedResources>` for new reusable UI text
- Keep JS interop focused on DOM integration, not business logic

Representative examples:

- `src/OpenAnima.Core/Components/Pages/Monitor.razor.cs`
- `src/OpenAnima.Core/Components/Shared/ProviderDialog.razor`
- `src/OpenAnima.Core/Components/Pages/Editor.razor`

---

## Testing Requirements

There is no evidence of a dedicated Blazor component test suite yet. The current baseline is:

- xUnit coverage for backend/runtime/CLI behavior
- manual verification for many UI flows
- service-level logic is often easier to test than component markup

Existing test references:

- `tests/OpenAnima.Tests/PerformanceTests.cs`
- `tests/OpenAnima.Tests/MemoryLeakTests.cs`
- `tests/OpenAnima.Cli.Tests/CliFoundationTests.cs`

For frontend-heavy changes:

- add automated tests when logic can be isolated into services
- otherwise document manual checks for keyboard handling, localization, JS interop, and state synchronization

---

## Code Review Checklist

- Does the component clean up all subscriptions and disposable resources?
- Is state owned by the right layer: component, scoped service, or singleton service?
- Are parameters and callbacks strongly typed?
- Are new user-facing strings localized unless there is a clear reason not to?
- Is JS interop narrow and typed?
- Are styles isolated unless they intentionally need to be global?
