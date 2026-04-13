# Frontend Development Guidelines

> Project-specific frontend conventions for OpenAnima.

---

## Overview

The frontend is a Blazor Server UI hosted inside `OpenAnima.Core`. That means “frontend” work is tightly coupled to runtime services and DI lifetimes:

- routeable pages live in `Components/Pages/`
- reusable UI lives in `Components/Shared/`
- component styles usually use `.razor.css`
- shared UI state is held in C# services, not a JavaScript state library

This is not a React codebase, so any “hook” guidance should be interpreted through Blazor lifecycle methods and DI services.

---

## Guidelines Index

| Guide | Description | Status |
|-------|-------------|--------|
| [Directory Structure](./directory-structure.md) | Component/page/service organization | Filled |
| [Component Guidelines](./component-guidelines.md) | Blazor component patterns, parameters, styling | Filled |
| [Hook Guidelines](./hook-guidelines.md) | Blazor lifecycle and service-based hook equivalents | Filled |
| [State Management](./state-management.md) | DI-service state ownership and persistence boundaries | Filled |
| [Quality Guidelines](./quality-guidelines.md) | Cleanup, localization, JS interop, review checklist | Filled |
| [Type Safety](./type-safety.md) | C# nullable safety, records, typed parameters | Filled |

---

## Pre-Development Checklist

Read these documents before frontend changes:

- any component work: [Directory Structure](./directory-structure.md) and [Component Guidelines](./component-guidelines.md)
- shared state or data flow changes: [State Management](./state-management.md)
- lifecycle-heavy work or “hook-like” logic: [Hook Guidelines](./hook-guidelines.md)
- new reusable component APIs: [Type Safety](./type-safety.md)
- polish/review pass: [Quality Guidelines](./quality-guidelines.md)

For changes that also touch persistence, runtime services, or module contracts, read [Cross-Layer Thinking Guide](../guides/cross-layer-thinking-guide.md).

---

## Collaboration Language

- Use Chinese for agent-user conversation in this project.
- Keep frontend spec documentation and guideline updates written in English.

---

## Core Frontend Patterns

- Build UI with Blazor components, not a separate JS SPA.
- Keep shared UI state in injected services.
- Use CSS isolation first.
- Keep JS interop small and DOM-focused.
- Use `IStringLocalizer<SharedResources>` for new reusable text where possible.

Representative files:

- `src/OpenAnima.Core/Components/Pages/Editor.razor`
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`
- `src/OpenAnima.Core/Components/Shared/ProviderDialog.razor`
- `src/OpenAnima.Core/Services/EditorStateService.cs`

---

**Language**: Use Chinese for collaboration, but keep frontend documentation in English.
