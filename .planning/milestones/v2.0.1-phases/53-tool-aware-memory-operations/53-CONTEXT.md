# Phase 53: Tool-Aware Memory Operations - Context

**Gathered:** 2026-03-22
**Status:** Ready for planning

<domain>
## Phase Boundary

LLM execution stays aware of its real tool surface and can manipulate memory relationships through explicit, provenance-safe tools. GetToolDescriptors injection wires into the LLM call path so agents know what tools are available. New memory_link and memory_recall tools extend the existing IWorkspaceTool surface. Tool descriptors are filtered by execution context so the model only sees tools it can actually call. Provider registry, editor dropdowns, automatic memory recall, and auto-sedimentation are separate phases (50–52, 54).

</domain>

<decisions>
## Implementation Decisions

### Tool Descriptor Injection Format
- XML tag format in system message: `<available-tools>` wrapping tool descriptors, consistent with Phase 52's `<system-memory>` convention
- Merged into the same messages[0] system message as Phase 52's memory injection — single unified platform context message
- Each tool descriptor includes: name + one-sentence description + required parameters list (concise summary, not full schema)
- When no tools are available (no active run, WorkspaceToolModule not enabled), the `<available-tools>` section is silently omitted — no empty tag injected

### memory_recall Tool
- Purpose: semantic retrieval of relevant memories by natural language keywords — distinct from memory_query which does URI prefix exact matching
- Reuses existing GlossaryIndex.FindGlossaryMatches and DisclosureMatcher for matching, returning matched nodes with content
- Agent-initiated (explicit tool call) vs Phase 52's automatic recall (implicit during every LLM call)
- Follows existing IWorkspaceTool pattern (MemoryQueryTool as reference)

### memory_link Tool
- Purpose: create typed directed edges between existing memory nodes
- Relationship type is free-text string (e.g., 'depends_on', 'related_to', 'implements') — agent defines the relationship
- Strict validation: both source and target nodes must already exist, otherwise return error (no auto-creation of placeholder nodes)
- Reuses existing IMemoryGraph.AddEdgeAsync for the actual edge creation
- Follows existing IWorkspaceTool pattern (MemoryWriteTool as reference)

### Context-Aware Tool Filtering
- Filter at module level: only inject tool descriptors when WorkspaceToolModule is enabled for the current Anima
- If Anima has no WorkspaceToolModule enabled, no tools are injected
- All IWorkspaceTool implementations treated uniformly — no separate filtering for memory vs workspace tools
- Injection point: LLMModule.ExecuteWithMessagesListAsync, querying WorkspaceToolModule.GetToolDescriptors() before building message list

### Provenance & Audit
- Every tool call (including memory_recall reads and memory_link writes) recorded as StepRecord in the run timeline via existing WorkspaceToolModule dispatch
- memory_link: MemoryEdge.SourceStepId and SourceArtifactId fields populated from current run context
- Complete run/step provenance: each edge traces back to the RunId, StepId, and timestamp of the tool invocation
- No additional audit mechanism beyond existing StepRecord — WorkspaceToolModule already handles this for all dispatched tools

### Tool Responsibility Separation
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

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` — TOOL-01 through TOOL-04 define all tool awareness and memory tool requirements

### Existing Tool Infrastructure
- `src/OpenAnima.Core/Tools/IWorkspaceTool.cs` — Tool contract: Descriptor property + ExecuteAsync method
- `src/OpenAnima.Core/Tools/ToolDescriptor.cs` — Record: Name, Description, Parameters list
- `src/OpenAnima.Core/Tools/ToolParameterSchema.cs` — Record: Name, Type, Description, Required
- `src/OpenAnima.Core/Modules/WorkspaceToolModule.cs` — Tool dispatcher with GetToolDescriptors() (line 178, never consumed by LLM), StepRecord per invocation, DI-injected IEnumerable<IWorkspaceTool>

### Existing Memory Tools (reference patterns)
- `src/OpenAnima.Core/Tools/MemoryQueryTool.cs` — URI prefix query tool (reference for memory_recall design)
- `src/OpenAnima.Core/Tools/MemoryWriteTool.cs` — Node create/update tool (reference for memory_link design)
- `src/OpenAnima.Core/Tools/MemoryDeleteTool.cs` — Node delete tool

### Memory Graph
- `src/OpenAnima.Core/Memory/IMemoryGraph.cs` — AddEdgeAsync for edge creation, FindGlossaryMatches for keyword matching, GetDisclosureNodesAsync for disclosure triggers
- `src/OpenAnima.Core/Memory/MemoryNode.cs` — Node record with provenance fields (SourceArtifactId, SourceStepId)

### LLM Module (integration point)
- `src/OpenAnima.Core/Modules/LLMModule.cs` — ExecuteWithMessagesListAsync is where tool descriptors should be injected alongside memory recall

### Phase 52 Context (memory injection pattern)
- `.planning/phases/52-automatic-memory-recall/52-CONTEXT.md` — XML tag system message format, messages[0] injection, MemoryRecallService architecture

### DI Registration
- `src/OpenAnima.Core/DependencyInjection/ToolServiceExtensions.cs` — Tool DI registration patterns
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` — Service registration patterns

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WorkspaceToolModule.GetToolDescriptors()`: Already implemented, returns all registered tool descriptors — just needs to be consumed by LLMModule
- `IMemoryGraph.AddEdgeAsync()`: Edge creation already implemented — memory_link tool wraps this
- `IMemoryGraph.FindGlossaryMatches()`: Aho-Corasick glossary matching already implemented — memory_recall tool wraps this
- `DisclosureMatcher.Match()`: Static disclosure matching — memory_recall can combine with glossary matches
- `MemoryQueryTool` / `MemoryWriteTool` / `MemoryDeleteTool`: Three existing memory tools establish the exact IWorkspaceTool pattern for new tools
- `StepRecorder`: WorkspaceToolModule already records every tool call as StepRecord — new tools get this automatically

### Established Patterns
- IWorkspaceTool pattern: Descriptor property + ExecuteAsync(workspaceRoot, parameters, ct) — all 15 existing tools follow this
- Tool DI registration: IWorkspaceTool implementations registered as transient in ToolServiceExtensions
- XML system message injection: Phase 52 establishes `<system-memory>` in messages[0] — tool descriptors use `<available-tools>` in same message
- ToolResult.Ok / ToolResult.Failed: Standard result envelope for all tool executions
- ToolResultMetadata: WorkspaceRoot, ToolName, DurationMs, Timestamp

### Integration Points
- `LLMModule.ExecuteWithMessagesListAsync()`: Inject tool descriptors alongside memory recall in the system message at messages[0]
- `WorkspaceToolModule` constructor: Already accepts `IEnumerable<IWorkspaceTool>` via DI — new tools auto-register
- `ToolServiceExtensions`: Add new tool registrations (MemoryLinkTool, MemoryRecallTool)

</code_context>

<specifics>
## Specific Ideas

- Tool descriptor XML format example:
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
- Combined system message structure: `<system-memory>` + `<available-tools>` in one messages[0] message
- memory_link validation example: if from_uri "core://agent/identity" exists but to_uri "project://unknown" does not → return ToolResult.Failed with "Target node not found: project://unknown"

</specifics>

<deferred>
## Deferred Ideas

- Per-tool enable/disable configuration (fine-grained tool filtering per Anima) — future phase if needed
- OpenAI-native function calling / tools protocol support — future enhancement, current approach uses XML prompt injection
- Tool permission levels (read-only vs write vs dangerous) — future safety enhancement

</deferred>

---

*Phase: 53-tool-aware-memory-operations*
*Context gathered: 2026-03-22*
