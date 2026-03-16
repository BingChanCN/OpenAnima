---
quick_task: 6
type: review
focus: correctness
scope: phase_34_activity_channel_model_and_phase_35_contracts_api_expansion
completed: 2026-03-16T07:25:00Z
reviewed_files: 19
findings:
  blockers: 4
  warnings: 2
  suggestions: 0
---

# Quick Task 6 Summary

**One-liner:** Phase 34 and Phase 35 are not production-ready as implemented; the review found 4 blockers and 2 warnings that the scoped test suite missed.

## Tasks Completed

| Task | Description | Status |
|------|-------------|--------|
| 1 | Review Phase 34 runtime dispatch changes and tests | Complete |
| 2 | Review Phase 35 Contracts API, DI bridge, and config semantics | Complete |
| 3 | Write review artifacts with evidence | Complete |

## Findings Summary

### Blockers

1. `AddAnimaServices()` registers `ICrossAnimaRouter` and `IAnimaRuntimeManager` in a circular way, and resolving either service hangs.
2. The Core routing “shims” are only `global using` aliases, so they do not preserve binary compatibility for already-built consumers of `OpenAnima.Core.Routing.*`.
3. The Phase 34 heartbeat channel path no longer reaches `HeartbeatModule.TickAsync`, so `HeartbeatModule.port.tick` pipelines stop firing.
4. The stateless-dispatch fork depends on a per-runtime `PluginRegistry` that is never populated, so the concurrent bypass path is effectively dead code.

### Warnings

1. The real chat UI path still bypasses `ActivityChannelHost.EnqueueChat`, leaving user input outside the Phase 34 serialization model.
2. `AnimaModuleConfigService.SetConfigAsync(animaId, moduleId, key, value)` can lose concurrent updates because it copies config state before acquiring the lock.

## Evidence

- Targeted regression suite: `85/85` passing
- Throwaway net8.0 DI probe: both `IAnimaRuntimeManager` and `ICrossAnimaRouter` timed out after 2 seconds when resolved from a service collection using `AddAnimaServices()`

## Recommendation

Do not treat either phase as done until the blocker-level runtime regressions are fixed and the tests are updated to exercise the real seams that changed.
