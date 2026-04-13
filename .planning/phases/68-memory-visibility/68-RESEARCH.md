# Phase 68: Memory Visibility - Research

**Researched:** 2026-04-01
**Domain:** Blazor chat tool visibility, event correlation, durable chat history, background sedimentation
**Confidence:** HIGH

## Summary

Phase 68 should extend the existing assistant-message tool-card system rather than introduce a second memory-specific component model. The current chat surface already has the right three anchors:

1. `ChatPanel.razor` appends `ToolCallInfo` objects to the current streaming assistant message as `LLMModule.tool_call.started` / `LLMModule.tool_call.completed` events arrive.
2. `ChatMessage.razor` renders those tool calls as collapsible cards inside the assistant bubble.
3. `ChatHistoryService` persists `ToolCallInfo` as JSON so tool visibility survives reload.

The main work is not “add a card.” The real work is enriching that existing tool-card model with memory-specific display metadata, adding a separate sedimentation summary payload, and making sure both live updates and persisted replay attach to the correct assistant bubble.

**Primary recommendation:** split the phase into three planning slices:
- Shared visibility model + chat persistence migration/update path
- Live event wiring and correlation in `ChatPanel`
- Rendering/localization/CSS in `ChatMessage`

That keeps the current shell intact, isolates the risky persistence race, and gives the planner a clean dependency structure.

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MEMV-01 | Explicit memory tool calls (`memory_create`, `memory_update`, `memory_delete`) displayed as tool cards in chat bubbles using the same interaction pattern as workspace tools | Reuse the existing `ToolCallInfo` + `ChatMessage` card stack; add memory-specific display fields instead of a second card component |
| MEMV-02 | Background sedimentation shows a single collapsed `N memories sedimented` summary chip in chat | Add a dedicated sedimentation-complete event and persist a per-message summary object separate from `tool_calls_json` |
| MEMV-03 | Memory tool cards have distinct visual treatment (`ToolCategory.Memory`) | Classify only `memory_create` / `memory_update` / `memory_delete` as memory cards; keep `memory_list` on the generic tool-card style |
</phase_requirements>

---

## Locked Decisions From Context and UI Contract

### Must Preserve

- Reuse the current collapsible tool-card shell. No second bespoke memory-card component.
- Folded memory-card header shows: operation label, target URI, one-line content summary.
- Delete cards still show operation label + URI even when there is no content summary.
- Sedimentation is exactly one non-expandable summary chip per assistant response.
- The chip shows total count only. No URI list, no create/update breakdown.
- Memory cards and sedimentation chip must persist with chat history and re-render on replay.
- `ToolCategory.Memory` must be visually distinct but subordinate to the assistant response.
- `memory_list` is not part of the distinct memory-card contract for this phase.

### Immediate Planning Implication

The view model must distinguish:
- Generic workspace tool calls
- Explicit memory tool calls
- Sedimentation summary metadata

Those are three separate concerns even though they all render inside the same assistant bubble.

---

## Existing Implementation Surface

### Chat Rendering

- `src/OpenAnima.Core/Components/Shared/ChatMessage.razor`
  - Renders `ToolCalls` above assistant markdown content
  - Each card is already collapsible and persisted through `ToolCallInfo.IsExpanded`
  - Tool badge row currently renders only `Used {0} tools`
- `src/OpenAnima.Core/Components/Shared/ChatMessage.razor.css`
  - Contains the generic tool-card shell
  - Has no category styling or summary-chip treatment yet

### Live Event Wiring

- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`
  - Subscribes to `LLMModule.tool_call.started`
  - Subscribes to `LLMModule.tool_call.completed`
  - Appends tool-call state to the current streaming assistant message
  - Persists the final assistant message after response completion via `ChatHistoryService.StoreMessageAsync(...)`

### Persistence

- `src/OpenAnima.Core/Services/ChatSessionState.cs`
  - `ChatSessionMessage` currently stores only `Role`, `Content`, `IsStreaming`, and `ToolCalls`
  - `ToolCallInfo` stores only generic tool metadata: `ToolName`, `Parameters`, `ResultSummary`, `Status`, `IsExpanded`
- `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs`
  - Stores `tool_calls_json`
  - Restores `ToolCallInfo` from JSON
  - Does not expose the database row `id`
  - Has no “update assistant message metadata” API
- `src/OpenAnima.Core/ChatPersistence/ChatDbInitializer.cs`
  - Creates `chat_messages`
  - Does not yet support schema migration for new columns

### Explicit Memory Tool Sources

- `src/OpenAnima.Core/Tools/MemoryCreateTool.cs`
- `src/OpenAnima.Core/Tools/MemoryUpdateTool.cs`
- `src/OpenAnima.Core/Tools/MemoryDeleteTool.cs`

All three already publish `Memory.operation` with `MemoryOperationPayload`.

Important details:
- `memory_create` uses parameter `path`
- `memory_update` / `memory_delete` use parameter `uri`
- `MemoryOperationPayload` already carries `Operation`, `Uri`, `Content`, and `Success`

### Sedimentation Source

- `src/OpenAnima.Core/Memory/SedimentationService.cs`
  - Knows `writtenUris.Count`
  - Currently emits no chat-visible event
  - Runs in background after the final response through `LLMModule.TriggerSedimentation(...)`
- `src/OpenAnima.Core/Modules/LLMModule.cs`
  - Publishes final response
  - Then calls `TriggerSedimentation(...)`
  - Sedimentation runs inside `Task.Run(..., CancellationToken.None)`

---

## Key Findings

### 1. Explicit memory cards can be correlated without new LLM-side identifiers

The generic tool lifecycle and the memory-operation event already line up well enough for this phase.

Observed order for a memory tool call:
- `LLMModule.tool_call.started` is published before tool execution
- The memory tool publishes `Memory.operation` from inside `ExecuteAsync(...)`
- `LLMModule.tool_call.completed` is published after the tool returns

This means `ChatPanel` can:
- Create the generic running card on `tool_call.started`
- Match the later `Memory.operation` to the latest running card for the same tool and URI/path
- Apply folded-memory metadata before the completed status arrives

Because agent tool calls execute in document order, matching the most recent running card is sufficient for Phase 68.

### 2. Memory-card classification must happen on tool start, not only on `Memory.operation`

`Memory.operation` is published only after the tool executes successfully.

That is too late for two reasons:
- Failed `memory_create` / `memory_update` / `memory_delete` calls must still look like memory cards
- The UI spec requires delete cards to keep memory-category styling even when execution fails

Recommendation:
- Classify memory cards from `ToolCallStartedPayload.ToolName`
- Use `Memory.operation` only to hydrate display fields such as operation label, canonical URI, and folded summary

### 3. Sedimentation visibility needs a separate payload from explicit tool visibility

`MemoryOperationPayload` is the wrong shape for the sedimentation chip:
- It models one operation at a time
- It has URI/content fields the chip must not expose
- Sedimentation is best represented as one aggregate count attached to one assistant response

Recommendation:
- Add a dedicated payload such as `SedimentationCompletedPayload`
- Suggested fields: `AnimaId`, `WrittenCount`
- Event name can be distinct, e.g. `Memory.sedimentation.completed`

Keep it count-only for the UI contract. Do not overload the chip event with URI detail.

### 4. Persistence is the hardest part of the phase

Explicit tool cards persist “for free” once `ToolCallInfo` gains the new fields, because `tool_calls_json` already round-trips through JSON.

The sedimentation chip does not.

Current state:
- There is no `sedimentation_json` or equivalent field in `chat_messages`
- `ChatHistoryService` can insert rows, but cannot update the just-stored assistant row later
- `ChatSessionMessage` does not know the backing database row id

Recommendation:
- Expose the existing `chat_messages.id` as a property on `ChatSessionMessage`
- Make `StoreMessageAsync(...)` return the inserted row id for assistant messages
- Add a separate nullable summary payload on `ChatSessionMessage`
- Add a new `sedimentation_json` column to `chat_messages`
- Add `UpdateAssistantVisibilityAsync(...)` (or equivalent) to persist post-response metadata updates

### 5. Phase 68 has a real race between response persistence and background sedimentation

`LLMModule` triggers sedimentation in the background after the response is published. `ChatPanel` persists the assistant message after it receives the final response. Either one can win the race.

The implementation therefore cannot assume:
- “assistant row exists before sedimentation event arrives”, or
- “sedimentation always arrives later”

Recommendation:
- Track the new persistence id on the in-memory `ChatSessionMessage`
- If sedimentation arrives before persistence id is known, update the in-memory message first and persist once the row id exists
- If sedimentation arrives after persistence id exists, update the row immediately

This race is the main reason the phase should not be planned as a pure CSS/UI task.

### 6. Existing chat DB initialization needs migration logic, not only CREATE TABLE

Phase 66 introduced `chat_messages` already. Existing users may already have `chat.db`.

`ChatDbInitializer` currently uses only a static `CREATE TABLE IF NOT EXISTS` script. That is not enough for adding Phase 68 fields.

Recommendation:
- Keep `CREATE TABLE IF NOT EXISTS` for new installs
- Add a lightweight migration step using `pragma_table_info('chat_messages')`
- If missing, add `sedimentation_json TEXT`

`tool_calls_json` does not need a schema change because richer `ToolCallInfo` serializes into the same column.

### 7. Localization work is part of the phase, not an afterthought

Current resources only provide `Chat.ToolCountBadge`. The UI contract adds new copy:
- Create memory
- Update memory
- Delete memory
- `1 memory sedimented`
- `{N} memories sedimented`
- Delete body copy about soft delete / recovery

Recommendation:
- Plan resource updates in `SharedResources.resx`, `SharedResources.en-US.resx`, and `SharedResources.zh-CN.resx`
- Do not hard-code English labels in Razor

---

## Recommended Architecture

### Shared Models

Recommended additions in `ChatSessionState.cs`:

```csharp
public enum ToolCategory
{
    Generic = 0,
    Memory = 1
}

public sealed class SedimentationSummaryInfo
{
    public int Count { get; set; }
}
```

Recommended `ToolCallInfo` additions:
- `ToolCategory Category`
- `string DisplayTitle`
- `string? TargetUri`
- `string? FoldedSummary`
- `string? OperationKind`

Recommended `ChatSessionMessage` additions:
- `long? PersistenceId`
- `SedimentationSummaryInfo? SedimentationSummary`

The exact names can vary, but the model must make the UI contract explicit instead of recomputing everything in Razor.

### Eventing

Keep existing subscriptions:
- `LLMModule.tool_call.started`
- `LLMModule.tool_call.completed`

Add:
- `Memory.operation` consumption in `ChatPanel`
- one dedicated sedimentation-complete event

Recommended sedimentation event emission point:
- `SedimentationService` after writes complete, because it already knows the final count

Recommended implementation detail:
- inject `IEventBus?` as an optional constructor dependency on `SedimentationService`
- publish only when `writtenUris.Count > 0`

That avoids changing the `ISedimentationService` return type and keeps most existing tests intact.

### Persistence

Recommended minimal persistence design:

- Continue storing enriched `ToolCallInfo` inside `tool_calls_json`
- Add `sedimentation_json TEXT` to `chat_messages`
- Surface `chat_messages.id` through `LoadHistoryAsync(...)`
- Return inserted id from `StoreMessageAsync(...)`
- Add update API for assistant visibility metadata after the initial insert

This is the smallest change set that still satisfies replay/persistence requirements.

### Rendering

Recommended `ChatMessage.razor` approach:
- Keep one tool-card loop
- Branch only inside the header/body fragments for `ToolCategory.Memory`
- Render sedimentation chip in the same badge row as the tool-count badge
- Keep chip non-expandable and visually quieter than cards

The rendering should be driven entirely from model fields prepared earlier by `ChatPanel` and persistence restore.

### Testability

If the `ChatPanel` event handlers start to carry too much mapping logic, extract a small helper/projector instead of leaving all correlation logic inline.

A projector/helper makes these cases unit-testable without a full Blazor component harness:
- tool-start classification
- URI/path normalization for memory tools
- folded-summary construction
- sedimentation summary attachment
- persistence-race handling rules

---

## Risks and Pitfalls

### Risk: `memory_list` accidentally styled as a memory card

Why it matters:
- Context explicitly leaves `memory_list` outside the locked Phase 68 contract

Guardrail:
- Only classify `memory_create`, `memory_update`, and `memory_delete` as `ToolCategory.Memory`

### Risk: matching the wrong running card

Why it matters:
- Multiple tool calls can appear in one assistant response

Guardrail:
- Match by tool name plus URI/path
- Prefer the latest running card because tools execute sequentially

### Risk: sedimentation chip lost on reload

Why it matters:
- The chip is not part of `tool_calls_json`

Guardrail:
- Persist sedimentation summary separately and replay it from DB

### Risk: schema drift for existing `chat.db`

Why it matters:
- This phase lands after Phase 66; existing users already have the table

Guardrail:
- Add explicit migration logic in `ChatDbInitializer`

### Risk: UI code recomputes display text on every render from raw parameters

Why it matters:
- `memory_create` uses `path`, while `memory_update` / `memory_delete` use `uri`
- replay should not depend on reconstructing the same logic from raw parameters forever

Guardrail:
- Persist normalized display metadata directly on `ToolCallInfo`

---

## Verification Surface

### Existing Test Files Worth Extending

- `tests/OpenAnima.Tests/ChatPersistence/ChatHistoryServiceTests.cs`
  - round-trip enriched `ToolCallInfo`
  - round-trip `sedimentation_json`
  - verify assistant-message visibility update API
- `tests/OpenAnima.Tests/Integration/ChatPersistenceIntegrationTests.cs`
  - replay full conversation with persisted memory metadata
- `tests/OpenAnima.Tests/Unit/ChatSessionStateTests.cs`
  - defaults for new `ToolCategory` / sedimentation summary properties
- `tests/OpenAnima.Tests/Unit/ToolCallEventPayloadTests.cs`
  - add new sedimentation-complete payload assertions
- `tests/OpenAnima.Tests/Unit/SedimentationServiceTests.cs`
  - verify event publication when sedimentation writes > 0 nodes
- `tests/OpenAnima.Tests/Unit/LLMModuleAgentLoopHardeningTests.cs`
  - keep existing hardening guarantee that sedimentation sees full history

### Recommended Manual Checks

- `memory_create` / `memory_update` / `memory_delete` appear as collapsible memory cards during a live chat response
- cards remain collapsed after refresh/restart
- delete card still shows URI when there is no summary text
- sedimentation chip appears once per assistant response and only shows count
- sedimentation chip reappears after reload for messages where it was persisted
- generic tool count badge still renders alongside the new chip

---

## Planning Recommendation

Recommended plan shape:

| Plan | Wave | Objective |
|------|------|-----------|
| 01 | 1 | Extend chat visibility models, DB migration, and persistence update APIs |
| 02 | 2 | Wire `ChatPanel` to classify explicit memory tools, consume `Memory.operation`, and attach/persist sedimentation summary |
| 03 | 2 | Update `ChatMessage` rendering, CSS, and localized strings for memory cards and sedimentation chip |

Why this split works:
- Plan 01 isolates the data contract and the persistence race
- Plans 02 and 03 can proceed in parallel once the data model is stable
- The phase goal stays aligned to the existing chat architecture instead of scattering logic across unrelated modules

---

*Phase: 68-memory-visibility*
*Research synthesized from roadmap, context, UI spec, current code, and existing tests on 2026-04-01*
