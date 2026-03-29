---
phase: 54-living-memory-sedimentation
plan: 01
subsystem: memory
tags: [sedimentation, memory-graph, llm-extraction, sqlite, tdd, step-recorder]

# Dependency graph
requires:
  - phase: 52-automatic-memory-recall
    provides: IMemoryGraph.WriteNodeAsync, QueryByPrefixAsync, MemoryNode with SourceStepId provenance
  - phase: 53-tool-aware-memory-operations
    provides: MemoryGraph snapshot versioning, StepRecorder observability pattern
  - phase: 50-provider-registry
    provides: LLMProviderRegistryService.GetDecryptedApiKey, ILLMProviderRegistry
  - phase: 51-llm-module-configuration
    provides: IAnimaModuleConfigService pattern for per-Anima LLM config

provides:
  - ISedimentationService interface with SedimentAsync method
  - SedimentationService: full extraction pipeline (LLM call -> JSON parse -> keyword normalize -> MemoryNode write -> StepRecord)
  - Testable service via llmCallOverride constructor parameter
  - 12 unit tests covering all LIVM requirements

affects:
  - 54-02 (LLMModule integration — will wire SedimentationService into post-execution path)
  - 55-memory-review-surfaces (will surface sediment:// nodes in review UI)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - llmCallOverride constructor parameter pattern for testable LLM-calling services
    - SedimentFakeStepRecorder scoped to avoid naming conflicts with existing test fakes
    - JSON context truncation (200 char cap) for existing node merge context in LLM prompt

key-files:
  created:
    - src/OpenAnima.Core/Memory/ISedimentationService.cs
    - src/OpenAnima.Core/Memory/SedimentationService.cs
    - tests/OpenAnima.Tests/Unit/SedimentationServiceTests.cs
  modified: []

key-decisions:
  - "SedimentationService accepts Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<string>>? llmCallOverride constructor parameter — production passes null (builds ChatClient from config), tests inject a fake delegate to avoid mocking the OpenAI SDK"
  - "Keyword normalization: if value starts with '[', treat as JSON array and keep as-is; otherwise split on comma and serialize to JSON array — matches MemoryWriteTool pattern"
  - "SedimentFakeStepRecorder named with prefix to avoid conflict with existing FakeStepRecorder in BootMemoryInjectorWiringTests.cs"

patterns-established:
  - "llmCallOverride pattern: LLM-calling services accept optional Func override for test injection, avoiding OpenAI SDK mocking complexity"
  - "Sedimentation is best-effort: all exceptions caught, logged as warning, never propagated — main flow always unaffected"

requirements-completed: [LIVM-01, LIVM-02, LIVM-03, LIVM-04]

# Metrics
duration: 5min
completed: 2026-03-22
---

# Phase 54 Plan 01: Living Memory Sedimentation Summary

**ISedimentationService and SedimentationService with TDD: secondary LLM extraction pipeline writing provenance-backed sediment:// MemoryNodes with keyword normalization, auto-snapshot on update, and best-effort error handling**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-22T13:40:19Z
- **Completed:** 2026-03-22T13:45:00Z
- **Tasks:** 1 (TDD: RED -> GREEN -> REFACTOR)
- **Files modified:** 3

## Accomplishments

- ISedimentationService interface defined with `SedimentAsync(animaId, messages, llmResponse, sourceStepId, ct)` in `OpenAnima.Core.Memory` namespace
- SedimentationService implements full extraction pipeline: query existing sediment:// nodes for merge context, build prompt, call extraction LLM, parse snake_case JSON, normalize keywords, write MemoryNodes with SourceStepId provenance
- All LIVM requirements covered: extraction (LIVM-01), provenance (LIVM-02), auto-snapshot on update via WriteNodeAsync (LIVM-03), skip-when-empty with reason logging (LIVM-04)
- 12 unit tests using in-memory SQLite pattern (same as Phase 53) with FakeLlm delegate injection

## Task Commits

Each task was committed atomically:

1. **Task 1: ISedimentationService interface, SedimentationService implementation, and unit tests (TDD)** - `edd1f8d` (feat)

**Plan metadata:** `[created below]` (docs: complete plan)

_Note: TDD task — RED (tests written first), GREEN (implementation makes tests pass), REFACTOR (unused variable cleaned up)_

## Files Created/Modified

- `src/OpenAnima.Core/Memory/ISedimentationService.cs` - Interface with `SedimentAsync` method signature
- `src/OpenAnima.Core/Memory/SedimentationService.cs` - Full implementation: extraction LLM call, JSON parsing with SnakeCaseLower, MemoryNode writing, StepRecord observability, keyword normalization, error catching
- `tests/OpenAnima.Tests/Unit/SedimentationServiceTests.cs` - 12 unit tests covering: happy path (2 items), provenance, keywords/disclosure trigger, skip-when-empty, auto-snapshot on update, LLM exception handling, malformed JSON, keyword normalization (CSV and array), step record observability, existing node context, content truncation

## Decisions Made

- **llmCallOverride pattern**: SedimentationService accepts `Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<string>>?` constructor parameter. Production passes `null` and builds its own ChatClient from provider config. Tests inject a delegate. This avoids mocking the OpenAI SDK entirely.
- **SedimentFakeStepRecorder naming**: Named with `Sediment` prefix to avoid `CS0101` duplicate type conflict with the existing `FakeStepRecorder` in `BootMemoryInjectorWiringTests.cs`.
- **Context truncation**: Each existing sediment:// node content truncated to 200 chars in the extraction LLM prompt to control token budget.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- `FakeStepRecorder` naming conflict: A `FakeStepRecorder` already existed in `BootMemoryInjectorWiringTests.cs` in the same namespace. Renamed to `SedimentFakeStepRecorder` to avoid `CS0101` duplicate type error.
- `MemoryLeakTests.UnloadModule_ReleasesMemory_After100Cycles`: Flaky GC-pressure test that occasionally fails when run with full suite concurrently. Verified it passes in isolation — pre-existing issue unrelated to this plan's changes.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- ISedimentationService and SedimentationService are ready for wiring into LLMModule (Plan 54-02)
- Service is in `OpenAnima.Core.Memory` namespace (already in LLMModule's allowlist from Phase 52/53)
- llmCallOverride pattern enables easy integration testing without real LLM keys

## Self-Check: PASSED

- `src/OpenAnima.Core/Memory/ISedimentationService.cs` — FOUND
- `src/OpenAnima.Core/Memory/SedimentationService.cs` — FOUND
- `tests/OpenAnima.Tests/Unit/SedimentationServiceTests.cs` — FOUND
- `.planning/phases/54-living-memory-sedimentation/54-01-SUMMARY.md` — FOUND
- Commit `edd1f8d` — FOUND

---
*Phase: 54-living-memory-sedimentation*
*Completed: 2026-03-22*
