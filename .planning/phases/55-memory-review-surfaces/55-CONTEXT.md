# Phase 55: Memory Review Surfaces - Context

**Gathered:** 2026-03-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can inspect how memory changed over time, why it exists (provenance), and how it connects to other memory — all from the existing `/memory` page. This phase extends the current MemoryNodeCard and MemoryGraph page with snapshot history browsing, provenance inspection, and edge relationship viewing. New memory capabilities (tools, sedimentation, recall) are delivered by prior phases (52–54).

</domain>

<decisions>
## Implementation Decisions

### Snapshot History Display
- Vertical timeline list within a collapsible section in MemoryNodeCard
- Each timeline entry shows SnapshotAt timestamp + content summary
- Expanding a snapshot shows the full content with highlighted differences compared to the next version (newer snapshot or current content)
- Highlight uses background color to mark changed sections (similar to diff highlighting)
- Each snapshot has a "Restore to this version" button — clicking overwrites the node content with the old version (current content auto-snapshots via existing WriteNodeAsync behavior)
- Timeline ordered newest-first (consistent with GetSnapshotsAsync return order)

### Provenance Inspection
- Provenance section in MemoryNodeCard expanded by default (since it's typically short)
- When SourceStepId is present: clickable to expand inline StepRecord details (StepType, StartedAt/CompletedAt, Status, Output summary truncated to ~200 chars)
- When SourceArtifactId is present: displayed alongside step info with artifact ID
- When neither SourceStepId nor SourceArtifactId exist (manually created nodes): display "手动创建" / "Manually created" label instead of provenance details
- StepRecord lookup requires querying the run/step database — a new service method or direct query needed to fetch StepRecord by ID

### Edge Relationship Display
- Collapsible "Relationships" section in MemoryNodeCard showing both outgoing and incoming edges
- Outgoing edges: "This node → Target" with Label and CreatedAt
- Incoming edges: "Source → This node" with Label and CreatedAt
- Separated into two sub-groups: "Outgoing" and "Incoming" for clarity
- Each edge's counterpart URI shows a hover tooltip with the target/source node's content summary (~100 chars)
- Clicking the counterpart URI navigates to that node (selects it in the left URI tree and loads its detail card)
- View-only — no UI-based edge creation or deletion in this phase
- Requires new `GetIncomingEdgesAsync(animaId, toUri)` method on IMemoryGraph (current API only supports outgoing edges via GetEdgesAsync)

### Page Layout & Navigation
- All new information integrated into existing MemoryNodeCard as collapsible sections (no new pages or tabs)
- Section order in card: URI pill → Content editor → Disclosure Trigger → Keywords → **Provenance** (default expanded) → **Snapshot History** (default collapsed) → **Relationships** (default collapsed) → Action buttons (Save/Delete)
- Collapsible sections use chevron icon toggle (▶/▼) with section header text
- Overall page layout unchanged: left URI tree panel + right detail panel
- Edge hover tooltip implemented as CSS-positioned popup (no external tooltip library)

### Claude's Discretion
- Exact diff highlighting algorithm (character-level or line-level)
- CSS styling details for timeline, collapsible sections, and hover tooltips
- StepRecord query implementation approach (new service method vs direct Dapper query)
- Hover tooltip positioning logic and animation
- Content summary truncation strategy for edge tooltips
- i18n key naming for new UI strings

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` — MEMUI-01 (snapshot history), MEMUI-02 (provenance inspection), MEMUI-03 (edge inspection)

### Existing /memory UI
- `src/OpenAnima.Core/Components/Pages/MemoryGraph.razor` — Current /memory page with URI tree + detail panel, node CRUD, search filter
- `src/OpenAnima.Core/Components/Shared/MemoryNodeCard.razor` — Node detail card with content editor, disclosure trigger, keywords, basic provenance display, save/delete actions

### Memory Graph API
- `src/OpenAnima.Core/Memory/IMemoryGraph.cs` — GetSnapshotsAsync (snapshot history), GetEdgesAsync (outgoing edges), GetNodeAsync, WriteNodeAsync (used for restore)
- `src/OpenAnima.Core/Memory/MemorySnapshot.cs` — Snapshot record: Id, Uri, AnimaId, Content, SnapshotAt
- `src/OpenAnima.Core/Memory/MemoryEdge.cs` — Edge record: Id, AnimaId, FromUri, ToUri, Label, CreatedAt
- `src/OpenAnima.Core/Memory/MemoryGraph.cs` — SQLite implementation with snapshot versioning, needs new GetIncomingEdgesAsync method
- `src/OpenAnima.Core/Memory/MemoryNode.cs` — Node record with SourceStepId, SourceArtifactId provenance fields

### Run System (for provenance lookup)
- `src/OpenAnima.Core/Runs/StepRecorder.cs` — StepRecord persistence, need to expose read-by-ID capability
- `src/OpenAnima.Core/Runs/RunService.cs` — Run lifecycle, step querying

### Prior Phase Contexts
- `.planning/phases/52-automatic-memory-recall/52-CONTEXT.md` — MemoryRecallService architecture, StepRecord observability pattern
- `.planning/phases/53-tool-aware-memory-operations/53-CONTEXT.md` — memory_link tool creating edges, edge provenance via StepRecord
- `.planning/phases/54-living-memory-sedimentation/54-CONTEXT.md` — Auto-sedimentation creating sediment:// nodes with provenance and snapshots

### i18n
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` — Chinese localization strings
- `src/OpenAnima.Core/Resources/SharedResources.resx` — English fallback strings

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IMemoryGraph.GetSnapshotsAsync()`: Returns snapshot history for a node, newest first, max 10 per node — ready for snapshot timeline
- `IMemoryGraph.GetEdgesAsync()`: Returns outgoing edges from a URI — ready for outgoing edge list
- `IMemoryGraph.WriteNodeAsync()`: Auto-snapshots before update — restore feature simply calls this with old content
- `MemoryNodeCard`: Existing card component with content/trigger/keyword editing — extend with new collapsible sections
- `IStringLocalizer<SharedResources>`: Existing i18n pattern for all UI strings

### Established Patterns
- Pure CSS dark theme: No component library — all new UI elements use custom CSS consistent with existing styles
- Blazor component parameters: MemoryNodeCard uses `[Parameter]` and `EventCallback<T>` for parent communication
- Confirmation dialogs: MemoryGraph.razor has confirm overlay pattern (for delete) — restore confirmation can follow same pattern
- IMemoryGraph singleton: All memory operations go through DI-injected IMemoryGraph

### Integration Points
- `MemoryNodeCard.razor`: Primary integration point — add collapsible sections for history, provenance, relationships
- `MemoryGraph.razor`: May need to pass additional data (snapshots, edges) to MemoryNodeCard, or card can fetch its own data
- `IMemoryGraph`: Add GetIncomingEdgesAsync method for incoming edge queries
- `StepRecorder` / `RunService`: Need read access to StepRecord by ID for provenance inline expansion

</code_context>

<specifics>
## Specific Ideas

- Snapshot timeline visual: each entry is a row with timestamp on the left, content preview on the right, expand button to show full content with diff highlights
- Diff highlighting: green background for content present in newer version but not in this snapshot, red/strikethrough for content removed in newer version
- Restore confirmation: follow existing confirm overlay pattern — "Restore to this version? Current content will be saved as a snapshot."
- Provenance "手动创建" label should use a subtle icon (e.g., pencil) to visually distinguish from auto-generated provenance
- Edge hover tooltip: shows first ~100 chars of the target/source node content, positioned near the hovered URI text

</specifics>

<deferred>
## Deferred Ideas

- Visual graph visualization (node+edge diagram) — future enhancement when the graph becomes complex enough to need it
- Edge creation/deletion from UI — current phase is view-only per MEMUI-03 "inspect" definition
- Snapshot diff at character level (Myers algorithm) — start with line-level highlighting, enhance later if needed
- Provenance deep-link to /runs page — current phase uses inline expansion, direct page navigation deferred

</deferred>

---

*Phase: 55-memory-review-surfaces*
*Context gathered: 2026-03-22*
