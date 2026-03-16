# Feature Research

**Domain:** SDK Runtime Parity — Plugin DI Injection, Per-Module Storage, Structured Message Input, External ContextModule — v1.8
**Researched:** 2026-03-16
**Confidence:** HIGH (all four features are well-understood .NET patterns; exact API surface is constrained by existing Contracts decisions)

---

## Context: What Already Exists

Before mapping features, the baseline matters. v1.7 shipped:

- `IModuleConfig`, `IModuleContext`, `ICrossAnimaRouter` all live in `OpenAnima.Contracts` — the interface surface is public.
- `PluginLoader` uses `Activator.CreateInstance(moduleType)` with a parameterless constructor — zero DI injection for external modules.
- `IModuleContext` exposes only `ActiveAnimaId` and `ActiveAnimaChanged` — no storage path.
- `ChatMessageInput` record lives in `OpenAnima.Core.LLM` (not Contracts) — external modules cannot reference it.
- `LLMModule` already builds a `List<ChatMessageInput>` internally but its input port accepts a single `string` prompt — no way to pass a pre-built message list from outside.
- Config is stored at `data/animas/{animaId}/module-configs/{moduleId}.json` — the path convention exists but is not surfaced to modules.

The four v1.8 features are surgical additions to close the gap between built-in and external modules.

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features that external module authors assume they have. Missing these = external modules are non-functional for any real use case.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| PluginLoader injects Contracts services into external module constructors | Any non-trivial module needs `IModuleConfig`, `IModuleContext`, `IEventBus` — without DI injection, external modules are limited to parameterless construction and cannot access any platform services | MEDIUM | `Activator.CreateInstance` must be replaced with constructor resolution. The host `IServiceProvider` is available in `PluginLoader`'s call chain. Pattern: reflect constructor parameters, resolve each from `IServiceProvider`, call `ActivatorUtilities.CreateInstance` or manual reflection. Cross-ALC type identity is the key constraint — parameter types must be matched by `FullName`, not CLR type identity. |
| `IModuleContext.DataDirectory` returns per-Anima per-module storage path | Modules that maintain state (conversation history, cache, user data) need a stable, isolated directory. Without it, every module author invents their own path convention — collisions and data loss follow. | LOW | Add `string DataDirectory { get; }` to `IModuleContext`. Implementation in `AnimaContext` computes `data/animas/{ActiveAnimaId}/module-data/{ModuleName}/` and `Directory.CreateDirectory` on first access. The module name must be passed at resolution time — either via a factory or by making `DataDirectory` a method `GetDataDirectory(string moduleId)`. Method form is safer (avoids stale path if AnimaId changes). |
| `ChatMessageInput` accessible from Contracts | External modules that want to pass structured message lists to LLMModule (or build their own LLM calls) need the record type. Currently it lives in `Core.LLM` — unreachable from external assemblies. | LOW | Move `ChatMessageInput` record to `OpenAnima.Contracts`. Add a shim in `Core.LLM` if needed for backward compat (`using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput`). No logic change — pure type relocation. |
| LLMModule accepts structured message list on a dedicated input port | A ContextModule that maintains conversation history needs to pass the full `List<ChatMessageInput>` to LLMModule, not just the latest user string. Without this port, the ContextModule cannot inject history into LLM calls. | MEDIUM | Add a second input port `messages` of a new port type (or reuse Text with JSON serialization). The cleaner approach: add `PortType.MessageList` or serialize `List<ChatMessageInput>` as JSON on a Text port. JSON-on-Text avoids a new port type and keeps the wiring engine unchanged. LLMModule deserializes on receipt. If both `prompt` and `messages` ports fire, `messages` takes precedence. |

### Differentiators (Competitive Advantage)

Features that make the SDK genuinely usable for real external modules, not just toy examples.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| External ContextModule as SDK validation artifact | Proves the entire SDK surface works end-to-end: DI injection, DataDirectory, structured messages, EventBus subscriptions — all from an external assembly. If ContextModule works, the SDK works. | MEDIUM | ContextModule is both a feature and a test. It loads via `PluginLoader`, receives `IModuleConfig` + `IModuleContext` + `IEventBus` via DI, persists conversation history to `DataDirectory`, and publishes a `List<ChatMessageInput>` JSON payload to LLMModule's `messages` port. Success = the chat pipeline works with multi-turn history from an external module. |
| `IModuleContext.GetDataDirectory(moduleId)` as a stable convention | Establishes the canonical storage path pattern for all future external modules. Modules that follow it get automatic per-Anima isolation without any path management code. | LOW | The method form (vs. a property) is important: `ActiveAnimaId` can change (Anima switching), so a property computed at call time is safer than a property cached at construction. The implementation creates the directory on first call — modules never need to call `Directory.CreateDirectory` themselves. |
| Constructor DI injection with graceful degradation | If a constructor parameter type cannot be resolved from `IServiceProvider` (e.g., a type the platform doesn't know about), `PluginLoader` falls back to `null` for optional parameters and fails with a clear error for required ones. This matches `ActivatorUtilities.CreateInstance` behavior. | LOW | The cross-ALC constraint means we cannot use `ActivatorUtilities` directly (type identity mismatch). Manual reflection: iterate constructor parameters, resolve by `FullName` match against registered services, pass `null` for unresolvable optional params. Log a warning for each unresolved optional param — helps module authors debug missing injections. |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| New `MessageList` port type in the wiring engine | "Structured messages deserve a first-class port type, not JSON-on-Text" | Requires changes to `PortType` enum, `PortTypeValidator`, `WiringEngine`, editor SVG rendering, and port color system — a large blast radius for a single use case | Serialize `List<ChatMessageInput>` as JSON on an existing Text port. LLMModule deserializes. The wiring engine stays unchanged. A real `MessageList` port type can be added in a future milestone when more modules need it. |
| Expose `ILLMService` in Contracts for external modules | "ContextModule should call the LLM directly, not route through LLMModule" | `ILLMService` wraps OpenAI SDK types (`ChatClient`, `ApiKeyCredential`) — exposing it in Contracts pulls the OpenAI SDK into the Contracts assembly, breaking the lightweight reference assembly principle | ContextModule passes structured messages to LLMModule via the `messages` port. LLMModule owns the LLM call. External modules that need LLM access use the EventBus routing pattern. |
| Per-Anima module instances (ANIMA-08) | "Each Anima should have its own ContextModule instance with independent history" | Requires replacing the global `IEventBus` singleton with per-Anima injection at construction time — a significant DI restructure that is explicitly deferred to a future milestone | ContextModule scopes its history by `ActiveAnimaId` from `IModuleContext`. One module instance, multiple Anima histories, keyed by animaId. Same pattern used by `AnimaModuleConfigService`. |
| Streaming message list input | "Pass messages as a stream, not a serialized blob" | `IAsyncEnumerable` across ALC boundaries requires careful marshaling; adds complexity to both the port system and LLMModule's input handling | Batch serialization (JSON string on Text port) is sufficient for conversation history. Streaming is a future concern if message lists grow very large. |

---

## Feature Dependencies

```
[PluginLoader DI Injection] (PLUG-01)
    ├──requires──> [IServiceProvider accessible in PluginLoader call chain] (already true — DI container exists)
    ├──requires──> [Cross-ALC type matching by FullName] (already established pattern in PluginLoader)
    └──enables──> [External ContextModule] (ECTX-01) — ContextModule needs IModuleConfig + IModuleContext + IEventBus

[IModuleContext.DataDirectory / GetDataDirectory] (STOR-01)
    ├──requires──> [IModuleContext in Contracts] (already done in v1.7)
    ├──requires──> [ActiveAnimaId non-nullable] (already guaranteed in v1.7)
    └──enables──> [External ContextModule] (ECTX-01) — ContextModule persists history to DataDirectory

[ChatMessageInput moved to Contracts] (MSG-01 prerequisite)
    ├──requires──> [Nothing new — pure type relocation]
    └──enables──> [LLMModule structured message port] (MSG-01)
                      └──enables──> [External ContextModule] (ECTX-01)

[LLMModule structured message list input port] (MSG-01)
    ├──requires──> [ChatMessageInput in Contracts] (type must be reachable from external assemblies)
    └──enables──> [External ContextModule] (ECTX-01) — ContextModule publishes message list to LLMModule

[External ContextModule] (ECTX-01)
    ├──requires──> [PLUG-01] DI injection
    ├──requires──> [STOR-01] DataDirectory
    └──requires──> [MSG-01] structured message port
```

### Dependency Notes

- PLUG-01 is the unblocking prerequisite. Without DI injection, ContextModule cannot receive any platform services and the other three features are untestable from an external assembly.
- STOR-01 and MSG-01 are independent of each other — they can be implemented in parallel.
- ECTX-01 is the integration test. It should be the last thing implemented, after the other three are working.
- `ChatMessageInput` relocation is a prerequisite for MSG-01 but has zero runtime risk — it is a pure type move with no logic change.

---

## MVP Definition

### Launch With (v1.8)

All four features are the milestone. There is no subset that validates the goal ("external SDK modules truly functional").

- [ ] **PLUG-01** — PluginLoader resolves constructor parameters from `IServiceProvider` using `FullName`-based type matching. External modules receive `IModuleConfig`, `IModuleContext`, `IEventBus`, and `ICrossAnimaRouter` (optional) via constructor injection. Unresolvable optional params get `null`; unresolvable required params produce a clear `LoadResult` error.
- [ ] **STOR-01** — `IModuleContext` gains `string GetDataDirectory(string moduleId)`. Implementation in `AnimaContext` computes `data/animas/{ActiveAnimaId}/module-data/{moduleId}/` and creates the directory on first call.
- [ ] **MSG-01** — `ChatMessageInput` moved to `OpenAnima.Contracts`. LLMModule gains a second input port `messages` (Text type, JSON-serialized `List<ChatMessageInput>`). When `messages` port fires, LLMModule deserializes and uses the provided list instead of building a single-turn list from `prompt`.
- [ ] **ECTX-01** — External ContextModule (SDK module, not built-in) that: receives DI services via constructor, maintains per-Anima conversation history in `DataDirectory`, serializes history as `List<ChatMessageInput>` JSON, publishes to LLMModule's `messages` port. End-to-end chat with multi-turn history works through the wiring engine.

### Add After Validation (v1.x)

- [ ] **`PortType.MessageList`** — first-class port type for structured message lists, once multiple modules need it (trigger: second external module that passes message lists)
- [ ] **`IModuleLifecycle` context object** — convenience wrapper injecting logger + config + context in one parameter (trigger: external module authors report constructor boilerplate friction)
- [ ] **ANIMA-08 resolution** — per-Anima module instances (trigger: user reports history bleed between Animas with the animaId-keyed workaround)

### Future Consideration (v2+)

- [ ] **Module marketplace / dynamic install** — download `.oamod` and load without manual file placement
- [ ] **`ILLMService` access for external modules** — documented EventBus delegation pattern or Contracts-level interface (after OpenAI SDK dependency is abstracted)
- [ ] **Per-Anima module instances (ANIMA-08)** — full isolation requires DI restructure

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| PluginLoader DI injection (PLUG-01) | HIGH | MEDIUM | P1 |
| IModuleContext.GetDataDirectory (STOR-01) | HIGH | LOW | P1 |
| ChatMessageInput to Contracts + LLMModule messages port (MSG-01) | HIGH | LOW | P1 |
| External ContextModule (ECTX-01) | HIGH | MEDIUM | P1 |

All four are P1 — this is a focused milestone with no P2/P3 scope.

---

## Implementation Detail Notes

### PLUG-01: Cross-ALC Constructor Resolution

The core challenge: `IServiceProvider.GetService(Type)` uses CLR type identity, which fails across `AssemblyLoadContext` boundaries. The external module's constructor parameter type `OpenAnima.Contracts.IModuleConfig` is a different CLR `Type` object than the one registered in the host DI container, even though both have `FullName == "OpenAnima.Contracts.IModuleConfig"`.

Resolution strategy:
1. Reflect the module type's constructors (prefer the one with the most parameters).
2. For each parameter, get its `FullName`.
3. Walk `IServiceProvider`'s registered services (via `IServiceCollection` snapshot or by trying known Contracts types) and match by `FullName`.
4. Build the argument array and call `Activator.CreateInstance(moduleType, args)`.

Known injectable Contracts types (the complete set for v1.8):
- `OpenAnima.Contracts.IModuleConfig`
- `OpenAnima.Contracts.IModuleContext`
- `OpenAnima.Contracts.IEventBus`
- `OpenAnima.Contracts.Routing.ICrossAnimaRouter`
- `Microsoft.Extensions.Logging.ILogger<T>` (generic — match by open generic `FullName` prefix)

`ILogger<T>` requires special handling: resolve `ILoggerFactory` from the host container and call `CreateLogger(moduleType.FullName)` to produce a typed logger without needing the generic type parameter to match across ALCs.

### STOR-01: DataDirectory Path Convention

```
data/animas/{animaId}/module-data/{moduleId}/
```

This mirrors the existing config path convention (`data/animas/{animaId}/module-configs/{moduleId}.json`) and keeps all per-Anima data co-located. The method signature:

```csharp
// In IModuleContext (Contracts)
string GetDataDirectory(string moduleId);
```

Not a property — `ActiveAnimaId` can change when the user switches Animas, so computing the path at call time is correct. The implementation calls `Directory.CreateDirectory` before returning, so callers never need to check existence.

### MSG-01: JSON-on-Text for Message List

LLMModule's new `messages` port receives a JSON string. The deserialization target is `List<ChatMessageInput>` where `ChatMessageInput` is now in Contracts. Priority rule: if `messages` port fires, use the provided list. If only `prompt` fires, build a single-turn list as before (backward compat). Both ports can coexist in a wiring graph — a module can wire either or both.

The JSON contract is simple:
```json
[
  {"role": "system", "content": "..."},
  {"role": "user", "content": "..."},
  {"role": "assistant", "content": "..."}
]
```

`System.Text.Json` with default options handles this without any custom converters.

### ECTX-01: ContextModule Design

ContextModule is an external SDK module (lives in `PortModule/` or a new `ContextModule/` directory, built with `oani new`). Its responsibilities:

1. Subscribe to `ChatInputModule.port.output` on `InitializeAsync` via `IEventBus`.
2. On each user message: append to in-memory history (keyed by `ActiveAnimaId`), persist to `DataDirectory/history.json`.
3. Serialize the full history as `List<ChatMessageInput>` JSON.
4. Publish to `LLMModule.port.messages` via `IEventBus`.

On startup, load history from `DataDirectory/history.json` if it exists (session persistence). On LLM response (subscribe to `LLMModule.port.response`): append the assistant turn to history and persist.

This module exercises all four v1.8 features in a single, testable artifact.

---

## Sources

- Existing codebase: `PluginLoader.cs` (Activator.CreateInstance pattern), `AnimaModuleConfigService.cs` (path convention), `LLMModule.cs` (ChatMessageInput usage), `IModuleContext.cs` (current interface surface)
- `IModuleConfig.cs`, `IModuleContext.cs` in Contracts — confirmed current interface boundaries
- `AnimaServiceExtensions.cs` — confirmed which Contracts types are registered in DI
- .NET `ActivatorUtilities.CreateInstance` docs — cross-ALC limitation confirmed by existing PluginLoader comment ("Use name-based comparison to handle cross-context type identity issues")
- Existing `AnimaModuleConfigService` path: `data/animas/{animaId}/module-configs/{moduleId}.json` — DataDirectory convention derived from this

---

*Feature research for: OpenAnima v1.8 SDK Runtime Parity*
*Researched: 2026-03-16*
