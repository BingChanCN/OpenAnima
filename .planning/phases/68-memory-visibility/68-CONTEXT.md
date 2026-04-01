# Phase 68: Memory Visibility - Context

**Gathered:** 2026-04-01
**Status:** Ready for planning

<domain>
## Phase Boundary

Expose memory operations inside the existing chat conversation UI. Explicit agent-invoked `memory_create`, `memory_update`, and `memory_delete` actions must appear as collapsible cards within the assistant bubble, and background sedimentation must appear as a single compact summary chip attached to the originating assistant response. This phase does not change memory CRUD semantics, memory recall behavior, `/memory` management surfaces, or cross-page/background execution behavior from Phase 69.

</domain>

<decisions>
## Implementation Decisions

### Explicit memory tool cards
- **D-01:** `memory_create`, `memory_update`, and `memory_delete` reuse the existing chat tool-card skeleton instead of introducing a second bespoke memory-card component model.
- **D-02:** Memory cards keep the same collapsible interaction pattern as current workspace tool cards so the chat UI remains consistent.
- **D-03:** The folded state of each explicit memory card shows: operation label, target URI, and a one-line content summary.
- **D-04:** Expanded state can continue to show parameter and result details through the existing card body pattern; the phase does not require a separate memory-detail layout.
- **D-05:** For delete operations, the folded state still surfaces the operation label and URI even when no content summary exists.

### Background sedimentation visibility
- **D-06:** Background sedimentation is surfaced as exactly one collapsed summary chip per assistant response, never one card or chip per sedimented node.
- **D-07:** The chip displays only the total count of sedimented memories.
- **D-08:** The chip must not break out create/update counts and must not expand into URI-level detail in this phase.
- **D-09:** Sedimentation visibility should remain subordinate to the conversation and explicit tool cards; the chat should not read like an operation log.

### Visual differentiation
- **D-10:** Memory cards must be visually distinct from generic workspace tool cards via `ToolCategory.Memory`.
- **D-11:** The visual treatment should be medium strength: dedicated iconography plus memory-specific border/title/accent styling and a subtle background or tag treatment.
- **D-12:** The differentiation must be noticeable at a glance but must not visually overpower assistant message content.
- **D-13:** The existing card layout and information hierarchy remain recognizable so memory cards still feel part of the same chat system.

### Persistence and replay
- **D-14:** Explicit memory cards persist with chat history and remain attached to the original assistant bubble after reload or restart.
- **D-15:** The sedimentation summary chip also persists with chat history and reattaches to the same assistant bubble on replay.
- **D-16:** Historical replay is part of the feature: users returning later should still be able to see what memory activity happened during that assistant response.
- **D-17:** Phase 68 should extend the existing chat-history persistence path instead of treating memory visibility as transient UI-only state.

### the agent's Discretion
- Exact localized copy for memory card titles and the sedimentation summary chip, as long as the locked information hierarchy above is preserved.
- Exact truncation length and formatting rules for the folded one-line content summary.
- Whether soft-delete wording in the UI says "delete" or "deprecate", provided it remains understandable and consistent with soft-delete semantics.
- Whether `memory_list` keeps generic tool-card styling or joins the memory category, since the locked Phase 68 requirement covers explicit `create` / `update` / `delete` visibility.

</decisions>

<specifics>
## Specific Ideas

- User explicitly preferred reusing the existing tool-card skeleton rather than creating a second memory-only card model.
- Preferred folded-card information hierarchy: operation + URI + one-line content summary.
- Preferred sedimentation UX: one quiet summary chip showing only the total count, not a create/update breakdown and not a URI list.
- User preferred discussing one gray area at a time rather than receiving a batched option dump; downstream agents should keep any follow-up clarification similarly incremental if needed.

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and milestone contract
- `.planning/ROADMAP.md` — Phase 68 goal, dependency on Phase 67, and the three success criteria for memory tool cards, sedimentation summary chip, and `ToolCategory.Memory`.
- `.planning/REQUIREMENTS.md` — `MEMV-01`, `MEMV-02`, `MEMV-03` define the user-visible contract for memory visibility.
- `.planning/PROJECT.md` — v2.0.4 milestone intent includes memory operation visibility in chat as a target feature.
- `.planning/STATE.md` — carries forward the decision that `ChatHistoryService` stores full `ToolCallInfo` objects specifically to enable Phase 68 visibility features.

### Prior phase decisions that constrain implementation
- `.planning/phases/66-platform-persistence/66-CONTEXT.md` — chat history persists per Anima and is restored in full UI history, which constrains replay behavior for memory visibility.
- `.planning/phases/67-memory-tools-sedimentation/67-02-SUMMARY.md` — explicit memory tools already publish `MemoryOperationPayload` events and are the dependency surface for Phase 68.
- `.planning/phases/67-memory-tools-sedimentation/67-01-SUMMARY.md` — delete is soft-delete (`deprecated=true`), which affects wording and user expectations for delete visibility.
- `.planning/milestones/v2.0.2-phases/60-hardening-and-memory-integration/60-UI-SPEC.md` — the established UI contract favors in-system extensions over introducing a disconnected second visual language.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/OpenAnima.Core/Components/Shared/ChatMessage.razor`: already renders collapsible tool cards inside assistant messages and is the natural reuse point for explicit memory cards plus sedimentation chip attachment.
- `src/OpenAnima.Core/Components/Shared/ChatMessage.razor.css`: already contains generic tool-card styling and is the natural place to add `ToolCategory.Memory` presentation rules.
- `src/OpenAnima.Core/Services/ChatSessionState.cs`: `ChatSessionMessage` already stores per-message tool-call state for live rendering and replay.
- `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs`: already serializes and restores per-message `ToolCallInfo`, providing the persistence path that Phase 68 should extend.
- `src/OpenAnima.Core/Events/ChatEvents.cs`: already defines `MemoryOperationPayload`, the shared event contract for memory visibility consumers.

### Established Patterns
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` subscribes to EventBus events and appends tool-visibility state to the current streaming assistant message before the final message is persisted.
- Existing tool visibility uses a simple started/completed lifecycle with collapsible cards; Phase 68 should preserve that interaction model rather than inventing a parallel UX.
- The chat system already distinguishes live streaming state from persisted replay state; memory visibility should follow the same lifecycle.

### Integration Points
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`: subscribe to memory visibility events and attach both explicit memory cards and the single sedimentation chip to the current assistant message.
- `src/OpenAnima.Core/Modules/LLMModule.cs`: current assistant-response lifecycle and tool-call event timing define where sedimentation visibility must anchor.
- `src/OpenAnima.Core/Memory/SedimentationService.cs`: currently knows the final written count but emits no chat-visible event; this is the obvious source for the summary-chip payload.
- `src/OpenAnima.Core/Tools/MemoryCreateTool.cs`, `src/OpenAnima.Core/Tools/MemoryUpdateTool.cs`, `src/OpenAnima.Core/Tools/MemoryDeleteTool.cs`: already publish `Memory.operation` and form the explicit-memory branch of the feature.
- `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs` plus `src/OpenAnima.Core/Services/ChatSessionState.cs`: must carry any added memory-visibility state through store/load so replay matches live behavior.

</code_context>

<deferred>
## Deferred Ideas

- Per-sedimented-node drilldown or expandable URI list for background sedimentation — out of scope; this phase locks to a single collapsed summary chip.
- Rich memory-specific cards with dedicated fields like keyword diff, old/new content diff, or provenance detail — intentionally deferred by choosing to reuse the existing tool-card skeleton.
- Cross-page/background execution guarantees while the user navigates away — belongs to Phase 69, not this visibility phase.

</deferred>

---

*Phase: 68-memory-visibility*
*Context gathered: 2026-04-01*
