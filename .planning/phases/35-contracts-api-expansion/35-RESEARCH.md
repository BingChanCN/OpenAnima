# Phase 35: Contracts API Expansion - Research

**Researched:** 2026-03-15
**Domain:** .NET interface migration, C# type-forward shims, assembly isolation, plugin SDK design
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**IModuleConfigSchema Design (API-04)**
- Define interface + supporting types in Contracts only. No auto-rendering implementation (deferred to AUTOUI-01 in v1.8+)
- Interface is optional — modules may implement `IModuleConfigSchema` to declare config fields, but are not required to
- `ConfigFieldType` enum (8 types): String, Int, Bool, Enum, Secret, MultilineText, Dropdown, Number
- `ConfigFieldDescriptor` record with full metadata (Key, Type, DisplayName, DefaultValue, Description, EnumValues, Group, Order, Required, ValidationPattern)
- Built-in modules do NOT adopt `IModuleConfigSchema` in Phase 35

**Feature Parity Definition (API-07)**
- Included: IModuleConfig, IModuleContext, ICrossAnimaRouter, IModuleConfigSchema
- Excluded: ILLMService, IHeartbeatService, IWiringEngine, IModuleService

**Interface Naming & Organization**
- `IAnimaModuleConfigService` → `IModuleConfig` (OpenAnima.Contracts root namespace)
- `IAnimaContext` → `IModuleContext` (OpenAnima.Contracts root namespace)
- `ICrossAnimaRouter` keeps name, moves to `OpenAnima.Contracts.Routing`
- `[Obsolete] IAnimaModuleConfigService` shim stays in Core for binary compat
- `[Obsolete] IAnimaContext` shim stays in Core for binary compat

**IModuleConfig Method Signature (simplified)**
```csharp
interface IModuleConfig
{
    Dictionary<string, string> GetConfig(string animaId, string moduleId);
    Task SetConfigAsync(string animaId, string moduleId, string key, string value);
}
```
Note: `SetConfigAsync` changes from `Dictionary<string, string> config` to `string key, string value` — single key-value pair.

**IModuleContext Design (simplified)**
```csharp
interface IModuleContext
{
    string ActiveAnimaId { get; }
    event Action? ActiveAnimaChanged;
}
```
Note: `ActiveAnimaId` changes from `string?` (nullable) to `string` (non-nullable). `SetActive()` is platform-internal, not on this interface.

**ICrossAnimaRouter Migration**
- Interface moves to `OpenAnima.Contracts.Routing` with full method surface
- 4 companion types move to same namespace: `PortRegistration`, `RouteResult`, `RouteRegistrationResult`, `RouteErrorKind`
- Type-forward shim in `OpenAnima.Core.Routing` for binary compatibility

**Canary Test (API-06)**
- Use existing PortModule as canary — no new module created
- Round-trip: build PortModule against new Contracts, pack as .oamod, load in runtime, verify it works
- PortModule enhanced to also test `IModuleConfig` and `IModuleContext` access

**Binary Compatibility (API-05)**
- Type-forward aliases in old Core namespaces ship in same commit as interface moves
- Old `using OpenAnima.Core.Services` / `using OpenAnima.Core.Anima` continue resolving via shims
- Any .oamod compiled against old Core namespaces must still load without recompilation

**Contracts Isolation**
- `OpenAnima.Contracts.csproj` must remain zero-dependency — no ProjectReference to Core, no PackageReferences
- `dotnet build` on Contracts project alone must succeed

### Claude's Discretion
- Internal implementation details of type-forward shim mechanism (TypeForwardedTo attribute vs interface inheritance vs using alias)
- Whether to move `ModuleMetadataRecord` to Contracts in Phase 35 (DECPL-05 is Phase 36 req, but may be prerequisite)
- ICrossAnimaRouter method signature cleanup (if any methods are platform-internal)
- Test structure and organization for canary round-trip test
- Order of interface migrations within the phase plans

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| API-01 | IModuleConfig interface (config read/write) exists in OpenAnima.Contracts | New interface defined in Contracts; AnimaModuleConfigService implements it; shim keeps old name in Core |
| API-02 | IAnimaContext (or IModuleContext with immutable AnimaId) exists in OpenAnima.Contracts | IModuleContext defined in Contracts; AnimaContext implements both; shim keeps IAnimaContext in Core |
| API-03 | ICrossAnimaRouter interface exists in OpenAnima.Contracts with type-forward shim in Core | Interface + 4 companion types moved to Contracts.Routing; Core namespace gets shim |
| API-04 | IModuleConfigSchema interface in Contracts — modules declare config fields, platform auto-renders sidebar | Pure contract definition only; implementation (auto-render) deferred to v1.8 |
| API-05 | Binary compatibility maintained — type-forward aliases in old Core namespaces for moved interfaces | C# type alias / interface inheritance shim pattern; DI must register both old and new names |
| API-06 | Canary .oamod round-trip test validates external plugin compatibility after interface moves | PortModule used as canary; compile against new Contracts; load via PluginLoadContext; verify DI injection |
| API-07 | External modules achieve feature parity with built-in modules via Contracts-only dependency | All 4 capability surfaces (config, context, routing, schema) in Contracts; PortModule demonstrates parity |
</phase_requirements>

---

## Summary

Phase 35 promotes four module-facing service interfaces from `OpenAnima.Core` to `OpenAnima.Contracts`, enabling external module authors to depend only on the zero-dependency Contracts assembly. The work is pure interface refactoring: no new runtime behavior, no new persistence, no UI changes.

The main technical challenge is the two-sided migration: interfaces must be added to Contracts (with slightly cleaned-up signatures) AND compatible shims must remain in Core so that existing built-in modules and tests continue compiling without changes. The `IAnimaContext` / `IModuleContext` split is the trickiest because Blazor components inject `IAnimaContext` by name via `@inject` and also call `SetActive()` which is absent from `IModuleContext`.

The `SetConfigAsync` signature changes from bulk (`Dictionary<string, string>`) to per-key (`string key, string value`) in the new `IModuleConfig`. Built-in modules and tests that call the old bulk form still compile against `IAnimaModuleConfigService` (the shim), so no call-site rewrite is required in Phase 35. Phase 36 (DECPL-01) is where modules actually switch their `using` statements.

**Primary recommendation:** Execute in dependency order — Routing companion types first (no DI complications), then IModuleConfig, then IModuleContext, then IModuleConfigSchema (new, no migration needed), then canary test.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET / C# | net8.0 | Target framework (Contracts + Core) | Project standard; Contracts must stay net8.0 to match Core |
| xUnit | 2.9.3 | Test framework | Existing project standard; 266 tests green |
| Microsoft.Extensions.Logging.Abstractions | 10.0.3 | Null loggers in tests | Already in test project |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| dotnet CLI | (runtime) | Building PortModule for canary test | ModuleTestHarness already uses this approach |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Interface inheritance shim | `[assembly: TypeForwardedTo(typeof(T))]` | TypeForwardedTo is for moved types (same interface identity); interface inheritance preserves old name as distinct type — use inheritance/alias approach since the interface is being *renamed*, not moved to a new assembly |
| `using` alias (file-scoped) | Interface inheritance | `using IAnimaContext = IModuleContext` is only file-scoped in C# 12, not assembly-wide; inheritance is cleaner |

---

## Architecture Patterns

### Recommended Project Structure After Phase 35

```
src/OpenAnima.Contracts/
├── IEventBus.cs             # (existing)
├── IModule.cs               # (existing)
├── IModuleConfig.cs         # NEW — was IAnimaModuleConfigService in Core
├── IModuleContext.cs         # NEW — was IAnimaContext in Core
├── IModuleConfigSchema.cs   # NEW — brand new interface
├── ConfigFieldDescriptor.cs # NEW — supporting record
├── ConfigFieldType.cs       # NEW — supporting enum
├── StatelessModuleAttribute.cs # (existing)
├── Ports/                   # (existing sub-namespace)
└── Routing/                 # NEW sub-namespace
    ├── ICrossAnimaRouter.cs  # MOVED from Core.Routing
    ├── PortRegistration.cs   # MOVED from Core.Routing
    ├── RouteResult.cs        # MOVED from Core.Routing (contains RouteErrorKind enum)
    └── RouteRegistrationResult.cs # MOVED from Core.Routing

src/OpenAnima.Core/
├── Routing/
│   ├── CrossAnimaRouter.cs  # (unchanged implementation, updates using)
│   ├── ICrossAnimaRouter.cs # REPLACED with shim: [Obsolete] interface ICrossAnimaRouter : Contracts.Routing.ICrossAnimaRouter {}
│   ├── PortRegistration.cs  # REPLACED with shim: [Obsolete] using alias or empty re-export
│   ├── RouteResult.cs       # REPLACED with shim
│   ├── RouteRegistrationResult.cs # REPLACED with shim
│   └── PendingRequest.cs    # (unchanged — internal type, not migrated)
├── Services/
│   ├── IAnimaModuleConfigService.cs # REPLACED with shim: [Obsolete] interface extending IModuleConfig
│   └── AnimaModuleConfigService.cs  # Implements both IModuleConfig (new) + IAnimaModuleConfigService (shim)
└── Anima/
    ├── IAnimaContext.cs      # REPLACED with shim: [Obsolete] interface extending IModuleContext + SetActive
    └── AnimaContext.cs       # Implements both IModuleContext (Contracts) and full IAnimaContext (Core shim)
```

### Pattern 1: Interface Rename Shim (Core keeps old name)

**What:** New interface defined in Contracts with cleaned-up API; old Core interface becomes a subtype extending the new one, adding Core-only members (SetActive, InitializeAsync).

**When to use:** When renaming an interface across assemblies while maintaining binary compatibility for existing consumers that depend on the old name.

**Example (IAnimaContext → IModuleContext):**
```csharp
// src/OpenAnima.Contracts/IModuleContext.cs (NEW)
namespace OpenAnima.Contracts;

public interface IModuleContext
{
    string ActiveAnimaId { get; }  // non-nullable — module always has active Anima
    event Action? ActiveAnimaChanged;
}
```

```csharp
// src/OpenAnima.Core/Anima/IAnimaContext.cs (SHIM — replaces old content)
using OpenAnima.Contracts;

namespace OpenAnima.Core.Anima;

/// <summary>
/// Obsolete: Use IModuleContext from OpenAnima.Contracts instead.
/// </summary>
[Obsolete("Use OpenAnima.Contracts.IModuleContext. This alias will be removed in v2.0.")]
public interface IAnimaContext : IModuleContext
{
    // Platform-internal operation — not on IModuleContext
    void SetActive(string animaId);
}
```

```csharp
// src/OpenAnima.Core/Anima/AnimaContext.cs (UPDATED — implements both)
using OpenAnima.Contracts;

namespace OpenAnima.Core.Anima;

public class AnimaContext : IAnimaContext  // IAnimaContext extends IModuleContext, so both satisfied
{
    private string _activeAnimaId = "";  // Non-nullable to match IModuleContext

    public event Action? ActiveAnimaChanged;
    public string ActiveAnimaId => _activeAnimaId;

    public void SetActive(string animaId)
    {
        if (_activeAnimaId == animaId) return;
        _activeAnimaId = animaId;
        ActiveAnimaChanged?.Invoke();
    }
}
```

**DI registration update needed:**
```csharp
// AnimaServiceExtensions.cs — register BOTH IModuleContext AND IAnimaContext
services.AddSingleton<AnimaContext>();  // concrete singleton
services.AddSingleton<IModuleContext>(sp => sp.GetRequiredService<AnimaContext>());
services.AddSingleton<IAnimaContext>(sp => sp.GetRequiredService<AnimaContext>());
```

### Pattern 2: Interface Rename Shim (IAnimaModuleConfigService → IModuleConfig)

**What:** New `IModuleConfig` in Contracts has simplified `SetConfigAsync(animaId, moduleId, key, value)` (per-key). Old `IAnimaModuleConfigService` shim in Core extends `IModuleConfig` and adds the bulk `SetConfigAsync(animaId, moduleId, Dictionary<string,string>)` overload plus `InitializeAsync()`.

**Why the per-key approach works:** The new interface is for external modules setting one key at a time (simpler). Built-in modules that set multiple keys at once continue using `IAnimaModuleConfigService.SetConfigAsync(Dictionary<string,string>)` from the shim interface.

```csharp
// src/OpenAnima.Contracts/IModuleConfig.cs (NEW)
namespace OpenAnima.Contracts;

public interface IModuleConfig
{
    Dictionary<string, string> GetConfig(string animaId, string moduleId);
    Task SetConfigAsync(string animaId, string moduleId, string key, string value);
}
```

```csharp
// src/OpenAnima.Core/Services/IAnimaModuleConfigService.cs (SHIM)
using OpenAnima.Contracts;

namespace OpenAnima.Core.Services;

[Obsolete("Use OpenAnima.Contracts.IModuleConfig. This alias will be removed in v2.0.")]
public interface IAnimaModuleConfigService : IModuleConfig
{
    // Bulk overload — used by built-in modules
    Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config);
    // Platform-internal init — not exposed to external modules
    Task InitializeAsync();
}
```

```csharp
// src/OpenAnima.Core/Services/AnimaModuleConfigService.cs (UPDATED)
// Must implement both: IModuleConfig.SetConfigAsync(key, value) and IAnimaModuleConfigService.SetConfigAsync(dict)
public class AnimaModuleConfigService : IAnimaModuleConfigService
{
    // New per-key implementation for IModuleConfig
    public async Task SetConfigAsync(string animaId, string moduleId, string key, string value)
    {
        var current = GetConfig(animaId, moduleId);
        current[key] = value;
        await SetConfigAsync(animaId, moduleId, current);  // delegate to bulk
    }

    // Existing bulk implementation retained for IAnimaModuleConfigService
    public async Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config)
    { /* existing implementation */ }
    // ...
}
```

### Pattern 3: Type File Move (Routing Companion Types)

**What:** `PortRegistration`, `RouteResult`, `RouteRegistrationResult`, and `RouteErrorKind` move to `OpenAnima.Contracts.Routing`. Old files in `OpenAnima.Core.Routing` become one-line shims using `global using` or empty namespace re-exports.

**Shim options (Claude's discretion):**
- **Option A (recommended) — empty subclass shim:** Deprecated type in Core namespace inherits from Contracts type. Works for classes/records. Problematic for enums.
- **Option B — `global using` alias:** `global using PortRegistration = OpenAnima.Contracts.Routing.PortRegistration;` in Core project's GlobalUsings.cs. Zero overhead, but is file-scoped to Core assembly only.
- **Option C — `[assembly: TypeForwardedTo]`:** Works only when the type physically moves assemblies (Core → Contracts). Here types move FROM Core INTO Contracts, so TypeForwardedTo is not applicable (it's for the source assembly to forward to destination).

**Correct mechanism:** Because types are moving from Core to Contracts (a dependency of Core), `[assembly: TypeForwardedTo]` cannot be used in Core to forward to Contracts types at the IL level. Use **Option B: project-level global using aliases** in Core's csproj or a dedicated `Shims.cs` file. This keeps Core code compiling with old type names.

```csharp
// src/OpenAnima.Core/Routing/Shims.cs (NEW — keeps old names working in Core)
// These aliases make old-namespace types resolve to Contracts types within Core assembly.
// External consumers that reference Core.Routing types directly need to update their using.
global using PortRegistration = OpenAnima.Contracts.Routing.PortRegistration;
global using RouteResult = OpenAnima.Contracts.Routing.RouteResult;
global using RouteRegistrationResult = OpenAnima.Contracts.Routing.RouteRegistrationResult;
global using RouteErrorKind = OpenAnima.Contracts.Routing.RouteErrorKind;
```

**IMPORTANT:** `global using` aliases are C# file-scoped syntax. They make the name available within the Core assembly's compilation, but external assemblies importing `OpenAnima.Core.Routing` directly still see a missing type. Test files that `using OpenAnima.Core.Routing` and reference `PortRegistration` directly will need to either keep their old `using` (if Core re-exports) or update to `using OpenAnima.Contracts.Routing`.

### Pattern 4: IModuleConfigSchema (New Interface — No Migration)

**What:** Brand-new interface with no existing equivalent. Pure addition to Contracts.

```csharp
// src/OpenAnima.Contracts/IModuleConfigSchema.cs (NEW)
namespace OpenAnima.Contracts;

public interface IModuleConfigSchema
{
    IReadOnlyList<ConfigFieldDescriptor> GetSchema();
}
```

```csharp
// src/OpenAnima.Contracts/ConfigFieldType.cs (NEW)
namespace OpenAnima.Contracts;

public enum ConfigFieldType
{
    String,
    Int,
    Bool,
    Enum,
    Secret,
    MultilineText,
    Dropdown,
    Number
}
```

```csharp
// src/OpenAnima.Contracts/ConfigFieldDescriptor.cs (NEW)
namespace OpenAnima.Contracts;

public record ConfigFieldDescriptor(
    string Key,
    ConfigFieldType Type,
    string DisplayName,
    string? DefaultValue,
    string? Description,
    string[]? EnumValues,
    string? Group,
    int Order,
    bool Required,
    string? ValidationPattern
);
```

### Pattern 5: Canary Test Structure

**What:** PortModule is compiled against the new Contracts, packed as .oamod, loaded via `PluginLoadContext`, and verified to receive injected `IModuleConfig`, `IModuleContext`, and `ICrossAnimaRouter` instances.

**Key constraint from STATE.md:** Delete `OpenAnima.Contracts.dll` from plugin output dir after build so `AssemblyDependencyResolver` falls back to Default context's shared assembly copy (avoids type identity mismatch).

### Anti-Patterns to Avoid

- **Adding `ProjectReference` to Core from Contracts:** Contracts must stay zero-dependency. If any new interface needs Core types, rethink the interface design.
- **Changing `IModuleContext.ActiveAnimaId` to `string?`:** The CONTEXT.md specifies non-nullable. AnimaContext must ensure it's always initialized (default `""`) before any module accesses it.
- **Registering only `IModuleContext` in DI and dropping `IAnimaContext`:** Blazor components use `@inject IAnimaContext` — both registrations must coexist.
- **Forgetting the `NullAnimaModuleConfigService` test helper:** It implements `IAnimaModuleConfigService` with the old `Dictionary` signature. Must be updated to also implement the new `IModuleConfig.SetConfigAsync(key, value)` overload.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Forwarding old type names | Custom proxy classes | `global using` alias or interface inheritance | C# provides both mechanisms natively; proxies have overhead and type identity problems |
| PortModule compilation for canary | New Roslyn-based compilation | dotnet CLI subprocess (ModuleTestHarness pattern) | Already proven approach in tests; STATE.md notes it's fragile but acceptable |
| Plugin loading | Custom AssemblyLoadContext | Existing `PluginLoadContext` infrastructure | Already handles the Contracts deduplication problem (delete local copy pattern) |
| DI multiple registrations | Factory delegation | `sp => sp.GetRequiredService<AnimaContext>()` lambda | Clean; avoids double-instantiation; standard .NET DI pattern |

---

## Common Pitfalls

### Pitfall 1: IAnimaContext Injection in Blazor Components
**What goes wrong:** After adding `IModuleContext` and keeping `IAnimaContext` as a shim, if DI is not updated to register BOTH, Blazor's `@inject IAnimaContext` fails at runtime with "No service of type IAnimaContext registered."
**Why it happens:** `AnimaContext` now implements `IAnimaContext` which extends `IModuleContext`. Without explicit DI registration for `IAnimaContext`, the DI container only knows about `IModuleContext`.
**How to avoid:** Register three entries in `AnimaServiceExtensions.AddAnimaServices()`:
  1. `AddSingleton<AnimaContext>()` (concrete)
  2. `AddSingleton<IModuleContext>(sp => sp.GetRequiredService<AnimaContext>())`
  3. `AddSingleton<IAnimaContext>(sp => sp.GetRequiredService<AnimaContext>())`
**Warning signs:** Runtime startup crash with DI resolution failure for `IAnimaContext`.

### Pitfall 2: SetConfigAsync Signature Ambiguity
**What goes wrong:** `AnimaModuleConfigService` must implement `IModuleConfig.SetConfigAsync(animaId, moduleId, key, value)` AND `IAnimaModuleConfigService.SetConfigAsync(animaId, moduleId, Dictionary<string,string>)`. These are distinct overloads. The compiler will resolve correctly, but tests that cast to `IAnimaModuleConfigService` and call the Dict overload must still compile.
**Why it happens:** Interface inheritance means both overloads are visible. C# overload resolution works by parameter count/type, so no ambiguity.
**How to avoid:** Implement both explicitly. Delegate per-key to bulk, or vice versa.
**Warning signs:** Compilation error "does not implement interface member SetConfigAsync(string, string, string, string)".

### Pitfall 3: NullAnimaModuleConfigService Breaks After Shim
**What goes wrong:** `NullAnimaModuleConfigService` in tests implements `IAnimaModuleConfigService` which now extends `IModuleConfig`. If `IModuleConfig` adds the per-key `SetConfigAsync` method, the test helper is missing an implementation.
**Why it happens:** Adding a method to the base interface creates a new abstract requirement.
**How to avoid:** Update `NullAnimaModuleConfigService` in the same commit that adds the per-key overload to `IModuleConfig`.
**Warning signs:** Compilation error "'NullAnimaModuleConfigService' does not implement interface member 'IModuleConfig.SetConfigAsync(string, string, string, string)'".

### Pitfall 4: Routing Test Files Reference Core.Routing Types Directly
**What goes wrong:** Test files with `using OpenAnima.Core.Routing` and references to `PortRegistration`, `RouteResult`, etc. break after those types move to Contracts.
**Why it happens:** `global using` aliases inside Core do not affect test assembly compilation.
**How to avoid:** After moving types to Contracts, update test files' `using` directives from `OpenAnima.Core.Routing` to `OpenAnima.Contracts.Routing`. This affects: `CrossAnimaRouterIntegrationTests.cs`, `CrossAnimaRoutingE2ETests.cs`, `RoutingModulesTests.cs`, `RoutingTypesTests.cs`, `CrossAnimaRouterTests.cs`.
**Warning signs:** Compilation error "type or namespace 'PortRegistration' could not be found" in test assembly.

### Pitfall 5: IModuleContext.ActiveAnimaId Nullable Contract
**What goes wrong:** Existing `IAnimaContext.ActiveAnimaId` is `string?`. The new `IModuleContext.ActiveAnimaId` is `string` (non-nullable). The `AnimaContext` concrete class must return a non-null value. If any code path calls `GetConfig()` before `SetActive()` is called, `ActiveAnimaId` is `""` (empty string) not null — callers checking `animaId != null` must switch to checking `animaId != ""`.
**Why it happens:** Semantic change in the interface contract.
**How to avoid:** Initialize `_activeAnimaId = ""` in `AnimaContext`. Built-in modules guard with `if (string.IsNullOrEmpty(animaId))` not `if (animaId == null)`. These modules are not changing in Phase 35 (they still use `IAnimaContext` shim which has `string?`) — this pitfall applies only to external modules using `IModuleContext` directly.
**Warning signs:** NullReferenceException from modules that previously guarded on null but now get empty string.

### Pitfall 6: Stub Configs in Tests Implement Wrong Interface
**What goes wrong:** Tests like `CrossAnimaRoutingE2ETests` have private `StubConfig` classes that implement `IAnimaModuleConfigService`. After shim is in place, these stubs must implement all members of both `IModuleConfig` and `IAnimaModuleConfigService`.
**Why it happens:** Interface inheritance requires all base interface members to be implemented.
**How to avoid:** Add the per-key `SetConfigAsync(animaId, moduleId, key, value)` stub method to each private test stub in the same commit. Files affected: `CrossAnimaRoutingE2ETests.cs`, `PromptInjectionIntegrationTests.cs`, `RoutingModulesTests.cs`, `HttpRequestModuleTests.cs`.
**Warning signs:** Test compilation failure.

### Pitfall 7: Contracts Isolation Broken by Accident
**What goes wrong:** Adding a type to Contracts that depends on a type only in Core (e.g., accidentally referencing `ILogger` from Microsoft.Extensions.Logging which IS in Core's NuGet packages but not in Contracts).
**Why it happens:** Contracts has no PackageReferences. `ILogger` and similar types are not available.
**How to avoid:** Run `dotnet build src/OpenAnima.Contracts/OpenAnima.Contracts.csproj` in isolation as part of each plan's verification. Keep all new Contracts types to plain C# primitives, generics, and collections.
**Warning signs:** Build failure on isolated Contracts build.

---

## Code Examples

### IModuleConfig in Contracts
```csharp
// Source: designed per CONTEXT.md decisions
// src/OpenAnima.Contracts/IModuleConfig.cs
namespace OpenAnima.Contracts;

/// <summary>
/// Module-facing configuration service. Read and write per-Anima module configuration.
/// </summary>
public interface IModuleConfig
{
    /// <summary>Returns all config values for the given Anima and module. Empty dict if none.</summary>
    Dictionary<string, string> GetConfig(string animaId, string moduleId);

    /// <summary>Saves a single configuration key-value pair. Persists to disk.</summary>
    Task SetConfigAsync(string animaId, string moduleId, string key, string value);
}
```

### IModuleContext in Contracts
```csharp
// src/OpenAnima.Contracts/IModuleContext.cs
namespace OpenAnima.Contracts;

/// <summary>
/// Read-only view of the active Anima identity. Injected by the platform into modules.
/// </summary>
public interface IModuleContext
{
    /// <summary>The ID of the currently active Anima. Never null or empty after initialization.</summary>
    string ActiveAnimaId { get; }

    /// <summary>Fires when the active Anima changes.</summary>
    event Action? ActiveAnimaChanged;
}
```

### DI Registration Update (AnimaServiceExtensions.cs)
```csharp
// Both old (IAnimaContext for Blazor components) and new (IModuleContext for modules) registered
services.AddSingleton<AnimaContext>();
services.AddSingleton<IModuleContext>(sp => sp.GetRequiredService<AnimaContext>());
services.AddSingleton<IAnimaContext>(sp => sp.GetRequiredService<AnimaContext>());
// IModuleConfig (new) + IAnimaModuleConfigService (shim) both resolve to same singleton
services.AddSingleton<AnimaModuleConfigService>(sp => new AnimaModuleConfigService(animasRoot));
services.AddSingleton<IModuleConfig>(sp => sp.GetRequiredService<AnimaModuleConfigService>());
services.AddSingleton<IAnimaModuleConfigService>(sp => sp.GetRequiredService<AnimaModuleConfigService>());
// ICrossAnimaRouter already in Contracts.Routing — no change needed to existing DI registration
```

### Contracts Isolation Verification Command
```bash
dotnet build src/OpenAnima.Contracts/OpenAnima.Contracts.csproj
```

### Full Test Suite Command
```bash
dotnet test tests/OpenAnima.Tests/ -q
```

### PortModule Canary Enhancement
```csharp
// PortModule.cs — enhanced for canary test
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Contracts.Routing;

namespace PortModule;

public class PortModule : IModule
{
    private readonly IModuleConfig? _config;
    private readonly IModuleContext? _context;
    private readonly ICrossAnimaRouter? _router;

    public PortModule(IModuleConfig? config = null, IModuleContext? context = null, ICrossAnimaRouter? router = null)
    {
        _config = config;
        _context = context;
        _router = router;
    }
    // ... existing Metadata, InitializeAsync, ShutdownAsync
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| All interfaces in Core | Module-facing interfaces in Contracts | Phase 35 (this work) | External modules can achieve feature parity without Core reference |
| `IAnimaModuleConfigService` (bulk SetConfigAsync) | `IModuleConfig` (per-key SetConfigAsync) | Phase 35 | Simpler API for external module authors; bulk overload stays on shim |
| `IAnimaContext.ActiveAnimaId` is `string?` | `IModuleContext.ActiveAnimaId` is `string` | Phase 35 | Removes null guard boilerplate for external modules |
| No config schema declaration | `IModuleConfigSchema` + `ConfigFieldDescriptor` | Phase 35 | Enables future auto-rendering (v1.8); modules can self-describe fields |

**Deprecated/outdated after Phase 35:**
- `IAnimaModuleConfigService` (Core.Services): deprecated, kept as shim. Phase 36 will remove usages in built-in modules.
- `IAnimaContext` (Core.Anima): deprecated, kept as shim. Phase 36 will remove usages in built-in modules (Blazor components may keep using it since they need `SetActive()`).

---

## Open Questions

1. **Where do Blazor components land long-term?**
   - What we know: Blazor components use `@inject IAnimaContext` and call `SetActive()`. `IModuleContext` is read-only. `IAnimaContext` shim keeps `SetActive()`.
   - What's unclear: Phase 36 (DECPL-01) says all 14 built-in modules go Contracts-only. Blazor components are part of Core, not modules — so they may legitimately keep `IAnimaContext` indefinitely.
   - Recommendation: Do not attempt to update Blazor components in Phase 35. Document that `IAnimaContext` shim is permanent for UI layer; `IModuleContext` is for modules only.

2. **Should ModuleMetadataRecord move to Contracts in Phase 35?**
   - What we know: CONTEXT.md flags this as Claude's discretion. DECPL-05 (Phase 36) requires it. PortModule canary does not currently use it directly (it defines its own `PortModuleMetadata`).
   - What's unclear: Whether any external module would need it before Phase 36.
   - Recommendation: Do NOT move `ModuleMetadataRecord` in Phase 35. PortModule demonstrates Contracts-only parity without it; DECPL-05 is explicitly Phase 36 scope.

3. **ICrossAnimaRouter: CompleteRequest and CancelPendingForAnima — are they platform-internal?**
   - What we know: `CompleteRequest` is called by `AnimaOutputPortModule` (a built-in module, currently Core). `CancelPendingForAnima` and `UnregisterAllForAnima` are called by `AnimaRuntimeManager` (Core-internal).
   - What's unclear: Phase 36 will decouple built-in modules — `AnimaOutputPortModule` may need `CompleteRequest` from Contracts.
   - Recommendation: Move the full method surface as-is (CONTEXT.md specifies "full method surface"). Phase 36 can refine if platform-internal methods need hiding.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none — conventional discovery |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ -q --filter "Category=Routing"` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/ -q` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| API-01 | IModuleConfig exists in Contracts, resolves from DI | unit | `dotnet test tests/OpenAnima.Tests/ -q --filter "FullyQualifiedName~IModuleConfig"` | ❌ Wave 0 |
| API-02 | IModuleContext exists in Contracts, resolves from DI | unit | `dotnet test tests/OpenAnima.Tests/ -q --filter "FullyQualifiedName~IModuleContext"` | ❌ Wave 0 |
| API-03 | ICrossAnimaRouter in Contracts.Routing, Core shim in place | unit/integration | `dotnet test tests/OpenAnima.Tests/ -q --filter "Category=Routing"` | ✅ (existing routing tests) |
| API-04 | IModuleConfigSchema + ConfigFieldDescriptor compile in Contracts isolation | unit | `dotnet build src/OpenAnima.Contracts/OpenAnima.Contracts.csproj` | ❌ Wave 0 |
| API-05 | Old Core namespace types still resolve after migration | compile | `dotnet build tests/OpenAnima.Tests/` | ✅ (all 266 tests must remain green) |
| API-06 | PortModule compiles against Contracts-only, loads successfully | integration | `dotnet test tests/OpenAnima.Tests/ -q --filter "FullyQualifiedName~Canary"` | ❌ Wave 0 |
| API-07 | External module uses IModuleConfig, IModuleContext, ICrossAnimaRouter from Contracts | integration | Part of canary test | ❌ Wave 0 (covered by API-06 test) |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ -q` (28 second baseline — all 266 must pass)
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/ -q` + `dotnet build src/OpenAnima.Contracts/OpenAnima.Contracts.csproj`
- **Phase gate:** Full suite green + Contracts isolation build green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/ContractsApiTests.cs` — covers API-01, API-02, API-04: verifies new types are in correct namespaces and Contracts isolation build passes
- [ ] `tests/OpenAnima.Tests/Integration/CanaryModuleTests.cs` — covers API-06, API-07: PortModule round-trip as canary
- [ ] Update `tests/OpenAnima.Tests/TestHelpers/NullAnimaModuleConfigService.cs` — add per-key `SetConfigAsync` implementation (required to satisfy `IModuleConfig` base interface)
- [ ] Update private stub configs in: `CrossAnimaRoutingE2ETests.cs`, `PromptInjectionIntegrationTests.cs`, `RoutingModulesTests.cs`, `HttpRequestModuleTests.cs` — add per-key `SetConfigAsync` method to each `StubConfig` class

---

## Sources

### Primary (HIGH confidence)
- Codebase direct read — `src/OpenAnima.Contracts/`, `src/OpenAnima.Core/`, `tests/OpenAnima.Tests/` (all interface definitions, implementation classes, DI wiring, test helpers read directly)
- `35-CONTEXT.md` — all locked design decisions
- `REQUIREMENTS.md` — requirement IDs and descriptions
- `STATE.md` — accumulated project decisions and known patterns

### Secondary (MEDIUM confidence)
- C# language specification for `global using` alias semantics and interface inheritance
- .NET DI container behavior for multiple registrations of same concrete type

### Tertiary (LOW confidence)
- None — all findings are verifiable from codebase or language spec

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all tooling already in use; no new dependencies
- Architecture: HIGH — all patterns verified against existing codebase; interface signatures read directly from source
- Pitfalls: HIGH — 6 of 7 pitfalls identified from direct code reading (concrete files, concrete line numbers); 1 from nullability analysis

**Research date:** 2026-03-15
**Valid until:** 2026-04-15 (stable domain — pure C# refactoring, no fast-moving ecosystem)
