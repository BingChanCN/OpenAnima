# Pitfalls Research

**Domain:** LLM Integration in .NET 8 Blazor Server Agent Platform
**Researched:** 2026-02-24
**Confidence:** MEDIUM

## Critical Pitfalls

### Pitfall 1: SignalR Circuit Timeout During Long LLM Calls

**What goes wrong:**
Blazor Server SignalR circuits disconnect during long-running LLM API calls (30+ seconds), causing the UI to freeze or show "Reconnecting..." indefinitely. User loses conversation state and must refresh the page.

**Why it happens:**
Default SignalR circuit timeout is 30 seconds. LLM API calls (especially with streaming disabled or complex prompts) can exceed this. Developers forget that Blazor Server maintains a persistent WebSocket connection that times out independently of HttpClient timeout.

**How to avoid:**
- Configure `CircuitOptions.DisconnectedCircuitMaxRetained` and `DisconnectedCircuitRetentionPeriod` in Program.cs
- Set `HubOptions.ClientTimeoutInterval` to at least 60 seconds for LLM workloads
- Use streaming responses to keep the circuit alive with incremental updates
- Implement keep-alive pings during long operations

**Warning signs:**
- "Reconnecting..." message appears during LLM calls
- Users report "page freezes" after asking questions
- SignalR connection logs show frequent disconnects
- Circuit disposal logs correlate with LLM API timing

**Phase to address:**
Phase 1 (API Client Setup) — Configure timeouts before first LLM call

---

### Pitfall 2: UI Thread Deadlock with StateHasChanged in Async Streaming

**What goes wrong:**
Streaming LLM responses freeze the UI completely. The chat interface becomes unresponsive, and the browser tab may show "Page Unresponsive". Partial responses don't appear incrementally as expected.

**Why it happens:**
Developers call `StateHasChanged()` synchronously from within an async streaming loop without wrapping in `InvokeAsync()`. Blazor Server requires all UI updates to be marshaled through the synchronization context. Calling StateHasChanged from the streaming thread (which is not the UI thread) causes deadlock or silent failure.

**How to avoid:**
- Always wrap StateHasChanged in `await InvokeAsync(StateHasChanged)` when streaming
- Use `await InvokeAsync(async () => { /* update state */ StateHasChanged(); })` pattern
- Consider throttling UI updates (e.g., every 50ms) instead of every token
- Test streaming with slow network conditions to expose timing issues

**Warning signs:**
- UI freezes during streaming responses
- Partial responses don't appear until stream completes
- Browser console shows "Dispatcher" or synchronization context errors
- Streaming works in console app but fails in Blazor

**Phase to address:**
Phase 2 (Chat UI with Streaming) — Must be correct from first streaming implementation

---

### Pitfall 3: HttpClient Timeout Mismatch with Streaming

**What goes wrong:**
Streaming LLM responses fail with "The request was canceled due to the configured HttpClient.Timeout" error after 100 seconds, even though the stream is actively receiving data. User sees partial response then error.

**Why it happens:**
Default HttpClient timeout is 100 seconds. For streaming responses, this timeout applies to the entire stream duration, not individual chunks. A long conversation response (200+ tokens at 20 tokens/sec = 10+ seconds) can exceed this. Developers configure OpenAI client timeout but forget HttpClient has its own timeout.

**How to avoid:**
- Set HttpClient.Timeout to `Timeout.InfiniteTimeSpan` for streaming clients
- Use CancellationToken for user-initiated cancellation instead of timeout
- Configure separate HttpClient instances: one for streaming (no timeout), one for non-streaming (with timeout)
- Implement application-level timeout logic (e.g., 5 minutes max conversation)

**Warning signs:**
- "HttpClient.Timeout" exceptions during streaming
- Streams fail consistently around 100 seconds
- Short responses work, long responses fail
- Timeout occurs even when data is actively streaming

**Phase to address:**
Phase 1 (API Client Setup) — Configure HttpClient correctly before streaming implementation

---

### Pitfall 4: Context Window Overflow Without Token Counting

**What goes wrong:**
LLM API calls fail with "This model's maximum context length is 128000 tokens" error after several conversation turns. User loses conversation history and must start over. Error appears suddenly without warning.

**Why it happens:**
Developers append messages to conversation history without tracking token count. Each turn adds system prompt + user message + assistant response. After 10-20 turns, total tokens exceed model's context window. Character count estimation (divide by 4) is inaccurate for code, special characters, or non-English text.

**How to avoid:**
- Use tiktoken library (.NET port: `SharpToken` or `TiktokenSharp`) for accurate token counting
- Track running token count for conversation history
- Implement sliding window: keep system prompt + recent N messages that fit in context
- Reserve tokens for response (e.g., use 75% of context for history, 25% for response)
- Warn user when approaching limit (e.g., "90% of context used")

**Warning signs:**
- "maximum context length" errors after multiple conversation turns
- Errors occur inconsistently (depends on response length)
- Long user messages cause immediate failures
- Error rate increases with conversation length

**Phase to address:**
Phase 3 (Context Management) — Must implement before multi-turn conversations

---

### Pitfall 5: In-Memory Conversation History Memory Leak

**What goes wrong:**
Application memory grows continuously as users have conversations. After hours of operation, memory usage reaches gigabytes. Eventually causes OutOfMemoryException or system slowdown.

**Why it happens:**
Conversation history stored in memory without cleanup strategy. Each user session (SignalR circuit) maintains full conversation history. When circuits disconnect (user closes browser), history isn't cleaned up. Scoped services holding conversation state aren't disposed properly.

**How to avoid:**
- Implement conversation history as Scoped service (per-circuit lifetime)
- Ensure proper Dispose implementation to clean up on circuit disconnect
- Set maximum conversation length (e.g., 50 messages, then trim oldest)
- Monitor memory usage in tests (connect/disconnect cycles)
- Consider LRU cache with size limit for conversation storage

**Warning signs:**
- Memory usage grows steadily over time
- Memory doesn't decrease when users disconnect
- GC collections don't reclaim memory
- Memory growth correlates with number of conversations

**Phase to address:**
Phase 3 (Context Management) — Implement cleanup strategy from start

---

### Pitfall 6: Rate Limiting Without Retry Strategy

**What goes wrong:**
LLM API calls fail with 429 "Rate limit exceeded" errors during normal usage. User sees error message, loses their input, and must retry manually. Errors increase during peak usage.

**Why it happens:**
OpenAI and compatible APIs have rate limits (requests per minute, tokens per minute). Developers don't implement exponential backoff retry logic. Multiple concurrent users or rapid-fire requests (e.g., user clicks send multiple times) trigger rate limits. The official OpenAI .NET SDK doesn't automatically retry 429 errors.

**How to avoid:**
- Implement exponential backoff with jitter for 429 errors
- Use Polly library for retry policies: `WaitAndRetryAsync` with exponential backoff
- Respect `Retry-After` header in 429 responses
- Implement request queuing to prevent concurrent request spikes
- Show "Sending..." state to prevent duplicate submissions

**Warning signs:**
- 429 errors in logs
- Errors occur during peak usage or rapid requests
- Users report "rate limit" errors
- Errors disappear after waiting a few seconds

**Phase to address:**
Phase 1 (API Client Setup) — Build retry logic into API client from start

---

### Pitfall 7: Streaming Response Cancellation Not Cleaning Up

**What goes wrong:**
User cancels LLM response (closes chat, navigates away, or clicks stop), but HTTP request continues in background. Resources aren't released, and rate limits are consumed by abandoned requests. Memory leaks from unclosed streams.

**Why it happens:**
Developers don't pass CancellationToken through the entire streaming pipeline. When SignalR circuit disconnects or user cancels, the streaming loop continues. HttpClient request isn't cancelled, and stream isn't disposed. Background task holds references preventing GC.

**How to avoid:**
- Pass CancellationToken from circuit lifetime through to HttpClient
- Link user cancellation token with circuit disconnection token
- Wrap streaming in try/finally to ensure disposal
- Use `await using` for IAsyncEnumerable streams
- Test cancellation scenarios (disconnect during streaming)

**Warning signs:**
- HTTP requests continue after user disconnects
- Memory usage doesn't decrease after cancellation
- Rate limit consumption higher than expected
- Logs show streaming loops running after circuit disposal

**Phase to address:**
Phase 2 (Chat UI with Streaming) — Implement cancellation from first streaming implementation

---

### Pitfall 8: Event Bus Integration Blocking LLM Calls

**What goes wrong:**
LLM API calls block the existing MediatR event bus, causing heartbeat delays or module communication failures. The 100ms heartbeat requirement is violated. System becomes unresponsive during LLM calls.

**Why it happens:**
Developers publish LLM request/response events through the existing synchronous event bus. LLM calls take seconds, blocking event handlers. Other modules waiting for events experience delays. The event bus wasn't designed for long-running operations.

**How to avoid:**
- Use fire-and-forget pattern for LLM calls: publish "LLM request started" event immediately, publish "LLM response received" event when complete
- Don't await LLM calls from within event handlers
- Consider separate async event bus for long-running operations
- LLM module should handle calls independently, not block event bus
- Monitor event bus latency to detect blocking

**Warning signs:**
- Heartbeat tick latency increases during LLM calls
- Event bus logs show delays correlating with LLM timing
- Modules report timeout waiting for events
- System becomes unresponsive during conversations

**Phase to address:**
Phase 1 (API Client Setup) — Design integration pattern before implementing

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Character count ÷ 4 for token estimation | No external library needed | Inaccurate, causes context overflow | Never — use tiktoken |
| Unlimited conversation history | Simpler implementation | Memory leak, performance degradation | Never — implement limits from start |
| No retry logic for API calls | Faster initial implementation | Poor reliability, user frustration | Never — rate limits are common |
| Synchronous LLM calls in event handlers | Simpler code flow | Blocks event bus, violates performance requirements | Never — breaks existing architecture |
| Global conversation state (Singleton) | Avoids per-user complexity | Shared state across users, security risk | Never — use Scoped services |
| Hardcoded API keys in code | Quick testing | Security vulnerability, can't change without rebuild | Only for local development, never commit |
| No streaming, wait for full response | Simpler UI implementation | Poor UX, long wait times, circuit timeouts | Acceptable for MVP if responses < 10 seconds |
| Storing full conversation in memory | Fast access, no database | Memory leak, lost on restart | Acceptable for v1.2, add persistence in v1.3 |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| OpenAI SDK → Blazor Server | Not configuring HttpClient timeout for streaming | Set Timeout.InfiniteTimeSpan for streaming client |
| Streaming → UI updates | Calling StateHasChanged without InvokeAsync | Always wrap in `await InvokeAsync(StateHasChanged)` |
| Conversation history → Context window | Appending messages without token counting | Use tiktoken to count tokens, implement sliding window |
| LLM calls → Event bus | Awaiting LLM calls in event handlers | Fire-and-forget pattern, publish completion event |
| User cancellation → HTTP request | Not passing CancellationToken to HttpClient | Link circuit token with request cancellation |
| Rate limiting → Retry | Failing immediately on 429 errors | Implement exponential backoff with Polly |
| API keys → Configuration | Hardcoding keys in code | Use appsettings.json with user secrets for development |
| Multiple providers → Client config | Single HttpClient for all providers | Separate clients per provider with different configs |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Updating UI for every streaming token | High CPU, UI jank, poor responsiveness | Throttle updates (every 50ms or every 5 tokens) | Streaming responses > 100 tokens |
| No conversation history limit | Memory growth, GC pressure, slowdown | Implement sliding window (e.g., 50 messages max) | After 20+ conversation turns |
| Synchronous token counting on every message | UI freezes during send | Cache token counts, count asynchronously | Messages > 1000 tokens |
| Serializing full conversation to SignalR | Large messages, bandwidth saturation | Send only new messages, use deltas | Conversations > 10 messages |
| No request queuing | Rate limit errors, failed requests | Queue requests, process sequentially | > 3 concurrent users |
| Logging full LLM responses | I/O bottleneck, large log files | Log summary only (token count, timing), full response at Debug level | Continuous operation |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Exposing API keys in client-side code | Key theft, unauthorized usage, cost | Keep keys server-side only, never send to browser |
| No input validation on user messages | Prompt injection, jailbreak attempts | Validate length, sanitize input, implement content filtering |
| Logging API keys in error messages | Key exposure in logs | Sanitize logs, use [REDACTED] for sensitive data |
| No rate limiting per user | DoS, cost explosion | Implement per-user rate limits (e.g., 10 requests/minute) |
| Trusting LLM output without validation | XSS, code injection if rendered as HTML | Sanitize LLM responses, escape HTML, validate code |
| No cost monitoring | Unexpected API bills | Track token usage, set budget alerts, implement usage caps |
| Storing conversation history without encryption | Data breach risk | Encrypt sensitive conversations (future: add persistence) |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| No loading indicator during LLM call | User thinks app is frozen, clicks multiple times | Show "Thinking..." or typing indicator immediately |
| Streaming without incremental display | Long wait, no feedback | Display tokens as they arrive, show progress |
| No error recovery | User loses input on error, must retype | Preserve input on error, allow retry |
| No cancellation option | User stuck waiting for long response | Provide "Stop" button, cancel on navigation |
| Context limit error without explanation | Cryptic error, user doesn't understand | Explain "conversation too long", offer to start new |
| No conversation history UI | User can't review previous messages | Show scrollable message history |
| Overwhelming system prompts in UI | User sees internal prompts, confusing | Hide system messages, show only user/assistant |
| No indication of streaming vs complete | User doesn't know if response is done | Show "..." while streaming, checkmark when complete |

## "Looks Done But Isn't" Checklist

- [ ] **Streaming responses:** Often missing InvokeAsync wrapper — verify UI updates don't freeze
- [ ] **Token counting:** Often using character estimation — verify tiktoken library integrated
- [ ] **Context window management:** Often missing token tracking — verify sliding window implemented
- [ ] **Cancellation:** Often missing CancellationToken propagation — verify requests cancel on disconnect
- [ ] **Rate limiting:** Often missing retry logic — verify 429 errors handled with backoff
- [ ] **Memory cleanup:** Often missing Dispose on conversation history — verify memory stable over time
- [ ] **HttpClient timeout:** Often using default 100s — verify Timeout.InfiniteTimeSpan for streaming
- [ ] **Event bus integration:** Often blocking on LLM calls — verify heartbeat maintains 100ms during conversations
- [ ] **Error handling:** Often showing raw API errors — verify user-friendly error messages
- [ ] **API key security:** Often hardcoded or logged — verify keys in config, not in code/logs

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Circuit timeout during LLM calls | LOW | Increase timeout config, implement streaming |
| UI deadlock with StateHasChanged | LOW | Add InvokeAsync wrappers, test thoroughly |
| HttpClient timeout on streaming | LOW | Change timeout to InfiniteTimeSpan, redeploy |
| Context window overflow | MEDIUM | Implement token counting, add sliding window logic |
| Memory leak from conversation history | MEDIUM | Add Dispose implementation, implement cleanup strategy |
| No retry logic for rate limits | LOW | Add Polly retry policy, configure exponential backoff |
| Streaming cancellation not working | MEDIUM | Add CancellationToken propagation, test cancellation scenarios |
| Event bus blocking | HIGH | Refactor integration pattern, separate async operations |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Circuit timeout during LLM calls | Phase 1 | LLM calls complete without circuit disconnect, timeout config verified |
| UI deadlock with streaming | Phase 2 | Streaming responses display incrementally without freezing |
| HttpClient timeout mismatch | Phase 1 | Long streaming responses (> 100s) complete successfully |
| Context window overflow | Phase 3 | Multi-turn conversations (20+ messages) work without errors |
| Memory leak from history | Phase 3 | Memory stable after 100 conversation cycles |
| Rate limiting without retry | Phase 1 | 429 errors automatically retried, no user-visible failures |
| Streaming cancellation | Phase 2 | User can cancel streaming, resources cleaned up |
| Event bus blocking | Phase 1 | Heartbeat maintains 100ms tick rate during LLM calls |

## Sources

- [How to display an AI response stream in Blazor Server - Reddit](https://www.reddit.com/r/Blazor/comments/1c998h7/how_to_display_an_ai_response_stream_in_blazor/)
- [LLM Context Window Management and Long-Context Strategies 2026 - Zylos AI](https://zylos.ai/research/2026-01-19-llm-context-management)
- [Context Window Overflow in 2026: Fix LLM Errors Fast - Redis](https://redis.io/blog/context-window-overflow/)
- [Blazor app doesn't refresh UI after StateHasChanged in async operation - Stack Overflow](https://stackoverflow.com/questions/76976391/blazor-app-doesnt-refresh-ui-after-statehaschanged-in-async-operation)
- [Explain Blazor SignalR / Circuit Timeouts in Detail - Stack Overflow](https://stackoverflow.com/questions/75150784/explain-blazor-signalr-circuit-timeouts-in-detail-please)
- [The Day My Blazor App Froze Mid-Demo - Medium](https://medium.com/careerbytecode/the-day-my-blazor-app-froze-mid-demo-and-what-i-learned-about-signalr-674ec8cb976d)
- [HttpClient.Timeout Error in C# OpenAI library - Stack Overflow](https://stackoverflow.com/questions/76491056/i-get-httpclient-timeout-error-in-c-sharp-openai-library)
- [Request Timeout for Azure OpenAI when Streaming - Microsoft Learn](https://learn.microsoft.com/en-us/answers/questions/1465402/request-timeout-for-azure-openai-when-streaming)
- [Rate limits - OpenAI API](https://developers.openai.com/api/docs/guides/rate-limits/)
- [How to handle rate limits - OpenAI for developers](https://developers.openai.com/cookbook/examples/how_to_handle_rate_limits/)
- [How to count tokens with Tiktoken - OpenAI for developers](https://developers.openai.com/cookbook/examples/how_to_count_tokens_with_tiktoken/)
- [Counting tokens - OpenAI API](https://developers.openai.com/api/docs/guides/token-counting)
- [Async/Await at Scale — Avoiding Hidden Deadlocks in .NET 8 - Medium](https://blog.stackademic.com/async-await-at-scale-avoiding-hidden-deadlocks-in-net-8-9c41ff53a4ae)
- [Hunting Down Memory Leaks in .NET: The Ultimate Developer's Guide - Medium](https://medium.com/@vikpoca/hunting-down-memory-leaks-in-net-the-ultimate-developers-guide-b9c81d990d63)
- [OpenAI .NET SDK - GitHub](https://github.com/openai/openai-dotnet)
- [Streaming API responses - OpenAI for developers](https://developers.openai.com/api/docs/guides/streaming-responses/)

---
*Pitfalls research for: LLM Integration in .NET 8 Blazor Server Agent Platform*
*Researched: 2026-02-24*
