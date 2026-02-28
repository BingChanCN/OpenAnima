# Project Research Summary

**Project:** OpenAnima v1.5 Multi-Anima Architecture
**Domain:** Multi-instance agent runtime with i18n and module ecosystem
**Researched:** 2026-02-28
**Confidence:** HIGH

## Executive Summary

OpenAnima v1.5 transforms the single-instance agent runtime into a multi-Anima architecture where users can create, manage, and run multiple independent AI agents simultaneously. The research reveals this requires shifting from singleton-based global state to a factory pattern with per-Anima isolation, analogous to multi-tenant SaaS architecture where each Anima is a "tenant" with isolated runtime state.

The recommended approach leverages Blazor Server's built-in scoped services with minimal new dependencies: Microsoft.Extensions.Localization 8.0.* for i18n with custom JSON localizer, System.Text.Json (built-in) for configuration persistence, and an AnimaRuntimeManager singleton factory to manage per-Anima instances. Each Anima gets its own EventBus, HeartbeatLoop, WiringEngine, and module instances, while shared infrastructure (PluginRegistry, PortRegistry) remains singleton. An AnimaContext scoped service acts as the "tenant resolver" identifying which Anima the current circuit is viewing.

Critical risks center on memory leaks from improper disposal (EventBus subscriptions, AssemblyLoadContext), service lifetime mismatches (singleton-to-scoped), concurrent configuration file writes, and culture switching requiring full page reload. Prevention requires implementing IAsyncDisposable for all event subscriptions, clearing Assembly references before Unload(), using atomic write-to-temp-then-rename pattern for file persistence, and establishing clear per-Anima state boundaries from the start. The architecture is well-documented with high confidence in stack choices and patterns.

## Key Findings

### Recommended Stack

The v1.5 stack maintains OpenAnima's "minimal dependencies" philosophy while adding only essential i18n support. Core runtime (.NET 8.0, Blazor Server, SignalR 8.0.x, OpenAI SDK 2.8.0) remains unchanged.

**Core technologies:**
- **Microsoft.Extensions.Localization 8.0.*** — Official .NET localization with IStringLocalizer support for Blazor Server
- **Custom JSON localizer** — JSON-based resource files (more flexible than .resx for translators, easier version control)
- **System.Text.Json (built-in)** — JSON serialization for configuration persistence, zero additional dependencies
- **Scoped services (built-in)** — Per-circuit state isolation, perfect for per-Anima state
- **State container pattern** — Lightweight reactive state management (20 LOC, no dependencies)

**Why minimal dependencies:** Built-in .NET 8 capabilities handle all requirements. JSON localizer avoids .resx complexity. Scoped services provide natural per-Anima isolation without external state management libraries.

### Expected Features

Research across VSCode, JetBrains, Unreal Engine, and multi-tenant SaaS patterns reveals clear table stakes and differentiators.

**Must have (table stakes):**

*Anima Management:*
- Create/list/switch/delete Animas — core multi-instance capability
- Independent execution per Anima — separate heartbeat loops, isolated module state
- Persist Anima configuration — save name, module connections, module configs to JSON per instance

*i18n:*
- Language switcher UI — dropdown or toggle in header/settings
- Chinese/English UI text — resource files for all UI strings
- Persist language preference — localStorage or config file
- Full page reload on switch — culture switching requires NavigateTo(forceLoad: true)

*Module Management:*
- Install/uninstall from .oamod — already have package loading (v1.4)
- Enable/disable toggle — standard plugin pattern (VSCode, JetBrains)
- Module metadata display — name, version, author, description
- Module status indicators — enabled/disabled/error states

*Module Configuration:*
- Click module → detail panel — standard node editor pattern (Blender, Unreal Engine)
- Edit module-specific settings — dynamic UI based on module schema
- Persist module config per Anima — save to Anima's config JSON
- Config validation — prevent invalid states

*Built-in Modules:*
- Fixed text output — basic data source with editable text field
- Text concatenation — two inputs → one output
- Text split by delimiter — common text operation
- Conditional branching — flow control with condition expression
- Configurable LLM — API URL, API key, model name in detail panel

**Should have (differentiators):**
- Heartbeat as optional module — flexibility, not all Animas need proactive loops
- Per-Anima chat interface — each instance has independent conversation
- Visual module status in editor — real-time feedback on execution state (already exists v1.3)
- Anima cloning — duplicate existing setup for experimentation
- Module search/filter — helps when module count grows

**Defer (v2+):**
- Module dependency resolution — complex graph resolution, wait for real patterns
- Module marketplace backend — infrastructure burden, validate local-first first
- Nested Anima instances — unclear value, high complexity
- Cross-Anima config sync — violates isolation, wait for user demand
- Auto-update modules — breaking changes break workflows, user loses control

### Architecture Approach

Transform from singleton runtime to multi-instance architecture using factory pattern with scoped context resolution. Each Anima is analogous to a "tenant" in multi-tenant SaaS with isolated runtime state.

**Major components:**

1. **AnimaContext (Scoped)** — Tracks current Anima ID for the circuit, acts as "tenant resolver"
2. **AnimaRuntimeManager (Singleton Factory)** — Creates/manages Anima instances, Dictionary<string, AnimaRuntime>
3. **AnimaRuntime (Per-Anima Container)** — Encapsulates one Anima's runtime: EventBus, HeartbeatLoop, WiringEngine, module instances
4. **AnimaConfigStore (Singleton)** — Persists Anima metadata to JSON files in `animas/` directory
5. **AnimaModuleRegistry (Per-Anima)** — Per-Anima module instances with isolated state
6. **EditorStateService (Scoped)** — Already correct, inject AnimaContext to resolve correct AnimaRuntime
7. **ConfigurationLoader (Per-Anima)** — Load/save from `wiring-configs/{animaId}/` directory

**Key architectural patterns:**
- **Scoped Context with Singleton Factory** — AnimaContext (scoped) holds tenant ID, AnimaRuntimeManager (singleton) manages all instances
- **Per-Tenant Directory Isolation** — Each Anima gets subdirectory for configs: `wiring-configs/anima-001/`, `animas/anima-001.json`
- **Module Instance Cloning** — Each Anima gets own module instances even though types are shared
- **Service Lifetime Changes** — EventBus/HeartbeatLoop/WiringEngine move from singleton to per-Anima (managed by factory)

**Data flow:**
```
Component → AnimaContext (scoped) → AnimaRuntimeManager (singleton)
                ↓                           ↓
        EditorStateService (scoped)    AnimaRuntime (per-Anima)
                ↓                           ↓
        WiringEngine (per-Anima) ←──────────┤
                ↓                           ↓
        EventBus (per-Anima) ←──────────────┤
                ↓                           ↓
        Modules (per-Anima instances) ←─────┘
```

### Critical Pitfalls

Research identified 10 critical pitfalls with prevention strategies:

1. **Circuit Memory Leaks from Event Subscriptions** — EventBus subscriptions create strong references from singleton to scoped components. Prevention: Implement IAsyncDisposable, unsubscribe in DisposeAsync(), use WeakReference for subscriptions.

2. **AssemblyLoadContext Memory Leaks** — Storing Assembly references in instance fields prevents Unload(). Prevention: Clear all Assembly references before Unload(), use WeakReference<Assembly>, explicitly GC.Collect() after Unload().

3. **Singleton-to-Scoped Service Lifetime Mismatch** — Migrating from singleton to scoped causes InvalidOperationException. Prevention: Audit all singleton services for scoped dependencies, use IServiceScopeFactory in singletons, convert per-Anima state to scoped.

4. **Configuration File Corruption from Concurrent Writes** — Multiple Animas saving simultaneously causes invalid JSON. Prevention: Write-to-temp-then-rename pattern, file-level locking, single-writer queue pattern.

5. **Culture Switching Requires Full Circuit Reconnect** — Changing CultureInfo mid-circuit doesn't update UI. Prevention: Use NavigationManager.NavigateTo(forceLoad: true), store preference in persistent storage, accept page reload.

6. **Shared Static State Across Module Instances** — Static fields are per-AppDomain, not per-instance. Prevention: Ban static mutable state in modules, use instance fields, register modules as scoped.

7. **Heartbeat Loop Concurrent Execution Race Conditions** — Multiple Animas run heartbeats concurrently. Prevention: Ensure EventBus is thread-safe, use SemaphoreSlim for critical sections, make operations idempotent.

8. **Missing Translation Fallback Strategy** — Missing translations show keys instead of text. Prevention: Implement fallback chain (requested → English → key), build-time validation, visual indicator in dev mode.

9. **Module Configuration UI State Desync** — UI form state, module instance state, and persisted config diverge. Prevention: Establish clear state flow (UI → validation → module → persistence), explicit Save button, show dirty state indicator.

10. **RTL Layout Breaks Visual Editor** — Arabic/Hebrew flip entire page but SVG coordinates don't. Prevention: Isolate editor canvas with `<div dir="ltr">`, keep UI chrome in RTL, test early.

## Implications for Roadmap

Based on combined research, suggested phase structure with clear dependencies and rationale:

### Phase 1: Multi-Anima Foundation
**Rationale:** Core architecture must exist before other features can use it. Establishes per-Anima isolation pattern that all subsequent phases depend on.

**Delivers:** AnimaMetadata, AnimaConfigStore, AnimaRuntime, AnimaRuntimeManager, AnimaContext, Program.cs DI registration

**Addresses:** 
- Multi-Anima architecture (FEATURES.md P1: create/list/switch/delete)
- Independent execution per Anima (FEATURES.md P1)
- Configuration persistence foundation (FEATURES.md P1)

**Avoids:**
- Circuit memory leaks (PITFALLS.md #1) — Implement IAsyncDisposable from start
- Singleton-to-scoped mismatch (PITFALLS.md #3) — Design with scoped services from day one
- Shared static state (PITFALLS.md #6) — Establish module isolation before building instances
- Heartbeat race conditions (PITFALLS.md #7) — Verify thread safety before enabling multiple instances

**Uses:**
- Scoped services (STACK.md) for per-circuit AnimaContext
- State container pattern (STACK.md) for reactive updates
- AnimaRuntimeManager factory (ARCHITECTURE.md Pattern 1)

### Phase 2: Service Migration & i18n
**Rationale:** Refactor singleton services into AnimaRuntime while adding i18n infrastructure. These are independent changes that can be developed in parallel.

**Delivers:** 
- Refactored EventBus/HeartbeatLoop/WiringEngine (per-Anima)
- JSON localizer, language switcher UI, preference persistence
- Updated EditorStateService to use AnimaContext

**Addresses:**
- i18n (FEATURES.md P1: language switcher, Chinese/English, persistence)
- Service lifetime migration (ARCHITECTURE.md Phase 2)

**Avoids:**
- Culture switching issues (PITFALLS.md #5) — Implement full page reload pattern
- Missing translations (PITFALLS.md #8) — Establish fallback before adding translations
- RTL layout breaks (PITFALLS.md #10) — Test with Arabic/Hebrew early

**Uses:**
- Microsoft.Extensions.Localization 8.0.* (STACK.md)
- Custom JSON localizer (STACK.md Pattern 2)
- NavigateTo(forceLoad: true) for culture switching (STACK.md Pattern 4)

### Phase 3: Module Management
**Rationale:** Extends existing ModuleRegistry (v1.0) with enable/disable capability. Requires Phase 1 for per-Anima module instances.

**Delivers:** Install/uninstall/enable/disable UI, module metadata display, module status indicators

**Addresses:**
- Module management (FEATURES.md P1: install/uninstall, enable/disable, metadata)
- Module isolation (ARCHITECTURE.md AnimaModuleRegistry)

**Avoids:**
- AssemblyLoadContext leaks (PITFALLS.md #2) — Clear Assembly references before Unload()

**Uses:**
- Module instance cloning (ARCHITECTURE.md Pattern 3)
- Per-Anima module registry (ARCHITECTURE.md)

### Phase 4: Module Configuration UI
**Rationale:** Requires Phase 1 for per-Anima config persistence and Phase 3 for module selection. Implements detail panel pattern from Unreal/Blender.

**Delivers:** ModuleConfigPanel component, click-to-select, detail panel, config persistence, validation

**Addresses:**
- Module configuration UI (FEATURES.md P1: click module → detail panel, edit settings, persist)
- Built-in configurable modules (FEATURES.md P1: LLM with API config)

**Avoids:**
- Config file corruption (PITFALLS.md #4) — Atomic write-to-temp-then-rename pattern
- Config UI state desync (PITFALLS.md #9) — Establish clear state flow before building UI

**Uses:**
- System.Text.Json (STACK.md) for config persistence
- Detail panel pattern (FEATURES.md from Unreal/Blender)
- Per-tenant directory isolation (ARCHITECTURE.md Pattern 2)

### Phase 5: Built-in Modules
**Rationale:** Demonstrates all previous phases working together. Validates multi-Anima architecture with real modules.

**Delivers:** FixedTextModule, TextConcatModule, TextSplitModule, ConditionalModule, ConfigurableLLMModule, optional HeartbeatModule

**Addresses:**
- Built-in modules (FEATURES.md P1: fixed text, concat, split, conditional, LLM)
- Heartbeat as optional module (FEATURES.md differentiator)

**Avoids:**
- Heartbeat refactor breaking changes — Keep core infrastructure, make it optional

**Uses:**
- Module SDK (already shipped v1.4)
- Port system (already shipped v1.3)
- Configuration UI (Phase 4)

### Phase Ordering Rationale

- **Phase 1 is foundation** — All other phases depend on AnimaRuntimeManager and per-Anima isolation
- **Phase 2 refactors services** — Must happen before Phase 3/4 can use per-Anima instances
- **Phase 2 i18n is parallel** — Independent of Anima architecture, can develop simultaneously
- **Phase 3 before Phase 4** — Module management provides selection mechanism for config UI
- **Phase 4 before Phase 5** — Built-in modules need config UI to be useful
- **Phase 5 validates everything** — Real modules prove architecture works

### Research Flags

**Phases likely needing deeper research during planning:**
- **Phase 1** — Service scope lifecycle management (when to create/dispose, memory leak prevention)
- **Phase 4** — Dynamic UI generation for module config (each module has different schema)
- **Phase 5** — Conditional module expression evaluation (need safe expression parser, avoid eval() security)

**Phases with standard patterns (skip research-phase):**
- **Phase 2 i18n** — Well-documented .NET pattern, IStringLocalizer is standard
- **Phase 3** — Module management follows VSCode/JetBrains patterns, straightforward

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Official Microsoft docs confirm IStringLocalizer support, System.Text.Json is built-in and well-documented |
| Features | HIGH | Table stakes validated by VSCode/JetBrains/Unreal patterns, multi-tenant SaaS patterns are proven |
| Architecture | HIGH | Multi-tenant isolation patterns are well-documented, factory pattern is industry standard |
| Pitfalls | MEDIUM | Patterns are well-known (memory leaks, race conditions), but specific to this architecture combination |

**Overall confidence:** HIGH

### Gaps to Address

- **Service scope lifecycle:** When to create/dispose child scopes for Animas? Keep all alive or lazy-load? Memory implications need testing with 10+ Animas.
- **Dynamic config UI generation:** Each module has different config schema. Need pattern for generating UI from schema (JSON Schema? Reflection? Custom attributes?). Research during Phase 4 planning.
- **Conditional expression evaluation:** ConditionalModule needs safe expression parser. Options: NCalc, DynamicExpresso, or custom parser? Security implications need research during Phase 5 planning.
- **Anima switching performance:** Switching should be instant. If all Animas run simultaneously, need to test CPU/memory impact with 10+ instances. Mitigation: Lazy instantiation, stop inactive Animas after timeout.

## Sources

### Primary (HIGH confidence)
- [ASP.NET Core Blazor globalization and localization](https://learn.microsoft.com/en-us/aspnet/core/blazor/globalization-localization) — Official Microsoft documentation
- [ASP.NET Core Blazor state management overview](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/) — Official guidance on scoped services
- [How to Build Multi-Tenant Apps in .NET](https://oneuptime.com/blog/post/2026-01-26-multi-tenant-apps-dotnet/view) — Tenant context pattern
- [AssemblyLoadContext.Unload silently fails](https://github.com/dotnet/runtime/issues/44679) — Assembly reference cleanup requirements
- [Concurrent Hosted Service Start and Stop in .NET 8](https://www.stevejgordon.co.uk/concurrent-hosted-service-start-and-stop-in-dotnet-8) — Hosted service concurrency
- [Details Panel in Unreal Engine](https://dev.epicgames.com/documentation/en-us/unreal-engine/details-panel-in-the-blueprints-visual-scriting-editor-for-unreal-engine) — Detail panel pattern
- [Managing Extensions in Visual Studio Code](https://code.visualstudio.com/docs/editor/extension-marketplace) — Extension management patterns

### Secondary (MEDIUM confidence)
- [Blazor Server Memory Management](https://amarozka.dev/blazor-server-memory-management-circuit-leaks/) — Circuit leak patterns
- [Blazor web app localization culture change exceptions](https://stackoverflow.com/questions/79516530/blazor-web-app-global-interactiveserver-net9-localization-during-culture-ch) — Culture switching issues
- [What does scoped lifetime mean in Blazor Server](https://stackoverflow.com/questions/76195106/what-does-scoped-lifetime-for-a-service-mean-in-blazor-server) — Service lifetime semantics
- [PhpStorm Documentation — Enabling and Disabling Plugins](https://www.jetbrains.com/phpstorm/help/enabling-and-disabling-plugins.html) — Plugin patterns
- [Implementing Custom JSON Localization in ASP.NET Core](https://gauravm.dev/articles/implementing-custom-json-localization-in-aspnet-core-web-api/) — JSON localizer pattern

### Tertiary (LOW confidence)
- [Multi-Agent Coordination Systems Enterprise Guide 2026](https://iterathon.tech/blog/multi-agent-coordination-systems-enterprise-guide-2026) — Multi-agent architecture (enterprise-focused)
- [Concurrent file write](https://stackoverflow.com/questions/1160233/concurrent-file-write) — File corruption patterns (general topic)

---
*Research completed: 2026-02-28*
*Ready for roadmap: yes*
