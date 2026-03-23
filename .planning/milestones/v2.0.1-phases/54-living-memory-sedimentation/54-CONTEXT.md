# Phase 54: Living Memory Sedimentation - Context

**Gathered:** 2026-03-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Completed LLM exchanges automatically turn stable learnings into durable memory updates with provenance and change history. The system extracts facts, preferences, entities, and task learnings from conversations using a secondary LLM call, writes them into the memory graph with full provenance, and maintains snapshot history. Raw transcript dumps are never stored as durable memory. Automatic memory recall (Phase 52), explicit memory tools (Phase 53), and memory review UI (Phase 55) are separate phases.

</domain>

<decisions>
## Implementation Decisions

### Extraction Mechanism
- Secondary LLM call after each completed LLM exchange to analyze conversation and extract stable knowledge
- Each extracted item is one atomic knowledge point (one fact, one preference, one entity, or one task learning per memory node)
- LLM returns structured JSON array, where each element maps directly to a MemoryNode (uri, content, keywords, disclosure_trigger, etc.)
- When LLM judges no stable knowledge exists in the exchange, it returns a JSON object with a `reason` field explaining why nothing was extracted (LIVM-04 compliance)
- The reason is logged for auditability but no memory nodes are created

### Sedimentation Timing & Trigger
- Sedimentation triggered after every LLM call completion (not at run end)
- Secondary LLM call runs asynchronously (fire-and-forget) — does not block the main module output or subsequent propagation
- On failure (LLM call failure, JSON parse error, write error): silently skip with warning log + StepRecord with error status. Main flow is never affected
- Sedimentation is a best-effort background operation

### Sedimentation LLM Configuration
- Sedimentation uses a separately configurable LLM (not necessarily the same as the triggering LLMModule)
- Allows users to assign a cheaper/faster model for the extraction task
- Configuration mechanism follows existing provider registry patterns (Phase 50/51)

### Memory URI & Organization
- All auto-sedimented nodes use the `sediment://` URI prefix, clearly distinguishing them from manually created memory (core://, run://, project://)
- URI path organized by knowledge type: `sediment://fact/{id}`, `sediment://preference/{id}`, `sediment://entity/{id}`, `sediment://learning/{id}`
- ID generation is Claude's discretion (hex, sequential, or content-derived)

### Auto-Generated Metadata
- Extraction LLM simultaneously generates both `keywords` (for GlossaryIndex matching) and `disclosure_trigger` (for DisclosureMatcher) per extracted node
- This ensures sedimented memories are immediately discoverable by Phase 52's automatic recall mechanism
- Keywords stored as JSON array, disclosure_trigger as plain string — consistent with existing MemoryNode schema

### Knowledge Merge Strategy
- When extracted knowledge overlaps with an existing memory node, update the existing node rather than creating a duplicate
- The extraction LLM receives a list of existing memory nodes (URI + content summary, ~200 chars per node) as part of its prompt context
- LLM decides whether each extracted knowledge point should create a new node or update an existing one, returning the target URI accordingly
- Updates use full content replacement via WriteNodeAsync (which auto-snapshots the old version before overwriting)

### Provenance
- Every sedimented node includes SourceStepId (the LLM step that produced the conversation) and SourceArtifactId (if the conversation produced an artifact)
- Sedimentation itself recorded as a 'Sedimentation' StepRecord in the run timeline, with extracted node URIs in the step output
- Complete audit trail: conversation → LLM step → sedimentation step → memory node(s) with provenance back-links

### Claude's Discretion
- Exact extraction prompt design and system message content
- JSON schema details for LLM extraction output
- Token budget for existing memory context in extraction prompt
- ID generation strategy for new sediment:// URIs
- SedimentationService internal API design and method signatures
- How to wire sedimentation into LLMModule's post-execution path
- Exact StepRecord type naming and output format for sedimentation steps
- Configuration UI/schema for sedimentation LLM selection

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` — LIVM-01 through LIVM-04 define all living memory sedimentation requirements

### Memory System (existing code)
- `src/OpenAnima.Core/Memory/IMemoryGraph.cs` — WriteNodeAsync (auto-snapshots on update), GetAllNodesAsync (for existing node context), QueryByPrefixAsync (for sediment:// prefix queries)
- `src/OpenAnima.Core/Memory/MemoryGraph.cs` — SQLite implementation with snapshot versioning (max 10 per node), glossary cache invalidation on write
- `src/OpenAnima.Core/Memory/MemoryNode.cs` — Node record with Uri, Content, DisclosureTrigger, Keywords, SourceArtifactId, SourceStepId, timestamps
- `src/OpenAnima.Core/Memory/MemorySnapshot.cs` — Snapshot record with Id, Uri, AnimaId, Content, SnapshotAt

### LLM Module (integration point)
- `src/OpenAnima.Core/Modules/LLMModule.cs` — ExecuteWithMessagesListAsync is where sedimentation should be triggered after LLM response completes

### Run System
- `src/OpenAnima.Core/Runs/StepRecorder.cs` — RecordStepStartAsync/RecordStepCompleteAsync for sedimentation timeline entries
- `src/OpenAnima.Core/Runs/RunService.cs` — Run lifecycle context for provenance (RunId, StepId)

### Existing Memory Tools (reference patterns)
- `src/OpenAnima.Core/Tools/MemoryWriteTool.cs` — Reference for how to write nodes with provenance (shows WriteNodeAsync usage pattern)

### Provider Registry (for sedimentation LLM config)
- `src/OpenAnima.Core/Providers/` — Provider registry infrastructure for configuring sedimentation LLM
- `.planning/phases/50-provider-registry/50-CONTEXT.md` — Provider registry decisions
- `.planning/phases/51-llm-module-configuration/51-CONTEXT.md` — LLM module configuration patterns (provider/model dropdown, precedence)

### Prior Phase Contexts
- `.planning/phases/52-automatic-memory-recall/52-CONTEXT.md` — MemoryRecallService architecture, XML system message format, StepRecord observability pattern
- `.planning/phases/53-tool-aware-memory-operations/53-CONTEXT.md` — Tool descriptor injection, memory_link/memory_recall tools, provenance via StepRecord

### DI Registration
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` — Service registration patterns for memory services

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MemoryGraph.WriteNodeAsync()`: Already handles snapshot versioning — update triggers auto-snapshot of old content, prune to 10 snapshots per node
- `MemoryGraph.GetAllNodesAsync()`: Can provide existing node list for extraction LLM context
- `MemoryGraph.QueryByPrefixAsync()`: Can query existing `sediment://` nodes specifically
- `StepRecorder`: RecordStepStartAsync/RecordStepCompleteAsync for sedimentation observability
- `LLMProviderRegistryService`: Provider/model lookup for configurable sedimentation LLM
- `ILLMService` / `ChatClient`: LLM call infrastructure for secondary extraction call

### Established Patterns
- Fire-and-forget background work: HeartbeatModule uses `Task.Run` with CTS cancellation — sedimentation follows same pattern
- StepRecord for observability: BootMemoryInjector and MemoryRecallService (Phase 52) use StepRecord for timeline entries — sedimentation uses same pattern
- Service singleton in DI: MemoryRecallService registered via RunServiceExtensions — SedimentationService follows same pattern
- Per-Anima config via IAnimaModuleConfigService: LLMModule uses this for provider selection — sedimentation LLM config follows same pattern

### Integration Points
- `LLMModule.ExecuteWithMessagesListAsync()`: Trigger sedimentation after LLM response, passing conversation messages and response
- `RunServiceExtensions`: Register SedimentationService in DI
- `IMemoryGraph`: Write extracted knowledge nodes, query existing nodes for merge context

</code_context>

<specifics>
## Specific Ideas

- Extraction LLM JSON output example:
  ```json
  {
    "extracted": [
      {
        "action": "create",
        "uri": "sediment://fact/proj-uses-sqlite",
        "content": "The project uses SQLite with Dapper for all persistence, including run data and memory graph.",
        "keywords": "sqlite,dapper,persistence,database",
        "disclosure_trigger": "database"
      },
      {
        "action": "update",
        "uri": "sediment://preference/coding-style",
        "content": "User prefers minimal code with no unnecessary abstractions. Three similar lines preferred over a premature abstraction.",
        "keywords": "coding style,minimal,abstraction",
        "disclosure_trigger": "code style"
      }
    ],
    "skipped_reason": null
  }
  ```
- When no knowledge extracted:
  ```json
  {
    "extracted": [],
    "skipped_reason": "Conversation was a simple greeting exchange with no stable facts, preferences, or learnings."
  }
  ```
- Sedimentation should use the same OpenAI-compatible API pattern as LLMModule — create ChatClient from provider config, send structured prompt, parse JSON response

</specifics>

<deferred>
## Deferred Ideas

- Per-Anima sedimentation policy controls (enable/disable sedimentation, category filters, frequency throttling) — future phase if noise becomes an issue
- Semantic/vector similarity for merge detection instead of LLM judgment — listed in REQUIREMENTS.md v2 as future capability
- Conflict detection and contradiction review across memory versions — listed in REQUIREMENTS.md v2
- Sedimentation from non-LLM module outputs (e.g., workspace tool results) — future expansion

</deferred>

---

*Phase: 54-living-memory-sedimentation*
*Context gathered: 2026-03-22*
