---
phase: 30-prompt-injection-and-format-detection
plan: 02
subsystem: modules
tags: [llm, routing, format-detection, prompt-injection, self-correction, tdd, event-bus]

# Dependency graph
requires:
  - phase: 30-01
    provides: FormatDetector.Detect(), RouteExtraction, FormatDetectionResult — consumed by LLMModule
  - phase: 29-routing-modules
    provides: AnimaRouteModule with request/trigger input ports; AnimaOutputPortModule

provides:
  - Extended LLMModule with system message injection (ICrossAnimaRouter, IAnimaModuleConfigService)
  - FormatDetector integration inside LLMModule.ExecuteAsync()
  - Self-correction retry loop (MaxRetries=2) with correction feedback message
  - Error output port (LLMModule.port.error) for exhausted-retry errors
  - PromptInjectionIntegrationTests: 7 integration tests covering all prompt injection scenarios
  - Backward compatibility: ICrossAnimaRouter? = null preserves original LLMModule behavior

affects:
  - Phase 31 (final wiring) — LLMModule now auto-routes based on AnimaRoute config
  - WiringInitializationService tests — LLMModule now has 3 ports (prompt, response, error)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Optional ICrossAnimaRouter? router = null for backward-compatible DI injection"
    - "Self-correction loop pattern: append assistant+user correction turns, re-call LLM up to MaxRetries"
    - "Format: request event BEFORE trigger event — AnimaRouteModule buffers payload first"
    - "BuildKnownServiceNames() queried per-ExecuteAsync (not cached) to reflect live config"
    - "Correction message includes both error reason AND format example for effective self-correction"

key-files:
  created:
    - tests/OpenAnima.Tests/Integration/PromptInjectionIntegrationTests.cs
  modified:
    - src/OpenAnima.Core/Modules/LLMModule.cs
    - tests/OpenAnima.Tests/Modules/ModuleTests.cs
    - tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs

key-decisions:
  - "BuildKnownServiceNames queries IAnimaModuleConfigService on every ExecuteAsync call — not cached at init (per RESEARCH.md anti-pattern warning)"
  - "Self-correction messages are ephemeral: correction turns appended to currentMessages list, never persisted to ChatContextManager"
  - "Correction message includes error reason AND format example — both required for effective LLM self-correction"
  - "No token budget cap: all configured services injected in system message (PROMPT-02 per user decision)"
  - "System message example uses first registered port name — representative for multi-service instruction"

patterns-established:
  - "TDD RED-GREEN for integration-heavy module extension: write event-based integration tests first"
  - "CapturingFakeLlmService pattern: captures message list + call count for integration test assertions"
  - "PresetAnimaModuleConfigService: test helper for config-driven behavior without full DI"

requirements-completed: [PROMPT-01, PROMPT-02, PROMPT-03, PROMPT-04, FMTD-03]

# Metrics
duration: 15min
completed: 2026-03-13
---

# Phase 30 Plan 02: LLMModule Integration Summary

**LLMModule extended with system-message prompt injection, FormatDetector routing-marker extraction, and 2-retry self-correction loop — wiring the complete prompt injection and format detection pipeline**

## Performance

- **Duration:** 15 min
- **Started:** 2026-03-13T13:30:21Z
- **Completed:** 2026-03-13T13:45:21Z
- **Tasks:** 2 (RED + GREEN, TDD)
- **Files modified:** 4

## Accomplishments

- LLMModule now injects a system message listing available AnimaRoute services and `<route>` format instructions when AnimaRoute is configured for the Anima
- FormatDetector.Detect() called after every LLM response — well-formed route markers dispatched to `AnimaRouteModule.port.request` then `.port.trigger` (request first — order enforced)
- Self-correction loop: malformed markers trigger a re-call with the specific error reason + format example appended as correction turn — up to 2 retries (3 total attempts)
- After max retries, error published to `LLMModule.port.error` output port (new port)
- All 7 new integration tests pass; all 2 existing LLMModule unit tests pass unchanged; full suite remains at 3 pre-existing failures

## Task Commits

Each task was committed atomically:

1. **RED — Failing PromptInjection integration tests** - `c516954` (test)
2. **GREEN — LLMModule extended with all required functionality** - `cb9a519` (feat)

_TDD: test commit precedes implementation commit._

## Files Created/Modified

- `src/OpenAnima.Core/Modules/LLMModule.cs` — Extended with router param, error output port, BuildKnownServiceNames(), BuildSystemMessage(), self-correction loop, DispatchRoutesAsync(), CallLlmAsync() helper
- `tests/OpenAnima.Tests/Integration/PromptInjectionIntegrationTests.cs` — 7 integration tests: no-config, system-msg injection, route dispatch+passthrough, self-correction, max-retries error port, plain text, backward compat
- `tests/OpenAnima.Tests/Modules/ModuleTests.cs` — Explicitly pass `router: null` in both LLMModule unit tests for documentation clarity
- `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs` — Updated port count assertion: LLMModule now has 3 ports (prompt, response, error)

## Decisions Made

- **BuildKnownServiceNames per ExecuteAsync call:** Queries `IAnimaModuleConfigService` on every execute rather than caching at initialization — prevents stale config when the user updates AnimaRoute settings without restarting the Anima.

- **Correction message includes format example:** The self-correction turn sends both the error reason ("Unclosed `<route>` tag") AND a concrete format example (`<route service="portName">...</route>`). LLMs respond better to "here's the correct format" than just "you made this error".

- **System message uses first port as example:** When building the system message, the format example uses the first registered port name. With the current single-route config design this is always the target port.

- **CallLlmAsync helper:** Extracted per-Anima vs. global-service selection into a separate private method — makes the retry loop cleaner (the same routing logic applies to both original call and correction calls).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] ModuleRuntimeInitializationTests port count needed update**
- **Found during:** GREEN phase (full test suite run)
- **Issue:** `WiringInitializationService_RegistersAllModulePorts` asserted `Assert.Equal(2, llmPorts.Count)` — after adding `[OutputPort("error", PortType.Text)]`, LLMModule now has 3 ports
- **Fix:** Updated assertion to `Assert.Equal(3, llmPorts.Count)` and added `Assert.Contains(llmPorts, p => p.Name == "error" && p.Direction == PortDirection.Output)`
- **Files modified:** `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs`
- **Verification:** Test now passes; targeted test suite still 25/25 passing
- **Committed in:** `cb9a519` (GREEN implementation commit)

**2. [Rule 1 - Bug] ModuleTests.cs lost InitializeAsync call after edit**
- **Found during:** GREEN phase (LLMModule_StateTransitions test timed out)
- **Issue:** When updating ModuleTests.cs to add `router: null`, the replacement accidentally dropped `await module.InitializeAsync()` and `Assert.Equal(ModuleExecutionState.Idle, ...)` from the first LLMModule test — causing a 5s timeout (module never subscribed to prompt port)
- **Fix:** Restored the two missing lines
- **Files modified:** `tests/OpenAnima.Tests/Modules/ModuleTests.cs`
- **Verification:** Test passes in <1s; 25 targeted tests all green
- **Committed in:** `cb9a519` (GREEN implementation commit)

---

**Total deviations:** 2 auto-fixed (Rule 1 — Bug)
**Impact on plan:** Both fixes necessary for correctness. No scope creep — fixes are strictly caused by adding the new error output port attribute and the edit operation respectively.

## Issues Encountered

None beyond the two auto-fixed regressions documented above.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- Prompt injection and format detection pipeline is complete
- LLMModule will automatically inject routing instructions and dispatch markers when AnimaRoute is configured
- Phase 30 is complete — both plans (FormatDetector TDD + LLMModule integration) are done
- Phase 31 (final wiring / milestone completion) can begin

---
*Phase: 30-prompt-injection-and-format-detection*
*Completed: 2026-03-13*
