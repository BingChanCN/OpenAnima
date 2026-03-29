# Phase 55: Memory Review Surfaces - Research

**Researched:** 2026-03-22
**Domain:** Blazor Server UI extension — MemoryNodeCard collapsible sections, SQLite read-only queries, line-level diff, i18n
**Confidence:** HIGH

## Summary

Phase 55 is a pure frontend extension phase with well-defined backend augmentations. The existing MemoryNodeCard.razor is the sole UI integration point. Three collapsible sections are added: Provenance (expanded by default), Snapshot History (collapsed), and Relationships (collapsed). All backing data already exists in the SQLite database; the only new backend work is adding `GetIncomingEdgesAsync` to IMemoryGraph/MemoryGraph and a `GetStepByIdAsync` method to IRunRepository/RunRepository.

The project uses Blazor Server with scoped CSS, no external component libraries, and a pure CSS dark theme driven by custom properties from `app.css`. All UI strings go through `IStringLocalizer<SharedResources>` with entries in both `SharedResources.en-US.resx` and `SharedResources.zh-CN.resx`. Tests use xUnit with in-memory SQLite via `RunDbConnectionFactory(isRaw: true)`.

The most complex implementation concern is the line-level diff algorithm for snapshot content comparison: a simple line-split approach is sufficient (character-level is explicitly deferred per CONTEXT.md). The second concern is data-fetch timing for lazy sections — snapshots and edges should only be fetched when the user expands the section, not on node select.

**Primary recommendation:** Implement in two plans: Plan 1 covers the backend augmentations (new query methods + i18n keys) and Plan 2 covers the MemoryNodeCard UI extensions (three sections + CSS).

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Snapshot History Display**
- Vertical timeline list within a collapsible section in MemoryNodeCard
- Each timeline entry shows SnapshotAt timestamp + content summary
- Expanding a snapshot shows the full content with highlighted differences compared to the next version (newer snapshot or current content)
- Highlight uses background color to mark changed sections (similar to diff highlighting)
- Each snapshot has a "Restore to this version" button — clicking overwrites the node content with the old version (current content auto-snapshots via existing WriteNodeAsync behavior)
- Timeline ordered newest-first (consistent with GetSnapshotsAsync return order)

**Provenance Inspection**
- Provenance section in MemoryNodeCard expanded by default (since it's typically short)
- When SourceStepId is present: clickable to expand inline StepRecord details (StepType, StartedAt/CompletedAt, Status, Output summary truncated to ~200 chars)
- When SourceArtifactId is present: displayed alongside step info with artifact ID
- When neither SourceStepId nor SourceArtifactId exist (manually created nodes): display "手动创建" / "Manually created" label instead of provenance details
- StepRecord lookup requires querying the run/step database — a new service method or direct query needed to fetch StepRecord by ID

**Edge Relationship Display**
- Collapsible "Relationships" section in MemoryNodeCard showing both outgoing and incoming edges
- Outgoing edges: "This node → Target" with Label and CreatedAt
- Incoming edges: "Source → This node" with Label and CreatedAt
- Separated into two sub-groups: "Outgoing" and "Incoming" for clarity
- Each edge's counterpart URI shows a hover tooltip with the target/source node's content summary (~100 chars)
- Clicking the counterpart URI navigates to that node (selects it in the left URI tree and loads its detail card)
- View-only — no UI-based edge creation or deletion in this phase
- Requires new `GetIncomingEdgesAsync(animaId, toUri)` method on IMemoryGraph (current API only supports outgoing edges via GetEdgesAsync)

**Page Layout & Navigation**
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

### Deferred Ideas (OUT OF SCOPE)
- Visual graph visualization (node+edge diagram)
- Edge creation/deletion from UI
- Snapshot diff at character level (Myers algorithm)
- Provenance deep-link to /runs page
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| MEMUI-01 | User can view snapshot history for a memory node from the `/memory` page | `IMemoryGraph.GetSnapshotsAsync` already returns newest-first ordered `MemorySnapshot` records with `Id`, `Content`, `SnapshotAt`. Collapsible section in MemoryNodeCard with lazy fetch on expand. Line-level diff compares adjacent snapshots. |
| MEMUI-02 | User can inspect the provenance of a memory node or recalled memory from the `/memory` page | `MemoryNode.SourceStepId` and `SourceArtifactId` fields already exist. New `GetStepByIdAsync` method on `IRunRepository` needed to fetch `StepRecord` inline. Provenance section expanded by default. |
| MEMUI-03 | User can inspect memory graph edges through supported tools or UI-backed data surfaces | `IMemoryGraph.GetEdgesAsync` covers outgoing edges. New `GetIncomingEdgesAsync(animaId, toUri)` on `IMemoryGraph`/`MemoryGraph` covers incoming. Node-click navigates via `SelectNode(uri)` call back to `MemoryGraph.razor`. |
</phase_requirements>

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Blazor Server | .NET 10 | Component model, event handling, DI | Project standard — all pages are Blazor Server |
| Dapper | 2.1.72 | SQLite query execution | Project standard — used in all DB operations |
| Microsoft.Data.Sqlite | 8.0.12 | SQLite connection | Project standard — RunDbConnectionFactory wraps it |
| IStringLocalizer<SharedResources> | .NET 10 | i18n for all UI copy | Project standard — every UI string uses this |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xUnit | 2.9.3 | Unit tests | All new backend methods need unit test coverage |
| NullLogger<T> | .NET 10 | Logger stub in tests | Project standard — used in MemoryGraphTests |

### No External Additions
This phase adds **zero** new NuGet packages. All needed dependencies are already present.

**Version verification:** Confirmed from `tests/OpenAnima.Tests/OpenAnima.Tests.csproj` and `src/OpenAnima.Core/RunPersistence/RunRepository.cs`.

---

## Architecture Patterns

### Recommended Project Structure

No new files beyond modifications to existing ones, plus:

```
src/OpenAnima.Core/
├── Memory/
│   ├── IMemoryGraph.cs           # Add GetIncomingEdgesAsync
│   └── MemoryGraph.cs            # Implement GetIncomingEdgesAsync
├── RunPersistence/
│   ├── IRunRepository.cs         # Add GetStepByIdAsync
│   └── RunRepository.cs          # Implement GetStepByIdAsync
├── Components/
│   └── Shared/
│       ├── MemoryNodeCard.razor      # Add 3 collapsible sections
│       └── MemoryNodeCard.razor.css  # Add CSS for new sections
└── Resources/
    ├── SharedResources.en-US.resx  # New Memory.* i18n keys
    └── SharedResources.zh-CN.resx  # Chinese translations

tests/OpenAnima.Tests/Unit/
└── MemoryGraphTests.cs            # Extend with GetIncomingEdgesAsync tests
```

### Pattern 1: Adding IMemoryGraph.GetIncomingEdgesAsync

**What:** New method on IMemoryGraph / MemoryGraph returning edges WHERE `to_uri = @toUri AND anima_id = @animaId`. Symmetrical to the existing `GetEdgesAsync`.

**When to use:** Called from MemoryNodeCard when Relationships section is expanded.

**Example (following existing GetEdgesAsync pattern):**
```csharp
// IMemoryGraph.cs — add after GetEdgesAsync
/// <summary>Returns all edges pointing TO <paramref name="toUri"/> for the given Anima.</summary>
Task<IReadOnlyList<MemoryEdge>> GetIncomingEdgesAsync(string animaId, string toUri, CancellationToken ct = default);

// MemoryGraph.cs — implementation
public async Task<IReadOnlyList<MemoryEdge>> GetIncomingEdgesAsync(string animaId, string toUri, CancellationToken ct = default)
{
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    var rows = await conn.QueryAsync<MemoryEdge>(
        """
        SELECT id AS Id, anima_id AS AnimaId, from_uri AS FromUri,
               to_uri AS ToUri, label AS Label, created_at AS CreatedAt
        FROM memory_edges
        WHERE anima_id = @animaId AND to_uri = @toUri
        ORDER BY id
        """,
        new { animaId, toUri });

    return rows.ToList();
}
```

The `idx_memory_edges_anima` index covers `(anima_id, from_uri)`. A companion index on `(anima_id, to_uri)` should be added in RunDbInitializer for incoming edge performance — add as an additive migration in `MigrateSchemaAsync`.

### Pattern 2: Adding IRunRepository.GetStepByIdAsync

**What:** New method on IRunRepository / RunRepository returning a single `StepRecord?` by `step_id`. Used for provenance inline expansion.

**When to use:** Called from MemoryNodeCard only when user clicks "Show details" on a provenance step.

**Example (following existing RunRepository query patterns):**
```csharp
// IRunRepository.cs — add after GetStepsByRunIdAsync
/// <summary>Returns the step event with the given step ID, or null if not found.</summary>
Task<StepRecord?> GetStepByIdAsync(string stepId, CancellationToken ct = default);

// RunRepository.cs — implementation
public async Task<StepRecord?> GetStepByIdAsync(string stepId, CancellationToken ct = default)
{
    const string sql = """
        SELECT step_id AS StepId, run_id AS RunId, propagation_id AS PropagationId,
               module_name AS ModuleName, status AS Status,
               input_summary AS InputSummary, output_summary AS OutputSummary,
               artifact_ref_id AS ArtifactRefId, error_info AS ErrorInfo,
               duration_ms AS DurationMs, occurred_at AS OccurredAt
        FROM step_events
        WHERE step_id = @stepId
        """;

    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    return await conn.QueryFirstOrDefaultAsync<StepRecord>(sql, new { stepId });
}
```

### Pattern 3: Collapsible Section in Blazor (no library)

**What:** Toggle `bool _isExpanded` via `@onclick` on header div. Conditionally render content. Chevron animation via CSS class on container.

**When to use:** All three new sections (Provenance, Snapshot History, Relationships).

**Example (matching StepTimelineRow.razor pattern):**
```razor
<!-- Section header -->
<div class="section-header @(_provenanceExpanded ? "expanded" : "")"
     role="button"
     tabindex="0"
     aria-expanded="@_provenanceExpanded"
     aria-controls="provenance-section"
     @onclick="() => _provenanceExpanded = !_provenanceExpanded"
     @onkeydown="e => { if (e.Key == "Enter" || e.Key == " ") _provenanceExpanded = !_provenanceExpanded; }">
    <span class="section-chevron">▶</span>
    <span>@L["Memory.Provenance"]</span>
</div>

@if (_provenanceExpanded)
{
    <div id="provenance-section" aria-hidden="false">
        <!-- content -->
    </div>
}
```

The CSS uses `transition: transform 0.15s ease` on `.section-chevron` and `.expanded .section-chevron { transform: rotate(90deg); }` — matching `.step-chevron` in StepTimelineRow.razor.css.

### Pattern 4: Lazy Data Fetch on Section Expand

**What:** Data for Snapshot History and Relationships is only fetched the first time the user expands the section, not on node select.

**When to use:** Both collapsed-by-default sections.

**Example:**
```csharp
private bool _snapshotsLoaded;
private IReadOnlyList<MemorySnapshot> _snapshots = Array.Empty<MemorySnapshot>();

private async Task ToggleSnapshotHistory()
{
    _snapshotHistoryExpanded = !_snapshotHistoryExpanded;
    if (_snapshotHistoryExpanded && !_snapshotsLoaded)
    {
        _snapshots = await MemoryGraphService.GetSnapshotsAsync(Node.AnimaId, Node.Uri);
        _snapshotsLoaded = true;
    }
}
```

On `OnParametersSet()` (node switch), reset `_snapshotsLoaded = false` and `_edgesLoaded = false` so data refreshes for the new node.

### Pattern 5: Line-Level Diff for Snapshot Content

**What:** Split both "old" (snapshot) and "new" (current or next-newer snapshot) content on `\n`. Use LCS (longest common subsequence) or a simple diff algorithm to produce `Added`, `Removed`, `Unchanged` line annotations.

**When to use:** When a snapshot row is expanded to show diff.

**Implementation approach (no library, simple O(n²) acceptable for ≤500-line nodes):**
```csharp
// Pure C# helper — no external dependency
private static IReadOnlyList<(DiffKind Kind, string Line)> ComputeLineDiff(string oldText, string newText)
{
    var oldLines = oldText.Split('\n');
    var newLines = newText.Split('\n');
    // Myers or simple LCS; for this phase a basic two-pointer LCS is sufficient
    // ...
}

public enum DiffKind { Unchanged, Added, Removed }
```

The UI renders each line as a `<div>` with class `diff-line-added`, `diff-line-removed`, or `diff-line-unchanged`. CSS applies the background colors from the UI-SPEC.

### Pattern 6: Edge Hover Tooltip (CSS-only, no library)

**What:** An absolutely-positioned `<div>` that appears on `@onmouseover` and disappears on `@onmouseout`. Uses CSS `position: absolute`, `z-index: 100`, `opacity` transition.

**When to use:** Counterpart URI in edge rows.

**Example:**
```razor
<span class="edge-uri-wrapper"
      @onmouseover="() => _tooltipUri = edge.ToUri"
      @onmouseout="() => _tooltipUri = null">
    <button class="edge-uri-link" @onclick="() => NavigateToNode(edge.ToUri)" ...>
        @edge.ToUri
    </button>
    @if (_tooltipUri == edge.ToUri && _nodeContentCache.TryGetValue(edge.ToUri, out var tip))
    {
        <div class="edge-tooltip" role="tooltip">@Truncate(tip, 100)</div>
    }
</span>
```

Node content for tooltips requires fetching `GetNodeAsync` for each counterpart URI when the Relationships section loads. Cache results in `Dictionary<string, string>` (`_nodeContentCache`).

### Pattern 7: Restore Confirmation

**What:** Reuse the existing `confirm-overlay` + `confirm-dialog` pattern from MemoryGraph.razor. The overlay is rendered inside MemoryNodeCard, not in the parent. Uses `EventCallback` to communicate the restore action back up if needed, or the card handles it directly via injected `IMemoryGraph`.

**When to use:** User clicks "Restore to this version" on any snapshot row.

**Key note:** `MemoryNodeCard` currently doesn't inject `IMemoryGraph` directly — it receives `MemoryNode` as a `[Parameter]` and communicates saves via `OnSaveNode` EventCallback. For the restore operation, the card should either (a) call `OnSaveNode.InvokeAsync(nodeWithOldContent)` — simplest, reuses existing flow — or (b) inject `IMemoryGraph` directly. Option (a) is preferred: it reuses WriteNodeAsync auto-snapshot behavior and the parent's `SaveNode` handler refreshes the node list and reloads `_selectedNode`.

### Anti-Patterns to Avoid

- **Fetching all data on node select:** Do not call GetSnapshotsAsync or GetEdgesAsync during `OnParametersSet`. Fetch lazily on section expand to avoid N×3 queries per tree navigation.
- **Skipping OnParametersSet reset:** If `_snapshotsLoaded` / `_edgesLoaded` are not reset when Node changes, the card will show stale data from the previous node.
- **Injecting IRunRepository into a Blazor component:** The existing provenance lookup needs `IRunRepository.GetStepByIdAsync`. Add this dependency injection to MemoryNodeCard sparingly; the card already injects `IStringLocalizer` only. Injecting `IRunRepository` is acceptable for this use case since it's already registered in DI.
- **Direct Dapper queries in Razor components:** All DB access must go through repository/service interfaces — never raw Dapper in a `.razor` file.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Incoming edges query | Custom SQL in component | `IMemoryGraph.GetIncomingEdgesAsync` (new method) | Keeps all SQLite access behind the interface; testable |
| StepRecord lookup | Direct Dapper in Razor | `IRunRepository.GetStepByIdAsync` (new method) | Keeps repository pattern consistent |
| Tooltip library | Any npm or NuGet library | CSS absolute positioning | Project is library-free; existing confirm-overlay proves the pattern |
| Animation library | Framer, Anime.js, etc. | CSS `transition: transform 0.15s ease` | StepTimelineRow already demonstrates this is sufficient |
| Diff library | DiffPlex, Google Diff | Simple C# line-split LCS helper | Character-level diff is deferred; line-level is ~30 lines of code |

**Key insight:** Every "complex" UI pattern in this phase (collapsibles, overlays, tooltips, status colors) already exists in the project — StepTimelineRow, MemoryGraph's confirm overlay, and MemoryNodeCard itself. The task is replication, not invention.

---

## Common Pitfalls

### Pitfall 1: Stale Section Data After Node Switch
**What goes wrong:** User views snapshots for node A, then selects node B. The snapshot section still shows node A's snapshots.
**Why it happens:** `_snapshotsLoaded` flag is not reset in `OnParametersSet()`.
**How to avoid:** In `OnParametersSet()`, always reset `_snapshotsLoaded = false`, `_edgesLoaded = false`, `_stepDetails = null`, and collapse sections back to defaults (`_snapshotHistoryExpanded = false`, `_relationshipsExpanded = false`).
**Warning signs:** Test by selecting node A (expand snapshots), then select node B — snapshot section should show empty or node B's data.

### Pitfall 2: Missing Index for to_uri Edge Query
**What goes wrong:** `GetIncomingEdgesAsync` does a table scan on `memory_edges` for `to_uri`. Slow for large graphs.
**Why it happens:** Existing index only covers `(anima_id, from_uri)`.
**How to avoid:** Add `CREATE INDEX IF NOT EXISTS idx_memory_edges_to_uri ON memory_edges(anima_id, to_uri)` in `RunDbInitializer.MigrateSchemaAsync`.
**Warning signs:** No observable issue in testing with small data sets; becomes a problem at scale.

### Pitfall 3: Diff "Next Version" Direction Confusion
**What goes wrong:** Diff shows "added" and "removed" from the wrong perspective — user sees "added" for content that was actually removed in the newer version.
**Why it happens:** Snapshot list is newest-first. Snapshot[0] is the most recent old version; Snapshot[N] is the oldest. "Current content" is newer than Snapshot[0].
**How to avoid:** For each snapshot row, compute diff as `(snapshot.Content = old, newerContent = new)`. "NewerContent" is either the current node content (for Snapshot[0]) or Snapshot[i-1].Content (for Snapshot[i]). Green = lines added in newer / not in this snapshot. Red = lines in this snapshot / removed in newer.
**Warning signs:** Diff shows everything as "removed" instead of "added" or vice versa.

### Pitfall 4: MemoryGraph.razor SelectNode vs MemoryNodeCard Navigation
**What goes wrong:** Clicking a counterpart URI in the edge list doesn't update the left-side tree selection.
**Why it happens:** MemoryNodeCard doesn't have direct access to MemoryGraph's `SelectNode` method or `_selectedUri` state.
**How to avoid:** Add a new `EventCallback<string> OnNavigateToUri` parameter on MemoryNodeCard. MemoryGraph.razor binds it: `OnNavigateToUri="SelectNode"`. Edge counterpart clicks call `await OnNavigateToUri.InvokeAsync(uri)`. This follows the same pattern as `OnSaveNode` and `OnDeleteNode` already used.
**Warning signs:** Edge URI click updates the detail panel but the left tree still shows the old selection highlighted.

### Pitfall 5: StepRecord Not Found (stepId references old/deleted run)
**What goes wrong:** User clicks "Show details" on provenance but the step record no longer exists (e.g., database was reset, or step was from a very old run).
**Why it happens:** `GetStepByIdAsync` returns null; component doesn't handle null gracefully.
**How to avoid:** After fetch, if null: set `_stepFetchError = true` and render the error copy ("Could not load step details...") from UI-SPEC. Never throw; silently degrade.
**Warning signs:** Unhandled NullReferenceException in the Razor component.

### Pitfall 6: Tooltip Content Fetch Latency
**What goes wrong:** Hover tooltip shows empty until async fetch completes; flickers on fast hover/unhover.
**Why it happens:** `GetNodeAsync` is async; content isn't available at hover time.
**How to avoid:** Pre-fetch all counterpart node contents when Relationships section first loads (not at hover time). Cache results in `_nodeContentCache`. Tooltip then reads from cache synchronously. If cache misses, show nothing (no error).
**Warning signs:** Tooltip appears blank on first hover, then correct on second hover.

---

## Code Examples

Verified patterns from existing codebase:

### Collapsible Header with Chevron (CSS pattern from StepTimelineRow)
```css
/* In MemoryNodeCard.razor.css */
.section-header {
    display: flex;
    align-items: center;
    gap: 8px;
    min-height: 44px;
    cursor: pointer;
    padding: 8px 0;
    border-bottom: 1px solid var(--border-color);
}

.section-chevron {
    font-size: 12px;
    color: var(--text-muted);
    transition: transform 0.15s ease;
    display: inline-block;
}

.section-header.expanded .section-chevron {
    transform: rotate(90deg);
    color: var(--accent-color);
}
```

### Restore Button Following btn-cancel Style
```css
/* btn-cancel is defined in MemoryGraph.razor.css, not in MemoryNodeCard */
/* Use ghost button style inline: */
.btn-restore {
    background: none;
    border: 1px solid var(--border-color);
    color: var(--text-secondary);
    border-radius: 4px;
    padding: 4px 8px;
    font-size: 14px;
    cursor: pointer;
}
```

### i18n Key Naming Convention
```
Memory.SnapshotHistory         → "Snapshot History"     / "快照历史"
Memory.Provenance              → "Provenance"           / "来源"
Memory.Relationships           → "Relationships"        / "关联关系"
Memory.Outgoing                → "Outgoing"             / "出边"
Memory.Incoming                → "Incoming"             / "入边"
Memory.ManuallyCreated         → "Manually created"     / "手动创建"
Memory.RestoreButton           → "Restore to this version" / "恢复到此版本"
Memory.ShowDiff                → "Show diff"            / "显示差异"
Memory.HideDiff                → "Hide diff"            / "隐藏差异"
Memory.ShowStepDetails         → "Show details"         / "显示详情"
Memory.HideStepDetails         → "Hide details"         / "隐藏详情"
Memory.EmptySnapshots          → "No previous snapshots." / "暂无历史快照。"
Memory.EmptyOutgoing           → "No outgoing relationships." / "暂无出边关系。"
Memory.EmptyIncoming           → "No incoming relationships." / "暂无入边关系。"
Memory.RestoreConfirmTitle     → "Restore to this version?" / "恢复到此版本？"
Memory.RestoreConfirmBody      → "Current content will be saved as a snapshot. This replaces the current node content." / "当前内容将保存为快照，并被此版本替换。"
Memory.RestoreConfirmAction    → "Restore"              / "恢复"
Memory.RestoreConfirmCancel    → "Keep current"         / "保留当前"
Memory.RestoreFlash            → "Restored"             / "已恢复"
Memory.ProvenanceStepError     → "Could not load step details. Try selecting another node, then return to this one." / "无法加载步骤详情，请尝试选择其他节点后重新查看。"
Memory.ProvenanceEdgeError     → "Could not load relationships. Refresh the page to retry." / "无法加载关系，请刷新页面重试。"
```

Key naming follows existing pattern: `Domain.SubKey` (Providers.*, RunDetail.*, etc.).

### MemoryNodeCard New Parameters Required
```csharp
// New EventCallback needed for cross-node navigation from edge relationships:
[Parameter] public EventCallback<string> OnNavigateToUri { get; set; }

// MemoryGraph.razor binding:
<MemoryNodeCard Node="_selectedNode"
                OnSaveNode="SaveNode"
                OnDeleteNode="ConfirmDelete"
                OnNavigateToUri="SelectNode" />
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Provenance shown as plain text only | Provenance expandable with StepRecord inline | Phase 55 | Users can see what produced the memory node |
| Snapshot retention only (no UI) | Snapshot timeline with diff view and restore | Phase 55 | Users can audit and revert memory changes |
| Edges only accessible via LLM tools | Edge list visible in UI | Phase 55 | Satisfies MEMUI-03 "UI-backed data surfaces" |

**Deprecated/outdated:**
- The existing "Provenance" label block in MemoryNodeCard.razor (lines 42–60) will be replaced by the new collapsible Provenance section. The `.node-provenance` and `.node-timestamps` CSS classes remain but the HTML structure changes.

---

## Open Questions

1. **MemoryNodeCard: inject IRunRepository directly vs. new service wrapper**
   - What we know: `IRunRepository` has `GetStepByIdAsync` (to be added). `MemoryNodeCard` currently injects only `IStringLocalizer`.
   - What's unclear: Whether to inject `IRunRepository` directly into the card, or create a thin `IMemoryProvenanceService` wrapper.
   - Recommendation: Inject `IRunRepository` directly. The card already does DB-adjacent work via event callbacks; adding one more DI dependency is acceptable. A thin wrapper adds interface ceremony for no testability gain since `RunRepository` already has an interface.

2. **Restore action: card-owned or parent-owned**
   - What we know: Current save path goes through `OnSaveNode` EventCallback to MemoryGraph.razor's `SaveNode` method, which calls `WriteNodeAsync`.
   - What's unclear: Whether to have the card call `OnSaveNode` with the old content (reuses existing flow) vs. handle restore directly via injected `IMemoryGraph`.
   - Recommendation: Use `OnSaveNode.InvokeAsync(nodeWithRestoredContent)` — reuses WriteNodeAsync auto-snapshot behavior and parent's state refresh. Simpler, no new injection needed.

3. **Tooltip fetch timing for content summaries**
   - What we know: Each edge counterpart URI needs a content summary for tooltip. `GetNodeAsync` is async.
   - What's unclear: Whether fetching all counterpart nodes during Relationships section load causes observable latency.
   - Recommendation: Fetch all counterpart nodes when Relationships section first opens (parallel Task.WhenAll for outgoing + incoming counterparts). Cache in `_nodeContentCache`. The number of edges per node is typically small (< 20) so parallel fetch is fine.

---

## Validation Architecture

> `workflow.nyquist_validation` is not present in `.planning/config.json` — treat as enabled.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | No xunit.runner.json — uses .csproj defaults |
| Quick run command | `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~MemoryGraphTests" --no-build` |
| Full suite command | `dotnet test tests/OpenAnima.Tests --no-build` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MEMUI-01 | `GetSnapshotsAsync` returns correct snapshots | unit | `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~MemoryGraphTests" --no-build` | ✅ (MemoryGraphTests.cs — extend) |
| MEMUI-02 | `GetStepByIdAsync` returns step or null | unit | `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~RunRepositoryTests" --no-build` | ✅ (RunRepositoryTests.cs — extend) |
| MEMUI-03 | `GetIncomingEdgesAsync` returns incoming edges | unit | `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~MemoryGraphTests" --no-build` | ✅ (MemoryGraphTests.cs — extend) |
| MEMUI-01 | Snapshot diff: line-level correct result | unit | `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~SnapshotDiffTests" --no-build` | ❌ Wave 0 — new file needed |
| MEMUI-01/02/03 | UI interactions (restore, expand, navigate) | manual | n/a | manual-only — no Blazor test framework in project |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~MemoryGraphTests|FullyQualifiedName~RunRepositoryTests" --no-build`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests --no-build`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/SnapshotDiffTests.cs` — covers the line-level diff helper (pure C# logic, no DB needed)

*(All other test infrastructure exists. MemoryGraphTests and RunRepositoryTests use established in-memory SQLite fixture pattern.)*

---

## Sources

### Primary (HIGH confidence)
- Direct file reads: `IMemoryGraph.cs`, `MemoryGraph.cs`, `MemoryNode.cs`, `MemorySnapshot.cs`, `MemoryEdge.cs` — confirmed API contracts
- Direct file reads: `IRunRepository.cs`, `RunRepository.cs`, `StepRecord.cs`, `StepRecorder.cs` — confirmed step lookup approach
- Direct file reads: `MemoryNodeCard.razor`, `MemoryNodeCard.razor.css`, `MemoryGraph.razor` — confirmed component structure and CSS patterns
- Direct file reads: `StepTimelineRow.razor`, `StepTimelineRow.razor.css` — confirmed collapsible/chevron pattern
- Direct file reads: `RunDbInitializer.cs` — confirmed schema, existing indexes, migration pattern
- Direct file reads: `SharedResources.en-US.resx`, `SharedResources.zh-CN.resx` — confirmed i18n key naming convention
- Direct file reads: `55-CONTEXT.md`, `55-UI-SPEC.md` — locked decisions, visual spec

### Secondary (MEDIUM confidence)
- `.planning/config.json` — confirmed `nyquist_validation` key absent (treat as enabled)
- `.planning/STATE.md` — confirmed accumulated decisions from phases 50–54

### Tertiary (LOW confidence)
- None — all findings are directly verified from codebase

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries confirmed from .csproj files
- Architecture: HIGH — all patterns traced to existing code in the project
- Pitfalls: HIGH — derived from direct code inspection (concrete field names, method signatures)
- Diff algorithm: MEDIUM — approach is chosen (line-level) but exact implementation left to Claude's discretion per CONTEXT.md

**Research date:** 2026-03-22
**Valid until:** Stable — no external dependencies. Valid until project code structure changes.
