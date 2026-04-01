# Phase 68: Memory Visibility - Research

**Researched:** 2026-04-01
**Domain:** Blazor chat UI, event-driven tool visibility, and chat-history persistence
**Confidence:** MEDIUM

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** `memory_create`, `memory_update`, and `memory_delete` reuse the existing chat tool-card skeleton instead of introducing a second bespoke memory-card component model.
- **D-02:** Memory cards keep the same collapsible interaction pattern as current workspace tool cards so the chat UI remains consistent.
- **D-03:** The folded state of each explicit memory card shows: operation label, target URI, and a one-line content summary.
- **D-04:** Expanded state can continue to show parameter and result details through the existing card body pattern; the phase does not require a separate memory-detail layout.
- **D-05:** For delete operations, the folded state still surfaces the operation label and URI even when no content summary exists.
- **D-06:** Background sedimentation is surfaced as exactly one collapsed summary chip per assistant response, never one card or chip per sedimented node.
- **D-07:** The chip displays only the total count of sedimented memories.
- **D-08:** The chip must not break out create/update counts and must not expand into URI-level detail in this phase.
- **D-09:** Sedimentation visibility should remain subordinate to the conversation and explicit tool cards; the chat should not read like an operation log.
- **D-10:** Memory cards must be visually distinct from generic workspace tool cards via `ToolCategory.Memory`.
- **D-11:** The visual treatment should be medium strength: dedicated iconography plus memory-specific border/title/accent styling and a subtle background or tag treatment.
- **D-12:** The differentiation must be noticeable at a glance but must not visually overpower assistant message content.
- **D-13:** The existing card layout and information hierarchy remain recognizable so memory cards still feel part of the same chat system.
- **D-14:** Explicit memory cards persist with chat history and remain attached to the original assistant bubble after reload or restart.
- **D-15:** The sedimentation summary chip also persists with chat history and reattaches to the same assistant bubble on replay.
- **D-16:** Historical replay is part of the feature: users returning later should still be able to see what memory activity happened during that assistant response.
- **D-17:** Phase 68 should extend the existing chat-history persistence path instead of treating memory visibility as transient UI-only state.

### Claude's Discretion
- Exact localized copy for memory card titles and the sedimentation summary chip, as long as the locked information hierarchy above is preserved.
- Exact truncation length and formatting rules for the folded one-line content summary.
- Whether soft-delete wording in the UI says "delete" or "deprecate", provided it remains understandable and consistent with soft-delete semantics.
- Whether `memory_list` keeps generic tool-card styling or joins the memory category, since the locked Phase 68 requirement covers explicit `create` / `update` / `delete` visibility.

### Deferred Ideas (OUT OF SCOPE)
- Per-sedimented-node drilldown or expandable URI list for background sedimentation — out of scope; this phase locks to a single collapsed summary chip.
- Rich memory-specific cards with dedicated fields like keyword diff, old/new content diff, or provenance detail — intentionally deferred by choosing to reuse the existing tool-card skeleton.
- Cross-page/background execution guarantees while the user navigates away — belongs to Phase 69, not this visibility phase.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MEMV-01 | Explicit memory tool calls (create/update/delete) displayed as tool cards in chat bubbles (same pattern as workspace tools) | Reuse the existing `ToolCallInfo` lifecycle from `LLMModule.tool_call.started/completed`; enrich matching cards with memory-specific folded preview fields instead of creating parallel cards. |
| MEMV-02 | Background sedimentation shows a single collapsed "N memories sedimented" summary chip in chat (not per-node) | Add one narrow sedimentation-summary payload and persist it on the assistant message after the initial row insert; do not emit per-node UI records. |
| MEMV-03 | Memory tool cards have distinct visual treatment (`ToolCategory.Memory` CSS class) to differentiate from workspace tools | Add an explicit category/classification field on chat tool-call state and drive CSS/class selection from that field rather than from tool-name string checks in markup. |
</phase_requirements>

## Summary

Phase 68 should be implemented as a thin extension of the existing chat tool-card pipeline, not as a second UI system. `ChatPanel.razor` already listens to `LLMModule.tool_call.started` and `LLMModule.tool_call.completed` and builds `ToolCallInfo` records on the in-flight assistant message. Because the explicit memory tools also publish `Memory.operation`, the correct design is to enrich those existing tool cards with `ToolCategory.Memory`, folded preview fields, and memory-specific styling when the matching memory event arrives. If Phase 68 instead appends a new memory card, explicit operations will double-render.

The harder part is sedimentation replay, not explicit tool cards. `LLMModule.TriggerSedimentation(...)` runs after the final assistant response is published, and `ChatPanel` persists the assistant row immediately after the response completes. That means the sedimentation summary chip cannot rely on the initial `StoreMessageAsync(...)` call alone. Phase 68 needs a post-response persistence update path so one summary chip can be attached to the same assistant message after sedimentation finishes.

The phase can stay small if it keeps three slices separate: enrich explicit cards, add one summary-chip event for background sedimentation, and extend chat-history persistence so both survive reload. No new UI library is needed. `## Validation Architecture` should exist for this phase because `.planning/config.json` does not disable Nyquist validation.

**Primary recommendation:** Extend the current tool-card state model with `ToolCategory.Memory` and folded preview metadata, publish one sedimentation-summary payload, and add a chat-history update path so post-response sedimentation visibility persists on the original assistant message.

## Standard Stack

### Core
| Library / System | Version | Purpose | Why Standard |
|------------------|---------|---------|--------------|
| Razor components in `OpenAnima.Core` | `net8.0` app | Render assistant bubbles, tool cards, and summary chips | Existing chat UI already lives here; Phase 68 is a markup/state extension, not a new frontend stack. |
| Repo `EventBus` + chat event payloads | in repo | Live delivery of tool lifecycle and memory events | `ChatPanel` already uses this path for tool cards, so memory visibility should stay on the same pipeline. |
| `Dapper` | 2.1.72 | Persist and restore chat message metadata | `ChatHistoryService` already serializes tool-call JSON through SQLite. |
| `Microsoft.Data.Sqlite` | 8.0.12 | Durable chat history storage | Existing `chat_messages` table is the required replay path for Phase 68. |

### Supporting
| Library / System | Version | Purpose | When to Use |
|------------------|---------|---------|-------------|
| `xunit` | 2.9.3 | Unit and integration verification | Use for state-mapping, persistence round-trip, and event-order tests. |
| `dotnet test` | SDK 10.0.103 available locally | Test execution | Use for quick targeted validation during plan execution and full-suite phase gates. |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Extending current tool-card model | A separate memory-card component model | Adds duplicate lifecycle, duplicate persistence rules, and higher regression risk for no user-facing gain. |
| Updating persisted assistant message metadata after sedimentation | A transient UI-only chip | Violates D-15/D-16 because replay after reload would lose sedimentation visibility. |
| One quiet summary chip | Per-node sedimentation cards | Conflicts with locked scope and would make chat read like an operation log. |

**Installation:** None — reuse the current app, event bus, and chat persistence stack.

## Architecture Patterns

### Recommended Project Structure
```text
src/OpenAnima.Core/
├── Components/Shared/ChatMessage.razor        # Render memory category cards + sedimentation chip
├── Components/Shared/ChatMessage.razor.css    # Add memory-specific class treatment
├── Components/Shared/ChatPanel.razor          # Subscribe, enrich, and persist message visibility state
├── Services/ChatSessionState.cs               # Add ToolCategory + persisted memory visibility fields
├── ChatPersistence/ChatHistoryService.cs      # Store/restore and post-store update of visibility metadata
├── ChatPersistence/ChatDbInitializer.cs       # Add additive chat schema migration if a new JSON column is used
├── Events/ChatEvents.cs                       # Add sedimentation summary payload
└── Memory/SedimentationService.cs             # Publish one summary event after write count is known
```

### Pattern 1: Enrich Existing Explicit Tool Cards
**What:** Keep the current started/completed card lifecycle and mutate the matching running/completed tool card when `Memory.operation` arrives.

**When to use:** `memory_create`, `memory_update`, and `memory_delete`.

**Example:**
```csharp
var card = current.ToolCalls.LastOrDefault(t =>
    t.ToolName == "memory_create" && t.Status == ToolCallStatus.Running);

if (card is not null)
{
    card.Category = ToolCategory.Memory;
    card.TargetUri = evt.Payload.Uri;
    card.FoldedSummary = BuildOneLineSummary(evt.Payload.Operation, evt.Payload.Content);
}
```

**Source:** existing tool-card matching in `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`, plus event timing from `src/OpenAnima.Core/Tools/MemoryCreateTool.cs`, `src/OpenAnima.Core/Tools/MemoryUpdateTool.cs`, and `src/OpenAnima.Core/Tools/MemoryDeleteTool.cs`.

### Pattern 2: Persist Sedimentation With a Follow-Up Update
**What:** Persist the assistant message normally when the response completes, then update the same stored message when the single sedimentation summary becomes available.

**When to use:** Background sedimentation summary chip only.

**Example:**
```csharp
var messageId = await _chatHistoryService.StoreMessageAsync(...);
assistantMessage.PersistedMessageId = messageId;

// later, when sedimentation completes
assistantMessage.SedimentationSummary = new SedimentationSummaryInfo(count);
await _chatHistoryService.UpdateVisibilityAsync(messageId, assistantMessage, ct);
```

**Source:** assistant rows are stored in `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`; sedimentation is triggered later in `src/OpenAnima.Core/Modules/LLMModule.cs`.

### Pattern 3: Keep Sedimentation Separate From Explicit Tool Calls
**What:** Model the chip as assistant-message metadata, not as a fake tool call.

**When to use:** MEMV-02.

**Example:**
```csharp
public sealed class ChatSessionMessage
{
    public List<ToolCallInfo> ToolCalls { get; } = new();
    public SedimentationSummaryInfo? SedimentationSummary { get; set; }
}
```

**Source:** `src/OpenAnima.Core/Services/ChatSessionState.cs` already keeps message-scoped UI state; the chip belongs on the message, not in the global chat stream.

### Anti-Patterns to Avoid
- **Appending a second explicit memory card:** `Memory.operation` should enrich the existing generic tool card, not create another visible entry.
- **Treating sedimentation as a tool card:** the requirement is a single quiet chip, not a fourth explicit tool lifecycle.
- **Persisting by "latest assistant row" heuristic only:** safe replay needs a stable target row or message reference for post-response updates.
- **Building folded memory previews directly from parameter dumps:** users asked for operation + URI + one-line summary, not raw key/value noise.

## Likely Implementation Slices

### Slice 1: Chat State and Rendering
- Add `ToolCategory` and folded-preview fields to `ToolCallInfo`.
- Add assistant-message sedimentation summary state to `ChatSessionMessage`.
- Update `ChatMessage.razor` and `ChatMessage.razor.css` to render memory-class cards and one collapsed sedimentation chip.

### Slice 2: Live Event Wiring
- Subscribe `ChatPanel.razor` to `Memory.operation`.
- Match memory events back onto the existing running/completed memory tool card.
- Ignore `memory_list` unless the implementer explicitly chooses to style it as memory under the allowed discretion area.

### Slice 3: Sedimentation Summary Event
- Add one narrow payload in `ChatEvents.cs` for sedimentation completion, carrying at minimum `AnimaId` and `Count`.
- Publish it from `SedimentationService` after writes complete, not per written node.
- Attach that summary to the current assistant message in `ChatPanel.razor`.

### Slice 4: Persistence and Replay
- Extend the chat persistence model so memory visibility reloads with chat history.
- Explicit tool cards can continue to serialize through the existing tool-call JSON if the new fields are added there.
- Sedimentation likely needs either a dedicated JSON field/column or a broader message metadata envelope because it is not a tool call and arrives after the initial insert.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Explicit memory card UI | A second component hierarchy for memory-only cards | The existing `ChatMessage.razor` tool-card skeleton | Reuses collapse behavior, badge layout, and replay semantics already proven in Phase 59/66. |
| Live visibility updates | Polling, DB reads, or cross-component callbacks | `EventBus` subscriptions in `ChatPanel.razor` | Events already fire at the exact execution points needed. |
| Sedimentation replay state | A transient in-memory cache only | `ChatHistoryService` plus additive schema update | The phase explicitly requires restart/reload replay. |
| CSS differentiation | Tool-name string checks scattered through Razor markup | A single `ToolCategory.Memory` classification | Easier to test, avoids markup drift, and directly satisfies MEMV-03. |

**Key insight:** The phase is mostly about state ownership and timing. The UI itself is small if the planner keeps all visibility attached to the existing assistant-message model.

## Common Pitfalls

### Pitfall 1: Duplicate Explicit Memory Cards
**What goes wrong:** One generic tool card renders from `LLMModule.tool_call.*`, then a second memory card is appended from `Memory.operation`.

**Why it happens:** Both event streams describe the same explicit operation.

**How to avoid:** Match the memory payload back onto the existing `ToolCallInfo` record and mutate that record in place.

**Warning signs:** The chat bubble shows two entries for one `memory_create` or `memory_update`.

### Pitfall 2: Sedimentation Chip Disappears After Reload
**What goes wrong:** The chip appears live, but replayed chat history does not show it.

**Why it happens:** Sedimentation finishes after the assistant message was already inserted into `chat_messages`.

**How to avoid:** Add a follow-up persistence update path for assistant-message visibility metadata.

**Warning signs:** Live session looks correct; refreshed session loses the sedimentation summary.

### Pitfall 3: Sedimentation Attaches to the Wrong Assistant Bubble
**What goes wrong:** The chip lands on a later assistant message.

**Why it happens:** Current chat state has no explicit message identifier; "last assistant message" is a brittle heuristic.

**How to avoid:** Capture a stable target row/message reference when the assistant response is first persisted.

**Warning signs:** Fast consecutive prompts or delayed sedimentation produce mismatched chips.

### Pitfall 4: Folded Memory Cards Become Too Noisy
**What goes wrong:** The collapsed state looks like a raw parameter/result dump.

**Why it happens:** Existing generic card body data is reused without a dedicated folded-preview model.

**How to avoid:** Add explicit folded-preview fields and truncate the content summary to one line.

**Warning signs:** Memory cards are visually taller and harder to scan than workspace cards when collapsed.

## Code Examples

Verified repo patterns that Phase 68 should extend:

### Existing Tool-Card Match/Mutate Pattern
```csharp
var info = current.ToolCalls.LastOrDefault(t =>
    t.ToolName == evt.Payload.ToolName && t.Status == ToolCallStatus.Running);

if (info != null)
{
    info.ResultSummary = evt.Payload.ResultSummary;
    info.Status = evt.Payload.Success ? ToolCallStatus.Success : ToolCallStatus.Failed;
}
```

**Source:** `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`

### Sedimentation Happens After Response Publication
```csharp
await PublishResponseAsync(responseText, ct);
TriggerSedimentation(animaId, history, responseText);
```

**Source:** `src/OpenAnima.Core/Modules/LLMModule.cs`

### Memory Tools Already Publish Explicit Operation Events
```csharp
await _eventBus.PublishAsync(new ModuleEvent<MemoryOperationPayload>
{
    EventName = "Memory.operation",
    SourceModuleId = "MemoryTools",
    Payload = new MemoryOperationPayload("create", animaId, path, content, null, true)
}, ct);
```

**Source:** `src/OpenAnima.Core/Tools/MemoryCreateTool.cs`

## Open Questions

1. **What persistence shape should hold the sedimentation chip?**
   - What we know: explicit tool cards already round-trip through `tool_calls_json`; sedimentation is not a tool call and arrives later.
   - What's unclear: whether to add a dedicated `memory_visibility_json`/`message_metadata_json` field or broaden the current storage contract.
   - Recommendation: keep sedimentation separate from tool calls and use a dedicated assistant-message metadata path.

2. **Does Phase 68 need a stable persisted message ID?**
   - What we know: replay-safe post-response updates are awkward without one.
   - What's unclear: whether an in-memory reference plus "latest assistant row" is sufficient before Phase 69 introduces background navigation.
   - Recommendation: if the persistence change is small, return/store the inserted message row ID now; it reduces risk and makes Phase 69 safer later.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none |
| Quick run command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~ChatHistoryServiceTests|FullyQualifiedName~ChatSessionStateTests|FullyQualifiedName~LLMModuleSedimentationTests"` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MEMV-01 | Explicit memory tool events enrich existing tool cards and do not duplicate them | unit + integration | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~MemoryVisibility"` | ❌ Wave 0 |
| MEMV-02 | One sedimentation summary chip attaches to the originating assistant message and replays after reload | integration | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~MemoryVisibility|FullyQualifiedName~ChatHistoryService"` | ❌ Wave 0 |
| MEMV-03 | `ToolCategory.Memory` maps to distinct CSS/class treatment without breaking generic cards | unit + manual smoke | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~MemoryVisibility"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~ChatHistoryServiceTests|FullyQualifiedName~ChatSessionStateTests|FullyQualifiedName~MemoryVisibility"`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/MemoryVisibilityStateTests.cs` — category mapping, folded preview formatting, duplicate-card prevention.
- [ ] `tests/OpenAnima.Tests/ChatPersistence/ChatHistoryServiceMemoryVisibilityTests.cs` — persistence round-trip for new memory visibility metadata and post-response update behavior.
- [ ] `tests/OpenAnima.Tests/Integration/MemoryVisibilityIntegrationTests.cs` — started → memory event → completed ordering, plus single sedimentation summary attachment.
- [ ] Manual chat UI smoke for CSS differentiation unless the implementation extracts class-selection logic into a pure helper that can be unit tested.

## Sources

### Primary (HIGH confidence)
- `.planning/phases/68-memory-visibility/68-CONTEXT.md` — locked scope, persistence expectations, and explicit UX constraints
- `.planning/REQUIREMENTS.md` — `MEMV-01`, `MEMV-02`, `MEMV-03`
- `.planning/ROADMAP.md` — phase goal, dependency, and success criteria
- `.planning/STATE.md` — prior decision that full `ToolCallInfo` persistence exists specifically for Phase 68
- `src/OpenAnima.Core/Components/Shared/ChatMessage.razor` — current card rendering path
- `src/OpenAnima.Core/Components/Shared/ChatMessage.razor.css` — current tool-card styling surface
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` — live event subscriptions, tool-card construction, and assistant-message persistence timing
- `src/OpenAnima.Core/Services/ChatSessionState.cs` — current chat state model
- `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs` — current store/restore contract
- `src/OpenAnima.Core/ChatPersistence/ChatDbInitializer.cs` — current `chat_messages` schema
- `src/OpenAnima.Core/Events/ChatEvents.cs` — existing tool and memory payloads
- `src/OpenAnima.Core/Modules/LLMModule.cs` — event order and sedimentation trigger timing
- `src/OpenAnima.Core/Memory/SedimentationService.cs` — final write-count source for the chip
- `src/OpenAnima.Core/Tools/MemoryCreateTool.cs`
- `src/OpenAnima.Core/Tools/MemoryUpdateTool.cs`
- `src/OpenAnima.Core/Tools/MemoryDeleteTool.cs`
- `tests/OpenAnima.Tests/ChatPersistence/ChatHistoryServiceTests.cs`
- `tests/OpenAnima.Tests/Unit/ChatSessionStateTests.cs`
- `tests/OpenAnima.Tests/Unit/LLMModuleSedimentationTests.cs`

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - this phase should reuse existing repo technologies and no new library choice is required.
- Architecture: MEDIUM - the main uncertainty is the exact persistence/update shape for the post-response sedimentation chip.
- Pitfalls: HIGH - duplicate-card risk and post-store sedimentation timing are directly visible in current code flow.

**Research date:** 2026-04-01
**Valid until:** 2026-05-01
