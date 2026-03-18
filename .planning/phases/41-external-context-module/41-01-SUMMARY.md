---
phase: 41-external-context-module
plan: 01
subsystem: plugin-loader
tags: [di-injection, module-storage, external-modules, tdd]
dependency_graph:
  requires: []
  provides: [bound-IModuleStorage-per-external-module]
  affects: [PluginLoader, ModuleStorageService, PluginManifest]
tech_stack:
  added: []
  patterns: [special-case-before-generic-lookup, factory-method-CreateBound, manifest-Id-fallback-to-Name]
key_files:
  created:
    - tests/OpenAnima.Tests/Integration/PluginLoaderDITests.cs (ECTX-01, ECTX-02 tests added)
  modified:
    - src/OpenAnima.Core/Services/ModuleStorageService.cs
    - src/OpenAnima.Core/Plugins/PluginLoader.cs
    - src/OpenAnima.Core/Plugins/PluginManifest.cs
decisions:
  - "manifest.Id ?? manifest.Name used as boundModuleId — manifests without explicit id field fall back to Name"
  - "IModuleStorage special case placed BEFORE generic ContractsTypeMap lookup to intercept and create bound instance"
  - "PluginManifest.Id added as optional field — no validation required, existing manifests without id continue to work"
metrics:
  duration: "~15 min"
  completed: "2026-03-18"
  tasks_completed: 1
  files_changed: 4
---

# Phase 41 Plan 01: PluginLoader Bound IModuleStorage Injection Summary

PluginLoader now injects a bound `ModuleStorageService` instance per external module using `manifest.Id ?? manifest.Name`, so external modules calling `GetDataDirectory()` receive a valid per-Anima path without `InvalidOperationException`.

## What Was Built

- `ModuleStorageService.CreateBound(string moduleId)` — factory method returning a new instance sharing the same `animasRoot`, `dataRoot`, and `context` but with the given `boundModuleId`
- `PluginManifest.Id` — optional `[JsonPropertyName("id")]` field, falls back to `Name` when absent
- `PluginLoader.ResolveParameter` — new IModuleStorage special case (before generic ContractsTypeMap lookup) that calls `unboundStorage.CreateBound(manifest.Id ?? manifest.Name)`
- `ResolveParameter` signature updated to accept `PluginManifest? manifest`
- Two new tests: `ExternalModule_WithIModuleStorage_ReceivesBoundInstance` (ECTX-01) and `ExistingModules_WithoutIModuleStorage_LoadWithoutRegression` (ECTX-02)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] PluginManifest missing Id field**
- **Found during:** Task 1 GREEN phase
- **Issue:** Plan referenced `manifest.Id` but `PluginManifest` only had `Name`, `Version`, `Description`, `EntryAssembly`
- **Fix:** Added optional `Id` property with `[JsonPropertyName("id")]`; PluginLoader uses `manifest.Id ?? manifest.Name` as fallback
- **Files modified:** `src/OpenAnima.Core/Plugins/PluginManifest.cs`
- **Commit:** 51930c1

## Verification

- `dotnet test --filter "PluginLoaderDI"` — 8/8 pass (7 existing + 1 new ECTX-01 + 1 new ECTX-02)
- Full suite: 381 passing, 1 pre-existing flaky GC test (`MemoryLeakTests`) unrelated to this change

## Self-Check: PASSED
