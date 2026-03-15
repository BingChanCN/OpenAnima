---
phase: 36-built-in-module-decoupling
plan: 01
subsystem: api
tags: [contracts, metadata, ssrf, docs, compatibility]

# Dependency graph
requires:
  - phase: 35-03
    provides: "Contracts API surface, Core shim conventions, and ContractsApiTests baseline"

provides:
  - "Canonical planning docs aligned on the authoritative 12 active built-in modules and the documented LLMModule exception"
  - "OpenAnima.Contracts.ModuleMetadataRecord as the shared metadata implementation for built-in and generated modules"
  - "OpenAnima.Contracts.Http.SsrfGuard as the Contracts-owned SSRF helper surface"
  - "Temporary Core compatibility shims for ModuleMetadataRecord and SsrfGuard so the repo still compiles before later module migrations"
  - "Focused ContractsApiTests and SsrfGuardTests coverage proving Contracts ownership and shim behavior"

affects:
  - "Phase 36 Plan 02 source/sink/text/branch module migration"
  - "Phase 36 Plan 03 routing and HttpRequestModule migration"
  - "Phase 36 Plan 04 CLI template modernization"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Contracts-first helper promotion with temporary Core compatibility shims"
    - "Reflection-based API surface assertions for moved helper types"

key-files:
  created:
    - src/OpenAnima.Contracts/ModuleMetadataRecord.cs
    - src/OpenAnima.Contracts/Http/SsrfGuard.cs
  modified:
    - .planning/PROJECT.md
    - .planning/ROADMAP.md
    - .planning/REQUIREMENTS.md
    - src/OpenAnima.Core/Modules/ModuleMetadataRecord.cs
    - src/OpenAnima.Core/Http/SsrfGuard.cs
    - tests/OpenAnima.Tests/Unit/ContractsApiTests.cs
    - tests/OpenAnima.Tests/Unit/SsrfGuardTests.cs
    - tests/OpenAnima.Tests/Integration/ActivityChannelIntegrationTests.cs
    - .planning/STATE.md

key-decisions:
  - "Document the authoritative 12-module runtime inventory explicitly in the canonical planning docs instead of relying on an implied count"
  - "Keep Core compatibility via a derived ModuleMetadataRecord shim and a forwarding SsrfGuard shim until later plans migrate module imports directly"

patterns-established:
  - "Moved helper types land in Contracts first, then temporary Core shims preserve compile compatibility for phased module migrations"
  - "When helper-type moves surface ambiguous test imports, remove the stale Core namespace import instead of introducing dual-type assertions everywhere"

requirements-completed: [DECPL-05]

# Metrics
duration: 35min
completed: 2026-03-15
---

# Phase 36 Plan 01: Built-in Module Decoupling Summary

**Canonical Phase 36 inventory now names the real 12 active built-in modules, while `ModuleMetadataRecord` and `SsrfGuard` ship from Contracts with Core compatibility shims and focused regression coverage**

## Performance

- **Duration:** 35 min
- **Started:** 2026-03-15T15:51:10Z
- **Completed:** 2026-03-15T16:26:04Z
- **Tasks:** 2
- **Files modified:** 10

## Accomplishments

- Canonical planning docs now spell out the authoritative 12 active built-in modules, keep the helper/support distinction explicit, and preserve the documented `LLMModule` exception
- `ModuleMetadataRecord` now exists in `OpenAnima.Contracts` as the shared metadata implementation, with a temporary Core shim keeping existing source files compiling
- `SsrfGuard` now exists in `OpenAnima.Contracts.Http`, and focused unit tests prove both the Contracts helper behavior and the temporary Core shim delegation

## Task Commits

Each task was committed atomically:

1. **Task 1: Normalize the authoritative inventory and exception wording across docs** - `c6af863` (docs)
2. **Task 2: Move ModuleMetadataRecord and SsrfGuard to Contracts with compatibility shims** - `dcff71a` (feat)

## Files Created/Modified

- `src/OpenAnima.Contracts/ModuleMetadataRecord.cs` - Contracts-owned shared metadata record
- `src/OpenAnima.Contracts/Http/SsrfGuard.cs` - Contracts-owned SSRF guard helper
- `src/OpenAnima.Core/Modules/ModuleMetadataRecord.cs` - Temporary compatibility shim inheriting from the Contracts record
- `src/OpenAnima.Core/Http/SsrfGuard.cs` - Temporary compatibility shim delegating to the Contracts helper
- `tests/OpenAnima.Tests/Unit/ContractsApiTests.cs` - Contracts ownership and shim coverage for ModuleMetadataRecord
- `tests/OpenAnima.Tests/Unit/SsrfGuardTests.cs` - Contracts helper assertions plus Core shim delegation check
- `.planning/PROJECT.md` - Explicit active-module inventory note
- `.planning/ROADMAP.md` - Phase 36 inventory normalization and plan progress seed
- `.planning/REQUIREMENTS.md` - Authoritative active inventory recorded in requirements

## Decisions Made

- The canonical docs now name the 12 active built-in modules explicitly so Phase 36 can no longer drift back to the old helper-file-based count
- The Core compatibility layer remains minimal: inheritance for `ModuleMetadataRecord`, forwarding for `SsrfGuard`; later plans will move module consumers to the Contracts types directly

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] ActivityChannelIntegrationTests became ambiguous after the helper move**
- **Found during:** Task 2 verification
- **Issue:** `ActivityChannelIntegrationTests` imported both `OpenAnima.Contracts` and `OpenAnima.Core.Modules`, so moving `ModuleMetadataRecord` into Contracts made the test project fail with an ambiguous type reference
- **Fix:** Removed the stale `using OpenAnima.Core.Modules` import so the test file resolves `ModuleMetadataRecord` from Contracts
- **Files modified:** `tests/OpenAnima.Tests/Integration/ActivityChannelIntegrationTests.cs`
- **Verification:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~ContractsApiTests|FullyQualifiedName~SsrfGuardTests" -v minimal`
- **Committed in:** `dcff71a`

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** The fix was compile-only and directly caused by the helper move. No scope creep.

## Issues Encountered

- `dotnet test /home/user/OpenAnima/tests/OpenAnima.Tests/ -q --filter "FullyQualifiedName~ContractsApiTests|FullyQualifiedName~SsrfGuardTests"` produced false MSBuild failures under the .NET 10 SDK (`Building target "CoreCompile" completely`) even after successful builds. Re-running the same target with normal verbosity completed successfully and the 71 targeted tests passed.

## Self-Check: PASSED

Key files and commits verified:
- `src/OpenAnima.Contracts/ModuleMetadataRecord.cs` - FOUND
- `src/OpenAnima.Contracts/Http/SsrfGuard.cs` - FOUND
- Commit `c6af863` - FOUND
- Commit `dcff71a` - FOUND
- Targeted verification passed via `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~ContractsApiTests|FullyQualifiedName~SsrfGuardTests" -v minimal` (71/71)

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The helper surfaces required by the built-in module cohorts now exist in Contracts, so Plans 02 and 03 can switch module files without inventing new abstractions
- `HttpRequestModule` can now consume `OpenAnima.Contracts.Http.SsrfGuard` directly in Plan 03
- Future Phase 36 verification should avoid `dotnet test -q` on `OpenAnima.Tests` until the SDK quiet-mode false failure is understood

---
*Phase: 36-built-in-module-decoupling*
*Completed: 2026-03-15*
