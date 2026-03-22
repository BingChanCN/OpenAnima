# Phase 53: Tool-Aware Memory Operations - Research

**Researched:** 2026-03-22
**Domain:** .NET / Blazor Server — IWorkspaceTool pattern, LLMModule message injection, IMemoryGraph
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Tool Descriptor Injection Format**
- XML tag format in system message: `<available-tools>` wrapping tool descriptors, consistent with Phase 52's `<system-memory>` convention
- Merged into the same messages[0] system message as Phase 52's memory injection — single unified platform context message
- Each tool descriptor includes: name + one-sentence description + required parameters list (concise summary, not full schema)
- When no tools are available (no active run, WorkspaceToolModule not enabled), the `<available-tools>` section is silently omitted — no empty tag injected

**memory_recall Tool**
- Purpose: semantic retrieval of relevant memories by natural language keywords — distinct from memory_query which does URI prefix exact matching
- Reuses existing GlossaryIndex.FindGlossaryMatches and DisclosureMatcher for matching, returning matched nodes with content
- Agent-initiated (explicit tool call) vs Phase 52's automatic recall (implicit during every LLM call)
- Follows existing IWorkspaceTool pattern (MemoryQueryTool as reference)

**memory_link Tool**
- Purpose: create typed directed edges between existing memory nodes
- Relationship type is free-text string (e.g., 'depends_on', 'related_to', 'implements') — agent defines the relationship
- Strict validation: both source and target nodes must already exist, otherwise return error (no auto-creation of placeholder nodes)
- Reuses existing IMemoryGraph.AddEdgeAsync for the actual edge creation
- Follows existing IWorkspaceTool pattern (MemoryWriteTool as reference)

**Context-Aware Tool Filtering**
- Filter at module level: only inject tool descriptors when WorkspaceToolModule is enabled for the current Anima
- If Anima has no WorkspaceToolModule enabled, no tools are injected
- All IWorkspaceTool implementations treated uniformly — no separate filtering for memory vs workspace tools
- Injection point: LLMModule.ExecuteWithMessagesListAsync, querying WorkspaceToolModule.GetToolDescriptors() before building message list

**Provenance & Audit**
- Every tool call (including memory_recall reads and memory_link writes) recorded as StepRecord in the run timeline via existing WorkspaceToolModule dispatch
- memory_link: MemoryEdge.SourceStepId and SourceArtifactId fields populated from current run context
- Complete run/step provenance: each edge traces back to the RunId, StepId, and timestamp of the tool invocation
- No additional audit mechanism beyond existing StepRecord — WorkspaceToolModule already handles this for all dispatched tools

**Tool Responsibility Separation**
- memory_query: precise node lookup by URI prefix (existing, unchanged)
- memory_recall: fuzzy keyword/glossary-based retrieval of relevant nodes (new)
- memory_link: create typed graph edges between nodes (new)
- memory_write: create/update node content (existing, unchanged)
- memory_delete: delete node and its edges (existing, unchanged)

### Claude's Discretion
- Exact XML tag naming and attribute format for `<available-tools>` section
- Token budget allocation between tool descriptors and memory recall content within the shared system message
- Internal implementation of tool descriptor collection from WorkspaceToolModule
- MemoryRecallTool parameter design details (query string format, result limit)
- MemoryLinkTool parameter naming and validation error messages

### Deferred Ideas (OUT OF SCOPE)
- Per-tool enable/disable configuration (fine-grained tool filtering per Anima) — future phase if needed
- OpenAI-native function calling / tools protocol support — future enhancement, current approach uses XML prompt injection
- Tool permission levels (read-only vs write vs dangerous) — future safety enhancement
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| TOOL-01 | LLM receives descriptors for only the tools available in the current execution context | WorkspaceToolModule.GetToolDescriptors() already implemented (line 178); inject in LLMModule.ExecuteWithMessagesListAsync with WorkspaceToolModule availability check |
| TOOL-02 | Developer-agent can create typed memory graph edges through a `memory_link` tool | IMemoryGraph.AddEdgeAsync already implemented; MemoryLinkTool wraps it with node-existence validation |
| TOOL-03 | Developer-agent can explicitly retrieve relevant memories through a `memory_recall` tool | IMemoryGraph.FindGlossaryMatches and DisclosureMatcher.Match already implemented; MemoryRecallTool wraps them |
| TOOL-04 | Developer-agent can manage memory graph relationships without bypassing existing node provenance rules | WorkspaceToolModule already records every dispatched tool call as a StepRecord automatically; new tools inherit this for free |
</phase_requirements>

---

## Summary

Phase 53 wires together three pieces of already-implemented infrastructure that were never connected. `WorkspaceToolModule.GetToolDescriptors()` exists but is never read by `LLMModule`. `IMemoryGraph.FindGlossaryMatches` and `DisclosureMatcher.Match` exist but are not surfaced as agent-callable tools. `IMemoryGraph.AddEdgeAsync` exists but has no tool wrapping it with proper validation.

The implementation is additive and low-risk. Two new `IWorkspaceTool` classes (`MemoryRecallTool`, `MemoryLinkTool`) follow the exact same pattern as the three existing memory tools (`MemoryQueryTool`, `MemoryWriteTool`, `MemoryDeleteTool`). One change is made to `LLMModule.ExecuteWithMessagesListAsync` to inject tool descriptors into the system message when a `WorkspaceToolModule` is present. Both new tools are registered in `RunServiceExtensions` alongside the existing memory tool registrations.

One critical discrepancy was found: the CONTEXT.md refers to populating `MemoryEdge.SourceStepId` and `SourceArtifactId` for provenance, but the actual `MemoryEdge` record (`MemoryEdge.cs`) does not contain these fields — it has only `Id`, `AnimaId`, `FromUri`, `ToUri`, `Label`, and `CreatedAt`. The `memory_edges` table INSERT in `MemoryGraph.AddEdgeAsync` also only persists those six fields. Provenance of edge creation is therefore achieved entirely through the `StepRecord` that `WorkspaceToolModule` automatically creates when dispatching any tool call — the edge itself does not carry a step reference.

**Primary recommendation:** Build two thin tool wrappers following the MemoryWriteTool/MemoryQueryTool pattern, add one optional `WorkspaceToolModule` dependency to `LLMModule`, and inject descriptors as an `<available-tools>` XML block in the system message. All infrastructure is already in place.

---

## Standard Stack

### Core (all already in the project)

| Library/Type | Version | Purpose | Why Standard |
|---|---|---|---|
| `IWorkspaceTool` | project contract | Tool interface with Descriptor + ExecuteAsync | All 15 existing tools follow this pattern |
| `ToolDescriptor` / `ToolParameterSchema` | project records | Self-describing tool metadata | Already used for all tools; GetToolDescriptors() already serializes these |
| `IMemoryGraph` | project contract | Memory persistence and retrieval | FindGlossaryMatches, AddEdgeAsync, GetNodeAsync all needed by new tools |
| `DisclosureMatcher` | project class | Static case-insensitive trigger matching | Used by memory_recall alongside glossary matches |
| `WorkspaceToolModule` | project module | Tool dispatcher; records StepRecord per invocation | Provides automatic provenance for all tool calls |
| `LLMModule` | project module | LLM call handler; integration point for descriptor injection | ExecuteWithMessagesListAsync is the injection site |
| `RunServiceExtensions` | project DI | Registers memory tools as IWorkspaceTool singletons | Both new tools registered here alongside existing memory tools |
| xunit 2.9.3 | test framework | Unit and integration tests | Existing project standard |
| Microsoft.Data.Sqlite | in-memory DB for tests | SQLite in-memory DB for tool unit tests | Used by MemoryModuleTests — same pattern for new tool tests |

### No New Dependencies

This phase introduces zero new NuGet packages. All infrastructure is already present.

---

## Architecture Patterns

### IWorkspaceTool Implementation Pattern

All 15 existing tools follow an identical structure. New tools must match exactly:

```csharp
// Source: src/OpenAnima.Core/Tools/MemoryWriteTool.cs (reference pattern)
public class MemoryRecallTool : IWorkspaceTool
{
    private readonly IMemoryGraph _memoryGraph;

    public MemoryRecallTool(IMemoryGraph memoryGraph)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
    }

    public ToolDescriptor Descriptor { get; } = new(
        "memory_recall",
        "Retrieve relevant memory nodes by keyword or phrase search",
        new ToolParameterSchema[]
        {
            new("query", "string", "Natural language query or keywords to search for", Required: true),
            new("anima_id", "string", "Anima ID to recall memories for", Required: true)
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        // validate required params -> MakeMeta
        // call IMemoryGraph.FindGlossaryMatches + DisclosureMatcher.Match
        // return ToolResult.Ok or ToolResult.Failed
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, Stopwatch sw) =>
        new() { WorkspaceRoot = workspaceRoot, ToolName = "memory_recall",
                DurationMs = (int)sw.ElapsedMilliseconds,
                Timestamp = DateTimeOffset.UtcNow.ToString("o") };
}
```

### DI Registration Pattern

New memory tools register in `RunServiceExtensions.AddRunServices` as singletons:

```csharp
// Source: src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs (lines 54-56)
// Existing pattern:
services.AddSingleton<IWorkspaceTool, MemoryQueryTool>();
services.AddSingleton<IWorkspaceTool, MemoryWriteTool>();
services.AddSingleton<IWorkspaceTool, MemoryDeleteTool>();

// New additions (same location):
services.AddSingleton<IWorkspaceTool, MemoryRecallTool>();
services.AddSingleton<IWorkspaceTool, MemoryLinkTool>();
```

WorkspaceToolModule receives `IEnumerable<IWorkspaceTool>` via DI constructor — new tools auto-appear in the tool registry and in `GetToolDescriptors()` with no other changes to WorkspaceToolModule.

### LLMModule Descriptor Injection Pattern

`LLMModule.ExecuteWithMessagesListAsync` already injects the routing system message at `messages[0]`. Tool descriptor injection follows the same pattern but is guarded by a nullable `WorkspaceToolModule` dependency:

```csharp
// Source: src/OpenAnima.Core/Modules/LLMModule.cs (ExecuteWithMessagesListAsync)
// Existing injection site — routing system message added at messages[0]:
if (knownServiceNames.Count > 0 && _router != null)
{
    messages.Insert(0, new ChatMessageInput("system", BuildSystemMessage(ports)));
}

// New pattern: tool descriptor injection prepended to same messages[0] system message
// WorkspaceToolModule injected as optional (nullable) constructor parameter
// Guard: only inject if WorkspaceToolModule is non-null and has tools
```

The system message content for tools follows the Phase 52 `<system-memory>` XML convention:

```xml
<available-tools>
  <tool name="file_read" description="Read file contents from workspace">
    <param name="path" required="true"/>
  </tool>
  <tool name="memory_recall" description="Retrieve relevant memories by keyword search">
    <param name="query" required="true"/>
    <param name="anima_id" required="true"/>
  </tool>
  <tool name="memory_link" description="Create a typed relationship between two memory nodes">
    <param name="from_uri" required="true"/>
    <param name="to_uri" required="true"/>
    <param name="relationship" required="true"/>
    <param name="anima_id" required="true"/>
  </tool>
</available-tools>
```

### Combined System Message Structure

Phase 52 injects `<system-memory>` at `messages[0]`. Phase 53 adds `<available-tools>` into the same system message to keep a single unified platform context block. The combined structure is:

```
messages[0]: system — "<system-memory>...</system-memory>\n\n<available-tools>...</available-tools>"
messages[1..n]: user/assistant conversation
```

When only one section is present (memory but no tools, or tools but no memory), only the present section appears.

### memory_link Validation Pattern

Strict pre-existence check before calling AddEdgeAsync:

```csharp
// Source pattern: MemoryWriteTool.cs validation style
var fromNode = await _memoryGraph.GetNodeAsync(animaId, fromUri, ct);
if (fromNode == null)
    return ToolResult.Failed("memory_link",
        $"Source node not found: {fromUri}. Ensure the source memory node exists before linking.",
        MakeMeta(workspaceRoot, sw));

var toNode = await _memoryGraph.GetNodeAsync(animaId, toUri, ct);
if (toNode == null)
    return ToolResult.Failed("memory_link",
        $"Target node not found: {toUri}. Ensure the target memory node exists before linking.",
        MakeMeta(workspaceRoot, sw));

await _memoryGraph.AddEdgeAsync(new MemoryEdge
{
    AnimaId = animaId,
    FromUri = fromUri,
    ToUri = toUri,
    Label = relationship,
    CreatedAt = DateTimeOffset.UtcNow.ToString("O")
}, ct);
```

### memory_recall Dual-Matcher Pattern

`memory_recall` combines glossary (keyword-based) matching via `IMemoryGraph.FindGlossaryMatches` with disclosure trigger matching via `DisclosureMatcher.Match`. Both are synchronous on the hot path after async node fetch:

```csharp
// Step 1: glossary keyword matching (synchronous — uses Aho-Corasick trie)
var glossaryMatches = _memoryGraph.FindGlossaryMatches(animaId, query);
// Returns IReadOnlyList<(string Keyword, string Uri)>

// Step 2: disclosure trigger matching (synchronous — substring scan)
var disclosureNodes = await _memoryGraph.GetDisclosureNodesAsync(animaId, ct);
var disclosureMatches = DisclosureMatcher.Match(disclosureNodes, query);
// Returns IReadOnlyList<MemoryNode>

// Combine: resolve glossary URIs to nodes, deduplicate with disclosure matches by URI
```

Note: `IMemoryGraph.FindGlossaryMatches` returns `(Keyword, Uri)` pairs, not full nodes. The tool must do a follow-up fetch (`GetNodeAsync` or `QueryByPrefixAsync`) to get content for each matched URI. Alternatively, since the result list is small, iterate and call `GetNodeAsync` per URI.

### Recommended Project Structure Impact

Phase 53 adds exactly these files:

```
src/OpenAnima.Core/Tools/
├── MemoryRecallTool.cs    (new — follows MemoryQueryTool pattern)
├── MemoryLinkTool.cs      (new — follows MemoryWriteTool pattern)
└── [existing tools unchanged]

tests/OpenAnima.Tests/Unit/
└── MemoryToolPhase53Tests.cs  (new — follows MemoryModuleTests pattern)
```

One modification:
- `src/OpenAnima.Core/Modules/LLMModule.cs` — add optional `WorkspaceToolModule?` constructor parameter + inject tool descriptors in `ExecuteWithMessagesListAsync`
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` — add two `AddSingleton<IWorkspaceTool>` registrations

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Keyword matching | Custom string scan | `IMemoryGraph.FindGlossaryMatches` | Aho-Corasick trie already built; handles multi-keyword single-pass matching |
| Disclosure trigger matching | Custom substring logic | `DisclosureMatcher.Match` | Static method handles null-trigger filtering and case-insensitive matching |
| Edge creation | Custom SQL INSERT | `IMemoryGraph.AddEdgeAsync` | Already implemented with WAL-safe connection management |
| Node existence check | Raw SQL | `IMemoryGraph.GetNodeAsync` | Returns null-safe MemoryNode; already handles the (AnimaId, Uri) primary key |
| Tool call provenance | Custom audit table | `WorkspaceToolModule` StepRecord dispatch | Every dispatched tool call automatically gets RecordStepStartAsync/RecordStepCompleteAsync |
| Tool descriptor serialization | Custom format | `WorkspaceToolModule.GetToolDescriptors()` | Already aggregates all IWorkspaceTool descriptors in one call |

**Key insight:** The primary value of this phase is wiring — most of the work is already done. Custom implementations would duplicate existing infrastructure and break the provenance contract that WorkspaceToolModule provides.

---

## Common Pitfalls

### Pitfall 1: MemoryEdge Does Not Have Provenance Fields

**What goes wrong:** CONTEXT.md mentions "MemoryEdge.SourceStepId and SourceArtifactId fields populated from current run context" but the actual `MemoryEdge` record has no such fields. The `memory_edges` table schema (inferred from `AddEdgeAsync` INSERT) only contains: `id`, `anima_id`, `from_uri`, `to_uri`, `label`, `created_at`. Attempting to set `SourceStepId` on `MemoryEdge` will be a compile error.

**Why it happens:** CONTEXT.md was written before code was fully verified. The provenance intent is satisfied differently: WorkspaceToolModule records a `StepRecord` for every tool invocation automatically. The edge is thus traceable through the step timeline, not through a field on the edge itself.

**How to avoid:** Do not attempt to add SourceStepId/SourceArtifactId to MemoryEdge or the database schema. Use the StepRecord mechanism exclusively for provenance. Confirm TOOL-04 requirement is satisfied by WorkspaceToolModule's existing dispatch recording, not by MemoryEdge fields.

**Warning signs:** Compile error on `new MemoryEdge { SourceStepId = ... }`. If this is encountered, confirm the provenance approach with the existing StepRecord pattern.

### Pitfall 2: LLMModule Does Not Currently Know About WorkspaceToolModule

**What goes wrong:** `LLMModule` has no reference to `WorkspaceToolModule` in its constructor. To inject tool descriptors, `WorkspaceToolModule` must be added as an optional constructor dependency (nullable, like `ICrossAnimaRouter?`). If registered as required, it breaks Anima configurations where WorkspaceToolModule is absent.

**Why it happens:** WorkspaceToolModule is an optional module — not all Animas have workspace tools. CONTEXT.md decision: "only inject tool descriptors when WorkspaceToolModule is enabled for the current Anima."

**How to avoid:** Add `WorkspaceToolModule? workspaceToolModule = null` as an optional constructor parameter in `LLMModule`, following the exact pattern of `ICrossAnimaRouter? router = null` on line 57 of LLMModule.cs. Guard all usage behind null check.

### Pitfall 3: FindGlossaryMatches Returns URIs Not Nodes

**What goes wrong:** `IMemoryGraph.FindGlossaryMatches(animaId, content)` returns `IReadOnlyList<(string Keyword, string Uri)>` — not `MemoryNode` objects. If the tool tries to return node content directly from this list, it will fail to compile or return incomplete data.

**Why it happens:** The glossary index stores only keyword-to-URI mappings for efficient multi-keyword matching. Content retrieval is a separate step.

**How to avoid:** After getting glossary URI hits, call `GetNodeAsync` for each URI to fetch the full node (content, provenance fields). Deduplicate with disclosure matches by URI before fetching.

### Pitfall 4: Glossary Trie Must Be Populated Before Use

**What goes wrong:** `FindGlossaryMatches` returns empty results if the Aho-Corasick trie has not been built for the Anima. The trie is populated by `RebuildGlossaryAsync` and cached per-Anima. If no memory nodes with Keywords have been written since last startup, the cache is empty.

**Why it happens:** `MemoryGraph` uses a `ConcurrentDictionary<string, GlossaryIndex>` cache populated lazily by `RebuildGlossaryAsync`. The cache is invalidated on write/delete but not rebuilt until explicitly called.

**How to avoid:** In `MemoryRecallTool.ExecuteAsync`, call `await _memoryGraph.RebuildGlossaryAsync(animaId, ct)` before calling `FindGlossaryMatches`. This follows the pattern established in Phase 52's design decision: "Glossary index rebuilt before every LLM call via `IMemoryGraph.RebuildGlossaryAsync`."

### Pitfall 5: System Message Ordering with Phase 52

**What goes wrong:** Phase 52 also inserts content at `messages[0]`. If both phases insert independently, one will overwrite the other. The combined system message must be assembled before the single `messages.Insert(0, ...)` call.

**Why it happens:** The decisions require `<system-memory>` and `<available-tools>` to share the same `messages[0]` entry.

**How to avoid:** Assemble all system message content (memory from Phase 52's MemoryRecallService + tools from WorkspaceToolModule) into a single string, then insert once. The implementation must coordinate Phase 52 and Phase 53 injection within one `messages[0]` slot.

### Pitfall 6: Empty Available-Tools Must Be Silently Omitted

**What goes wrong:** Injecting an empty `<available-tools/>` tag wastes tokens and may confuse the LLM.

**How to avoid:** Retrieve `GetToolDescriptors()` result first. If the list is empty (no tools registered or WorkspaceToolModule absent), skip the `<available-tools>` block entirely. Never emit `<available-tools/>` with no children.

---

## Code Examples

### Existing Memory Tool — Exact Pattern to Follow

```csharp
// Source: src/OpenAnima.Core/Tools/MemoryQueryTool.cs
public class MemoryQueryTool : IWorkspaceTool
{
    private readonly IMemoryGraph _memoryGraph;

    public MemoryQueryTool(IMemoryGraph memoryGraph)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
    }

    public ToolDescriptor Descriptor { get; } = new(
        "memory_query",
        "Query memory nodes by URI prefix or exact URI for the active Anima",
        new ToolParameterSchema[]
        {
            new("uri_prefix", "string", "URI prefix to search, e.g. 'core://' or exact URI", Required: true),
            new("anima_id", "string", "Anima ID to query memories for", Required: true)
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("uri_prefix", out var uriPrefix) || string.IsNullOrWhiteSpace(uriPrefix))
            return ToolResult.Failed("memory_query", "Missing required parameter: uri_prefix", MakeMeta(workspaceRoot, sw));

        // ... execute, return ToolResult.Ok(...)
    }

    private ToolResultMetadata MakeMeta(string workspaceRoot, Stopwatch sw) =>
        new() { WorkspaceRoot = workspaceRoot, ToolName = "memory_query",
                DurationMs = (int)sw.ElapsedMilliseconds, Timestamp = DateTimeOffset.UtcNow.ToString("o") };
}
```

### WorkspaceToolModule.GetToolDescriptors() — Already Implemented

```csharp
// Source: src/OpenAnima.Core/Modules/WorkspaceToolModule.cs line 178
public IReadOnlyList<ToolDescriptor> GetToolDescriptors() =>
    _tools.Values.Select(t => t.Descriptor).ToList().AsReadOnly();
```

### LLMModule Optional Dependency Pattern — Follow This for WorkspaceToolModule

```csharp
// Source: src/OpenAnima.Core/Modules/LLMModule.cs line 54-67
// Existing optional dependency pattern:
public LLMModule(ILLMService llmService, IEventBus eventBus, ILogger<LLMModule> logger,
    IAnimaModuleConfigService configService, IModuleContext animaContext,
    ILLMProviderRegistry providerRegistry, LLMProviderRegistryService registryService,
    ICrossAnimaRouter? router = null)  // <-- optional, nullable
```

### MemoryGraph.AddEdgeAsync — Actual Signature

```csharp
// Source: src/OpenAnima.Core/Memory/IMemoryGraph.cs line 43
Task AddEdgeAsync(MemoryEdge edge, CancellationToken ct = default);

// Source: src/OpenAnima.Core/Memory/MemoryEdge.cs — ACTUAL fields (no SourceStepId):
public record MemoryEdge
{
    public int Id { get; init; }
    public string AnimaId { get; init; } = string.Empty;
    public string FromUri { get; init; } = string.Empty;
    public string ToUri { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;   // relationship type goes here
    public string CreatedAt { get; init; } = string.Empty;
}
```

### Test Pattern — In-Memory SQLite for Tool Tests

```csharp
// Source: tests/OpenAnima.Tests/Unit/MemoryModuleTests.cs
private const string DbConnectionString = "Data Source=MyTestDb;Mode=Memory;Cache=Shared";
private readonly SqliteConnection _keepAlive;  // must stay open for shared-cache to persist

public TestClass()
{
    _keepAlive = new SqliteConnection(DbConnectionString);
    _keepAlive.Open();
    _factory = new RunDbConnectionFactory(DbConnectionString, isRaw: true);
    _initializer = new RunDbInitializer(_factory);
    _graph = new MemoryGraph(_factory, NullLogger<MemoryGraph>.Instance);
    _initializer.EnsureCreatedAsync().GetAwaiter().GetResult();
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|---|---|---|---|
| LLM has no awareness of available tools | Tool descriptors injected via XML system message | Phase 53 | Model knows what it can call without hallucinating tool names |
| Memory retrieval only via URI prefix (memory_query) | Also via keyword/glossary (memory_recall) | Phase 53 | Agent can find relevant memories without knowing exact URIs |
| Graph edges created only by direct IMemoryGraph calls from code | Agent can create edges via memory_link tool | Phase 53 | Agent-driven relationship building with automatic provenance via StepRecord |
| Tool descriptor XML in LLMModule never consumed | WorkspaceToolModule.GetToolDescriptors() wired to LLMModule | Phase 53 | Completes the tool surface that WorkspaceToolModule already exposes |

**Deprecated/outdated:**
- Nothing is removed in this phase. All changes are additive.

---

## Open Questions

1. **Phase 52 integration — MemoryRecallService availability**
   - What we know: Phase 52 adds `MemoryRecallService` that injects `<system-memory>` at `messages[0]`; Phase 53 adds `<available-tools>` to the same message.
   - What's unclear: Phase 52 is listed as "Pending" (not yet implemented). If Phase 53 is planned before Phase 52 is complete, LLMModule may not yet have MemoryRecallService injected. The planner must decide whether Phase 53 should add tool descriptor injection independently (building messages[0] from scratch if no memory block exists) or assume Phase 52 is complete.
   - Recommendation: Plan Phase 53 assuming Phase 52 runs first (as listed in REQUIREMENTS.md traceability). The combined system message builder in LLMModule should be designed to gracefully handle either or both sections being absent.

2. **MemoryEdge provenance discrepancy — CONTEXT.md vs actual code**
   - What we know: CONTEXT.md says "MemoryEdge.SourceStepId and SourceArtifactId fields populated from current run context" but the MemoryEdge record has neither field. MemoryGraph.AddEdgeAsync inserts only 5 fields.
   - What's unclear: Whether the CONTEXT.md provenance intent requires a schema change to add SourceStepId/SourceArtifactId to MemoryEdge/memory_edges, or whether the StepRecord-based provenance is sufficient for TOOL-04.
   - Recommendation: TOOL-04 requirement ("without bypassing existing node provenance rules") is satisfied by WorkspaceToolModule's automatic StepRecord dispatch. No schema change needed. The planner should note this discrepancy and confirm that MemoryEdge schema is unchanged.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | none — uses xunit defaults |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryToolPhase53" -x` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TOOL-01 | LLMModule injects `<available-tools>` block when WorkspaceToolModule present | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~ToolDescriptorInjection" -x` | Wave 0 |
| TOOL-01 | No `<available-tools>` block when WorkspaceToolModule absent | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~ToolDescriptorInjection" -x` | Wave 0 |
| TOOL-01 | No empty `<available-tools/>` when tool list is empty | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~ToolDescriptorInjection" -x` | Wave 0 |
| TOOL-02 | MemoryLinkTool creates edge when both nodes exist | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryLinkTool" -x` | Wave 0 |
| TOOL-02 | MemoryLinkTool returns Failed when source node absent | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryLinkTool" -x` | Wave 0 |
| TOOL-02 | MemoryLinkTool returns Failed when target node absent | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryLinkTool" -x` | Wave 0 |
| TOOL-03 | MemoryRecallTool returns matched nodes for known keywords | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryRecallTool" -x` | Wave 0 |
| TOOL-03 | MemoryRecallTool returns empty list (not error) for no matches | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryRecallTool" -x` | Wave 0 |
| TOOL-03 | MemoryRecallTool returns disclosure-triggered nodes | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryRecallTool" -x` | Wave 0 |
| TOOL-04 | MemoryLinkTool validates node existence before AddEdgeAsync | unit | Covered by TOOL-02 tests | Wave 0 |
| TOOL-04 | WorkspaceToolModule records StepRecord for memory_recall calls | unit (existing WorkspaceToolModule tests) | `dotnet test tests/OpenAnima.Tests/ -x` | existing |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryToolPhase53" -x`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/MemoryToolPhase53Tests.cs` — covers TOOL-01 descriptor injection, TOOL-02 MemoryLinkTool, TOOL-03 MemoryRecallTool (follows MemoryModuleTests.cs pattern with shared-cache SQLite)

*(No framework install needed — xunit already present. In-memory SQLite pattern fully established in MemoryModuleTests.cs.)*

---

## Sources

### Primary (HIGH confidence)

All findings are based on direct code inspection of the actual project source. No external library research required — this phase builds exclusively on existing project infrastructure.

- `src/OpenAnima.Core/Tools/IWorkspaceTool.cs` — Tool contract
- `src/OpenAnima.Core/Tools/ToolDescriptor.cs` — Descriptor record
- `src/OpenAnima.Core/Tools/ToolParameterSchema.cs` — Parameter schema record
- `src/OpenAnima.Core/Tools/ToolResult.cs` — Result envelope
- `src/OpenAnima.Core/Tools/MemoryQueryTool.cs` — Reference implementation for MemoryRecallTool
- `src/OpenAnima.Core/Tools/MemoryWriteTool.cs` — Reference implementation for MemoryLinkTool
- `src/OpenAnima.Core/Tools/MemoryDeleteTool.cs` — Additional reference
- `src/OpenAnima.Core/Modules/WorkspaceToolModule.cs` — GetToolDescriptors() implementation (line 178)
- `src/OpenAnima.Core/Modules/LLMModule.cs` — ExecuteWithMessagesListAsync integration point
- `src/OpenAnima.Core/Memory/IMemoryGraph.cs` — AddEdgeAsync, FindGlossaryMatches, GetDisclosureNodesAsync
- `src/OpenAnima.Core/Memory/MemoryEdge.cs` — CRITICAL: actual fields confirmed (no SourceStepId)
- `src/OpenAnima.Core/Memory/MemoryGraph.cs` — AddEdgeAsync INSERT confirmed (6 fields only)
- `src/OpenAnima.Core/Memory/MemoryNode.cs` — Node record with provenance fields
- `src/OpenAnima.Core/Memory/DisclosureMatcher.cs` — Static Match method
- `src/OpenAnima.Core/Memory/GlossaryIndex.cs` — Aho-Corasick trie; FindMatches API
- `src/OpenAnima.Core/Memory/BootMemoryInjector.cs` — StepRecord pattern reference
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` — Memory tool registration site
- `src/OpenAnima.Core/DependencyInjection/ToolServiceExtensions.cs` — Non-memory tool registration pattern
- `src/OpenAnima.Contracts/IModuleContext.cs` — ActiveAnimaId property
- `src/OpenAnima.Core/Runs/IRunService.cs` — GetActiveRun contract
- `tests/OpenAnima.Tests/Unit/MemoryModuleTests.cs` — Test pattern with SpyStepRecorder and in-memory SQLite

### Secondary (MEDIUM confidence)

- `.planning/phases/52-automatic-memory-recall/52-CONTEXT.md` — system-memory XML format, messages[0] injection pattern, MemoryRecallService architecture (Phase 52 not yet implemented)
- `.planning/phases/53-tool-aware-memory-operations/53-CONTEXT.md` — locked decisions
- `.planning/phases/53-tool-aware-memory-operations/53-UI-SPEC.md` — step rendering contract, copywriting for tool error messages

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all types directly verified in source code
- Architecture patterns: HIGH — IWorkspaceTool pattern consistent across all 15 existing tools
- Pitfalls: HIGH — discrepancy in MemoryEdge fields verified by direct inspection of MemoryEdge.cs and MemoryGraph.cs AddEdgeAsync INSERT
- Test patterns: HIGH — MemoryModuleTests.cs provides exact template

**Research date:** 2026-03-22
**Valid until:** 2026-04-22 (stable codebase; no external dependencies changing)
