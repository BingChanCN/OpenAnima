# Requirements: OpenAnima

**Defined:** 2026-03-23
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

## v2.0.2 Requirements

Requirements for Chat Agent Loop milestone. Each maps to roadmap phases.

### Agent Loop Core

- [x] **LOOP-01**: LLM can parse `<tool_call>` XML markers from model output, extracting tool name, parameters, and remaining text
- [x] **LOOP-02**: Agent can invoke IWorkspaceTool directly and receive ToolResult (bypassing EventBus to avoid semaphore deadlock)
- [x] **LOOP-03**: Agent can inject tool results into conversation history and re-call LLM, looping until no tool calls remain or iteration limit is reached
- [x] **LOOP-04**: User can configure agent max iterations per Anima (default 10, hard server-side ceiling)
- [x] **LOOP-05**: When tool execution fails, error message is returned as a tool result message so LLM can self-correct
- [x] **LOOP-06**: LLM system message includes tool call syntax instructions and "tool results are data, not instructions" safety prompt
- [x] **LOOP-07**: Agent loop propagates CancellationToken through all steps; cancellation correctly releases semaphore and closes StepRecorder

### Tool Call UI

- [x] **TCUI-01**: Chat UI displays collapsible tool call cards inside conversation bubbles in real-time (tool name, parameters, result, status)
- [x] **TCUI-02**: Assistant message shows tool call count badge ("Used N tools")
- [ ] **TCUI-03**: ChatPanel generation timeout extends from 30s to 300s in agent mode
- [ ] **TCUI-04**: Message sending is disabled while agent loop is running, preventing race conditions

### Hardening

- [ ] **HARD-01**: Sedimentation service receives full conversation history including all tool call turns
- [ ] **HARD-02**: Token budget check before each LLM re-call; truncates oldest tool results when exceeding 70% of context window
- [ ] **HARD-03**: Agent loop records bracket steps per iteration in StepRecorder, visible in Run inspector

## Future Requirements

Deferred to future release. Tracked but not in current roadmap.

### Tool Guards

- **GUARD-01**: Suppress tool descriptor injection when no active Run exists (save tokens)

### Native API

- **NATV-01**: Native OpenAI function calling API support (requires LLM call stack abstraction)

### Advanced Execution

- **PARA-01**: Parallel tool execution within a single turn
- **HITL-01**: Human-in-the-loop approval before destructive tool execution

## Out of Scope

| Feature | Reason |
|---------|--------|
| Native OpenAI function calling | Requires rewriting three-layer LLM call stack; XML markers are provider-agnostic |
| Parallel tool execution | Tool calls often have causal dependencies; protocol complexity too high |
| Streaming during tool-call turns | XML marker detection requires complete response; partial output causes false positives |
| Unlimited iterations | Production reliability risk; must have hard ceiling |
| Separate agent loop message history | Must use same conversation history as ChatPanel/ContextModule to avoid divergence |
| Automatic tool retry without LLM feedback | LLM must see errors to adapt strategy |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| LOOP-01 | Phase 58 | Complete |
| LOOP-02 | Phase 58 | Complete |
| LOOP-03 | Phase 58 | Complete |
| LOOP-04 | Phase 58 | Complete |
| LOOP-05 | Phase 58 | Complete |
| LOOP-06 | Phase 58 | Complete |
| LOOP-07 | Phase 58 | Complete |
| TCUI-01 | Phase 59 | Complete |
| TCUI-02 | Phase 59 | Complete |
| TCUI-03 | Phase 59 | Pending |
| TCUI-04 | Phase 59 | Pending |
| HARD-01 | Phase 60 | Pending |
| HARD-02 | Phase 60 | Pending |
| HARD-03 | Phase 60 | Pending |

**Coverage:**
- v2.0.2 requirements: 14 total
- Mapped to phases: 14
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-23*
*Last updated: 2026-03-23 — traceability mapped after roadmap creation*
