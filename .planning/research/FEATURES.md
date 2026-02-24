# Feature Research: LLM Integration

**Domain:** LLM API integration, chat interface, conversation context management
**Researched:** 2026-02-24
**Confidence:** MEDIUM
**Milestone:** v1.2 LLM Integration (subsequent milestone)

## Context

This research focuses ONLY on features for LLM integration. The core platform (module loading, event bus, heartbeat loop) and WebUI dashboard (v1.1) are already built. This milestone adds LLM API calling, chat interface, and conversation context management.

**Existing features (already available):**
- Modular plugin runtime with isolated assembly loading
- Typed contracts, event-driven communication (MediatR)
- Real-time web dashboard with SignalR push updates
- Module management UI (load/unload from browser)
- Heartbeat monitoring with 100ms tick loop

**v1.2 Goal:** Give agents the ability to call LLMs and hold conversations — the first step toward intelligent behavior.

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist in LLM chat applications. Missing these = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Message display (user/assistant/system) | Standard chat pattern — users need to see conversation flow | LOW | Role-based styling (user=right, assistant=left), timestamp optional for v1.2 |
| Text input for user messages | Core interaction — how users communicate with LLM | LOW | Textarea with send button, Enter to send (Shift+Enter for newline) |
| Streaming response display | Modern LLM UX standard — users expect token-by-token output | MEDIUM | Requires SSE or streaming API support, incremental DOM updates via SignalR |
| Conversation history display | Users need context of what was said | LOW | Scrollable message list, newest at bottom |
| Auto-scroll to latest message | Users expect to see new messages without manual scroll | LOW | Scroll to bottom on new message, disable if user scrolled up manually |
| Basic error handling | API calls fail (rate limit, auth, network) — users need to know why | LOW | Display error messages in chat or toast notification |
| API configuration | Users need to set endpoint, API key, model name | LOW | Settings panel or config file (appsettings.json), validate before first call |
| Message role formatting | OpenAI API requires system/user/assistant message structure | LOW | Internal message format matching API spec (role + content) |
| Token/context window awareness | LLM APIs have token limits — app must respect them | MEDIUM | Token counting library (tiktoken or equivalent), truncate or error before API call |

### Differentiators (Competitive Advantage)

Features that set the chat interface apart. Not required, but valuable.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|----------|
| Copy message content | Users want to extract LLM responses for use elsewhere | LOW | Copy button per message, clipboard API |
| Markdown rendering with code highlighting | LLM responses often include code — readable formatting matters | MEDIUM | Markdown parser (Markdig) + syntax highlighter (Prism.js or highlight.js) |
| Message regeneration | Users want to retry if response is poor quality | MEDIUM | Resend last user message, replace assistant response in history |
| Token usage display | Power users want to track API costs | LOW | Display tokens used per message or total for conversation |
| Context window visualization | Users want to know how much context remains | MEDIUM | Progress bar or indicator showing tokens used vs model limit |
| Typing indicator during streaming | Visual feedback that LLM is responding | LOW | Animated "..." indicator while streaming active |
| Conversation summarization | Extends conversation beyond token limit | HIGH | Summarize old messages to compress context (requires extra LLM call) |
| Multiple system prompts | Users want to test different agent behaviors | LOW | Dropdown or config for system message templates |
| Export conversation | Users want to save conversations for later | LOW | Export as JSON, markdown, or text file |
| Message editing | Users want to rephrase and retry from that point | MEDIUM | Edit user message, regenerate from that point (creates conversation branch) |
| Module integration | Other modules can subscribe to chat events | LOW | Publish ChatMessageSent/ChatResponseReceived events via existing EventBus |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good but create problems for v1.2 scope.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Persistent conversation storage | Users want to save conversations across sessions | Adds database complexity, explicitly out of scope per PROJECT.md | Export to file for now, defer persistence to v1.3+ |
| Multi-conversation management | Users want multiple chat threads | Adds UI complexity, state management overhead, not needed for testing | Single conversation for v1.2, add later if validated |
| Conversation branching UI | Users want to explore alternate responses visually | Complex UX, tree visualization is hard to design well | Message editing creates implicit branches, defer visual tree to v2+ |
| Advanced prompt engineering UI | Users want fine control over temperature, top_p, frequency_penalty, etc. | Overwhelming for most users, niche power-user feature | Expose via config file (appsettings.json), not UI for v1.2 |
| Real-time collaboration | Multiple users in same chat session | Adds auth, sync, conflict resolution complexity | Single-user for v1.2, not needed for agent testing |
| Local model hosting | Users want to avoid API costs, run llama.cpp locally | Explicitly out of scope per PROJECT.md | OpenAI-compatible API only, architecture allows future addition |
| Voice input/output | Users want to speak to agent | Audio complexity, not core to v1.2 goal | Text-only for v1.2, defer to future milestone |
| Image/multimodal support | Users want to send images to vision models | Multimodal complexity, not core to v1.2 goal | Text-only for v1.2, defer to future milestone |

## Feature Dependencies

```
API Configuration
    └──requires──> LLM API Client

LLM API Client
    └──requires──> Message Role Formatting
    └──requires──> Token Counting
    └──requires──> HTTP client with streaming support

Chat Interface (UI)
    └──requires──> Message Display Component
    └──requires──> Text Input Component
    └──requires──> Conversation History State
    └──requires──> SignalR Hub (already exists from v1.1)

Streaming Response Display
    └──requires──> LLM API Client (streaming mode)
    └──requires──> Message Display (incremental updates)
    └──requires──> SignalR Hub (push tokens to browser)

Context Window Management
    └──requires──> Token Counting
    └──requires──> Conversation History
    └──requires──> Model token limit configuration

Message Regeneration
    └──requires──> Conversation History
    └──requires──> LLM API Client

Conversation Summarization
    └──requires──> Context Window Management
    └──requires──> LLM API Client (for summarization call)

Module Integration
    └──requires──> EventBus (already exists from v1.0)
    └──requires──> Chat event types (ChatMessageSent, ChatResponseReceived)

Markdown Rendering ──enhances──> Message Display
Token Usage Display ──enhances──> Context Window Management
Message Editing ──conflicts──> Simple Linear History (creates branches, needs state management)
```

### Dependency Notes

- **Streaming Response Display requires SignalR Hub:** Already exists from v1.1, reuse for pushing streaming tokens to browser
- **Context Window Management requires Token Counting:** Can't manage context without knowing token usage per message
- **Message Regeneration requires Conversation History:** Need to replay conversation up to regeneration point
- **Conversation Summarization requires LLM API Client:** Summarization itself is an LLM call (extra cost + latency)
- **Message Editing conflicts with Simple Linear History:** Editing creates alternate conversation paths (branches), requires more complex state management than linear array
- **Module Integration leverages existing EventBus:** Other modules can react to chat events (e.g., proactive conversation initiator module)

## MVP Definition

### Launch With (v1.2)

Minimum viable LLM integration — what's needed to validate the concept.

- [ ] **LLM API Client** — Core capability, enables all other features
  - OpenAI-compatible endpoint support (OpenAI, Azure OpenAI, Anthropic, etc.)
  - Streaming response support (SSE or chunked transfer)
  - Error handling (rate limit, auth, network, model errors)
- [ ] **API Configuration** — Users must be able to set endpoint, key, model
  - Settings panel in dashboard or appsettings.json
  - Validate configuration before first call
- [ ] **Message Role Formatting** — Required for OpenAI-compatible API calls
  - Internal message structure: { role: "system"|"user"|"assistant", content: string }
  - Convert to API format on send
- [ ] **Token Counting** — Prevents API errors from exceeding context window
  - Token counting library (tiktoken port for C# or equivalent)
  - Count tokens per message and total conversation
- [ ] **Chat Interface (basic)** — Text input, message display, conversation history
  - Textarea for user input (Enter to send, Shift+Enter for newline)
  - Message list with role-based styling
  - Scrollable history container
- [ ] **Streaming Response Display** — Modern UX standard, users expect it
  - Display tokens as they arrive (incremental updates)
  - SignalR push from server to browser
- [ ] **Auto-scroll to Latest** — Basic usability requirement
  - Scroll to bottom on new message
  - Disable auto-scroll if user manually scrolled up
- [ ] **Basic Error Handling** — Users need to know when API calls fail
  - Display error messages in chat or toast
  - Distinguish error types (auth, rate limit, network, model)
- [ ] **Context Window Management** — Truncate or error when approaching limit
  - Token-based truncation: keep messages until 80% of limit
  - Sliding window fallback: drop oldest messages first

### Add After Validation (v1.x)

Features to add once core LLM integration is working and validated.

- [ ] **Markdown Rendering** — Add when users complain about unformatted code blocks
  - Markdig for markdown parsing
  - Prism.js or highlight.js for code syntax highlighting
- [ ] **Copy Message Content** — Add when users want to extract responses
  - Copy button per message
  - Clipboard API integration
- [ ] **Message Regeneration** — Add when users want to retry poor responses
  - Resend last user message
  - Replace assistant response in history
- [ ] **Token Usage Display** — Add when power users want cost tracking
  - Display tokens per message or total conversation
  - Optional: estimate cost based on model pricing
- [ ] **Typing Indicator** — Add for polish, not critical for functionality
  - Animated "..." while streaming active
- [ ] **Export Conversation** — Add when users want to save conversations
  - Export as JSON, markdown, or text file
  - Download button in chat UI
- [ ] **Multiple System Prompts** — Add when users want to test different agent behaviors
  - Dropdown or config for system message templates
  - Predefined templates (helpful assistant, code reviewer, etc.)
- [ ] **Module Integration** — Add when other modules need to react to chat
  - Publish ChatMessageSent/ChatResponseReceived events via EventBus
  - Example: proactive conversation initiator module

### Future Consideration (v2+)

Features to defer until product-market fit is established.

- [ ] **Persistent Conversation Storage** — Defer until database architecture decided (v1.3+)
  - Requires database (SQLite, PostgreSQL, etc.)
  - Schema for conversations, messages, metadata
- [ ] **Multi-conversation Management** — Defer until single conversation validated
  - Sidebar with conversation list
  - Create/delete/switch conversations
- [ ] **Conversation Summarization** — Defer until users hit context limits regularly
  - Summarize old messages to compress context
  - Requires extra LLM call (cost + latency)
- [ ] **Message Editing** — Defer until conversation branching UX designed
  - Edit user message, regenerate from that point
  - Creates conversation branches (complex state management)
- [ ] **Context Window Visualization** — Defer until users need fine-grained awareness
  - Progress bar showing tokens used vs limit
  - Color-coded (green/yellow/red)
- [ ] **Advanced Prompt Engineering UI** — Defer until power users request it
  - Temperature, top_p, frequency_penalty, presence_penalty sliders
  - Max tokens, stop sequences configuration
- [ ] **Voice Input/Output** — Defer until text-based chat validated
  - Speech-to-text for input
  - Text-to-speech for output
- [ ] **Image/Multimodal Support** — Defer until vision models needed
  - Image upload for vision models
  - Image display in chat history

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority | Rationale |
|---------|------------|---------------------|----------|-----------|
| LLM API Client | HIGH | MEDIUM | P1 | Core capability, nothing works without it |
| API Configuration | HIGH | LOW | P1 | Users must be able to set endpoint/key/model |
| Message Role Formatting | HIGH | LOW | P1 | Required for OpenAI-compatible API calls |
| Token Counting | HIGH | MEDIUM | P1 | Prevents API errors, enables context management |
| Chat Interface (basic) | HIGH | LOW | P1 | Core interaction, users need to send messages |
| Streaming Response Display | HIGH | MEDIUM | P1 | Modern UX standard, users expect it |
| Auto-scroll to Latest | MEDIUM | LOW | P1 | Basic usability, easy to implement |
| Basic Error Handling | HIGH | LOW | P1 | Users need failure feedback |
| Context Window Management | HIGH | MEDIUM | P1 | Prevents API errors, enables long conversations |
| Markdown Rendering | MEDIUM | MEDIUM | P2 | Nice-to-have, not blocking |
| Copy Message Content | MEDIUM | LOW | P2 | Useful, but not critical |
| Message Regeneration | MEDIUM | MEDIUM | P2 | Valuable for poor responses, not urgent |
| Token Usage Display | LOW | LOW | P2 | Power user feature, not critical |
| Typing Indicator | LOW | LOW | P2 | Polish, not functionality |
| Export Conversation | LOW | LOW | P2 | Useful, but not urgent |
| Multiple System Prompts | LOW | LOW | P2 | Testing feature, not critical |
| Module Integration | MEDIUM | LOW | P2 | Enables other modules, not urgent |
| Persistent Storage | MEDIUM | HIGH | P3 | Database complexity, defer |
| Multi-conversation Management | MEDIUM | MEDIUM | P3 | Validate single conversation first |
| Conversation Summarization | LOW | HIGH | P3 | Complex, not needed yet |
| Message Editing | MEDIUM | MEDIUM | P3 | Branching complexity, defer |
| Context Window Visualization | LOW | MEDIUM | P3 | Nice-to-have, not urgent |
| Advanced Prompt Engineering UI | LOW | MEDIUM | P3 | Niche feature, defer |

**Priority key:**
- P1: Must have for v1.2 launch (table stakes)
- P2: Should have, add in v1.x (valuable but not blocking)
- P3: Nice to have, future consideration (complex or premature)

## Integration with Existing OpenAnima Features

| Existing Feature | How LLM Features Use It | Notes |
|------------------|-------------------------|-------|
| Modular plugin runtime | LLM client as module, chat UI as module | Leverage existing module loading/unloading |
| Event bus (MediatR) | Modules can subscribe to chat events (message sent, response received) | Enables other modules to react to conversations |
| Web dashboard (Blazor Server) | Chat panel embedded in dashboard | Reuse existing Blazor Server + SignalR infrastructure |
| SignalR real-time push | Stream LLM responses to browser in real-time | Existing SignalR hub can push streaming tokens |
| Heartbeat monitoring | LLM calls don't block heartbeat | Fire-and-forget pattern already established in v1.1 |
| Module registry | Chat module registered like any other module | Standard module lifecycle (load/unload) |

## Context Management Strategies

Based on research, common approaches for handling token limits:

### 1. Sliding Window (Simple)
Keep last N messages, drop oldest when limit approached.

**Pros:**
- Simple to implement (array slice or queue)
- Predictable behavior
- No extra LLM calls

**Cons:**
- Loses early context (system message, initial instructions)
- No semantic awareness (drops important messages)
- Fixed window size may not match token limits

**Recommendation:** Use as fallback for v1.2 MVP

### 2. Token-based Truncation
Keep messages until token limit approached, drop oldest first.

**Pros:**
- Maximizes context usage
- Respects actual token limits (not message count)
- Simple to implement with token counting

**Cons:**
- Still loses early context
- No semantic awareness

**Recommendation:** Use for v1.2 MVP (primary strategy)

### 3. Conversation Summarization
Summarize old messages to compress context, keep recent messages verbatim.

**Pros:**
- Preserves semantic information
- Extends conversation length significantly
- Intelligent context compression

**Cons:**
- Requires extra LLM call (cost + latency)
- Lossy compression (details lost)
- Complexity in deciding when to summarize

**Recommendation:** Defer to v2+ (high complexity, not needed for v1.2)

### 4. Hybrid Approach
Sliding window + summarization for very old messages.

**Pros:**
- Best of both worlds
- Maximizes conversation length with semantic preservation

**Cons:**
- Most complex to implement
- Multiple LLM calls for summarization
- Overkill for v1.2 scope

**Recommendation:** Defer to v2+ (overkill for v1.2)

### v1.2 Recommendation

**Token-based truncation with sliding window fallback:**
1. Count tokens per message on send/receive
2. Keep messages until 80% of context window filled
3. When approaching limit, drop oldest messages first
4. Always preserve system message (if present)
5. Warn user when context truncated

**Why this approach:**
- Simple to implement (no extra LLM calls)
- Predictable behavior (users understand "oldest messages dropped")
- Respects actual token limits (not arbitrary message count)
- Good enough for v1.2 testing and validation

## Competitor Feature Analysis

| Feature | ChatGPT Web | Open WebUI | Claude Web | Our Approach |
|---------|-------------|------------|------------|--------------|
| Streaming responses | Yes, token-by-token | Yes, token-by-token | Yes, token-by-token | Yes — table stakes (P1) |
| Markdown rendering | Yes, with code highlighting | Yes, with code highlighting | Yes, with code highlighting | Yes — P2 for v1.x |
| Message editing | Yes, creates branch | Yes, creates branch | Yes, creates branch | Defer to v2+ (complexity) |
| Conversation history | Persistent, searchable | Persistent, exportable | Persistent, searchable | In-memory only for v1.2 (per PROJECT.md) |
| Multi-conversation | Yes, sidebar with threads | Yes, sidebar with threads | Yes, sidebar with threads | Defer to v2+ (single conversation first) |
| Token usage display | Yes, in settings | Yes, per message | No | Yes — P2 for v1.x |
| System prompt customization | Yes, via "Custom Instructions" | Yes, per conversation | Yes, via "Custom Instructions" | Yes — P2 for v1.x (config file) |
| Export conversation | Yes, JSON/markdown | Yes, JSON/markdown | Yes, markdown | Yes — P2 for v1.x |
| Context summarization | Automatic (hidden) | Manual or automatic | Automatic (hidden) | Defer to v2+ (high complexity) |
| Copy message | Yes, button per message | Yes, button per message | Yes, button per message | Yes — P2 for v1.x |
| Voice input/output | Yes (mobile/desktop) | Plugin support | No | Defer to v2+ (not core) |
| Image/multimodal | Yes (vision models) | Yes (vision models) | Yes (vision models) | Defer to v2+ (not core) |

**Key insights:**
- **Streaming is universal:** All modern LLM chat interfaces stream responses token-by-token
- **Markdown rendering is expected:** Code blocks are common in LLM responses, formatting matters
- **Persistent storage is common:** But PROJECT.md explicitly defers this to v1.3+, in-memory for v1.2
- **Multi-conversation is standard:** But not needed for v1.2 agent testing, single conversation is enough
- **Message editing creates branches:** Complex UX, defer until conversation branching designed

## Sources

### Context Window Management
- [LangGraph Tutorial: Message History Management with Sliding Windows](https://aiproduct.engineer/tutorials/langgraph-tutorial-message-history-management-with-sliding-windows-unit-12-exercise-3) — Sliding window implementation patterns
- [LLM Chat History Summarization Guide October 2025](https://mem0.ai/blog/llm-chat-history-summarization-guide-2025) — Summarization strategies
- [Context Window Management Strategies - ApX Machine Learning](https://apxml.com/courses/langchain-production-llm/chapter-3-advanced-memory-management/context-window-management) — Token-based truncation patterns

### API Best Practices
- [Building Bulletproof LLM Applications: A Guide to Applying SRE Best Practices](https://medium.com/google-cloud/building-bulletproof-llm-applications-a-guide-to-applying-sre-best-practices-1564b72fd22e) — Error handling, retry logic, rate limiting
- [Best practices for handling API rate limits and implementing retry mechanisms](https://community.monday.com/t/best-practices-for-handling-api-rate-limits-and-implementing-retry-mechanisms/106286) — Rate limit handling patterns

### Chat Interface Patterns
- [How to implement message editing and response regeneration features](https://community.latenode.com/t/how-to-implement-message-editing-and-response-regeneration-features-with-langchain-or-langgraph/39411) — Message editing and regeneration UX

### Streaming Responses
- [Responses API streaming - the simple guide to "events"](https://community.openai.com/t/responses-api-streaming-the-simple-guide-to-events/1363122) — OpenAI streaming API patterns
- [OpenAI Responses API: A Comprehensive Guide](https://medium.com/@odhitom09/openai-responses-api-a-comprehensive-guide-ad546132b2ed) — Comprehensive API guide

### Additional Research
- PROJECT.md — Existing features, constraints, out-of-scope items
- Training data on LLM chat interfaces (ChatGPT, Claude, Open WebUI patterns)
- Training data on OpenAI API (chat completion, streaming, token counting)

---
*Feature research for: OpenAnima v1.2 LLM Integration*
*Researched: 2026-02-24*
