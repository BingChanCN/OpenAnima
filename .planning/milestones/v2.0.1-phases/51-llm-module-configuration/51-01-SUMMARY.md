---
phase: 51-llm-module-configuration
plan: 01
subsystem: llm
tags: [llm, provider-registry, config-schema, cascading-dropdown, tdd, unit-tests]

# Dependency graph
requires:
  - phase: 50-provider-registry
    provides: LLMProviderRegistryService with GetDecryptedApiKey, ILLMProviderRegistry interface, LLMProviderInfo/LLMModelInfo DTOs

provides:
  - ConfigFieldType.CascadingDropdown enum value for two-tier provider/model UI rendering
  - LLMModule.GetSchema() returning 5 fields (llmProviderSlug, llmModelId, apiUrl, apiKey, modelName)
  - LLMModule.CallLlmAsync three-layer precedence (provider-backed > manual > global)
  - Auto-clear on deleted provider (removes both llmProviderSlug + llmModelId)
  - Auto-clear on deleted model (removes only llmModelId, retains llmProviderSlug)
  - __manual__ sentinel bypass for legacy apiUrl/apiKey/modelName config path
  - 14 unit tests covering all LLMN requirements including deleted-model auto-clear
  - NullLLMProviderRegistry + NullRegistryServiceFactory test helpers

affects:
  - 51-02 (UI sidebar plan needs GetSchema() and CascadingDropdown to render)
  - Future modules implementing IModuleConfigSchema (established pattern)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - IModuleConfigSchema.GetSchema() as the standard module config contract
    - ClearProviderSelectionAsync / ClearModelSelectionAsync for stale config auto-repair
    - NullLLMProviderRegistry + NullRegistryServiceFactory for integration test isolation
    - CapturingEventBus pattern for testing modules that subscribe to event ports

key-files:
  created:
    - tests/OpenAnima.Tests/Unit/LLMModuleProviderConfigTests.cs
    - tests/OpenAnima.Tests/TestHelpers/NullLLMProviderRegistry.cs
  modified:
    - src/OpenAnima.Contracts/ConfigFieldType.cs
    - src/OpenAnima.Core/Modules/LLMModule.cs
    - tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs
    - tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs
    - tests/OpenAnima.Tests/Unit/ContractsApiTests.cs

key-decisions:
  - "LLMModule constructor upgraded from IModuleConfig to IAnimaModuleConfigService to enable SetConfigAsync(dict) for auto-clear logic"
  - "Auto-clear uses GetConfig then Remove then SetConfigAsync(dict) pattern — not individual key deletion"
  - "Re-read config after ClearProviderSelectionAsync / ClearModelSelectionAsync to get fresh state for Layer 2"
  - "NullRegistryServiceFactory uses Lazy<T> singleton with shared temp dir for integration test isolation"
  - "BuiltInModuleDecouplingTests updated to document Core.Providers + Core.Services as intentional Phase 51 exceptions"
  - "ContractsApiTests: ConfigFieldType enum count updated from 8 to 9 (CascadingDropdown added)"

patterns-established:
  - "CapturingEventBus: subscribe to messages port handler directly for unit-testable LLM routing"
  - "NullLLMProviderRegistry.Instance: drop-in for tests that don't exercise provider-backed config"

requirements-completed: [LLMN-01, LLMN-02, LLMN-03, LLMN-04, LLMN-05]

# Metrics
duration: 23min
completed: 2026-03-22
---

# Phase 51 Plan 01: LLM Module Configuration Summary

**ConfigFieldType.CascadingDropdown + LLMModule GetSchema() with three-layer provider/manual/global CallLlmAsync precedence and stale-config auto-clear**

## Performance

- **Duration:** 23 min
- **Started:** 2026-03-22T12:06:14Z
- **Completed:** 2026-03-22T12:29:00Z
- **Tasks:** 2 (TDD: RED + GREEN)
- **Files modified:** 13

## Accomplishments
- Added `ConfigFieldType.CascadingDropdown` for two-tier provider/model UI rendering (Plan 02 dependency)
- Implemented `IModuleConfigSchema` on `LLMModule` with `GetSchema()` returning 5 fields in two groups (provider: orders 0-1, manual: orders 10-12)
- Extended `CallLlmAsync` with deterministic three-layer resolution: provider-backed (ILLMProviderRegistry + GetDecryptedApiKey) > manual per-Anima > global ILLMService
- Auto-clear on deleted provider removes both `llmProviderSlug` + `llmModelId`; deleted model removes only `llmModelId` (retaining the user's provider choice)
- 14 unit tests (all passing), full suite 537 tests green

## Task Commits

Each task was committed atomically:

1. **Task 1: Add CascadingDropdown + RED test scaffold** - `e4a6e46` (test)
2. **Task 2: Implement IModuleConfigSchema + provider precedence** - `71a733c` (feat)

## Files Created/Modified
- `/home/user/OpenAnima/src/OpenAnima.Contracts/ConfigFieldType.cs` - Added CascadingDropdown enum value after Number
- `/home/user/OpenAnima/src/OpenAnima.Core/Modules/LLMModule.cs` - IModuleConfigSchema implementation, new constructor params, three-layer CallLlmAsync, ClearProvider/ClearModel helpers
- `/home/user/OpenAnima/tests/OpenAnima.Tests/Unit/LLMModuleProviderConfigTests.cs` - 14 [Fact] tests with FakeLLMService, FakeAnimaModuleConfigService, FakeModuleContext, CapturingEventBus
- `/home/user/OpenAnima/tests/OpenAnima.Tests/TestHelpers/NullLLMProviderRegistry.cs` - NullLLMProviderRegistry + NullRegistryServiceFactory for integration tests
- Integration tests updated (6 files): added NullLLMProviderRegistry.Instance + NullRegistryServiceFactory.Instance to existing LLMModule constructor calls
- `ContractsApiTests.cs` - Updated enum count assertion 8 -> 9
- `BuiltInModuleDecouplingTests.cs` - Updated LLMModule Core-using allowlist to include Providers + Services
- `ModuleRuntimeInitializationTests.cs` - Added LLMProviderRegistryService + ILLMProviderRegistry DI registrations

## Decisions Made
- Changed `IModuleConfig configService` to `IAnimaModuleConfigService configService` in constructor to access `SetConfigAsync(animaId, moduleId, Dictionary<string, string>)` for auto-clear logic (the dict overload is on `IAnimaModuleConfigService`, not `IModuleConfig`)
- Re-read config after provider/model clear operations rather than mutating in-place to ensure fresh state for Layer 2 resolution
- Used `NullRegistryServiceFactory` with `Lazy<T>` singleton pattern for integration tests that need the concrete type but don't require any providers
- `ClearModelSelectionAsync` retains `llmProviderSlug` to preserve user's provider choice — only the model binding is stale

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed interface mismatches in existing integration tests**
- **Found during:** Task 2 (GREEN phase - building test suite)
- **Issue:** 8 integration/unit test files constructed `LLMModule` with old constructor (missing `providerRegistry` + `registryService` parameters), blocking compilation
- **Fix:** Added `NullLLMProviderRegistry.Instance, NullRegistryServiceFactory.Instance` to each construction site; added `using OpenAnima.Core.Providers;` where needed; created `NullLLMProviderRegistry.cs` test helper
- **Files modified:** 6 integration test files + 2 unit test files + new TestHelpers file
- **Committed in:** 71a733c (Task 2 commit)

**2. [Rule 1 - Bug] Updated ConfigFieldType enum count contract test**
- **Found during:** Task 2 (full suite run)
- **Issue:** `ContractsApiTests.ConfigFieldType_Has_Exactly_Eight_Values` failed because the intentional addition of `CascadingDropdown` made it 9
- **Fix:** Updated assertion to 9 and renamed test to `ConfigFieldType_Has_Exactly_Nine_Values`
- **Files modified:** tests/OpenAnima.Tests/Unit/ContractsApiTests.cs
- **Committed in:** 71a733c (Task 2 commit)

**3. [Rule 1 - Bug] Updated LLMModule Core-using allowlist in decoupling test**
- **Found during:** Task 2 (full suite run)
- **Issue:** `BuiltInModuleDecouplingTests` failed because it expected only `Core.LLM` but now `Core.Providers` and `Core.Services` are intentionally added by Phase 51
- **Fix:** Updated the test to allow exactly `{Core.LLM, Core.Providers, Core.Services}` with named documentation
- **Files modified:** tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs
- **Committed in:** 71a733c (Task 2 commit)

**4. [Rule 3 - Blocking] Added provider registry DI registrations to ModuleRuntimeInitializationTests**
- **Found during:** Task 2 (full suite run)
- **Issue:** `ModuleRuntimeInitializationTests.BuiltInModules_AllResolveFromTheRealDIContainer` failed because the test's DI container didn't register `ILLMProviderRegistry` / `LLMProviderRegistryService`
- **Fix:** Added both registrations using a temp providers directory, consistent with `AddProviderServices()` pattern
- **Files modified:** tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs
- **Committed in:** 71a733c (Task 2 commit)

---

**Total deviations:** 4 auto-fixed (1 blocking constructor change propagation, 1 contract test update, 1 decoupling allowlist update, 1 DI test registration)
**Impact on plan:** All auto-fixes were direct consequences of the planned constructor change. No scope creep.

## Issues Encountered
- `MemoryLeakTests.UnloadModule_ReleasesMemory_After100Cycles` showed 1 intermittent failure (GC-based, non-deterministic). Re-running the suite showed all 537 tests pass consistently. Pre-existing flaky behavior unrelated to Phase 51 changes.

## Next Phase Readiness
- `ConfigFieldType.CascadingDropdown` is ready for Plan 02 UI sidebar rendering
- `IModuleConfigSchema` with GetSchema() is ready for sidebar auto-render integration
- Three-layer precedence is live — selecting a provider+model in sidebar will immediately affect LLM routing
- Auto-clear logic runs on the first execution after provider/model deletion, no manual cleanup needed
