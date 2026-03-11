---
phase: 17-e2e-module-pipeline-integration-editor-polish
plan: 01
subsystem: chat
tags: [chatpanel, module-pipeline, wiring, integration-tests]

# Dependency graph
requires:
  - phase: 16-module-runtime-initialization-port-registration
    provides: ChatInputModule/LLMModule/ChatOutputModule runtime initialization and port registration
provides:
  - ChatPanel routes send/regenerate through ChatInputModule -> LLMModule -> ChatOutputModule only (no direct LLM fallback)
  - Runtime validation for required chat pipeline topology in current wiring configuration
  - Integration coverage for configured/missing chat pipeline and GUID-based node wiring routing
affects: [chat-ui, wiring-engine, phase-17-02-runtime-feedback]

# Tech tracking
tech-stack:
  added: []
  patterns: [module-pipeline-only-chat-flow, typed-event-routing-by-port-type]

key-files:
  created:
    - src/OpenAnima.Core/Services/ChatPipelineConfigurationValidator.cs
    - tests/OpenAnima.Tests/Integration/ChatPanelModulePipelineTests.cs
  modified:
    - src/OpenAnima.Core/Components/Shared/ChatPanel.razor
    - src/OpenAnima.Core/Components/Shared/ChatPanel.razor.css
    - src/OpenAnima.Core/Wiring/WiringEngine.cs
    - tests/OpenAnima.Tests/Integration/ModulePipelineIntegrationTests.cs

key-decisions:
  - "ChatPanel blocks send/regenerate when required chain is missing and shows guided navigation to /editor"
  - "WiringEngine resolves connection routing by node.ModuleName (with ModuleId fallback) so editor GUID node IDs still route runtime events"
  - "EventBus routing subscriptions must use payload-typed handlers (string/DateTime/object) to preserve delivery semantics"

patterns-established:
  - "Chat pipeline readiness gate: validate ChatInput.userMessage -> LLM.prompt and LLM.response -> ChatOutput.displayText before chat actions"
  - "Typed port routing in WiringEngine via source port metadata from IPortRegistry"

requirements-completed: [E2E-01]

# Metrics
duration: 22min
completed: 2026-02-27
---

# Phase 17 Plan 01: E2E Module Pipeline Integration Summary

**ChatPanel now uses only the module pipeline with runtime topology checks, and integration tests lock behavior for configured/missing wiring and GUID-based editor node graphs**

## Performance

- **Duration:** 22 min
- **Started:** 2026-02-27T17:28:00+08:00
- **Completed:** 2026-02-27T17:49:52+08:00
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- Replaced direct `_llmService` streaming path in ChatPanel with `ChatInputModule.SendMessageAsync` + `ChatOutputModule.OnMessageReceived` response flow
- Added live wiring validation and guided editor CTA when required chat chain is not configured
- Extended integration tests to verify missing pipeline behavior and GUID-node routing through WiringEngine

## Task Commits

Each task was committed atomically:

1. **Task 1: Replace ChatPanel direct LLM path with module pipeline path** - `ef19fe7` (feat)
2. **Task 2: Add integration tests for configured and missing pipeline states** - `8c3132d` (test)

## Files Created/Modified
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` - Module-only send/regenerate flow, output event synchronization, pipeline gating, guided editor prompt
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor.css` - Guided pipeline-missing status styles and editor CTA button
- `src/OpenAnima.Core/Services/ChatPipelineConfigurationValidator.cs` - Runtime validator for required ChatInput -> LLM -> ChatOutput wiring topology
- `src/OpenAnima.Core/Wiring/WiringEngine.cs` - ModuleName-based routing resolution with typed event subscriptions by port type
- `tests/OpenAnima.Tests/Integration/ChatPanelModulePipelineTests.cs` - Tests for valid/missing topology and missing-pipeline no-delivery behavior
- `tests/OpenAnima.Tests/Integration/ModulePipelineIntegrationTests.cs` - Added GUID node topology routing integration test

## Decisions Made
- Removed direct API fallback path entirely to enforce single execution path through module wiring
- Used `IWiringEngine.GetCurrentConfiguration()` + topology validator for dynamic readiness checks instead of hardcoded assumptions
- Implemented typed EventBus routing in WiringEngine because untyped/object subscriptions do not receive typed payload publishes

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] WiringEngine routing dropped typed payload events in GUID-node topology test**
- **Found during:** Task 2 (integration test expansion)
- **Issue:** `Subscribe<object>` in WiringEngine did not receive `string`/`DateTime` publishes, so routed chat events never reached target ports
- **Fix:** Added typed subscription/publish path selection by source port metadata (`Text -> string`, `Trigger -> DateTime`, fallback object)
- **Files modified:** `src/OpenAnima.Core/Wiring/WiringEngine.cs`
- **Verification:** `dotnet test ... --filter "FullyQualifiedName~ChatPanelModulePipelineTests|FullyQualifiedName~ModulePipelineIntegrationTests"` passed (6/6)
- **Committed in:** `8c3132d` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Required for functional E2E routing correctness with real editor-generated node IDs.

## Issues Encountered
- Initial filtered test command in sandbox failed with MSBuild named-pipe permission error; resolved by running approved `dotnet test` prefix outside sandbox.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- ChatPanel now enforces module-pipeline-only behavior with explicit user guidance when not wired
- WiringEngine routing supports editor GUID node IDs for typed payload event propagation
- Ready for Plan 17-02 editor runtime status and rejection feedback polish

---
*Phase: 17-e2e-module-pipeline-integration-editor-polish*
*Completed: 2026-02-27*
