# Phase 48: Artifact & Memory Foundation - Research

**Researched:** 2026-03-21
**Domain:** SQLite artifact persistence, graph-based memory, Blazor UI integration
**Confidence:** HIGH

## Summary

Phase 48 builds two tightly coupled subsystems on top of the existing Phase 45-47 run/step infrastructure. The artifact store is a straightforward extension: a new `artifacts` table in `runs.db`, content files on disk at `data/artifacts/{runId}/{artifactId}.ext`, and a hook in `StepRecorder.RecordStepCompleteAsync` to write the artifact and populate `ArtifactRefId`. The memory graph is more novel — a Nocturne-inspired URI-routed node/edge store with disclosure triggers, Aho-Corasick glossary linking, snapshot versioning, and System Boot identity injection. Both subsystems follow the established Dapper/SQLite/record-type/SemaphoreSlim patterns exactly.

The UI has two surfaces: `ArtifactViewer` embedded in the existing `StepTimelineRow` accordion (Phase 47 component), and a new `/memory` page with a URI tree + `MemoryNodeCard` detail panel. The UI-SPEC is fully approved and prescribes exact layout, MIME rendering rules, color tokens, and copy strings.

No new NuGet packages are required. Aho-Corasick can be hand-rolled as a simple trie (the keyword set is small — agent glossary terms, not web-scale). Markdown rendering already uses Markdig (already in the project). JSON tree rendering is a hand-rolled Blazor component following the existing pattern.

**Primary recommendation:** Implement in four sequential waves — (1) DB schema + IArtifactStore, (2) IMemoryGraph + memory tables, (3) StepRecorder hook + MemoryModule wiring, (4) Blazor UI components.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Artifact storage: SQLite metadata in `artifacts` table within `data/runs.db` + filesystem content at `data/artifacts/{runId}/{artifactId}.ext`
- Artifact metadata columns: artifact ID, run ID, step ID, MIME type, file size, file path, timestamp
- Classification by MIME type (text/plain, text/markdown, application/json, text/html) — generic and extensible
- Every artifact must have runId + stepId — no orphan artifacts
- Lifecycle follows run: deleting a run cleans up artifact files and metadata
- StepRecorder populates ArtifactRefId when writing step completion
- Memory graph: URI tree + free edges ("Graph Backend, Tree Frontend" like Nocturne)
- URI path as primary index (e.g., `core://agent/identity`, `run://abc123/findings`, `project://myapp/architecture`)
- Free edges (aliases/links) between nodes for cross-node associations
- Node–Memory–Edge topology: nodes hold content, edges express relationships with typed labels
- Memory retrieval: disclosure conditional triggers + URI path queries
- Disclosure routing: each memory node binds a human-readable trigger condition; system scans and injects matching memories contextually
- URI path queries: structured queries by path prefix and tag filtering
- Glossary auto-hyperlinking (豆辞典): Aho-Corasick multi-pattern matching
- Memory write model: agent autonomous CRUD + system-derived from artifacts
- Both sources carry full provenance metadata (source artifact ID, source step ID, timestamp)
- Automatic snapshots + version history on every write
- Memory scope: per-Anima private graphs with cross-Anima sharing via CrossAnimaRouter pattern
- System Boot: each Anima configures core memory URIs; on run start, Boot memories auto-inject as first steps
- Boot memory injection appears as explicit step records in the run timeline
- Memory injection: module ports (wiring graph) + tool calls (LLM tool surface)
- MemoryModule with input ports (query request, write request) and output ports (retrieval results)
- Memory CRUD exposed as workspace tools for LLM direct invocation
- Injected memory appears as explicit step records with provenance links
- Artifact viewing: inline in step detail accordion, smart rendering by MIME type
- Truncated preview: first 200 lines or 10KB + "查看完整内容" expand button
- Memory provenance links clickable and navigable to source

### Claude's Discretion
- SQLite `artifacts` table schema design (exact columns, indexes, migrations)
- Memory graph SQLite schema (nodes, edges, snapshots tables)
- Aho-Corasick implementation details for glossary auto-hyperlinking
- Disclosure condition matching algorithm (exact string, regex, or LLM-based)
- Exact truncation thresholds for artifact inline preview
- MemoryModule port schema and event types
- Memory tool parameter schemas for LLM tool surface
- Snapshot storage format and retention policy for version history
- Auto-derivation rules for system-generated memory records from artifacts

### Deferred Ideas (OUT OF SCOPE)
- Vector/embedding-based memory retrieval (VEC-01) — deferred to v2.x per REQUIREMENTS.md
- Memory dashboard/management UI (human audit panel like Nocturne's React dashboard) — could be a future phase
- Memory export/import between Anima instances — future phase
- Memory conflict resolution for cross-Anima shared memories — future phase
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| ART-01 | System can persist intermediate notes, reports, and final outputs as durable artifacts linked to run and step records | IArtifactStore + artifacts table + filesystem content; StepRecorder hook writes artifact and sets ArtifactRefId |
| ART-02 | User can inspect run artifacts from the run inspector with source linkage back to the generating step | ArtifactViewer component in StepTimelineRow accordion; provenance links to stepId/runId |
| MEM-01 | System can store retrieval records derived from artifacts with provenance metadata including source artifact, step, and timestamp | IMemoryGraph + memory_nodes table with source_artifact_id, source_step_id, created_at columns |
| MEM-02 | Any memory injected into a run is inspectable and links back to its source artifact or step | Memory injection recorded as explicit StepRecord with provenance fields; clickable in run inspector |
| MEM-03 | Retrieved memory can be used to ground downstream run decisions without relying on hidden session-only prompt state | MemoryModule output port publishes retrieval results as explicit wiring events; memory tool calls return structured results stored as step records |
</phase_requirements>

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dapper | 2.1.72 | SQLite ORM for artifact/memory tables | Already in project; all run/step persistence uses it |
| Microsoft.Data.Sqlite | 8.0.12 | SQLite driver | Already in project; WAL mode established |
| Markdig | 0.41.3 | Markdown-to-HTML for artifact preview | Already in project (used by chat rendering) |
| xunit | 2.9.3 | Unit tests for repository and graph logic | Already in test project |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.IO | built-in | Artifact file read/write | Content storage at data/artifacts/{runId}/ |
| System.Text.Json | built-in | JSON artifact preview serialization | JSON tree rendering in ArtifactViewer |
| Microsoft.Extensions.Logging | built-in | Structured logging for artifact/memory ops | All service operations |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-rolled Aho-Corasick | NuGet AhoCorasick library | Keyword set is small (agent glossary); hand-rolled trie is ~80 lines, no dependency needed |
| Filesystem content storage | SQLite BLOB storage | Filesystem is simpler for large content, avoids SQLite page bloat, matches CONTEXT.md decision |
| Regex disclosure matching | LLM-based matching | Regex is deterministic and fast; LLM matching deferred — start with substring/regex, upgrade later |

**Installation:** No new packages required. All dependencies already present.

---

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Artifacts/
│   ├── IArtifactStore.cs          # interface
│   ├── ArtifactRecord.cs          # immutable record
│   ├── ArtifactStore.cs           # implementation
│   └── ArtifactFileWriter.cs      # filesystem content write/read
├── Memory/
│   ├── IMemoryGraph.cs            # interface
│   ├── MemoryNode.cs              # immutable record
│   ├── MemoryEdge.cs              # immutable record
│   ├── MemorySnapshot.cs          # immutable record
│   ├── MemoryGraph.cs             # implementation
│   ├── GlossaryIndex.cs           # Aho-Corasick trie
│   └── DisclosureMatcher.cs       # trigger condition matching
├── Modules/
│   └── MemoryModule.cs            # IModuleExecutor with query/write ports
├── RunPersistence/
│   ├── RunDbInitializer.cs        # extend SchemaScript with new tables
│   └── RunRepository.cs           # extend with artifact/memory query methods
└── Components/
    ├── Shared/
    │   ├── ArtifactViewer.razor
    │   ├── ArtifactViewer.razor.css
    │   ├── MemoryNodeCard.razor
    │   └── MemoryNodeCard.razor.css
    └── Pages/
        ├── MemoryGraph.razor
        └── MemoryGraph.razor.css
```

### Pattern 1: IArtifactStore — write-then-link
**What:** ArtifactStore writes content to disk first, then inserts metadata row, then returns artifactId. StepRecorder calls it and sets ArtifactRefId on the completion StepRecord.
**When to use:** Any module that produces durable output (LLM response, tool result, report).
**Example:**
```csharp
// Follows RunRepository per-operation connection pattern
public async Task<string> WriteArtifactAsync(
    string runId, string stepId, string mimeType, string content,
    CancellationToken ct = default)
{
    var artifactId = Guid.NewGuid().ToString("N")[..12];
    var ext = MimeToExtension(mimeType);
    var relativePath = Path.Combine(runId, $"{artifactId}{ext}");
    var fullPath = Path.Combine(_artifactsRoot, relativePath);

    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    await File.WriteAllTextAsync(fullPath, content, ct);

    var record = new ArtifactRecord
    {
        ArtifactId = artifactId,
        RunId = runId,
        StepId = stepId,
        MimeType = mimeType,
        FilePath = relativePath,
        FileSizeBytes = Encoding.UTF8.GetByteCount(content),
        CreatedAt = DateTimeOffset.UtcNow.ToString("O")
    };

    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);
    await conn.ExecuteAsync(InsertArtifact, record);

    return artifactId;
}
```

### Pattern 2: IMemoryGraph — URI-keyed node CRUD with snapshot
**What:** Every write snapshots the previous state before overwriting. Nodes are keyed by URI string. Edges are typed relationships between node URIs.
**When to use:** Agent CRUD via MemoryModule ports or tool calls; system auto-derivation from artifacts.
**Example:**
```csharp
public record MemoryNode
{
    public string Uri { get; init; } = string.Empty;          // e.g. "core://agent/identity"
    public string AnimaId { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string? DisclosureTrigger { get; init; }           // human-readable condition
    public string? Keywords { get; init; }                    // JSON array of strings
    public string? SourceArtifactId { get; init; }
    public string? SourceStepId { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}
```

### Pattern 3: GlossaryIndex — Aho-Corasick trie
**What:** On each memory write, rebuild the trie from all keyword→URI mappings for the Anima. On content scan, return all matched keyword→URI pairs.
**When to use:** Auto-hyperlinking in ArtifactViewer and MemoryNodeCard content display.
**Example:**
```csharp
// Simple trie node — no NuGet dependency needed
private class TrieNode
{
    public Dictionary<char, TrieNode> Children { get; } = new();
    public TrieNode? Failure { get; set; }
    public string? MatchedUri { get; set; }  // non-null = terminal node
}
```

### Pattern 4: System Boot injection
**What:** On `RunService.StartRunAsync`, query memory nodes with `uri LIKE 'core://%'` for the Anima, inject each as a synthetic StepRecord with `ModuleName = "BootMemory"` before the first real module fires.
**When to use:** Every run start when the Anima has configured boot URIs.

### Pattern 5: Disclosure scan
**What:** Before each module execution, scan all memory nodes for the Anima where `disclosure_trigger IS NOT NULL`. Match trigger text against current context (substring match for v2.0). Inject matching nodes as StepRecords with `ModuleName = "MemoryModule"`.
**When to use:** Called from WiringEngine routing path, same intercept point as StepRecorder.

### Anti-Patterns to Avoid
- **Storing artifact content in SQLite BLOBs:** Causes page bloat and makes content unreadable outside the app. Use filesystem.
- **Mutable memory node rows:** Follow the snapshot pattern — never UPDATE in place, always snapshot then overwrite. Enables audit/rollback.
- **Orphan artifacts:** Every artifact write must have a valid runId + stepId. Enforce at the IArtifactStore interface level.
- **Loading full artifact content on page load:** ArtifactViewer loads truncated preview (200 lines / 10KB) on mount; full content only on expand click.
- **Rebuilding Aho-Corasick trie on every content scan:** Rebuild only on memory write. Cache per-Anima in a ConcurrentDictionary.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Markdown rendering | Custom MD parser | Markdig (already in project) | Already used for chat; handles headings, bold, code blocks |
| SQLite connection management | Custom pool | Per-operation SqliteConnection (established pattern) | WAL mode handles concurrency; matches all existing repositories |
| JSON serialization | Custom serializer | System.Text.Json | Already used throughout; camelCase options established |
| File path sanitization | Custom path builder | Path.Combine + Path.GetFullPath | Prevents path traversal; use GetFullPath to verify stays under artifactsRoot |

**Key insight:** The artifact store is ~150 lines following RunRepository exactly. The memory graph is the novel part — but even there, the SQLite/Dapper/record pattern is identical. The only genuinely new algorithm is the Aho-Corasick trie, which is ~80 lines.

---

## Common Pitfalls

### Pitfall 1: ArtifactRefId written with wrong stepId
**What goes wrong:** StepRecorder creates a new StepId for the completion record (line 117 in StepRecorder.cs) — different from the start stepId. If ArtifactStore is called with the start stepId, the link is wrong.
**Why it happens:** StepRecorder currently creates a fresh StepId on completion (see line 117: `StepId = Guid.NewGuid().ToString("N")[..8]`). The start and complete records are separate rows.
**How to avoid:** Pass the completion StepId (the one written to the DB) to ArtifactStore, not the start stepId. Or refactor to use a single stepId for both start and complete events.
**Warning signs:** ArtifactRefId in step_events points to a stepId that doesn't exist in the DB.

### Pitfall 2: Artifact files orphaned after run delete
**What goes wrong:** Run is deleted from DB but `data/artifacts/{runId}/` directory remains on disk.
**Why it happens:** No cascade delete for filesystem content.
**How to avoid:** IArtifactStore exposes `DeleteArtifactsByRunAsync(runId)` — called from RunService.CancelRunAsync or a cleanup path. Document this dependency explicitly.
**Warning signs:** Disk usage grows unbounded across test runs.

### Pitfall 3: Memory snapshot table grows unbounded
**What goes wrong:** Every write creates a snapshot row. High-frequency agent writes fill the table quickly.
**Why it happens:** No retention policy defined yet (Claude's Discretion).
**How to avoid:** Keep last N snapshots per URI (recommend N=10 for v2.0). Prune on write: `DELETE FROM memory_snapshots WHERE uri = @Uri AND id NOT IN (SELECT id FROM memory_snapshots WHERE uri = @Uri ORDER BY id DESC LIMIT 10)`.
**Warning signs:** memory_snapshots table row count >> memory_nodes row count by orders of magnitude.

### Pitfall 4: Disclosure scan blocks WiringEngine routing
**What goes wrong:** Disclosure scan runs synchronously in the hot routing path, adding latency to every module execution.
**Why it happens:** Scanning all memory nodes for an Anima on every step is O(N nodes).
**How to avoid:** Cache disclosure-eligible nodes in memory (ConcurrentDictionary per animaId). Invalidate cache on memory write. Scan is then in-memory, not a DB query.
**Warning signs:** Step duration increases proportionally with memory node count.

### Pitfall 5: In-memory SQLite test isolation
**What goes wrong:** ArtifactStore and MemoryGraph tests share the same in-memory DB and interfere with each other.
**Why it happens:** Shared-cache in-memory SQLite reuses the same DB across connections with the same name.
**How to avoid:** Use unique DB names per test class (e.g., `"Data Source=ArtifactStoreTests;Mode=Memory;Cache=Shared"`). Follow the RunRepositoryTests pattern exactly — keepalive connection + `isRaw: true` factory.
**Warning signs:** Tests pass in isolation but fail when run together.

---

## Code Examples

### SQLite schema additions (extend RunDbInitializer.SchemaScript)
```sql
-- Source: established pattern from RunDbInitializer.cs
CREATE TABLE IF NOT EXISTS artifacts (
    artifact_id     TEXT NOT NULL PRIMARY KEY,
    run_id          TEXT NOT NULL,
    step_id         TEXT NOT NULL,
    mime_type       TEXT NOT NULL,
    file_path       TEXT NOT NULL,
    file_size_bytes INTEGER NOT NULL,
    created_at      TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS memory_nodes (
    uri                 TEXT NOT NULL,
    anima_id            TEXT NOT NULL,
    content             TEXT NOT NULL,
    disclosure_trigger  TEXT,
    keywords            TEXT,          -- JSON array
    source_artifact_id  TEXT,
    source_step_id      TEXT,
    created_at          TEXT NOT NULL,
    updated_at          TEXT NOT NULL,
    PRIMARY KEY (uri, anima_id)
);

CREATE TABLE IF NOT EXISTS memory_edges (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    anima_id    TEXT NOT NULL,
    from_uri    TEXT NOT NULL,
    to_uri      TEXT NOT NULL,
    label       TEXT NOT NULL,
    created_at  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS memory_snapshots (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    uri         TEXT NOT NULL,
    anima_id    TEXT NOT NULL,
    content     TEXT NOT NULL,
    snapshot_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_artifacts_run_id ON artifacts(run_id);
CREATE INDEX IF NOT EXISTS idx_artifacts_step_id ON artifacts(step_id);
CREATE INDEX IF NOT EXISTS idx_memory_nodes_anima ON memory_nodes(anima_id);
CREATE INDEX IF NOT EXISTS idx_memory_edges_anima ON memory_edges(anima_id, from_uri);
CREATE INDEX IF NOT EXISTS idx_memory_snapshots_uri ON memory_snapshots(uri, anima_id, id DESC);
```

### ArtifactRecord (immutable record pattern)
```csharp
// Follows StepRecord pattern from src/OpenAnima.Core/Runs/StepRecord.cs
public record ArtifactRecord
{
    public string ArtifactId { get; init; } = string.Empty;
    public string RunId { get; init; } = string.Empty;
    public string StepId { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}
```

### MemoryModule port declaration
```csharp
// Follows WorkspaceToolModule pattern from src/OpenAnima.Core/Modules/WorkspaceToolModule.cs
[InputPort("query", PortType.Text)]
[InputPort("write", PortType.Text)]
[OutputPort("result", PortType.Text)]
public class MemoryModule : IModuleExecutor { ... }
```

### ArtifactViewer MIME dispatch (Blazor)
```razor
@* Follows existing Blazor scoped CSS pattern — no external component library *@
@if (Artifact != null)
{
    <section class="artifact-section" role="region" aria-label="Artifact: @FileName">
        <div class="artifact-header">
            <span class="mime-badge">@Artifact.MimeType</span>
            <span class="artifact-filename">@FileName</span>
            <span class="artifact-size">@FormatSize(Artifact.FileSizeBytes)</span>
        </div>
        <div class="artifact-content">
            @if (Artifact.MimeType == "text/markdown")
            {
                <div class="markdown-body">@((MarkupString)_renderedHtml)</div>
            }
            else if (Artifact.MimeType == "application/json")
            {
                <JsonTree Data="@_jsonContent" />
            }
            else
            {
                <pre class="plain-text">@_truncatedContent</pre>
            }
        </div>
        @if (_isTruncated)
        {
            <button class="expand-btn" @onclick="LoadFullContent"
                    aria-label="View full content of @FileName">
                查看完整内容
            </button>
        }
    </section>
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Session-only prompt state for memory | Explicit provenance-backed step records | Phase 48 | Memory is inspectable, auditable, and survives session restart |
| ArtifactRefId always null | ArtifactRefId populated on step completion | Phase 48 | OBS-02 (per-step linked artifacts) becomes satisfiable |
| No memory graph | URI-routed node/edge graph with disclosure triggers | Phase 48 | Agent has durable identity and grounded context across runs |

**Deprecated/outdated:**
- `ArtifactRefId = null` in StepRecorder: Phase 48 populates this field — the null default was a Phase 45 placeholder.

---

## Open Questions

1. **StepId consistency between start and complete records**
   - What we know: StepRecorder currently generates a new StepId for the completion record (line 117). The start record has a different StepId.
   - What's unclear: Should ArtifactRefId on the completion record point to the completion stepId or the start stepId? Which is more useful for UI tracing?
   - Recommendation: Use the completion stepId (the one written with ArtifactRefId set). The completion record is the canonical "this step produced this artifact" record. Document this in the planner.

2. **Disclosure trigger matching algorithm**
   - What we know: CONTEXT.md leaves this as Claude's Discretion. Options are substring, regex, or LLM-based.
   - What's unclear: LLM-based matching is more powerful but adds latency and cost.
   - Recommendation: Start with case-insensitive substring match for v2.0. The trigger condition is human-readable text — substring is sufficient for the initial use case and can be upgraded to regex or LLM in a later phase.

3. **MemoryModule vs WorkspaceToolModule for memory tool calls**
   - What we know: CONTEXT.md says memory CRUD is exposed both as module ports and as workspace tools.
   - What's unclear: Should memory tools be registered as `IWorkspaceTool` implementations (picked up by WorkspaceToolModule) or as a separate tool surface inside MemoryModule?
   - Recommendation: Register as `IWorkspaceTool` implementations. WorkspaceToolModule already handles dispatch, concurrency, and step recording. Avoids duplicating that infrastructure.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | none — discovered by convention |
| Quick run command | `dotnet test --filter "ArtifactStore\|MemoryGraph\|MemoryModule" --no-build` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/ --no-build` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| ART-01 | WriteArtifactAsync persists metadata row + content file | unit | `dotnet test --filter ArtifactStoreTests --no-build` | ❌ Wave 0 |
| ART-01 | ArtifactRefId populated on StepRecorder.RecordStepCompleteAsync | unit | `dotnet test --filter StepRecorderArtifactTests --no-build` | ❌ Wave 0 |
| ART-02 | GetArtifactsByRunIdAsync returns artifacts with correct stepId link | unit | `dotnet test --filter ArtifactStoreTests --no-build` | ❌ Wave 0 |
| MEM-01 | WriteNodeAsync persists node with provenance fields | unit | `dotnet test --filter MemoryGraphTests --no-build` | ❌ Wave 0 |
| MEM-01 | WriteNodeAsync creates snapshot before overwrite | unit | `dotnet test --filter MemoryGraphTests --no-build` | ❌ Wave 0 |
| MEM-02 | Memory injection recorded as StepRecord with provenance | unit | `dotnet test --filter MemoryModuleTests --no-build` | ❌ Wave 0 |
| MEM-03 | QueryAsync returns nodes matching URI prefix | unit | `dotnet test --filter MemoryGraphTests --no-build` | ❌ Wave 0 |
| MEM-03 | Disclosure scan returns nodes whose trigger matches context | unit | `dotnet test --filter DisclosureMatcherTests --no-build` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "ArtifactStore\|MemoryGraph\|MemoryModule\|DisclosureMatcher" --no-build`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/ --no-build`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/ArtifactStoreTests.cs` — covers ART-01, ART-02
- [ ] `tests/OpenAnima.Tests/Unit/MemoryGraphTests.cs` — covers MEM-01, MEM-03
- [ ] `tests/OpenAnima.Tests/Unit/DisclosureMatcherTests.cs` — covers MEM-03 disclosure scan
- [ ] `tests/OpenAnima.Tests/Unit/MemoryModuleTests.cs` — covers MEM-02

All test files follow the `RunRepositoryTests` in-memory SQLite pattern: unique DB name per class, keepalive connection, `isRaw: true` factory, `EnsureCreatedAsync()` in constructor.

---

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection — `StepRecorder.cs`, `RunRepository.cs`, `RunDbInitializer.cs`, `IRunRepository.cs`, `IStepRecorder.cs`, `WorkspaceToolModule.cs`, `RunRepositoryTests.cs`, `RunServiceExtensions.cs`
- `.planning/phases/48-artifact-memory-foundation/48-CONTEXT.md` — locked decisions and integration points
- `.planning/phases/48-artifact-memory-foundation/48-UI-SPEC.md` — approved UI contract
- `.planning/REQUIREMENTS.md` — ART-01, ART-02, MEM-01, MEM-02, MEM-03 acceptance criteria

### Secondary (MEDIUM confidence)
- `.planning/STATE.md` — accumulated decisions from Phase 45-47 establishing patterns
- Nocturne Memory reference design (URI routing, disclosure triggers, glossary auto-hyperlinking, System Boot, snapshot versioning) — cited in CONTEXT.md canonical refs

### Tertiary (LOW confidence)
- None — all findings grounded in direct codebase inspection and locked CONTEXT.md decisions.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages already in project, versions verified from .csproj
- Architecture: HIGH — patterns derived directly from existing Phase 45-47 code
- Pitfalls: HIGH — identified from direct code reading (StepRecorder line 117 issue is concrete)
- UI patterns: HIGH — UI-SPEC is approved and fully prescriptive

**Research date:** 2026-03-21
**Valid until:** 2026-04-21 (stable stack, no fast-moving dependencies)
