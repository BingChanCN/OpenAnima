# Project Research Summary

**Project:** OpenAnima v1.2 LLM Integration
**Domain:** LLM API integration for .NET 8 Blazor Server agent platform
**Researched:** 2026-02-24
**Confidence:** HIGH

## Executive Summary

OpenAnima v1.2 adds LLM capabilities to an existing modular agent platform built on .NET 8 Blazor Server. The research reveals a straightforward integration path: use the official OpenAI .NET SDK (2.8.0) with SharpToken (2.0.4) for token counting, implement streaming responses via existing SignalR infrastructure, and manage conversation history in-memory with token-based sliding window. The architecture follows established patterns already validated in v1.0-v1.1 (service facades, SignalR typed clients, singleton state management).

The critical insight is that Blazor Server's SignalR circuit architecture creates unique challenges for long-running LLM calls. Three configuration decisions must be correct from the start: (1) SignalR circuit timeouts extended to 60+ seconds, (2) HttpClient timeout set to infinite for streaming, and (3) all UI updates wrapped in InvokeAsync to prevent deadlocks. Getting these wrong causes user-visible failures (frozen UI, disconnections, timeout errors) that are difficult to debug after implementation.

Key risks center on context window management and memory leaks. Without accurate token counting (SharpToken), conversations will hit API limits unpredictably. Without proper cleanup on circuit disconnect, memory will grow unbounded. Both are preventable with upfront design: token-based truncation with 80% buffer, and scoped service lifetime with proper disposal. The recommended approach delivers a production-ready chat interface in three phases: API client setup, streaming UI, and context management.

## Key Findings

### Recommended Stack

The v1.2 milestone requires only two new packages added to the existing .NET 8 Blazor Server foundation. OpenAI 2.8.0 provides the official SDK with full streaming support and OpenAI-compatible provider flexibility (works with OpenRouter, Together, Anthropic via base URL configuration). SharpToken 2.0.4 is the .NET port of tiktoken for accurate token counting, essential for context window management and cost estimation. Both are stable releases with high confidence.

**Core technologies:**
- **OpenAI 2.8.0**: Official LLM API client — Maintained by OpenAI, supports streaming and all modern features, works with any OpenAI-compatible provider
- **SharpToken 2.0.4**: Token counting (tiktoken port) — Accurate token estimation for GPT-4o/o1/o3, pure .NET with no native dependencies
- **System.Text.Json**: Conversation serialization — Built into .NET 8, used for in-memory message storage
- **Existing stack unchanged**: .NET 8 runtime, Blazor Server, MediatR event bus, Pure CSS theme all remain

### Expected Features

Research identifies clear feature tiers based on user expectations and competitive analysis. Table stakes features are non-negotiable for a functional LLM chat interface: message display with role formatting, text input, streaming token-by-token responses, conversation history, auto-scroll, basic error handling, API configuration, and token/context window awareness. Missing any of these makes the product feel incomplete.

**Must have (table stakes):**
- Streaming response display — Modern UX standard, users expect token-by-token output
- Message display (user/assistant/system) — Standard chat pattern with role-based styling
- Text input with send button — Core interaction, Enter to send
- Conversation history display — Scrollable message list showing full context
- Token/context window awareness — Prevents API errors from exceeding limits
- Basic error handling — Users need to know when API calls fail (rate limit, auth, network)
- API configuration — Users must set endpoint, API key, model name

**Should have (competitive):**
- Markdown rendering with code highlighting — LLM responses often include code blocks
- Copy message content — Users want to extract responses for use elsewhere
- Message regeneration — Retry if response quality is poor
- Token usage display — Power users want to track API costs
- Typing indicator during streaming — Visual feedback that LLM is responding

**Defer (v2+):**
- Persistent conversation storage — Adds database complexity, explicitly out of scope per PROJECT.md
- Multi-conversation management — Validate single conversation first
- Conversation summarization — High complexity, not needed until users regularly hit context limits
- Message editing — Creates conversation branches, requires complex state management
- Voice input/output — Not core to v1.2 agent testing goal
- Image/multimodal support — Text-only for v1.2

### Architecture Approach

The integration adds three new components that follow existing OpenAnima patterns: LlmClient (OpenAI SDK wrapper), ConversationManager (in-memory history with token counting), and ChatService (service facade orchestrating both). All integrate through the existing RuntimeHub SignalR hub, reusing validated real-time push infrastructure. The architecture maintains separation of concerns with service facades, typed SignalR clients, and singleton state management—patterns already proven in v1.0-v1.1.

**Major components:**
1. **LlmClient** — OpenAI API wrapper handling requests, streaming, and error handling (singleton service)
2. **ConversationManager** — In-memory conversation history with token-based sliding window and context management (singleton service)
3. **ChatService** — Service facade orchestrating LlmClient and ConversationManager, injected into UI layer (singleton service)
4. **Chat.razor + code-behind** — Blazor page with SignalR hub connection for real-time streaming updates (follows Monitor.razor pattern)
5. **RuntimeHub extensions** — Add chat methods (SendMessage, ClearConversation) to existing SignalR hub (centralized real-time communication)

### Critical Pitfalls

Eight critical pitfalls identified, all preventable with correct initial configuration and patterns. The top three account for 80% of reported issues in Blazor Server + LLM integrations.

1. **SignalR Circuit Timeout During Long LLM Calls** — Default 30s timeout causes "Reconnecting..." during LLM calls. Fix: Configure CircuitOptions and HubOptions to 60+ seconds, implement streaming to keep circuit alive.

2. **UI Thread Deadlock with StateHasChanged in Async Streaming** — Calling StateHasChanged without InvokeAsync freezes UI completely. Fix: Always wrap in `await InvokeAsync(StateHasChanged)` when streaming, test with slow network conditions.

3. **HttpClient Timeout Mismatch with Streaming** — Default 100s timeout kills active streams. Fix: Set HttpClient.Timeout to Timeout.InfiniteTimeSpan for streaming clients, use CancellationToken for user cancellation.

4. **Context Window Overflow Without Token Counting** — Conversations fail with "maximum context length" errors after multiple turns. Fix: Use SharpToken for accurate counting, implement sliding window with 75% context usage buffer.

5. **In-Memory Conversation History Memory Leak** — Memory grows continuously without cleanup. Fix: Implement conversation history as Scoped service with proper Dispose, set maximum conversation length.

6. **Rate Limiting Without Retry Strategy** — 429 errors during normal usage. Fix: Implement exponential backoff with Polly library, respect Retry-After header.

7. **Streaming Response Cancellation Not Cleaning Up** — Resources leak when user cancels. Fix: Pass CancellationToken through entire pipeline, link circuit disconnection token with request cancellation.

8. **Event Bus Integration Blocking LLM Calls** — LLM calls block existing MediatR event bus, violating 100ms heartbeat requirement. Fix: Fire-and-forget pattern, publish completion events asynchronously.

## Implications for Roadmap

Based on research, suggested phase structure follows dependency order and pitfall prevention:

### Phase 1: API Client Setup & Configuration
**Rationale:** Must configure timeouts, retry logic, and HttpClient correctly before any LLM calls. Pitfalls 1, 3, 6, and 8 must be addressed here—getting configuration wrong causes user-visible failures that are difficult to debug after implementation.

**Delivers:**
- OpenAI SDK integration with configurable providers
- LlmClient abstraction with streaming support
- Proper timeout configuration (SignalR circuit 60s+, HttpClient infinite for streaming)
- Retry logic with exponential backoff for rate limits
- API configuration UI or appsettings.json setup
- Event bus integration pattern (fire-and-forget, non-blocking)

**Addresses:**
- API Configuration (table stakes from FEATURES.md)
- Message Role Formatting (table stakes)
- LlmClient component (from ARCHITECTURE.md)

**Avoids:**
- Pitfall 1: Circuit timeout during LLM calls
- Pitfall 3: HttpClient timeout mismatch
- Pitfall 6: Rate limiting without retry
- Pitfall 8: Event bus blocking

**Verification:**
- LLM calls complete without circuit disconnect
- Long streaming responses (> 100s) work
- 429 errors automatically retried
- Heartbeat maintains 100ms tick rate during LLM calls

### Phase 2: Chat UI with Streaming
**Rationale:** Streaming is table stakes for modern LLM UX. Must implement with correct InvokeAsync patterns from the start—Pitfall 2 (UI deadlock) is nearly impossible to debug if baked into initial implementation. Depends on Phase 1 API client being configured correctly.

**Delivers:**
- Chat.razor page with message display and text input
- Streaming response display with incremental token updates
- SignalR hub integration for real-time push
- Auto-scroll to latest message
- Basic error handling UI
- Typing indicator during streaming
- User cancellation with proper cleanup

**Uses:**
- OpenAI SDK streaming API (from STACK.md)
- Existing SignalR infrastructure (from ARCHITECTURE.md)
- Pure CSS dark theme (consistent with existing UI)

**Implements:**
- Chat.razor + code-behind (from ARCHITECTURE.md)
- RuntimeHub extensions (from ARCHITECTURE.md)
- IChatClient typed interface (from ARCHITECTURE.md)

**Avoids:**
- Pitfall 2: UI deadlock with StateHasChanged (wrap in InvokeAsync)
- Pitfall 7: Streaming cancellation not cleaning up (CancellationToken propagation)

**Verification:**
- Streaming responses display incrementally without freezing
- User can cancel streaming, resources cleaned up
- UI remains responsive during long responses
- No "Dispatcher" or synchronization context errors

### Phase 3: Context Management & Token Counting
**Rationale:** Multi-turn conversations require context window management. Must implement before users hit token limits—Pitfall 4 (context overflow) and Pitfall 5 (memory leak) cause unpredictable failures after several conversation turns. Depends on Phase 2 conversation history being established.

**Delivers:**
- SharpToken integration for accurate token counting
- ConversationManager with in-memory history
- Token-based sliding window (80% context usage buffer)
- Context window visualization or warning
- Memory cleanup on circuit disconnect
- Token usage display per message

**Uses:**
- SharpToken 2.0.4 (from STACK.md)
- Token-based truncation strategy (from FEATURES.md)
- ConversationManager component (from ARCHITECTURE.md)

**Implements:**
- IConversationManager interface (from ARCHITECTURE.md)
- ConversationManager with sliding window (from ARCHITECTURE.md)
- TokenCounter utility (from ARCHITECTURE.md)

**Avoids:**
- Pitfall 4: Context window overflow (accurate token counting, proactive trimming)
- Pitfall 5: Memory leak from conversation history (proper Dispose, cleanup strategy)

**Verification:**
- Multi-turn conversations (20+ messages) work without errors
- Memory stable after 100 conversation cycles
- Token count accurate (matches OpenAI's calculation)
- Context window warnings appear before API errors

### Phase Ordering Rationale

- **Phase 1 first:** Configuration errors cause cascading failures in later phases. Circuit timeouts, HttpClient settings, and retry logic must be correct before implementing streaming or context management.
- **Phase 2 second:** Streaming depends on API client being configured correctly. UI deadlock patterns are hard to fix retroactively—must use InvokeAsync from first implementation.
- **Phase 3 third:** Context management only matters after multi-turn conversations work. Token counting and sliding window can be added incrementally without breaking existing functionality.

This ordering minimizes rework and prevents "looks done but isn't" scenarios where features appear to work but fail under real usage (long conversations, network issues, concurrent users).

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 2 (Chat UI):** Blazor Server streaming patterns with SignalR are well-documented but nuanced. May need targeted research on InvokeAsync patterns and throttling strategies for high-frequency UI updates.

Phases with standard patterns (skip research-phase):
- **Phase 1 (API Client):** OpenAI SDK usage is well-documented with official examples. Configuration patterns are straightforward.
- **Phase 3 (Context Management):** Token counting and sliding window are established patterns with clear implementation examples.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | OpenAI SDK and SharpToken are stable releases with verified versions from NuGet API. Official packages with strong documentation. |
| Features | MEDIUM | Feature priorities based on competitive analysis and community consensus. Table stakes features are clear, but differentiator value needs validation. |
| Architecture | HIGH | Follows existing OpenAnima patterns (service facades, SignalR typed clients, singleton state). Integration points are well-defined. |
| Pitfalls | MEDIUM | Pitfalls sourced from Stack Overflow, Reddit, and Medium articles. Common issues are well-documented, but severity estimates are based on community reports. |

**Overall confidence:** HIGH

### Gaps to Address

- **Streaming UI update throttling:** Research identifies the need to throttle UI updates (every 50ms or every 5 tokens) but doesn't provide specific implementation guidance. Needs experimentation during Phase 2 to find optimal balance between responsiveness and performance.

- **Token counting accuracy for non-English text:** SharpToken is accurate for English but may have edge cases with special characters or non-English languages. Needs validation during Phase 3 testing.

- **Multi-user scaling:** v1.2 is single-user, but architecture should support future multi-user. Needs validation that singleton ConversationManager can be replaced with scoped service without major refactoring.

- **Error message UX:** Research identifies need for user-friendly error messages but doesn't specify exact wording or recovery flows. Needs UX design during Phase 2 implementation.

## Sources

### Primary (HIGH confidence)
- [OpenAI .NET SDK GitHub](https://github.com/openai/openai-dotnet) — Official repository, stable release verification
- [NuGet API - OpenAI 2.8.0](https://api.nuget.org/v3-flatcontainer/openai/index.json) — Version verification
- [NuGet API - SharpToken 2.0.4](https://api.nuget.org/v3-flatcontainer/sharptoken/index.json) — Version verification
- [OpenAI API Documentation](https://developers.openai.com/api/docs/quickstart/) — Official API reference
- [Microsoft Learn - SignalR with Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor) — Official Blazor Server patterns
- [Microsoft Learn - Blazor Dependency Injection](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection) — DI patterns

### Secondary (MEDIUM confidence)
- [Stack Overflow - Blazor StateHasChanged in async operations](https://stackoverflow.com/questions/76976391/blazor-app-doesnt-refresh-ui-after-statehaschanged-in-async-operation) — UI deadlock patterns
- [Stack Overflow - Blazor SignalR circuit timeouts](https://stackoverflow.com/questions/75150784/explain-blazor-signalr-circuit-timeouts-in-detail-please) — Timeout configuration
- [Stack Overflow - HttpClient timeout with OpenAI](https://stackoverflow.com/questions/76491056/i-get-httpclient-timeout-error-in-c-sharp-openai-library) — Streaming timeout issues
- [Reddit - AI response stream in Blazor Server](https://www.reddit.com/r/Blazor/comments/1c998h7/how_to_display_an_ai_response_stream_in_blazor/) — Community streaming patterns
- [Medium - Blazor app froze mid-demo](https://medium.com/careerbytecode/the-day-my-blazor-app-froze-mid-demo-and-what-i-learned-about-signalr-674ec8cb976d) — Real-world pitfall examples
- [Medium - Building Real-Time Chat with Blazor Server](https://medium.com/@andryhadj/building-a-real-time-chat-application-with-blazor-server-a-deep-dive-into-event-driven-f881ed4332f4) — Architecture patterns
- [Zylos AI - LLM Context Window Management 2026](https://zylos.ai/research/2026-01-19-llm-context-management) — Context management strategies
- [Redis Blog - Context Window Overflow 2026](https://redis.io/blog/context-window-overflow/) — Token counting best practices

### Tertiary (LOW confidence)
- [OpenAI Community - Handling long conversations](https://community.openai.com/t/handling-long-conversations-with-context-management/614212) — Community discussion on context management
- [LangGraph Tutorial - Message History with Sliding Windows](https://aiproduct.engineer/tutorials/langgraph-tutorial-message-history-management-with-sliding-windows-unit-12-exercise-3) — Sliding window implementation
- [Mem0 Blog - LLM Chat History Summarization 2025](https://mem0.ai/blog/llm-chat-history-summarization-guide-2025) — Summarization strategies (deferred to v2+)

---
*Research completed: 2026-02-24*
*Ready for roadmap: yes*
