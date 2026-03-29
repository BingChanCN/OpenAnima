# Phase 54: Living Memory Sedimentation - Research

**Researched:** 2026-03-22
**Domain:** Async background LLM extraction, memory graph persistence, provenance tracking
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Extraction Mechanism**
- Secondary LLM call after each completed LLM exchange to analyze conversation and extract stable knowledge
- Each extracted item is one atomic knowledge point (one fact, one preference, one entity, or one task learning per memory node)
- LLM returns structured JSON array, where each element maps directly to a MemoryNode (uri, content, keywords, disclosure_trigger, etc.)
- When LLM judges no stable knowledge exists in the exchange, it returns a JSON object with a `reason` field explaining why nothing was extracted (LIVM-04 compliance)
- The reason is logged for auditability but no memory nodes are created

**Sedimentation Timing & Trigger**
- Sedimentation triggered after every LLM call completion (not at run end)
- Secondary LLM call runs asynchronously (fire-and-forget) — does not block the main module output or subsequent propagation
- On failure (LLM call failure, JSON parse error, write error): silently skip with warning log + StepRecord with error status. Main flow is never affected
- Sedimentation is a best-effort background operation

**Sedimentation LLM Configuration**
- Sedimentation uses a separately configurable LLM (not necessarily the same as the triggering LLMModule)
- Allows users to assign a cheaper/faster model for the extraction task
- Configuration mechanism follows existing provider registry patterns (Phase 50/51)

**Memory URI & Organization**
- All auto-sedimented nodes use the `sediment://` URI prefix, clearly distinguishing them from manually created memory (core://, run://, project://)
- URI path organized by knowledge type: `sediment://fact/{id}`, `sediment://preference/{id}`, `sediment://entity/{id}`, `sediment://learning/{id}`
- ID generation is Claude's discretion (hex, sequential, or content-derived)

**Auto-Generated Metadata**
- Extraction LLM simultaneously generates both `keywords` (for GlossaryIndex matching) and `disclosure_trigger` (for DisclosureMatcher) per extracted node
- This ensures sedimented memories are immediately discoverable by Phase 52's automatic recall mechanism
- Keywords stored as JSON array, disclosure_trigger as plain string — consistent with existing MemoryNode schema

**Knowledge Merge Strategy**
- When extracted knowledge overlaps with an existing memory node, update the existing node rather than creating a duplicate
- The extraction LLM receives a list of existing memory nodes (URI + content summary, ~200 chars per node) as part of its prompt context
- LLM decides whether each extracted knowledge point should create a new node or update an existing one, returning the target URI accordingly
- Updates use full content replacement via WriteNodeAsync (which auto-snapshots the old version before overwriting)

**Provenance**
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

### Deferred Ideas (OUT OF SCOPE)
- Per-Anima sedimentation policy controls (enable/disable sedimentation, category filters, frequency throttling) — future phase if noise becomes an issue
- Semantic/vector similarity for merge detection instead of LLM judgment — listed in REQUIREMENTS.md v2 as future capability
- Conflict detection and contradiction review across memory versions — listed in REQUIREMENTS.md v2
- Sedimentation from non-LLM module outputs (e.g., workspace tool results) — future expansion
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| LIVM-01 | System can automatically extract stable facts, preferences, entities, or task learnings from completed LLM exchanges into the memory graph | SedimentationService fires after each LLMModule completion; secondary LLM call extracts structured JSON; WriteNodeAsync persists each atomic item |
| LIVM-02 | Automatic memory writes create or update memory nodes with provenance linking back to source run, step, or artifact | MemoryNode.SourceStepId and SourceArtifactId fields carry provenance; sedimentation StepRecord records the trigger step ID; IRunService.GetActiveRun provides current RunId |
| LIVM-03 | Automatic memory writes update snapshot history so users can review what changed over time | MemoryGraph.WriteNodeAsync already auto-snapshots old content on update; max 10 snapshots per URI; Phase 55 will surface these in UI |
| LIVM-04 | System avoids storing raw transcript dumps as durable memory when no stable knowledge was extracted | LLM returns `{"extracted":[],"skipped_reason":"..."}` when no stable knowledge found; service skips write and logs reason; no nodes created |
</phase_requirements>

---

## Summary

Phase 54 adds automatic knowledge extraction from completed LLM conversations into the memory graph. The implementation centers on a new `SedimentationService` that fires a secondary LLM call in a fire-and-forget background task after each `LLMModule.ExecuteWithMessagesListAsync` completes. The secondary call receives the conversation messages, the LLM response, and a truncated list of existing `sediment://` nodes as context; it returns a structured JSON payload that the service maps directly to `MemoryNode` records and persists via `IMemoryGraph.WriteNodeAsync`.

The entire system is built on existing infrastructure: `IMemoryGraph.WriteNodeAsync` already handles snapshot versioning (LIVM-03 for free); `IStepRecorder` already handles run-timeline observability; `LLMProviderRegistryService.GetDecryptedApiKey` plus the OpenAI `ChatClient` pattern are the same path `LLMModule.CallLlmAsync` uses; and `Task.Run` fire-and-forget matches the `HeartbeatModule` pattern. No new persistence schema, no new DI infrastructure beyond registering `SedimentationService` as a singleton.

The most consequential design decision is how sedimentation wires into `LLMModule`: the service is injected as an optional dependency (same pattern as `IMemoryRecallService` and `IStepRecorder`), and triggered by calling `_ = SedimentAsync(...)` after `PublishResponseAsync` to ensure the response is published to downstream modules before background work starts.

**Primary recommendation:** Build `SedimentationService` as a self-contained singleton with a single public method `SedimentAsync(animaId, messages, llmResponse, sourceStepId, ct)`. Inject it optionally into `LLMModule`. Trigger asynchronously with `_ = Task.Run(() => ...)` after response publication. All failure paths log a warning and record an error-status `StepRecord` — the main pipeline is never blocked or interrupted.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `OpenAI` (Azure SDK fork) | Already in project | Create `ChatClient` for secondary extraction LLM call | Same client used by `LLMModule.CompleteWithCustomClientAsync` |
| `System.Text.Json` | Built-in (.NET 10) | Parse extraction JSON response | Already used throughout project (MemoryGraph, tools) |
| `Dapper` + SQLite | Already in project | Persistence via `IMemoryGraph.WriteNodeAsync` | Project's only ORM; schema already covers all required fields |
| xUnit | 2.9.3 (already in project) | Unit tests for SedimentationService | Project test framework |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.Extensions.Logging` | Built-in | Warning logs on skipped sedimentation | Already used throughout |
| `Microsoft.Data.Sqlite` | Already in project | In-memory SQLite for test fixtures | Used in `MemoryToolPhase53Tests`, `MemoryRecallServiceTests` |

**Installation:** No new packages required. All dependencies already present in the project.

---

## Architecture Patterns

### Recommended Project Structure

```
src/OpenAnima.Core/
├── Memory/
│   ├── SedimentationService.cs         # New: fire-and-forget extraction service
│   ├── ISedimentationService.cs        # New: optional interface for testability
│   ├── IMemoryGraph.cs                 # Existing (WriteNodeAsync, GetAllNodesAsync)
│   └── MemoryNode.cs                   # Existing (record with all required fields)
├── Modules/
│   └── LLMModule.cs                    # Modified: inject + trigger SedimentationService
└── DependencyInjection/
    └── RunServiceExtensions.cs         # Modified: register SedimentationService

tests/OpenAnima.Tests/Unit/
└── SedimentationServiceTests.cs        # New: unit tests with FakeMemoryGraph + fake LLM
```

### Pattern 1: Fire-and-Forget Background Task

**What:** Background work launched with `Task.Run` using a separate `CancellationTokenSource`. The caller returns immediately; failures are caught internally.

**When to use:** Any sedimentation trigger point — after `PublishResponseAsync` in `LLMModule.ExecuteWithMessagesListAsync`.

**Example:**

```csharp
// Source: HeartbeatModule.cs pattern (Task.Run with CTS)
// In LLMModule.ExecuteWithMessagesListAsync, after PublishResponseAsync:

if (_sedimentationService != null)
{
    // Capture values needed in background task — avoid closure over mutable state
    var capturedAnimaId = animaId;
    var capturedMessages = new List<ChatMessageInput>(messages); // snapshot
    var capturedResponse = result.Content;
    var capturedStepId = /* step ID of the LLM call that just completed */;

    _ = Task.Run(async () =>
    {
        try
        {
            await _sedimentationService.SedimentAsync(
                capturedAnimaId, capturedMessages, capturedResponse, capturedStepId,
                CancellationToken.None); // not linked to caller CTS — background lifetime
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sedimentation failed silently for Anima {AnimaId}", capturedAnimaId);
        }
    }, CancellationToken.None);
}
```

### Pattern 2: Extraction LLM Call (Same Client Pattern as LLMModule)

**What:** Resolve provider config via `IAnimaModuleConfigService` using a dedicated "Sedimentation" module key. Create `ChatClient`, call `CompleteChatAsync`, parse JSON response.

**When to use:** Inside `SedimentationService.SedimentAsync` to call the extraction model.

**Example:**

```csharp
// Source: LLMModule.CompleteWithCustomClientAsync pattern
// Config keys: sedimentProviderSlug, sedimentModelId (or fallback to global ILLMService)
var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(baseUrl) };
var chatClient = new ChatClient(modelName, new ApiKeyCredential(apiKey), clientOptions);
var completion = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);
var json = completion.Value.Content[0].Text;
```

### Pattern 3: Extraction JSON Parsing

**What:** Parse the LLM's JSON response into a typed DTO. Handle both "items extracted" and "nothing extracted" cases.

**Example:**

```csharp
// Source: CONTEXT.md <specifics> section (canonical JSON schema)
public record SedimentationResult(
    List<SedimentedItem> Extracted,
    string? SkippedReason);

public record SedimentedItem(
    string Action,         // "create" or "update"
    string Uri,
    string Content,
    string? Keywords,      // comma-separated or JSON array
    string? DisclosureTrigger);

// Parse:
var result = JsonSerializer.Deserialize<SedimentationResult>(json,
    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

if (result.Extracted.Count == 0)
{
    _logger.LogDebug("Sedimentation skipped: {Reason}", result.SkippedReason);
    return; // LIVM-04: no node written
}
```

### Pattern 4: StepRecord Observability for Sedimentation

**What:** Record sedimentation in the run timeline using `IStepRecorder`. Same pattern as `MemoryRecallService` in Phase 52.

**When to use:** Wrap the entire sedimentation attempt: start before extraction LLM call, complete with extracted URIs, or fail with error info.

**Example:**

```csharp
// Source: LLMModule.cs lines 266-267 (MemoryRecall StepRecord pattern)
var stepId = await _stepRecorder.RecordStepStartAsync(
    animaId, "Sedimentation", $"Extracting from LLM exchange (source step: {sourceStepId})",
    propagationId: null, ct);

// ... do extraction work ...

await _stepRecorder.RecordStepCompleteAsync(
    stepId, "Sedimentation",
    $"{nodesWritten} nodes sedimented: {string.Join(", ", writtenUris)}", ct);

// On error:
await _stepRecorder.RecordStepFailedAsync(stepId, "Sedimentation", exception, ct);
```

### Pattern 5: MemoryNode Write with Provenance

**What:** Construct `MemoryNode` with all required provenance fields and call `WriteNodeAsync`. Update path: check if node exists first — WriteNodeAsync handles snapshot automatically.

**Example:**

```csharp
// Source: MemoryWriteTool.cs (WriteNodeAsync usage pattern)
var now = DateTimeOffset.UtcNow.ToString("O");
var node = new MemoryNode
{
    Uri = item.Uri,               // e.g. "sediment://fact/proj-uses-sqlite"
    AnimaId = animaId,
    Content = item.Content,
    DisclosureTrigger = item.DisclosureTrigger,
    Keywords = NormalizeKeywordsToJson(item.Keywords), // ensure JSON array format
    SourceStepId = sourceStepId,  // LIVM-02: provenance to triggering LLM step
    SourceArtifactId = null,      // populated only if conversation produced artifact
    CreatedAt = now,
    UpdatedAt = now
};

await _memoryGraph.WriteNodeAsync(node, ct); // auto-snapshots if URI already exists (LIVM-03)
```

### Pattern 6: Existing Node Context for Merge Decisions

**What:** Provide the extraction LLM with a truncated list of existing `sediment://` nodes so it can decide create vs update.

**When to use:** Build the extraction prompt in `SedimentationService`.

**Example:**

```csharp
// Source: IMemoryGraph.QueryByPrefixAsync — returns sediment:// nodes only
var existingNodes = await _memoryGraph.QueryByPrefixAsync(animaId, "sediment://", ct);

// Truncate to ~200 chars per node for token budget
var contextSummary = string.Join("\n", existingNodes.Select(n =>
    $"- {n.Uri}: {n.Content[..Math.Min(200, n.Content.Length)]}"));
```

### Anti-Patterns to Avoid

- **Blocking the LLM pipeline:** Never `await` sedimentation inline before `PublishResponseAsync`. Must be fire-and-forget.
- **Throwing exceptions to the caller:** All errors inside `SedimentAsync` are caught and logged as warnings. `RecordStepFailedAsync` is called when a step was started.
- **Writing empty/noise nodes:** When `extracted.Count == 0` (LIVM-04), the service must return without writing any nodes. Logging the skipped_reason satisfies auditability.
- **Storing raw transcript dumps:** The extraction prompt must explicitly instruct the LLM to extract stable facts/preferences/entities/learnings only — not summarize or dump the conversation.
- **Forgetting to snapshot-safe update:** `WriteNodeAsync` already handles this. Never call DELETE + INSERT to "update" — always use `WriteNodeAsync` with the same URI, which auto-snapshots.
- **Keyword format mismatch:** `MemoryNode.Keywords` must be a JSON array string (e.g., `["sqlite","dapper"]`), not a comma-separated string. The extraction LLM may return either format; normalize before writing.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Snapshot versioning on update | Custom snapshot logic in SedimentationService | `IMemoryGraph.WriteNodeAsync` | Already snapshots old content and prunes to 10 per node automatically |
| LLM client construction | Custom HTTP client wrapper | `OpenAI.ChatClient` with `OpenAIClientOptions` | Same pattern as `LLMModule.CompleteWithCustomClientAsync`; handles auth, retry headers |
| Provider config resolution | Custom config lookup | `IAnimaModuleConfigService.GetConfig(animaId, "Sedimentation")` + `LLMProviderRegistryService.GetDecryptedApiKey` | Established 3-layer precedence in LLMModule; follow same pattern |
| StepRecord for sedimentation | Custom audit table | `IStepRecorder.RecordStepStartAsync/CompleteAsync/FailedAsync` | Already wired in DI; provides run timeline entries, convergence guard integration, SignalR push |
| Glossary invalidation after write | Manual cache invalidation | `WriteNodeAsync` invalidates glossary cache automatically | `MemoryGraph.WriteNodeAsync` calls `_glossaryCache.TryRemove(animaId, out _)` on every write |
| URI uniqueness for sediment nodes | Collision detection table | Content-derived or UUID-based ID + `sediment://type/{id}` scheme | `WriteNodeAsync` upserts by (URI, AnimaId) — same URI = update, new URI = insert |

**Key insight:** Every core concern (snapshots, cache invalidation, observability, LLM connectivity) is already solved in the existing infrastructure. Phase 54 is primarily orchestration — calling the right existing methods in the right sequence.

---

## Common Pitfalls

### Pitfall 1: CancellationToken Leak into Background Task

**What goes wrong:** Passing the caller's `CancellationToken` into `Task.Run(() => SedimentAsync(..., ct))` causes the background task to be cancelled when the LLMModule's handler finishes (which may cancel the token).

**Why it happens:** The caller's `ct` represents the LLM call's lifecycle, not the sedimentation lifetime.

**How to avoid:** Always use `CancellationToken.None` for the background sedimentation task. Sedimentation is best-effort; it should run to completion independently.

**Warning signs:** `OperationCanceledException` in sedimentation logs with no apparent error cause.

---

### Pitfall 2: Closing Over Mutable LLMModule State in `Task.Run`

**What goes wrong:** The `messages` list and `result.Content` string are created inside `ExecuteWithMessagesListAsync`. If not captured by value into the lambda, the background task may observe modified state (e.g., if `messages` is reused or cleared in subsequent calls).

**Why it happens:** `Task.Run` launches asynchronously; the outer method may continue mutating shared variables before the background task reads them.

**How to avoid:** Snapshot all values before `Task.Run`: `var capturedMessages = new List<ChatMessageInput>(messages); var capturedResponse = result.Content;`

**Warning signs:** Sedimentation extracting empty or incorrect conversation content.

---

### Pitfall 3: BuiltInModuleDecouplingTests Failure

**What goes wrong:** Adding a new `using OpenAnima.Core.Memory;` reference to `LLMModule.cs` triggers `BuiltInModuleDecouplingTests` failure because the allowlist in `BuiltInModuleDecouplingTests.cs` hardcodes the exact set of allowed Core usings for `LLMModule.cs`.

**Why it happens:** `OpenAnima.Core.Memory` is already in the allowlist from Phase 52 (`IMemoryRecallService`). If `ISedimentationService` is placed in `OpenAnima.Core.Memory`, no new using is needed. But if placed elsewhere, the allowlist needs updating.

**How to avoid:** Place `ISedimentationService` in `OpenAnima.Core.Memory` namespace — the using is already permitted. No allowlist change required.

**Warning signs:** `BuiltInModuleDecouplingTests.NonLlmBuiltInModules_HaveNoCoreUsings_AndLlmModuleHasOnlyTheDocumentedException` fails after LLMModule modification.

---

### Pitfall 4: Keywords JSON Format Mismatch

**What goes wrong:** The extraction LLM may return keywords as a comma-separated string (`"sqlite,dapper,persistence"`) instead of a JSON array (`["sqlite","dapper","persistence"]`). Writing the comma-separated form directly into `MemoryNode.Keywords` breaks `GlossaryIndex.Build` parsing and causes `JsonException` warnings.

**Why it happens:** LLMs don't always follow JSON sub-field schemas precisely. The CONTEXT.md example shows keywords as a comma-separated string in the outer JSON, but `MemoryNode.Keywords` requires a JSON array.

**How to avoid:** Normalize keywords in `SedimentationService` before constructing `MemoryNode`: if value starts with `[`, treat as JSON array; otherwise, split on comma and serialize to JSON array.

**Warning signs:** `MemoryGraph` logs `"Failed to parse Keywords JSON for node {Uri}"` warnings after sedimentation.

---

### Pitfall 5: StepRecord Without Active Run

**What goes wrong:** `IStepRecorder.RecordStepStartAsync` returns `null` when no active run exists for the Anima (per `StepRecorder.cs` line 62: `if (context == null) return null`). Passing `null` stepId to `RecordStepCompleteAsync` is a no-op (per line 96: `if (stepId == null) return`). This is safe but means sedimentation produces no run timeline entry outside of an active run.

**Why it happens:** Sedimentation is triggered inside LLM calls, which can theoretically run outside a run context.

**How to avoid:** This is expected behavior — no special handling needed. Sedimentation still writes nodes; it just won't appear in the run timeline when no run is active. Document this in code comments.

**Warning signs:** Not actually an error. If silence is unexpected, confirm by checking `_runService.GetActiveRun(animaId) != null` before expecting StepRecord entries.

---

### Pitfall 6: GetAllNodesAsync Token Budget for Merge Context

**What goes wrong:** For an Anima with hundreds of memory nodes, `GetAllNodesAsync` returns the full list. Passing all nodes as context to the extraction LLM inflates token usage and may exceed the extraction model's context window.

**Why it happens:** The CONTEXT.md specifies ~200 chars per node for context, but doesn't cap the total node count.

**How to avoid:** Use `QueryByPrefixAsync(animaId, "sediment://")` to fetch only sediment nodes (more focused than all nodes). Cap at a token budget (e.g., 50 nodes × 200 chars = ~10K chars). Drop oldest if over limit. This is Claude's discretion per CONTEXT.md.

**Warning signs:** Extraction LLM returns errors about context length, or sedimentation becomes noticeably slow.

---

## Code Examples

### LLM Extraction Prompt (System + User Message)

```
// System message (Claude's discretion on exact wording):
You are a memory extraction assistant. Given a conversation between a user and an AI assistant,
extract stable, reusable knowledge: facts about the project, user preferences, named entities,
or task learnings. Do NOT summarize the conversation or store ephemeral exchanges.

Return a JSON object with this schema:
{
  "extracted": [
    {
      "action": "create" | "update",
      "uri": "sediment://fact/{id}" | "sediment://preference/{id}" | "sediment://entity/{id}" | "sediment://learning/{id}",
      "content": "single atomic knowledge statement",
      "keywords": "comma,separated,keywords",
      "disclosure_trigger": "single phrase that would trigger recall"
    }
  ],
  "skipped_reason": null | "explanation if nothing extracted"
}

Existing memories (for merge decisions):
{contextSummary}

// User message: the conversation to analyze
<conversation>
{messages serialized as role: content pairs}
</conversation>
```

### SedimentationService Skeleton (Claude's discretion on exact signatures)

```csharp
// Source: patterns from MemoryRecallService + LLMModule.CompleteWithCustomClientAsync
public class SedimentationService : ISedimentationService
{
    private readonly IMemoryGraph _memoryGraph;
    private readonly IStepRecorder? _stepRecorder;
    private readonly IAnimaModuleConfigService _configService;
    private readonly LLMProviderRegistryService _registryService;
    private readonly ILLMProviderRegistry _providerRegistry;
    private readonly ILogger<SedimentationService> _logger;

    public async Task SedimentAsync(
        string animaId,
        IReadOnlyList<ChatMessageInput> messages,
        string llmResponse,
        string? sourceStepId,
        CancellationToken ct)
    {
        var stepId = await _stepRecorder?.RecordStepStartAsync(
            animaId, "Sedimentation", $"Extracting from exchange (source: {sourceStepId})", null, ct);

        try
        {
            // 1. Build context: existing sediment:// nodes (truncated)
            var existingNodes = await _memoryGraph.QueryByPrefixAsync(animaId, "sediment://", ct);
            var contextSummary = BuildContextSummary(existingNodes);

            // 2. Call extraction LLM
            var extractionJson = await CallExtractionLlmAsync(animaId, messages, llmResponse, contextSummary, ct);

            // 3. Parse JSON
            var result = ParseExtractionResult(extractionJson);

            if (result.Extracted.Count == 0)
            {
                _logger.LogDebug("Sedimentation skipped for {AnimaId}: {Reason}", animaId, result.SkippedReason);
                await _stepRecorder?.RecordStepCompleteAsync(stepId, "Sedimentation",
                    $"Skipped: {result.SkippedReason}", ct);
                return;
            }

            // 4. Write each node (WriteNodeAsync handles snapshot/upsert)
            var writtenUris = new List<string>();
            foreach (var item in result.Extracted)
            {
                var node = BuildMemoryNode(animaId, item, sourceStepId);
                await _memoryGraph.WriteNodeAsync(node, ct);
                writtenUris.Add(item.Uri);
            }

            await _stepRecorder?.RecordStepCompleteAsync(stepId, "Sedimentation",
                $"{writtenUris.Count} nodes: {string.Join(", ", writtenUris)}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sedimentation failed for Anima {AnimaId}", animaId);
            if (stepId != null)
                await _stepRecorder?.RecordStepFailedAsync(stepId, "Sedimentation", ex, ct);
        }
    }
}
```

### DI Registration Pattern

```csharp
// Source: RunServiceExtensions.cs — add after IMemoryRecallService registration
services.AddSingleton<ISedimentationService, SedimentationService>();
```

### LLMModule Integration Point

```csharp
// Source: LLMModule.cs constructor pattern (optional dependency)
// Constructor parameter:
ISedimentationService? sedimentationService = null

// In ExecuteWithMessagesListAsync, after PublishResponseAsync + before method returns:
if (_sedimentationService != null)
{
    var capturedAnimaId = animaId;
    var capturedMessages = new List<ChatMessageInput>(messages);
    var capturedResponse = result.Content!;
    // sourceStepId: the StepRecord ID recorded for the LLM call itself (if step recording is active)

    _ = Task.Run(async () =>
    {
        try { await _sedimentationService.SedimentAsync(
            capturedAnimaId, capturedMessages, capturedResponse, null, CancellationToken.None); }
        catch (Exception ex)
        { _logger.LogWarning(ex, "Background sedimentation threw for {AnimaId}", capturedAnimaId); }
    }, CancellationToken.None);
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Manual memory writes only (MemoryWriteTool) | Automatic extraction via secondary LLM call | Phase 54 (this phase) | Reduces agent burden; creates self-maintaining memory graph |
| All memory equally available on recall | `sediment://` prefix distinguishes auto-sedimented from manual | Phase 54 | Enables future policy controls; clear provenance of origin |
| WriteNodeAsync silently overwrites | WriteNodeAsync auto-snapshots before overwriting | Since Phase 52 | History tracking free from sedimentation's perspective |
| Single LLM config per Anima | Separate sedimentation LLM config (cheaper model) | Phase 54 | Cost optimization; extraction can use smaller/faster model |

---

## Open Questions

1. **Source Step ID availability in LLMModule**
   - What we know: `IStepRecorder.RecordStepStartAsync` returns a stepId string (8-char hex). LLMModule currently does not record a StepRecord for its own LLM calls (only for MemoryRecall steps).
   - What's unclear: Should sedimentation's `SourceStepId` reference a new per-LLM-call StepRecord, or is null acceptable?
   - Recommendation: Null is acceptable for LIVM-02 compliance (provenance to run is provided via RunId context). Optionally, LLMModule can record a "LLMCall" StepRecord and pass that ID to sedimentation. This is Claude's discretion.

2. **Configuration key namespace for sedimentation LLM**
   - What we know: LLMModule uses `llmProviderSlug` + `llmModelId` per the Phase 51 schema. Sedimentation needs its own provider/model config.
   - What's unclear: Exact config key names and whether a new `IModuleConfigSchema` is needed for sedimentation.
   - Recommendation: Use `sedimentProviderSlug` + `sedimentModelId` config keys under a `"Sedimentation"` module ID. Follows `GetConfig(animaId, "Sedimentation")` pattern. Schema registration is Claude's discretion.

3. **Keywords format from extraction LLM**
   - What we know: `MemoryNode.Keywords` must be a JSON array. CONTEXT.md example shows comma-separated in the outer extraction JSON.
   - What's unclear: Whether the LLM will reliably return comma-separated or JSON array format.
   - Recommendation: Accept both formats in `SedimentationService` and normalize to JSON array before constructing `MemoryNode`. See Pitfall 4.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none (tests auto-discover) |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ -c Release --filter "FullyQualifiedName~Sedimentation"` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/ -c Release` |

**Current suite baseline:** 565 tests passing (verified 2026-03-22).

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LIVM-01 | SedimentationService extracts facts, preferences, entities, learnings from conversation | unit | `dotnet test tests/OpenAnima.Tests/ -c Release --filter "FullyQualifiedName~SedimentationService"` | ❌ Wave 0 |
| LIVM-01 | No extraction when LLM returns empty extracted array with skipped_reason | unit | `dotnet test tests/OpenAnima.Tests/ -c Release --filter "FullyQualifiedName~SedimentationService"` | ❌ Wave 0 |
| LIVM-02 | Written MemoryNode.SourceStepId is populated from trigger step | unit | `dotnet test tests/OpenAnima.Tests/ -c Release --filter "FullyQualifiedName~SedimentationService"` | ❌ Wave 0 |
| LIVM-02 | StepRecord created in run timeline for sedimentation | unit | `dotnet test tests/OpenAnima.Tests/ -c Release --filter "FullyQualifiedName~SedimentationService"` | ❌ Wave 0 |
| LIVM-03 | Update to existing sediment:// node produces snapshot in memory_snapshots | unit | `dotnet test tests/OpenAnima.Tests/ -c Release --filter "FullyQualifiedName~SedimentationService"` | ❌ Wave 0 |
| LIVM-04 | Empty extraction (skipped_reason set) writes no nodes | unit | `dotnet test tests/OpenAnima.Tests/ -c Release --filter "FullyQualifiedName~SedimentationService"` | ❌ Wave 0 |
| LIVM-04 | LLM failure during extraction produces warning log, no exception propagation | unit | `dotnet test tests/OpenAnima.Tests/ -c Release --filter "FullyQualifiedName~SedimentationService"` | ❌ Wave 0 |
| LIVM-01 | LLMModule wiring: sedimentation fires after response is published | unit | `dotnet test tests/OpenAnima.Tests/ -c Release --filter "FullyQualifiedName~LLMModuleSedimentation"` | ❌ Wave 0 |
| Integration | BuiltInModuleDecouplingTests still passes after LLMModule changes | integration | `dotnet test tests/OpenAnima.Tests/ -c Release --filter "Category=Integration"` | ✅ (existing, will verify) |

### Sampling Rate

- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ -c Release --filter "FullyQualifiedName~Sedimentation"`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/ -c Release`
- **Phase gate:** Full suite green (565+ tests) before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `tests/OpenAnima.Tests/Unit/SedimentationServiceTests.cs` — covers LIVM-01, LIVM-02, LIVM-03, LIVM-04; uses `FakeMemoryGraph` (existing) + `FakeLlmClient` (new or inline stub)
- [ ] `tests/OpenAnima.Tests/Unit/LLMModuleSedimentationTests.cs` — covers LLMModule wiring (fire-and-forget trigger, no blocking); uses existing `FakeMemoryGraph` + new `FakeSedimentationService` stub

---

## Sources

### Primary (HIGH confidence)
- `src/OpenAnima.Core/Memory/IMemoryGraph.cs` — Complete interface: WriteNodeAsync (auto-snapshot), QueryByPrefixAsync, GetAllNodesAsync
- `src/OpenAnima.Core/Memory/MemoryGraph.cs` — WriteNodeAsync implementation: confirms auto-snapshot + 10-snapshot prune + glossary cache invalidation
- `src/OpenAnima.Core/Memory/MemoryNode.cs` — Record schema: SourceStepId, SourceArtifactId, Keywords (JSON array), DisclosureTrigger
- `src/OpenAnima.Core/Modules/LLMModule.cs` — Integration point: ExecuteWithMessagesListAsync structure; CompleteWithCustomClientAsync pattern; optional dependency injection pattern for IMemoryRecallService
- `src/OpenAnima.Core/Modules/HeartbeatModule.cs` — Fire-and-forget `Task.Run` pattern with CTS
- `src/OpenAnima.Core/Runs/StepRecorder.cs` — StepRecord lifecycle: RecordStepStartAsync returns null if no active run (safe no-op); RecordStepFailedAsync for error path
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` — DI registration pattern: singleton services, IMemoryRecallService registration precedent
- `src/OpenAnima.Core/Providers/LLMProviderRegistryService.cs` — GetDecryptedApiKey for sedimentation LLM config resolution
- `.planning/phases/54-living-memory-sedimentation/54-CONTEXT.md` — All locked decisions, canonical JSON schema, deferred items

### Secondary (MEDIUM confidence)
- `src/OpenAnima.Core/Tools/MemoryWriteTool.cs` — WriteNodeAsync usage pattern with SourceStepId/SourceArtifactId fields
- `tests/OpenAnima.Tests/Unit/MemoryToolPhase53Tests.cs` — In-memory SQLite test fixture pattern (shared keepalive connection, FakeMemoryGraph not needed for write tests)
- `tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs` — LLMModule allowlist for Core usings; `OpenAnima.Core.Memory` already permitted

### Tertiary (LOW confidence)
- None. All findings verified from authoritative source code.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all dependencies verified in project files
- Architecture: HIGH — all patterns traced to existing code with file + line references
- Pitfalls: HIGH — traced from actual code behavior (StepRecorder null return, MemoryGraph snapshot logic, BuiltInModuleDecouplingTests allowlist)
- Test infrastructure: HIGH — test suite confirmed running at 565 passing (2026-03-22)

**Research date:** 2026-03-22
**Valid until:** 2026-04-22 (stable stack, no fast-moving dependencies)
