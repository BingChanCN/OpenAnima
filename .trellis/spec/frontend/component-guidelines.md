# Component Guidelines

> How components are built in this project.

---

## Overview

Components are standard Blazor components with a pragmatic mix of:

- inline `@code` blocks for most logic
- optional `.razor.css` files for isolated styling
- occasional `.razor.cs` partial classes when the logic is large or integration-heavy
- injected application services for shared state and behavior

Representative examples:

- `src/OpenAnima.Core/Components/Shared/NodeCard.razor`
- `src/OpenAnima.Core/Components/Shared/ProviderDialog.razor`
- `src/OpenAnima.Core/Components/Pages/Monitor.razor.cs`

---

## Component Structure

Typical structure:

1. `@using` directives
2. `@inject` dependencies
3. markup
4. `@code` with parameters, local state, lifecycle methods, handlers, and disposal

When a component subscribes to external events, it usually implements `IDisposable` or `IAsyncDisposable` and unsubscribes on teardown.

Examples:

- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`: event subscriptions and async disposal
- `src/OpenAnima.Core/Components/Shared/ChatInput.razor`: JS object reference setup and cleanup
- `src/OpenAnima.Core/Components/Shared/NodeCard.razor`: parameters plus service-driven rendering

---

## Props Conventions

Blazor parameters are strongly typed and usually declared at the top of `@code`.

- use `[Parameter]` for incoming values
- use `[Parameter, EditorRequired]` for required component inputs
- use `EventCallback` or `EventCallback<T>` for child-to-parent actions
- default nullable/component-required behavior should be explicit

Representative examples:

- `src/OpenAnima.Core/Components/Shared/NodeCard.razor`: `[Parameter, EditorRequired] public ModuleNode Node { get; set; } = null!;`
- `src/OpenAnima.Core/Components/Shared/ProviderCard.razor`: strongly typed callbacks such as `EventCallback<LLMProviderRecord>`
- `src/OpenAnima.Core/Components/Shared/TimelineFilterBar.razor`: multiple typed parameters and callbacks for controlled UI state

---

## Styling Patterns

The default styling approach is CSS isolation with `ComponentName.razor.css`.

- component-scoped styles belong in `.razor.css`
- global assets belong in `wwwroot/css/app.css`
- inline styles are used sparingly for dynamic values or temporary layout glue

Representative examples:

- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor.css`
- `src/OpenAnima.Core/Components/Shared/NodeCard.razor.css`
- `src/OpenAnima.Core/Components/Pages/Dashboard.razor.css`

Avoid pushing a component’s styling into `app.css` unless it is truly global.

---

## Accessibility

Accessibility is handled at the component level with normal HTML semantics plus keyboard support where needed.

Common patterns in the codebase:

- keyboard handlers on focusable containers
- `title` attributes and status text for SVG-heavy UI
- `role` or visible status messaging for empty/configuration states
- real `<button>` elements for actions instead of clickable `<div>` wrappers

Representative examples:

- `src/OpenAnima.Core/Components/Pages/Editor.razor`: `tabindex="0"` and keyboard handling
- `src/OpenAnima.Core/Components/Shared/NodeCard.razor`: tooltip text for SVG nodes and ports
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`: status messaging when the pipeline is not configured

---

## Common Mistakes

- Forgetting to unsubscribe from service events in `Dispose` or `DisposeAsync`
- Using JS for state that should live in a Blazor service
- Passing loosely typed `object` values where a record or dedicated type is clearer
- Skipping CSS isolation and leaking component styles globally
