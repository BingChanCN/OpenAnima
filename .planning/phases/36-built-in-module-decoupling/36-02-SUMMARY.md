---
phase: 36-built-in-module-decoupling
plan: 02
subsystem: api
tags: [contracts, modules, chat, heartbeat, text, branching]

# Dependency graph
requires:
  - phase: 36-01
    provides: "Contracts-owned ModuleMetadataRecord and the canonical Phase 36 inventory baseline"

provides:
  - "All seven low-risk built-in module files now use Contracts-first module-facing surfaces"
  - "Text/branch utility modules now consume IModuleConfig and IModuleContext directly"
  - "ChatInputModule remains a direct EventBus publisher with no dead channel-host path"

affects:
  - "Phase 36 Plan 03 routing and HTTP module migration"
  - "Phase 36 Plan 05 source-level decoupling audit"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Contracts-first module migration with explicit OpenAnima.Contracts.ModuleMetadataRecord construction"
    - "Audit-first task execution when part of the cohort is already aligned"

key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Modules/FixedTextModule.cs
    - src/OpenAnima.Core/Modules/TextJoinModule.cs
    - src/OpenAnima.Core/Modules/TextSplitModule.cs
    - src/OpenAnima.Core/Modules/ConditionalBranchModule.cs

key-decisions:
  - "Leave ChatInputModule, ChatOutputModule, and HeartbeatModule unchanged once audit confirmed they already matched the Contracts-first target"
  - "Use explicit OpenAnima.Contracts.ModuleMetadataRecord construction inside OpenAnima.Core.Modules files to avoid resolving the temporary Core shim"

patterns-established:
  - "When a cohort audit shows some files already satisfy the plan target, keep the commit surface minimal and only change the true deltas"
  - "Text/branch modules can migrate to IModuleConfig and IModuleContext without any behavior rewrite or test fixture churn"

requirements-completed: [DECPL-01]

# Metrics
duration: 36min
completed: 2026-03-15
---

# Phase 36 Plan 02: Built-in Module Decoupling Summary

**The low-risk built-in cohort now uses Contracts-first module-facing surfaces, with the text and branch utilities moved to `IModuleConfig`/`IModuleContext` and the chat-heartbeat files confirmed already aligned**

## Performance

- **Duration:** 36 min
- **Started:** 2026-03-15T16:35:00Z
- **Completed:** 2026-03-15T17:11:18Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- Audited the seven-file low-risk cohort and confirmed `ChatInputModule`, `ChatOutputModule`, and `HeartbeatModule` already satisfied the target state: no Core module-facing imports, no dead channel-host path, and Contracts metadata construction
- Moved `FixedTextModule`, `TextJoinModule`, `TextSplitModule`, and `ConditionalBranchModule` to `IModuleConfig` and `IModuleContext`
- Bound the changed files directly to `OpenAnima.Contracts.ModuleMetadataRecord` and kept all targeted module regression tests green

## Task Commits

Each task was committed atomically when source changes were required:

1. **Task 1: Audit source/sink/heartbeat modules and confirm target state** - No source delta required after audit
2. **Task 2: Move text and branch utility modules to Contracts config/context surfaces** - `02cb6cb` (feat)

## Files Created/Modified

- `src/OpenAnima.Core/Modules/FixedTextModule.cs` - now uses `IModuleConfig`, `IModuleContext`, and Contracts metadata directly
- `src/OpenAnima.Core/Modules/TextJoinModule.cs` - now uses Contracts config/context types and Contracts metadata
- `src/OpenAnima.Core/Modules/TextSplitModule.cs` - now uses Contracts config/context types and Contracts metadata
- `src/OpenAnima.Core/Modules/ConditionalBranchModule.cs` - now uses Contracts config/context types and Contracts metadata

## Decisions Made

- The chat and heartbeat modules were left untouched once the audit showed the plan targets were already true in the current codebase
- Only the text/branch files changed, keeping the cohort migration narrow and behavior-preserving

## Deviations from Plan

None - plan executed as written. The Task 1 audit simply found that no additional source edit was needed for the three source/sink/heartbeat files.

## Issues Encountered

None

## Self-Check: PASSED

Key files and commits verified:
- `src/OpenAnima.Core/Modules/FixedTextModule.cs` - FOUND
- `src/OpenAnima.Core/Modules/ConditionalBranchModule.cs` - FOUND
- Commit `02cb6cb` - FOUND
- `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~ModuleTests|FullyQualifiedName~ModulePipelineIntegrationTests" -v minimal` passed (24/24)
- `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~ConcurrencyGuardTests|FullyQualifiedName~ActivityChannelSoakTests|FullyQualifiedName~ModulePipelineIntegrationTests" -v minimal` passed (9/9)

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The non-routing low-risk cohort is now fully Contracts-first at the source-file level
- Routing and HTTP modules can follow the same metadata/config/context pattern without needing new helper surfaces

---
*Phase: 36-built-in-module-decoupling*
*Completed: 2026-03-15*
