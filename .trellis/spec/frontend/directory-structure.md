# Directory Structure

> How frontend code is organized in this project.

---

## Overview

The frontend is a Blazor Server UI hosted inside `OpenAnima.Core`, not a separate SPA. UI code lives next to application services and runtime logic, so component code often talks directly to injected services from the same assembly.

Primary frontend folders:

- `src/OpenAnima.Core/Components/Pages/`: routeable pages
- `src/OpenAnima.Core/Components/Shared/`: reusable UI building blocks
- `src/OpenAnima.Core/Components/Layout/`: app shell and layout components
- `src/OpenAnima.Core/Resources/`: localization resources
- `src/OpenAnima.Core/wwwroot/css` and `src/OpenAnima.Core/wwwroot/js`: global assets and JS interop helpers

---

## Directory Layout

```text
src/OpenAnima.Core/
├── Components/
│   ├── Layout/          # Main layout and shell
│   ├── Pages/           # Routeable pages such as Editor, Monitor, Settings
│   ├── Shared/          # Reusable components and dialogs
│   ├── App.razor
│   ├── Routes.razor
│   └── _Imports.razor
├── Resources/           # SharedResources.*.resx localization files
├── wwwroot/css/         # Global CSS
└── wwwroot/js/          # JS interop helpers
```

Component-adjacent styling usually lives in `ComponentName.razor.css`. Code-behind files exist, but they are rare and only used when a component really benefits from a separate partial class.

---

## Module Organization

- Put pages under `Components/Pages/`.
- Put reusable or embedded UI under `Components/Shared/`.
- Keep component-specific styles in a sibling `.razor.css` file.
- Use `wwwroot/js` only for DOM concerns that Blazor cannot express cleanly, such as textarea helpers or canvas interop.
- Put durable or shared UI state in injected services such as `EditorStateService`, `ChatSessionState`, and `LanguageService`, not inside ad hoc static component fields.

Representative examples:

- `src/OpenAnima.Core/Components/Pages/Editor.razor`: routeable page composed from shared components
- `src/OpenAnima.Core/Components/Shared/NodeCard.razor`: reusable visual editor component with local parameters
- `src/OpenAnima.Core/Components/Shared/ChatInput.razor`: small component plus targeted JS interop

---

## Naming Conventions

- Use `PascalCase` for `.razor`, `.razor.css`, and `.razor.cs` files.
- Page components use noun-based names like `Editor`, `Monitor`, `Settings`, `Runs`.
- Shared UI components use descriptive names like `ProviderDialog`, `NodeCard`, `TimelineFilterBar`.
- Keep style file names aligned exactly with the component name: `ChatPanel.razor` and `ChatPanel.razor.css`.

Avoid introducing frontend-only folders that duplicate existing concepts, such as a new `widgets/` directory when `Shared/` already covers the use case.

---

## Examples

- `src/OpenAnima.Core/Components/Pages/Monitor.razor` and `Monitor.razor.cs`: page plus code-behind split when the logic is substantial
- `src/OpenAnima.Core/Components/Shared/ProviderDialog.razor` and `ProviderDialog.razor.css`: reusable modal component with isolated styling
- `src/OpenAnima.Core/Components/Layout/MainLayout.razor`: layout-level UI belongs in `Layout/`

---

## Common Mistakes

- Placing routeable pages in `Shared/`
- Adding global CSS for a component that already has an isolated `.razor.css`
- Moving component state into JS when an injected Blazor service should own it
- Creating a partial class for trivial logic that is clearer in the component’s `@code` block
