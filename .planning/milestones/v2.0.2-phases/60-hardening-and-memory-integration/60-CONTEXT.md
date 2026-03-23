# Phase 60: Hardening and Memory Integration - Context

**Gathered:** 2026-03-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Agent loop interactions are durably observable in the Run inspector and the memory graph receives clean, useful content from agent exchanges rather than raw tool output JSON. This phase adds StepRecorder iteration brackets, token budget management within the agent loop, and full-history sedimentation for agent conversations.

</domain>

<decisions>
## Implementation Decisions

### StepRecorder Iteration Brackets (HARD-03)
- Parent-child step model: each agent iteration records a parent step "AgentIteration #N" with child steps for individual tool calls within that iteration
- Outer "AgentLoop" bracket step wraps all iterations — records total duration and iteration count in outputSummary
- Parent step inputSummary shows the first 200 characters of the LLM response for that iteration (including tool_call markers)
- Child tool call steps use the iteration's PropagationId to associate with their parent
- StepRecorder.RecordStepStartAsync called at iteration start, RecordStepCompleteAsync at iteration end (after all tool calls dispatched)

### Token Budget Management (HARD-02)
- Before each LLM re-call in RunAgentLoopAsync, count tokens in accumulated history using SharpToken (cl100k_base)
- When token count exceeds 70% of agentContextWindowSize, drop the oldest assistant+tool message pairs from history (preserving system message + original user messages)
- New config key: `agentContextWindowSize` (int, default 128000, added to LLMModule GetSchema() in "agent" group)
- After truncation, insert a system message: "[Earlier tool results were trimmed to fit context window]" so the LLM is aware of trimmed history
- Token counting uses the same SharpToken already in the project (CTX-01 from v1.2)

### Sedimentation Full History (HARD-01)
- TriggerSedimentation in RunAgentLoopAsync passes the complete accumulated history list (all user/assistant/tool role messages), not just the original messages + final response
- When the complete history is too long for the sedimentation LLM context window, truncate tool result message content to first 500 characters each, preserving the full message structure and reasoning chain
- ISedimentationService.SedimentAsync signature unchanged — the existing `IReadOnlyList<ChatMessageInput> messages` parameter receives the full expanded list, and `llmResponse` receives the final clean response

### Claude's Discretion
- Exact PropagationId format for linking parent/child iteration steps
- Whether to add a new IStepRecorder method for bracket steps or reuse existing start/complete pattern
- Token counting batch size (count every iteration vs. periodic check)
- Exact position of truncation notice in message list

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Hardening Requirements
- `.planning/REQUIREMENTS.md` — HARD-01, HARD-02, HARD-03 define all hardening requirements

### Agent Loop Implementation
- `src/OpenAnima.Core/Modules/LLMModule.cs` — RunAgentLoopAsync is the loop to modify; TriggerSedimentation is the sedimentation call point
- `.planning/phases/58-agent-loop-core/58-CONTEXT.md` — Phase 58 decisions: XML markers, direct dispatch, _executionGuard semantics

### StepRecorder Infrastructure
- `src/OpenAnima.Core/Runs/StepRecorder.cs` — Current implementation with RecordStepStartAsync/CompleteAsync/FailedAsync, PropagationId tracking, convergence checks
- `src/OpenAnima.Core/Runs/IStepRecorder.cs` — Interface contract for step recording

### Sedimentation Infrastructure
- `src/OpenAnima.Core/Memory/SedimentationService.cs` — SedimentAsync with BuildExtractionMessages, existing tool result truncation in contextSummary
- `src/OpenAnima.Core/Memory/ISedimentationService.cs` — Interface contract

### Token Counting
- `src/OpenAnima.Core/LLM/` — Existing SharpToken integration from CTX-01 (v1.2)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **StepRecorder**: Already has PropagationId tracking in ConcurrentDictionary — can use this to link parent bracket steps with child tool call steps
- **SharpToken**: Already in project for token counting (CTX-01) — reuse for agent loop budget checks
- **SedimentationService.BuildExtractionMessages**: Already builds `<conversation>` XML from messages list — will receive full history including tool role messages without interface changes
- **TriggerSedimentation**: Already snapshots messages before Task.Run — change to pass full history instead of original messages

### Established Patterns
- **StepRecord with start/complete pairs**: RecordStepStartAsync returns stepId, RecordStepCompleteAsync closes it — iteration brackets follow this same pattern
- **Fire-and-forget sedimentation**: Task.Run with CancellationToken.None — pattern preserved
- **Per-module SemaphoreSlim**: Agent loop already holds _executionGuard for full duration — no new concurrency concerns

### Integration Points
- **RunAgentLoopAsync**: Main modification point — add bracket step recording at loop boundaries and per-iteration, add token budget check before each CallLlmAsync
- **LLMModule.GetSchema()**: Add agentContextWindowSize config field in "agent" group
- **TriggerSedimentation call in RunAgentLoopAsync**: Change from passing original messages to passing accumulated history

</code_context>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 60-hardening-and-memory-integration*
*Context gathered: 2026-03-23*
