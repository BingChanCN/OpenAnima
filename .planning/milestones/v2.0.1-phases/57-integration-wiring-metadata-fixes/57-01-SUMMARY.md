---
phase: 57-integration-wiring-metadata-fixes
plan: "01"
subsystem: memory
tags: [boot-recall, memory-recall, settings, provider, csharp, blazor]

# Dependency graph
requires:
  - phase: 52-automatic-memory-recall
    provides: MemoryRecallService with Disclosure/Glossary recall
  - phase: 55-memory-review-surfaces
    provides: BootMemoryInjector and boot node concepts established
  - phase: 56-sedimentation-llm-configuration
    provides: Settings.razor IAnimaRuntimeManager injection pattern
provides:
  - MemoryRecallService.RecallAsync returns Boot-type RecalledNodes for core:// prefix URIs
  - Boot nodes survive Disclosure and Glossary merge attempts (type and priority preserved)
  - Settings.razor disable/delete confirm dialogs show real affected module counts
affects: [LLMModule, MemoryRecallService, Settings]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Boot nodes seeded before Disclosure into deduplication dictionary to guarantee priority
    - Merge-on-collision pattern: existingBoot.RecallType == "Boot" guard in disclosure loop
    - Glossary merge uses `with` expression preserving existing RecallType automatically

key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Memory/MemoryRecallService.cs
    - tests/OpenAnima.Tests/Unit/MemoryRecallServiceTests.cs
    - tests/OpenAnima.Tests/Unit/LLMModuleMemoryTests.cs

key-decisions:
  - "Boot recall seeded at top of RecallAsync before Disclosure seeding — not after — ensures byUri dictionary starts with Boot entries so Disclosure foreach sees them and merges instead of overwriting"
  - "Glossary merge block uses `with` expression on existing entry which naturally preserves RecallType=Boot when already set — no additional Boot guard needed in glossary loop"
  - "Settings.razor CountAffectedModules reuses injected ConfigService (IAnimaModuleConfigService) already present for Sedimentation config rather than adding a new ModuleConfig inject"

patterns-established:
  - "Boot priority pattern: seed boot nodes first into deduplication dictionary before any other recall type"
  - "Merge reason format: '{existing.Reason} + disclosure' preserves chain-of-custody for multi-source nodes"

requirements-completed: [MEMR-01, PROV-03, PROV-04]

# Metrics
duration: 19min
completed: 2026-03-23
---

# Phase 57 Plan 01: Integration Wiring and Metadata Fixes Summary

**Boot recall wired into MemoryRecallService via QueryByPrefixAsync("core://") with priority preservation across Disclosure/Glossary merges, and Settings.razor provider impact dialogs replaced hardcoded 0 with real module count from AnimaManager**

## Performance

- **Duration:** 19 min
- **Started:** 2026-03-23T00:40:33Z
- **Completed:** 2026-03-23T01:00:19Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments

- MemoryRecallService.RecallAsync now queries core:// prefix nodes unconditionally before disclosure/glossary logic, seeding them as RecallType="Boot" with highest priority
- Boot nodes survive disclosure merge: when a node appears in both PrefixNodes and DisclosureNodes, it retains RecallType="Boot" with merged reason "boot + disclosure"
- Boot nodes survive glossary merge: the existing glossary `with` expression naturally preserves RecallType="Boot" without extra guard code
- LLMModule boot-memory XML section is now populated when core:// nodes exist (dead code activated)
- Settings.razor disable/delete confirm dialogs now show real count of LLM modules bound to the provider being disabled/deleted
- 4 new tests added and all 603 tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Add boot recall to MemoryRecallService and unit tests (TDD)** - `55104a4` (feat)
2. **Task 2: Compute real provider impact counts in Settings.razor** - `5b89a46` (feat, committed in Phase 56 run)

## Files Created/Modified

- `src/OpenAnima.Core/Memory/MemoryRecallService.cs` - Added QueryByPrefixAsync("core://") boot query at top of RecallAsync, boot seeding block, disclosure merge guard, updated log message with boot count
- `tests/OpenAnima.Tests/Unit/MemoryRecallServiceTests.cs` - Added 3 new tests: RecallAsync_BootNodes_ReturnedWithBootRecallType, RecallAsync_BootNodeNotOverwrittenByDisclosure, RecallAsync_BootNodeNotOverwrittenByGlossary
- `tests/OpenAnima.Tests/Unit/LLMModuleMemoryTests.cs` - Added ExecuteWithMessages_BootNodes_AppearInBootMemoryXmlSection test

## Decisions Made

- Boot recall seeded at top of RecallAsync before Disclosure seeding — ensures byUri dictionary starts with Boot entries so the disclosure foreach sees them and merges instead of overwriting
- Glossary merge block uses `with` expression on the existing entry which naturally preserves RecallType=Boot when already set — no additional Boot guard needed in the glossary loop (confirmed by test)
- Settings.razor CountAffectedModules reuses already-injected ConfigService rather than adding a duplicate ModuleConfig inject — the existing IAnimaModuleConfigService has the GetConfig method needed

## Deviations from Plan

None — plan executed exactly as written. The Settings.razor changes (Task 2) were discovered to already be committed as part of Phase 56 execution (commit 5b89a46), so no duplicate commit was needed. All acceptance criteria verified as met.

## Issues Encountered

- The `UnloadModule_ReleasesMemory_After100Cycles` test is GC-timing-sensitive and shows occasional flakiness when 4 new tests are added to the full suite run. The test passes consistently in isolation. This is a pre-existing issue not introduced by this plan.

## Next Phase Readiness

- Boot memory recall is fully wired: MemoryRecallService produces Boot nodes, LLMModule renders them in `<boot-memory>` XML, boot nodes survive all merge paths
- Provider disable/delete impact dialogs are accurate — users see real affected module counts
- All 603 tests green (603 is baseline + 4 new boot recall tests)

---
*Phase: 57-integration-wiring-metadata-fixes*
*Completed: 2026-03-23*
