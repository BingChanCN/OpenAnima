# Phase 17: E2E Module Pipeline Integration & Editor Polish - Context

**Gathered:** 2026-02-27
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire ChatPanel to module pipeline for end-to-end conversation via modules, add visual feedback for connection rejection, and formally verify RTIM requirements. ChatPanel stops using direct API calls and routes all conversation through the module wiring system. Editor nodes display real-time module status with visual indicators.

</domain>

<decisions>
## Implementation Decisions

### ChatPanel Integration Behavior
- Manual wiring required — user must connect ChatInput→LLM→ChatOutput in editor before ChatPanel works
- When pipeline is not configured, ChatPanel shows guided prompt with link/button to navigate to editor page
- Complete replacement of old direct API calls — no fallback to legacy code
- ChatPanel dynamically reads current wiring configuration from editor, not hardcoded to a fixed pipeline

### Module Status Display
- Node border color indicates state: running=green, error=red, stopped=gray
- Smooth CSS transition on border color changes, with subtle pulse animation while running
- Hover tooltip on nodes shows status text and error details (when applicable)
- Error nodes display additional warning icon (top-right corner) for emphasis

### Connection Rejection Feedback
- Claude's Discretion — visual rejection when dragging incompatible ports (animation, color, styling details left to implementation)

### Error Indicators
- Claude's Discretion — how module execution errors surface beyond the border+icon+tooltip pattern decided above

### Claude's Discretion
- Connection rejection visual feedback design details (animation type, colors, timing)
- Exact error message formatting in tooltips
- Pulse animation intensity and timing
- Guided prompt wording and layout in ChatPanel empty state
- How to detect and validate that required pipeline modules exist in current wiring config

</decisions>

<specifics>
## Specific Ideas

- ChatPanel should feel the same as v1.2 once pipeline is wired — user sends message, gets streamed response, same UX
- The "pipeline not configured" state should guide the user clearly, not just show an error
- Border color approach keeps the node cards clean — no extra clutter, but errors get the extra warning icon to stand out

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 17-e2e-module-pipeline-integration-editor-polish*
*Context gathered: 2026-02-27*
