# Phase 35: Contracts API Expansion - Context

**Gathered:** 2026-03-15
**Status:** Ready for research/planning

<domain>
## Phase Boundary

Promote essential module-facing interfaces (config, context, routing, config schema) to OpenAnima.Contracts so external module authors can achieve feature parity with built-in modules without referencing Core. Includes type-forward shims for binary compatibility and a canary .oamod round-trip test. No auto-rendering UI — schema interface is defined but not consumed by platform until v1.8.

</domain>

<decisions>
## Implementation Decisions

### IModuleConfigSchema Design (API-04)
- **Scope:** Define interface + supporting types in Contracts only. No auto-rendering implementation (deferred to AUTOUI-01 in v1.8+)
- **Interface is optional** — modules may implement `IModuleConfigSchema` to declare config fields, but are not required to. Non-implementing modules continue using hand-written Razor sidebar
- **ConfigFieldType enum (8 types):** String, Int, Bool, Enum, Secret, MultilineText, Dropdown, Number
- **ConfigFieldDescriptor record (complete metadata):**
  ```csharp
  record ConfigFieldDescriptor(
      string Key,
      ConfigFieldType Type,
      string DisplayName,
      string? DefaultValue,
      string? Description,
      string[]? EnumValues,  // for Enum and Dropdown types
      string? Group,          // logical grouping
      int Order,              // display order within group
      bool Required,
      string? ValidationPattern  // regex for validation
  );
  ```
- Built-in modules do NOT adopt `IModuleConfigSchema` in Phase 35 — they continue with existing Razor components. Adoption is a Phase 36 or v1.8 task.

### Feature Parity Definition (API-07)
- **Included capabilities:**
  1. Read/write sidebar configuration (`IModuleConfig`)
  2. Sense Anima context — know active Anima ID (`IModuleContext`)
  3. Participate in cross-Anima routing (`ICrossAnimaRouter`)
  4. Declare config fields via schema (`IModuleConfigSchema`)
- **Excluded capabilities (confirmed out of scope):**
  - LLM service invocation (`ILLMService`) — external modules bring their own SDK
  - Heartbeat control (`IHeartbeatService`) — modules participate via `ITickable`
  - Wiring engine control (`IWiringEngine`) — deeply Core-internal
  - Module management (`IModuleService`) — platform-internal

### Interface Naming & Organization
- **Renamed for external developer clarity:**
  | Old Name (Core) | New Name (Contracts) | Notes |
  |-----------------|---------------------|-------|
  | `IAnimaModuleConfigService` | `IModuleConfig` | Simplified |
  | `IAnimaContext` | `IModuleContext` | Module-facing subset |
  | `ICrossAnimaRouter` | `ICrossAnimaRouter` | Keep as-is |
  | (new) | `IModuleConfigSchema` | New interface |

- **Type-forward compatibility shims in Core:**
  - `[Obsolete] IAnimaModuleConfigService` → alias for `IModuleConfig`
  - `[Obsolete] IAnimaContext` → alias for `IModuleContext`
  - Both in old namespaces to avoid breaking existing code

- **Namespace placement:**
  - `IModuleConfig`, `IModuleContext`, `IModuleConfigSchema` → `OpenAnima.Contracts` (root namespace, alongside IEventBus, IModule etc.)
  - `ICrossAnimaRouter` + companion types → `OpenAnima.Contracts.Routing` (sub-namespace, like existing `Contracts.Ports`)
  - Companion types to move: `PortRegistration`, `RouteResult`, `RouteRegistrationResult`, `RouteErrorKind`

### IModuleConfig Method Signature (simplified from IAnimaModuleConfigService)
- **Module-facing API only** — no `InitializeAsync()` (platform-internal):
  ```csharp
  interface IModuleConfig
  {
      Dictionary<string, string> GetConfig(string animaId, string moduleId);
      Task SetConfigAsync(string animaId, string moduleId, string key, string value);
  }
  ```
- Core implementation class continues to implement `InitializeAsync()` internally — it just isn't part of the Contracts interface

### IModuleContext Design (simplified from IAnimaContext)
- **Read-only perspective** — no `SetActive()` (platform-internal operation):
  ```csharp
  interface IModuleContext
  {
      string ActiveAnimaId { get; }
      event Action? ActiveAnimaChanged;
  }
  ```
- Core's `AnimaContext` class implements both `IModuleContext` (Contracts) and retains `SetActive()` for internal use

### ICrossAnimaRouter Migration
- Interface moves to `OpenAnima.Contracts.Routing` with full method surface (RegisterPort, UnregisterPort, GetPortsForAnima, RouteRequestAsync, etc.)
- 4 companion types (`PortRegistration`, `RouteResult`, `RouteRegistrationResult`, `RouteErrorKind`) also move to same namespace
- Type-forward shim in `OpenAnima.Core.Routing` namespace for binary compatibility

### Canary Test (API-06)
- Use existing **PortModule** as canary — no new module created
- Round-trip test: build PortModule against new Contracts, pack as .oamod, load in runtime, verify it works
- PortModule enhanced to also test `IModuleConfig` and `IModuleContext` access (optional — Claude's discretion on test coverage)

### Binary Compatibility (API-05)
- Type-forward aliases in old Core namespaces shipped in same commit as interface moves
- Old `using OpenAnima.Core.Services` / `using OpenAnima.Core.Anima` continue resolving via `[TypeForwardedTo]` or interface aliasing
- Any .oamod compiled against old Core namespaces must still load without recompilation

### Contracts Isolation (Success Criteria #5)
- `OpenAnima.Contracts.csproj` must remain zero-dependency — no ProjectReference to Core, no PackageReferences
- `dotnet build` on Contracts project alone must succeed

### Claude's Discretion
- Internal implementation details of type-forward shim mechanism (TypeForwardedTo attribute vs interface inheritance vs using alias)
- Whether to move `ModuleMetadataRecord` to Contracts in Phase 35 (DECPL-05 is Phase 36 req, but may be prerequisite)
- ICrossAnimaRouter method signature cleanup (if any methods are platform-internal)
- Test structure and organization for canary round-trip test
- Order of interface migrations within the phase plans

</decisions>

<specifics>
## Specific Ideas

- Prior decision from STATE.md: "Interface moves to Contracts: type-forward aliases in old Core namespaces must ship in same commit — binary compat for .oamod packages"
- Prior decision from STATE.md: "Module migration order: simplest first" — applies to Phase 36, but Phase 35 should consider dependency order for interface moves
- Known blocker from STATE.md: "ILLMService move also requires ChatMessageInput move — v1.7 vs v1.8 scope decision needed" → Resolved: excluded from Phase 35
- ConfigFieldDescriptor uses 8 types (String, Int, Bool, Enum, Secret, MultilineText, Dropdown, Number) — designed for future auto-rendering in v1.8

</specifics>

<code_context>
## Existing Code Insights

### Migration Candidates (current locations)
- `IAnimaModuleConfigService` → `src/OpenAnima.Core/Services/IAnimaModuleConfigService.cs` (methods: GetConfig, SetConfigAsync, InitializeAsync)
- `AnimaModuleConfigService` → `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs` (implementation stays in Core)
- `IAnimaContext` → `src/OpenAnima.Core/Anima/IAnimaContext.cs` (properties: ActiveAnimaId, SetActive, ActiveAnimaChanged)
- `AnimaContext` → `src/OpenAnima.Core/Anima/AnimaContext.cs` (implementation stays in Core)
- `ICrossAnimaRouter` → `src/OpenAnima.Core/Routing/ICrossAnimaRouter.cs`
- `PortRegistration` → `src/OpenAnima.Core/Routing/PortRegistration.cs`
- `RouteResult` → `src/OpenAnima.Core/Routing/RouteResult.cs`
- `RouteRegistrationResult` → `src/OpenAnima.Core/Routing/RouteRegistrationResult.cs`
- `RouteErrorKind` → `src/OpenAnima.Core/Routing/RouteErrorKind.cs`

### Contracts Project (target)
- `src/OpenAnima.Contracts/OpenAnima.Contracts.csproj` — zero dependencies, 13 existing source files
- Existing namespace patterns: `OpenAnima.Contracts` (root) + `OpenAnima.Contracts.Ports` (sub-namespace)

### Consumer Impact
- 9 of 12 built-in modules use `IAnimaModuleConfigService` + `IAnimaContext`
- 4 of 12 built-in modules use `ICrossAnimaRouter`
- 2 external modules (PortModule, SampleModule) currently reference Contracts only — will gain new capabilities

### Test Infrastructure
- `NullAnimaModuleConfigService` → `tests/OpenAnima.Tests/TestHelpers/NullAnimaModuleConfigService.cs` (must be updated for new interface name)
- 256 tests currently green (Phase 34 P01 baseline)

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 35-contracts-api-expansion*
*Context gathered: 2026-03-15*
