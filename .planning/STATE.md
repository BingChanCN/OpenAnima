---
gsd_state_version: 1.0
milestone: v1.9
milestone_name: Event-Driven Propagation Engine
status: unknown
last_updated: "2026-03-19T15:16:25.110Z"
progress:
  total_phases: 3
  completed_phases: 3
  total_plans: 6
  completed_plans: 6
---

# Project State: OpenAnima

**Last updated:** 2026-03-19
**Current milestone:** v1.9 Event-Driven Propagation Engine

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-19)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Phase 44 — config-schema-sidebar

## Current Position

Phase: 44 (config-schema-sidebar) — EXECUTING
Plan: 1 of 1

## Decisions Made

1. **FullName type matching**: Cross-AssemblyLoadContext type resolution uses FullName string comparison (consistent with existing IModule discovery pattern)
2. **Contracts services optional**: IModuleConfig, IModuleContext, IEventBus, ICrossAnimaRouter resolve to null with warning on failure (graceful degradation)
3. **Non-Contracts required params error**: Unknown parameters without default values produce LoadResult error (fail fast)
4. **ILogger via ILoggerFactory**: Non-generic ILogger created via ILoggerFactory.CreateLogger(moduleType.FullName) to avoid cross-context generic type issues
5. **Greedy constructor selection**: Constructor with most parameters wins (ASP.NET Core DI compatible)
- [Phase 38-pluginloader-di-injection]: IServiceProvider injected into ModuleService via constructor — no interface change needed, implementation detail, auto-registered by ASP.NET Core
- [Phase 38-pluginloader-di-injection]: CrossAnimaRouter disambiguated via (Lazy<IAnimaRuntimeManager>?)null cast — primary Lazy overload is canonical
- [Phase 38-pluginloader-di-injection]: MSBuild node reuse disabled via MSBUILDDISABLENODEREUSE=1 env var when spawning dotnet build from tests
- [Phase 39-contracts-type-migration-structured-messages]: using alias pattern for Core files — explicit scoped import, avoids namespace pollution in LLM layer
- [Phase 39-contracts-type-migration-structured-messages]: Semaphore is primary priority mechanism for messages vs prompt port — messages acquires first, prompt Wait(0) returns false
- [Phase 39-contracts-type-migration-structured-messages]: ExecuteWithMessagesListAsync extracted as shared pipeline — both prompt and messages paths use it after building their message list
- [Phase 40-module-storage-path]: IModuleStorage DI singleton has no boundModuleId — built-in modules use explicit GetDataDirectory(moduleId); external modules receive bound instance via PluginLoader
- [Phase 40-module-storage-path]: ValidateModuleId rejects null/whitespace, .., /, \ — dots and hyphens allowed for qualified module names
- [Phase 41-external-context-module]: manifest.Id ?? manifest.Name used as boundModuleId — manifests without explicit id fall back to Name
- [Phase 41-external-context-module]: IModuleStorage special case placed before generic ContractsTypeMap lookup in ResolveParameter
- [Phase 41-external-context-module]: module.json CopyToOutputDirectory=PreserveNewest — PluginLoader needs manifest alongside DLL in build output
- [Phase 41-external-context-module]: ContextModule tests load from build output dir (not .oamod) — avoids build dependency fragility in test suite
- [Phase 42-propagation-engine]: FixedTextModule.Subscribe<DateTime> for trigger port — matches HeartbeatModule tick output payload type
- [Phase 42-propagation-engine]: HeartbeatModule.TickAsync retained as public method for Phase 43 standalone timer integration
- [Phase 42-01]: ConnectionGraph accepts cyclic graphs — HasCycle is informational only (DFS), no topo sort
- [Phase 42-01]: Per-module SemaphoreSlim keyed by targetModuleRuntimeName — one semaphore per target module for wave isolation
- [Phase 42-01]: ITickable removed from Contracts — HeartbeatModule no longer implements it; HeartbeatLoop fallback path removed
- [Phase 42-03]: WiringDIIntegrationTests cycle test rewritten to assert acceptance (IsLoaded == true)
- [Phase 42-03]: AnimaRuntime_OnTick test converted to sync — no ExecuteAsync or HeartbeatModule.execute event
- [Phase 42-03]: [StatelessModule] added to FixedTextModule and HeartbeatModule (both are stateless signal processors)
- [Phase 43-heartbeat-refactor]: HeartbeatModule refactored to standalone PeriodicTimer with IModuleConfigSchema and config-driven interval
- [Phase 43-heartbeat-refactor]: HeartbeatModule unit tests prove BEAT-05 (standalone timer tick publish) and BEAT-06 (configurable interval with 50ms clamp) — 394 tests green
- [Phase 44-config-schema-sidebar]: ModuleSchemaService uses static built-in type map + IServiceProvider.GetService — avoids reflection scanning at runtime
- [Phase 44-config-schema-sidebar]: Schema defaults merged into _currentConfig in LoadConfig — auto-save not triggered on load, only on user edits
- [Phase 44-config-schema-sidebar]: Raw kvp fallback preserved in EditorConfigSidebar — non-schema modules continue working unchanged

## v1.9 Decisions

- **No convergence control**: Cycle dampening (TTL, energy decay) explicitly deferred — wait for real-world usage to reveal need
- **Modules can stop propagation**: A module that produces no output naturally terminates the wave — no explicit stop mechanism needed
- **HeartbeatModule becomes signal source**: Emits trigger on output port; WiringEngine no longer has a heartbeat-driven loop

## Performance Metrics

**Velocity:**

- Total plans completed: 93 (across v1.0–v1.8)

**By Milestone:**

| Milestone | Phases | Plans | Shipped |
|-----------|--------|-------|---------|
| v1.0 Core Platform | 2 | 5 | 2026-02-21 |
| v1.1 WebUI Dashboard | 5 | 10 | 2026-02-23 |
| v1.2 LLM Integration | 3 | 6 | 2026-02-25 |
| v1.3 Visual Wiring | 10 | 21 | 2026-02-28 |
| v1.4 Module SDK | 3 | 8 | 2026-02-28 |
| v1.5 Multi-Anima | 5 | 13 | 2026-03-09 |
| v1.6 Cross-Anima Routing | 4 | 8 | 2026-03-14 |
| v1.7 Runtime Foundation | 6 | 13 | 2026-03-16 |
| v1.8 SDK Runtime Parity | 4 | 9 | 2026-03-18 |
| Phase 42-propagation-engine P02 | 2 | 2 tasks | 2 files |
| Phase 42 P01 | 17m | 2 tasks | 8 files |
| Phase 42-propagation-engine P03 | 15 | 2 tasks | 7 files |
| Phase 43-heartbeat-refactor P02 | 7min | 2 tasks | 2 files |
| Phase 44-config-schema-sidebar P01 | 8min | 2 tasks | 3 files |

## Accumulated Context

### Technical Debt (carried forward to v1.9)

- MODMGMT-01/02/03/06: Full install/uninstall/search UI deferred
- ANIMA-08: Global IEventBus singleton kept for DI — full per-Anima module instances deferred to v2+
- ILLMService remains in Core (ChatMessageInput now moved to Contracts)
- Schema mismatch between CLI and Runtime (extended manifest fields)
- TextJoin fixed 3 input ports — static port system limitation

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|--------------|
| 3 | 交叉评审一下phase 36 | 2026-03-16 | e7464d2 | [3-phase-36](./quick/3-phase-36/) |
| 4 | Phase 36 code quality review | 2026-03-16 | f5feaf8 | [4-phase-36-code-review](./quick/4-phase-36-code-review/) |
| 5 | Phase 36 code review fixes (W1, W2, S1, S2, S3) | 2026-03-16 | 9bc2d97 | [5-phase-36-code-review-2-warnings-3-sugges](./quick/5-phase-36-code-review-2-warnings-3-sugges/) |
| 6 | code review phase 34 35 | 2026-03-16 | 4b26aa9 | [6-code-review-phase-34-35](./quick/6-code-review-phase-34-35/) |

---

*State updated: 2026-03-19*
*Stopped at: Completed 43-02-PLAN.md — Phase 43 complete*
