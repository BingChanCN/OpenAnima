# Phase 52: Automatic Memory Recall - Context

**Gathered:** 2026-03-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Developer-agent runs and LLM calls automatically recall relevant memory without overwhelming prompt context. Boot memory is injected at run start, disclosure triggers and glossary keywords are matched during LLM calls, and all injected memory is ranked, deduplicated, bounded, and explainable. Memory tools (memory_link, memory_recall) and conversation auto-sedimentation are separate phases (53, 54).

</domain>

<decisions>
## Implementation Decisions

### Memory Recall Architecture
- Recall logic encapsulated in an independent `MemoryRecallService` — not embedded directly in LLMModule
- Service exposes "given context, return recalled memory list" contract
- LLMModule calls MemoryRecallService, then assembles system message from results
- This design enables future chain/associative recall (iterative multi-hop) without LLMModule changes

### Memory Injection Format
- Recalled memory injected as a single system message at messages[0] (before routing system message)
- XML tag structure with `<system-memory>`, `<boot-memory>`, `<recalled-memory>` sections
- Each `<node>` includes `uri` attribute and `reason` attribute explaining why it was recalled
- Boot memory and conversation-triggered memory combined in one system message
- When no memory is recalled (no boot nodes, no matches), no system message is injected — behavior identical to memory system being off

### Token Budget & Bounding
- Fixed token cap (hard limit) for total memory injection — specific value is Claude's discretion
- Two-layer control: first truncate individual node content (~500 characters), then drop excess nodes by priority
- Priority order: Boot > Disclosure > Glossary; within same type, sorted by UpdatedAt descending (newest first)
- Goal: maximize number of recalled nodes while keeping each node content meaningful

### Runtime Visibility
- Boot memory: keep existing BootMemoryInjector pattern — each boot node recorded as a separate 'BootMemory' StepRecord in the run timeline
- Conversation-triggered recall: each LLM call's memory recall recorded as an independent 'MemoryRecall' StepRecord before the LLM step
- No Step recorded when no memory is recalled (silent skip)

### Provenance
- Full provenance per recalled node: URI, trigger reason (disclosure/glossary + matched keyword), source artifact ID, source step ID, content summary
- Provenance visible in both StepRecord (run timeline) and XML `reason` attribute (in prompt to LLM)

### Trigger & Matching Behavior
- Match scope: only the latest user message in the current LLM call (not entire conversation history)
- Deduplication: URI-based — same node recalled by both disclosure and glossary is injected once, with merged reason (e.g., "disclosure + glossary: keyword1")
- Glossary index rebuilt before every LLM call via `IMemoryGraph.RebuildGlossaryAsync` to ensure freshness

### Claude's Discretion
- Exact token cap value for memory injection
- MemoryRecallService internal API design and method signatures
- How to wire BootMemoryInjector into RunService.StartRunAsync
- Glossary rebuild performance optimization (caching, dirty flag, etc.)
- Exact XML tag naming and attribute format details

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` — MEMR-01 through MEMR-05 define all memory recall requirements

### Memory System (existing code)
- `src/OpenAnima.Core/Memory/BootMemoryInjector.cs` — Boot memory injector, registered in DI but never called from run-start path; records each boot node as StepRecord
- `src/OpenAnima.Core/Memory/DisclosureMatcher.cs` — Static Match method: case-insensitive substring matching of DisclosureTrigger against context string
- `src/OpenAnima.Core/Memory/GlossaryIndex.cs` — Aho-Corasick trie for single-pass multi-keyword matching; Build + FindMatches API
- `src/OpenAnima.Core/Memory/IMemoryGraph.cs` — Full memory graph interface: GetDisclosureNodesAsync, FindGlossaryMatches, RebuildGlossaryAsync, QueryByPrefixAsync
- `src/OpenAnima.Core/Memory/MemoryNode.cs` — Node record with Uri, Content, DisclosureTrigger, Keywords, SourceArtifactId, SourceStepId, timestamps

### Run System (integration points)
- `src/OpenAnima.Core/Runs/RunService.cs` — StartRunAsync is where BootMemoryInjector should be called; currently does not invoke it
- `src/OpenAnima.Core/Runs/IRunService.cs` — Run lifecycle contract
- `src/OpenAnima.Core/Runs/StepRecorder.cs` — RecordStepStartAsync/RecordStepCompleteAsync for timeline entries

### LLM Module (integration point)
- `src/OpenAnima.Core/Modules/LLMModule.cs` — ExecuteWithMessagesListAsync is where memory recall should be wired; currently assembles messages list without memory injection

### DI Registration
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` — BootMemoryInjector already registered as singleton (line 51)

### Phase 50 Context
- `.planning/phases/50-provider-registry/50-CONTEXT.md` — Provider registry decisions affecting LLM module configuration

### Phase 51 Context
- `.planning/phases/51-llm-module-configuration/51-CONTEXT.md` — LLM module dropdown selection and precedence order decisions

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `BootMemoryInjector`: Fully implemented, just needs to be called from RunService.StartRunAsync
- `DisclosureMatcher.Match()`: Static method ready for use — pass disclosure nodes and context string
- `GlossaryIndex`: Aho-Corasick trie ready — Build from keyword-URI pairs, FindMatches on content
- `IMemoryGraph`: Complete API with GetDisclosureNodesAsync, FindGlossaryMatches, RebuildGlossaryAsync
- `StepRecorder`: RecordStepStartAsync/RecordStepCompleteAsync for timeline entries
- `SharpToken TokenCounter`: Existing token counting utility for enforcing budget cap

### Established Patterns
- System message injection: LLMModule already inserts routing system message at messages[0] — memory system message uses same pattern
- StepRecord for observability: BootMemoryInjector already uses RecordStepStartAsync/RecordStepCompleteAsync — same pattern for MemoryRecall steps
- XML markers: `<route>` tags established in FormatDetector — `<system-memory>` tags follow same convention
- Singleton service in DI: BootMemoryInjector registered via RunServiceExtensions — MemoryRecallService follows same pattern

### Integration Points
- `RunService.StartRunAsync()`: Call BootMemoryInjector.InjectBootMemoriesAsync after run creation, before returning
- `LLMModule.ExecuteWithMessagesListAsync()`: Call MemoryRecallService before message assembly, insert system message at messages[0]
- `RunServiceExtensions`: Register MemoryRecallService in DI

</code_context>

<specifics>
## Specific Ideas

- Future chain/associative recall (multi-hop: recall → combine → recall again) noted as future capability — current architecture should support it by iterating within MemoryRecallService
- XML tag format example:
  ```xml
  <system-memory>
    <boot-memory>
      <node uri="core://agent/identity">Content here</node>
    </boot-memory>
    <recalled-memory>
      <node uri="run://abc/findings" reason="glossary: architecture">Content here</node>
    </recalled-memory>
  </system-memory>
  ```

</specifics>

<deferred>
## Deferred Ideas

- Chain/associative memory recall (multi-hop iterative recall) — future phase beyond Phase 55
- Semantic/vector memory retrieval — listed in REQUIREMENTS.md v2 as future capability

</deferred>

---

*Phase: 52-automatic-memory-recall*
*Context gathered: 2026-03-22*
