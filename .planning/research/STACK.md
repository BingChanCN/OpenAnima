# Technology Stack

**Project:** OpenAnima v1.8 SDK Runtime Parity
**Researched:** 2026-03-16
**Confidence:** HIGH

## Executive Summary

v1.8 adds three capabilities to the existing platform: DI injection into isolated plugin modules, per-module persistent storage paths, and structured message list input for LLMModule. A fourth feature — the external ContextModule — is the SDK validation proof that the first three work together.

**Zero new NuGet packages required.** Every primitive needed (`IServiceProvider`, `ActivatorUtilities`, `Path.Combine`, `IReadOnlyList<T>`) is BCL or already in the project. The work is entirely in two categories:

1. **Type moves**: `ChatMessageInput` and `LLMResult` must move from `OpenAnima.Core.LLM` to `OpenAnima.Contracts` so external modules can reference them without taking a Core dependency.
2. **Behavioral changes**: `PluginLoader.LoadModule()` must accept an `IServiceProvider` and use `ActivatorUtilities.CreateInstance` instead of `Activator.CreateInstance`. `IModuleContext` gains a `DataDirectory` property.

The v1.7 zero-dependency principle holds for v1.8.

---

## Baseline: Validated v1.7 Stack (Unchanged)

| Package | Version | Status |
|---------|---------|--------|
| .NET 8.0 | runtime | unchanged |
| Blazor Server + SignalR | 8.0.x | unchanged |
| OpenAI SDK | 2.8.0 | unchanged |
| SharpToken | 2.0.4 | unchanged |
| Markdig + Markdown.ColorCode | 0.41.3 / 3.0.1 | unchanged |
| System.CommandLine | 2.0.0-beta4 | unchanged |
| Microsoft.Extensions.Http.Resilience | 8.7.0 | unchanged |

Existing architecture v1.8 builds on:
- `PluginLoadContext` (isCollectible: true) with `AssemblyDependencyResolver` — Contracts assembly falls through to Default context via null return in `Load()`
- `PluginLoader.LoadModule()` — currently uses `Activator.CreateInstance(moduleType)` with zero DI
- `IModuleContext` in Contracts — currently only `ActiveAnimaId` + `ActiveAnimaChanged` event
- `ChatMessageInput(string Role, string Content)` record — currently in `OpenAnima.Core.LLM`, unreachable by external modules
- `ILLMService` — currently in `OpenAnima.Core.LLM`, unreachable by external modules
- `AnimaServiceExtensions.AddAnimaServices()` — registers `IModuleContext`, `IModuleConfig`, `IEventBus`, `ICrossAnimaRouter` as singletons

---

## New Stack Elements for v1.8

### Feature 1: PluginLoader DI Injection

**Problem:** `PluginLoader.LoadModule()` calls `Activator.CreateInstance(moduleType)` which requires a parameterless constructor. External modules that declare constructor parameters (e.g., `IEventBus`, `IModuleContext`, `IModuleConfig`) fail to instantiate.

**Solution:** `ActivatorUtilities.CreateInstance(IServiceProvider, Type)` from `Microsoft.Extensions.DependencyInjection.Abstractions`.

| Primitive | Namespace | Already in Project | Purpose |
|-----------|-----------|-------------------|---------|
| `ActivatorUtilities.CreateInstance(IServiceProvider, Type)` | `Microsoft.Extensions.DependencyInjection` | YES — transitive via `Microsoft.Extensions.DependencyInjection` which is already referenced by the ASP.NET Core host | Resolves constructor parameters from DI container, falls back to default values for optional params. Handles the cross-context type identity problem because it resolves by the service type registered in the container, not by the loaded assembly's type. |
| `IServiceProvider` | `System.ComponentModel` | YES — BCL | Passed into `PluginLoader.LoadModule(string dir, IServiceProvider services)` as new parameter |

**Why `ActivatorUtilities` over manual reflection:**
`ActivatorUtilities.CreateInstance` handles constructor overload selection (picks the constructor with the most resolvable parameters), optional parameters, and throws a clear `InvalidOperationException` when a required service is missing. Manual reflection would require reimplementing this logic.

**Cross-context type identity note:** The `PluginLoadContext` already handles this correctly — `OpenAnima.Contracts` is NOT in the plugin's `.deps.json` (it's a peer reference), so `Load()` returns null for it, causing the Default context's Contracts assembly to be used. This means the `IEventBus` instance from DI and the `IEventBus` interface the plugin compiled against are the same type identity. No change needed to `PluginLoadContext`.

**Signature change:**
```csharp
// Before
public LoadResult LoadModule(string moduleDirectory)

// After
public LoadResult LoadModule(string moduleDirectory, IServiceProvider services)
```

`Activator.CreateInstance(moduleType)` → `ActivatorUtilities.CreateInstance(services, moduleType)`

---

### Feature 2: IModuleContext.DataDirectory

**Problem:** External modules have no standard path for persistent storage. Each module inventing its own path convention leads to collisions and non-portable modules.

**Solution:** Add `string DataDirectory { get; }` to `IModuleContext` in Contracts. The platform implementation (`AnimaContext`) computes the path as:

```
{dataRoot}/animas/{animaId}/modules/{moduleId}/
```

This mirrors the existing per-Anima config directory convention (`{dataRoot}/animas/{animaId}/`) already established in `AnimaServiceExtensions.AddAnimaServices()`.

| Primitive | Namespace | Already in Project | Purpose |
|-----------|-----------|-------------------|---------|
| `Path.Combine` | `System.IO` | YES — BCL | Constructs the per-Anima per-module path |
| `Directory.CreateDirectory` | `System.IO` | YES — BCL | Ensures directory exists on first access (lazy creation in getter or on first use) |

**Interface addition (Contracts — no breaking change):**
```csharp
public interface IModuleContext
{
    string ActiveAnimaId { get; }
    event Action? ActiveAnimaChanged;
    string DataDirectory { get; }  // NEW
}
```

**Implementation note:** `AnimaContext` currently implements `IModuleContext`. It already knows `ActiveAnimaId`. `DataDirectory` needs the module ID to construct the path. Two options:

- Option A: `DataDirectory` is computed from `ActiveAnimaId` + a module ID set via a platform-internal setter (same pattern as `ActiveAnimaId` mutation being platform-internal)
- Option B: `IModuleContext` is per-module (not per-Anima singleton) — each module gets its own `IModuleContext` instance with its module ID baked in

Option B is cleaner for external modules (they just call `context.DataDirectory` without knowing their own ID) but requires changing how `IModuleContext` is registered in DI. Option A keeps the singleton pattern but requires the platform to set the module ID before calling module methods.

**Recommendation: Option B** — a thin `ModuleContext` wrapper per module, created by `PluginLoader` after instantiation, injected via property setter (same pattern as `IEventBus` property injection used today). The `IModuleContext` singleton in DI remains for built-in modules; external modules get a dedicated instance.

---

### Feature 3: LLMModule Structured Message Input — Type Move

**Problem:** `ChatMessageInput(string Role, string Content)` is defined in `OpenAnima.Core.LLM`. External modules that want to pass a structured message list to LLMModule's input port cannot reference this type without taking a dependency on `OpenAnima.Core`.

**Solution:** Move `ChatMessageInput` and `LLMResult` records to `OpenAnima.Contracts`. Add a shim in `OpenAnima.Core.LLM` that inherits or aliases the Contracts type for backward compatibility during transition.

| Type | Move From | Move To | Why |
|------|-----------|---------|-----|
| `ChatMessageInput(string Role, string Content)` | `OpenAnima.Core.LLM` | `OpenAnima.Contracts` | External modules need to construct message lists. This is a pure data record with no Core dependencies — safe to move. |
| `LLMResult(bool Success, string? Content, string? Error)` | `OpenAnima.Core.LLM` | `OpenAnima.Contracts` | Companion to `ChatMessageInput`; same reasoning. |
| `ILLMService` | `OpenAnima.Core.LLM` | `OpenAnima.Contracts` | External modules that want to call the LLM service directly need this interface. Depends only on `ChatMessageInput` and `LLMResult` (both moving to Contracts). |

**Backward compatibility:** After the move, `OpenAnima.Core.LLM` keeps type aliases:
```csharp
// OpenAnima.Core.LLM — backward compat shims
global using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput;
global using LLMResult = OpenAnima.Contracts.LLMResult;
```

This avoids touching the 14 built-in modules that already use these types.

**LLMModule port change:** The `prompt` input port currently accepts `string`. For structured message input, it needs to accept `IReadOnlyList<ChatMessageInput>`. Two approaches:

- Add a second input port `messages` of type `Text` (serialized JSON) — avoids port type system changes but requires serialization
- Keep `prompt` as `string`, add `messages` as a new port accepting the structured type via a new `PortType.MessageList` — requires port type system extension

**Recommendation:** Add a `messages` input port that accepts `IReadOnlyList<ChatMessageInput>` serialized as JSON string (PortType.Text). LLMModule deserializes on receipt. This avoids port type system changes (deferred to future milestone) while enabling the ContextModule to pass structured history. `System.Text.Json` is already in the BCL.

---

### Feature 4: External ContextModule (SDK Validation)

The ContextModule is a proof-of-concept external module that validates features 1-3 work end-to-end. It requires no additional stack beyond what features 1-3 establish.

**What it needs from the SDK:**
- `IEventBus` (already in Contracts)
- `IModuleContext` with `DataDirectory` (feature 2)
- `ChatMessageInput` (feature 3 — must be in Contracts)
- Constructor injection via DI (feature 1)

**Storage:** Conversation history persisted to `context.DataDirectory/history.json` using `System.Text.Json` (BCL, already used in the project).

---

## Installation

```bash
# No new packages for v1.8 — everything is BCL or already referenced

# ActivatorUtilities is in Microsoft.Extensions.DependencyInjection.Abstractions
# which is already a transitive dependency of the ASP.NET Core host.
# Verify it resolves:
dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj

# After type moves, verify Contracts compiles standalone:
dotnet build src/OpenAnima.Contracts/OpenAnima.Contracts.csproj

# Verify external module project compiles against Contracts only:
dotnet build PortModule/
```

---

## Alternatives Considered

### DI Injection Approach

| Recommended | Alternative | Why Not |
|-------------|-------------|---------|
| `ActivatorUtilities.CreateInstance(IServiceProvider, Type)` | Manual reflection: find constructor, resolve params from IServiceProvider | `ActivatorUtilities` already handles overload selection, optional params, and clear error messages. Reimplementing it is ~50 lines of fragile reflection code for no benefit. |
| `ActivatorUtilities.CreateInstance` | Register external module types in DI container | External modules are loaded dynamically from disk — their types are unknown at DI registration time. Cannot pre-register. `ActivatorUtilities` is designed for exactly this "resolve from container but don't pre-register" pattern. |
| Pass `IServiceProvider` to `PluginLoader.LoadModule()` | Make `PluginLoader` itself a DI service with `IServiceProvider` injected | Both work. Passing as parameter is simpler — `PluginLoader` is currently a plain class with no DI registration. Keeping it that way avoids a registration change. |

### DataDirectory Design

| Recommended | Alternative | Why Not |
|-------------|-------------|---------|
| Per-module `IModuleContext` instance with module ID baked in | Single `IModuleContext` singleton with `GetDataDirectory(string moduleId)` method | Requiring modules to pass their own ID to get their own directory is awkward API design. Modules shouldn't need to know their own ID string — the platform knows it. Per-instance context is cleaner. |
| Lazy `Directory.CreateDirectory` on first access | Pre-create all module directories at load time | Pre-creation requires knowing all modules at startup. Lazy creation is simpler and avoids creating directories for modules that never use storage. |

### ChatMessageInput Location

| Recommended | Alternative | Why Not |
|-------------|-------------|---------|
| Move `ChatMessageInput` to `OpenAnima.Contracts` | Keep in Core, expose via a separate `OpenAnima.SDK` package | A separate SDK package adds a third project to maintain and a versioning surface. The Contracts project already IS the SDK — it's what external modules reference. Moving the type there is the correct layering. |
| Move `ChatMessageInput` to Contracts | Serialize message list as JSON string, define schema in docs | Serialization works but loses type safety for external module authors. The ContextModule would need to construct JSON strings manually. Moving the type is cleaner. |

---

## What NOT to Add

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| MEF (Managed Extensibility Framework) | The project already has `PluginLoadContext` + `AssemblyDependencyResolver` for isolation. MEF would conflict with the existing duck-typing + name-based type resolution approach and add ~200KB of dependencies. | Existing `PluginLoadContext` + `ActivatorUtilities` |
| `Microsoft.Extensions.DependencyInjection` NuGet (explicit) | Already a transitive dependency of the ASP.NET Core host. Adding it explicitly creates a version management burden with no benefit. | Rely on transitive reference |
| New `PortType.MessageList` | Extending the port type system is a larger change that affects the visual editor, wiring validation, and port color system. Not needed for v1.8 — JSON serialization over `PortType.Text` achieves the same result. | `PortType.Text` + `System.Text.Json` deserialization in LLMModule |
| Separate `OpenAnima.SDK` NuGet package | Adds a third project, versioning surface, and publish pipeline. `OpenAnima.Contracts` already serves as the SDK — external modules reference it as a project reference or DLL. | `OpenAnima.Contracts` project reference |
| `Newtonsoft.Json` | Project already uses `System.Text.Json` (BCL). Adding a second JSON library for conversation history serialization is unnecessary. | `System.Text.Json` |

---

## Integration Points with Existing Architecture

| Existing Component | v1.8 Change |
|--------------------|-------------|
| `PluginLoader.LoadModule()` | Signature gains `IServiceProvider services` parameter. `Activator.CreateInstance(moduleType)` → `ActivatorUtilities.CreateInstance(services, moduleType)`. Callers (`AnimaRuntimeManager`, `ModuleDirectoryWatcher`) pass the DI container. |
| `IModuleContext` (Contracts) | Gains `string DataDirectory { get; }`. Non-breaking addition — existing implementations (`AnimaContext`) must implement it. |
| `AnimaContext` | Implements new `DataDirectory` property. Needs module ID to compute path — either via platform-internal setter or per-module wrapper instance. |
| `ChatMessageInput` | Moves from `OpenAnima.Core.LLM` to `OpenAnima.Contracts`. Core keeps a `global using` alias for backward compat. |
| `LLMResult` | Same move as `ChatMessageInput`. |
| `ILLMService` | Moves from `OpenAnima.Core.LLM` to `OpenAnima.Contracts`. Core keeps a `global using` alias. |
| `LLMModule` | Gains second input port `messages` (PortType.Text, JSON-encoded `IReadOnlyList<ChatMessageInput>`). When `messages` port fires, uses the deserialized list directly instead of wrapping the string in a single user message. |
| `AnimaServiceExtensions.AddAnimaServices()` | No change to registrations — `IModuleContext`, `IModuleConfig`, `IEventBus`, `ICrossAnimaRouter` are already registered. `PluginLoader` callers gain access to `IServiceProvider` via the host's DI container. |
| `OpenAnima.Contracts.csproj` | Gains `ChatMessageInput.cs`, `LLMResult.cs`, `ILLMService.cs` (moved from Core). No new NuGet references. |

---

## Version Compatibility

| Component | Version | Notes |
|-----------|---------|-------|
| `ActivatorUtilities` | Built-in via `Microsoft.Extensions.DependencyInjection.Abstractions` | Available since .NET Core 1.0. `CreateInstance(IServiceProvider, Type, object[])` overload handles both required and optional constructor params. Already transitively referenced. |
| `System.Text.Json` | Built-in .NET 8 BCL | `JsonSerializer.Serialize` / `Deserialize` for conversation history and message list port encoding. No package needed. |
| `System.IO.Path` + `Directory` | Built-in .NET 8 BCL | `Path.Combine` + `Directory.CreateDirectory` for `DataDirectory` path construction. |
| `OpenAnima.Contracts` | v1.8 additions | `ChatMessageInput`, `LLMResult`, `ILLMService` moved in; `IModuleContext.DataDirectory` added. All additive — no breaking changes to `IModule`, `IModuleExecutor`, `IEventBus`, `ITickable`. |

---

## Confidence Assessment

| Area | Confidence | Rationale |
|------|------------|-----------|
| `ActivatorUtilities.CreateInstance` for DI injection | HIGH | Official .NET DI docs confirm this is the canonical pattern for "resolve from container without pre-registration." Direct code inspection of `PluginLoader.cs` confirms the exact line to change. |
| `PluginLoadContext` type identity — no change needed | HIGH | Direct code inspection of `PluginLoadContext.cs` confirms Contracts assembly falls through to Default context (returns null in `Load()`). This is the correct behavior for shared contracts. |
| `ChatMessageInput` move to Contracts — safe | HIGH | Direct code inspection confirms `ChatMessageInput` is a pure data record with no Core dependencies. `ILLMService` depends only on `ChatMessageInput` and `LLMResult` — both moving. No circular dependency risk. |
| `IModuleContext.DataDirectory` — additive, non-breaking | HIGH | Interface addition. Existing `AnimaContext` implementation must add the property — one implementation site. No consumers break (they gain a new capability). |
| JSON encoding for structured message port | MEDIUM | Avoids port type system changes (correct for v1.8 scope) but adds a serialization contract between ContextModule and LLMModule. If the schema changes, both sides must update. Acceptable for v1.8; revisit with typed ports in a future milestone. |
| No new NuGet packages needed | HIGH | All named primitives are BCL or already transitively referenced. Verified by tracing the dependency graph from `OpenAnima.Core.csproj`. |

---

## Sources

- Direct codebase inspection: `PluginLoader.cs`, `PluginLoadContext.cs`, `IModuleContext.cs`, `AnimaServiceExtensions.cs`, `ILLMService.cs`, `LLMModule.cs` — Determines exact change sites, current signatures, and what's missing. HIGH confidence (first-party source).
- [Microsoft Learn — ActivatorUtilities.CreateInstance](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.activatorutilities.createinstance) — Confirms API signature, behavior for missing required services, overload selection. HIGH confidence.
- [Microsoft Learn — Create .NET app with plugin support](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support) — AssemblyLoadContext + shared contracts pattern; confirms why Contracts must fall through to Default context. HIGH confidence.
- [Microsoft Learn — About AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext) — Type identity across contexts; confirms the null-return pattern in `Load()` is correct for shared assemblies. HIGH confidence.
- `.planning/PROJECT.md` — v1.8 requirements (PLUG-01, STOR-01, MSG-01, ECTX-01), existing tech debt description, key decisions table. HIGH confidence (first-party source).

---

*Stack research for: v1.8 SDK Runtime Parity (PluginLoader DI injection, IModuleContext.DataDirectory, structured message input, external ContextModule)*
*Researched: 2026-03-16*
*Confidence: HIGH*
