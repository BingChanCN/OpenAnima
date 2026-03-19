---
phase: 40-module-storage-path
plan: "01"
subsystem: contracts-storage
tags: [contracts, storage, di, plugin-loader, tdd]
dependency_graph:
  requires: []
  provides: [IModuleStorage, ModuleStorageService]
  affects: [PluginLoader, AnimaServiceExtensions]
tech_stack:
  added: []
  patterns: [per-anima-path-convention, path-traversal-validation, greedy-constructor-di]
key_files:
  created:
    - src/OpenAnima.Contracts/IModuleStorage.cs
    - src/OpenAnima.Core/Services/ModuleStorageService.cs
    - tests/OpenAnima.Tests/Unit/ModuleStorageServiceTests.cs
  modified:
    - src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs
    - src/OpenAnima.Core/Plugins/PluginLoader.cs
    - tests/OpenAnima.Tests/Unit/ContractsApiTests.cs
decisions:
  - "IModuleStorage registered without boundModuleId in DI — built-in modules use explicit GetDataDirectory(moduleId) overload; external modules receive bound instance via PluginLoader"
  - "ValidateModuleId rejects null/whitespace, .., /, \\ — dots and hyphens allowed for qualified module names"
metrics:
  duration_seconds: 365
  completed_date: "2026-03-18"
  tasks_completed: 2
  files_changed: 6
---

# Phase 40 Plan 01: Module Storage Path Summary

IModuleStorage interface and ModuleStorageService implementation with per-Anima path convention, path traversal validation, DI registration, and PluginLoader ContractsTypeMap entry.

## What Was Built

- `IModuleStorage` in `OpenAnima.Contracts` — 3 methods: `GetDataDirectory()`, `GetDataDirectory(string)`, `GetGlobalDataDirectory(string)`
- `ModuleStorageService` in `OpenAnima.Core.Services` — resolves paths dynamically from `IModuleContext.ActiveAnimaId`, auto-creates directories, validates moduleId
- DI registration in `AnimaServiceExtensions.AddAnimaServices` as singleton
- `PluginLoader.ContractsTypeMap` entry for `IModuleStorage` — external modules can inject it via constructor
- 14 unit tests in `ModuleStorageServiceTests` + 6 surface tests in `ContractsApiTests`

## Path Conventions

| Method | Path |
|--------|------|
| `GetDataDirectory(moduleId)` | `{animasRoot}/{activeAnimaId}/module-data/{moduleId}/` |
| `GetGlobalDataDirectory(moduleId)` | `{dataRoot}/module-data/{moduleId}/` |

## Decisions Made

1. **No boundModuleId in DI singleton** — The singleton registered in `AnimaServiceExtensions` has no bound moduleId. Built-in modules call `GetDataDirectory(moduleId)` explicitly. External modules loaded via PluginLoader receive a bound instance (future work in Phase 41+).
2. **Validation allows dots and hyphens** — `My.Valid.Module` and `my-module` are valid; only `..`, `/`, `\` are rejected to prevent path traversal.

## Deviations from Plan

None — plan executed exactly as written.

## Test Results

| Suite | Tests | Result |
|-------|-------|--------|
| ModuleStorage (new) | 14 | PASS |
| ContractsApiTests | 55 | PASS |
| Full suite | 374 | PASS |

## Self-Check: PASSED
