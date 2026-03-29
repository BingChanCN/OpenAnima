# Phase 67: Memory Tools & Sedimentation - Research

**Researched:** 2026-03-29
**Domain:** Memory CRUD tools (agent-invoked), EventBus event publishing, bilingual sedimentation quality
**Confidence:** HIGH

## Summary

Phase 67 transforms the existing memory tool surface (`memory_write`, `memory_query`, `memory_delete`) into the canonical `memory_create`, `memory_update`, `memory_delete`, `memory_list` quartet specified by the requirements, adds soft-delete semantics (deprecated flag rather than hard delete), wires all tool operations through `MemoryOperationPayload` EventBus events, and upgrades the sedimentation prompt to produce bilingual (Chinese + English) keywords with broader trigger conditions. The phase builds on Phase 65's four-table schema (`memory_nodes`, `memory_contents`, `memory_edges`, `memory_uri_paths`) and reuses the IWorkspaceTool / AgentToolDispatcher dispatch pattern established in Phases 46/53.

The current codebase already has `MemoryWriteTool`, `MemoryQueryTool`, `MemoryDeleteTool`, `MemoryRecallTool`, and `MemoryLinkTool` registered as `IWorkspaceTool` singletons. Phase 67 replaces `MemoryWriteTool` with two distinct tools (`MemoryCreateTool` and `MemoryUpdateTool`), replaces `MemoryQueryTool` with `MemoryListTool` (prefix-based listing, not query), and changes `MemoryDeleteTool` from hard delete to soft delete. All four tools publish `MemoryOperationPayload` events so Phase 68 can display them as tool cards.

**Primary recommendation:** Rename and refactor the existing tool classes to match the `memory_create` / `memory_update` / `memory_delete` / `memory_list` naming convention. Add a `deprecated` column to `memory_nodes` via ALTER TABLE migration. Replace the SedimentationService prompt with a bilingual extraction prompt that caps input at 20 messages and returns both Chinese and English keywords.

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MEMT-01 | Agent can create new memory nodes via memory_create tool with specified path, content, and keywords | New `MemoryCreateTool` class implementing IWorkspaceTool, calls `IMemoryGraph.WriteNodeAsync` for new nodes only |
| MEMT-02 | Agent can update existing memory node content via memory_update tool | New `MemoryUpdateTool` class that resolves node by UUID or URI, verifies existence before calling `WriteNodeAsync` (which appends content version) |
| MEMT-03 | Agent can soft-delete memory nodes via memory_delete tool (deprecated flag, recoverable) | Modify `MemoryDeleteTool` to SET `deprecated = 1` instead of DELETE cascade; add `deprecated` column to `memory_nodes` |
| MEMT-04 | Agent can list memory nodes by prefix via memory_list tool | New `MemoryListTool` class using `IMemoryGraph.QueryByPrefixAsync`, filtering out deprecated nodes |
| MEMT-05 | All memory tools publish MemoryOperationPayload events for downstream visibility | New `MemoryOperationPayload` event record + `IEventBus.PublishAsync` call in each tool's ExecuteAsync |
| MEMS-01 | Sedimentation prompt generates bilingual (Chinese + English) keywords | Updated ExtractionSystemPrompt with explicit bilingual keyword instruction |
| MEMS-02 | Sedimentation prompt generates broader trigger conditions (introduction + query scenarios) | Updated prompt instructions requiring multi-scenario triggers |
| MEMS-03 | Sedimentation input capped at last 20 messages to control cost and focus | `messages.TakeLast(20)` applied in `SedimentAsync` before building extraction prompt |
</phase_requirements>

---

## Standard Stack

### Core (Existing - No New Dependencies)
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| IWorkspaceTool | Production | Tool contract for agent-dispatchable operations | Phase 46/53 proven pattern; AgentToolDispatcher picks up via DI |
| IMemoryGraph | Production | Memory CRUD persistence layer | Phase 65 four-table schema; WriteNodeAsync, QueryByPrefixAsync, DeleteNodeAsync already exist |
| IEventBus | Production | Event publishing for ModuleEvent<TPayload> | Phase 42 infrastructure; LLMModule already publishes ToolCallStartedPayload/ToolCallCompletedPayload |
| SedimentationService | Production | LLM-based knowledge extraction from conversations | Phase 54 established; needs prompt modification, not architecture change |
| AgentToolDispatcher | Production | Dispatches tool calls from LLM response to IWorkspaceTool | Phase 58 agent loop; dictionary-based lookup by tool name |
| Dapper + SQLite | Production | Database operations with Busy Timeout=5000 | Phase 65 proven; RunDbConnectionFactory manages connections |

### Supporting
| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| RunDbInitializer | Production | Schema migration (ALTER TABLE) | Adding `deprecated` column to memory_nodes |
| ToolDescriptor | Production | Self-describing tool metadata for LLM prompt injection | Each new tool defines its own Descriptor |
| ToolResult | Production | Structured result envelope (Ok/Failed) | Returned by every tool ExecuteAsync |
| ModuleEvent<T> | Production | Generic event wrapper with typed payload | Wrapping MemoryOperationPayload for EventBus |

### No Alternatives Needed
All components are existing production code. No new NuGet packages required.

**Installation:** None required. All dependencies already in project.

---

## Architecture Patterns

### Current Memory Tool Files (to be refactored)
```
src/OpenAnima.Core/Tools/
    MemoryWriteTool.cs     -> Split into MemoryCreateTool.cs + MemoryUpdateTool.cs
    MemoryQueryTool.cs     -> Rename to MemoryListTool.cs
    MemoryDeleteTool.cs    -> Modify for soft-delete
    MemoryRecallTool.cs    -> Keep as-is (not part of Phase 67)
    MemoryLinkTool.cs      -> Keep as-is (not part of Phase 67)
```

### Recommended File Changes
```
src/OpenAnima.Core/
    Tools/
        MemoryCreateTool.cs     # NEW: memory_create tool
        MemoryUpdateTool.cs     # NEW: memory_update tool
        MemoryDeleteTool.cs     # MODIFIED: soft-delete (deprecated flag)
        MemoryListTool.cs       # NEW: memory_list tool (replaces MemoryQueryTool for listing)
        MemoryQueryTool.cs      # KEEP or REMOVE: decide in plan
        MemoryRecallTool.cs     # UNCHANGED
        MemoryLinkTool.cs       # UNCHANGED
    Events/
        ChatEvents.cs           # ADD: MemoryOperationPayload record
    Memory/
        MemoryNode.cs           # ADD: Deprecated property
        MemoryGraph.cs          # MODIFY: soft-delete, filter deprecated from queries
        SedimentationService.cs # MODIFY: bilingual prompt, 20-msg cap
    RunPersistence/
        RunDbInitializer.cs     # ADD: ALTER TABLE migration for deprecated column
    DependencyInjection/
        RunServiceExtensions.cs # UPDATE: register new tools, remove old
```

### Pattern 1: Memory Tool with EventBus Publishing

**What:** Each memory tool injects IEventBus and publishes a MemoryOperationPayload event after successful operation.

**When to use:** All four memory tools (create, update, delete, list).

**Key design:** The EventBus is injected into each tool. Publication happens AFTER the database write succeeds, not before. This ensures Phase 68 consumers only see successful operations.

**Example:**
```csharp
// Source: Pattern derived from LLMModule.cs lines 989-1007 (ToolCallStartedPayload publishing)
public record MemoryOperationPayload(
    string Operation,      // "create" | "update" | "delete" | "list"
    string AnimaId,
    string Uri,
    string? Content,       // null for delete/list
    int? NodeCount,        // non-null for list results
    bool Success);

// In MemoryCreateTool.ExecuteAsync:
await _eventBus.PublishAsync(new ModuleEvent<MemoryOperationPayload>
{
    EventName = "Memory.operation",
    SourceModuleId = "MemoryTools",
    Payload = new MemoryOperationPayload(
        Operation: "create",
        AnimaId: animaId,
        Uri: uri,
        Content: content,
        NodeCount: null,
        Success: true)
}, ct);
```

### Pattern 2: Soft Delete via Deprecated Column

**What:** Instead of CASCADE DELETE (current behavior), soft-delete sets `deprecated = 1` on the memory_nodes row. Node is hidden from recall and listings but recoverable from /memory UI.

**When to use:** `memory_delete` tool invocation.

**Schema change:**
```sql
-- Migration: add deprecated column to memory_nodes
ALTER TABLE memory_nodes ADD COLUMN deprecated INTEGER NOT NULL DEFAULT 0;
```

**Query filter (all non-delete queries must exclude deprecated):**
```sql
-- Example: QueryByPrefixAsync
WHERE p.anima_id = @animaId AND p.uri LIKE @Prefix || '%'
  AND n.deprecated = 0
```

**Soft-delete implementation:**
```csharp
// In MemoryGraph, new method or modified DeleteNodeAsync:
public async Task SoftDeleteNodeAsync(string animaId, string uri, CancellationToken ct = default)
{
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    var uuid = await conn.QueryFirstOrDefaultAsync<string>(
        "SELECT node_uuid FROM memory_uri_paths WHERE uri = @uri AND anima_id = @animaId",
        new { uri, animaId });

    if (uuid == null) return;

    await conn.ExecuteAsync(
        "UPDATE memory_nodes SET deprecated = 1, updated_at = @Now WHERE uuid = @uuid",
        new { uuid, Now = DateTimeOffset.UtcNow.ToString("O") });

    _glossaryCache.TryRemove(animaId, out _);
}
```

### Pattern 3: Tool Naming Convention (memory_create, memory_update, memory_delete, memory_list)

**What:** Four tools with standardized names matching the requirements exactly.

**Tool Parameter Design:**

| Tool | Required Parameters | Optional Parameters |
|------|-------------------|---------------------|
| `memory_create` | `path` (URI), `content`, `keywords` | `disclosure_trigger` |
| `memory_update` | `uri` (or `uuid`), `content` | `keywords`, `disclosure_trigger` |
| `memory_delete` | `uri` | - |
| `memory_list` | `uri_prefix` | - |

**Critical note on anima_id:** The current tools require `anima_id` as an explicit parameter. This is fragile -- the LLM must know the anima_id. A better approach is to inject the `IModuleContext` (which has `ActiveAnimaId`) into the tools and auto-resolve anima_id. This removes a required parameter and prevents wrong-anima writes.

**Example: MemoryCreateTool:**
```csharp
public class MemoryCreateTool : IWorkspaceTool
{
    private readonly IMemoryGraph _memoryGraph;
    private readonly IEventBus _eventBus;

    public MemoryCreateTool(IMemoryGraph memoryGraph, IEventBus eventBus)
    {
        _memoryGraph = memoryGraph;
        _eventBus = eventBus;
    }

    public ToolDescriptor Descriptor { get; } = new(
        "memory_create",
        "Create a new memory node at the given path with content and keywords",
        new ToolParameterSchema[]
        {
            new("path", "string", "Memory URI path, e.g. 'project://myapp/architecture'", Required: true),
            new("content", "string", "Memory content text", Required: true),
            new("keywords", "string", "Comma-separated keywords for recall (bilingual ok)", Required: true),
            new("disclosure_trigger", "string", "Optional trigger phrase for disclosure recall", Required: false)
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        // ... parameter validation ...
        // ... WriteNodeAsync ...
        // ... PublishAsync MemoryOperationPayload ...
        // ... return ToolResult.Ok ...
    }
}
```

### Pattern 4: Bilingual Sedimentation Prompt

**What:** Updated ExtractionSystemPrompt that explicitly requires bilingual keywords and broader triggers.

**Current prompt issues (from design discussion):**
1. Keywords are English-only -- Chinese content produces no Chinese keywords
2. Disclosure triggers are too narrow (single phrase)
3. No message cap -- full conversation sent to extraction LLM

**Updated prompt:**
```
You are a memory extraction assistant. Given a conversation between a user and an AI assistant,
extract stable, reusable knowledge: facts, preferences, entities, or learnings.

CRITICAL REQUIREMENTS:
1. Keywords MUST be bilingual: include BOTH Chinese and English versions.
   Example: "architecture,架构,Blazor,设计模式,design patterns"
2. Disclosure triggers must cover MULTIPLE scenarios:
   - When user asks about the topic
   - When user introduces themselves or the topic comes up naturally
   - When the topic is related to other recalled memories
   Example: "asks about project architecture OR discusses system design OR mentions Blazor components"
3. Each item must be ONE atomic knowledge point
4. Use "update" with existing URI when refining existing knowledge
5. Use "create" with descriptive ID for new knowledge

Return JSON: { "extracted": [...], "skipped_reason": null or "..." }
Each item: { "action": "create"|"update", "uri": "sediment://{type}/{id}",
             "content": "...", "keywords": "kw1,关键词2,...", "disclosure_trigger": "..." }
```

### Pattern 5: 20-Message Input Cap

**What:** Cap sedimentation input at the last 20 messages of conversation.

**Where to apply:** In `SedimentationService.SedimentAsync`, before calling `BuildExtractionMessages`.

**Implementation:**
```csharp
// In SedimentAsync, before BuildExtractionMessages:
var cappedMessages = messages.Count > 20
    ? messages.Skip(messages.Count - 20).ToList()
    : messages;

var chatMessages = BuildExtractionMessages(cappedMessages, llmResponse, contextSummary);
```

**Rationale:** 20 messages is roughly 2000-4000 tokens of context, which is enough for the extraction LLM to understand the conversation topic without cost explosion. The cap applies to the messages parameter, not the full chat history.

### Anti-Patterns to Avoid

- **Hard delete in memory_delete tool:** Requirements explicitly say "deprecated flag, recoverable from /memory UI." Never cascade-delete from memory_delete tool.
- **Requiring anima_id as explicit tool parameter:** The LLM doesn't know its own anima_id reliably. Inject IModuleContext or resolve from AgentToolDispatcher context.
- **Publishing EventBus events before DB write:** If the DB write fails, the event creates an inconsistent state. Always publish after successful persistence.
- **Removing existing MemoryWriteTool before adding replacements:** Phase 67 must maintain backward compat until new tools are registered. Add new tools first, then remove/deprecate old.
- **English-only sedimentation keywords:** The whole point of MEMS-01 is bilingual extraction. Prompt must explicitly request both languages.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Tool dispatch infrastructure | Custom routing layer | IWorkspaceTool + AgentToolDispatcher | Existing pattern handles tool lookup, execution, error wrapping, XML formatting |
| Event publishing | Manual callback system | IEventBus.PublishAsync<MemoryOperationPayload> | Phase 42 infrastructure, decouples tools from UI consumers |
| UUID generation | Custom ID scheme | `Guid.NewGuid().ToString("D")` | Already used in MemoryGraph.WriteNodeAsync for new nodes |
| Content versioning | Manual version tracking | memory_contents table with MAX(id) | Phase 65 auto-versioning via WriteNodeAsync |
| Keyword normalization | Custom parser | SedimentationService.NormalizeKeywords | Already handles comma-separated to JSON array conversion |
| Schema migration | Manual DDL execution | RunDbInitializer migration pattern | Proven atomic migration with IF NOT EXISTS idempotence |

**Key insight:** Phase 67 is primarily a refactoring and prompt-improvement phase, not a greenfield implementation. The infrastructure already exists. The risk is breaking existing functionality while renaming tools and changing delete semantics.

---

## Current Schema State (from Phase 65)

### Four Tables

**memory_nodes** (UUID primary key, stable node identity):
```sql
uuid        TEXT NOT NULL PRIMARY KEY,
anima_id    TEXT NOT NULL,
node_type   TEXT NOT NULL DEFAULT 'Fact',
display_name TEXT,
created_at  TEXT NOT NULL,
updated_at  TEXT NOT NULL
-- Phase 67 adds: deprecated INTEGER NOT NULL DEFAULT 0
```

**memory_contents** (versioned content, newest = MAX(id)):
```sql
id                  INTEGER PRIMARY KEY AUTOINCREMENT,
node_uuid           TEXT NOT NULL,
anima_id            TEXT NOT NULL,
content             TEXT NOT NULL,
disclosure_trigger  TEXT,
keywords            TEXT,
source_artifact_id  TEXT,
source_step_id      TEXT,
created_at          TEXT NOT NULL
```

**memory_edges** (UUID-based directed relationships):
```sql
id                  INTEGER PRIMARY KEY AUTOINCREMENT,
anima_id            TEXT NOT NULL,
parent_uuid         TEXT NOT NULL,
child_uuid          TEXT NOT NULL,
label               TEXT NOT NULL,
priority            INTEGER NOT NULL DEFAULT 0,
weight              REAL NOT NULL DEFAULT 1.0,
bidirectional       INTEGER NOT NULL DEFAULT 0,
disclosure_trigger  TEXT,
created_at          TEXT NOT NULL
```

**memory_uri_paths** (URI-to-UUID routing):
```sql
id          INTEGER PRIMARY KEY AUTOINCREMENT,
uri         TEXT NOT NULL,
node_uuid   TEXT NOT NULL,
anima_id    TEXT NOT NULL,
created_at  TEXT NOT NULL
```

### Key Indexes
- `idx_memory_nodes_anima` on `memory_nodes(anima_id)`
- `idx_memory_contents_node` on `memory_contents(node_uuid, id DESC)`
- `idx_memory_uri_paths_uri_anima` UNIQUE on `memory_uri_paths(uri, anima_id)`

### Write Path (WriteNodeAsync)
1. Resolve existing UUID from `memory_uri_paths` by (URI, AnimaId)
2. If exists: UPDATE `memory_nodes.updated_at`, INSERT new `memory_contents` row, prune to 10 versions
3. If new: Generate UUID, INSERT into `memory_nodes`, `memory_contents`, `memory_uri_paths`
4. Invalidate glossary cache

### Soft-Delete Migration
```sql
-- New column needed:
ALTER TABLE memory_nodes ADD COLUMN deprecated INTEGER NOT NULL DEFAULT 0;

-- New index for efficient filtering:
CREATE INDEX IF NOT EXISTS idx_memory_nodes_deprecated ON memory_nodes(anima_id, deprecated);
```

All existing queries that return nodes to callers must add `AND n.deprecated = 0` to their WHERE clauses. The exceptions are:
- `/memory` page `GetAllNodesAsync`: Should show deprecated nodes with visual indicator for recovery
- Or: Add a separate `GetAllNodesIncludingDeprecatedAsync` method for the UI

---

## Memory CRUD Operation Patterns

### memory_create
1. Validate required params: `path`, `content`, `keywords`
2. Check node does NOT exist at path (fail if already exists -- use memory_update instead)
3. Build MemoryNode with URI = path, Keywords = normalized JSON array
4. Call `IMemoryGraph.WriteNodeAsync(node)`
5. Publish `MemoryOperationPayload("create", ...)`
6. Return `ToolResult.Ok` with created URI and node UUID

### memory_update
1. Validate required params: `uri` (or `uuid`), `content`
2. Check node EXISTS at uri (fail if not found)
3. Build MemoryNode with same URI, new content, optional new keywords/trigger
4. Call `IMemoryGraph.WriteNodeAsync(node)` -- appends new content version, UUID unchanged
5. Publish `MemoryOperationPayload("update", ...)`
6. Return `ToolResult.Ok` with URI and UUID

### memory_delete (soft-delete)
1. Validate required param: `uri`
2. Call new `IMemoryGraph.SoftDeleteNodeAsync(animaId, uri)` or equivalent
3. This sets `deprecated = 1` on memory_nodes row, does NOT delete edges/contents/paths
4. Publish `MemoryOperationPayload("delete", ...)`
5. Return `ToolResult.Ok` with URI and "deprecated" status

### memory_list
1. Validate required param: `uri_prefix`
2. Call `IMemoryGraph.QueryByPrefixAsync(animaId, uriPrefix)` -- filters `deprecated = 0`
3. Publish `MemoryOperationPayload("list", ..., NodeCount: results.Count)`
4. Return `ToolResult.Ok` with prefix, count, and node summaries (URI, display_name, node_type, keywords)

---

## Agent Tool Infrastructure

### How Tools Are Registered (DI)
```csharp
// In RunServiceExtensions.cs:
services.AddSingleton<IWorkspaceTool, MemoryQueryTool>();
services.AddSingleton<IWorkspaceTool, MemoryWriteTool>();
services.AddSingleton<IWorkspaceTool, MemoryDeleteTool>();
services.AddSingleton<IWorkspaceTool, MemoryRecallTool>();
services.AddSingleton<IWorkspaceTool, MemoryLinkTool>();
```

Phase 67 changes this to:
```csharp
services.AddSingleton<IWorkspaceTool, MemoryCreateTool>();
services.AddSingleton<IWorkspaceTool, MemoryUpdateTool>();
services.AddSingleton<IWorkspaceTool, MemoryDeleteTool>();  // modified for soft-delete
services.AddSingleton<IWorkspaceTool, MemoryListTool>();
services.AddSingleton<IWorkspaceTool, MemoryRecallTool>();  // unchanged
services.AddSingleton<IWorkspaceTool, MemoryLinkTool>();    // unchanged
```

### How Tools Are Dispatched
`AgentToolDispatcher` builds a `Dictionary<string, IWorkspaceTool>` from all `IEnumerable<IWorkspaceTool>` via DI. Tool names are matched case-insensitively. Each tool is called via `tool.ExecuteAsync(workspaceRoot, parameters, ct)`.

### How EventBus Events Are Published
The LLMModule publishes `ToolCallStartedPayload` and `ToolCallCompletedPayload` events around tool dispatch (lines 989-1007 in LLMModule.cs). Memory tools should publish their own `MemoryOperationPayload` events **inside** their ExecuteAsync, following the same `ModuleEvent<T>` pattern.

### Tool Injection Challenge
Current memory tools only inject `IMemoryGraph`. Phase 67 tools need:
- `IMemoryGraph` -- for CRUD operations
- `IEventBus` -- for MemoryOperationPayload events

Since tools are registered as `IWorkspaceTool` singletons, the `IEventBus` must also be singleton-compatible. IEventBus is already a singleton (EventBus in Phase 42), so this is safe.

**anima_id resolution:** The `workspaceRoot` parameter won't help resolve anima_id. Two approaches:
1. Keep `anima_id` as an explicit tool parameter (current approach, used by all existing memory tools)
2. Inject `IModuleContext` to auto-resolve (cleaner but requires DI change)

Recommendation: Keep `anima_id` as explicit parameter for now. The LLM is instructed to pass it via the system prompt. Changing to auto-resolve would require modifying `IWorkspaceTool.ExecuteAsync` signature or adding a context parameter, which is a bigger refactor.

---

## Bilingual Keyword Extraction Approach

### Current Problem
The existing SedimentationService prompt says: `"keywords": "comma,separated,keywords"`. It does not mention language requirements. Result: all keywords are English-only, even when conversation is in Chinese.

### Solution: Bilingual Prompt Instructions

The updated prompt must explicitly require:
1. **Chinese keywords** for Chinese-language content
2. **English keywords** for English-language content
3. **Both languages** when content is bilingual/mixed

### Keyword Format
Keywords remain stored as JSON array strings in `memory_contents.keywords` column. No schema change needed. The array simply includes both languages:
```json
["architecture", "架构", "Blazor", "组件设计", "component design"]
```

### Trigger Condition Broadening
Current triggers are too narrow: a single phrase like "asks about architecture". Requirements say "broader trigger conditions (covers both introduction and query scenarios)."

Updated format uses OR-separated multi-scenario triggers:
```
"asks about project architecture OR discusses system design OR mentions component patterns"
```

The `DisclosureMatcher` does case-insensitive substring matching, so multi-scenario triggers work automatically if any substring matches.

**Important:** The DisclosureMatcher checks if the trigger string is a substring of the context. With OR-separated triggers, the literal string "asks about project architecture OR discusses system design" would need to appear in the context. This means the trigger format must remain a **single phrase** that the DisclosureMatcher can match as a substring, OR the DisclosureMatcher must be updated to split on " OR " and check each part independently.

**Recommendation:** Update `DisclosureMatcher` to split trigger on " OR " and match any sub-condition. This is a small change with large recall improvement.

---

## Sedimentation Algorithm (Message Windowing)

### Current Flow
1. LLMModule calls `SedimentAsync(animaId, messages, llmResponse, sourceStepId)`
2. SedimentationService queries existing `sediment://` nodes for merge context (cap 50)
3. Builds extraction prompt with full conversation messages
4. Calls secondary LLM for extraction
5. Parses JSON response
6. Writes each extracted item as MemoryNode

### Phase 67 Changes
1. **Cap messages at 20:** `messages.TakeLast(20)` before prompt building
2. **Bilingual prompt:** Updated ExtractionSystemPrompt
3. **Broader triggers:** Updated prompt instructions for multi-scenario triggers

### Cost Control
20 messages at ~100 tokens each = ~2000 input tokens for the conversation portion. Plus system prompt (~500 tokens) + context summary (~1000 tokens) = ~3500 total input tokens per sedimentation call. This is well within budget for fast/cheap models like GPT-4o-mini or Claude Haiku.

---

## Common Pitfalls

### Pitfall 1: Breaking Existing memory_write / memory_query Tool Names
**What goes wrong:** LLM system prompts reference `memory_write` tool name. After renaming to `memory_create` / `memory_update`, existing configurations break silently -- LLM tries to call `memory_write` but AgentToolDispatcher returns "unknown tool."
**Why it happens:** Tool names are string-matched in AgentToolDispatcher's dictionary. Renaming the tool class changes the Descriptor.Name, which changes the dictionary key.
**How to avoid:** Update the WorkspaceToolModule's tool_definitions prompt (injected into LLM system prompt) simultaneously with tool registration. Verify the LLM prompt includes `memory_create`, `memory_update`, `memory_delete`, `memory_list`.
**Warning signs:** "Unknown tool 'memory_write'" in logs after Phase 67 deployment.

### Pitfall 2: Deprecated Nodes Still Appearing in Recall
**What goes wrong:** After soft-deleting a node, it still appears in disclosure matching and glossary keyword matching because `GetDisclosureNodesAsync` and `RebuildGlossaryAsync` don't filter `deprecated = 0`.
**Why it happens:** Forgetting to add the deprecated filter to ALL query methods in MemoryGraph.
**How to avoid:** Systematic audit of every SELECT query in MemoryGraph.cs. Add `AND n.deprecated = 0` to: `GetNodeAsync`, `QueryByPrefixAsync`, `GetAllNodesAsync`, `GetDisclosureNodesAsync`. Do NOT add to `GetNodeByUuidAsync` (needed for recovery).
**Warning signs:** Soft-deleted nodes appearing in recall results; glossary matches against deprecated nodes.

### Pitfall 3: MemoryOperationPayload Not Published on Failure
**What goes wrong:** Tool fails (e.g., node not found for update) but no event is published. Phase 68 consumers never learn about the failed attempt.
**Why it happens:** Event publishing placed only in the success path, not in the failure path.
**How to avoid:** Publish MemoryOperationPayload with `Success: false` in failure paths too. Phase 68 can decide whether to display failed operations.
**Warning signs:** UI shows no activity for failed tool calls.

### Pitfall 4: Sedimentation Prompt Returns Only English Keywords
**What goes wrong:** Despite bilingual instructions, the extraction LLM returns English-only keywords because the instruction is insufficiently explicit.
**Why it happens:** LLMs default to the language of the prompt instructions (English). Without strong emphasis and examples, they produce English keywords even from Chinese conversation.
**How to avoid:** Include explicit bilingual examples in the prompt. Add a validation step: if conversation contains Chinese characters but keywords are all ASCII, log a warning.
**Warning signs:** Keyword arrays contain no CJK characters despite Chinese conversation content.

### Pitfall 5: DisclosureMatcher Fails with Multi-Scenario Triggers
**What goes wrong:** Trigger like "asks about architecture OR discusses design patterns" never matches because DisclosureMatcher does substring matching on the entire trigger string.
**Why it happens:** Current DisclosureMatcher.Match() does `context.Contains(trigger, StringComparison.OrdinalIgnoreCase)` -- treats the entire trigger as a single substring, including the " OR " separator.
**How to avoid:** Update DisclosureMatcher to split on " OR " and check each sub-trigger independently. Alternatively, keep triggers as single phrases (simpler, less powerful).
**Warning signs:** Disclosure recall rate drops to near-zero with new multi-scenario triggers.

### Pitfall 6: ALTER TABLE Migration Fails on Existing Databases
**What goes wrong:** The `ALTER TABLE memory_nodes ADD COLUMN deprecated INTEGER NOT NULL DEFAULT 0` fails because SQLite doesn't support adding NOT NULL columns without defaults in all versions, or the migration runs multiple times.
**Why it happens:** Migration idempotence not handled. SQLite supports `ALTER TABLE ... ADD COLUMN` but the column check needs IF NOT EXISTS equivalent.
**How to avoid:** Check if column exists first via `PRAGMA table_info(memory_nodes)`, then ALTER only if missing. This is the existing RunDbInitializer migration pattern.
**Warning signs:** "duplicate column name: deprecated" error on second startup.

---

## Code Examples

### Example 1: MemoryOperationPayload Record

```csharp
// Source: Pattern from ChatEvents.cs (ToolCallStartedPayload/ToolCallCompletedPayload)
namespace OpenAnima.Core.Events;

/// <summary>
/// Event payload published when a memory tool operation completes.
/// Consumed by Phase 68 visibility components (tool cards, summary chips).
/// </summary>
public record MemoryOperationPayload(
    string Operation,      // "create" | "update" | "delete" | "list"
    string AnimaId,
    string Uri,
    string? Content,       // null for delete/list
    int? NodeCount,        // non-null for list results
    bool Success);
```

### Example 2: MemoryCreateTool (Full Implementation Pattern)

```csharp
// Source: MemoryWriteTool.cs pattern + IEventBus integration
public class MemoryCreateTool : IWorkspaceTool
{
    private readonly IMemoryGraph _memoryGraph;
    private readonly IEventBus _eventBus;

    public MemoryCreateTool(IMemoryGraph memoryGraph, IEventBus eventBus)
    {
        _memoryGraph = memoryGraph;
        _eventBus = eventBus;
    }

    public ToolDescriptor Descriptor { get; } = new(
        "memory_create",
        "Create a new memory node at the given path with content and keywords",
        new ToolParameterSchema[]
        {
            new("path", "string", "Memory URI path, e.g. 'project://myapp/architecture'", Required: true),
            new("content", "string", "Memory content text", Required: true),
            new("keywords", "string", "Comma-separated keywords for recall (bilingual ok)", Required: true),
            new("anima_id", "string", "Anima ID owning this memory", Required: true),
            new("disclosure_trigger", "string", "Optional trigger phrase for disclosure recall", Required: false)
        });

    public async Task<ToolResult> ExecuteAsync(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!parameters.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
            return ToolResult.Failed("memory_create", "Missing required parameter: path", MakeMeta(workspaceRoot, sw));
        if (!parameters.TryGetValue("content", out var content) || string.IsNullOrWhiteSpace(content))
            return ToolResult.Failed("memory_create", "Missing required parameter: content", MakeMeta(workspaceRoot, sw));
        if (!parameters.TryGetValue("anima_id", out var animaId) || string.IsNullOrWhiteSpace(animaId))
            return ToolResult.Failed("memory_create", "Missing required parameter: anima_id", MakeMeta(workspaceRoot, sw));
        if (!parameters.TryGetValue("keywords", out var keywordsRaw) || string.IsNullOrWhiteSpace(keywordsRaw))
            return ToolResult.Failed("memory_create", "Missing required parameter: keywords", MakeMeta(workspaceRoot, sw));

        parameters.TryGetValue("disclosure_trigger", out var trigger);

        // Check node doesn't already exist
        var existing = await _memoryGraph.GetNodeAsync(animaId, path, ct);
        if (existing != null)
            return ToolResult.Failed("memory_create", $"Node already exists at '{path}'. Use memory_update to modify.", MakeMeta(workspaceRoot, sw));

        // Normalize keywords
        var keywordsJson = NormalizeKeywords(keywordsRaw);

        var now = DateTimeOffset.UtcNow.ToString("O");
        var node = new MemoryNode
        {
            Uri = path,
            AnimaId = animaId,
            Content = content,
            Keywords = keywordsJson,
            DisclosureTrigger = string.IsNullOrWhiteSpace(trigger) ? null : trigger,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _memoryGraph.WriteNodeAsync(node, ct);

        // Publish event for Phase 68 visibility
        await _eventBus.PublishAsync(new ModuleEvent<MemoryOperationPayload>
        {
            EventName = "Memory.operation",
            SourceModuleId = "MemoryTools",
            Payload = new MemoryOperationPayload("create", animaId, path, content, null, true)
        }, ct);

        sw.Stop();
        return ToolResult.Ok("memory_create", new { path, anima_id = animaId, status = "created" }, MakeMeta(workspaceRoot, sw));
    }

    // ... NormalizeKeywords, MakeMeta helpers ...
}
```

### Example 3: Soft-Delete in MemoryGraph

```csharp
// Source: Derived from existing DeleteNodeAsync pattern in MemoryGraph.cs
public async Task SoftDeleteNodeAsync(string animaId, string uri, CancellationToken ct = default)
{
    await using var conn = _factory.CreateConnection();
    await conn.OpenAsync(ct);

    var uuid = await conn.QueryFirstOrDefaultAsync<string>(
        "SELECT node_uuid FROM memory_uri_paths WHERE uri = @uri AND anima_id = @animaId",
        new { uri, animaId });

    if (uuid == null) return;

    await conn.ExecuteAsync(
        "UPDATE memory_nodes SET deprecated = 1, updated_at = @Now WHERE uuid = @uuid",
        new { uuid, Now = DateTimeOffset.UtcNow.ToString("O") });

    _glossaryCache.TryRemove(animaId, out _);
}
```

### Example 4: 20-Message Cap in Sedimentation

```csharp
// Source: SedimentationService.SedimentAsync modification
public async Task SedimentAsync(
    string animaId,
    IReadOnlyList<ChatMessageInput> messages,
    string llmResponse,
    string? sourceStepId,
    CancellationToken ct = default)
{
    // MEMS-03: Cap input at last 20 messages
    var cappedMessages = messages.Count > 20
        ? (IReadOnlyList<ChatMessageInput>)messages.Skip(messages.Count - 20).ToList()
        : messages;

    // ... rest of existing SedimentAsync logic uses cappedMessages ...
    var chatMessages = BuildExtractionMessages(cappedMessages, llmResponse, contextSummary);
}
```

### Example 5: Updated Bilingual Extraction Prompt

```csharp
// Source: SedimentationService.ExtractionSystemPrompt replacement
private const string ExtractionSystemPrompt = """
    You are a memory extraction assistant. Given a conversation, extract stable, reusable knowledge.

    CRITICAL REQUIREMENTS:
    1. Keywords MUST be bilingual when conversation contains Chinese:
       - Include BOTH Chinese and English versions of each concept
       - Example keywords: "architecture,架构,Blazor,设计模式,design patterns"
    2. Disclosure triggers must cover MULTIPLE scenarios separated by " OR ":
       - A question about the topic
       - Natural mention of the topic in conversation
       - Related topics that would benefit from this knowledge
       Example: "discusses architecture OR asks about system design OR mentions component structure"
    3. Each item must be ONE atomic, reusable knowledge point
    4. Use "update" action with existing URI to refine existing knowledge
    5. Use "create" action with descriptive ID for new knowledge
    6. Do NOT extract greetings, acknowledgments, or ephemeral exchanges

    Return JSON:
    {
      "extracted": [
        {
          "action": "create" or "update",
          "uri": "sediment://fact/{id}" or "sediment://preference/{id}" etc.,
          "content": "single atomic knowledge statement",
          "keywords": "keyword1,关键词2,keyword3,关键词4",
          "disclosure_trigger": "scenario1 OR scenario2 OR scenario3"
        }
      ],
      "skipped_reason": null or "explanation if nothing extracted"
    }
    """;
```

---

## DisclosureMatcher Update for Multi-Scenario Triggers

The current `DisclosureMatcher.Match` does simple substring matching. For " OR "-separated triggers to work, it needs modification:

```csharp
// Current behavior:
// context.Contains(node.DisclosureTrigger, StringComparison.OrdinalIgnoreCase)

// Updated behavior:
public static IReadOnlyList<MemoryNode> Match(IReadOnlyList<MemoryNode> nodes, string context)
{
    var matched = new List<MemoryNode>();
    foreach (var node in nodes)
    {
        if (string.IsNullOrEmpty(node.DisclosureTrigger)) continue;

        // Split on " OR " for multi-scenario matching
        var triggers = node.DisclosureTrigger.Split(" OR ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (triggers.Any(t => context.Contains(t, StringComparison.OrdinalIgnoreCase)))
            matched.Add(node);
    }
    return matched;
}
```

---

## /memory Page Impact

The `/memory` page (`MemoryGraph.razor`) calls `IMemoryGraph.GetAllNodesAsync(animaId)` to load the node tree. After Phase 67:

1. **Default listing excludes deprecated nodes:** `GetAllNodesAsync` filters `deprecated = 0`
2. **Recovery UI needed:** Either:
   - Add a "Show deprecated" toggle that calls a variant including deprecated nodes
   - Add `GetAllNodesIncludingDeprecatedAsync` method to IMemoryGraph
3. **Deprecated visual indicator:** Nodes with `deprecated = 1` should render with strikethrough or muted styling
4. **Undelete action:** MemoryNodeCard needs an "Restore" button for deprecated nodes that sets `deprecated = 0`

**Recommendation for Phase 67:** Keep the /memory page showing ALL nodes (including deprecated) with visual distinction. The `GetAllNodesAsync` used by recall/sedimentation should filter deprecated, but the UI query should include them. This means two query behaviors:
- `GetAllNodesAsync(animaId)` -- filters `deprecated = 0` (for recall, sedimentation, listings)
- `GetAllNodesAsync(animaId, includeDeprecated: true)` -- shows all (for /memory UI)

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single `memory_write` tool (create or update) | Separate `memory_create` and `memory_update` tools | Phase 67 | Clearer intent, prevents accidental overwrites |
| Hard delete via `memory_delete` (cascade) | Soft delete via `deprecated` flag | Phase 67 | Recoverable deletes, user trust preserved |
| `memory_query` for prefix search | `memory_list` for prefix navigation | Phase 67 | Agent self-awareness of knowledge graph structure |
| No EventBus events from memory tools | MemoryOperationPayload on all operations | Phase 67 | Phase 68 visibility, logging, audit trail |
| English-only sedimentation keywords | Bilingual (Chinese + English) keywords | Phase 67 | Recall works for Chinese users |
| Full conversation sent to extraction LLM | Last 20 messages only | Phase 67 | Cost control, focused extraction |
| Single-phrase disclosure triggers | Multi-scenario " OR "-separated triggers | Phase 67 | Broader recall coverage |

---

## Implementation Checklist (per Success Criterion)

### SC-1: memory_create tool
- [ ] Create `MemoryCreateTool.cs` implementing `IWorkspaceTool`
- [ ] Tool name: "memory_create"
- [ ] Required params: path, content, keywords, anima_id
- [ ] Optional param: disclosure_trigger
- [ ] Existence check: fail if node already exists at path
- [ ] Register in `RunServiceExtensions.cs`
- [ ] Remove `MemoryWriteTool` registration (replaced by create + update)
- [ ] Test: create node, verify on /memory page

### SC-2: memory_update tool
- [ ] Create `MemoryUpdateTool.cs` implementing `IWorkspaceTool`
- [ ] Tool name: "memory_update"
- [ ] Required params: uri, content, anima_id
- [ ] Optional params: keywords, disclosure_trigger
- [ ] Existence check: fail if node does NOT exist
- [ ] Calls WriteNodeAsync (appends content version, UUID unchanged)
- [ ] Register in `RunServiceExtensions.cs`
- [ ] Test: update node, verify UUID same, content changed

### SC-3: memory_delete soft-delete
- [ ] Add `deprecated` column to memory_nodes (ALTER TABLE migration)
- [ ] Add `Deprecated` property to MemoryNode record
- [ ] Add `SoftDeleteNodeAsync` method to IMemoryGraph/MemoryGraph
- [ ] Modify MemoryDeleteTool to call SoftDeleteNodeAsync instead of DeleteNodeAsync
- [ ] Filter `deprecated = 0` in all recall/list queries
- [ ] Keep deprecated visible in /memory UI with recovery option
- [ ] Test: delete node, verify hidden from recall, visible in /memory

### SC-4: memory_list tool
- [ ] Create `MemoryListTool.cs` implementing `IWorkspaceTool`
- [ ] Tool name: "memory_list"
- [ ] Required params: uri_prefix, anima_id
- [ ] Returns node summaries (URI, display_name, node_type, keywords)
- [ ] Filters out deprecated nodes
- [ ] Register in `RunServiceExtensions.cs`
- [ ] Remove or keep `MemoryQueryTool` (decide: keep for backward compat or remove)
- [ ] Test: list by prefix, verify correct results

### SC-5: MemoryOperationPayload events
- [ ] Create MemoryOperationPayload record in Events/ChatEvents.cs
- [ ] Inject IEventBus into all four memory tools
- [ ] Publish event after each successful operation
- [ ] Publish event with Success=false for failures
- [ ] EventName: "Memory.operation"
- [ ] Test: subscribe to EventBus, verify payload on each operation

### SC-6: Bilingual sedimentation
- [ ] Replace ExtractionSystemPrompt with bilingual version
- [ ] Add " OR "-split to DisclosureMatcher.Match
- [ ] Cap messages at 20 in SedimentAsync
- [ ] Update tests for new prompt and message cap
- [ ] Test: sedimentation with Chinese conversation produces Chinese keywords

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (existing) |
| Config file | None -- tests in `tests/OpenAnima.Tests/` directory |
| Quick run command | `dotnet test tests/OpenAnima.Tests --filter "MemoryCreate\|MemoryUpdate\|MemoryDelete\|MemoryList\|Sedimentation\|DisclosureMatcher" --no-build -x` |
| Full suite command | `dotnet test tests/OpenAnima.Tests --no-build` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MEMT-01 | memory_create writes node, appears in query | Unit | `dotnet test --filter "MemoryCreateTool" --no-build -x` | No - Wave 0 |
| MEMT-01 | memory_create fails if node already exists | Unit | `dotnet test --filter "MemoryCreateTool" --no-build -x` | No - Wave 0 |
| MEMT-02 | memory_update changes content, UUID stable | Unit | `dotnet test --filter "MemoryUpdateTool" --no-build -x` | No - Wave 0 |
| MEMT-02 | memory_update fails if node not found | Unit | `dotnet test --filter "MemoryUpdateTool" --no-build -x` | No - Wave 0 |
| MEMT-03 | memory_delete sets deprecated flag | Unit | `dotnet test --filter "MemoryDeleteTool\|SoftDelete" --no-build -x` | No - Wave 0 |
| MEMT-03 | deprecated nodes hidden from recall | Unit | `dotnet test --filter "MemoryRecallService\|Deprecated" --no-build -x` | No - Wave 0 |
| MEMT-04 | memory_list returns nodes by prefix | Unit | `dotnet test --filter "MemoryListTool" --no-build -x` | No - Wave 0 |
| MEMT-05 | EventBus receives MemoryOperationPayload | Unit | `dotnet test --filter "MemoryOperation" --no-build -x` | No - Wave 0 |
| MEMS-01 | Bilingual keywords in extraction | Unit | `dotnet test --filter "SedimentationService" --no-build -x` | Exists (update needed) |
| MEMS-02 | Broader triggers with " OR " | Unit | `dotnet test --filter "DisclosureMatcher" --no-build -x` | Exists (update needed) |
| MEMS-03 | Input capped at 20 messages | Unit | `dotnet test --filter "SedimentationService" --no-build -x` | Exists (update needed) |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests --filter "MemoryCreate\|MemoryUpdate\|MemoryDelete\|MemoryList\|Sedimentation\|DisclosureMatcher" --no-build -x`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests --no-build` (full suite, 701 tests)
- **Phase gate:** Full suite green before verify

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/MemoryCreateToolTests.cs` -- covers MEMT-01
- [ ] `tests/OpenAnima.Tests/Unit/MemoryUpdateToolTests.cs` -- covers MEMT-02
- [ ] `tests/OpenAnima.Tests/Unit/MemoryListToolTests.cs` -- covers MEMT-04
- [ ] `tests/OpenAnima.Tests/Unit/MemoryDeleteToolTests.cs` -- update for soft-delete, covers MEMT-03
- [ ] `tests/OpenAnima.Tests/Unit/MemoryOperationEventTests.cs` -- covers MEMT-05
- [ ] Update `SedimentationServiceTests.cs` -- covers MEMS-01, MEMS-02, MEMS-03
- [ ] Update `DisclosureMatcherTests.cs` if exists -- covers multi-scenario trigger split

---

## Open Questions

1. **Should MemoryQueryTool be kept alongside MemoryListTool?**
   - What we know: MemoryQueryTool does prefix-based search (same as memory_list). MemoryRecallTool does keyword+disclosure search (different purpose).
   - What's unclear: Whether removing memory_query breaks any existing system prompt configurations.
   - Recommendation: **Remove MemoryQueryTool, replace with MemoryListTool.** The tools do the same thing with different names. Keeping both is confusing for the LLM.

2. **Should /memory UI show deprecated nodes by default?**
   - What we know: Requirements say "recoverable from the /memory UI." Users need to see deprecated nodes to recover them.
   - What's unclear: Whether to show them inline (with visual distinction) or behind a toggle.
   - Recommendation: **Show inline with muted/strikethrough styling.** A toggle adds UI complexity. Inline display makes recovery discoverable.

3. **Should DisclosureMatcher split on " OR " or keep single-phrase triggers?**
   - What we know: Multi-scenario triggers improve recall. But splitting changes existing trigger behavior.
   - What's unclear: Whether any existing triggers contain the literal string " OR ".
   - Recommendation: **Implement the split.** Existing triggers don't contain " OR " (verified by grep). The change is backward-compatible.

---

## Sources

### Primary (HIGH confidence)
- **Phase 65 codebase** -- MemoryGraph.cs, RunDbInitializer.cs, IMemoryGraph.cs (all four-table schema implementation)
- **Phase 53/58 codebase** -- AgentToolDispatcher.cs, IWorkspaceTool.cs, ToolDescriptor.cs (tool infrastructure)
- **Phase 54 codebase** -- SedimentationService.cs (current extraction prompt and flow)
- **Phase 42 codebase** -- IEventBus.cs, ModuleEvent.cs, EventBus.cs (event publishing)
- **LLMModule.cs** -- ToolCallStartedPayload/CompletedPayload event publishing pattern (lines 989-1007)

### Secondary (MEDIUM confidence)
- **REQUIREMENTS.md** -- MEMT-01 through MEMS-03 requirement definitions
- **ROADMAP.md** -- Phase 67 success criteria and dependency chain
- **STATE.md** -- Accumulated decisions from Phases 65-66

### Tertiary (validated)
- **SQLite ALTER TABLE** -- SQLite supports `ALTER TABLE ... ADD COLUMN` with DEFAULT value. Verified against SQLite documentation: columns added with DEFAULT do not require table rebuild.

---

## Metadata

**Confidence breakdown:**
- Standard stack: **HIGH** -- All components are existing production code, no new dependencies
- Architecture: **HIGH** -- Direct extension of proven IWorkspaceTool + EventBus patterns
- Pitfalls: **HIGH** -- Based on direct code analysis (MemoryGraph queries, DisclosureMatcher behavior, tool registration)
- Sedimentation: **MEDIUM** -- Bilingual prompt effectiveness depends on LLM behavior; prompt design is best-effort

**Research date:** 2026-03-29
**Valid until:** 2026-04-28 (30 days -- architecture is stable, no framework changes expected)
