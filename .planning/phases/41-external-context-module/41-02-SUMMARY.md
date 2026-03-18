---
phase: 41-external-context-module
plan: 02
subsystem: external-modules
tags: [context-module, conversation-history, imodule, ieventbus, imodulestorage, oamod, tdd]

dependency_graph:
  requires:
    - phase: 41-01
      provides: bound-IModuleStorage-per-external-module
    - phase: 40-module-storage-path
      provides: ModuleStorageService with CreateBound factory
    - phase: 39-contracts-type-migration-structured-messages
      provides: ChatMessageInput with SerializeList/DeserializeList
  provides:
    - working-ContextModule-external-module
    - oamod-packaging-via-msbuild
    - ectx-01-ectx-02-validated
  affects: [v1.8-sdk-runtime-parity, external-module-authors]

tech-stack:
  added: [Microsoft.Extensions.Logging.Abstractions 8.0.0 (ContextModule project)]
  patterns:
    - lock(_history) for thread-safe history mutations, no lock held during async I/O
    - BuildOutputList copies history snapshot before prepending system message
    - CopyToOutputDirectory=PreserveNewest for module.json alongside DLL
    - gitignore negation pattern to track module source while ignoring build output

key-files:
  created:
    - modules/ContextModule/ContextModule.cs
    - modules/ContextModule/ContextModule.csproj
    - modules/ContextModule/module.json
    - tests/OpenAnima.Tests/Integration/ContextModuleTests.cs
  modified:
    - .gitignore (untrack modules/ build output, track ContextModule source)

key-decisions:
  - "module.json CopyToOutputDirectory=PreserveNewest — PluginLoader needs manifest alongside DLL in build output"
  - "Microsoft.Extensions.Logging.Abstractions added as Private=false — ILogger optional param, not bundled in .oamod"
  - "gitignore negation: modules/ ignored as build output, explicit !modules/ContextModule/ exceptions for source tracking"
  - "ContextModule tests load from build output dir (not .oamod) — avoids build dependency fragility in test suite"

patterns-established:
  - "External module pattern: IModule + port attributes + optional DI params + lock for thread safety"
  - "MSBuild PackageOamod: stage DLL+manifest, ZipDirectory, copy to Core/modules/"

requirements-completed: [ECTX-01, ECTX-02]

duration: 16min
completed: 2026-03-18
---

# Phase 41 Plan 02: ContextModule External Module Summary

**External ContextModule with multi-turn history, history.json persistence, system message injection, and per-Anima isolation — capstone of v1.8 SDK Runtime Parity**

## Performance

- **Duration:** 16 min
- **Started:** 2026-03-18T15:10:04Z
- **Completed:** 2026-03-18T15:26:00Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments

- ContextModule IModule implementation with userMessage/llmResponse input ports and messages/displayHistory output ports
- history.json persistence via bound IModuleStorage, restored on re-initialization
- System message prepended from IModuleConfig per call (not persisted to history)
- MSBuild PackageOamod target produces ContextModule.oamod and copies to Core/modules/
- 7 integration tests covering ECTX-01 and ECTX-02, all passing (389 total suite green)

## Task Commits

1. **Task 1: ContextModule project and implementation** - `abfa77c` (feat)
2. **Task 2 RED: Failing integration tests** - `1e98cad` (test)
3. **Task 2 GREEN: module.json to build output** - `26599fb` (feat)

## Files Created/Modified

- `modules/ContextModule/ContextModule.cs` - IModule implementation with history management
- `modules/ContextModule/ContextModule.csproj` - net8.0 project with PackageOamod MSBuild target
- `modules/ContextModule/module.json` - Module manifest with port declarations
- `tests/OpenAnima.Tests/Integration/ContextModuleTests.cs` - 7 integration tests (ECTX-01, ECTX-02)
- `.gitignore` - Track ContextModule source, ignore build output

## Decisions Made

- `module.json CopyToOutputDirectory=PreserveNewest` — PluginLoader requires manifest alongside DLL; without this, tests loading from build output dir would fail
- `Microsoft.Extensions.Logging.Abstractions` added as `<Private>false</Private>` — ILogger is optional, not bundled in .oamod to avoid type identity issues
- gitignore negation pattern (`!modules/ContextModule/`) — modules/ is build output by convention, but ContextModule source needs tracking
- Tests load from `modules/ContextModule/bin/Debug/net8.0/` not from .oamod — avoids build dependency fragility in CI

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] modules/ gitignored, blocking commit of source files**
- **Found during:** Task 1 commit
- **Issue:** `.gitignore` had `modules/` as "Modules output" — git refused to stage ContextModule source files
- **Fix:** Added negation exceptions `!modules/`, `!modules/ContextModule/`, `!modules/ContextModule/*.cs`, etc.
- **Files modified:** `.gitignore`
- **Verification:** `git add modules/ContextModule/...` succeeded after fix
- **Committed in:** abfa77c (Task 1 commit)

**2. [Rule 3 - Blocking] Microsoft.Extensions.Logging.Abstractions missing from ContextModule.csproj**
- **Found during:** Task 1 build
- **Issue:** ContextModule.cs uses `ILogger` but the package wasn't referenced; build failed with CS0234/CS0246
- **Fix:** Added `<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0">` with `<Private>false</Private>`
- **Files modified:** `modules/ContextModule/ContextModule.csproj`
- **Verification:** `dotnet build` succeeded
- **Committed in:** abfa77c (Task 1 commit)

**3. [Rule 3 - Blocking] module.json not copied to build output, PluginLoader couldn't find manifest**
- **Found during:** Task 2 RED phase
- **Issue:** PluginLoader.LoadModule requires `module.json` in the same directory as the DLL; build output only had the DLL
- **Fix:** Added `<None Include="module.json" CopyToOutputDirectory="PreserveNewest" />` to csproj
- **Files modified:** `modules/ContextModule/ContextModule.csproj`
- **Verification:** `ls bin/Debug/net8.0/` shows `module.json` alongside `ContextModule.dll`
- **Committed in:** 26599fb (Task 2 GREEN commit)

---

**Total deviations:** 3 auto-fixed (all Rule 3 blocking)
**Impact on plan:** All fixes necessary for build and test execution. No scope creep.

## Issues Encountered

- Path calculation for `ContextModuleDir` in tests was off by one level (6 levels up instead of 5). Fixed before RED commit.

## Next Phase Readiness

- v1.8 SDK Runtime Parity milestone complete — ContextModule proves the full SDK surface works for external module authors
- ECTX-01 and ECTX-02 requirements validated
- ContextModule.oamod ready for deployment to Core/modules/

---
*Phase: 41-external-context-module*
*Completed: 2026-03-18*

## Self-Check: PASSED
