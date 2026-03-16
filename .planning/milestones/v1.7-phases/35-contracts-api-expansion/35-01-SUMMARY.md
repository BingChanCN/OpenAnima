---
phase: 35-contracts-api-expansion
plan: 01
subsystem: api
tags: [contracts, interfaces, routing, config-schema, module-sdk]

# Dependency graph
requires:
  - phase: 34-activity-channel-model
    provides: Stable AnimaRuntime with 266/266 tests green baseline
provides:
  - IModuleConfig interface (module-facing config read/write with per-key SetConfigAsync)
  - IModuleContext interface (non-nullable ActiveAnimaId + ActiveAnimaChanged event)
  - IModuleConfigSchema interface (optional self-describing config schema)
  - ConfigFieldType enum (8 values)
  - ConfigFieldDescriptor record (10 properties)
  - ICrossAnimaRouter interface in Contracts.Routing (full 7-method surface + IDisposable)
  - PortRegistration record in Contracts.Routing
  - RouteResult record + RouteErrorKind enum in Contracts.Routing
  - RouteRegistrationResult record in Contracts.Routing
affects: [35-02-core-shims-di-wiring, 35-03-canary-test, phase-36-module-migration]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Sub-namespace pattern for grouped types: OpenAnima.Contracts.Routing mirrors OpenAnima.Contracts.Ports"
    - "Contracts isolation: zero PackageReference/ProjectReference — BCL primitives only"
    - "Static factory methods on record types (Ok/Failed/NotFound, Success/DuplicateError)"

key-files:
  created:
    - src/OpenAnima.Contracts/IModuleConfig.cs
    - src/OpenAnima.Contracts/IModuleContext.cs
    - src/OpenAnima.Contracts/IModuleConfigSchema.cs
    - src/OpenAnima.Contracts/ConfigFieldType.cs
    - src/OpenAnima.Contracts/ConfigFieldDescriptor.cs
    - src/OpenAnima.Contracts/Routing/ICrossAnimaRouter.cs
    - src/OpenAnima.Contracts/Routing/PortRegistration.cs
    - src/OpenAnima.Contracts/Routing/RouteResult.cs
    - src/OpenAnima.Contracts/Routing/RouteRegistrationResult.cs
  modified: []

key-decisions:
  - "IModuleConfig.SetConfigAsync uses per-key (string key, string value) signature, NOT bulk Dictionary — locked user decision"
  - "IModuleContext.ActiveAnimaId is non-nullable string (not string?) — platform guarantees initialization before module use"
  - "ICrossAnimaRouter full 7-method surface retained in Contracts — no stripping of platform-internal methods"
  - "ConfigFieldType has exactly 8 values: String, Int, Bool, Enum, Secret, MultilineText, Dropdown, Number"
  - "ConfigFieldDescriptor has exactly 10 properties including optional EnumValues, Group, ValidationPattern"
  - "Routing companion types in Contracts.Routing sub-namespace (parallel to existing Contracts.Ports pattern)"
  - "RouteErrorKind enum co-located in RouteResult.cs matching Core pattern (same-file enum + record)"

patterns-established:
  - "Contracts.Routing sub-namespace: new routing interfaces and types go here, not root namespace"
  - "All Contracts types use BCL primitives only — zero external dependencies"
  - "XML doc comments on all public types and members with <param> tags on records"

requirements-completed: [API-01, API-02, API-03, API-04]

# Metrics
duration: 4min
completed: 2026-03-15
---

# Phase 35 Plan 01: Contracts API Interface Definitions Summary

**9 new contract types in OpenAnima.Contracts — IModuleConfig, IModuleContext, IModuleConfigSchema (with ConfigFieldType + ConfigFieldDescriptor), and full ICrossAnimaRouter + 3 routing companion types in Contracts.Routing — all zero-dependency, builds in isolation**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-15T12:09:58Z
- **Completed:** 2026-03-15T12:13:37Z
- **Tasks:** 2
- **Files modified:** 9 created

## Accomplishments

- Defined all module-facing config/context interfaces in OpenAnima.Contracts root namespace
- Created IModuleConfigSchema with supporting ConfigFieldType (8-value enum) and ConfigFieldDescriptor (10-property record) for optional self-describing config
- Promoted full ICrossAnimaRouter surface and all companion types to OpenAnima.Contracts.Routing namespace, establishing the sub-namespace pattern parallel to existing Contracts.Ports
- Contracts project builds in complete isolation (no ProjectReference, no PackageReference) — 266/266 tests unaffected

## Task Commits

Each task was committed atomically:

1. **Task 1: Define IModuleConfig, IModuleContext, IModuleConfigSchema contracts** - `45b3f51` (feat)
2. **Task 2: Define routing types in Contracts.Routing namespace** - `eb5331e` (feat)

**Plan metadata:** `a1d8aa9` (docs: complete plan)

## Files Created/Modified

- `src/OpenAnima.Contracts/IModuleConfig.cs` - Module-facing config read/write (per-key SetConfigAsync)
- `src/OpenAnima.Contracts/IModuleContext.cs` - Read-only Anima identity (non-nullable ActiveAnimaId)
- `src/OpenAnima.Contracts/IModuleConfigSchema.cs` - Optional self-describing config schema interface
- `src/OpenAnima.Contracts/ConfigFieldType.cs` - 8-value enum for field rendering hints
- `src/OpenAnima.Contracts/ConfigFieldDescriptor.cs` - 10-property record with full field metadata
- `src/OpenAnima.Contracts/Routing/ICrossAnimaRouter.cs` - Full 7-method routing interface + IDisposable
- `src/OpenAnima.Contracts/Routing/PortRegistration.cs` - Port registration record (AnimaId, PortName, Description)
- `src/OpenAnima.Contracts/Routing/RouteResult.cs` - Result record + RouteErrorKind enum with static factories
- `src/OpenAnima.Contracts/Routing/RouteRegistrationResult.cs` - Registration result record with static factories

## Decisions Made

- IModuleConfig.SetConfigAsync is per-key `(string animaId, string moduleId, string key, string value)` — not the bulk Dictionary form used by IAnimaModuleConfigService. This was a locked user decision.
- IModuleContext.ActiveAnimaId is `string` (non-nullable) — platform guarantees this is set before module initialization
- ICrossAnimaRouter retains its full 7-method surface in Contracts — no methods stripped for being "platform-internal"
- RouteErrorKind enum co-located in RouteResult.cs following the Core pattern (avoids extra file for a small enum)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All 9 contract types exist in OpenAnima.Contracts with correct namespaces and signatures
- Contracts project builds in isolation with zero dependencies
- 266/266 tests pass — Core is untouched, no regressions
- Plan 02 (Core shims + DI wiring) can now proceed: Core implementations will implement the new Contracts interfaces, and type-forward aliases will be added in the old Core namespaces for binary compatibility

---
*Phase: 35-contracts-api-expansion*
*Completed: 2026-03-15*

## Self-Check: PASSED

- All 9 source files exist at correct paths
- Both task commits verified (45b3f51, eb5331e)
- Contracts builds in isolation with 0 errors, 0 warnings
- 266/266 tests pass (no Core changes)
- SUMMARY.md created, STATE.md updated, ROADMAP.md updated, requirements marked complete
