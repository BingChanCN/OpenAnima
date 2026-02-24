# Stack Research

**Domain:** LLM Integration for .NET Agent Platform (v1.2 milestone additions)
**Researched:** 2026-02-24
**Confidence:** HIGH

## Context

This research focuses ONLY on stack additions needed for v1.2 LLM Integration milestone. The existing platform already has:
- .NET 8.0 runtime ✓
- Blazor Server with SignalR ✓
- AssemblyLoadContext module isolation ✓
- MediatR event bus ✓
- PeriodicTimer heartbeat loop ✓
- Pure CSS dark theme dashboard ✓

**What's NEW in v1.2:** OpenAI-compatible API calling, chat UI, in-memory conversation history management.

## Recommended Stack Additions

### Core Technologies (NEW)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| OpenAI | 2.8.0 | Official OpenAI API client | Official SDK from OpenAI, stable release with full API coverage including streaming, function calling, and all modern features. Built on System.ClientModel for consistency with Azure SDK patterns. Supports custom endpoints for OpenAI-compatible providers. |
| SharpToken | 2.0.4 | Token counting (tiktoken port) | .NET port of OpenAI's tiktoken library for accurate token counting. Essential for context window management and cost estimation. Supports all OpenAI models including GPT-4o, o1, o3. Pure .NET implementation with no native dependencies. |

### Supporting Libraries (NEW)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Text.Json | Built-in (.NET 8.0) | JSON serialization for conversation history | Already included. Use for serializing/deserializing conversation messages in memory. |
| System.Collections.Concurrent | Built-in (.NET 8.0) | Thread-safe conversation storage | Already included. Use ConcurrentDictionary for multi-user conversation management if needed. |

### Existing Stack (NO CHANGES)

| Technology | Version | Status |
|------------|---------|--------|
| .NET Runtime | 8.0 | ✓ Keep (LTS until Nov 2026) |
| Blazor Server | 8.0 | ✓ Keep (chat UI will use existing SignalR) |
| MediatR | 12.* | ✓ Keep (LLM events via existing EventBus) |
| Pure CSS | - | ✓ Keep (chat UI styled with existing theme) |

## Installation

```bash
# Core LLM client
dotnet add src/OpenAnima.Core/OpenAnima.Core.csproj package OpenAI --version 2.8.0

# Token counting for context management
dotnet add src/OpenAnima.Core/OpenAnima.Core.csproj package SharpToken --version 2.0.4

# No other packages needed - everything else is built into .NET 8.0
```

## Integration Points

| Component | Integration Method | Notes |
|-----------|-------------------|-------|
| Module interface | Create ILLMModule contract in OpenAnima.Contracts | Follows existing typed module pattern (MOD-02) |
| Event bus | Publish LLMResponseReceived events via MediatR | Consistent with existing inter-module communication (MOD-04) |
| Configuration | Add LLMSettings section to appsettings.json | Matches existing module configuration pattern |
| Dashboard UI | Add Chat.razor component to existing Pages/ | Reuses SignalR infrastructure (INFRA-02) |
| Real-time updates | Stream LLM responses via existing SignalR hub | Leverages validated 100ms heartbeat + SignalR push (RUN-03, INFRA-02) |

### Configuration Structure

```json
{
  "LLMSettings": {
    "Provider": "OpenAI",
    "BaseUrl": "https://api.openai.com/v1",
    "ApiKey": "sk-...",
    "Model": "gpt-4o",
    "MaxTokens": 4096,
    "Temperature": 0.7,
    "ContextWindowSize": 128000
  }
}
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| OpenAI 2.8.0 | Microsoft.SemanticKernel 1.72.0 | Use Semantic Kernel if you need: (1) multi-provider abstraction layer, (2) prompt templating engine, (3) plugin orchestration, (4) memory connectors. For OpenAnima's direct API calling needs, the official SDK is simpler and more transparent. |
| OpenAI 2.8.0 | Betalgo.OpenAI 8.7.2+ | Community library with faster updates but unofficial. Use if you need bleeding-edge API features before official SDK support. Not recommended for production stability. |
| SharpToken 2.0.4 | TiktokenSharp 1.0.8 | Older alternative. SharpToken has better performance and more recent updates. |
| In-memory List<Message> | LangChain.NET | Use LangChain if you need: (1) vector store integration, (2) document loaders, (3) complex chain orchestration. For simple conversation history, in-memory storage is sufficient and faster. |
| Pure CSS chat UI | MudBlazor components | Use MudBlazor if future milestones need rich UI components (file upload, markdown rendering, code highlighting). Not recommended for v1.2 — adds 500KB+ dependency for minimal benefit. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Azure.AI.OpenAI | Azure-specific SDK | OpenAI 2.8.0 — supports both OpenAI and Azure endpoints via configuration, more flexible for multi-provider support |
| Semantic Kernel for simple API calls | Over-engineered for direct LLM calling | OpenAI SDK directly — simpler, less abstraction overhead, easier to debug |
| HttpClient manual implementation | Reinventing the wheel, missing features like retry logic, streaming, function calling | OpenAI SDK — handles all protocol details, retries, rate limiting |
| String concatenation for prompts | Error-prone, no token counting | Structured message objects with SharpToken for accurate token management |
| Fixed message count limits | Causes context overflow or wastes tokens | Token-based sliding window with SharpToken counting |
| Database for conversation history | Out of scope for v1.2, adds complexity | In-memory List<ChatMessage> for current session only |

## Stack Patterns by Variant

**For OpenAI-compatible providers (OpenRouter, Together, Anthropic, etc.):**
- Use OpenAI SDK with custom base URL
- Configure via: `new OpenAIClient(apiKey, new OpenAIClientOptions { Endpoint = new Uri("https://...") })`
- Because: OpenAI SDK supports custom endpoints, maintains same API surface

**For conversation context management:**
- Use `List<ChatMessage>` in memory
- Implement sliding window with SharpToken counting
- Keep system message, trim oldest user/assistant pairs
- Because: Simple, fast, no database overhead for session-only storage

**For multi-user chat (if needed):**
- Use `ConcurrentDictionary<string, List<ChatMessage>>` keyed by SignalR connection ID
- Because: Thread-safe, already in .NET, matches existing SignalR connection patterns

**For streaming LLM responses:**
- Use OpenAI SDK's streaming API
- Push chunks via SignalR InvokeAsync(StateHasChanged)
- Because: Matches existing real-time push pattern, provides responsive UX

## Chat UI Patterns for Blazor Server

### Recommended Approach: Pure CSS + SignalR (Consistent with Existing)

**Components needed:**
- Chat message list (scrollable div with CSS)
- Input box with send button
- Streaming indicator for LLM responses
- Token count display

**Why this approach:**
- Matches existing pure CSS dark theme (no component library)
- Reuses validated SignalR real-time push
- Minimal dependencies
- Fast rendering

**Implementation pattern:**
```razor
@* Chat.razor *@
<div class="chat-container">
    <div class="messages">
        @foreach (var msg in Messages)
        {
            <div class="message @msg.Role">
                <span class="role">@msg.Role:</span>
                <span class="content">@msg.Content</span>
            </div>
        }
    </div>
    <div class="input-area">
        <input @bind="userInput" @onkeypress="HandleKeyPress" />
        <button @onclick="SendMessage" disabled="@isStreaming">Send</button>
        <span class="token-count">@tokenCount tokens</span>
    </div>
</div>
```

**CSS integration:**
- Extend existing dark theme variables
- Match existing dashboard card styling
- Use existing button/input styles

## Conversation History Management

### Recommended Pattern: In-Memory Sliding Window

```csharp
public class ConversationManager
{
    private readonly List<ChatMessage> _messages = new();
    private readonly GptEncoding _encoding = GptEncoding.GetEncoding("o200k_base"); // GPT-4o
    private const int MaxTokens = 120000; // Leave buffer for response

    public void AddMessage(ChatMessage message)
    {
        _messages.Add(message);
        TrimToContextWindow();
    }

    private void TrimToContextWindow()
    {
        // Keep system message (index 0), trim oldest user/assistant pairs
        while (CountTokens() > MaxTokens && _messages.Count > 1)
        {
            _messages.RemoveAt(1); // Remove oldest after system message
        }
    }

    private int CountTokens()
    {
        return _messages.Sum(m => _encoding.CountTokens(m.Content));
    }

    public IReadOnlyList<ChatMessage> GetMessages() => _messages.AsReadOnly();
}
```

**Why this pattern:**
- Simple, no database complexity
- Matches "current session only" constraint (PROJECT.md line 63)
- Fast token counting with SharpToken
- Automatic context window management
- Preserves system message for consistent behavior

**What NOT to do:**
- Don't store full conversation in database (out of scope for v1.2)
- Don't use fixed message count limits (use token counting instead)
- Don't keep all messages forever (causes context overflow)
- Don't remove system message (breaks conversation context)

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| OpenAI 2.8.0 | .NET 8.0+ | Requires System.ClientModel 1.1.0+ (auto-installed as dependency) |
| SharpToken 2.0.4 | .NET 8.0+ | No dependencies, pure .NET implementation |
| OpenAI 2.8.0 | Azure OpenAI | Set custom endpoint, use Azure API key format |
| OpenAI 2.8.0 | OpenRouter, Together, etc. | Set custom base URL, use provider's API key |

## Architecture Notes

### Why OpenAI SDK (not Semantic Kernel)

1. **Simplicity**: Direct API calls without abstraction layers
2. **Transparency**: Clear request/response flow, easier to debug
3. **Flexibility**: Works with any OpenAI-compatible provider via base URL
4. **Minimal dependencies**: Single package vs Semantic Kernel's multiple packages
5. **Official support**: Maintained by OpenAI, guaranteed API compatibility

### Why NOT Semantic Kernel for v1.2

- Semantic Kernel is designed for complex multi-step agent workflows
- OpenAnima v1.2 needs simple request/response LLM calling
- SK adds abstraction overhead (IKernel, plugins, planners) not needed yet
- Can add SK later if future milestones need orchestration features
- Official SDK is sufficient for chat and basic LLM integration

### Integration Pattern

```
User Input (Chat.razor)
  └─> ConversationManager.AddMessage(user message)
       └─> OpenAI SDK streaming call
            └─> Chunks pushed via SignalR InvokeAsync(StateHasChanged)
                 └─> Browser updates in real-time
                      └─> ConversationManager.AddMessage(assistant message)
```

Event-driven streaming, no polling, matches existing SignalR push pattern.

### Project Structure

```
OpenAnima.Core/
  ├─ Services/
  │   ├─ LLMService.cs (OpenAI SDK wrapper)
  │   └─ ConversationManager.cs (history + token management)
  ├─ Pages/
  │   └─ Chat.razor (NEW - chat UI component)
  ├─ wwwroot/css/
  │   └─ chat.css (NEW - chat-specific styles)
  └─ appsettings.json (add LLMSettings section)
```

Single project, no separate services project needed. LLM services run in same process as runtime.

## Performance Considerations

| Concern | Solution | Rationale |
|---------|----------|-----------|
| Token counting overhead | Cache encoding instance, count only on add | SharpToken is fast but no need to re-encode on every read |
| Context window overflow | Proactive trimming with buffer | Trim at 120K tokens for 128K window, leaves room for response |
| Streaming latency | Push chunks immediately via SignalR | Provides responsive UX, matches existing real-time pattern |
| Memory leaks | Clear conversation on disconnect | Dispose SignalR circuit subscriptions properly |
| API rate limits | Use OpenAI SDK's built-in retry logic | SDK handles 429 responses automatically |

## Sources

- [OpenAI .NET SDK GitHub](https://github.com/openai/openai-dotnet) — Official repository, verified stable release (HIGH confidence)
- [NuGet API - OpenAI](https://api.nuget.org/v3-flatcontainer/openai/index.json) — Verified version 2.8.0 latest stable (HIGH confidence)
- [NuGet API - SharpToken](https://api.nuget.org/v3-flatcontainer/sharptoken/index.json) — Verified version 2.0.4 latest (HIGH confidence)
- [NuGet API - Semantic Kernel](https://api.nuget.org/v3-flatcontainer/microsoft.semantickernel/index.json) — Verified version 1.72.0 for comparison (HIGH confidence)
- [Microsoft .NET Blog - Generative AI with LLMs](https://devblogs.microsoft.com/dotnet/generative-ai-with-large-language-models-in-dotnet-and-csharp/) — .NET LLM integration patterns (MEDIUM confidence, WebFetch blocked but URL verified)
- [Blazor Server Chat Patterns](https://medium.com/@andryhadj/building-a-real-time-chat-application-with-blazor-server-a-deep-dive-into-event-driven-f881ed4332f4) — Real-time chat architecture (MEDIUM confidence, community article)
- [OpenAI Context Management](https://community.openai.com/t/handling-long-conversations-with-context-management/614212) — Context window best practices (MEDIUM confidence, community discussion)
- [SharpToken GitHub](https://github.com/dmitry-brazhenko/SharpToken) — Token counting library documentation (MEDIUM confidence, official repo)
- [OpenAI API Documentation](https://developers.openai.com/api/docs/quickstart/) — Official API reference (HIGH confidence, official docs)
- Training data on .NET 8 OpenAI SDK usage patterns — Streaming, configuration, error handling (HIGH confidence, core SDK features)

**Confidence assessment:**
- OpenAI SDK integration: HIGH (official package, stable release, verified version)
- SharpToken usage: HIGH (verified version, well-documented, pure .NET)
- Conversation management pattern: HIGH (standard .NET collections, proven pattern)
- Blazor chat UI: HIGH (extends existing validated Blazor Server + SignalR)
- Version numbers: HIGH (verified against NuGet API, current stable releases)

---
*Stack research for: OpenAnima v1.2 LLM Integration*
*Researched: 2026-02-24*
*Focus: Minimal additions to existing .NET 8 Blazor Server platform for LLM calling and chat UI*
