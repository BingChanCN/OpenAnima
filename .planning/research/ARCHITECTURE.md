# Architecture Research: LLM Integration

**Domain:** LLM API integration for agent platform
**Researched:** 2026-02-24
**Confidence:** HIGH

## Integration Overview

LLM capabilities integrate into existing OpenAnima architecture through three new components that follow established patterns:

```
┌─────────────────────────────────────────────────────────────┐
│                    Blazor UI Layer                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │   Monitor    │  │   Modules    │  │  Chat (NEW)  │       │
│  │   Page       │  │   Page       │  │   Page       │       │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘       │
│         │                 │                 │                │
├─────────┴─────────────────┴─────────────────┴────────────────┤
│                    SignalR Hub Layer                         │
│  ┌──────────────────────────────────────────────────────┐    │
│  │  RuntimeHub (existing) + IChatClient (NEW)           │    │
│  └──────────────────────────────────────────────────────┘    │
├─────────────────────────────────────────────────────────────┤
│                    Service Facade Layer                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │ ModuleService│  │HeartbeatSvc  │  │ ChatService  │       │
│  │  (existing)  │  │  (existing)  │  │    (NEW)     │       │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘       │
│         │                 │                 │                │
├─────────┴─────────────────┴─────────────────┴────────────────┤
│                    Core Runtime Layer                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │PluginRegistry│  │HeartbeatLoop │  │ConversationMgr│      │
│  │  (existing)  │  │  (existing)  │  │    (NEW)     │       │
│  └──────────────┘  └──────────────┘  └──────┬───────┘       │
│                                              │                │
│  ┌──────────────────────────────────────────┴───────┐        │
│  │           LLM Client Abstraction (NEW)           │        │
│  │  (OpenAI SDK wrapper with provider config)       │        │
│  └──────────────────────────────────────────────────┘        │
└─────────────────────────────────────────────────────────────┘
                            ↓
                    ┌───────────────┐
                    │  OpenAI API   │
                    │  (or compat)  │
                    └───────────────┘
```

## New Components

### Component Responsibilities

| Component | Responsibility | Integration Point |
|-----------|----------------|-------------------|
| **LlmClient** | OpenAI API wrapper, request/response handling, streaming | Singleton service, injected into ChatService |
| **ConversationManager** | In-memory conversation history, context window management, token counting | Singleton service, injected into ChatService |
| **ChatService** | Service facade for UI, orchestrates LlmClient + ConversationManager | Registered in Program.cs DI container |
| **Chat.razor** | Blazor page for chat UI, message display, input handling | New page in Components/Pages/ |
| **Chat.razor.cs** | Code-behind with SignalR hub connection for real-time updates | Follows Monitor.razor.cs pattern |
| **IChatClient** | SignalR typed client interface for server→client push | Extends existing hub pattern |

## Existing Components (Modified)

| Component | Modification | Reason |
|-----------|--------------|--------|
| **RuntimeHub** | Add chat-related server methods (SendMessage, ClearConversation) | Centralized SignalR hub for all real-time features |
| **Program.cs** | Register LlmClient, ConversationManager, ChatService as singletons | Follow existing DI pattern |
| **Navigation** | Add Chat link to main layout | Standard UI integration |

## Recommended Project Structure

```
src/OpenAnima.Core/
├── Services/
│   ├── IModuleService.cs          # Existing
│   ├── ModuleService.cs           # Existing
│   ├── IHeartbeatService.cs       # Existing
│   ├── HeartbeatService.cs        # Existing
│   ├── IChatService.cs            # NEW - facade interface
│   └── ChatService.cs             # NEW - orchestrates LLM + conversation
├── Llm/                           # NEW folder
│   ├── ILlmClient.cs              # NEW - abstraction for LLM providers
│   ├── OpenAiClient.cs            # NEW - OpenAI SDK wrapper
│   ├── LlmOptions.cs              # NEW - configuration (API key, model, etc.)
│   ├── IConversationManager.cs    # NEW - conversation history interface
│   ├── ConversationManager.cs     # NEW - in-memory history + context window
│   ├── ConversationMessage.cs     # NEW - message model (role, content, tokens)
│   └── TokenCounter.cs            # NEW - token estimation utility
├── Hubs/
│   ├── RuntimeHub.cs              # MODIFIED - add chat methods
│   ├── IRuntimeClient.cs          # Existing
│   └── IChatClient.cs             # NEW - typed client for chat push
├── Components/
│   └── Pages/
│       ├── Monitor.razor          # Existing
│       ├── Modules.razor          # Existing
│       ├── Chat.razor             # NEW - chat UI
│       └── Chat.razor.cs          # NEW - code-behind with SignalR
└── Program.cs                     # MODIFIED - register new services
```

### Structure Rationale

- **Services/:** Service facades follow existing pattern (IModuleService, IHeartbeatService). ChatService fits naturally here.
- **Llm/:** New folder isolates LLM-specific logic. Keeps core runtime clean. Easy to extend with new providers later.
- **Hubs/:** RuntimeHub is the single SignalR hub. Adding chat methods keeps real-time communication centralized.
- **Components/Pages/:** Chat.razor follows existing page pattern (Monitor.razor, Modules.razor).

## Architectural Patterns

### Pattern 1: Service Facade with DI

**What:** Service layer abstracts business logic from UI. Blazor pages inject services, not direct dependencies.

**When to use:** Already established in OpenAnima (ModuleService, HeartbeatService). ChatService follows same pattern.

**Trade-offs:**
- Pro: Clean separation, testable, follows existing conventions
- Pro: Easy to mock for testing
- Con: Extra layer of indirection (acceptable for consistency)

**Example:**
```csharp
// Services/IChatService.cs
public interface IChatService
{
    Task<string> SendMessageAsync(string userMessage, CancellationToken ct = default);
    Task ClearConversationAsync();
    IReadOnlyList<ConversationMessage> GetHistory();
}

// Services/ChatService.cs
public class ChatService : IChatService
{
    private readonly ILlmClient _llmClient;
    private readonly IConversationManager _conversationManager;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ILlmClient llmClient,
        IConversationManager conversationManager,
        ILogger<ChatService> logger)
    {
        _llmClient = llmClient;
        _conversationManager = conversationManager;
        _logger = logger;
    }

    public async Task<string> SendMessageAsync(string userMessage, CancellationToken ct = default)
    {
        _conversationManager.AddUserMessage(userMessage);
        var messages = _conversationManager.GetMessagesForApi();
        var response = await _llmClient.GetCompletionAsync(messages, ct);
        _conversationManager.AddAssistantMessage(response);
        return response;
    }
}

// Program.cs registration
builder.Services.AddSingleton<IChatService, ChatService>();
```

### Pattern 2: SignalR Hub with Typed Clients

**What:** RuntimeHub uses typed client interfaces (IRuntimeClient, IChatClient) for compile-time safety on server→client calls.

**When to use:** Already used for heartbeat/module updates. Extend for chat real-time updates.

**Trade-offs:**
- Pro: Type-safe, IntelliSense support, compile-time errors
- Pro: Consistent with existing RuntimeHub pattern
- Con: Requires interface definition for each client method

**Example:**
```csharp
// Hubs/IChatClient.cs
public interface IChatClient
{
    Task ReceiveMessage(string role, string content, DateTime timestamp);
    Task ReceiveConversationCleared();
}

// Hubs/RuntimeHub.cs (extended)
public class RuntimeHub : Hub<IRuntimeClient>
{
    private readonly IChatService _chatService;
    private readonly IHubContext<RuntimeHub, IChatClient> _chatHubContext;

    public async Task SendChatMessage(string message)
    {
        var response = await _chatService.SendMessageAsync(message);
        await Clients.All.ReceiveMessage("assistant", response, DateTime.UtcNow);
    }
}
```

### Pattern 3: In-Memory State with Singleton Lifetime

**What:** ConversationManager holds conversation history in memory (ConcurrentDictionary or similar). Singleton lifetime means state persists across requests.

**When to use:** v1.2 milestone explicitly scopes to in-memory, session-only. No database complexity.

**Trade-offs:**
- Pro: Simple, fast, no database setup
- Pro: Matches existing PluginRegistry pattern (also in-memory singleton)
- Con: State lost on restart (acceptable for v1.2)
- Con: Not suitable for multi-user (v1.2 is single-user)

**Example:**
```csharp
// Llm/ConversationManager.cs
public class ConversationManager : IConversationManager
{
    private readonly List<ConversationMessage> _messages = new();
    private readonly int _maxTokens;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public void AddUserMessage(string content)
    {
        _lock.Wait();
        try
        {
            _messages.Add(new ConversationMessage("user", content, EstimateTokens(content)));
            TrimToContextWindow();
        }
        finally
        {
            _lock.Release();
        }
    }

    private void TrimToContextWindow()
    {
        // Keep system message, trim oldest user/assistant messages
        while (GetTotalTokens() > _maxTokens && _messages.Count > 1)
        {
            _messages.RemoveAt(1); // Keep index 0 (system message)
        }
    }
}
```

### Pattern 4: Provider Abstraction with OpenAI SDK

**What:** ILlmClient interface abstracts LLM provider. OpenAiClient implements using official OpenAI .NET SDK.

**When to use:** v1.2 uses OpenAI-compatible APIs. Abstraction allows future providers (Anthropic, local models) without changing ChatService.

**Trade-offs:**
- Pro: Decouples business logic from provider specifics
- Pro: Easy to add new providers later
- Con: Slight overhead (acceptable for flexibility)

**Example:**
```csharp
// Llm/ILlmClient.cs
public interface ILlmClient
{
    Task<string> GetCompletionAsync(
        IEnumerable<ConversationMessage> messages,
        CancellationToken ct = default);

    IAsyncEnumerable<string> GetCompletionStreamAsync(
        IEnumerable<ConversationMessage> messages,
        CancellationToken ct = default);
}

// Llm/OpenAiClient.cs
public class OpenAiClient : ILlmClient
{
    private readonly OpenAI.Chat.ChatClient _client;

    public OpenAiClient(IOptions<LlmOptions> options)
    {
        var apiKey = options.Value.ApiKey;
        var model = options.Value.Model;
        _client = new OpenAI.Chat.ChatClient(model, apiKey);
    }

    public async Task<string> GetCompletionAsync(
        IEnumerable<ConversationMessage> messages,
        CancellationToken ct = default)
    {
        var chatMessages = messages.Select(m =>
            new OpenAI.Chat.ChatMessage(m.Role, m.Content)).ToList();

        var completion = await _client.CompleteChatAsync(chatMessages, ct);
        return completion.Content[0].Text;
    }
}
```

## Data Flow

### Chat Message Flow

```
User types message in Chat.razor
    ↓
Chat.razor.cs calls hubConnection.InvokeAsync("SendChatMessage", message)
    ↓
RuntimeHub.SendChatMessage() receives message
    ↓
ChatService.SendMessageAsync() orchestrates:
    ├─→ ConversationManager.AddUserMessage()
    ├─→ ConversationManager.GetMessagesForApi() (with context window trim)
    ├─→ LlmClient.GetCompletionAsync() → OpenAI API
    └─→ ConversationManager.AddAssistantMessage(response)
    ↓
RuntimeHub pushes via Clients.All.ReceiveMessage()
    ↓
Chat.razor.cs receives via hubConnection.On<string, string, DateTime>("ReceiveMessage")
    ↓
Chat.razor updates UI with new message
```

### Context Window Management Flow

```
ConversationManager maintains:
├─→ List<ConversationMessage> _messages (in-memory)
├─→ int _maxTokens (from config, e.g., 4000 for GPT-3.5)
└─→ TrimToContextWindow() called after each AddUserMessage/AddAssistantMessage

TrimToContextWindow logic:
1. Calculate total tokens: Sum(_messages.Select(m => m.Tokens))
2. If total > _maxTokens:
   - Keep system message (index 0)
   - Remove oldest user/assistant messages (FIFO)
   - Repeat until total <= _maxTokens
3. Return trimmed list for API call
```

### Configuration Flow

```
appsettings.json
    ↓
LlmOptions bound via IOptions<LlmOptions>
    ↓
Injected into OpenAiClient constructor
    ↓
Used to initialize OpenAI.Chat.ChatClient

LlmOptions properties:
- ApiKey: string (from env var or appsettings)
- BaseUrl: string (default: https://api.openai.com/v1)
- Model: string (default: gpt-3.5-turbo)
- MaxTokens: int (default: 4000)
- Temperature: double (default: 0.7)
```

## Integration Points

### New Service Registration (Program.cs)

```csharp
// Add LLM configuration
builder.Services.Configure<LlmOptions>(
    builder.Configuration.GetSection("Llm"));

// Register LLM services as singletons
builder.Services.AddSingleton<ILlmClient, OpenAiClient>();
builder.Services.AddSingleton<IConversationManager, ConversationManager>();
builder.Services.AddSingleton<IChatService, ChatService>();
```

### RuntimeHub Extension

```csharp
// Hubs/RuntimeHub.cs
public class RuntimeHub : Hub<IRuntimeClient> // Keep existing interface
{
    private readonly IModuleService _moduleService;
    private readonly IHeartbeatService _heartbeatService;
    private readonly IChatService _chatService; // NEW
    private readonly IHubContext<RuntimeHub, IChatClient> _chatHubContext; // NEW

    // Existing methods: LoadModule, UnloadModule, StartHeartbeat, StopHeartbeat

    // NEW chat methods
    public async Task SendChatMessage(string message)
    {
        var response = await _chatService.SendMessageAsync(message);
        await _chatHubContext.Clients.All.ReceiveMessage(
            "assistant", response, DateTime.UtcNow);
    }

    public async Task ClearConversation()
    {
        await _chatService.ClearConversationAsync();
        await _chatHubContext.Clients.All.ReceiveConversationCleared();
    }
}
```

### Chat Page Integration

```csharp
// Components/Pages/Chat.razor.cs
public partial class Chat : IAsyncDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IChatService ChatService { get; set; } = default!;

    private HubConnection? hubConnection;
    private List<ConversationMessage> messages = new();
    private string currentMessage = "";

    protected override async Task OnInitializedAsync()
    {
        // Load existing history from service
        messages = ChatService.GetHistory().ToList();

        // Connect to SignalR hub
        hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/hubs/runtime"))
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<string, string, DateTime>("ReceiveMessage",
            (role, content, timestamp) =>
            {
                messages.Add(new ConversationMessage(role, content, timestamp));
                InvokeAsync(StateHasChanged);
            });

        await hubConnection.StartAsync();
    }

    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(currentMessage)) return;

        await hubConnection!.InvokeAsync("SendChatMessage", currentMessage);
        currentMessage = "";
    }
}
```

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| Single user (v1.2) | In-memory singleton ConversationManager. No persistence. Simple and fast. |
| Multi-user (future) | Replace singleton with scoped service. Add user ID to conversation keys. Consider Redis for shared state. |
| Persistent history (future) | Add database layer (SQLite/PostgreSQL). ConversationManager becomes repository pattern. |
| High throughput (future) | Add message queue (RabbitMQ/Azure Service Bus) for async LLM calls. Prevents blocking SignalR hub. |

### Scaling Priorities

1. **First bottleneck:** LLM API latency blocks SignalR hub thread
   - **Fix:** Move LLM calls to background task queue. Return immediately, push response when ready.

2. **Second bottleneck:** In-memory state doesn't survive restarts
   - **Fix:** Add SQLite database for conversation persistence. Minimal complexity, local-first.

## Anti-Patterns

### Anti-Pattern 1: Blocking SignalR Hub with Long-Running LLM Calls

**What people do:** Call `await _llmClient.GetCompletionAsync()` directly in hub method, blocking the hub thread for 2-10 seconds.

**Why it's wrong:** SignalR hub methods should return quickly. Long-running operations block the hub thread pool, degrading responsiveness for all clients.

**Do this instead:** For v1.2 (single user), acceptable to block since only one user. For future multi-user, use background task queue (IHostedService with Channel<T>) to process LLM calls asynchronously.

### Anti-Pattern 2: Storing API Keys in appsettings.json Committed to Git

**What people do:** Put OpenAI API key directly in appsettings.json and commit to repository.

**Why it's wrong:** Exposes secrets in version control. Security risk.

**Do this instead:** Use environment variables or User Secrets for development. For production, use Azure Key Vault or similar. Example:
```csharp
// appsettings.json
"Llm": {
  "ApiKey": "", // Empty, set via env var
  "Model": "gpt-3.5-turbo"
}

// Environment variable: LLM__APIKEY=sk-...
// Or User Secrets: dotnet user-secrets set "Llm:ApiKey" "sk-..."
```

### Anti-Pattern 3: No Token Counting Before API Call

**What people do:** Send entire conversation history to API without checking token count. Hits API limit, gets error.

**Why it's wrong:** OpenAI API has token limits (e.g., 4096 for gpt-3.5-turbo). Exceeding limit causes 400 error.

**Do this instead:** Implement token counting in ConversationManager. Trim oldest messages to stay under limit. Use tiktoken library or rough estimation (1 token ≈ 4 characters for English).

### Anti-Pattern 4: Mixing Business Logic in Blazor Code-Behind

**What people do:** Put LLM call logic directly in Chat.razor.cs.

**Why it's wrong:** Violates separation of concerns. Hard to test. Doesn't follow existing service facade pattern.

**Do this instead:** Keep Chat.razor.cs thin. Only handle UI state and SignalR events. Business logic goes in ChatService.

## Build Order

Recommended implementation sequence based on dependencies:

### Phase 1: Core LLM Infrastructure
1. **LlmOptions.cs** - Configuration model (no dependencies)
2. **ILlmClient.cs** - Interface definition (no dependencies)
3. **OpenAiClient.cs** - Implementation using OpenAI SDK (depends on: LlmOptions)
4. **ConversationMessage.cs** - Data model (no dependencies)
5. **TokenCounter.cs** - Utility for token estimation (no dependencies)

### Phase 2: Conversation Management
6. **IConversationManager.cs** - Interface definition (depends on: ConversationMessage)
7. **ConversationManager.cs** - Implementation (depends on: ConversationMessage, TokenCounter)

### Phase 3: Service Layer
8. **IChatService.cs** - Interface definition (depends on: ConversationMessage)
9. **ChatService.cs** - Implementation (depends on: ILlmClient, IConversationManager)

### Phase 4: SignalR Integration
10. **IChatClient.cs** - Typed client interface (no dependencies)
11. **RuntimeHub.cs** - Extend with chat methods (depends on: IChatService, IChatClient)

### Phase 5: UI Layer
12. **Chat.razor** - UI markup (no dependencies)
13. **Chat.razor.cs** - Code-behind (depends on: IChatService, RuntimeHub, ConversationMessage)
14. **Navigation** - Add Chat link to main layout

### Phase 6: Configuration & Registration
15. **appsettings.json** - Add Llm section
16. **Program.cs** - Register services in DI container

### Dependency Graph
```
LlmOptions ─→ OpenAiClient ─→ ILlmClient ─┐
                                          ├─→ ChatService ─→ RuntimeHub ─→ Chat.razor.cs
ConversationMessage ─→ ConversationManager ─┘
TokenCounter ─────────┘
```

## Sources

- [Building a Real-Time Chat Application with Blazor Server](https://medium.com/@andryhadj/building-a-real-time-chat-application-with-blazor-server-a-deep-dive-into-event-driven-f881ed4332f4)
- [How to Build Real-Time Apps with SignalR in .NET](https://oneuptime.com/blog/post/2026-01-29-realtime-apps-signalr-dotnet/view)
- [Use ASP.NET Core SignalR with Blazor - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor?view=aspnetcore-10.0)
- [ASP.NET Core Blazor dependency injection - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-10.0)
- [The official .NET library for the OpenAI API - GitHub](https://github.com/openai/openai-dotnet)
- [Azure OpenAI client library for .NET - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.openai-readme?view=azure-dotnet)
- [Managing Context and Memory for OpenAI API Chat Applications](https://alvincrespo.hashnode.dev/managing-context-and-memory-for-openai-api-chat-applications)
- [Context Window Overflow in 2026: Fix LLM Errors Fast - Redis](https://redis.io/blog/context-window-overflow/)

---
*Architecture research for: OpenAnima LLM Integration*
*Researched: 2026-02-24*
