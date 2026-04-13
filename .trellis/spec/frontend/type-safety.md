# Type Safety

> Type safety patterns in this project.

---

## Overview

Frontend type safety here means C# and Blazor type safety, not TypeScript. The project uses:

- `Nullable` enabled
- strongly typed component parameters and `EventCallback<T>`
- records for immutable DTOs and payloads
- explicit parsing and guard clauses at configuration boundaries

Representative examples:

- `src/OpenAnima.Core/OpenAnima.Core.csproj`
- `src/OpenAnima.Core/Components/Shared/NodeCard.razor`
- `src/OpenAnima.Core/Providers/ProviderModelRecord.cs`

---

## Type Organization

- Put cross-assembly types in `OpenAnima.Contracts`
- Put feature-local records next to the owning feature in `OpenAnima.Core`
- Keep component result types or UI-only records near the component when they are not broadly reused

Representative examples:

- `src/OpenAnima.Contracts/Ports/PortMetadata.cs`: shared contract type
- `src/OpenAnima.Core/Components/Shared/ProviderDialogResult.cs`: UI-local result record
- `src/OpenAnima.Core/ViewportPersistence/ViewportState.cs`: feature-local persisted record

---

## Validation

Validation is mostly explicit rather than attribute-heavy.

- use guard clauses and `string.IsNullOrWhiteSpace`
- use `TryGetValue`, `TryParse`, and null checks at config boundaries
- use typed JS interop calls such as `InvokeAsync<bool>` or `InvokeAsync<string>`

Representative examples:

- `src/OpenAnima.Core/Components/Shared/ProviderDialog.razor`: explicit input validation
- `src/OpenAnima.Core/Components/Shared/ChatInput.razor`: typed JS interop results
- `src/OpenAnima.Core/Modules/LLMModule.cs`: config extraction and parsing through explicit checks

---

## Common Patterns

- `record` and `record struct` style immutable payloads
- `EventCallback<T>` for parent-child interaction
- `[Parameter, EditorRequired]` for required component inputs
- typed service APIs instead of `object`-based blobs

Representative examples:

- `src/OpenAnima.Core/Components/Shared/NodeCard.razor`
- `src/OpenAnima.Core/Components/Shared/ProviderCard.razor`
- `src/OpenAnima.Core/Events/ChatEvents.cs`

---

## Forbidden Patterns

- New `object` or weakly typed payloads where a record is easy to define
- Using `null!` broadly when the component contract can instead be made explicit with `[EditorRequired]` or nullable types
- Unchecked `JSRuntime` calls that return untyped values
- Duplicating shared contract shapes in multiple assemblies
