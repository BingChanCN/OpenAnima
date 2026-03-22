# Phase 52: Automatic Memory Recall - Research

**Researched:** 2026-03-22
**Domain:** .NET memory recall integration — BootMemoryInjector wiring, MemoryRecallService, LLMModule injection, StepRecord observability
**Confidence:** HIGH

## Summary

This phase wires together already-implemented memory infrastructure into two integration points: run startup (boot memory) and LLM call dispatch (conversation-triggered recall). The code primitives — `BootMemoryInjector`, `DisclosureMatcher`, `GlossaryIndex`, `IMemoryGraph`, and `IStepRecorder` — exist and are tested. No new persistence layer or matching algorithm is needed; the work is orchestration.

The central new artifact is `MemoryRecallService`: a standalone service that accepts animaId + context string and returns a ranked, deduplicated, budget-bounded list of recalled `MemoryNode` objects with provenance metadata. `LLMModule.ExecuteWithMessagesListAsync` calls this service and inserts a single XML system message at `messages[0]` before the routing system message is inserted. `RunService.StartRunAsync` calls `BootMemoryInjector.InjectBootMemoriesAsync` after the run is placed in `Running` state.

The phase does not introduce new libraries, new persistence tables, or new UI surfaces. Token bounding uses character-count approximation (no SharpToken dependency exists in the codebase). All recalled nodes get provenance — URI, trigger reason, source artifact/step — stored both in the XML `reason` attribute and in a `MemoryRecall` StepRecord in the run timeline.

**Primary recommendation:** Build `MemoryRecallService` as a thin orchestration layer over the existing primitives; inject into `LLMModule` via constructor and into `RunService` via the existing `BootMemoryInjector` call pattern. No new primitive-level code is needed.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Memory Recall Architecture**
- Recall logic encapsulated in an independent `MemoryRecallService` — not embedded directly in LLMModule
- Service exposes "given context, return recalled memory list" contract
- LLMModule calls MemoryRecallService, then assembles system message from results
- This design enables future chain/associative recall (iterative multi-hop) without LLMModule changes

**Memory Injection Format**
- Recalled memory injected as a single system message at messages[0] (before routing system message)
- XML tag structure with `<system-memory>`, `<boot-memory>`, `<recalled-memory>` sections
- Each `<node>` includes `uri` attribute and `reason` attribute explaining why it was recalled
- Boot memory and conversation-triggered memory combined in one system message
- When no memory is recalled (no boot nodes, no matches), no system message is injected — behavior identical to memory system being off

**Token Budget & Bounding**
- Fixed token cap (hard limit) for total memory injection — specific value is Claude's discretion
- Two-layer control: first truncate individual node content (~500 characters), then drop excess nodes by priority
- Priority order: Boot > Disclosure > Glossary; within same type, sorted by UpdatedAt descending (newest first)
- Goal: maximize number of recalled nodes while keeping each node content meaningful

**Runtime Visibility**
- Boot memory: keep existing BootMemoryInjector pattern — each boot node recorded as a separate 'BootMemory' StepRecord in the run timeline
- Conversation-triggered recall: each LLM call's memory recall recorded as an independent 'MemoryRecall' StepRecord before the LLM step
- No Step recorded when no memory is recalled (silent skip)

**Provenance**
- Full provenance per recalled node: URI, trigger reason (disclosure/glossary + matched keyword), source artifact ID, source step ID, content summary
- Provenance visible in both StepRecord (run timeline) and XML `reason` attribute (in prompt to LLM)

**Trigger & Matching Behavior**
- Match scope: only the latest user message in the current LLM call (not entire conversation history)
- Deduplication: URI-based — same node recalled by both disclosure and glossary is injected once, with merged reason (e.g., "disclosure + glossary: keyword1")
- Glossary index rebuilt before every LLM call via `IMemoryGraph.RebuildGlossaryAsync` to ensure freshness

### Claude's Discretion
- Exact token cap value for memory injection
- MemoryRecallService internal API design and method signatures
- How to wire BootMemoryInjector into RunService.StartRunAsync
- Glossary rebuild performance optimization (caching, dirty flag, etc.)
- Exact XML tag naming and attribute format details

### Deferred Ideas (OUT OF SCOPE)
- Chain/associative memory recall (multi-hop iterative recall) — future phase beyond Phase 55
- Semantic/vector memory retrieval — listed in REQUIREMENTS.md v2 as future capability
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| MEMR-01 | Developer-agent run startup injects core boot memory into the run timeline automatically | `BootMemoryInjector.InjectBootMemoriesAsync` exists; needs one call-site in `RunService.StartRunAsync` after run enters Running state |
| MEMR-02 | LLM calls automatically recall matching memory nodes using disclosure triggers from the active conversation context | `DisclosureMatcher.Match()` and `IMemoryGraph.GetDisclosureNodesAsync()` exist; `MemoryRecallService` coordinates them |
| MEMR-03 | LLM calls automatically recall matching memory nodes using glossary keyword matches from the active conversation context | `IMemoryGraph.RebuildGlossaryAsync` + `FindGlossaryMatches()` exist; called before each LLM invocation |
| MEMR-04 | Memory injected into LLM context is ranked, deduplicated, and bounded so recall does not overwhelm prompt budget | Priority ordering (Boot > Disclosure > Glossary), URI deduplication, and character-based token cap implemented in `MemoryRecallService` |
| MEMR-05 | Recalled memory includes visible provenance showing why it was recalled and where it came from | `reason` XML attribute on each `<node>`; `MemoryRecall` StepRecord with provenance summary in the run timeline |
</phase_requirements>

---

## Standard Stack

### Core
| Component | Location | Purpose | Why Standard |
|-----------|----------|---------|--------------|
| `BootMemoryInjector` | `OpenAnima.Core/Memory/BootMemoryInjector.cs` | Queries `core://` nodes and records them as BootMemory StepRecords | Already implemented; just needs call-site wired |
| `DisclosureMatcher` | `OpenAnima.Core/Memory/DisclosureMatcher.cs` | Static case-insensitive substring match of DisclosureTrigger vs context | Fully tested, zero dependencies |
| `GlossaryIndex` | `OpenAnima.Core/Memory/GlossaryIndex.cs` | Aho-Corasick multi-keyword scan | O(n) scan; existing implementation proven in tests |
| `IMemoryGraph` | `OpenAnima.Core/Memory/IMemoryGraph.cs` | `GetDisclosureNodesAsync`, `FindGlossaryMatches`, `RebuildGlossaryAsync` | Complete API; `MemoryGraph` is the concrete singleton |
| `IStepRecorder` | `OpenAnima.Core/Runs/IStepRecorder.cs` | `RecordStepStartAsync` / `RecordStepCompleteAsync` | Established observability primitive throughout the codebase |
| `RunServiceExtensions` | `OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` | DI registration point — `BootMemoryInjector` already registered at line 51 | Add `MemoryRecallService` singleton here |

### New Artifacts (to be created this phase)
| Artifact | Purpose |
|----------|---------|
| `MemoryRecallService` | Orchestrates disclosure + glossary matching, deduplication, priority ranking, token bounding; returns `RecalledMemoryResult` |
| `RecalledMemoryResult` (record) | Carries recalled node list + provenance metadata; passed from service to LLMModule |
| `IMemoryRecallService` (interface) | Contract enabling test doubles; LLMModule and RunService depend on this abstraction |

### No New Libraries
No NuGet packages are introduced. Character-based length approximation (~4 chars per token) is sufficient for the token cap; the codebase has no SharpToken dependency. Exact cap value (Claude's discretion): **6,000 characters** (~1,500 tokens at 4 chars/token), giving generous headroom without flooding context.

**Verification:** Confirmed by searching all `*.csproj` files — no SharpToken or tiktoken package found.

---

## Architecture Patterns

### Recommended Project Structure

```
src/OpenAnima.Core/Memory/
├── BootMemoryInjector.cs         (existing — no changes needed)
├── DisclosureMatcher.cs          (existing — no changes needed)
├── GlossaryIndex.cs              (existing — no changes needed)
├── IMemoryGraph.cs               (existing — no changes needed)
├── IMemoryRecallService.cs       (NEW — service contract)
├── MemoryRecallService.cs        (NEW — orchestration logic)
├── MemoryNode.cs                 (existing — no changes needed)
└── RecalledMemoryResult.cs       (NEW — result record)
```

Integration changes (not new files):
- `RunService.cs` — add `BootMemoryInjector` call in `StartRunAsync`
- `LLMModule.cs` — add `IMemoryRecallService` constructor parameter; call before message assembly
- `RunServiceExtensions.cs` — register `MemoryRecallService` as singleton

### Pattern 1: MemoryRecallService — Recall and Rank

**What:** Service takes `(animaId, contextString)`, loads disclosure nodes and runs glossary scan, deduplicates by URI, applies priority sort (Boot > Disclosure > Glossary within type, newest UpdatedAt first), truncates content to 500 chars per node, drops tail nodes beyond character cap.

**When to use:** Called once per LLM invocation from `LLMModule.ExecuteWithMessagesListAsync`.

**Example (conceptual signature):**
```csharp
// IMemoryRecallService.cs
public interface IMemoryRecallService
{
    Task<RecalledMemoryResult> RecallAsync(
        string animaId,
        string context,
        CancellationToken ct = default);
}

// RecalledMemoryResult.cs
public record RecalledMemoryResult
{
    public IReadOnlyList<RecalledNode> Nodes { get; init; } = [];
    public bool HasAny => Nodes.Count > 0;
}

public record RecalledNode
{
    public MemoryNode Node { get; init; } = null!;
    public string Reason { get; init; } = string.Empty;  // e.g., "disclosure", "glossary: architecture", "disclosure + glossary: arch"
    public string RecallType { get; init; } = string.Empty;  // "Disclosure" | "Glossary"
}
```

### Pattern 2: Boot Memory Wiring in RunService.StartRunAsync

**What:** After the run transitions to Running state and the `RunContext` is stored in `_activeRuns`, call `BootMemoryInjector.InjectBootMemoriesAsync`. This must happen after the run is active because `StepRecorder.RecordStepStartAsync` requires an active run to be retrievable via `_runService.GetActiveRun(animaId)`.

**When to use:** End of `StartRunAsync`, before `PushRunStateChangedAsync`.

```csharp
// In RunService.StartRunAsync, after _activeRuns[runId] = context; and _animaActiveRunMap[animaId] = runId;
await _bootMemoryInjector.InjectBootMemoriesAsync(animaId, ct);
```

**Dependency injection:** `RunService` constructor gains `BootMemoryInjector` parameter.

### Pattern 3: LLMModule Memory Injection

**What:** In `ExecuteWithMessagesListAsync`, before the routing system message is inserted, call `MemoryRecallService.RecallAsync` with the latest user message as context. If `result.HasAny`, insert the XML system message at `messages[0]`.

**Context string extraction:** The latest user message is the last entry in the messages list with `Role == "user"`. If no user message exists (edge case), skip recall.

**Injection position:** Memory system message inserted first (index 0), then routing system message inserted at index 0 pushing memory to index 1. This gives prompt ordering: memory context → routing instructions → conversation.

**When to use:** Every LLM call that has at least one user message.

### Pattern 4: XML System Message Assembly

**What:** Build the memory system message from `RecalledMemoryResult`. Boot nodes go into `<boot-memory>`, conversation-triggered nodes into `<recalled-memory>`. Each node has `uri` and `reason` attributes.

```xml
<system-memory>
  <boot-memory>
    <node uri="core://agent/identity">Content here (truncated to 500 chars)</node>
  </boot-memory>
  <recalled-memory>
    <node uri="run://abc/findings" reason="glossary: architecture">Content here</node>
    <node uri="core://risks/known" reason="disclosure + glossary: risk">Content here</node>
  </recalled-memory>
</system-memory>
```

**Note:** Boot nodes do not need a `reason` attribute — their reason is implicit (always injected at run start).

### Pattern 5: MemoryRecall StepRecord

**What:** Before the LLM call, record a `MemoryRecall` StepRecord with a summary of recalled URIs and reasons. Pattern is identical to the BootMemory step pattern in `BootMemoryInjector`.

```csharp
var summary = string.Join("; ", result.Nodes.Select(n => $"{n.Node.Uri} ({n.Reason})"));
var stepId = await _stepRecorder.RecordStepStartAsync(animaId, "MemoryRecall", summary, null, ct);
await _stepRecorder.RecordStepCompleteAsync(stepId, "MemoryRecall", $"{result.Nodes.Count} nodes recalled", ct);
```

**Silent skip:** If `result.HasAny` is false, no step is recorded.

### Anti-Patterns to Avoid

- **Embedding recall logic in LLMModule directly:** Violates the encapsulation decision and prevents future multi-hop recall. Always delegate to `IMemoryRecallService`.
- **Calling `BootMemoryInjector.InjectBootMemoriesAsync` before `_activeRuns[runId] = context`:** `StepRecorder` calls `_runService.GetActiveRun()` which requires the run to already be registered — ordering matters.
- **Inserting memory system message after routing system message:** Memory goes at index 0, routing goes at index 0 (pushing memory to 1). If order is reversed — routing inserted first, memory second — memory becomes index 0 and routing becomes index 1, which is acceptable but inconsistent with intent. Safest: insert memory first, then insert routing (which shifts memory to position 1).
- **Rebuilding glossary after every individual node write (not after bulk writes):** `RebuildGlossaryAsync` is designed to be called before each LLM invocation, not after every graph mutation. Calling it in every write path would be redundant.
- **Matching entire conversation history instead of latest user message:** The decision locks match scope to the latest user message only. Scanning history would increase false positive noise and latency.
- **Using token library for budget math:** No token library exists in the codebase. Use character-count approximation.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Multi-keyword text scan | Custom loop | `GlossaryIndex.FindMatches()` + `IMemoryGraph.FindGlossaryMatches()` | Aho-Corasick O(n) already implemented and tested |
| Substring disclosure matching | Custom Contains loop | `DisclosureMatcher.Match()` | Already handles null trigger exclusion, case-insensitive |
| Boot node querying | Manual URI prefix filter | `IMemoryGraph.QueryByPrefixAsync("core://")` | Tested, handles empty result gracefully |
| Step timeline recording | Custom DB insert | `IStepRecorder.RecordStepStartAsync` / `RecordStepCompleteAsync` | Handles convergence guard, SignalR push, truncation |
| Glossary trie construction | Hand-rolled trie | `GlossaryIndex.Build()` | Already handles failure links, case normalization |

**Key insight:** All primitives are implemented and tested. This phase is orchestration, not algorithm design.

---

## Common Pitfalls

### Pitfall 1: RunService.StartRunAsync — Call Order for BootMemoryInjector

**What goes wrong:** Calling `InjectBootMemoriesAsync` before `_activeRuns[runId] = context` is set. `StepRecorder.RecordStepStartAsync` calls `_runService.GetActiveRun(animaId)` which returns null if the run is not yet in `_activeRuns`. The call becomes a silent no-op instead of injecting boot memories.

**Why it happens:** The run creation sequence has multiple steps; easy to insert the call at the wrong point.

**How to avoid:** Call `InjectBootMemoriesAsync` after both `_activeRuns[runId] = context` and `_animaActiveRunMap[animaId] = runId` are set — these are the prerequisites `GetActiveRun` needs.

**Warning signs:** Test shows boot nodes present but no BootMemory StepRecord appears in timeline.

### Pitfall 2: Memory System Message vs. Routing System Message Order

**What goes wrong:** If routing system message is inserted at index 0 first, then memory is inserted at index 0 second, memory ends up at position 0 and routing at position 1. The LLM sees memory context first and routing instructions second — this is actually the desired order (memory → routing → conversation). The pitfall is assuming a different order and getting it backwards.

**How to avoid:** Establish and document the insertion order explicitly. Insert memory first (produces [memory, ...conversation]), then insert routing (produces [routing, memory, ...conversation]). Final order: routing at 0, memory at 1, conversation after.

**Warning signs:** LLM doesn't honor routing format because routing system message is buried after memory.

### Pitfall 3: Glossary Not Rebuilt Before Matching

**What goes wrong:** `IMemoryGraph.FindGlossaryMatches()` returns empty list even though nodes with keywords exist. The glossary trie is only populated after `RebuildGlossaryAsync` is called; if the call is omitted, the trie is empty.

**Why it happens:** `FindGlossaryMatches` is synchronous and returns silently when the trie is empty — no error is thrown.

**How to avoid:** Always call `await _memoryGraph.RebuildGlossaryAsync(animaId, ct)` before `FindGlossaryMatches` in `MemoryRecallService.RecallAsync`. The rebuild is idempotent.

**Warning signs:** Glossary recall always returns zero matches even with matching keywords.

### Pitfall 4: URI Deduplication — Merge Reasons Correctly

**What goes wrong:** A node is recalled by both disclosure and glossary; it gets injected twice (once per source), wasting token budget and confusing the LLM.

**Why it happens:** Both code paths return the same URI independently; without a shared dictionary, deduplication is missed.

**How to avoid:** Use a `Dictionary<string, RecalledNode>` keyed by URI during assembly. When a URI is already present, merge the `Reason` strings (e.g., "disclosure + glossary: architecture").

**Warning signs:** Same URI appears twice in the generated XML system message.

### Pitfall 5: Token Budget — Order of Operations

**What goes wrong:** Applying the per-node content truncation (500 chars) AFTER the total budget cap instead of before. This means budget calculation is done on un-truncated content, causing incorrect node selection.

**Why it happens:** The two-layer truncation logic described in the CONTEXT.md is easy to implement in the wrong order.

**How to avoid:** Truncate individual node content first (500 char cap per node), then accumulate character count and drop tail nodes when the running total exceeds the hard cap.

---

## Code Examples

Verified patterns from existing source code:

### Boot Memory Pattern (BootMemoryInjector.cs — already working)
```csharp
// Source: src/OpenAnima.Core/Memory/BootMemoryInjector.cs
public async Task InjectBootMemoriesAsync(string animaId, CancellationToken ct = default)
{
    var bootNodes = await _memoryGraph.QueryByPrefixAsync(animaId, "core://", ct);
    if (bootNodes.Count == 0) return;

    foreach (var node in bootNodes)
    {
        var stepId = await _stepRecorder.RecordStepStartAsync(
            animaId, "BootMemory", $"Boot: {node.Uri}", null, ct);
        var summary = node.Content.Length > 500 ? node.Content[..500] : node.Content;
        await _stepRecorder.RecordStepCompleteAsync(stepId, "BootMemory", summary, ct);
    }
}
```

### Disclosure Matching
```csharp
// Source: src/OpenAnima.Core/Memory/DisclosureMatcher.cs
// disclosureNodes from: await _memoryGraph.GetDisclosureNodesAsync(animaId, ct)
var matched = DisclosureMatcher.Match(disclosureNodes, userMessage);
```

### Glossary Matching
```csharp
// Source: src/OpenAnima.Core/Memory/IMemoryGraph.cs
await _memoryGraph.RebuildGlossaryAsync(animaId, ct);
var keywordMatches = _memoryGraph.FindGlossaryMatches(animaId, userMessage);
// Returns: IReadOnlyList<(string Keyword, string Uri)>
```

### Routing System Message Insertion (existing LLMModule pattern)
```csharp
// Source: src/OpenAnima.Core/Modules/LLMModule.cs line ~178
messages.Insert(0, new ChatMessageInput("system", BuildSystemMessage(ports)));
```

### MemoryRecall StepRecord (analogous to existing BootMemory pattern)
```csharp
// Pattern: record recalled nodes before LLM call
var summary = string.Join("; ", result.Nodes.Select(n => $"{n.Node.Uri} ({n.Reason})"));
var stepId = await _stepRecorder.RecordStepStartAsync(animaId, "MemoryRecall", summary, null, ct);
await _stepRecorder.RecordStepCompleteAsync(stepId, "MemoryRecall",
    $"{result.Nodes.Count} nodes recalled", ct);
```

### DI Registration Pattern (RunServiceExtensions.cs)
```csharp
// Source: src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs line 51
services.AddSingleton<BootMemoryInjector>();
// New pattern — same file, same method:
services.AddSingleton<IMemoryRecallService, MemoryRecallService>();
```

---

## State of the Art

| Old Approach | Current Approach | Status | Impact |
|--------------|-----------------|--------|--------|
| BootMemoryInjector registered in DI but never called | Call from `RunService.StartRunAsync` after run goes Running | This phase wires it | Boot memory becomes active |
| LLMModule assembles messages with only routing system message | LLMModule prepends memory system message before routing | This phase adds it | Conversation-triggered recall becomes active |
| Glossary match returns results but nothing consumes them | `MemoryRecallService` consumes `FindGlossaryMatches` output | This phase creates the consumer | MEMR-03 satisfied |

**Already complete (no changes needed):**
- `DisclosureMatcher` — fully implemented and unit tested
- `GlossaryIndex` — Aho-Corasick with failure links, tested
- `IMemoryGraph` with `GetDisclosureNodesAsync`, `RebuildGlossaryAsync`, `FindGlossaryMatches` — complete
- `BootMemoryInjector` with StepRecord pattern — complete
- `RunServiceExtensions` DI registration for `BootMemoryInjector` — complete

---

## Open Questions

1. **Glossary rebuild caching (Claude's discretion)**
   - What we know: `RebuildGlossaryAsync` reloads all nodes for the animaId on each call; this is potentially expensive for large memory graphs.
   - What's unclear: Whether a dirty-flag or version-counter approach should be implemented now vs. left as a simple "always rebuild."
   - Recommendation: Start with "always rebuild before each LLM call." Performance optimization (dirty flag on `WriteNodeAsync`) can be added in Phase 54 (Living Memory Sedimentation) when write volume increases. Keep it simple for Phase 52.

2. **Exact token cap value (Claude's discretion)**
   - What we know: Needs to be a fixed character budget; individual nodes capped at 500 chars.
   - Recommendation: **6,000 characters** for total injection (~1,500 tokens at 4 chars/token average). This allows up to 12 fully-sized nodes, which is generous without overwhelming a typical 8k-32k context window. Planner may adjust.

3. **LLMModule constructor injection vs. property injection for IMemoryRecallService**
   - What we know: All existing `LLMModule` dependencies are constructor-injected. The existing DI pattern uses `ICrossAnimaRouter? router = null` as optional.
   - Recommendation: Use optional constructor injection (`IMemoryRecallService? memoryRecallService = null`) for backward compatibility with tests that create `LLMModule` directly without the service. When null, recall is silently skipped.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (.NET 10.0) |
| Config file | `tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |
| Quick run command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~MemoryRecall"` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MEMR-01 | Boot memory injected at StartRunAsync | Unit | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~BootMemory"` | ❌ Wave 0 |
| MEMR-02 | Disclosure trigger matching injects node into LLM messages | Unit | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~MemoryRecall"` | ❌ Wave 0 |
| MEMR-03 | Glossary keyword match injects node into LLM messages | Unit | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~MemoryRecall"` | ❌ Wave 0 |
| MEMR-04 | Priority ordering, deduplication, and character cap enforce token budget | Unit | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~MemoryRecallService"` | ❌ Wave 0 |
| MEMR-05 | Recalled nodes include provenance in StepRecord and XML reason attribute | Unit | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~MemoryRecall"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~MemoryRecall OR FullyQualifiedName~BootMemory"`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/MemoryRecallServiceTests.cs` — covers MEMR-02, MEMR-03, MEMR-04, MEMR-05 (unit tests with fake IMemoryGraph)
- [ ] `tests/OpenAnima.Tests/Unit/BootMemoryInjectorWiringTests.cs` — covers MEMR-01 (verifies `RunService.StartRunAsync` calls injector)
- [ ] `tests/OpenAnima.Tests/Unit/LLMModuleMemoryTests.cs` — covers MEMR-02, MEMR-03, MEMR-05 (integration: LLMModule inserts memory system message)

*(Existing tests: `DisclosureMatcherTests.cs` and `MemoryGraphTests.cs` already cover primitives — no changes needed.)*

---

## Sources

### Primary (HIGH confidence)
- Direct code reading: `src/OpenAnima.Core/Memory/BootMemoryInjector.cs` — complete implementation
- Direct code reading: `src/OpenAnima.Core/Memory/DisclosureMatcher.cs` — static Match method
- Direct code reading: `src/OpenAnima.Core/Memory/GlossaryIndex.cs` — Aho-Corasick implementation
- Direct code reading: `src/OpenAnima.Core/Memory/IMemoryGraph.cs` — full interface contract
- Direct code reading: `src/OpenAnima.Core/Memory/MemoryNode.cs` — node record fields
- Direct code reading: `src/OpenAnima.Core/Modules/LLMModule.cs` — `ExecuteWithMessagesListAsync` flow, message insertion patterns
- Direct code reading: `src/OpenAnima.Core/Runs/RunService.cs` — `StartRunAsync` call sequence
- Direct code reading: `src/OpenAnima.Core/Runs/StepRecorder.cs` — `RecordStepStartAsync` / `RecordStepCompleteAsync` signatures
- Direct code reading: `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` — DI registration patterns
- Direct code reading: `.planning/phases/52-automatic-memory-recall/52-CONTEXT.md` — all locked decisions
- Direct test reading: `tests/OpenAnima.Tests/Unit/DisclosureMatcherTests.cs`, `MemoryGraphTests.cs`

### Secondary (MEDIUM confidence)
- `.planning/REQUIREMENTS.md` — MEMR-01 through MEMR-05 descriptions
- `.planning/STATE.md` — Phase 52 context notes

### Tertiary (LOW confidence)
- None

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — All components verified by direct code inspection
- Architecture patterns: HIGH — Based on existing call patterns in BootMemoryInjector and LLMModule
- Pitfalls: HIGH — Call-order pitfall verified by reading StepRecorder.RecordStepStartAsync (requires active run); deduplication pitfall obvious from reading both match paths

**Research date:** 2026-03-22
**Valid until:** 2026-04-22 (stable codebase — only changes if LLMModule or RunService signature changes)
