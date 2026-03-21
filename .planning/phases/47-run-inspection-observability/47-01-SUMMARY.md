---
phase: 47-run-inspection-observability
plan: "01"
subsystem: runs
tags: [observability, testing, localization, logging, navigation]
dependency_graph:
  requires: []
  provides: [PropagationColorAssigner, RunCard-navigation, BeginScope-injection, RunDetail-i18n-keys, WiringEngineScopeTests]
  affects: [RunCard, WiringEngine, RunService, SharedResources]
tech_stack:
  added: []
  patterns: [ILogger.BeginScope ambient scope, hand-rolled ILogger spy for unit tests, TDD red-green]
key_files:
  created:
    - src/OpenAnima.Core/Runs/PropagationColorAssigner.cs
    - tests/OpenAnima.Tests/Unit/PropagationColorTests.cs
    - tests/OpenAnima.Tests/Unit/RunDetailTimelineTests.cs
    - tests/OpenAnima.Tests/Unit/WiringEngineScopeTests.cs
  modified:
    - src/OpenAnima.Core/Components/Shared/RunCard.razor
    - src/OpenAnima.Core/Wiring/WiringEngine.cs
    - src/OpenAnima.Core/Runs/RunService.cs
    - src/OpenAnima.Core/Resources/SharedResources.en-US.resx
    - src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx
decisions:
  - "WiringEngineScopeTests uses hand-rolled ILogger<WiringEngine> spy (no mocking library available) — CapturingScopeLogger captures BeginScope state objects for assertion"
  - "BeginScope wraps the inner try/catch in all 3 WiringEngine port-type branches (Text, Trigger, object) so scope is active during ForwardPayloadAsync and step recorder calls"
  - "RunService BeginScope wraps only the LogInformation call in StartRunAsync/PauseRunAsync — scope is ambient for any downstream log calls within that block"
metrics:
  duration: 29min
  completed: "2026-03-21"
  tasks_completed: 2
  files_changed: 9
---

# Phase 47 Plan 01: Foundation — Test Scaffolds, Utility, Navigation, Logging Summary

**One-liner:** Deterministic PropagationId-to-color utility, RunCard click-to-detail navigation, ILogger.BeginScope injection in WiringEngine and RunService, 22 RunDetail i18n keys in both locales, and 11 passing unit tests.

---

## Tasks Completed

| # | Name | Commit | Key Files |
|---|------|--------|-----------|
| 1 | Test scaffolds + PropagationColorAssigner | fe2f656 | PropagationColorAssigner.cs, PropagationColorTests.cs, RunDetailTimelineTests.cs, WiringEngineScopeTests.cs |
| 2 | RunCard navigation + BeginScope + localization | 60fec49 | RunCard.razor, WiringEngine.cs, RunService.cs, SharedResources.en-US.resx, SharedResources.zh-CN.resx |

---

## Verification

```
dotnet build src/OpenAnima.Core/ --no-restore  →  0 Error(s)
dotnet test --filter PropagationColorTests|RunDetailTimelineTests|WiringEngineScopeTests
  Passed: 11, Failed: 0, Skipped: 0
```

---

## Deviations from Plan

None — plan executed exactly as written.

---

## Self-Check: PASSED

- `src/OpenAnima.Core/Runs/PropagationColorAssigner.cs` — FOUND
- `tests/OpenAnima.Tests/Unit/PropagationColorTests.cs` — FOUND
- `tests/OpenAnima.Tests/Unit/RunDetailTimelineTests.cs` — FOUND
- `tests/OpenAnima.Tests/Unit/WiringEngineScopeTests.cs` — FOUND
- Commit fe2f656 — FOUND
- Commit 60fec49 — FOUND
