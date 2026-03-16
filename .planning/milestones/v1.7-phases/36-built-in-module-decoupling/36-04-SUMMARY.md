---
phase: 36-built-in-module-decoupling
plan: 04
subsystem: api
tags: [contracts, llm, cli, templates, tests]

# Dependency graph
requires:
  - phase: 36-02
    provides: "Contracts-first metadata/config/context pattern for built-in module files"
  - phase: 36-03
    provides: "Routing and HTTP migrations that isolate LLMModule as the only intentional Core-facing exception"

provides:
  - "LLMModule now uses Contracts metadata/config/context/routing surfaces while retaining only the documented OpenAnima.Core.LLM dependency"
  - "`oani new` now generates Contracts-only module source using ModuleMetadataRecord directly"
  - "OpenAnima.Cli.Tests has a deterministic console-capture baseline again, with no accepted false failures"

affects:
  - "Phase 36 Plan 05 source-level decoupling audit, DI verification, and full-suite proof"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Serialize in-process CLI console capture behind a shared lock when tests mutate Console.Out and Console.Error"
    - "Generated module scaffolds should construct ModuleMetadataRecord directly instead of emitting per-module metadata helper classes"

key-files:
  created: []
  modified:
    - src/OpenAnima.Core/Modules/LLMModule.cs
    - src/OpenAnima.Cli/Templates/module-cs.tmpl
    - tests/OpenAnima.Cli.Tests/CliFoundationTests.cs

key-decisions:
  - "Keep OpenAnima.Core.LLM as the only remaining Core import in LLMModule and do not expand this plan into a broader LLM service migration"
  - "Repair the CLI baseline by centralizing console capture and disabling test parallelization instead of weakening pack/validate/new assertions"

patterns-established:
  - "Inside OpenAnima.Core.Modules, bind metadata explicitly to OpenAnima.Contracts.ModuleMetadataRecord until the temporary Core shim can be removed"
  - "CLI scaffolding should stay minimal: Contracts-only imports, direct ModuleMetadataRecord construction, and no generated OpenAnima.Core references"

requirements-completed: [DECPL-04, DECPL-05]

# Metrics
duration: 67min
completed: 2026-03-15
---

# Phase 36 Plan 04: Built-in Module Decoupling Summary

**`LLMModule` is now Contracts-first everywhere except `OpenAnima.Core.LLM`, and the CLI template/test baseline now matches that Contracts-only module shape**

## Performance

- **Duration:** 67 min
- **Started:** 2026-03-15T17:36:22Z
- **Completed:** 2026-03-15T18:43:36Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments

- Moved `LLMModule` from Core config/context/routing imports to `IModuleConfig`, `IModuleContext`, `OpenAnima.Contracts.Routing`, and explicit `OpenAnima.Contracts.ModuleMetadataRecord`, while keeping `OpenAnima.Core.LLM` as the only planned exception
- Updated `oani new` module scaffolding to construct `ModuleMetadataRecord` directly and removed the generated per-module metadata helper class
- Repaired the CLI test baseline by centralizing stdout/stderr capture, serializing console mutation, and strengthening template/new-command assertions around the Contracts-only output

## Task Commits

Each task was committed atomically:

1. **Task 1: Make `LLMModule` Contracts-first except for the documented Core.LLM surface** - `1d5c577` (feat)
2. **Task 2: Modernize the module template and restore the CLI test baseline** - `a90b396` (feat)

## Files Created/Modified

- `src/OpenAnima.Core/Modules/LLMModule.cs` - now uses Contracts config/context/routing surfaces and explicit Contracts metadata binding
- `src/OpenAnima.Cli/Templates/module-cs.tmpl` - now generates direct `ModuleMetadataRecord` construction with no inline metadata class
- `tests/OpenAnima.Cli.Tests/CliFoundationTests.cs` - now centralizes safe console capture, disables assembly-level parallelization, and asserts Contracts-only generated output

## Decisions Made

- `module-csproj.tmpl` needed no changes because it already kept the generated project Contracts-only; only the generated C# shape and assertions required updates
- The CLI fix should serialize access to `Console.SetOut` and `Console.SetError` across the assembly because `Program.Main` is exercised in-process by multiple command test classes

## Deviations from Plan

None - plan executed as written.

## Issues Encountered

- The pre-fix CLI baseline had false failures caused by unsafe shared console capture (`Console.SetOut`/`Console.SetError`) and disposed writer reuse. Centralizing capture in a shared helper resolved the issue without deleting or weakening the affected command assertions.

## Self-Check: PASSED

Key files and commits verified:
- `src/OpenAnima.Core/Modules/LLMModule.cs` - FOUND
- `tests/OpenAnima.Cli.Tests/CliFoundationTests.cs` - FOUND
- Commit `1d5c577` - FOUND
- Commit `a90b396` - FOUND
- `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~ModuleTests|FullyQualifiedName~ModulePipelineIntegrationTests|FullyQualifiedName~PromptInjectionIntegrationTests|FullyQualifiedName~ChatPanelModulePipelineTests" -v minimal` passed (34/34)
- `dotnet test tests/OpenAnima.Cli.Tests/OpenAnima.Cli.Tests.csproj -v minimal` passed (76/76)

## User Setup Required

None - no new runtime configuration or external services are required.

## Next Phase Readiness

- The last remaining source-level Core exception is now isolated to `OpenAnima.Core.LLM`, so Plan 05 can codify that policy directly in audit tests
- The CLI suite is green again, which removes the final blocker for an honest full-suite verification pass in the last wave

---
*Phase: 36-built-in-module-decoupling*
*Completed: 2026-03-15*
