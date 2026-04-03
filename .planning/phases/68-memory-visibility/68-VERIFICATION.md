---
phase: 68-memory-visibility
verified: 2026-04-03T04:25:15Z
status: human_needed
score: 15/15 must-haves verified
human_verification:
  - test: "Live memory create/update/delete rendering in chat"
    expected: "Each explicit memory action appears as a collapsed card in the originating assistant bubble, with memory icon, localized operation title, URI pill, and folded summary behavior; delete shows the recoverable note only in the expanded body."
    why_human: "Requires end-to-end UI observation and interaction, not just static code inspection."
  - test: "Background sedimentation chip timing and placement"
    expected: "Exactly one count-only sedimentation chip appears on the originating assistant bubble before the generic tool-count badge, whether the event lands before or after the assistant row is persisted."
    why_human: "Requires asynchronous runtime timing checks and visual placement validation."
  - test: "Memory-card visual differentiation and localization"
    expected: "Memory cards are visually distinct from generic tool cards, delete cards use the destructive variant without overpowering the whole bubble, and English/Chinese strings resolve from resources."
    why_human: "Visual treatment and localization quality cannot be fully judged from source alone."
---

# Phase 68: Memory Visibility Verification Report

**Phase Goal:** Users can see exactly when and how the agent creates, updates, or deletes memories directly in the chat interface.
**Verified:** 2026-04-03T04:25:15Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

Must-haves were taken from the frontmatter of `68-01-PLAN.md`, `68-02-PLAN.md`, and `68-03-PLAN.md`. Roadmap success criteria for Phase 68 are covered by the verified truths below.

### Observable Truths

| # | Truth | Status | Evidence |
| --- | --- | --- | --- |
| 1 | ChatSessionMessage carries both a persisted row id and an optional sedimentation summary payload for replay. | ✓ VERIFIED | `src/OpenAnima.Core/Services/ChatSessionState.cs:11-18`; restored in `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs:118-138` |
| 2 | ToolCallInfo carries memory-visibility metadata needed for replay: category, normalized target URI, and folded summary. | ✓ VERIFIED | `src/OpenAnima.Core/Services/ChatSessionState.cs:39-49`; populated in `src/OpenAnima.Core/Services/ChatMemoryVisibilityProjector.cs:19-26,53-60`; round-tripped in `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs:179-195` |
| 3 | ChatEvents defines a count-only SedimentationCompletedPayload for the aggregate chip path. | ✓ VERIFIED | `src/OpenAnima.Core/Events/ChatEvents.cs:41-45` |
| 4 | `chat_messages` stores `sedimentation_json` and upgrades existing DBs via `pragma_table_info('chat_messages')`. | ✓ VERIFIED | `src/OpenAnima.Core/ChatPersistence/ChatDbInitializer.cs:17-31,56-62` |
| 5 | ChatHistoryService inserts assistant messages, returns the inserted row id, reloads richer metadata, and patches assistant visibility after the initial insert. | ✓ VERIFIED | `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs:39-88,99-177` |
| 6 | `memory_create`, `memory_update`, and `memory_delete` are classified as `ToolCategory.Memory` as soon as the running tool card is created. | ✓ VERIFIED | `src/OpenAnima.Core/Services/ChatMemoryVisibilityProjector.cs:19-26,98-107`; used at tool-start in `src/OpenAnima.Core/Components/Shared/ChatPanel.razor:164-175` |
| 7 | `memory_list` remains on the generic tool-card path in this phase. | ✓ VERIFIED | `src/OpenAnima.Core/Services/ChatMemoryVisibilityProjector.cs:24,98-107`; only create/update/delete are explicit memory tools |
| 8 | `Memory.operation` hydrates the latest matching memory card with normalized URI and one-line folded summary. | ✓ VERIFIED | Source events in `src/OpenAnima.Core/Tools/MemoryCreateTool.cs:89-94`, `src/OpenAnima.Core/Tools/MemoryUpdateTool.cs:91-96`, `src/OpenAnima.Core/Tools/MemoryDeleteTool.cs:48-53`; projection in `src/OpenAnima.Core/Services/ChatMemoryVisibilityProjector.cs:29-63,147-164`; handler in `src/OpenAnima.Core/Components/Shared/ChatPanel.razor:194-210` |
| 9 | Background sedimentation publishes one aggregate SedimentationCompletedPayload only when the written count is greater than zero. | ✓ VERIFIED | `src/OpenAnima.Core/Memory/SedimentationService.cs:157-196` |
| 10 | ChatPanel attaches the sedimentation summary to the originating assistant bubble and persists it whether the event arrives before or after the assistant row id is known. | ✓ VERIFIED | Event handler `src/OpenAnima.Core/Components/Shared/ChatPanel.razor:212-230`; initial insert stores `PersistenceId` at `:430-446`; late persistence patch at `:540-551` |
| 11 | Explicit memory tool cards reuse the existing collapsible tool-card shell and remain collapsed by default on live render and replay. | ✓ VERIFIED | Same shared loop and toggle at `src/OpenAnima.Core/Components/Shared/ChatMessage.razor:34-66`; collapse state defaults false via `src/OpenAnima.Core/Services/ChatSessionState.cs:49`; replay comes from `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs:118-138` |
| 12 | Folded memory-card headers show operation label, URI, and one-line summary in the order locked by the UI spec. | ✓ VERIFIED | Header order in `src/OpenAnima.Core/Components/Shared/ChatMessage.razor:44-64`; localized titles in `:180-186`; folded one-line summary comes from `src/OpenAnima.Core/Services/ChatMemoryVisibilityProjector.cs:147-164` |
| 13 | `ToolCategory.Memory` cards render with memory-specific classes and a delete-specific variant without affecting generic workspace tool cards. | ✓ VERIFIED | Class selection in `src/OpenAnima.Core/Components/Shared/ChatMessage.razor:196-219`; styles in `src/OpenAnima.Core/Components/Shared/ChatMessage.razor.css:160-166,168-170,252-255` |
| 14 | The sedimentation chip renders once per assistant bubble, before the generic tool-count badge, and never expands into per-node detail. | ✓ VERIFIED | Single summary object in `src/OpenAnima.Core/Services/ChatMemoryVisibilityProjector.cs:65-71`; chip-before-badge render order in `src/OpenAnima.Core/Components/Shared/ChatMessage.razor:100-113`; payload remains count-only in `src/OpenAnima.Core/Memory/SedimentationService.cs:188-195` |
| 15 | All new user-facing copy comes from SharedResources resource keys rather than hard-coded strings in Razor. | ✓ VERIFIED | Razor uses resource lookups at `src/OpenAnima.Core/Components/Shared/ChatMessage.razor:87,182-193`; keys exist in `src/OpenAnima.Core/Resources/SharedResources.resx:659-675` and Chinese translations in `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx:659-675` |

**Score:** 15/15 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
| --- | --- | --- | --- |
| `src/OpenAnima.Core/Services/ChatSessionState.cs` | Tool category, replay ids, sedimentation summary, enriched tool metadata | ✓ VERIFIED | Exists; substantive contract at `:11-18` and `:34-49`; `gsd-tools verify artifacts` passed |
| `src/OpenAnima.Core/Events/ChatEvents.cs` | Sedimentation completion event contract | ✓ VERIFIED | Exists; count-only payload at `:41-45`; `gsd-tools verify artifacts` passed |
| `src/OpenAnima.Core/ChatPersistence/ChatDbInitializer.cs` | `sedimentation_json` schema + additive migration | ✓ VERIFIED | Exists; schema and pragma migration at `:17-31,56-62`; `gsd-tools verify artifacts` passed |
| `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs` | Insert-row-id return path and assistant visibility update API | ✓ VERIFIED | Exists; insert/load/update API at `:39-88,99-177`; `gsd-tools verify artifacts` passed |
| `src/OpenAnima.Core/Services/ChatMemoryVisibilityProjector.cs` | Central memory-tool mapping and folded-summary hydration | ✓ VERIFIED | Exists; create/apply/find methods at `:12-164`; `gsd-tools verify artifacts` passed |
| `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` | Event subscriptions, persistence race handling, runtime wiring | ✓ VERIFIED | Exists; subscriptions and persistence hooks at `:87-99,194-230,430-446,540-551`; `gsd-tools verify artifacts` passed |
| `src/OpenAnima.Core/Memory/SedimentationService.cs` | Aggregate count-only sedimentation event publication | ✓ VERIFIED | Exists; publishes count-only event at `:188-195`; `gsd-tools verify artifacts` passed |
| `src/OpenAnima.Core/Components/Shared/ChatMessage.razor` | Memory card header rendering and sedimentation chip placement | ✓ VERIFIED | Exists; memory-card header at `:34-64`, badge row at `:100-113`; `gsd-tools verify artifacts` passed |
| `src/OpenAnima.Core/Components/Shared/ChatMessage.razor.css` | Memory-specific visual treatment and chip styling | ✓ VERIFIED | Exists; `.tool-card-memory` and chip styles at `:160-166,348-371`; `gsd-tools verify artifacts` passed |
| `src/OpenAnima.Core/Resources/SharedResources.resx` | Localized memory titles and sedimentation chip copy | ✓ VERIFIED | Exists; keys at `:659-675`; `gsd-tools verify artifacts` passed |

### Key Link Verification

| From | To | Via | Status | Details |
| --- | --- | --- | --- | --- |
| `ChatHistoryService.cs` | `ChatSessionState.cs` | JSON round-trip preserves enriched tool and sedimentation metadata | ✓ WIRED | `LoadHistoryAsync` hydrates `PersistenceId`, `ToolCalls`, and `SedimentationSummary` at `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs:118-138` |
| `ChatDbInitializer.cs` | `ChatHistoryService.cs` | `sedimentation_json` exists before store/load/update paths use it | ✓ WIRED | Schema + migration at `src/OpenAnima.Core/ChatPersistence/ChatDbInitializer.cs:17-31,56-62`; service reads/writes the column at `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs:52-53,109-110,160-167` |
| `SedimentationService.cs` | `ChatPanel.razor` | Count-only sedimentation event drives summary-chip attachment | ✓ WIRED | Event published at `src/OpenAnima.Core/Memory/SedimentationService.cs:188-195`; subscribed and applied at `src/OpenAnima.Core/Components/Shared/ChatPanel.razor:98-99,212-230` |
| `ChatPanel.razor` | `ChatHistoryService.cs` | Assistant bubble updates persist after initial insert | ✓ WIRED | `PersistenceId` captured at `src/OpenAnima.Core/Components/Shared/ChatPanel.razor:434-446`; updates persisted at `:540-551` |
| `ChatPanel.razor` | `ChatMemoryVisibilityProjector.cs` | Tool-start and memory-operation mapping stays out of Razor handlers | ✓ WIRED | Projector used at `src/OpenAnima.Core/Components/Shared/ChatPanel.razor:169-171,201-202,227` |
| `ChatMessage.razor` | `ChatSessionState.cs` | Rendering consumes persisted category/URI/folded-summary/sedimentation fields | ✓ WIRED | Props and rendering path at `src/OpenAnima.Core/Components/Shared/ChatPanel.razor:47-51` and `src/OpenAnima.Core/Components/Shared/ChatMessage.razor:47-58,100-113` |
| `ChatMessage.razor` | `SharedResources.resx` | Operation labels and chip copy come from resource keys | ✓ WIRED | Resource lookups at `src/OpenAnima.Core/Components/Shared/ChatMessage.razor:87,182-193`; keys at `src/OpenAnima.Core/Resources/SharedResources.resx:659-675` |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
| --- | --- | --- | --- | --- |
| `src/OpenAnima.Core/Components/Shared/ChatPanel.razor` | `Messages`, `message.ToolCalls`, `message.SedimentationSummary` | SQLite history from `_chatHistoryService.LoadHistoryAsync(...)` plus live `Memory.operation` and `Memory.sedimentation.completed` handlers | Yes | ✓ FLOWING |
| `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs` | `tool_calls_json`, `sedimentation_json`, `PersistenceId` | Real `chat_messages` SQL insert/select/update statements | Yes | ✓ FLOWING |
| `src/OpenAnima.Core/Components/Shared/ChatMessage.razor` | `ToolCalls`, `SedimentationSummary` | Props passed from `ChatPanel` for each `ChatSessionMessage` | Yes | ✓ FLOWING |
| `src/OpenAnima.Core/Memory/SedimentationService.cs` | `writtenUris.Count` | Real memory writes via `_memoryGraph.WriteNodeAsync(...)` inside the extraction loop | Yes | ✓ FLOWING |
| `src/OpenAnima.Core/Services/ChatMemoryVisibilityProjector.cs` | `TargetUri`, `FoldedSummary`, `SedimentationSummary.Count` | `ToolCallStartedPayload`, `MemoryOperationPayload`, and `SedimentationCompletedPayload` values | Yes | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| --- | --- | --- | --- |
| Core phase 68 code compiles | `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj --no-restore -v q` | Provided evidence says build succeeded | ✓ PASS |
| Targeted phase 68 tests pass | `dotnet test tests/OpenAnima.Tests --filter "<phase-68-targets>" --no-restore -v q` | Provided evidence says the targeted memory-visibility test filter succeeded | ✓ PASS |
| Plan must-have artifacts and links resolve | `node /home/user/.codex/get-shit-done/bin/gsd-tools.cjs verify artifacts ...` and `verify key-links ...` for `68-01/02/03-PLAN.md` | Verified 10/10 artifacts and 7/7 key links in this verification run | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
| --- | --- | --- | --- | --- |
| `MEMV-01` | `68-01`, `68-02`, `68-03` | Explicit memory tool calls displayed as tool cards in chat bubbles, using the same pattern as workspace tools | ✓ SATISFIED | Memory tools emit `Memory.operation` at `MemoryCreateTool.cs:89-94`, `MemoryUpdateTool.cs:91-96`, `MemoryDeleteTool.cs:48-53`; projector marks them as `ToolCategory.Memory` at `ChatMemoryVisibilityProjector.cs:19-26`; `ChatMessage.razor:34-64` renders them in the shared collapsible tool-card loop |
| `MEMV-02` | `68-01`, `68-02`, `68-03` | Background sedimentation shows one collapsed `N memories sedimented` summary chip, not per-node detail | ✓ SATISFIED | Count-only event contract at `ChatEvents.cs:41-45`; one aggregate publication at `SedimentationService.cs:188-195`; one summary object on message at `ChatMemoryVisibilityProjector.cs:65-71`; one chip before tool badge at `ChatMessage.razor:100-113` |
| `MEMV-03` | `68-03` | Memory tool cards have distinct visual treatment via `ToolCategory.Memory` CSS class | ✓ SATISFIED | `ToolCategory.Memory` defined at `ChatSessionState.cs:31-49`; class added at `ChatMessage.razor:204-212`; CSS treatment at `ChatMessage.razor.css:160-166,168-170` |

Orphaned requirements: none. Phase 68 in `REQUIREMENTS.md` maps exactly `MEMV-01`, `MEMV-02`, and `MEMV-03`, and all three appear in phase 68 plan frontmatter.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
| --- | --- | --- | --- | --- |
| None | - | No TODO/FIXME/placeholder/stub patterns in phase-touched production files | ℹ️ Info | Regex hits were limited to legitimate nullable branches and optional parameters; no hollow UI or static placeholder paths were found |

### Human Verification Required

### 1. Live Memory Tool Cards

**Test:** Trigger agent-visible `memory_create`, `memory_update`, and `memory_delete` operations from the chat UI.
**Expected:** Each operation appears in the originating assistant bubble as a collapsed memory card using the shared tool-card interaction model, with memory icon, localized title, URI pill, and folded summary behavior; delete shows the recoverable note only when expanded.
**Why human:** This requires end-to-end runtime interaction and visible UI confirmation.

### 2. Sedimentation Chip Timing

**Test:** Trigger a response that causes background sedimentation, including a case where the sedimentation event lands before the assistant row is persisted and a case where it lands afterward.
**Expected:** Exactly one count-only sedimentation chip appears on the originating assistant bubble, before the generic tool-count badge, with no per-node cards.
**Why human:** This depends on asynchronous event timing and visual placement, which static inspection cannot fully confirm.

### 3. Visual Differentiation and Localization

**Test:** Compare a generic workspace tool card and a memory tool card in both English and Chinese UI.
**Expected:** Memory cards show the intended accent treatment, delete cards show the destructive variant without turning the whole card destructive, and all memory/sedimentation labels resolve from localization resources.
**Why human:** Visual styling quality and localization presentation still need a human eye.

### Gaps Summary

No automated gaps were found. All 15 plan-level must-have truths, 10 required artifacts, and 7 key links were verified against the actual codebase. The remaining work is human confirmation of the visible UI behavior and asynchronous in-chat experience.

---

_Verified: 2026-04-03T04:25:15Z_
_Verifier: Claude (gsd-verifier)_
