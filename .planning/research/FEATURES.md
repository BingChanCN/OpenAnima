# Feature Research

**Domain:** Runtime Foundation — Concurrency Model, Plugin API, Module Decoupling — v1.7
**Researched:** 2026-03-14
**Confidence:** HIGH (concurrency patterns well-established; exact API surface design is project-specific judgment)

---

## Feature Landscape

### Area 1: Activity Channel Execution Model (CONC-01 / CONC-02 / CONC-03)

The goal: make module execution concurrency-safe and introduce an Activity Channel model for stateful Animas.

**Problem statement from codebase:** Every module in the current system is a singleton registered in DI. `ExecuteAsync` on a given module can be called concurrently if two heartbeat ticks fire before the prior tick's execution chain finishes, or if cross-Anima routing delivers a request while a heartbeat-triggered execution is in flight. The current `_state` field, `_pendingPrompt` field, and similar per-module state variables are written and read without synchronization — a classic shared-mutable-state race condition.

#### Table Stakes

Features users/developers expect from any concurrency-safe agent runtime. Missing these means data corruption or dropped messages under realistic load.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Race-free module state mutation | Module `_state`, `_pendingPrompt`, `_pendingRequest` etc. are shared fields — concurrent writes corrupt them | MEDIUM | SemaphoreSlim(1,1) guard on `ExecuteAsync` is the idiomatic .NET async lock; already used in HeartbeatLoop's `_tickLock` |
| Skip-tick behavior when module is busy | If a module is already running, a new tick should be dropped, not queued — prevents cascading backlog | LOW | Early-return pattern: `if (!await _lock.WaitAsync(0)) return;` (zero-timeout tryacquire) |
| Stateless Anima parallel execution | Mechanical/utility Animas (fixed-text, text-split, etc.) have no shared state — parallel requests should be allowed | MEDIUM | Stateless request isolation = separate `ExecuteAsync` invocation per-request with no shared mutable fields; requires module-level flag or interface marker |
| Stateful Anima channel-internal serialization | Chat-capable Animas have conversation state — concurrent LLM calls would interleave messages nonsensically | MEDIUM | `Channel<ExecutionRequest>` with single consumer loop; parallel channels (heartbeat, chat) serialized within each channel |
| Heartbeat tick does not block on I/O modules | LLMModule and HttpRequestModule await external calls (seconds); HeartbeatLoop tick should not block waiting for them | MEDIUM | Fire-and-forget dispatch for I/O modules with their own per-module SemaphoreSlim guard; already partially present in HeartbeatLoop's `_tickLock` skip pattern |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Named Activity Channels (heartbeat vs. chat) | Two parallel activity channels per stateful Anima — heartbeat channel processes proactive agent ticks; chat channel processes user messages — both serialized internally, run concurrently with each other | HIGH | This is the Orleans Grain reentrancy pattern applied at the Anima level: two logical "tracks" of execution that don't block each other. Key insight: a user message should not wait for a heartbeat tick to finish, and vice versa. Requires `Channel<T>` per named channel with dedicated consumer task. |
| Correlation-ID-scoped execution context | Each request flowing through the wiring graph carries an execution context (correlationId, channel name, timestamp) — allows concurrent in-flight requests to be distinguishable in logs and monitoring | MEDIUM | `ModuleExecutionContext` record threaded through `ExecuteAsync(context, ct)` instead of implicit module state fields. Prerequisite for stateless module parallelism. |
| Per-module concurrency mode declaration | Module declares whether it is `Serialized` or `Stateless` via attribute or interface — runtime enforces the right execution strategy without module authors needing to write synchronization code | MEDIUM | Analogous to Orleans' `[Reentrant]` attribute. `[StatelessModule]` marker attribute; runtime checks and spawns per-invocation vs. serialized dispatch. |

#### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Global lock on WiringEngine execution | "Simplest way to prevent races — lock the whole execution" | Serializes all Animas, destroys parallel Anima benefit, kills heartbeat cadence | Per-module SemaphoreSlim locks; channel-per-activity model |
| Unbounded execution queue per module | "Don't drop messages — queue them all" | Queue grows unbounded under backpressure; memory leak in pathological loops | Bounded channel with skip-on-full (drop policy); explicit backpressure via BoundedChannelOptions.DropOldest |
| Thread-per-module approach | "Give each module its own dedicated thread for isolation" | ~14 modules × N Animas = unsustainable thread count; .NET thread pool is better | Async/await with SemaphoreSlim; no dedicated threads needed |

---

### Area 2: Plugin API Surface — Contracts Layer (API-01 / API-02)

The goal: move `IAnimaModuleConfigService`, `IAnimaContext`, `ICrossAnimaRouter` (and any other interfaces external modules need) from `OpenAnima.Core` into `OpenAnima.Contracts`, so external plugin authors have feature parity with built-in modules.

**Current state from codebase:**
- `OpenAnima.Contracts` contains: `IModule`, `IModuleExecutor`, `IModuleInput`, `IModuleOutput`, `IModuleMetadata`, `ITickable`, `IEventBus`, `ModuleEvent`, `ModuleExecutionState`, and all Port types.
- `OpenAnima.Core` contains (not accessible to external plugins): `IAnimaModuleConfigService`, `IAnimaContext`, `ICrossAnimaRouter`, `ILLMService`, and `IAnimaRuntimeManager`.
- Every built-in module constructor takes `IAnimaModuleConfigService` and `IAnimaContext` injected from DI — these are Core types. External plugins cannot access them.

#### Table Stakes

Features external plugin authors assume they have. Missing these = external plugins cannot do anything meaningful beyond trivial text transformation.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| `IModuleConfig` (config read/write) in Contracts | Every non-trivial module needs per-Anima config (API URL, delay, label, etc.); without this, external modules are un-configurable | LOW | Move `IAnimaModuleConfigService` (renamed to `IModuleConfig` or `IAnimaModuleConfig`) to Contracts. Exposes only `GetConfig(animaId, moduleId)` and `SetConfigAsync(...)` — no `InitializeAsync` (that's an implementation detail). |
| `IAnimaContext` in Contracts | Modules need to know which Anima they are running under to scope their config lookups | LOW | `IAnimaContext` is already a lean interface (2 members). Move to Contracts as-is or add to a `OpenAnima.Contracts.Runtime` namespace. |
| `ILogger<T>` injection in plugin constructors | External module developers expect standard .NET logging; without it they have no observability | LOW | Already works via DI — not a Contracts change; document that `ILogger<T>` is automatically available via host DI. Template generator should include it. |
| Module config schema declaration | The config sidebar in the editor needs to know what fields a module has to render the UI | MEDIUM | `IModuleConfigSchema` or `IModuleMetadata` extension: method `GetConfigFields()` returning `IReadOnlyList<ConfigField>` — each field has id, label, type (text/dropdown/number), default value. Built-in modules currently hard-code their UI in Razor. External modules cannot do that. This is the bridge. |
| Deterministic module identity scoping | Module config is keyed by `(animaId, moduleId)` where `moduleId = Metadata.Name` — external modules must follow the same convention | LOW | Document the convention. `IModuleMetadata.Name` is already the key. No API change needed, just spec clarity. |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| `ICrossAnimaRouter` in Contracts | External modules can participate in cross-Anima routing (send requests, register services) — not just built-in modules | MEDIUM | `ICrossAnimaRouter` interface is already clean (no implementation details leak through it). Move to Contracts. External routing modules become possible (e.g., a third-party module that calls a specialized Anima). |
| `IModuleConfigSchema` for sidebar auto-generation | Render config UI for any module (built-in or external) from a schema declaration — no Razor component needed per-module | HIGH | This is a significant DX improvement. External module devs declare fields in C#; the platform renders the sidebar automatically. Built-in modules that currently use custom Razor components would need migration. |
| `IModuleLifecycle` context injection | Rich lifecycle context object passed at `InitializeAsync` time — provides logger, config, animaContext, and (optionally) router in a single object | LOW | Reduces constructor injection boilerplate from 4+ params to 1. Pattern from Apache Ignite's `IPluginContext<T>` and .NET Generic Host's `IHostApplicationLifetime`. Does not replace DI — adds a convenience wrapper for the most common services. |

#### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Expose `ILLMService` in Contracts | "Let external modules call the LLM directly" | Leaks the LLM service contract (OpenAI SDK types) into Contracts; breaks the abstraction; external modules should route to LLMModule via EventBus, not call LLM directly | Keep `ILLMService` in Core; if external modules need LLM access, document the EventBus pattern |
| Expose `IAnimaRuntimeManager` in Contracts | "Let external modules create or manage Animas" | Anima lifecycle management is a host concern, not a module concern; exposing it to modules violates the responsibility boundary | If a module needs to discover other Animas' ports, use `ICrossAnimaRouter.GetPortsForAnima()` which is already scoped appropriately |
| Auto-discover config fields via reflection | "Scan module class for property attributes to build the schema" | Fragile across `AssemblyLoadContext` boundaries (type identity issues); also mixes config schema with module implementation | Explicit `GetConfigFields()` method on an interface — opt-in, deterministic, ALC-safe |

---

### Area 3: Built-in Module Decoupling (DECPL-01)

The goal: migrate all 14 built-in modules to depend only on `OpenAnima.Contracts`, not `OpenAnima.Core` internals.

**Current dependency audit (from codebase grep):**

| Module | Core Imports Used | Why |
|--------|-------------------|-----|
| FixedTextModule | `IAnimaModuleConfigService`, `IAnimaContext` | Reads config for text content |
| TextJoinModule | `IAnimaModuleConfigService`, `IAnimaContext` | Reads config for join separator |
| TextSplitModule | `IAnimaModuleConfigService`, `IAnimaContext` | Reads config for split delimiter |
| ConditionalBranchModule | `IAnimaModuleConfigService`, `IAnimaContext` | Reads condition expression from config |
| HttpRequestModule | `IAnimaModuleConfigService`, `IAnimaContext`, `SsrfGuard` | Reads URL/method/headers config; SSRF protection |
| LLMModule | `IAnimaModuleConfigService`, `IAnimaContext`, `ILLMService`, `ICrossAnimaRouter`, `FormatDetector` | Full feature set |
| AnimaInputPortModule | `IAnimaModuleConfigService`, `IAnimaContext`, `ICrossAnimaRouter` | Registers routing port |
| AnimaOutputPortModule | `IAnimaModuleConfigService`, `IAnimaContext`, `ICrossAnimaRouter` | Completes routing requests |
| AnimaRouteModule | `IAnimaModuleConfigService`, `IAnimaContext`, `ICrossAnimaRouter` | Dispatches cross-Anima requests |
| ChatInputModule | None (Core) | Already clean — only uses Contracts |
| ChatOutputModule | None audited | Likely uses Core.Services |
| HeartbeatModule | None audited | Likely clean |

#### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| All modules compile against Contracts only | Plugin authors expect that built-in modules are held to the same standard as plugins — if built-ins use Core, the standard is a lie | LOW-MEDIUM | Purely mechanical: move `IAnimaModuleConfigService` → Contracts, move `IAnimaContext` → Contracts, update `using` in all 14 modules. No logic changes. |
| Tests compile and pass after decoupling | Regression safety: decoupling must not break existing behavior | LOW | Existing integration tests provide the safety net. Only using-statement changes expected. |
| Module project template (oani new) updated | Template should generate a module that uses the new Contracts-only API, not stale Core references | LOW | One file change: `NewCommand.cs` template strings. |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| `SsrfGuard` abstracted behind `IUrlValidator` in Contracts | HttpRequestModule can be decoupled AND external modules that make HTTP requests get SSRF protection without reimplementing it | MEDIUM | `IUrlValidator` interface in Contracts: `bool IsAllowed(string url)`. Core provides default `SsrfGuard` impl. Modules receive it via DI injection — they don't need to know about SSRF. |
| `IModuleConfigSchema` generation on built-in modules | Once built-in modules implement `GetConfigFields()`, the custom Razor sidebar components can be replaced with a single generic `<AutoConfigSidebar>` component | HIGH | Large DX improvement for future module authors — proves the pattern works on battle-tested modules before external authors see it. This is a Phase 2 enhancement, not Phase 1. |
| Per-Anima module instances (ANIMA-08 resolution) | With Contracts-only modules, modules no longer have hard ties to Core singletons — the path to per-Anima module instantiation opens up | HIGH | ANIMA-08 (global IEventBus singleton for DI compatibility) is a blocker for full per-Anima isolation. Decoupling is a prerequisite, not the final step. Scope to v1.7 only if ANIMA-08 is resolved. |

#### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Move all of `OpenAnima.Core` into `OpenAnima.Contracts` | "Just merge the projects — simpler" | Contracts must be a lightweight reference assembly; pulling in ASP.NET Core, SignalR, and LLM SDK types defeats the purpose | Move only the interfaces needed by module authors; keep implementations in Core |
| Migrate `FormatDetector` and `LLMService` to Contracts | "LLMModule needs them and LLMModule should only depend on Contracts" | `FormatDetector` is a Core utility; `LLMService` wraps the OpenAI SDK — both are implementation concerns, not contracts | `LLMModule` can remain in `Core/Modules/` and reference both Contracts and Core; only the public interface (`IModuleExecutor`) needs to be in Contracts |

---

## Feature Dependencies

```
[Activity Channel Model]
    ├──requires──> [SemaphoreSlim per-module guard] (baseline concurrency safety)
    ├──requires──> [ModuleExecutionContext record] (request identity for stateless execution)
    └──enhances──> [HeartbeatLoop skip-tick behavior] (already exists; explicit with new model)

[Module Config API in Contracts (IModuleConfig)]
    ├──requires──> [IAnimaContext in Contracts] (config is keyed by animaId)
    └──enables──> [Built-in Module Decoupling] (modules can drop Core.Services import)

[IAnimaContext in Contracts]
    └──enables──> [Built-in Module Decoupling] (modules can drop Core.Anima import)

[ICrossAnimaRouter in Contracts]
    ├──requires──> [IModuleConfig in Contracts] (routing modules need config)
    ├──requires──> [IAnimaContext in Contracts] (routing modules need animaId)
    └──enables──> [External routing modules] (AnimaRoute-equivalent plugins possible)

[Built-in Module Decoupling]
    ├──requires──> [IModuleConfig in Contracts] (replaces Core.Services.IAnimaModuleConfigService)
    ├──requires──> [IAnimaContext in Contracts] (replaces Core.Anima.IAnimaContext)
    └──unlocks──> [ANIMA-08 resolution] (path to per-Anima module instances)

[IModuleConfigSchema]
    ├──requires──> [Built-in Module Decoupling] (proves the pattern on real modules first)
    └──enables──> [Auto-rendered config sidebar] (replaces per-module Razor components)
```

### Dependency Notes

- **IAnimaContext before IModuleConfig:** `IModuleConfig.GetConfig(animaId, moduleId)` requires `animaId` — modules get the `animaId` from `IAnimaContext.ActiveAnimaId`. Both must move to Contracts together (one `using` change replaces two Core imports per module).
- **Config move before decoupling:** The mechanical decoupling of 14 modules is blocked until the interfaces they depend on (`IAnimaModuleConfigService`, `IAnimaContext`) live in Contracts. This is the critical path dependency.
- **Activity Channel model is independent:** Concurrency fixes do not depend on the Contracts changes. These two work streams can be phased in parallel (separate roadmap phases) or sequenced.
- **ICrossAnimaRouter is optional for Phase 1:** Only AnimaInputPortModule, AnimaOutputPortModule, and AnimaRouteModule need it. The other 11 modules can be fully decoupled without touching the router interface. Moving the router to Contracts can be a Phase 2 item.

---

## MVP Definition

### Launch With (v1.7)

Minimum set that hardens the runtime and enables external module parity.

- [ ] **SemaphoreSlim per-module concurrency guard** — prevents race conditions on `_state` and `_pending*` fields in all 14 modules (CONC-01)
- [ ] **Stateless/stateful Anima execution policy** — mechanical Animas (no chat state) allow concurrent request-level execution; chat Animas serialize execution per activity channel (CONC-02, CONC-03)
- [ ] **`IModuleConfig` interface in Contracts** — renamed/moved from `Core.Services.IAnimaModuleConfigService`; exposes only `GetConfig` and `SetConfigAsync` (API-01)
- [ ] **`IAnimaContext` moved to Contracts** — same interface, new namespace; DI registration unchanged (API-01)
- [ ] **All 14 built-in modules updated** to use `Contracts.IModuleConfig` and `Contracts.IAnimaContext` instead of Core types (DECPL-01)
- [ ] **Module project template updated** to reflect Contracts-only dependency (SDK consistency)
- [ ] **Module management UI** — install, uninstall, list, search modules (MODMGMT-01/02/03/06)

### Add After Validation (v1.x)

- [ ] **`ICrossAnimaRouter` moved to Contracts** — enables external routing modules; blocked until built-in module decoupling is stable
- [ ] **`IModuleConfigSchema` interface** — external modules declare config fields programmatically; platform auto-renders sidebar
- [ ] **`IModuleLifecycle` context object** — convenience wrapper injecting logger + config + animaContext in one parameter at InitializeAsync time
- [ ] **Activity Channel named-channel model** — explicit `heartbeat` and `chat` channels per stateful Anima with dedicated `Channel<T>` consumer loops
- [ ] **`[StatelessModule]` marker attribute** — runtime spawns per-invocation execution for stateless modules instead of serializing

### Future Consideration (v2+)

- [ ] **Per-Anima module instances (ANIMA-08 resolution)** — full isolation requires replacing the global `IEventBus` singleton with per-Anima injection at module construction time; significant DI restructure
- [ ] **`IUrlValidator` in Contracts** — SSRF protection abstracted behind an interface; HttpRequestModule and external HTTP modules share the guard
- [ ] **Auto-rendered config sidebar** — replace per-module Razor components with a single generic component driven by `IModuleConfigSchema`
- [ ] **`ILLMService` access pattern for external modules** — document EventBus-based LLM delegation pattern for plugin authors who want LLM access without directly invoking Core

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Per-module SemaphoreSlim concurrency guard | HIGH | LOW | P1 |
| IModuleConfig in Contracts | HIGH | LOW | P1 |
| IAnimaContext moved to Contracts | HIGH | LOW | P1 |
| All 14 modules decoupled (using change) | HIGH | LOW | P1 |
| Module management UI (install/uninstall/search) | HIGH | MEDIUM | P1 |
| Stateless/stateful execution policy | MEDIUM | MEDIUM | P1 |
| SDK template updated | MEDIUM | LOW | P1 |
| ICrossAnimaRouter in Contracts | MEDIUM | LOW | P2 |
| IModuleConfigSchema interface | HIGH | HIGH | P2 |
| IModuleLifecycle context object | MEDIUM | LOW | P2 |
| Named Activity Channels (heartbeat/chat) | MEDIUM | HIGH | P2 |
| StatelessModule attribute | LOW | MEDIUM | P2 |
| IUrlValidator in Contracts | LOW | MEDIUM | P3 |
| Per-Anima module instances (ANIMA-08) | HIGH | HIGH | P3 |
| Auto-rendered config sidebar | HIGH | HIGH | P3 |

**Priority key:**
- P1: Must have for v1.7 launch
- P2: Should have, add when v1.7 core is stable
- P3: Future milestone

---

## Implementation Detail Notes

### Concurrency: Per-Module SemaphoreSlim Pattern

The idiomatic .NET async lock for module execution is already used in `HeartbeatLoop._tickLock`. Apply the same pattern per-module:

```csharp
private readonly SemaphoreSlim _executionLock = new(1, 1);

public async Task ExecuteAsync(CancellationToken ct = default)
{
    // Skip if already executing (drop, not queue)
    if (!await _executionLock.WaitAsync(0, ct)) return;
    try
    {
        // ... execution logic
    }
    finally
    {
        _executionLock.Release();
    }
}
```

Zero-timeout `WaitAsync(0)` ensures no queuing — the tick is dropped if the prior execution hasn't finished. This matches the existing HeartbeatLoop `_skippedCount` tracking pattern. `SkippedCount` can be surfaced per-module in the editor monitor for observability.

The `_pendingPrompt`, `_pendingRequest`, and similar intermediate state fields are safe once the lock is held — no further synchronization needed inside the lock body.

### Contracts: Minimal Interface Surface for Modules

The moved interfaces should expose only what module authors genuinely need — not the full implementation API:

```csharp
// OpenAnima.Contracts.IModuleConfig
public interface IModuleConfig
{
    Dictionary<string, string> GetConfig(string animaId, string moduleId);
    Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config);
}

// OpenAnima.Contracts.IAnimaContext (move as-is — already lean)
public interface IAnimaContext
{
    string? ActiveAnimaId { get; }
    void SetActive(string animaId);
    event Action? ActiveAnimaChanged;
}
```

`IAnimaModuleConfigService.InitializeAsync()` is an implementation detail (startup bootstrapping) — omit from the Contracts interface. The Core implementation class still has it; modules never call `InitializeAsync()` directly.

### Decoupling: What Changes per Module

Each of the 14 modules needs two `using` changes:
- `using OpenAnima.Core.Services;` → `using OpenAnima.Contracts;` (for `IModuleConfig`)
- `using OpenAnima.Core.Anima;` → `using OpenAnima.Contracts;` (for `IAnimaContext`)

Constructor parameter types change: `IAnimaModuleConfigService` → `IModuleConfig`. DI registrations in `AnimaServiceExtensions.cs` and `WiringServiceExtensions.cs` must add the new Contracts interface binding (the Core implementation implements both the old and new interfaces during the transition, or the old interface is removed after migration).

Modules that also reference `ICrossAnimaRouter` (AnimaInputPort, AnimaOutputPort, AnimaRoute) are Phase 2 — the router interface move is deferred until the simpler config/context move is stable.

### Activity Channel: Naming Convention

Based on the Orleans grain model and `System.Threading.Channels` patterns:

- `heartbeat` channel: driven by `HeartbeatLoop` ticks — proactive agent behavior
- `chat` channel: driven by user messages from `ChatInputModule` — reactive conversation

Each channel is `Channel<ExecutionRequest>.CreateBounded(capacity: 1, options: DropOldest)` for the heartbeat channel (dropping stale ticks is acceptable) and `Channel<ExecutionRequest>.CreateUnbounded()` for the chat channel (user messages should not be silently dropped).

A stateful Anima has both channels. A stateless/mechanical Anima has neither — each `ExecuteAsync` call spawns directly on the thread pool with no channel buffering.

---

## Competitor / Reference Analysis

| Feature | Orleans (Grains) | Semantic Kernel Agents | Our Approach |
|---------|------------------|------------------------|--------------|
| Concurrency per unit | Single-threaded grain turns; `[Reentrant]` for opt-in interleaving | Actor-based runtime per orchestration invocation | Per-module SemaphoreSlim; named channels for heartbeat/chat |
| Plugin API surface | Grain interfaces + `IGrainFactory` (host services) | Kernel `IKernelPlugin` with function/filter registration | `IModuleConfig` + `IAnimaContext` + `ICrossAnimaRouter` in Contracts |
| Internal/external parity | Grains are grains — built-in and external use identical interfaces | Built-in and custom plugins are identical `KernelFunction` | Built-in modules depend on Core (current gap); target: Contracts-only |
| Config injection | Constructor DI on grains; `[Inject]` attributes | `KernelArguments` passed at invocation time | `IModuleConfig.GetConfig(animaId, moduleId)` — per-Anima keyed config |
| Logging | `ILogger<T>` via DI (standard) | `ILogger<T>` via DI (standard) | `ILogger<T>` via DI — already works; document in SDK |

**Key insight from Orleans:** The grain single-threaded turn model eliminates almost all races inside a grain. OpenAnima modules are not grains (they share a process-level DI container), but the SemaphoreSlim pattern achieves the same serialization guarantee within each module. Named channels (heartbeat/chat) achieve the Orleans reentrancy pattern — two logical tracks that interleave without blocking each other.

**Key insight from .NET Channels:** `System.Threading.Channels` with `SingleReader = true` provides lock-free, backpressure-aware serialized processing. The bounded channel with `DropOldest` for heartbeat ticks is the canonical pattern for "skip if busy" without a manual `isOperating` flag.

---

## Sources

- [Channels - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)
- [An Introduction to System.Threading.Channels - .NET Blog](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/)
- [Request scheduling (Orleans grains) - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/orleans/grains/request-scheduling)
- [Orleans Virtual Actors in Practice - DevelopersVoice](https://developersvoice.com/blog/dotnet/orleans-virtual-actors-in-practice/)
- [Building a Plugin-Ready Modular Monolith in .NET - DevelopersVoice](https://developersvoice.com/blog/dotnet/building_plugin_ready_modular_monolith/)
- [Plugin Architecture Pattern in C# - Code Maze](https://code-maze.com/csharp-plugin-architecture-pattern/)
- [GitHub - natemcmaster/DotNetCorePlugins](https://github.com/natemcmaster/DotNetCorePlugins)
- [IPluginContext<T> - Apache Ignite.NET](https://ignite.apache.org/releases/latest/dotnetdoc/api/Apache.Ignite.Core.Plugin.IPluginContext-1.html)
- [Semantic Kernel Agent Orchestration | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/)
- [Deep Dive Into Race Condition Problem in .NET - Medium](https://resulhsn.medium.com/deep-dive-into-race-condition-problem-in-net-5e881f64e554)
- [Building High-Performance .NET Apps With C# Channels - AntonDevTips](https://antondevtips.com/blog/building-high-performance-dotnet-apps-with-csharp-channels)

---

*Feature research for: OpenAnima v1.7 Runtime Foundation — Concurrency, Plugin API, Module Decoupling*
*Researched: 2026-03-14*
