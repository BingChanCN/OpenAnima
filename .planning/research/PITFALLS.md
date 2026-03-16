# Pitfalls Research

**Domain:** .NET 8 Modular Runtime — SDK Runtime Parity (DI Injection, Structured Messages, Storage Paths)
**Researched:** 2026-03-16
**Confidence:** HIGH

## Context

This document covers pitfalls specific to v1.8: adding PluginLoader DI injection for external modules,
IModuleContext.DataDirectory per-Anima per-Module storage, LLMModule structured message list input,
and validating all of the above with a real external ContextModule.

The system already ships with (v1.7 baseline):
- PluginLoader using Activator.CreateInstance() with parameterless constructor — zero DI for external modules
- IModuleContext exposing only ActiveAnimaId and ActiveAnimaChanged — no DataDirectory
- LLMModule accepting single string prompt, building messages list internally
- ChatMessageInput record in OpenAnima.Core.LLM (not in Contracts)
- AnimaRuntimeManager.DeleteAsync deleting the Anima directory recursively
- AssemblyLoadContext isolation: PluginLoadContext falls back to Default context for unknown assemblies
- Name-based type comparison for IModule cross-context identity

The four pitfall domains for v1.8:

1. DI injection into ALC-isolated plugins — type identity, service lifetime, constructor resolution
2. WiringEngine string-to-structured data — backward compat with existing string-port modules
3. Storage paths — DataDirectory convention, cleanup on delete, path traversal
4. LLMModule multi-turn messages — token budget, context window overflow, history growth

---
## Critical Pitfalls

### Pitfall 1: DI Services Injected into Plugins Fail Due to Cross-Context Type Identity

**What goes wrong:**
PluginLoader currently uses Activator.CreateInstance(moduleType) with a parameterless constructor.
When DI injection is added, the natural approach is to pass an IServiceProvider to PluginLoader
and call ActivatorUtilities.CreateInstance(serviceProvider, moduleType). This fails silently or
throws InvalidOperationException because the moduleType was loaded in PluginLoadContext (an
isolated AssemblyLoadContext), while the IServiceProvider holds registrations against types loaded
in the Default context. The CLR treats IModuleContext from PluginLoadContext and IModuleContext
from Default context as different types — even though they have the same FullName. The DI
container cannot match the constructor parameter type to any registered service.

**Why it happens:**
PluginLoadContext.Load() returns null for OpenAnima.Contracts (falls back to Default context),
which means Contracts types ARE shared. However, if the plugin's DLL was compiled against a
different version of Contracts (or if the .deps.json resolution picks up a local copy), the
plugin's Contracts assembly loads into PluginLoadContext instead of falling back to Default.
The fallback only works when the resolver returns null — if the plugin ships its own copy of
OpenAnima.Contracts.dll, the resolver finds it and loads it into the plugin context, breaking
type identity for every Contracts interface.

**How to avoid:**
- In PluginLoadContext.Load(), explicitly return null for any assembly whose name starts with
  "OpenAnima.Contracts" — do not rely solely on the resolver returning null:
  if (assemblyName.Name == "OpenAnima.Contracts") return null;
- After loading the plugin assembly, verify that the IModuleContext type the plugin references
  is the same object as the Default context's IModuleContext:
  var pluginContractsRef = assembly.GetReferencedAssemblies()
      .FirstOrDefault(a => a.Name == "OpenAnima.Contracts");
  Assert pluginContractsRef resolves to the Default context's Contracts assembly.
- Use ActivatorUtilities.CreateInstance only after confirming type identity; fall back to
  manual constructor resolution via reflection if needed.
- Add a test: load a plugin that declares IModuleContext in its constructor; verify the
  injected instance is the same object as the host's IModuleContext singleton.

**Warning signs:**
- InvalidOperationException: "Unable to resolve service for type 'OpenAnima.Contracts.IModuleContext'"
  when ActivatorUtilities.CreateInstance is called with the plugin type
- Plugin loads successfully (IModule found, instantiated) but constructor parameters are null
- Two different Assembly objects for "OpenAnima.Contracts" visible in AppDomain.CurrentDomain.GetAssemblies()
- Plugin's module.json references a specific Contracts version that differs from the host

**Phase to address:**
PluginLoader DI Injection phase — explicit Contracts assembly exclusion in PluginLoadContext.Load()
must be the first change, before any service injection is attempted.

---

### Pitfall 2: Service Lifetime Mismatch — Singleton Services Injected into Per-Load Plugin Instances

**What goes wrong:**
IModuleContext, IModuleConfig, IEventBus, and ICrossAnimaRouter are all registered as singletons
in the host DI container. External modules are loaded once per PluginLoader.LoadModule() call and
stored in PluginRegistry. If a module is unloaded and reloaded (hot-reload), a new module instance
is created but the same singleton services are injected. This is correct for singletons. However,
if the module stores a reference to a service that is later disposed (e.g., if IEventBus is
replaced during Anima reset), the module holds a stale reference to a disposed service and throws
ObjectDisposedException on the next execution.

The inverse problem: if a module is designed to be per-Anima (one instance per Anima), but the
host registers it as a singleton and injects it once, all Animas share the same module instance
and its internal state (conversation history, pending prompts, etc.) bleeds across Animas.

**Why it happens:**
The current architecture keeps a global IEventBus singleton (ANIMA-08 tech debt). External modules
injected with this singleton will subscribe to events from ALL Animas, not just the one they
belong to. The module has no way to filter by Anima unless it uses IModuleContext.ActiveAnimaId —
but ActiveAnimaId is a global cursor, not a per-module scope.

**How to avoid:**
- Document clearly in SDK: external modules receive singleton services; they must use
  IModuleContext.ActiveAnimaId to scope their behavior per-Anima, not assume one instance per Anima
- For ContextModule specifically: history must be keyed by animaId (Dictionary<string, List<...>>),
  not stored as a flat list field
- Do NOT inject per-Anima scoped services into plugins via the singleton DI container — there is
  no scoped lifetime for plugins in this architecture
- Add a test: two Animas active simultaneously; ContextModule receives a message for Anima A;
  verify Anima B's history is not affected

**Warning signs:**
- ContextModule conversation history growing with messages from multiple Animas mixed together
- ObjectDisposedException from IEventBus after Anima deletion and recreation
- Module state (e.g., history list) not resetting when switching between Animas

**Phase to address:**
PluginLoader DI Injection phase — document the singleton-only constraint before ContextModule
is written; ContextModule author must key all state by animaId from the start.

---

### Pitfall 3: ActivatorUtilities.CreateInstance Fails on Optional Constructor Parameters

**What goes wrong:**
LLMModule's constructor has ICrossAnimaRouter? router = null as an optional parameter.
ActivatorUtilities.CreateInstance resolves ALL constructor parameters from DI, including optional
ones. If ICrossAnimaRouter is not registered (or registered as null), ActivatorUtilities throws
InvalidOperationException rather than using the default null value. This is different from
Activator.CreateInstance behavior and different from how ASP.NET Core DI handles optional
parameters in controllers (which uses GetService, not GetRequiredService, for optional params).

For external modules, if the module author declares an optional service parameter that is not
registered in the host, the entire module fails to load — with a confusing error that says
"unable to resolve" rather than "parameter is optional, using default."

**Why it happens:**
ActivatorUtilities.CreateInstance uses a greedy constructor selection algorithm. It picks the
constructor with the most parameters it can satisfy. Optional parameters with defaults are not
treated as "optional" by ActivatorUtilities — they are treated as required unless the caller
explicitly handles them. The behavior differs from standard DI container resolution.

**How to avoid:**
- Use a custom instantiation helper that mirrors ASP.NET Core's controller activation:
  for each constructor parameter, call sp.GetService(paramType) (not GetRequiredService);
  if null and parameter has a default value, use the default; otherwise fail with a clear error
- Alternatively, use ActivatorUtilities.CreateFactory to pre-compile the factory and handle
  optional parameters explicitly
- Document in SDK: optional constructor parameters in external modules must have a default value
  of null; the host will pass null if the service is not registered
- Add a test: load a plugin with an optional ICrossAnimaRouter? parameter; verify it loads
  successfully when ICrossAnimaRouter is not registered

**Warning signs:**
- InvalidOperationException: "No service for type 'OpenAnima.Contracts.Routing.ICrossAnimaRouter'"
  when loading a module that declares it as optional
- Module loads in unit tests (which mock all services) but fails in production (where some
  optional services may not be registered)
- PluginLoader error log showing "constructor resolution failed" for a module that compiles cleanly

**Phase to address:**
PluginLoader DI Injection phase — implement the custom instantiation helper before testing with
any module that has optional constructor parameters.

---

### Pitfall 4: WiringEngine Port Subscription Breaks When LLMModule Input Changes from string to IReadOnlyList

**What goes wrong:**
WiringEngine.CreateRoutingSubscription uses a switch on PortType to create typed EventBus
subscriptions: PortType.Text subscribes as Subscribe<string>. If LLMModule's "prompt" input port
changes from accepting string to accepting IReadOnlyList<ChatMessageInput>, the WiringEngine
subscription for that port must also change to Subscribe<IReadOnlyList<ChatMessageInput>>. But
WiringEngine only knows about PortType (Text, Trigger) — it has no knowledge of the CLR type
behind a port. All Text ports are treated as string. A module that publishes
IReadOnlyList<ChatMessageInput> to a Text port will have its payload silently dropped because
the WiringEngine subscription is typed to string, not the list type.

**Why it happens:**
The port type system (PortType enum) is a semantic category, not a CLR type. PortType.Text means
"text data" but the WiringEngine hardcodes string as the CLR type for all Text ports. This worked
when all Text ports carried strings. Adding a structured type to a Text port breaks the implicit
contract without any compile-time error.

**How to avoid:**
- Do NOT change LLMModule's "prompt" input port CLR type from string to a structured type.
  Instead, keep the port as string and have LLMModule internally parse the structured format,
  OR add a NEW port (e.g., "messages" of a new PortType.MessageList) alongside the existing
  "prompt" port for backward compatibility.
- The cleanest approach for v1.8: LLMModule adds a second input port "messages" that accepts
  a serialized JSON string of ChatMessageInput[]; ContextModule serializes its history to JSON
  and publishes to "messages"; LLMModule deserializes internally. This keeps all ports as string
  and avoids WiringEngine changes.
- If a new PortType is added (e.g., PortType.MessageList), WiringEngine.CreateRoutingSubscription
  must be updated to handle it with the correct CLR type.
- Add a test: wire ContextModule.messages_out -> LLMModule.messages; verify the payload arrives
  correctly typed at LLMModule.

**Warning signs:**
- LLMModule receives null or empty payload on the "messages" port despite ContextModule publishing
- WiringEngine logs show "subscription created for Text port" but LLMModule never fires
- EventBus publish count for "ContextModule.port.messages" > 0 but LLMModule execution count = 0
- No compile error when changing port CLR type — the mismatch is entirely runtime

**Phase to address:**
LLMModule Structured Input phase — decide the port strategy (new port vs. JSON-in-string vs.
new PortType) before writing any code; the decision affects WiringEngine, ContextModule, and
LLMModule simultaneously.

---

### Pitfall 5: ChatMessageInput Lives in OpenAnima.Core.LLM — External Modules Cannot Reference It

**What goes wrong:**
ChatMessageInput is defined in OpenAnima.Core.LLM namespace, inside the Core project. External
modules (ContextModule) that need to build a list of ChatMessageInput records cannot reference
this type without taking a dependency on OpenAnima.Core — which is the host runtime, not the
SDK contract. This creates a circular dependency: external module depends on Core, Core loads
external modules.

Even if the external module references Core as a NuGet package (not a project reference), the
type identity problem from Pitfall 1 reappears: Core is loaded in the Default context, but if
the module ships its own copy of Core.dll, the ChatMessageInput type from the module's copy is
a different CLR type than the one the host uses.

**Why it happens:**
ChatMessageInput was placed in Core.LLM because it was only used by LLMService and LLMModule,
both of which are Core-internal. The v1.8 requirement to have an external ContextModule produce
ChatMessageInput lists exposes this placement as wrong — it should be in Contracts.

**How to avoid:**
- Move ChatMessageInput (and LLMResult, StreamingResult) from OpenAnima.Core.LLM to
  OpenAnima.Contracts before writing ContextModule
- Add a type-forward shim in Core.LLM: using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput;
  so existing Core code compiles without changes
- ILLMService interface must also move to Contracts (or a new ILLMService in Contracts that
  uses the Contracts ChatMessageInput) — otherwise external modules cannot call ILLMService
- Verify: ContextModule project references only OpenAnima.Contracts, not OpenAnima.Core

**Warning signs:**
- ContextModule.csproj has a ProjectReference to OpenAnima.Core
- Build error in ContextModule: "The type 'ChatMessageInput' is defined in an assembly that is
  not referenced"
- Two different ChatMessageInput types visible at runtime (one from Core, one from Contracts)
- LLMModule receiving a payload typed as Contracts.ChatMessageInput but expecting Core.LLM.ChatMessageInput

**Phase to address:**
LLMModule Structured Input phase — ChatMessageInput must move to Contracts as the first step,
before any structured message passing is implemented.

---

### Pitfall 6: DataDirectory Path Traversal — Module Can Escape Its Sandbox

**What goes wrong:**
IModuleContext.DataDirectory returns a path like:
  {animasRoot}/{animaId}/modules/{moduleId}/
If the moduleId is derived from user input or module manifest without sanitization, a malicious
or buggy module.json could set id to "../../../system" and escape the Anima directory. Even
without malicious intent, a module that uses Path.Combine(DataDirectory, userInput) without
validation can write files outside its designated directory.

**Why it happens:**
Path.Combine on Windows does not sanitize ".." segments. A path like:
  Path.Combine("data/animas/abc123/modules/mymod", "../../config.json")
resolves to "data/animas/abc123/config.json" — outside the module's directory but still within
the Anima directory. With a deeper traversal, it can reach anywhere on the filesystem.

**How to avoid:**
- When computing DataDirectory, normalize the full path and assert it starts with animasRoot:
  var fullPath = Path.GetFullPath(Path.Combine(animasRoot, animaId, "modules", moduleId));
  if (!fullPath.StartsWith(Path.GetFullPath(animasRoot)))
      throw new SecurityException("Module data directory escapes animas root");
- Sanitize moduleId before using it as a path component: allow only alphanumeric, hyphen, dot;
  reject any moduleId containing path separators or ".."
- Document in SDK: DataDirectory is a pre-created, pre-validated path; modules must not
  construct sub-paths using user-controlled strings without their own validation
- Add a test: moduleId = "../escape"; verify DataDirectory computation throws or returns a
  safe path within animasRoot

**Warning signs:**
- module.json id field containing "/" or "" or ".."
- DataDirectory path containing ".." segments after Path.Combine
- Files appearing outside the expected {animasRoot}/{animaId}/modules/ tree

**Phase to address:**
DataDirectory Storage phase — path validation must be implemented before DataDirectory is
exposed in IModuleContext; never expose an unvalidated path.

---

### Pitfall 7: DataDirectory Not Cleaned Up on Anima Delete or Module Uninstall

**What goes wrong:**
AnimaRuntimeManager.DeleteAsync deletes the Anima directory recursively:
  Directory.Delete(dir, recursive: true)
where dir = {animasRoot}/{animaId}. If DataDirectory is {animasRoot}/{animaId}/modules/{moduleId}/,
it IS inside the Anima directory and WILL be deleted on Anima delete. This is correct behavior.

However, if DataDirectory is placed outside the Anima directory (e.g., a shared data root like
{appRoot}/module-data/{moduleId}/), it will NOT be cleaned up on Anima delete. The orphaned
data accumulates indefinitely. Similarly, if a module is "uninstalled" (future MODMGMT-03),
its per-Anima data directories across all Animas must be cleaned up — but there is no current
mechanism to enumerate all Animas and delete their module-specific subdirectories.

**Why it happens:**
The natural temptation is to put module data in a flat structure keyed only by moduleId (not
animaId) to make it easy for the module to find its data regardless of which Anima is active.
This breaks the per-Anima isolation guarantee and makes cleanup impossible without a registry.

**How to avoid:**
- DataDirectory MUST be nested inside the Anima directory:
  {animasRoot}/{animaId}/modules/{moduleId}/
  This ensures Anima delete automatically cleans up all module data for that Anima.
- For module uninstall (future): enumerate all Anima directories and delete the module's
  subdirectory from each; this is a future concern but the directory structure must support it
- Create the DataDirectory on first access (lazy creation), not at module load time — avoids
  creating empty directories for modules that never write data
- Add a test: create an Anima, have ContextModule write to DataDirectory, delete the Anima,
  verify the DataDirectory no longer exists

**Warning signs:**
- DataDirectory path not containing the animaId segment
- Module data directories surviving Anima deletion
- {appRoot}/module-data/ directory growing unboundedly across Anima create/delete cycles

**Phase to address:**
DataDirectory Storage phase — directory structure decision is the first design choice; must be
{animasRoot}/{animaId}/modules/{moduleId}/ from the start.

---

### Pitfall 8: ContextModule History Grows Without Bound — Context Window Overflow

**What goes wrong:**
ContextModule maintains a conversation history list. On each heartbeat tick (or chat message),
it appends to the list and passes the full list to LLMModule. With no eviction policy, the list
grows indefinitely. After enough turns, the total token count of the history exceeds the LLM's
context window. The LLM API returns a 400 error (context_length_exceeded) or silently truncates
the input. The module has no way to know which messages were dropped, and the conversation
becomes incoherent.

The existing ChatContextManager tracks token counts for the chat UI but is not accessible to
external modules (it lives in Core.Services). ContextModule has no token counting capability
unless it implements its own.

**Why it happens:**
Conversation history management is a solved problem in chat UIs (the existing ChatContextManager
handles it for the built-in chat), but external modules have no access to token counting
utilities. The natural first implementation of ContextModule is a simple List<ChatMessageInput>
with no eviction — it works for short conversations and fails silently for long ones.

**How to avoid:**
- ContextModule must implement a sliding window eviction policy: keep the system message (if any)
  plus the N most recent turns that fit within a configurable token budget
- Token counting: either expose a token counting utility in Contracts (ITokenCounter interface),
  or have ContextModule use a simple heuristic (4 chars per token) for budget estimation
- The configurable budget should default to a safe value (e.g., 80% of a 4096-token context)
  and be overridable via IModuleConfig
- Add a test: ContextModule with 200 turns of history; verify the messages list passed to
  LLMModule never exceeds the configured token budget

**Warning signs:**
- LLM API returning 400 context_length_exceeded after many conversation turns
- ContextModule history list length growing without bound in long-running tests
- LLM responses becoming incoherent or referencing very old context that should have been evicted
- No token counting logic in ContextModule implementation

**Phase to address:**
External ContextModule phase — eviction policy must be part of the initial ContextModule design,
not added as a fix after context overflow is observed.

---

### Pitfall 9: ContextModule Persisting History to DataDirectory Blocks the EventBus Callback

**What goes wrong:**
ContextModule subscribes to chat events via EventBus and appends to history. If it also persists
history to DataDirectory on every message (for crash recovery), the file I/O happens inside the
EventBus callback. EventBus callbacks are awaited via Task.WhenAll in the heartbeat tick. Slow
file I/O (especially on Windows with antivirus scanning) can cause the tick to exceed 100ms,
triggering the tick-skip guard and dropping subsequent ticks.

**Why it happens:**
The natural implementation is: receive message -> append to history -> save to disk -> pass to LLM.
All three steps happen synchronously in the callback. File I/O is the bottleneck.

**How to avoid:**
- Separate history persistence from history update: update the in-memory list synchronously,
  then fire-and-forget the disk write (or use a background Channel<T> for persistence)
- For v1.8, persistence is not required — history can be in-memory only (lost on restart);
  add persistence only if explicitly required
- If persistence is added, use async file I/O with ConfigureAwait(false) and do not await it
  inside the EventBus callback; use a dedicated persistence channel

**Warning signs:**
- Heartbeat tick latency > 100ms after ContextModule is loaded
- HeartbeatLoop SkippedCount increasing after ContextModule starts persisting history
- File I/O appearing in EventBus callback stack traces in profiler

**Phase to address:**
External ContextModule phase — decide upfront whether v1.8 requires persistence; if not,
explicitly defer it and document the decision.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Keep ChatMessageInput in Core.LLM, have ContextModule reference Core | Avoids Contracts change | External modules depend on Core; type identity breaks across ALC | Never |
| Use Activator.CreateInstance with property injection for DI | Avoids constructor resolution complexity | Modules can silently miss services; no compile-time safety | Never for required services |
| DataDirectory outside Anima directory (flat by moduleId) | Simpler path construction | Orphaned data on Anima delete; no per-Anima isolation | Never |
| No token eviction in ContextModule v1 | Faster to implement | Context overflow after ~20 turns; silent LLM errors | Acceptable only if max history is capped at a small fixed number (e.g., 10 turns) |
| Rely on PluginLoadContext fallback for Contracts assembly sharing | No explicit exclusion needed | If plugin ships its own Contracts.dll, type identity breaks silently | Never — explicit exclusion is 1 line |
| Store ContextModule history as flat List without animaId key | Simpler code | History bleeds across Animas; all Animas share one history | Never |
| Skip DataDirectory path traversal validation | Faster to implement | Module can write outside its sandbox | Never |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| PluginLoader + DI | ActivatorUtilities.CreateInstance without explicit Contracts exclusion in ALC | Exclude "OpenAnima.Contracts" in PluginLoadContext.Load() before attempting DI injection |
| ContextModule + IModuleContext | Using ActiveAnimaId as a field key without null check | ActiveAnimaId is guaranteed non-null at module use time (per IModuleContext contract) but key all state by animaId from the start |
| LLMModule + structured messages | Changing "prompt" port CLR type from string | Add a new "messages" port; keep "prompt" port as string for backward compat |
| DataDirectory + Path.Combine | Path.Combine(DataDirectory, userInput) without validation | Validate that the resulting path starts with DataDirectory after Path.GetFullPath |
| ContextModule + EventBus | Awaiting file I/O inside EventBus callback | Fire-and-forget persistence; keep callback fast |
| ChatMessageInput + Contracts | Referencing Core.LLM.ChatMessageInput from external module | Move ChatMessageInput to Contracts first; external modules reference only Contracts |
| Module uninstall + DataDirectory | Assuming Anima delete cleans up module data | It does IF DataDirectory is inside the Anima directory; verify the path structure |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| ContextModule history unbounded growth | LLM API 400 errors after many turns; incoherent responses | Sliding window eviction with token budget | After ~20-50 turns depending on message length |
| File I/O in EventBus callback for history persistence | Tick latency > 100ms; SkippedCount growing | Background Channel for persistence; in-memory only for v1.8 | On first file write if antivirus is active |
| ActivatorUtilities.CreateInstance with many optional params | Module load time increases; confusing errors | Custom instantiation helper with GetService for optional params | With 3+ optional constructor parameters |
| ContextModule passing full history on every tick | Unnecessary LLM calls on heartbeat ticks | Only pass history when a new message arrives; gate on chat event, not heartbeat | With heartbeat at 100ms and history > 10 messages |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| DataDirectory path not validated against animasRoot | Module writes outside its sandbox to arbitrary filesystem paths | Path.GetFullPath normalization + StartsWith(animasRoot) check |
| moduleId used as path component without sanitization | Path traversal via "../" in module.json id field | Allowlist validation: alphanumeric, hyphen, dot only; max 64 chars |
| IModuleConfig.GetConfig exposed to external modules with full animaId+moduleId scope | Module reads config of other modules or other Animas | Scope the Contracts-facing API: module receives a pre-scoped IModuleConfig that only exposes its own config |
| ContextModule persisting conversation history to disk unencrypted | Sensitive conversation data readable by any process | Document: DataDirectory is unencrypted local storage; users must be aware; encryption is out of scope for v1.8 |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| ContextModule history eviction silently drops old messages | User references old context; LLM has no memory of it | Log eviction events; optionally show "context trimmed" indicator in chat UI |
| DataDirectory created eagerly for all modules on load | Empty directories for every module even if never used | Create DataDirectory lazily on first access |
| LLMModule "messages" port not visible in editor palette | User cannot wire ContextModule to LLMModule visually | Ensure new port is declared with InputPortAttribute so PortDiscovery finds it |

## "Looks Done But Isn't" Checklist

- [ ] **PluginLoader DI injection:** Looks done when a module with IModuleContext constructor loads.
  Verify: load a plugin that ships its own copy of OpenAnima.Contracts.dll; confirm it still
  receives the host's IModuleContext singleton (not a new instance from the plugin's Contracts copy).
- [ ] **ChatMessageInput in Contracts:** Looks done when ContextModule compiles. Verify: ContextModule
  project has zero references to OpenAnima.Core; only OpenAnima.Contracts is referenced.
- [ ] **DataDirectory path safety:** Looks done when the path is returned. Verify: moduleId = "../escape";
  confirm DataDirectory computation throws SecurityException or returns a safe path.
- [ ] **DataDirectory cleanup on Anima delete:** Looks done when DeleteAsync runs. Verify: create Anima,
  write a file to DataDirectory, delete Anima, assert the file no longer exists.
- [ ] **ContextModule history isolation:** Looks done when one Anima works. Verify: two Animas active;
  send messages to Anima A; assert Anima B's ContextModule history is empty.
- [ ] **Token budget eviction:** Looks done when short conversations work. Verify: 100-turn conversation;
  assert messages list passed to LLMModule never exceeds configured token budget.
- [ ] **Backward compat — existing string prompt port:** Looks done when LLMModule compiles. Verify:
  wire a FixedTextModule to LLMModule.prompt; confirm it still works after structured input is added.
- [ ] **Optional constructor params in plugins:** Looks done when a module with required params loads.
  Verify: load a plugin with ICrossAnimaRouter? router = null; confirm it loads when ICrossAnimaRouter
  is not registered.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Contracts assembly loaded in plugin context (type identity break) | MEDIUM | Add explicit exclusion in PluginLoadContext.Load(); reload all plugins; verify with assembly identity test |
| ChatMessageInput in wrong assembly | HIGH | Move to Contracts; add type-forward shim in Core.LLM; recompile all modules; test round-trip |
| DataDirectory path traversal | LOW | Add Path.GetFullPath + StartsWith validation; add test; no data migration needed |
| ContextModule history bleeding across Animas | MEDIUM | Refactor history storage to Dictionary<string, List<...>> keyed by animaId; existing history lost |
| Context window overflow | LOW | Add sliding window eviction; configure token budget; existing history truncated on next load |
| File I/O blocking EventBus callback | MEDIUM | Extract persistence to background Channel; test tick latency after change |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Contracts assembly in plugin context | Phase 1: PluginLoader DI | Plugin-ships-own-Contracts test: host singleton received, not new instance |
| Optional constructor params | Phase 1: PluginLoader DI | Optional-param test: module loads without ICrossAnimaRouter registered |
| Singleton lifetime + per-Anima state | Phase 1: PluginLoader DI | Two-Anima isolation test: history does not bleed |
| ChatMessageInput in Core.LLM | Phase 2: LLMModule Structured Input | ContextModule zero-Core-reference build check |
| WiringEngine string-only port routing | Phase 2: LLMModule Structured Input | Backward compat test: FixedTextModule -> LLMModule.prompt still works |
| DataDirectory path traversal | Phase 3: DataDirectory Storage | Path traversal test: moduleId="../escape" throws or is sanitized |
| DataDirectory cleanup on delete | Phase 3: DataDirectory Storage | Delete-Anima test: DataDirectory files removed |
| ContextModule history unbounded | Phase 4: External ContextModule | 100-turn test: messages list within token budget |
| File I/O in EventBus callback | Phase 4: External ContextModule | Tick latency test: < 100ms with ContextModule loaded and active |

## Sources

- OpenAnima source code — PluginLoader, PluginLoadContext, AnimaRuntimeManager, LLMModule,
  WiringEngine, IModuleContext, IModuleConfig, ChatContextManager, TokenCounter (HIGH confidence)
- OpenAnima PROJECT.md — v1.8 requirements, known tech debt, key decisions (HIGH confidence)
- Microsoft Docs — AssemblyLoadContext: https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext
  (HIGH confidence — explicit fallback behavior documented)
- Microsoft Docs — ActivatorUtilities: https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.activatorutilities
  (HIGH confidence — optional parameter behavior documented)
- Microsoft Docs — Path.GetFullPath for path traversal prevention:
  https://learn.microsoft.com/en-us/dotnet/standard/io/file-path-formats (HIGH confidence)
- .NET Runtime GitHub — AssemblyLoadContext isolation patterns:
  https://github.com/dotnet/runtime/blob/main/docs/design/features/assemblyloadcontext.md
  (HIGH confidence)
- OpenAI API docs — context_length_exceeded error:
  https://platform.openai.com/docs/guides/error-codes (HIGH confidence)

---
*Pitfalls research for: SDK Runtime Parity (DI Injection, Structured Messages, Storage Paths) — OpenAnima v1.8*
*Researched: 2026-03-16*
