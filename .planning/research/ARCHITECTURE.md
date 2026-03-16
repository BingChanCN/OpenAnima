# Architecture Patterns

**Domain:** SDK Runtime Parity — PluginLoader DI, IModuleContext.DataDirectory, LLM structured input, external ContextModule
**Researched:** 2026-03-16
**Confidence:** HIGH (all conclusions from direct codebase inspection — no external sources needed)

---

## Current Architecture (v1.7 Baseline)

```
IServiceProvider (singleton DI container)
  ├── AnimaContext  (implements IModuleContext — ActiveAnimaId, ActiveAnimaChanged)
  ├── AnimaModuleConfigService  (implements IModuleConfig — per-Anima per-module config)
  ├── ICrossAnimaRouter
  ├── IEventBus  (global singleton — known limitation ANIMA-08)
  ├── LLMModule, ChatInputModule, ... (12 built-in modules — AddSingleton, full DI)
  └── AnimaRuntimeManager
        └── AnimaRuntime (per-Anima)
              ├── EventBus (isolated)
              ├── PluginRegistry (isolated)
              ├── HeartbeatLoop
              ├── WiringEngine
              └── ActivityChannelHost (3 named channels: heartbeat/chat/routing)

PluginLoader (standalone, no DI)
  └── Activator.CreateInstance(moduleType)  ← zero DI, parameterless constructor only
```

**The gap:** External modules loaded via PluginLoader get no services. Built-in modules get
full DI because they are registered with `services.AddSingleton<T>()` and resolved by the
container. External modules bypass the container entirely.

---

## v1.8 Integration Points

### PLUG-01: PluginLoader DI Injection

**What needs to change:** `PluginLoader.LoadModule()` currently calls
`Activator.CreateInstance(moduleType)` with no arguments. It needs to resolve constructor
parameters from `IServiceProvider` instead.

**Constraint — cross-context type identity:** The external module's constructor parameter
types (e.g. `IModuleConfig`) are loaded from the plugin's `PluginLoadContext`, not the
host's. Direct `ActivatorUtilities.CreateInstance(sp, moduleType)` will fail because the
host's `IModuleConfig` type identity does not match the plugin's loaded copy.

**Solution — reflection-based service injection:**
Inspect constructor parameters by `FullName`, resolve matching services from
`IServiceProvider` by interface FullName comparison, then invoke the constructor via
`ConstructorInfo.Invoke`. This mirrors the existing duck-typing pattern already used for
`ITickable` in `HeartbeatLoop`.

**Component changes:**

| Component | Change Type | What Changes |
|-----------|-------------|--------------|
| `PluginLoader` | Modify | Accept `IServiceProvider` in constructor; replace `Activator.CreateInstance` with reflection-based injection |
| `AnimaServiceExtensions` or `AnimaRuntime` | Modify | Pass `IServiceProvider` when constructing `PluginLoader` |

**New injection flow:**
```
PluginLoader.LoadModule(moduleDirectory)
  → reflect constructor parameters by FullName
  → for each param: match against known Contracts interface FullNames
  → resolve from IServiceProvider: IModuleConfig, IModuleContext, IEventBus, ICrossAnimaRouter
  → invoke constructor with resolved args (null for unresolvable optional params)
  → call InitializeAsync()
```

**Services injectable into external modules (Contracts surface only):**
- `IModuleConfig` — per-Anima per-module config (singleton in DI)
- `IModuleContext` — ActiveAnimaId + ActiveAnimaChanged event (singleton in DI)
- `IEventBus` — global singleton (ANIMA-08 known limitation, acceptable for v1.8)
- `ICrossAnimaRouter` — cross-Anima routing (singleton in DI)

**PortModule canary** already declares these as optional constructor parameters — it will
validate injection without any changes to the module itself.

---

### STOR-01: IModuleContext.DataDirectory

**What needs to change:** `IModuleContext` in `OpenAnima.Contracts` currently exposes only
`ActiveAnimaId` and `ActiveAnimaChanged`. It needs a method that returns a per-Anima
per-module storage path.

**Path convention:**
```
data/animas/{animaId}/module-data/{moduleId}/
```
This mirrors the existing `module-configs/{moduleId}.json` pattern in
`AnimaModuleConfigService`.

**Component changes:**

| Component | Change Type | What Changes |
|-----------|-------------|--------------|
| `IModuleContext` (Contracts) | Modify | Add `string GetDataDirectory(string moduleId)` method |
| `AnimaContext` (Core) | Modify | Implement `GetDataDirectory` — constructs path from `animasRoot + ActiveAnimaId + "module-data" + moduleId`, calls `Directory.CreateDirectory` |
| `AnimaServiceExtensions` | Modify | Pass `animasRoot` to `AnimaContext` constructor (currently `AnimaContext` has no constructor params) |

**Design decision — method vs property:**
`GetDataDirectory(string moduleId)` as a method (not a property) because the path is
module-specific. A module calls `_context.GetDataDirectory(Metadata.Name)` and gets a
ready-to-use path.

**Directory creation:** `GetDataDirectory` creates the directory on first call (same
pattern as `AnimaModuleConfigService.GetConfigPath`). Modules do not need to call
`Directory.CreateDirectory` themselves.

---

### MSG-01: LLMModule Structured Message List Input

**Current state:** `LLMModule` subscribes to `LLMModule.port.prompt` (a `string` event).
Internally it already builds a `List<ChatMessageInput>` — it adds a system message if
routing is configured, then appends the user string as a single user message. The
`ChatMessageInput` record lives in `OpenAnima.Core.LLM` (not Contracts).

**What needs to change:** Add a second input port that accepts a pre-built message list,
allowing an upstream module (ContextModule) to pass conversation history directly.

**Recommended approach — JSON-serialized Text port:**
Avoid adding a new `PortType` enum value (breaking change to Contracts, affects port
validation, editor rendering, all existing port type switches). Instead, define a
convention: the `messages` port carries a JSON-serialized `List<ChatMessageInput>`.
`LLMModule` deserializes on receipt. This is consistent with how `ConditionalBranchModule`
passes expression strings through Text ports.

**ChatMessageInput location:** Currently in `OpenAnima.Core.LLM`. For external modules
(ContextModule) to construct `ChatMessageInput` values, the record must move to
`OpenAnima.Contracts`. `ILLMService` also lives in Core — it stays there for v1.8
(PROJECT.md notes this as known debt).

**Component changes:**

| Component | Change Type | What Changes |
|-----------|-------------|--------------|
| `ChatMessageInput` record | Move | `OpenAnima.Core.LLM` → `OpenAnima.Contracts` |
| `LLMModule` | Modify | Add `[InputPort("messages", PortType.Text)]`; subscribe to `LLMModule.port.messages`; deserialize JSON to `List<ChatMessageInput>`; prepend system message if routing configured; `messages` port takes precedence over `prompt` port |
| `ILLMService` | No change | Stays in Core for v1.8 |

**Port priority:** When both `prompt` and `messages` ports receive data in the same tick,
`messages` takes precedence (it carries full history). `prompt` remains for backward
compatibility with existing single-turn wiring configurations.

---

### ECTX-01: External ContextModule

**What it is:** An external SDK module (lives outside Core, loaded via PluginLoader) that:
1. Subscribes to `ChatInputModule.port.userMessage` — receives user input
2. Maintains an in-memory conversation history (`List<ChatMessageInput>`)
3. Appends the new user message to history
4. Serializes history to JSON
5. Publishes to `ContextModule.port.messages` (Text port, JSON payload)
6. LLMModule receives the full history on its `messages` port
7. Subscribes to `LLMModule.port.response` — appends assistant response to history

**Dependencies this module needs (all injectable via PLUG-01):**
- `IEventBus` — subscribe to user messages, publish to LLM messages port
- `IModuleContext` — get `ActiveAnimaId` for storage path (via STOR-01)
- `IModuleConfig` — read config (e.g. max history length)

**Storage:** Uses `IModuleContext.GetDataDirectory("ContextModule")` to persist history
across sessions. On `InitializeAsync`, loads history from disk. On each message, appends
and persists.

**Component:** New file, external to Core. Lives in a separate project (e.g.
`ContextModule/`). No changes to Core required beyond PLUG-01, STOR-01, MSG-01.

---

## Data Flow After v1.8

### Single-turn (existing, unchanged)
```
User types → ChatInputModule → [chat channel] → EventBus
  → LLMModule.port.prompt (string)
  → LLMModule builds [user message]
  → LLM API call
  → ChatOutputModule → UI
```

### Multi-turn with ContextModule (new)
```
User types → ChatInputModule → [chat channel] → EventBus
  → ContextModule.port.userMessage (string)
  → ContextModule appends to history List<ChatMessageInput>
  → ContextModule serializes to JSON
  → ContextModule publishes to ContextModule.port.messages (Text/JSON)
  → WiringEngine routes to LLMModule.port.messages
  → LLMModule deserializes List<ChatMessageInput>
  → LLMModule prepends system message if routing configured
  → LLM API call with full history
  → ChatOutputModule → UI
  → ContextModule subscribes to LLMModule.port.response
  → ContextModule appends assistant response to history
  → ContextModule persists history to DataDirectory
```

---

## Component Boundary Map

```
OpenAnima.Contracts (public SDK surface — changes in v1.8)
  ├── IModule, IModuleExecutor, IModuleMetadata
  ├── IModuleConfig
  ├── IModuleContext  (+ GetDataDirectory — new method)
  ├── IEventBus, ICrossAnimaRouter
  ├── ChatMessageInput  (moved from Core.LLM — new)
  └── Ports/, Routing/

OpenAnima.Core (runtime host — changes in v1.8)
  ├── Plugins/PluginLoader       (modify: accept IServiceProvider, reflection-based injection)
  ├── Anima/AnimaContext          (modify: implement GetDataDirectory, accept animasRoot)
  ├── DependencyInjection/AnimaServiceExtensions  (modify: pass animasRoot to AnimaContext)
  ├── Modules/LLMModule           (modify: add messages port, use ChatMessageInput from Contracts)
  └── LLM/ILLMService             (no change — stays in Core)

ContextModule/ (new external project)
  └── ContextModule.cs            (new: IModuleExecutor, uses IEventBus + IModuleContext + IModuleConfig)
```

---

## Build Order

Dependencies flow strictly in this order — each step unblocks the next:

**Step 1 — Move ChatMessageInput to Contracts**
Unblocks: ContextModule can reference it. LLMModule still compiles (same type, new namespace).
Risk: Any Core code importing `OpenAnima.Core.LLM.ChatMessageInput` needs namespace update.
Scope: `OpenAnima.Contracts` (add record), `OpenAnima.Core.LLM` (remove record, add shim or
update usings), `LLMModule.cs` (update using).

**Step 2 — Add IModuleContext.GetDataDirectory**
Unblocks: AnimaContext implementation, ContextModule storage.
No downstream breakage — additive interface change.
Scope: `OpenAnima.Contracts/IModuleContext.cs` (add method signature).

**Step 3 — Implement AnimaContext.GetDataDirectory**
Requires: Step 2 (interface), animasRoot passed to AnimaContext constructor.
Unblocks: ContextModule can call `_context.GetDataDirectory(...)`.
Scope: `AnimaContext.cs` (add constructor param + implementation),
`AnimaServiceExtensions.cs` (pass animasRoot).

**Step 4 — PluginLoader DI injection**
Requires: Steps 1-3 complete (so injected services expose full v1.8 surface).
Unblocks: External modules receive all Contracts services.
Scope: `PluginLoader.cs` (add IServiceProvider param, replace Activator.CreateInstance).

**Step 5 — LLMModule messages port**
Requires: Step 1 (ChatMessageInput in Contracts).
Can be done in parallel with Steps 2-4.
Scope: `LLMModule.cs` (add port attribute, add subscription, add deserialization).

**Step 6 — ContextModule (external)**
Requires: Steps 1-5 complete.
Validates the entire v1.8 surface end-to-end.
Scope: New project `ContextModule/`.

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: ActivatorUtilities.CreateInstance for cross-context types
**What:** Using `ActivatorUtilities.CreateInstance(serviceProvider, pluginType)` directly.
**Why bad:** The plugin's `IModuleConfig` type loaded in `PluginLoadContext` has a different
identity than the host's `IModuleConfig`. The DI container cannot match them — throws at
runtime with `InvalidOperationException`.
**Instead:** Reflect constructor parameters by `FullName`, resolve from `IServiceProvider`
by interface name, invoke constructor via `ConstructorInfo.Invoke`. Same pattern as the
existing duck-typing for `ITickable`.

### Anti-Pattern 2: Adding PortType.MessageList to Contracts
**What:** New enum value for structured message payloads.
**Why bad:** Breaks port validation, editor rendering, and all existing port type switches
in WiringEngine, PortTypeValidator, and the SVG editor. Requires coordinated changes across
multiple components.
**Instead:** JSON-serialize `List<ChatMessageInput>` over existing `PortType.Text` port.
Convention documented in module metadata description field.

### Anti-Pattern 3: Moving ILLMService to Contracts in v1.8
**What:** Moving `ILLMService` alongside `ChatMessageInput` to Contracts.
**Why bad:** `ILLMService` exposes `IAsyncEnumerable<StreamingResult>` — streaming is a
Core concern. External modules should not call LLM directly; they compose via ports.
**Instead:** Only `ChatMessageInput` moves to Contracts. `ILLMService` stays in Core.

### Anti-Pattern 4: Storing conversation history in LLMModule
**What:** Adding history accumulation directly to `LLMModule`.
**Why bad:** LLMModule is a singleton shared across Animas (ANIMA-08). Per-Anima history
stored in a singleton creates cross-Anima contamination.
**Instead:** History lives in ContextModule (external, stateful per-Anima via DataDirectory).
LLMModule remains stateless — it receives a complete message list each call.

### Anti-Pattern 5: IModuleContext.DataDirectory as a property
**What:** `string DataDirectory { get; }` on `IModuleContext`.
**Why bad:** The path is module-specific. A property would return the same path for all
modules using the same context instance, or require the module to pass its own ID in a
separate call anyway.
**Instead:** `string GetDataDirectory(string moduleId)` as a method. The module passes
`Metadata.Name` and gets back a ready-to-use, already-created directory path.

---

## Scalability Considerations

| Concern | v1.8 (current) | Future |
|---------|----------------|--------|
| Module instance isolation | Global singleton (ANIMA-08 deferred) | Per-Anima instances when ANIMA-08 resolved |
| History storage | In-memory + DataDirectory JSON file | Could swap to SQLite per module |
| DI injection scope | IServiceProvider singleton services only | Per-Anima scoped services when ANIMA-08 resolved |
| Cross-context type matching | FullName string comparison | No change needed — already proven pattern |

---

## Sources

- Codebase direct analysis: `PluginLoader.cs`, `AnimaContext.cs`, `LLMModule.cs`,
  `AnimaServiceExtensions.cs`, `AnimaModuleConfigService.cs`, `AnimaRuntime.cs`,
  `IModuleContext.cs`, `IModuleConfig.cs`, `ILLMService.cs`, `PortModule.cs`,
  `WiringServiceExtensions.cs`, `AnimaRuntimeManager.cs`
- Confidence: HIGH — all findings from direct source inspection
- Known decisions from PROJECT.md: ANIMA-08 (global IEventBus singleton), ILLMService stays in Core

---

*Architecture research for: OpenAnima v1.8 SDK Runtime Parity*
*Researched: 2026-03-16*
