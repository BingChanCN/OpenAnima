# Phase 66: Platform Persistence - Research

**Researched:** 2026-03-26
**Domain:** Persistent state management (viewport, chat history) across application restarts
**Confidence:** HIGH

## Summary

Phase 66 implements durable storage for two distinct layers of editor state: **(1) visual viewport position/zoom** persisted as per-Anima JSON files, and **(2) full chat message history** with partial context restore based on token budgets. The phase builds on Phase 65's SQLite infrastructure (RunDbConnectionFactory, Dapper, atomic migrations) and reuses the debounce pattern established in EditorStateService's auto-save mechanism. Key insight: chat history survives application restart in full; LLM context is a *configurable token-budget slice* of that history, not a fixed message count.

**Primary recommendation:** Create two parallel persistence layers (viewport JSON + chat SQLite), implement chat history restore on Anima switch with token-budget truncation in ChatContextManager, add a single "LLM context budget" Settings field independent from LLMOptions global config.

---

## User Constraints (from CONTEXT.md)

### Locked Decisions

**Viewport state storage:**
- Independent JSON file: `{animaId}.viewport.json` alongside `{name}.json` wiring files
- Debounce delay: **1000ms** (higher frequency than config changes)
- Per-Anima scoping — switching Animas restores that Anima's viewport

**Chat history storage:**
- **Independent SQLite file: `chat.db`** (separate from `runs.db`)
- Table: `chat_messages` with columns: `anima_id`, `role`, `content`, `tool_calls_json`, `input_tokens`, `output_tokens`, `created_at`
- Per-Anima filtering by `anima_id`
- Write timing: messages written immediately after completion (user on send, assistant after stream ends)
- SQLite stores **full history, truncation only on LLM consumption**

**Chat history UI restore:**
- Restore all messages from SQLite on Anima load, auto-scroll to bottom
- Interrupted messages (IsStreaming=true at shutdown) restore with partial content, labeled **[interrupted]**
- Settings page has independent "LLM context budget" (separate from LLMModule config)

**LLM context window truncation:**
- **Token-budget-based**, not message-count-based
- Walk history from newest to oldest, accumulate token counts, stop when budget exceeded
- Default token budget: configurable in Settings page (not LLMModule config, not appsettings)
- Reuse `ChatContextManager` token tracking infrastructure

### Claude's Discretion

- Exact Settings UI layout for token budget entry
- `chat.db` file location (same directory as `runs.db` or same as config directory)
- Schema index strategy for `chat_messages` table
- How `[interrupted]` label is visually rendered (style, color)

### Deferred Ideas (OUT OF SCOPE)

- Streaming resilience (LLM continues after navigation) — Phase 69
- Per-session chat export / clear history UI — future

---

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PERS-01 | Wiring layout (pan/zoom/scale) persists across restarts per Anima | JSON viewport storage with debounce, loaded on Anima init |
| PERS-02 | Chat history persists across restarts per Anima with scrollback | SQLite chat_messages table, full history stored, immediate writes |
| PERS-03 | Chat UI restore separate from LLM context (token-budget, configurable) | Independent token-budget truncation in ChatContextManager, Settings UI entry |

---

## Standard Stack

### Core Persistence Infrastructure
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| RunDbConnectionFactory | Production | SQLite connection factory with Busy Timeout=5000 | Established Phase 65 pattern, prevents concurrent write failures |
| RunDbInitializer | Production | Schema creation with IF NOT EXISTS idempotence | Phase 65 proven atomic migration pattern |
| Dapper ORM | Production | Lightweight SQL execution and mapping | Phase 65 dependency, proven in memory schema |
| JsonSerializer (System.Text.Json) | .NET native | Viewport state serialization | Already used in ConfigurationLoader, no extra dependency |

### Reusable Patterns
| Pattern | Location | Purpose |
|---------|----------|---------|
| Debounce auto-save | EditorStateService.TriggerAutoSave() | 500ms CancellationTokenSource swap + Task.Delay |
| Per-Anima scoping | memory_nodes/memory_edges via `anima_id` column | All DB work filters by anima_id |
| Hosted service init | AnimaInitializationService | Application startup hook, runs before web server |
| Config directory resolution | ConfigurationLoader._configDirectory | Directory.CreateDirectory if missing, Path.Combine pattern |

### Supporting Services
| Service | Version | Purpose | When to Use |
|---------|---------|---------|------------|
| ChatContextManager | Production | Token counting, context utilization tracking | Already tracks token counts; reuse for truncation walk |
| ChatSessionState | Production | In-memory chat messages (Blazor circuit lifespan) | Extend with restore logic on Anima switch |
| EditorStateService | Production | Pan/zoom/scale state + auto-save trigger | Add viewport save call to UpdatePan/UpdateScale methods |
| IConfigurationLoader | Production | Async JSON save/load with validation | Port pattern for viewport JSON operations |

### New Factories & Initializers
| Class | Purpose | Pattern Origin |
|-------|---------|-----------------|
| ChatDbConnectionFactory | Create chat.db connections with Busy Timeout=5000 | Copy RunDbConnectionFactory constructor shape |
| ChatDbInitializer | CREATE TABLE chat_messages + indexes | Copy RunDbInitializer structure, IF NOT EXISTS statements |
| ViewportStateService | Load/save viewport JSON, debounce trigger | Pattern from EditorStateService + ConfigurationLoader |

---

## Architecture Patterns

### Recommended Project Structure

```
src/OpenAnima.Core/
├── ChatPersistence/                    # New folder
│   ├── ChatDbConnectionFactory.cs      # Factory (copy from RunDbConnectionFactory)
│   ├── ChatDbInitializer.cs            # Schema creation (copy from RunDbInitializer)
│   ├── ChatHistoryService.cs           # Restore/persist chat messages
│   └── ChatMessage.cs                  # DTO for DB rows
├── ViewportPersistence/                # New folder
│   ├── ViewportStateService.cs         # Load/save viewport JSON + debounce
│   └── ViewportState.cs                # Simple record: { scale, panX, panY }
├── Services/
│   ├── ChatSessionState.cs             # Extended with restore hooks
│   ├── EditorStateService.cs           # Extended with viewport save triggers
│   └── ChatContextManager.cs           # Extended token-budget truncation logic
└── DependencyInjection/
    └── AnimaServiceExtensions.cs       # Register new services + factories
```

### Pattern 1: SQLite Persistence Layer (Chat History)

**What:** SQLite chat_messages table with Dapper mapping, following Phase 65 RunDb pattern exactly.

**When to use:** For durable full-history storage with per-Anima scoping and immediate writes on message completion.

**Key decisions:**
1. **Same folder as runs.db** (easier discovery, centralized database location)
2. **Immediate writes** — no batching. User sends message → write immediately. Assistant stream ends → write full message with token counts
3. **Full history stored** — never delete, only truncate on LLM context preparation
4. **tool_calls_json** stores tool call metadata as JSON string (denormalized for simplicity; Phase 68 visibility work can extend)

**Example:**

```csharp
// Source: Dapper + SQLite pattern from Phase 65
public class ChatHistoryService
{
    private readonly ChatDbConnectionFactory _factory;
    private readonly ILogger<ChatHistoryService> _logger;

    public ChatHistoryService(ChatDbConnectionFactory factory, ILogger<ChatHistoryService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    // Write user or assistant message immediately after completion
    public async Task StoreMessageAsync(string animaId, string role, string content,
        List<ToolCall> toolCalls, int inputTokens, int outputTokens, CancellationToken ct)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var toolCallsJson = toolCalls.Any() ? JsonSerializer.Serialize(toolCalls) : null;

        await conn.ExecuteAsync(
            @"INSERT INTO chat_messages (anima_id, role, content, tool_calls_json, input_tokens, output_tokens, created_at)
              VALUES (@AnimaId, @Role, @Content, @ToolCallsJson, @InputTokens, @OutputTokens, @CreatedAt)",
            new { AnimaId = animaId, Role = role, Content = content, ToolCallsJson = toolCallsJson,
                  InputTokens = inputTokens, OutputTokens = outputTokens, CreatedAt = DateTime.UtcNow },
            cancellationToken: ct);

        _logger.LogDebug("Stored {Role} message for Anima {AnimaId}", role, animaId);
    }

    // Restore all messages for Anima, including [interrupted] flag
    public async Task<List<ChatSessionMessage>> LoadHistoryAsync(string animaId, CancellationToken ct)
    {
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<ChatMessageRow>(
            @"SELECT role, content, tool_calls_json, input_tokens, output_tokens
              FROM chat_messages
              WHERE anima_id = @AnimaId
              ORDER BY created_at ASC",
            new { AnimaId = animaId },
            cancellationToken: ct);

        return rows.Select(r => new ChatSessionMessage
        {
            Role = r.Role,
            Content = r.Content,
            IsStreaming = false,
            ToolCalls = r.ToolCallsJson != null ?
                JsonSerializer.Deserialize<List<ToolCallInfo>>(r.ToolCallsJson) ?? new() : new()
        }).ToList();
    }
}

public record ChatMessageRow
{
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
    public string? ToolCallsJson { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
}
```

### Pattern 2: JSON Viewport Persistence

**What:** Simple per-Anima viewport state (scale, panX, panY) stored as `{animaId}.viewport.json` alongside `{name}.json` wiring files.

**When to use:** Fast, human-readable restore of visual editor position. No complex schema needed.

**Key decisions:**
1. **Filename is `{animaId}.viewport.json`** (matches anima_id, not config name, for per-Anima identity)
2. **1000ms debounce** (pan/zoom events are higher frequency; 1000ms is reasonable for viewport-only)
3. **Debounce timing independent from config auto-save** (viewport is separate concern)
4. **Restore on Editor component mount** or when switching Animas

**Example:**

```csharp
// Source: ConfigurationLoader pattern + EditorStateService debounce
public record ViewportState
{
    public double Scale { get; init; } = 1.0;
    public double PanX { get; init; } = 0;
    public double PanY { get; init; } = 0;
}

public class ViewportStateService
{
    private readonly string _configDirectory;
    private readonly ILogger<ViewportStateService> _logger;
    private CancellationTokenSource? _viewportDebounce;

    public ViewportStateService(string configDirectory, ILogger<ViewportStateService> logger)
    {
        _configDirectory = configDirectory;
        _logger = logger;
    }

    public async Task<ViewportState> LoadAsync(string animaId, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_configDirectory, $"{animaId}.viewport.json");

        if (!File.Exists(filePath))
        {
            return new ViewportState(); // default: scale 1.0, pan (0,0)
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var state = await JsonSerializer.DeserializeAsync<ViewportState>(stream, cancellationToken: ct);
            return state ?? new ViewportState();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load viewport for Anima {AnimaId}, using defaults", animaId);
            return new ViewportState();
        }
    }

    public async void TriggerSaveViewport(string animaId, double scale, double panX, double panY)
    {
        // Cancel previous debounce
        _viewportDebounce?.Cancel();
        _viewportDebounce?.Dispose();
        _viewportDebounce = new CancellationTokenSource();

        try
        {
            await Task.Delay(1000, _viewportDebounce.Token);

            var viewport = new ViewportState { Scale = scale, PanX = panX, PanY = panY };
            var filePath = Path.Combine(_configDirectory, $"{animaId}.viewport.json");

            if (!Directory.Exists(_configDirectory))
            {
                Directory.CreateDirectory(_configDirectory);
            }

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, viewport,
                new JsonSerializerOptions { WriteIndented = true },
                _viewportDebounce.Token);

            _logger.LogDebug("Saved viewport for Anima {AnimaId}: scale={Scale}, pan=({PanX},{PanY})",
                animaId, scale, panX, panY);
        }
        catch (OperationCanceledException)
        {
            // Debounce was cancelled, ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save viewport for Anima {AnimaId}", animaId);
        }
    }
}
```

### Pattern 3: Token-Budget-Based Context Truncation

**What:** Walk chat history from newest to oldest, accumulating token counts until budget exhausted.

**When to use:** Preparing chat history for LLM — limit context window to configurable token budget while keeping most recent messages.

**Integration with ChatContextManager:**

```csharp
// Extend existing ChatContextManager
public class ChatContextManager
{
    private readonly int _llmContextBudget; // Per-Anima, loaded from Settings at startup

    /// <summary>
    /// Truncate chat history to fit within LLM context budget.
    /// Walk from tail (newest) backward, stop when budget exceeded.
    /// Returns messages in chronological order (oldest first).
    /// </summary>
    public List<ChatSessionMessage> TruncateHistoryToContextBudget(
        List<ChatSessionMessage> fullHistory)
    {
        if (fullHistory.Count == 0)
            return new();

        int tokensBudget = _llmContextBudget;
        int tokensUsed = 0;
        var selectedMessages = new List<ChatSessionMessage>();

        // Walk backward (newest to oldest)
        for (int i = fullHistory.Count - 1; i >= 0; i--)
        {
            var msg = fullHistory[i];
            int msgTokens = _tokenCounter.CountTokens(msg.Content);

            if (tokensUsed + msgTokens > tokensBudget && selectedMessages.Count > 0)
            {
                // Budget exceeded; keep what we have
                break;
            }

            selectedMessages.Insert(0, msg); // prepend to maintain order
            tokensUsed += msgTokens;
        }

        _logger.LogDebug(
            "Truncated history: {FullCount} → {SelectedCount} messages, {Tokens} tokens",
            fullHistory.Count, selectedMessages.Count, tokensUsed);

        return selectedMessages;
    }
}
```

### Pattern 4: On-Startup Integration (Anima Switch)

**What:** When Anima is loaded or switched, restore chat history and viewport from persistent storage.

**When to use:** Application startup or user clicks different Anima in sidebar.

**Integration points:**

```csharp
// OnAnimaChanged event in ChatPanel.razor (existing)
private void OnAnimaChanged()
{
    _chatSessionState.Messages.Clear(); // OLD: just clear

    // NEW: restore from SQLite
    _ = RestoreChatHistoryAsync();
    InvokeAsync(StateHasChanged);
}

private async Task RestoreChatHistoryAsync()
{
    var activeAnimaId = _animaContext.ActiveAnimaId;
    if (string.IsNullOrEmpty(activeAnimaId))
        return;

    var messages = await _chatHistoryService.LoadHistoryAsync(activeAnimaId, CancellationToken.None);
    _chatSessionState.Messages.Clear();
    _chatSessionState.Messages.AddRange(messages);

    // Auto-scroll to bottom
    await JS.InvokeVoidAsync("scrollChatToBottom", "chat-messages");
}

// In Editor.razor (or EditorStateService)
protected override async Task OnInitializedAsync()
{
    var viewport = await _viewportStateService.LoadAsync(_animaContext.ActiveAnimaId);
    _editorState.UpdatePan(viewport.PanX, viewport.PanY);
    _editorState.UpdateScale(viewport.Scale);
    // Re-render to apply viewport
    StateHasChanged();
}
```

### Pattern 5: Debounce Trigger on Viewport Change

**What:** Hook UpdatePan / UpdateScale in EditorStateService to trigger async viewport save (not sync).

**When to use:** After pan/zoom state changes.

**Integration:**

```csharp
// In EditorStateService
public void UpdatePan(double panX, double panY)
{
    PanX = panX;
    PanY = panY;
    // Trigger async debounce save
    TriggerViewportSave();
}

public void UpdateScale(double scale)
{
    Scale = Math.Clamp(scale, 0.1, 3.0);
    NotifyStateChanged();
    // Trigger async debounce save
    TriggerViewportSave();
}

private async void TriggerViewportSave()
{
    var activeId = _animaContext.ActiveAnimaId;
    if (activeId != null)
    {
        _viewportStateService.TriggerSaveViewport(activeId, Scale, PanX, PanY);
    }
}
```

### Anti-Patterns to Avoid

- **Storing full chat history in Settings/Config files:** Use SQLite instead. Chat grows unbounded; JSON files don't scale.
- **Truncating chat history at write time:** Store full history, truncate only on LLM consumption. Users expect scrollback to work.
- **Message-count-based context limits:** Use token counts. Message size varies wildly; token budget is what LLMs actually care about.
- **Saving viewport on *every* pan/zoom event:** Use debounce. Pan/zoom events fire frequently; 1000ms debounce prevents write thrashing.
- **Mixing viewport and config auto-save timing:** Keep separate debounces. Different frequencies and concerns.
- **Restoring chat history to ChatSessionState at app startup:** Restore only when Anima is switched/loaded. Preserves existing session if not switching Animas.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|------------|-------------|-----|
| SQLite connection pooling & timeouts | Custom connection manager | RunDbConnectionFactory + Busy Timeout=5000 | Phase 65 proven, prevents concurrent write failures, handles connection lifecycle |
| Async file I/O with proper stream disposal | Manual file ops with try/catch | System.Text.Json + `await using` streams | Language built-in, prevents handle leaks |
| Debounce logic with cancellation | Timer + flag tracking | CancellationTokenSource swap + Task.Delay | Existing pattern in EditorStateService, proven in production |
| JSON serialization for viewports | Manual JSON string building | JsonSerializer with options | Standard library, handles camelCase, indentation automatically |
| Database schema migration & idempotence | Custom DDL scripts | RunDbInitializer pattern (IF NOT EXISTS on all statements) | Single atomic transaction, automatic backup, proven migration path |
| Token counting for context truncation | Character count / message count approximation | ChatContextManager._tokenCounter (existing) | Accurate LLM context math, already integrated, prevents context overrun |
| Per-Anima state scoping | Application-level singletons with manual checks | `anima_id` column + WHERE clauses | Proven memory schema pattern, scales to many Animas |

**Key insight:** SQLite with Dapper is not optional — chat history is effectively permanent (survives restarts), so SQL is the right storage model. JSON suffices for viewport-only state because viewport is transient (reset on new session anyway) and small.

---

## Common Pitfalls

### Pitfall 1: Chat History Lost on App Restart Without Persist Trigger

**What goes wrong:** Developer forgets to call `ChatHistoryService.StoreMessageAsync()` when message is complete, or timing is wrong (writes happen after app shuts down). User closes app mid-conversation; history appears empty on restart.

**Why it happens:** No explicit hook — need to intercept message completion in ChatPanel / ChatOutputModule and persist immediately. Easy to miss if pattern isn't established upfront.

**How to avoid:**
1. Establish the rule: "Every message (user or assistant) is persisted immediately after completion, before UI state change."
2. Create a single `ChatHistoryService.StoreMessageAsync()` call in ChatPanel's `SendMessage()` method (user messages) and `HandleChatOutputReceived()` (assistant messages).
3. Test: Verify chat.db has rows after each message, even if app crashes.

**Warning signs:**
- Chat history empty after restart despite seeing messages before shutdown
- chat.db file doesn't grow after sending messages
- Logs show no "Stored {Role} message" debug entries

### Pitfall 2: Viewport State Not Restored on Anima Load

**What goes wrong:** User closes editor with viewport at pan (100, 200), scale 1.5. Opens app, switches to same Anima. Viewport resets to (0, 0), scale 1.0 instead of restoring.

**Why it happens:** Restore logic only runs once on Editor component mount, but Editor doesn't remount on Anima switch (it re-renders in place). Need explicit handler on ActiveAnimaChanged event.

**How to avoid:**
1. Add `_animaContext.ActiveAnimaChanged += OnAnimaChanged` in Editor.razor OnInitialized
2. In OnAnimaChanged, call `_editorState.LoadViewport()` to restore state
3. Test: Switch Animas in sidebar, verify viewport matches last position

**Warning signs:**
- Viewport always defaults to (0,0)/1.0 after switching Animas
- `.viewport.json` files exist on disk but are never loaded
- Logs show no viewport restore activity

### Pitfall 3: Token Budget Not Respected — LLM Context Explodes

**What goes wrong:** Full chat history (with 50 messages @ 200 tokens each = 10K tokens) is sent to LLM with 8K token budget. LLM truncates server-side, losing recent context.

**Why it happens:** Truncation logic runs after history is loaded, not before. Or token budget default is too high / not per-Anima. Or truncation uses message count instead of tokens.

**How to avoid:**
1. **Always truncate before sending to LLM.** Not after. Don't rely on server truncation.
2. **Use token counts, not message counts.** Walk from newest backward, accumulate tokens, stop when budget exceeded.
3. **Make budget configurable per Anima, with sensible default (e.g., 4000 tokens = ~20% of typical 128K context).**
4. Test: Chat with 50+ messages, verify only last N messages sent to LLM by inspecting prompt in logs.

**Warning signs:**
- "context_length_exceeded" errors from API
- Very old messages appearing in LLM response (indicating full history was sent)
- Token counts in logs exceed budget
- No truncation logic visible in LLM prompt construction

### Pitfall 4: [interrupted] Flag Never Applied or Confusing to Users

**What goes wrong:** App crashes mid-stream. User restarts, sees message continued where it left off, but doesn't realize it was interrupted. Labels like "[interrupted]" are missing or unclear.

**Why it happens:** Easy to forget the "mark interrupted messages" step. Or visual styling is too subtle (same color as normal messages).

**How to avoid:**
1. On restore: if `IsStreaming == true`, append " **[interrupted]**" to message content or add a visual badge.
2. Use distinct visual treatment: different background color, warning icon, or info banner above message.
3. Test: Kill app while assistant is streaming. Restart, verify label is visible and clear.

**Warning signs:**
- No [interrupted] label visible in chat
- Users report confusion about incomplete messages
- No visual distinction from normal messages

### Pitfall 5: chat.db Locked / Concurrent Write Failures

**What goes wrong:** Multiple parts of code try to write to chat.db simultaneously (e.g., user message write + auto-sedimentation write). SQLite locks, times out, messages are lost.

**Why it happens:** Busy Timeout set too low or not set at all. No coordination between write paths.

**How to avoid:**
1. **Always use `ChatDbConnectionFactory` with Busy Timeout=5000 (same as runs.db).** Copy the constructor exactly.
2. **Keep writes simple and fast.** One INSERT per message, no complex transactions spanning multiple operations.
3. **Test: Intentionally write from two async tasks simultaneously, verify no errors and both writes succeed.**

**Warning signs:**
- "database is locked" errors in logs
- Messages disappearing without error
- SQLite lock timeouts
- No chat.db writes despite successful message send

### Pitfall 6: Settings Token Budget Field Ignored or Wrong Type

**What goes wrong:** User sets token budget to 5000 in Settings, but LLM still receives full 10K token history.

**Why it happens:** Settings value not read on startup. Or read but cached at app startup and never refreshed. Or field is string, not int, causing parse errors.

**How to avoid:**
1. **Store token budget in a per-Anima config store or Settings table, NOT in appsettings.json or LLMOptions.**
2. **Load on Anima switch, not on app startup.** Token budget is per-Anima, not global.
3. **Type-safe: Use int property, validate range (e.g., 1000-128000).**
4. Test: Change token budget in Settings, send new message, verify LLM receives truncated history matching new budget.

**Warning signs:**
- Settings field is string/nullable and shows "null" in logs
- Token budget change in Settings has no effect
- LLM context size doesn't change when Settings value changes

---

## Code Examples

Verified patterns from production code:

### Example 1: SQLite Factory + Initializer Setup (from Phase 65 pattern)

```csharp
// Source: RunDbConnectionFactory + DependencyInjection/AnimaServiceExtensions
public class ChatDbConnectionFactory
{
    private readonly string _connectionString;

    public ChatDbConnectionFactory(string dbPath)
    {
        _connectionString = $"Data Source={dbPath};Busy Timeout=5000";
    }

    public SqliteConnection CreateConnection() =>
        new SqliteConnection(_connectionString);
}

// In AnimaServiceExtensions.cs
services.AddSingleton(provider =>
{
    var runsDbPath = Path.Combine(dataDir, "chat.db");
    return new ChatDbConnectionFactory(runsDbPath);
});

services.AddSingleton(provider =>
{
    var factory = provider.GetRequiredService<ChatDbConnectionFactory>();
    var logger = provider.GetRequiredService<ILogger<ChatDbInitializer>>();
    return new ChatDbInitializer(factory, logger);
});

// On app startup (in hosted service or Program.cs)
var initializer = services.GetRequiredService<ChatDbInitializer>();
await initializer.EnsureCreatedAsync();
```

### Example 2: Chat Message Write Trigger (from ChatPanel.razor pattern)

```csharp
// In ChatPanel.razor or extracted service
private async Task SendMessage(string userInput)
{
    if (string.IsNullOrWhiteSpace(userInput)) return;

    // 1. Add user message to UI state
    _chatSessionState.Messages.Add(new ChatSessionMessage
    {
        Role = "user",
        Content = userInput,
        IsStreaming = false
    });

    // 2. Persist user message immediately
    var tokens = _contextManager.TotalInputTokens;
    await _chatHistoryService.StoreMessageAsync(
        _animaContext.ActiveAnimaId,
        role: "user",
        content: userInput,
        toolCalls: new(),
        inputTokens: tokens,
        outputTokens: 0,
        _cancellationToken);

    // 3. Send to LLM (existing logic)
    await _contextManager.SendMessageAsync(userInput);

    // ... LLM response handling ...
}

// After LLM stream completes
private void OnAssistantMessageComplete(string fullResponse, int outputTokens)
{
    // Mark message as complete
    var lastMsg = _chatSessionState.Messages.LastOrDefault(m => m.Role == "assistant" && m.IsStreaming);
    if (lastMsg != null)
    {
        lastMsg.IsStreaming = false;
        lastMsg.Content = fullResponse;
    }

    // Persist assistant message
    _ = _chatHistoryService.StoreMessageAsync(
        _animaContext.ActiveAnimaId,
        role: "assistant",
        content: fullResponse,
        toolCalls: lastMsg?.ToolCalls ?? new(),
        inputTokens: 0,
        outputTokens: outputTokens,
        _cancellationToken);
}
```

### Example 3: Viewport Restore on Anima Load (from EditorStateService + ConfigurationLoader pattern)

```csharp
// In Editor.razor or new EditorInitializer component
protected override async Task OnInitializedAsync()
{
    var animaId = _animaContext.ActiveAnimaId;
    if (string.IsNullOrEmpty(animaId))
        return;

    // Load wiring config (existing)
    var config = await _configLoader.LoadAsync("default");
    _editorState.LoadConfiguration(config);

    // NEW: Load viewport state
    var viewport = await _viewportStateService.LoadAsync(animaId);
    _editorState.UpdatePan(viewport.PanX, viewport.PanY);
    _editorState.UpdateScale(viewport.Scale);

    StateHasChanged();
}

// Hook pan/zoom to save trigger
private void OnPanChange(double panX, double panY)
{
    _editorState.UpdatePan(panX, panY);
    _viewportStateService.TriggerSaveViewport(
        _animaContext.ActiveAnimaId,
        _editorState.Scale,
        _editorState.PanX,
        _editorState.PanY);
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Chat history in-memory only (ChatSessionState.Messages) | Dual storage: SQLite full history + in-memory UI cache | Phase 66 | Chat survives app restart; users see scrollback |
| Viewport lost on restart, manual pan/zoom after load | Per-Anima viewport JSON restore | Phase 66 | Visual editor restores position instantly |
| LLM context = all messages since session start | Token-budget-based truncation, configurable per Anima | Phase 66 | Context window stays within budget, most recent messages preserved |
| Settings → LLMModule config → appsettings.json (three layers) | Settings → Per-Anima config store (clean separation) | Phase 66 | Token budget is Anima-specific, not LLM-global |

**Deprecated/outdated:**
- Storing chat history as JSON files — SQLite is the standard for durable message logs
- Message-count limits (e.g., "keep last 10 messages") — token budgets are LLM-aware
- Viewport in config.json — separate JSON file is cleaner, faster to update without validation

---

## Open Questions

1. **chat.db location: same directory as runs.db or config directory?**
   - *What we know:* Phase 65 puts runs.db in a fixed data directory; config files go in per-Anima config directory. Phase 66 CONTEXT.md lists this as Claude's Discretion.
   - *What's unclear:* Grouping databases vs. distributed storage per concern.
   - *Recommendation:* **Same directory as runs.db (centralized database location).** Easier discovery, single backup target, clearer intent ("data" folder). Config files stay separate because they're text + version-controlled; databases are binary + derived.

2. **tool_calls_json schema: store ToolCallInfo objects or simplified summary?**
   - *What we know:* ChatSessionMessage.ToolCalls is `List<ToolCallInfo>`. Phase 68 will add memory visibility for tool calls.
   - *What's unclear:* Whether to store full ToolCallInfo (with parameters, status, result) or just name + summary for chat recall.
   - *Recommendation:* **Store full ToolCallInfo as JSON.** Phase 68 memory visibility will need details. Cost is minimal (few KB per message). Simplification is premature.

3. **[interrupted] label rendering: badge, suffix, or banner?**
   - *What we know:* CONTEXT.md says "labeled **[interrupted]**" but leaves visual treatment to Claude's Discretion.
   - *What's unclear:* CSS styling, position in message, color, icon.
   - *Recommendation:* **Suffix + subtle badge styling.** Append " **[incomplete]**" to content, render in orange/warning color. Matches existing warning styling in RunDetail for incomplete runs.

4. **Token budget default value?**
   - *What we know:* LLMOptions.MaxContextTokens defaults to 128000 (global LLM context). Phase 66 token budget is per-Anima, separate.
   - *What's unclear:* Sensible per-Anima default. Too low (1000) loses context. Too high (50000) defeats truncation purpose.
   - *Recommendation:* **Default: 4000 tokens (~20% of typical LLM context window).** Allows ~20 messages of typical length while keeping recent context. Configurable in Settings, with validation range 1000-128000.

---

## Validation Architecture

> Note: `.planning/config.json` does not have an explicit `workflow.nyquist_validation` key. Default behavior: validation enabled.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (existing) |
| Config file | None — tests in `tests/OpenAnima.Tests/` directory |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ -k "ChatHistory or Viewport" --no-build` |
| Full suite command | `dotnet test --no-build` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PERS-01 | Viewport (scale, pan) saved to `{animaId}.viewport.json` on pan/zoom | Unit | `dotnet test tests/OpenAnima.Tests/ -k "ViewportSave" -x` | ❌ Wave 0 |
| PERS-01 | Viewport state restored from file on Editor init | Unit | `dotnet test tests/OpenAnima.Tests/ -k "ViewportRestore" -x` | ❌ Wave 0 |
| PERS-02 | Chat message written to chat.db immediately after completion (user role) | Unit | `dotnet test tests/OpenAnima.Tests/ -k "ChatMessageStore" -x` | ❌ Wave 0 |
| PERS-02 | Chat message written to chat.db immediately after completion (assistant role) | Unit | `dotnet test tests/OpenAnima.Tests/ -k "ChatMessageAssistant" -x` | ❌ Wave 0 |
| PERS-02 | Full chat history restored from chat.db on Anima load | Unit | `dotnet test tests/OpenAnima.Tests/ -k "ChatHistoryRestore" -x` | ❌ Wave 0 |
| PERS-02 | Interrupted messages (IsStreaming=true) restored with [interrupted] suffix | Unit | `dotnet test tests/OpenAnima.Tests/ -k "InterruptedMessage" -x` | ❌ Wave 0 |
| PERS-03 | Token budget loaded from Settings on Anima init | Unit | `dotnet test tests/OpenAnima.Tests/ -k "TokenBudgetLoad" -x` | ❌ Wave 0 |
| PERS-03 | Chat history truncated to token budget before sending to LLM | Unit | `dotnet test tests/OpenAnima.Tests/ -k "ContextTruncation" -x` | ❌ Wave 0 |
| PERS-03 | Token budget configurable in Settings UI (default 4000) | Integration | `dotnet test tests/OpenAnima.Tests/ -k "SettingsTokenBudget" -x` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ -k "ChatHistory or Viewport" --no-build -x` (chat & viewport unit tests only)
- **Per wave merge:** `dotnet test --no-build` (full suite, 662 tests)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `tests/OpenAnima.Tests/ChatPersistence/ChatHistoryServiceTests.cs` — covers PERS-02 (store, restore, interrupted)
- [ ] `tests/OpenAnima.Tests/ViewportPersistence/ViewportStateServiceTests.cs` — covers PERS-01 (save, load, debounce)
- [ ] `tests/OpenAnima.Tests/Services/ChatContextManagerTests.cs` — add tests for PERS-03 (token-budget truncation)
- [ ] `tests/OpenAnima.Tests/Integration/ChatPersistenceIntegrationTests.cs` — end-to-end chat.db lifecycle
- [ ] `tests/OpenAnima.Tests/Integration/ViewportPersistenceIntegrationTests.cs` — end-to-end viewport.json lifecycle
- [ ] ChatDbInitializer class — schema setup
- [ ] IModuleConfigStore implementation or extend for per-Anima token budget config

---

## Sources

### Primary (HIGH confidence)

- **Phase 65 completed codebase**
  - RunDbConnectionFactory (singleton, Busy Timeout=5000, IAsyncDisposable pattern)
  - RunDbInitializer (IF NOT EXISTS idempotence, atomic migrations, Dapper + Transactions)
  - Memory schema (per-Anima scoping via anima_id column, UUID PKs, four-table model)

- **Official C# / .NET documentation**
  - SqliteConnection: [Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite)
  - System.Text.Json: [JSON serialization](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview)
  - CancellationTokenSource: [async cancellation patterns](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource)

- **Production code inventory**
  - EditorStateService: Debounce pattern (CancellationTokenSource swap, async void TriggerAutoSave, Task.Delay with token)
  - ConfigurationLoader: JSON save/load (Directory.CreateDirectory, Path.Combine, JsonSerializerOptions)
  - ChatContextManager: Token counting (existing _tokenCounter, TotalInputTokens/TotalOutputTokens tracking)
  - ChatSessionState: In-memory message list (List<ChatSessionMessage>, Role/Content/IsStreaming/ToolCalls)

### Secondary (MEDIUM confidence)

- CONTEXT.md decisions (2026-03-26): All Locked Decisions section verified against code patterns
- REQUIREMENTS.md PERS-01/02/03: Phase 66 scope confirmed, dependencies on Phase 65 confirmed
- PROJECT.md: Architecture references (per-Anima scoping, independent EventBus/WiringEngine, config directory resolution)

### Tertiary (validated as current)

- Dapper NuGet package: [Dapper documentation](https://github.com/DapperLib/Dapper) — standard micro-ORM for .NET
- SQLite PRAGMA journal_mode=WAL: [SQLite WAL docs](https://www.sqlite.org/wal.html) — concurrent read/write safe mode

---

## Metadata

**Confidence breakdown:**
- **Standard stack: HIGH** — All components derived directly from Phase 65 proven code (RunDbConnectionFactory, RunDbInitializer, Dapper), verified in codebase
- **Architecture: HIGH** — Patterns directly lifted from existing EditorStateService (debounce), ConfigurationLoader (JSON I/O), ChatSessionState (message list), ChatContextManager (token tracking)
- **Pitfalls: MEDIUM-HIGH** — Based on common SQLite concurrency issues (Busy Timeout), state restoration patterns (known failure modes), token counting logic (research verified)

**Research date:** 2026-03-26
**Valid until:** 2026-04-26 (30 days — architecture is stable, no major framework changes expected)

**Next phase dependency:** Phase 67 (Memory Tools) depends on chat.db schema for sedimentation input; Phase 69 (Chat Resilience) depends on chat history restore mechanism
