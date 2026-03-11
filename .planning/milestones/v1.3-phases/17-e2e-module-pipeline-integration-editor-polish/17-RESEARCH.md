# Phase 17: E2E Module Pipeline Integration & Editor Polish - Research

**Researched:** 2026-02-27
**Domain:** Blazor ChatPanel integration, module pipeline routing, editor UX feedback, runtime status propagation
**Confidence:** HIGH — findings based on direct repository inspection

## Summary

Phase 17 is a gap-closure phase with three concrete outcomes:

1. Route ChatPanel through the module pipeline (`ChatInputModule -> LLMModule -> ChatOutputModule`) instead of direct `ILLMService` calls (E2E-01).
2. Provide explicit visual rejection feedback when users attempt incompatible port connections (PORT-02 polish surfaced by phase goal).
3. Ensure runtime module state and errors are shown clearly in editor nodes (RTIM-01, RTIM-02).

Most infrastructure already exists:
- Modules and port contracts are implemented.
- Runtime hub emits module state and error events.
- Editor receives SignalR updates and stores runtime state.
- NodeCard already colors borders from state and can show error popup.

Primary gaps are integration and UX behavior consistency:
- `ChatPanel.razor` still uses direct `_llmService.StreamWithUsageAsync(...)` path.
- Connection type mismatch silently cancels in `EditorStateService.EndConnectionDrag(...)`.
- Node status/error presentation does not yet match the Phase 17 context decisions (border transitions, running pulse, warning icon, tooltip details).

## Phase Requirements Coverage

| Req ID | Requirement | Current State | Needed in Plans |
|---|---|---|---|
| E2E-01 | ChatInput→LLM→ChatOutput conversation parity with v1.2 | Partial (pipeline modules exist; ChatPanel bypasses them) | Rewire ChatPanel + integration tests + not-configured guidance |
| RTIM-01 | Real-time module status in editor nodes | Partial (SignalR + state store exist) | Correct module identity mapping + polished visual state UX |
| RTIM-02 | Module execution errors shown on nodes | Partial (error state + popup exists) | Improve discoverability/indicators and verify wiring to runtime events |

## Key Code Findings

### 1) ChatPanel still bypasses module pipeline
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor:7` injects `ILLMService` directly.
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor:135` streams directly from `_llmService.StreamWithUsageAsync(...)`.

This conflicts with Phase 17 context decision to fully replace direct API path.

### 2) Modules and event contracts are ready
- `src/OpenAnima.Core/Modules/ChatInputModule.cs:32` exposes `SendMessageAsync` and publishes to `ChatInputModule.port.userMessage`.
- `src/OpenAnima.Core/Modules/LLMModule.cs:36` subscribes to `LLMModule.port.prompt` and publishes `LLMModule.port.response`.
- `src/OpenAnima.Core/Modules/ChatOutputModule.cs:37` subscribes to `ChatOutputModule.port.displayText` and fires `OnMessageReceived`.

### 3) Runtime status pipeline exists but mapping must be verified
- `src/OpenAnima.Core/Wiring/WiringEngine.cs:175` emits `ReceiveModuleStateChanged(moduleId, "Running")`.
- `src/OpenAnima.Core/Wiring/WiringEngine.cs:197` emits `ReceiveModuleError(moduleId, ...)`.
- `src/OpenAnima.Core/Components/Shared/EditorCanvas.razor:91` receives hub events and calls `_state.UpdateModuleState(moduleId, state)`.
- `src/OpenAnima.Core/Services/EditorStateService.cs:78` stores state by `moduleId`.
- `src/OpenAnima.Core/Components/Shared/NodeCard.razor:34` derives border color by `Node.ModuleId`.

Because `ModuleNode.ModuleId` is instance GUID while runtime executes by node IDs in the loaded wiring graph, this can work only if IDs remain consistent through load path. Plan should add explicit tests around this mapping.

### 4) Connection rejection is currently silent
- `src/OpenAnima.Core/Services/EditorStateService.cs:378` only creates connection when port types match.
- On mismatch, flow falls through and clears drag state with no rejection marker (`src/OpenAnima.Core/Services/EditorStateService.cs:395`).

Need a transient rejection signal consumed by UI.

### 5) Existing E2E tests are close but not enough for phase goal
- `tests/OpenAnima.Tests/Integration/ModulePipelineIntegrationTests.cs:16` validates module pipeline flow with EventBus.
- Missing coverage for ChatPanel integration and editor runtime feedback interactions.

## Recommended Plan Shape

### Plan 17-01 (Wave 1)
ChatPanel-to-pipeline integration and end-to-end parity tests.

### Plan 17-02 (Wave 2)
Editor polish: incompatible-connection feedback, runtime status visuals, error discoverability, plus focused UI-state tests.

Sequencing rationale: finish data-path correctness before UI polish so UX behavior validates against real module execution.

## Validation Architecture

### Automated checks
- Targeted integration tests for ChatPanel + module pipeline bridge.
- Targeted integration/unit tests for runtime status and connection rejection state transitions.
- Full test suite regression run.

### Manual acceptance checks
- Wire ChatInput→LLM→ChatOutput in editor and send message from ChatPanel.
- Confirm ChatPanel shows guided “pipeline not configured” state when required chain absent.
- Drag incompatible output->input and observe explicit rejection animation/indicator.
- Observe running/error/stopped visuals on nodes during execution and fault.

### Required new/updated test areas
- ChatPanel pipeline behavior and fallback guidance.
- RuntimeHub event -> EditorStateService -> NodeCard visual state chain.
- Rejection feedback state lifecycle (trigger, render, clear).

## Risks and Mitigations

1. **Risk:** ChatPanel context/token UX regresses after switching from direct stream path.
   - **Mitigation:** keep existing ChatContextManager updates and event publishing semantics, adapt to module output callback path.

2. **Risk:** Node status updates appear on wrong/no nodes due ID mismatch.
   - **Mitigation:** add tests that assert runtime state updates map to `ModuleNode.ModuleId` values from loaded config.

3. **Risk:** Rejection feedback introduces noisy persistent error UI.
   - **Mitigation:** design transient feedback with short-lived state and reset on drag end/new drag.

## Open Questions Resolved for Planning

- Should ChatPanel fall back to old direct API path? **No** (locked decision in context).
- Should “pipeline missing” be hard error or guided UX? **Guided UX with clear action to open editor**.
- Should status/error indicators rely only on border color? **No** — include warning icon + tooltip/error details.

## Sources

- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`
- `src/OpenAnima.Core/Modules/ChatInputModule.cs`
- `src/OpenAnima.Core/Modules/LLMModule.cs`
- `src/OpenAnima.Core/Modules/ChatOutputModule.cs`
- `src/OpenAnima.Core/Wiring/WiringEngine.cs`
- `src/OpenAnima.Core/Services/EditorStateService.cs`
- `src/OpenAnima.Core/Components/Shared/NodeCard.razor`
- `src/OpenAnima.Core/Components/Shared/EditorCanvas.razor`
- `tests/OpenAnima.Tests/Integration/ModulePipelineIntegrationTests.cs`
