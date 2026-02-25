# Phase 8: API Client Setup & Configuration - Research

**Researched:** 2026-02-25
**Domain:** LLM API Integration (OpenAI-compatible endpoints)
**Confidence:** HIGH

## Summary

Phase 8 establishes the foundation for LLM integration by implementing an OpenAI-compatible API client with streaming support, error handling, and retry logic. The research confirms that the official OpenAI .NET SDK (version 2.8.0) is the standard choice, providing built-in retry logic, streaming support, and comprehensive error handling. The SDK automatically retries transient failures (408, 429, 500-504) with exponential backoff up to 3 times.

Key architectural decisions: Use IOptions pattern for configuration, register ChatClient as singleton for thread-safety, configure SignalR circuit timeout to 60+ seconds for long-running LLM calls, and use InvokeAsync for all UI updates during streaming to prevent deadlocks. Token counting should use SharpToken 2.0.4 (or Microsoft.ML.Tokenizers for better performance in .NET 9+).

**Primary recommendation:** Use OpenAI .NET SDK 2.8.0 with IOptions configuration pattern, register as singleton service, and implement streaming with proper Blazor Server synchronization using InvokeAsync.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| LLM-01 | User can configure LLM endpoint, API key, and model name via appsettings.json | IOptions pattern with validation; OpenAIClientOptions supports custom endpoints |
| LLM-02 | Runtime can call OpenAI-compatible chat completion API with system/user/assistant messages | ChatClient.CompleteChatAsync with List<ChatMessage> (SystemChatMessage, UserChatMessage, AssistantChatMessage) |
| LLM-03 | Runtime can receive streaming responses token-by-token from LLM API | ChatClient.CompleteChatStreamingAsync returns IAsyncEnumerable<StreamingChatCompletionUpdate> |
| LLM-04 | User sees meaningful error messages when API calls fail | OpenAI SDK throws specific exceptions; map HTTP status codes (401, 429, 500, 503) to user-friendly messages |
| LLM-05 | Runtime retries transient API failures with exponential backoff | Built-in: SDK automatically retries 408, 429, 500-504 up to 3 times with exponential backoff |
</phase_requirements>

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| OpenAI | 2.8.0 | Official OpenAI .NET SDK for chat completions | Official SDK from OpenAI, built-in retry logic, streaming support, thread-safe, maintained by OpenAI + Microsoft |
| Microsoft.Extensions.Options | 8.0+ | Configuration binding and validation | Standard .NET configuration pattern, built-in validation, type-safe |
| Microsoft.Extensions.Http | 8.0+ | HttpClient factory integration | Proper HttpClient lifecycle management, prevents socket exhaustion |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| SharpToken | 2.0.4 | Token counting (tiktoken port) | Phase 10 - context window management; use for .NET 8 |
| Microsoft.ML.Tokenizers | 1.0+ | Official .NET tokenizer library | Alternative to SharpToken for .NET 9+; better performance |
| Polly | 8.0+ | Advanced retry/circuit breaker | Only if custom retry logic needed beyond SDK defaults |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| OpenAI SDK | Custom HttpClient | Lose built-in retry, streaming helpers, type safety; only if extreme customization needed |
| IOptions | Direct IConfiguration | Lose validation, type safety, testability; avoid |
| SharpToken | TiktokenSharp | SharpToken has better performance and more active maintenance |

**Installation:**
```bash
dotnet add package OpenAI --version 2.8.0
# Token counting (Phase 10):
dotnet add package SharpToken --version 2.0.4
```

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── LLM/
│   ├── LLMOptions.cs              # Configuration model
│   ├── ILLMService.cs             # Service interface
│   ├── LLMService.cs              # ChatClient wrapper with error handling
│   └── LLMErrorMapper.cs          # Maps exceptions to user messages
└── Program.cs                      # Register services
```

### Pattern 1: Configuration with IOptions

**What:** Type-safe configuration binding with validation
**When to use:** All configuration that comes from appsettings.json
**Example:**
```csharp
// Source: Microsoft Learn - Options pattern in ASP.NET Core
// LLMOptions.cs
public class LLMOptions
{
    public const string SectionName = "LLM";

    public string Endpoint { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4";
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 120;
}

// Program.cs
builder.Services.Configure<LLMOptions>(
    builder.Configuration.GetSection(LLMOptions.SectionName));

// Validation (optional but recommended)
builder.Services.AddOptions<LLMOptions>()
    .Bind(builder.Configuration.GetSection(LLMOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// appsettings.json
{
  "LLM": {
    "Endpoint": "https://api.openai.com/v1",
    "ApiKey": "sk-...",
    "Model": "gpt-4",
    "MaxRetries": 3,
    "TimeoutSeconds": 120
  }
}
```

### Pattern 2: ChatClient as Singleton Service

**What:** Register ChatClient as singleton for thread-safety and resource efficiency
**When to use:** Always - ChatClient is thread-safe and designed for reuse
**Example:**
```csharp
// Source: OpenAI .NET SDK 2.8.0 README
// Program.cs
builder.Services.AddSingleton<ChatClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;

    return new ChatClient(
        model: options.Model,
        credential: new ApiKeyCredential(options.ApiKey),
        options: new OpenAIClientOptions
        {
            Endpoint = new Uri(options.Endpoint)
        });
});
```

### Pattern 3: Streaming with Blazor Server InvokeAsync

**What:** Use InvokeAsync to marshal UI updates from background threads during streaming
**When to use:** All UI updates triggered by async operations (streaming LLM responses)
**Example:**
```csharp
// Source: Blazor University - Thread safety using InvokeAsync
await foreach (StreamingChatCompletionUpdate update
    in client.CompleteChatStreamingAsync(messages))
{
    if (update.ContentUpdate.Count > 0)
    {
        var token = update.ContentUpdate[0].Text;

        // CRITICAL: Use InvokeAsync for UI updates from background thread
        await InvokeAsync(() =>
        {
            currentMessage += token;
            StateHasChanged();
        });
    }
}
```

### Pattern 4: Multi-Turn Conversation with Message History

**What:** Build conversation history with typed message objects
**When to use:** All chat completions (even single-turn should use this pattern)
**Example:**
```csharp
// Source: OpenAI .NET SDK 2.8.0 Migration Guide
List<ChatMessage> messages = new()
{
    new SystemChatMessage("You are a helpful assistant."),
    new UserChatMessage("When was the Nobel Prize founded?")
};

var result = await client.CompleteChatAsync(messages);

// Add assistant response to history
messages.Add(new AssistantChatMessage(result));

// Continue conversation
messages.Add(new UserChatMessage("Who was the first person to be awarded one?"));
result = await client.CompleteChatAsync(messages);
```

### Pattern 5: Error Handling with Specific Exception Types

**What:** Catch and map SDK exceptions to user-friendly messages
**When to use:** All API calls - wrap in try/catch with specific exception handling
**Example:**
```csharp
// Source: OpenAI API Error Codes documentation
try
{
    var result = await client.CompleteChatAsync(messages);
    return new LLMResult { Success = true, Content = result.Content[0].Text };
}
catch (ClientResultException ex) when (ex.Status == 401)
{
    return new LLMResult { Success = false, Error = "Invalid API key. Check your configuration." };
}
catch (ClientResultException ex) when (ex.Status == 429)
{
    return new LLMResult { Success = false, Error = "Rate limit exceeded. Please wait and try again." };
}
catch (ClientResultException ex) when (ex.Status >= 500)
{
    return new LLMResult { Success = false, Error = "OpenAI service error. Please try again later." };
}
catch (HttpRequestException ex)
{
    return new LLMResult { Success = false, Error = $"Network error: {ex.Message}" };
}
catch (TaskCanceledException)
{
    return new LLMResult { Success = false, Error = "Request timed out. Try a shorter prompt." };
}
```

### Anti-Patterns to Avoid

- **Creating new ChatClient per request:** Wastes resources, loses connection pooling. Always use singleton.
- **Blocking UI thread during streaming:** Never use `.Result` or `.Wait()` in Blazor components. Always use `await` and `InvokeAsync`.
- **Ignoring retry exhaustion:** SDK retries 3 times then throws. Always catch final exception and show user-friendly message.
- **Hardcoding API keys:** Never commit API keys. Use appsettings.json (gitignored) or environment variables.
- **Not configuring SignalR timeout:** Default 30s circuit timeout will disconnect during long LLM calls. Must configure to 60+ seconds.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| HTTP retry logic | Custom retry with Thread.Sleep | OpenAI SDK built-in retry | SDK handles exponential backoff, jitter, retry-after headers automatically |
| Token counting | String.Split or regex | SharpToken or Microsoft.ML.Tokenizers | Tokenization is model-specific (BPE), not simple word splitting; edge cases are complex |
| Streaming parser | Manual SSE parsing | SDK's CompleteChatStreamingAsync | SDK handles SSE format, partial JSON, reconnection, error events |
| API authentication | Manual header injection | SDK's ApiKeyCredential | SDK handles auth header format, credential rotation, security best practices |
| Request timeout | HttpClient.Timeout = fixed value | SDK default + SignalR config | Streaming needs infinite HttpClient timeout; control via SignalR circuit timeout instead |

**Key insight:** The OpenAI SDK encapsulates years of production learnings about API quirks, edge cases, and failure modes. Custom implementations miss subtle behaviors like retry-after headers, rate limit backoff strategies, and streaming error recovery.

## Common Pitfalls

### Pitfall 1: SignalR Circuit Timeout During Long LLM Calls

**What goes wrong:** Blazor Server disconnects after 30 seconds (default) during streaming LLM response, losing UI state
**Why it happens:** Default `DisconnectedCircuitMaxRetained` and `DisconnectedCircuitRetentionPeriod` are too short for LLM calls
**How to avoid:** Configure SignalR circuit timeout to 60+ seconds in Program.cs
**Warning signs:** "Circuit disconnected" errors during streaming; UI resets mid-response

```csharp
// Program.cs
builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DisconnectedCircuitMaxRetained = 100;
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
    });

builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
```

### Pitfall 2: HttpClient Timeout Kills Streaming Responses

**What goes wrong:** HttpClient.Timeout (default 100 seconds) aborts streaming before LLM finishes
**Why it happens:** Streaming responses can take minutes; HttpClient timeout applies to entire request duration
**How to avoid:** OpenAI SDK handles this internally, but if using custom HttpClient, set `Timeout = Timeout.InfiniteTimeSpan`
**Warning signs:** TaskCanceledException after ~100 seconds during streaming

```csharp
// Only needed if NOT using OpenAI SDK's built-in client
var httpClient = new HttpClient
{
    Timeout = Timeout.InfiniteTimeSpan // Streaming can take indefinite time
};
```

### Pitfall 3: Blazor UI Deadlock from Non-InvokeAsync Updates

**What goes wrong:** UI freezes or throws "current thread is not associated with the Dispatcher" during streaming
**Why it happens:** Streaming callbacks run on background thread; direct StateHasChanged() violates Blazor's synchronization context
**How to avoid:** Wrap ALL UI updates in `InvokeAsync(() => { ... })`
**Warning signs:** UI doesn't update during streaming; InvalidOperationException about dispatcher

```csharp
// WRONG - will deadlock or throw
await foreach (var update in stream)
{
    message += update.Text;
    StateHasChanged(); // ❌ Called from background thread
}

// CORRECT - marshals to UI thread
await foreach (var update in stream)
{
    await InvokeAsync(() =>
    {
        message += update.Text;
        StateHasChanged(); // ✅ Safe on UI thread
    });
}
```

### Pitfall 4: Not Handling Partial Streaming Failures

**What goes wrong:** Stream starts successfully but fails mid-response; user sees incomplete message with no error
**Why it happens:** Network issues, rate limits, or server errors can occur after streaming begins
**How to avoid:** Wrap streaming loop in try/catch; track completion state; show error if stream doesn't finish
**Warning signs:** Messages cut off mid-sentence; no error shown to user

```csharp
bool streamCompleted = false;
try
{
    await foreach (var update in client.CompleteChatStreamingAsync(messages))
    {
        // Process updates...
    }
    streamCompleted = true;
}
catch (Exception ex)
{
    await InvokeAsync(() =>
    {
        errorMessage = streamCompleted
            ? "Response completed with errors"
            : $"Streaming failed: {ex.Message}";
        StateHasChanged();
    });
}
```

### Pitfall 5: Ignoring Built-In Retry Exhaustion

**What goes wrong:** SDK retries 3 times automatically, then throws exception; app crashes if not caught
**Why it happens:** Developers assume SDK "handles" all errors, but it only retries transient failures
**How to avoid:** Always wrap API calls in try/catch; distinguish between retried errors (429, 500) and permanent errors (401, 403)
**Warning signs:** Unhandled exceptions after multiple retry attempts; app crashes on rate limits

```csharp
// SDK automatically retries these 3 times:
// - 408 Request Timeout
// - 429 Too Many Requests
// - 500, 502, 503, 504 Server Errors

// After 3 retries, SDK throws - YOU must catch it
try
{
    var result = await client.CompleteChatAsync(messages);
}
catch (ClientResultException ex)
{
    // This means SDK already retried 3 times and failed
    _logger.LogError(ex, "LLM call failed after retries: {Status}", ex.Status);
    // Show user-friendly message
}
```

### Pitfall 6: Token Counting Inaccuracy

**What goes wrong:** Manual token estimation (word count * 1.3) causes context window overflow or premature truncation
**Why it happens:** Tokenization is model-specific BPE (Byte Pair Encoding), not word-based
**How to avoid:** Use SharpToken or Microsoft.ML.Tokenizers with correct model encoding
**Warning signs:** "Context length exceeded" errors despite seeming under limit; messages truncated too early

```csharp
// WRONG - inaccurate
int tokens = text.Split(' ').Length * 1.3; // ❌

// CORRECT - model-specific tokenization
var encoding = GptEncoding.GetEncodingForModel("gpt-4");
int tokens = encoding.Encode(text).Count; // ✅
```

## Code Examples

Verified patterns from official sources:

### Basic Chat Completion (Non-Streaming)

```csharp
// Source: OpenAI .NET SDK 2.8.0 README
using OpenAI.Chat;

ChatClient client = new(
    model: "gpt-4",
    apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

List<ChatMessage> messages = new()
{
    new SystemChatMessage("You are a helpful assistant."),
    new UserChatMessage("Hello!")
};

ChatCompletion completion = await client.CompleteChatAsync(messages);
Console.WriteLine(completion.Content[0].Text);
```

### Streaming Chat Completion

```csharp
// Source: OpenAI .NET SDK 2.8.0 README
await foreach (StreamingChatCompletionUpdate update
    in client.CompleteChatStreamingAsync(messages))
{
    if (update.ContentUpdate.Count > 0)
    {
        Console.Write(update.ContentUpdate[0].Text);
    }
}
```

### Custom Endpoint Configuration

```csharp
// Source: Context7 - OpenAI .NET SDK
using OpenAI;
using OpenAI.Chat;

ChatClient client = new(
    model: "gpt-4",
    credential: new ApiKeyCredential(apiKey),
    options: new OpenAIClientOptions
    {
        Endpoint = new Uri("https://your-custom-endpoint.com/v1")
    });
```

### Token Counting

```csharp
// Source: SharpToken NuGet package
using SharpToken;

var encoding = GptEncoding.GetEncodingForModel("gpt-4");
var tokens = encoding.Encode("Hello, world!");
Console.WriteLine($"Token count: {tokens.Count}"); // 4

// Count without encoding (more efficient)
int count = encoding.CountTokens("Hello, world!");
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| OpenAI 1.x (Betalgo.OpenAI) | OpenAI 2.x (official SDK) | 2024 Q2 | Official SDK from OpenAI+Microsoft; breaking API changes |
| Manual SSE parsing | SDK streaming methods | 2024 Q2 | Built-in streaming support with proper error handling |
| Polly for all retry logic | SDK built-in retry | 2024 Q2 | Retry logic included; Polly only needed for custom policies |
| SharpToken only | Microsoft.ML.Tokenizers | 2024 Q4 (.NET 9) | Official .NET tokenizer; better performance |
| Environment variables only | IOptions pattern | Always preferred | Type-safe, validated configuration |

**Deprecated/outdated:**
- **Betalgo.OpenAI.GPT3:** Community library replaced by official OpenAI SDK
- **Manual HttpClient with JSON parsing:** SDK provides type-safe client
- **OPENAI_KEY environment variable:** SDK only reads OPENAI_API_KEY (not OPENAI_KEY)

## Open Questions

1. **Should we support multiple LLM providers (Anthropic, Azure OpenAI) in Phase 8?**
   - What we know: OpenAI SDK supports Azure OpenAI via endpoint configuration
   - What's unclear: Whether to abstract provider differences now or defer to future phase
   - Recommendation: Start with OpenAI-compatible only; add provider abstraction in Phase 11+ if needed

2. **Should we implement custom retry logic beyond SDK defaults?**
   - What we know: SDK retries 408, 429, 500-504 up to 3 times with exponential backoff
   - What's unclear: Whether 3 retries is sufficient for production use
   - Recommendation: Start with SDK defaults; add Polly if monitoring shows retry exhaustion

3. **Should we use SharpToken or Microsoft.ML.Tokenizers?**
   - What we know: SharpToken works on .NET 8; Microsoft.ML.Tokenizers is official but newer
   - What's unclear: Stability and performance comparison on .NET 8
   - Recommendation: Use SharpToken 2.0.4 for Phase 10 (proven stable); migrate to Microsoft.ML.Tokenizers when upgrading to .NET 9+

## Sources

### Primary (HIGH confidence)
- [OpenAI .NET SDK 2.8.0](https://github.com/openai/openai-dotnet) - Official SDK documentation and examples
- [Context7: /openai/openai-dotnet/openai_2.8.0](https://context7.com/openai/openai-dotnet) - API patterns and configuration
- [OpenAI API Error Codes](https://developers.openai.com/api/docs/guides/error-codes/) - Official error handling guide
- [SharpToken 2.0.4 NuGet](https://www.nuget.org/packages/SharpToken) - Token counting library
- [Microsoft Learn: Options Pattern](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options) - Configuration best practices

### Secondary (MEDIUM confidence)
- [ASP.NET Core Blazor SignalR guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/signalr) - Circuit timeout configuration
- [Blazor University: Thread safety using InvokeAsync](https://blazor-university.com/components/multi-threaded-rendering/invokeasync) - UI synchronization patterns
- [Microsoft Learn: Build resilient HTTP apps](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience) - Retry and resilience patterns
- [Polly in .NET: Effective Retry and Timeout Policies](https://medium.com/asp-dotnet/polly-in-net-effective-retry-and-timeout-policies-for-httpclient-0d4712cc5d15) - Advanced retry strategies

### Tertiary (LOW confidence)
- Community discussions on SignalR timeout issues - anecdotal evidence for 60+ second configuration
- Stack Overflow threads on streaming response handling - practical patterns but not authoritative

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Official SDK with extensive documentation and production use
- Architecture: HIGH - Patterns verified from official Microsoft Learn and SDK docs
- Pitfalls: HIGH - Documented in official sources and STATE.md from previous research

**Research date:** 2026-02-25
**Valid until:** 2026-04-25 (60 days - stable ecosystem, but check for SDK updates)

**Critical blockers identified in STATE.md:**
1. ✅ SignalR circuit timeout must be 60+ seconds - CONFIRMED and documented
2. ✅ HttpClient timeout must be infinite for streaming - SDK handles this internally
3. ✅ All UI updates must use InvokeAsync - CONFIRMED and documented with examples
4. ✅ Token counting must be accurate - SharpToken 2.0.4 recommended

**Next steps for planner:**
- Create configuration infrastructure (LLMOptions, appsettings.json)
- Implement LLMService with ChatClient singleton
- Add error handling with user-friendly messages
- Configure SignalR timeouts for long-running operations
- Add streaming support with InvokeAsync pattern
