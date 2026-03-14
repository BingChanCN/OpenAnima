# Project Research Summary

**Project:** OpenAnima v1.7 Runtime Foundation
**Domain:** .NET 8 Modular AI Agent Runtime — Concurrency, Plugin API, Module Decoupling
**Researched:** 2026-03-14
**Confidence:** HIGH

## Executive Summary

OpenAnima v1.7 is a runtime hardening and plugin API expansion milestone for an in-process, single-user Blazor Server AI agent runtime. The three work streams are tightly coupled: two known race conditions (LLMModule `_pendingPrompt` field, WiringEngine `_failedModules` HashSet) must be fixed before introducing an Activity Channel concurrency model, which itself is a prerequisite for the Contracts API expansion that enables full built-in module decoupling. Research confirms that all required primitives ship in the .NET 8 BCL — `Channel<T>`, `SemaphoreSlim`, `CancellationToken` — with zero new NuGet dependencies required.

The recommended approach treats the three work streams as four sequential phases after a mandatory pre-flight: (0) clean the test baseline, (1) fix the two pre-existing race conditions, (2) introduce the `ActivityChannel` component that serializes per-Anima execution, and (3+4) expand the Contracts API surface and mechanically decouple the 14 built-in modules. The Activity Channel pattern — a single `Channel<ActivityRequest>` consumer per `AnimaRuntime` — is the idiomatic .NET 8 actor-mailbox pattern. It eliminates all intra-Anima races without adding synchronization inside module code. Cross-Anima parallelism is preserved because each Anima gets its own channel and consumer Task.

The dominant risk is the Contracts API expansion: moving interfaces between assemblies is a binary breaking change that silently breaks externally-compiled `.oamod` packages. The mitigation is mandatory compatibility forwarding in the old Core namespaces before any interface move ships, combined with a canary `.oamod` round-trip test on every interface-move commit. A secondary risk is the three pre-existing test failures caused by the global `IEventBus` singleton (ANIMA-08 tech debt) — if left unresolved, concurrency changes will amplify them into indistinguishable regressions. Fix the baseline first.

---

## Key Findings

### Recommended Stack

No new NuGet packages are needed for v1.7. Every required primitive is already in the .NET 8 BCL. The work is purely architectural: introducing `Channel<T>` at the scheduling layer, moving interfaces from `Core` to `Contracts`, and replacing `using OpenAnima.Core.*` with `using OpenAnima.Contracts` in 14 module files. The existing v1.6 stack (Blazor Server, OpenAI SDK 2.8.0, SharpToken, Markdig, System.CommandLine, Microsoft.Extensions.Http.Resilience) is unchanged.

**Core technologies:**
- `System.Threading.Channels.Channel<T>` (BCL): per-Anima activity serialization — the canonical .NET async mailbox pattern with `SingleReader = true` consumer loop and lock-free `TryWrite` from producers
- `System.Threading.SemaphoreSlim` (BCL): already used in `HeartbeatLoop`; extend the same `WaitAsync(0)` skip-tick pattern to stateless Anima request gating
- `OpenAnima.Contracts` interface additions: `IModuleContext`, `IAnimaModuleConfigService` (moved), `ICrossAnimaRouter` (moved), `ILLMService` (moved), `ISsrfGuard` (new) — no implementation changes, only interface location changes
- `ConcurrentDictionary<string, byte>` (BCL): replace `HashSet<string>` in `WiringEngine._failedModules` — single-line fix for the parallel task write race

### Expected Features

**Must have (v1.7 launch):**
- Race-free module execution — per-module `SemaphoreSlim(1,1)` skip guard preventing `_state`/`_pendingPrompt` corruption
- `WiringEngine._failedModules` thread safety — `ConcurrentDictionary` replacing `HashSet`
- `ActivityChannel` component — single consumer per `AnimaRuntime` serializing HeartbeatTick, UserMessage, and IncomingRoute activities
- `IModuleContext` in Contracts — immutable `AnimaId` set at module init, replacing the UI-state `IAnimaContext` in modules
- `IAnimaModuleConfigService` moved to Contracts — enables all 9 config-dependent modules to drop Core.Services dependency
- All 14 built-in modules decoupled from `OpenAnima.Core.*` — only `OpenAnima.Contracts` references in module files
- Module project template (`oani new`) updated to reflect Contracts-only dependency
- Module management UI — install, uninstall, list, search (MODMGMT-01/02/03/06)
- Clean test baseline — 3 pre-existing failures resolved before concurrency work begins

**Should have (v1.x after stabilization):**
- `ICrossAnimaRouter` moved to Contracts — enables external routing modules; defer until decoupling of simpler modules is stable
- `ILLMService` and `ChatMessageInput` moved to Contracts — enables external LLM-capable modules; depends on also moving `ChatMessageInput`
- `IModuleConfigSchema` interface — external modules declare config fields; platform auto-renders sidebar
- `IModuleLifecycle` context object — convenience DI wrapper reducing 4+ constructor params to 1
- Named Activity Channels (heartbeat vs. chat) — two parallel tracks per stateful Anima

**Defer (v2+):**
- Per-Anima module instances (ANIMA-08 resolution) — requires full DI restructure to replace the global `IEventBus` singleton
- `IUrlValidator` / `ISsrfGuard` in Contracts — SSRF protection abstracted for external HTTP modules
- Auto-rendered config sidebar — replace per-module Razor components with generic `<AutoConfigSidebar>`

### Architecture Approach

The v1.7 architecture inserts one new component (`ActivityChannel`) between the HeartbeatLoop and WiringEngine, moves a set of interfaces from `Core` to `Contracts`, and updates module `using` directives. No existing component interfaces change. The `ActivityChannel` is a per-`AnimaRuntime` `Channel<ActivityRequest>` with a single consumer Task: heartbeat ticks, user messages, and cross-Anima route deliveries all enter through `TryPost(ActivityRequest)` and are processed serially. WiringEngine, EventBus, and PluginLoader are untouched.

**Major components after v1.7:**
1. `ActivityChannel` (new, `Core/Runtime/`) — owns `Channel<ActivityRequest>` + consumer Task; started/stopped alongside `HeartbeatLoop`; receives `HeartbeatTickActivity`, `UserMessageActivity`, `IncomingRouteActivity`; calls `WiringEngine.ExecuteAsync` and `EventBus.PublishAsync` from the serial consumer loop
2. `AnimaRuntime` (modified) — gains `ActivityChannel` property; `HeartbeatLoop.ExecuteTickAsync` enqueues instead of calling WiringEngine directly; `CrossAnimaRouter.RouteRequestAsync` enqueues instead of publishing directly to EventBus
3. `OpenAnima.Contracts` (expanded) — gains `ActivityRequest` hierarchy, `IModuleContext`, `IAnimaModuleConfigService`, `ICrossAnimaRouter` + supporting DTOs, `ILLMService` + `ChatMessageInput`, `ISsrfGuard`; all existing interfaces (`IModule`, `IEventBus`, `ITickable`, etc.) are unchanged

**Key patterns:**
- Mailbox/Channel-per-Anima: all state-mutating work for an Anima flows through one channel; eliminates intra-Anima races without module-level locks
- Interface Promotion: move interface to Contracts without moving implementation; DI registration stays `AddSingleton<IContractsInterface, CoreImpl>()`
- Execution-Scoped Identity: `IModuleContext.AnimaId` replaces `IAnimaContext.ActiveAnimaId` in modules; set once at init via property injection; never changes

### Critical Pitfalls

1. **Activity Channel deadlock from bounded channel + WriteAsync in tick path** — use `Channel.CreateUnbounded<T>()` exclusively; always `TryWrite` (never `WriteAsync`) from HeartbeatLoop tick path; HeartbeatLoop's existing `_tickLock` skip guard is the backpressure mechanism
2. **Interface move is a binary breaking change** — moving `IAnimaContext` or `IAnimaModuleConfigService` to a different assembly silently breaks `.oamod` packages compiled against old Core; keep type-forward alias in old namespace; run canary `.oamod` round-trip test on every interface-move commit
3. **Pre-existing 3 test failures amplified by concurrency changes** — document all 3 failing test names before starting v1.7; fix them (ANIMA-08 global singleton root cause) before any Activity Channel work; any 4th failure after concurrency changes is a new regression
4. **DI registration fails silently after module decoupling** — module constructor type mismatch shows as runtime `InvalidOperationException`, not compile error; migrate one module at a time, run integration tests after each, add startup smoke test that resolves all 14 module types
5. **Blazor Server SynchronizationContext corruption** — EventBus and ActivityChannel callbacks fire on ThreadPool threads; all Blazor component subscriptions must use `await InvokeAsync(StateHasChanged)` not bare `StateHasChanged()`; audit all components when ActivityChannel events are wired to UI state

---

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 0: Pre-flight — Test Baseline
**Rationale:** Three pre-existing test failures are caused by ANIMA-08 (global `IEventBus` singleton). Concurrency changes will make order-sensitive tests indistinguishable from regressions unless the baseline is clean. This is a blocking prerequisite for all other v1.7 work.
**Delivers:** Full test suite at 0 failures; documented ANIMA-08 root cause; known-flaky test trait annotations removed
**Addresses:** Pitfall 10 (pre-existing failures amplified by concurrency changes)
**Avoids:** Inability to distinguish regression from pre-existing debt during Activity Channel work

### Phase 1: Concurrency Fixes
**Rationale:** Fix the two known races before introducing the Activity Channel model. The channel model would hide the `_pendingPrompt` symptom while leaving the root cause. Both fixes are mechanical (1-3 line changes) with immediate test coverage.
**Delivers:** Race-free `WiringEngine._failedModules` (ConcurrentDictionary); race-free `LLMModule._pendingPrompt` and `ConditionalBranchModule._pendingInput` (local capture or Channel<string>); `volatile` / `Interlocked.Exchange` on trigger-buffer fields
**Addresses:** CONC-01 (race conditions on shared mutable fields)
**Avoids:** Pitfall 1 (pendingPrompt race), Pitfall 2 (WiringEngine HashSet race)
**Research flag:** Standard patterns — skip `/gsd:research-phase`

### Phase 2: Activity Channel Model
**Rationale:** With a clean concurrency baseline, introduce the `ActivityChannel` component. This is the highest-architectural-impact change in v1.7 and should be isolated in its own phase so any integration failures are attributable.
**Delivers:** `ActivityRequest` hierarchy in Contracts; `ActivityChannel` class in `Core/Runtime/`; `AnimaRuntime` gains `ActivityChannel` property; `HeartbeatLoop.ExecuteTickAsync` enqueues instead of calling WiringEngine directly; `CrossAnimaRouter.RouteRequestAsync` enqueues `IncomingRouteActivity`; `ChatInputModule.SendMessageAsync` enqueues `UserMessageActivity`; 10-second soak test confirming no deadlock
**Addresses:** CONC-02/CONC-03 (stateless and stateful Anima execution safety)
**Uses:** `Channel.CreateUnbounded<T>()` with `SingleReader = true`; `Task.Run(() => RunConsumerAsync(ct))`; `CancellationTokenSource.CreateLinkedTokenSource`
**Avoids:** Pitfall 3 (Activity Channel deadlock), Pitfall 4 (Blazor SynchronizationContext — audit components in this phase)
**Research flag:** Architecture is fully specified in ARCHITECTURE.md — skip `/gsd:research-phase`; however, verify Blazor component audit scope before committing

### Phase 3: Contracts API Expansion
**Rationale:** Once the ActivityChannel is stable, expand the Contracts surface. This enables external plugins to access config, identity, routing, and LLM services without referencing Core — the prerequisite for Phase 4's module decoupling. Interface moves are binary breaking; compatibility shims must ship in the same commit as the move.
**Delivers:** `IModuleContext` in Contracts; `IAnimaModuleConfigService` moved to Contracts with type-forward in `Core.Services`; `ICrossAnimaRouter` + supporting DTOs moved to Contracts; `ILLMService` + `ChatMessageInput` moved to Contracts; `ISsrfGuard` new in Contracts; all Core implementations updated to implement new Contracts interfaces; DI registrations updated; canary `.oamod` round-trip test passing
**Addresses:** API-01 (Contracts API surface), API-02 (external module parity)
**Avoids:** Pitfall 5 (binary breaking interface move), Pitfall 6 (adding members to IEventBus/IModule), Pitfall 7 (circular dependency in Contracts)
**Research flag:** Binary compatibility rules are well-documented — skip `/gsd:research-phase`; CI enforcement of zero ProjectReferences in Contracts is the key guard

### Phase 4: Built-in Module Decoupling
**Rationale:** With Contracts containing all required interfaces, the mechanical decoupling of 14 modules is unblocked. Migrate in dependency order (simplest first) and validate DI resolution after each module — never batch-commit.
**Delivers:** All 14 modules using only `OpenAnima.Contracts` — zero `using OpenAnima.Core.*`; `IAnimaContext` constructor params replaced with `IModuleContext`; DI registrations updated; startup smoke test resolves all 14 module types; `oani new` template updated; `dotnet build OpenAnima.Contracts` in isolation succeeds; `WeakReference<IModule>` unload test passing
**Migration order:** ChatInputModule, ChatOutputModule, HeartbeatModule → FixedTextModule, TextJoinModule, TextSplitModule, ConditionalBranchModule → AnimaInputPortModule, AnimaOutputPortModule, AnimaRouteModule → LLMModule, HttpRequestModule; FormatDetector already clean
**Addresses:** DECPL-01 (module decoupling)
**Avoids:** Pitfall 8 (DI registration break), Pitfall 9 (AssemblyLoadContext unload failure), Pitfall 11 (EventBus subscription lifecycle leak)
**Research flag:** Standard refactor pattern — skip `/gsd:research-phase`

### Phase 5: Module Management UI
**Rationale:** With the plugin API hardened by Phases 3-4, the module management UI (install/uninstall/search via `oani` CLI and editor UI) can be built on a stable foundation. Decoupled modules mean the install/unload paths are clean.
**Delivers:** `oani module install/uninstall/list/search` commands (MODMGMT-01/02/03/06); editor UI for module lifecycle; hot-reload paths with proper `ShutdownAsync` call sequencing
**Addresses:** MODMGMT requirements
**Avoids:** Pitfall 11 (EventBus subscription lifecycle leak during hot-reload)
**Research flag:** Likely needs `/gsd:research-phase` — CLI UX patterns and AssemblyLoadContext hot-reload edge cases warrant deeper investigation

### Phase Ordering Rationale

- Phase 0 before Phase 1: pre-existing test failures make regression attribution impossible during concurrency work; must be clean baseline
- Phase 1 before Phase 2: the `_pendingPrompt` and `_failedModules` races must be fixed at source before the Activity Channel hides symptoms
- Phase 2 before Phase 3: `ActivityRequest` hierarchy lives in Contracts — it is defined in Phase 2 and can be cleanly moved to Contracts in Phase 3 without awkward mid-phase shuffles
- Phase 3 before Phase 4: modules cannot be decoupled until the interfaces they depend on exist in Contracts
- Phase 4 before Phase 5: module management UI depends on a stable install/unload path; a module with lingering Core references could cause spurious unload failures during hot-reload

### Research Flags

Phases needing deeper research during planning:
- **Phase 5 (Module Management UI):** AssemblyLoadContext hot-reload edge cases, CLI UX patterns for module lifecycle management — sparse documentation on Blazor Server + ALC hot-reload interaction

Phases with standard patterns (skip research-phase):
- **Phase 0:** Fixing known test isolation issues — root cause documented (ANIMA-08 global singleton)
- **Phase 1:** Mechanical race fixes — `ConcurrentDictionary` swap and local-capture pattern are established BCL idioms
- **Phase 2:** `Channel<T>` mailbox pattern is fully documented in official .NET docs; architecture is fully specified in ARCHITECTURE.md
- **Phase 3:** Interface promotion pattern is established; binary-compat rules are documented; no unknowns
- **Phase 4:** Purely mechanical `using` directive changes; migration order and validation steps are fully specified

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Zero new dependencies — all BCL primitives; verified `System.Threading.Channels` BCL inclusion for .NET 8; NuGet search confirms no package reference needed |
| Features | HIGH | Requirements derived from direct codebase inspection of 14 modules and their Core imports; MVP vs. v1.x split is principled; concurrency patterns are well-established |
| Architecture | HIGH | All conclusions from live codebase inspection + official .NET docs; `ActivityChannel` design is a direct application of the documented `Channel<T>` single-reader pattern; no inferred behavior |
| Pitfalls | HIGH | Two known races confirmed by code inspection; binary-compat pitfall confirmed by Microsoft's own breaking-changes policy; Blazor SynchronizationContext pitfall confirmed by official Blazor docs |

**Overall confidence:** HIGH

### Gaps to Address

- **`ILLMService` scope in v1.7:** Moving `ILLMService` to Contracts also requires moving `ChatMessageInput` — assess whether this is in scope for Phase 3 or deferred to v1.8. The direction is correct; the question is timing. Resolve during Phase 3 planning.
- **ANIMA-08 resolution depth:** The 3 pre-existing test failures are caused by the global `IEventBus` singleton. Phase 0 may reveal that a full ANIMA-08 fix is needed, not just test isolation shims. Scope must be assessed during Phase 0 execution and a decision made before Phase 2 begins.
- **Stateless Anima policy boundary:** Research identifies stateless vs. stateful as a meaningful distinction for execution policy, but the mechanism for declaring a module stateless (`bool IsStateless` on `AnimaDescriptor`? `[StatelessModule]` attribute?) is unresolved. Must be decided during Phase 2 planning. The `SemaphoreSlim(N,N)` path vs. the `ActivityChannel` path depends on this flag.
- **Module Management UI scope:** MODMGMT-01/02/03/06 are listed in the FEATURES.md MVP but Phase 5 is the first time a deeper look at the UI design is called for. Phase 5 should use `/gsd:research-phase` to investigate CLI patterns, Blazor UI scaffolding, and ALC hot-reload interaction before implementation.

---

## Sources

### Primary (HIGH confidence)
- OpenAnima source codebase (`/home/user/OpenAnima/src/`) — direct inspection of LLMModule, WiringEngine, HeartbeatLoop, EventBus, AnimaRuntime, AnimaRuntimeManager, IAnimaContext, IAnimaModuleConfigService — all architecture and pitfall conclusions
- [Microsoft Learn — Channels in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) — `Channel<T>` API, BCL inclusion, `ReadAllAsync` consumer pattern
- [.NET Blog — Introduction to System.Threading.Channels](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/) — producer-consumer patterns, backpressure, completion semantics
- [NuGet — System.Threading.Channels 8.0.0](https://www.nuget.org/packages/System.Threading.Channels/8.0.0) — confirms BCL inclusion for .NET 8 targets
- [Microsoft Learn — SemaphoreSlim](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim) — `WaitAsync(CancellationToken)` API, async throttling pattern
- [Microsoft Learn — Create .NET app with plugin support](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support) — AssemblyLoadContext plugin contracts pattern
- [Microsoft Docs — Breaking changes and .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes) — namespace/assembly move is binary breaking
- [Microsoft Docs — .NET API changes that affect compatibility](https://learn.microsoft.com/en-us/dotnet/core/compatibility/library-change-rules) — interface member addition is a breaking change
- [Microsoft Docs — Blazor Server SynchronizationContext](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context) — `InvokeAsync` requirement for background threads
- [GitHub — dotnet/runtime issue #33221](https://github.com/dotnet/runtime/issues/33221) — `ConcurrentDictionary.GetOrAdd` factory outside lock
- [GitHub — dotnet/runtime issue #75418](https://github.com/dotnet/runtime/issues/75418) — bounded channel deadlock with `WriteAsync`
- OpenAnima PROJECT.md — ANIMA-08 tech debt, v1.7 active requirements, known race conditions

### Secondary (MEDIUM confidence)
- [Blazor University — Thread safety using InvokeAsync](https://blazor-university.com/components/multi-threaded-rendering/invokeasync/) — re-entrancy at await points, InvokeAsync not a full serialization guarantee
- [Real Plugin Systems in .NET: AssemblyLoadContext — Jan 2026](https://jordansrowles.medium.com/real-plugin-systems-in-net-assemblyloadcontext-unloadability-and-reflection-free-discovery-81f920c83644) — "five lies" of AssemblyLoadContext unloadability
- [Building High-Performance .NET Apps with C# Channels](https://antondevtips.com/blog/building-high-performance-dotnet-apps-with-csharp-channels) — Channel<T> usage patterns corroborating official docs
- [blog.semirhamid.com — .NET Concurrency: lock, SemaphoreSlim & Channels](https://blog.semirhamid.com/net-concurrency-lock-semaphore-slim-and-channels) — SemaphoreSlim vs Channel<T> comparison
- [Orleans — Request scheduling (grain turns)](https://learn.microsoft.com/en-us/dotnet/orleans/grains/request-scheduling) — reference for named-channel / reentrancy pattern analogy

---
*Research completed: 2026-03-14*
*Ready for roadmap: yes*
