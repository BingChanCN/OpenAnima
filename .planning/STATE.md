---
gsd_state_version: 1.0
milestone: v1.8
milestone_name: SDK Runtime Parity
status: shipped
last_updated: "2026-03-18T17:00:00.000Z"
last_activity: "2026-03-18 — v1.8 SDK Runtime Parity milestone shipped"
progress:
  total_phases: 4
  completed_phases: 4
  total_plans: 8
  completed_plans: 8
  percent: 100
---

# Project State: OpenAnima

**Last updated:** 2026-03-18
**Current milestone:** v1.8 SDK Runtime Parity — SHIPPED

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-18)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Planning next milestone

## Current Position

Phase: 41-external-context-module
Plan: 02 (complete)
Status: Phase 41 Plan 02 Complete — ContextModule capstone, 389 tests passing, ECTX-01 ECTX-02 validated
Last activity: 2026-03-18 — Completed Phase 41 Plan 02: ContextModule external module, v1.8 SDK Runtime Parity milestone complete

Progress: [██████████] 100%

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

## Performance Metrics

**Velocity:**
- Total plans completed: 85 (across v1.0–v1.7)

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
| Phase 38-pluginloader-di-injection P01 | 621 | 2 tasks | 2 files |
| Phase 38-pluginloader-di-injection P02 | 260 | 2 tasks | 2 files |
| Phase 38-pluginloader-di-injection P03 | 45 | 2 tasks | 7 files |
| Phase 39-contracts-type-migration-structured-messages P01 | 8 | 2 tasks | 6 files |
| Phase 39-contracts-type-migration-structured-messages P02 | 884 | 2 tasks | 3 files |
| Phase 40-module-storage-path P01 | 365 | 2 tasks | 6 files |
| Phase 41-external-context-module P01 | 15 | 1 tasks | 4 files |
| Phase 41-external-context-module P02 | 16 | 2 tasks | 5 files |

## Accumulated Context

### Technical Debt (carried forward to next milestone)

- MODMGMT-01/02/03/06: Full install/uninstall/search UI deferred
- ANIMA-08: Global IEventBus singleton kept for DI — full per-Anima module instances deferred to v2+
- ILLMService remains in Core (ChatMessageInput now moved to Contracts — resolved)
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

*State updated: 2026-03-18*
*Stopped at: Phase 40 Plan 01 complete — IModuleStorage, 374 tests passing, STOR-01 validated*
