# Phase 60: Hardening and Memory Integration - Research

**Researched:** 2026-03-23
**Domain:** C# agent loop hardening — step recording, token budget management, sedimentation wiring
**Confidence:** HIGH

## Summary

Phase 60 is an internal hardening pass with three independent modifications to `RunAgentLoopAsync` in `LLMModule.cs`. All required infrastructure already exists in the codebase: `IStepRecorder` (with full PropagationId carry-through), `TokenCounter` (SharpToken wrapper), and `ISedimentationService` (with `TriggerSedimentation` fire-and-forget). No new services, interfaces, or external packages are required.

The three changes are surgically scoped: (1) wrap each loop iteration in RecordStepStart/Complete brackets plus an outer AgentLoop bracket, (2) count tokens before each `CallLlmAsync` and drop oldest assistant+tool pairs when over budget, (3) change the two `TriggerSedimentation` call sites to pass `history` instead of the original `messages`. Each change is isolated and carries zero risk of breaking the others.

The UI-SPEC (already approved) constrains how bracket steps surface in `StepTimelineRow.razor` — CSS class additions driven purely by `ModuleName` values ("AgentLoop", "AgentIteration #N"). The rendering layer needs no new component, only new CSS classes.

**Primary recommendation:** Implement in three separable waves: HARD-03 (bracket steps) first because it is the most testable standalone, then HARD-02 (token budget), then HARD-01 (sedimentation wiring) which is a one-line change per call site.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### StepRecorder Iteration Brackets (HARD-03)
- Parent-child step model: each agent iteration records a parent step "AgentIteration #N" with child steps for individual tool calls within that iteration
- Outer "AgentLoop" bracket step wraps all iterations — records total duration and iteration count in outputSummary
- Parent step inputSummary shows the first 200 characters of the LLM response for that iteration (including tool_call markers)
- Child tool call steps use the iteration's PropagationId to associate with their parent
- StepRecorder.RecordStepStartAsync called at iteration start, RecordStepCompleteAsync at iteration end (after all tool calls dispatched)

#### Token Budget Management (HARD-02)
- Before each LLM re-call in RunAgentLoopAsync, count tokens in accumulated history using SharpToken (cl100k_base)
- When token count exceeds 70% of agentContextWindowSize, drop the oldest assistant+tool message pairs from history (preserving system message + original user messages)
- New config key: `agentContextWindowSize` (int, default 128000, added to LLMModule GetSchema() in "agent" group)
- After truncation, insert a system message: "[Earlier tool results were trimmed to fit context window]" so the LLM is aware of trimmed history
- Token counting uses the same SharpToken already in the project (CTX-01 from v1.2)

#### Sedimentation Full History (HARD-01)
- TriggerSedimentation in RunAgentLoopAsync passes the complete accumulated history list (all user/assistant/tool role messages), not just the original messages + final response
- When the complete history is too long for the sedimentation LLM context window, truncate tool result message content to first 500 characters each, preserving the full message structure and reasoning chain
- ISedimentationService.SedimentAsync signature unchanged — the existing `IReadOnlyList<ChatMessageInput> messages` parameter receives the full expanded list, and `llmResponse` receives the final clean response

### Claude's Discretion
- Exact PropagationId format for linking parent/child iteration steps
- Whether to add a new IStepRecorder method for bracket steps or reuse existing start/complete pattern
- Token counting batch size (count every iteration vs. periodic check)
- Exact position of truncation notice in message list

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| HARD-01 | Sedimentation service receives full conversation history including all tool call turns | TriggerSedimentation already snapshots messages before Task.Run; changing `messages` → `history` at both call sites is a two-line change. BuildExtractionMessages iterates any IReadOnlyList — accepts tool role messages without modification. |
| HARD-02 | Token budget check before each LLM re-call; truncates oldest tool results when exceeding 70% of context window | TokenCounter.CountMessages already exists and handles IReadOnlyList<ChatMessageInput>. agentContextWindowSize config field follows the identical pattern as agentMaxIterations (Int, "agent" group). Truncation loop targets assistant+tool pairs — pairs always appear consecutively in history. |
| HARD-03 | Agent loop records bracket steps per iteration in StepRecorder, visible in Run inspector | IStepRecorder.RecordStepStartAsync/RecordStepCompleteAsync already support arbitrary moduleName strings and PropagationId. StepRecorder ConcurrentDictionary tracking handles concurrent bracket steps correctly. UI-SPEC already approved — CSS-only change in StepTimelineRow.razor. |
</phase_requirements>

## Standard Stack

### Core — Already in Project

| Library | Version | Purpose | Notes |
|---------|---------|---------|-------|
| SharpToken (via `TokenCounter`) | existing | Token counting for budget check | `TokenCounter.CountMessages(IReadOnlyList<ChatMessageInput>)` already implemented in `src/OpenAnima.Core/LLM/TokenCounter.cs` |
| `IStepRecorder` | existing | Step bracket recording | Full PropagationId carry-through verified in `StepRecorderPropagationTests` |
| `ISedimentationService` | existing | Memory extraction from conversation | Signature unchanged; `BuildExtractionMessages` accepts any role including "tool" |

### No New Packages Required

All three requirements are satisfied entirely with existing infrastructure. No NuGet additions needed.

**Version verification:** Not applicable — no new packages.

## Architecture Patterns

### Key Files and Their Roles

```
src/OpenAnima.Core/
├── Modules/LLMModule.cs         # PRIMARY — RunAgentLoopAsync, TriggerSedimentation, GetSchema
├── LLM/TokenCounter.cs          # Reuse CountMessages() for HARD-02 budget check
├── Runs/IStepRecorder.cs        # RecordStepStartAsync / RecordStepCompleteAsync contract
├── Runs/StepRecorder.cs         # ConcurrentDictionary PropagationId tracking
├── Memory/SedimentationService.cs   # BuildExtractionMessages — iterates messages by role
├── Memory/ISedimentationService.cs  # SedimentAsync signature (unchanged)
└── Components/Shared/
    └── StepTimelineRow.razor    # CSS class additions for agent-loop-bracket, agent-iteration
```

### Pattern 1: StepRecorder Start/Complete Pair (HARD-03)

The existing MemoryRecall step in LLMModule demonstrates the exact pattern to replicate for iteration brackets:

```csharp
// Existing pattern (from ExecuteWithMessagesListAsync, lines 303-307):
var stepId = await _stepRecorder.RecordStepStartAsync(
    animaId, "MemoryRecall", summary, propagationId: null, ct);
await _stepRecorder.RecordStepCompleteAsync(
    stepId, "MemoryRecall", $"{count} nodes recalled", ct);
```

The outer AgentLoop bracket and per-iteration AgentIteration brackets follow this same pattern:

```csharp
// Outer bracket — wraps the full loop
var loopStepId = await _stepRecorder!.RecordStepStartAsync(
    animaId, "AgentLoop", $"Starting agent loop (max {maxIterations} iterations)", null, ct);

// Per-iteration bracket inside the for loop
var iterStepId = await _stepRecorder!.RecordStepStartAsync(
    animaId, $"AgentIteration #{iteration + 1}",
    result.Content[..Math.Min(200, result.Content.Length)],
    loopStepId,  // PropagationId links child to parent bracket
    ct);

// ... dispatch tool calls (each tool call step uses iterStepId as PropagationId) ...

await _stepRecorder!.RecordStepCompleteAsync(
    iterStepId, $"AgentIteration #{iteration + 1}",
    $"{parsed.ToolCalls.Count} tool calls dispatched", ct);

// After loop exits:
await _stepRecorder!.RecordStepCompleteAsync(
    loopStepId, "AgentLoop",
    $"{completedIterations} iterations completed in {elapsed}ms", ct);
```

**PropagationId design:** Using the parent step's stepId as the PropagationId for child steps is the correct pattern. `RecordStepStartAsync` returns the 8-char stepId (generated by `Guid.NewGuid().ToString("N")[..8]`). Child steps pass this as `propagationId` — the ConcurrentDictionary in `StepRecorder` stores it for carry-through to completion records.

### Pattern 2: Token Budget Truncation Loop (HARD-02)

```csharp
// TokenCounter is constructed with a model name; use cl100k_base fallback
var tokenCounter = new TokenCounter("cl100k_base");

// Before each CallLlmAsync in the loop:
var agentContextWindowSize = ReadAgentContextWindowSize(animaId);  // new config reader
var budget = (int)(agentContextWindowSize * 0.70);
var currentTokens = tokenCounter.CountMessages(history);

if (currentTokens > budget)
{
    // Find system messages boundary — preserve all system messages at index 0
    // Find original user messages — preserve those too
    // Drop oldest assistant+tool pairs from middle of history
    TruncateHistory(history, budget, tokenCounter);
}
```

**Truncation boundary logic:** The history structure after one iteration is:
- `[0]` system message (tool syntax block)
- `[1]` original user message(s)
- `[2]` assistant message with tool_call markers (iteration 1)
- `[3]` tool role message with results (iteration 1)
- `[4]` assistant message (iteration 2)
- `[5]` tool role message (iteration 2)
- ...

Pairs are always at consecutive even/odd indices starting from index 2. Drop from the earliest pair (index 2+3) first. After dropping, insert the truncation notice system message at position after the last original user message (preserves context for LLM).

**Config reader pattern** (follows ReadAgentMaxIterations exactly):
```csharp
private int ReadAgentContextWindowSize(string animaId)
{
    var config = _configService.GetConfig(animaId, Metadata.Name);
    if (config.TryGetValue("agentContextWindowSize", out var sizeStr) &&
        int.TryParse(sizeStr, out var sizeVal) && sizeVal > 0)
        return sizeVal;
    return 128000; // default
}
```

**Schema field** (added to GetSchema() in "agent" group after agentMaxIterations at Order 21):
```csharp
new ConfigFieldDescriptor(
    Key: "agentContextWindowSize",
    Type: ConfigFieldType.Int,
    DisplayName: "Agent Context Window Size",
    DefaultValue: "128000",
    Description: "Maximum tokens for agent loop history. Tool results are trimmed when usage exceeds 70% of this limit.",
    EnumValues: null,
    Group: "agent",
    Order: 22,
    Required: false,
    ValidationPattern: null),
```

### Pattern 3: TriggerSedimentation Call Site Change (HARD-01)

Two call sites in `RunAgentLoopAsync` (lines 920 and 925):

```csharp
// BEFORE (passes original messages — only system+user, no tool turns):
TriggerSedimentation(animaId, messages, detection.PassthroughText);
TriggerSedimentation(animaId, messages, responseText);

// AFTER (passes full accumulated history):
TriggerSedimentation(animaId, history, detection.PassthroughText);
TriggerSedimentation(animaId, history, responseText);
```

`TriggerSedimentation` already snapshots the list before `Task.Run` — passing `history` is safe. The `BuildExtractionMessages` in `SedimentationService` iterates messages by role using a simple foreach, so "tool" role messages appear in the `<conversation>` XML block naturally.

**Sedimentation tool result truncation:** The CONTEXT.md decision specifies truncating tool result message content to first 500 characters when history is long. This truncation happens in `TriggerSedimentation` before the snapshot, or inside `SedimentationService.BuildExtractionMessages`. The safest place is inside `BuildExtractionMessages` — it already has 200-char truncation for context summary nodes. Add a parallel truncation: when building the conversation XML, if a message has role "tool", truncate its content to 500 chars.

### Pattern 4: UI CSS Addition (HARD-03 UI surface)

Per the approved UI-SPEC, `StepTimelineRow.razor` (or its CSS file) needs three new CSS classes driven by `ModuleName`:

```css
/* AgentLoop outer bracket row */
.step-row.agent-loop-bracket {
    background: var(--surface-dark);
    border-top: 1px solid var(--border-color);
    border-bottom: 1px solid var(--border-color);
}

/* AgentIteration #N parent row */
.step-row.agent-iteration {
    padding-left: 24px;
    background: var(--surface-card);
    border-left-style: dashed;
}

/* Child tool call rows (by PropagationId chain) */
.step-row.agent-tool-call {
    padding-left: 40px;
    background: var(--bg-primary);
}
```

Class assignment in `StepTimelineRow.razor` is driven by `ModuleName`:
- `"AgentLoop"` → `agent-loop-bracket`
- starts with `"AgentIteration"` → `agent-iteration`
- PropagationId matches an AgentIteration's stepId → `agent-tool-call`

### Anti-Patterns to Avoid

- **Dropping system messages:** Never drop index 0 (system message with tool syntax). Always preserve all system messages and original user messages.
- **Mismatched bracket close:** If a loop iteration throws, the outer AgentLoop bracket step and the current iteration bracket step will be left open (Running status). Add try/catch in the loop to call `RecordStepFailedAsync` on both stepIds.
- **Closure over mutable `history` in TriggerSedimentation:** `TriggerSedimentation` already copies `messages` to `capturedMessages = new List<>(messages)` before `Task.Run`. When passing `history`, the snapshot still happens — but `history` continues to be mutated in subsequent iterations. The snapshot must be taken at the point of the call (before subsequent loop iterations), which the current `TriggerSedimentation` implementation handles correctly.
- **TokenCounter allocation per-iteration:** Allocating `new TokenCounter(...)` per iteration is wasteful since it triggers lazy SharpToken encoding initialization. Allocate once at the start of `RunAgentLoopAsync`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Token counting | Custom character/word estimator | `TokenCounter.CountMessages()` | Already accurate per cl100k_base; SharpToken handles per-message overhead |
| PropagationId generation | New UUID scheme | Return value of `RecordStepStartAsync` (8-char hex) | StepRecorder generates this internally — simply pass it as the child's `propagationId` |
| Sedimentation truncation | New ISedimentationService overload | Inline truncation in `BuildExtractionMessages` for "tool" role | ISedimentationService signature stays unchanged per CONTEXT.md decision |

**Key insight:** Every building block for Phase 60 already exists. The phase is about wiring them together correctly, not building new infrastructure.

## Common Pitfalls

### Pitfall 1: Open Bracket Steps on Cancellation
**What goes wrong:** If `ct.ThrowIfCancellationRequested()` fires mid-iteration, the AgentLoop and AgentIteration bracket steps have status "Running" forever.
**Why it happens:** `OperationCanceledException` propagates up before `RecordStepCompleteAsync` is called.
**How to avoid:** Wrap the loop body in try/catch/finally. In the `finally` block, call `RecordStepCompleteAsync` (or `RecordStepFailedAsync`) for any open bracket step IDs.
**Warning signs:** Run inspector timeline shows "Running" status permanently for AgentIteration rows after cancellation.

### Pitfall 2: Token Count Includes Already-Trimmed History Overhead
**What goes wrong:** Inserting the truncation notice system message *after* counting tokens means the check may not fire again even though the notice added tokens.
**Why it happens:** The check runs once per iteration before `CallLlmAsync`, not after the notice insertion.
**How to avoid:** Insert the truncation notice only once (track a `bool truncationNoticeInserted` flag). Do not re-count after insertion — the notice is short (~10 tokens).

### Pitfall 3: History Snapshot Timing for Sedimentation
**What goes wrong:** Sedimentation receives a snapshot of `history` at the *end* of the last iteration, which is correct — but only if the snapshot is taken before the loop exits.
**Why it happens:** `TriggerSedimentation` is called after the loop, but `history` may contain the final LLM response assistant message or not, depending on when the snapshot is taken.
**How to avoid:** The existing `TriggerSedimentation` takes a `new List<>(messages)` snapshot inside the method. Pass `history` (which at the call point contains all accumulated messages through the final response) — this is the correct and complete set.

### Pitfall 4: agentContextWindowSize = 0 or Very Small
**What goes wrong:** If a user sets `agentContextWindowSize` to a very small value (e.g., 100), every iteration triggers truncation and the agent loses context immediately.
**Why it happens:** No minimum validation on the config value.
**How to avoid:** In `ReadAgentContextWindowSize`, enforce a minimum (e.g., `Math.Max(sizeVal, 1000)`) or clamp to a reasonable floor.

### Pitfall 5: Sedimentation Receives tool Role Messages It Doesn't Recognize
**What goes wrong:** `BuildExtractionMessages` builds `<conversation>` XML with `msg.Role: msg.Content` — "tool" role appears as `tool: [raw JSON or long output]`.
**Why it happens:** The method iterates all messages without special handling for "tool" role.
**How to avoid:** Per CONTEXT.md decision, truncate tool message content to first 500 characters in `BuildExtractionMessages`. This keeps the XML readable for the sedimentation LLM.

## Code Examples

### RecordStepStartAsync Signature (verified from IStepRecorder.cs)
```csharp
Task<string?> RecordStepStartAsync(
    string animaId,
    string moduleName,
    string? inputSummary,
    string? propagationId,
    CancellationToken ct = default);
```
Returns `null` when no active run — all bracket step code must null-check the returned stepId.

### RecordStepCompleteAsync Signature (verified from IStepRecorder.cs)
```csharp
Task RecordStepCompleteAsync(
    string? stepId,
    string moduleName,
    string? outputSummary,
    CancellationToken ct = default);
```
Null `stepId` is a no-op — safe to call even if `RecordStepStartAsync` returned null.

### TokenCounter.CountMessages Signature (verified from TokenCounter.cs)
```csharp
public int CountMessages(IReadOnlyList<ChatMessageInput> messages)
```
Returns total tokens including 3-token-per-message overhead and 3-token assistant priming. Handles empty list correctly (returns 3 for assistant priming only).

### TriggerSedimentation Snapshot Pattern (verified from LLMModule.cs lines 945-968)
```csharp
private void TriggerSedimentation(string animaId, List<ChatMessageInput> messages, string response)
{
    if (_sedimentationService == null) return;
    var capturedAnimaId = animaId;
    var capturedMessages = new List<ChatMessageInput>(messages); // snapshot here
    var capturedResponse = response;
    _ = Task.Run(async () => { ... }, CancellationToken.None);
}
```
The snapshot is taken synchronously in `TriggerSedimentation` — passing `history` at the loop's end captures the full accumulated state.

### History Structure After Two Iterations
```
index 0: system  "...tool syntax block..."
index 1: user    "user's original message"
index 2: assistant "<tool_call name='x'>...</tool_call>"   (iteration 1 LLM response)
index 3: tool    "<tool_result>...</tool_result>"           (iteration 1 tool results)
index 4: assistant "<tool_call name='y'>...</tool_call>"   (iteration 2 LLM response)
index 5: tool    "<tool_result>...</tool_result>"           (iteration 2 tool results)
```
When truncating: drop pairs from index 2+3 forward. Preserve index 0 (system) and index 1 (user).

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|------------------|--------|
| Pass original messages to sedimentation | Pass full history including tool turns | Memory graph reflects agent reasoning, not just final answer |
| No token budget check | Budget check before each re-call, 70% threshold | Loop never sends oversized context |
| No step brackets for agent loop | AgentLoop + AgentIteration bracket hierarchy | Run inspector shows iteration granularity |

## Open Questions

1. **Where exactly to insert the truncation notice system message**
   - What we know: after oldest pairs are dropped, a notice must inform the LLM that history was truncated
   - What's unclear: whether to insert immediately after system message(s) at index 1, or immediately before the next assistant message
   - Recommendation: insert at index 1 (after the system message at 0, before original user messages at 1+) — this keeps system prompts first and user messages together

2. **TokenCounter model name in agent loop context**
   - What we know: `TokenCounter` constructor takes a model name and falls back to `cl100k_base` for unknowns
   - What's unclear: whether to read the actual configured model name for counting vs. always using cl100k_base
   - Recommendation: use `new TokenCounter("cl100k_base")` directly — the CONTEXT.md decision specifies cl100k_base and it avoids a config read just for token counting

3. **Cancellation during AgentLoop bracket step**
   - What we know: LOOP-07 (Phase 58) covers cancellation releasing the semaphore and closing StepRecorder
   - What's unclear: the Phase 58 implementation detail for how bracket steps are closed on cancellation
   - Recommendation: use try/finally in the loop to guarantee bracket step closure regardless of exit path

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none — standard `dotnet test` discovery |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --filter "Category=AgentLoop|AgentHardening" -x` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| HARD-01 | Sedimentation receives full history (user+assistant+tool roles) | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModuleAgentLoopHardeningTests" -x` | ❌ Wave 0 |
| HARD-01 | Tool message content truncated to 500 chars in sedimentation | unit | same file | ❌ Wave 0 |
| HARD-02 | Token count check before each LLM call | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModuleAgentLoopHardeningTests" -x` | ❌ Wave 0 |
| HARD-02 | Oldest assistant+tool pairs dropped when over 70% budget | unit | same file | ❌ Wave 0 |
| HARD-02 | Truncation notice inserted after dropping pairs | unit | same file | ❌ Wave 0 |
| HARD-02 | agentContextWindowSize config field in GetSchema() "agent" group | unit | same file | ❌ Wave 0 |
| HARD-03 | AgentLoop bracket step recorded (start + complete) | unit | same file | ❌ Wave 0 |
| HARD-03 | AgentIteration #N bracket step per iteration, inputSummary = first 200 chars of LLM response | unit | same file | ❌ Wave 0 |
| HARD-03 | Child tool call step PropagationId matches parent iteration stepId | unit | same file | ❌ Wave 0 |
| HARD-03 | Bracket steps closed on loop completion (not left Running) | unit | same file | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~AgentLoop" -x`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/LLMModuleAgentLoopHardeningTests.cs` — covers HARD-01, HARD-02, HARD-03
  - Needs: spy `IStepRecorder` that captures RecordStepStart/Complete calls with moduleName and propagationId
  - Needs: spy `ISedimentationService` that captures the messages list (already exists as `FakeSedimentationService` in `LLMModuleSedimentationTests.cs` — reuse or copy)
  - Needs: `AgentConfigService` extended with `agentContextWindowSize` key (or new config service variant)

*(Existing test infrastructure: xUnit, NullLogger, FakeModuleContext, NullLLMProviderRegistry — all in TestHelpers/. No new framework or fixture files needed.)*

## Sources

### Primary (HIGH confidence)
- `src/OpenAnima.Core/Modules/LLMModule.cs` — RunAgentLoopAsync (lines 836-931), TriggerSedimentation (lines 945-968), GetSchema() (lines 97-176)
- `src/OpenAnima.Core/Runs/IStepRecorder.cs` — full interface contract
- `src/OpenAnima.Core/Runs/StepRecorder.cs` — ConcurrentDictionary PropagationId tracking (lines 37, 71-77)
- `src/OpenAnima.Core/LLM/TokenCounter.cs` — CountMessages signature and implementation
- `src/OpenAnima.Core/Memory/SedimentationService.cs` — BuildExtractionMessages (lines 196-217), SedimentAsync (lines 83-175)
- `src/OpenAnima.Core/Memory/ISedimentationService.cs` — SedimentAsync signature (unchanged)
- `.planning/phases/60-hardening-and-memory-integration/60-CONTEXT.md` — all locked decisions
- `.planning/phases/60-hardening-and-memory-integration/60-UI-SPEC.md` — CSS class contract for bracket rows

### Secondary (MEDIUM confidence)
- `tests/OpenAnima.Tests/Unit/LLMModuleAgentLoopTests.cs` — existing test patterns, SequenceLlmService spy, AgentConfigService fake
- `tests/OpenAnima.Tests/Unit/LLMModuleSedimentationTests.cs` — FakeSedimentationService pattern (reusable in new test file)
- `tests/OpenAnima.Tests/Unit/StepRecorderPropagationTests.cs` — PropagationId carry-through verified

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all infrastructure is existing, verified by reading source files directly
- Architecture: HIGH — patterns extracted from existing working code; no speculation
- Pitfalls: HIGH — derived from code structure analysis (cancellation paths, closure semantics, null stepId handling)

**Research date:** 2026-03-23
**Valid until:** 2026-04-23 (stable codebase; no external dependencies)
