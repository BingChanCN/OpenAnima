# Requirements: OpenAnima

**Defined:** 2026-02-24
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe

## v1.2 Requirements

Requirements for LLM Integration milestone. Each maps to roadmap phases.

### LLM API Client

- [x] **LLM-01**: User can configure LLM endpoint, API key, and model name via appsettings.json
- [x] **LLM-02**: Runtime can call OpenAI-compatible chat completion API with system/user/assistant messages
- [x] **LLM-03**: Runtime can receive streaming responses token-by-token from LLM API
- [x] **LLM-04**: User sees meaningful error messages when API calls fail (auth, rate limit, network, model errors)
- [x] **LLM-05**: Runtime retries transient API failures with exponential backoff

### Chat Interface

- [x] **CHAT-01**: User can send text messages to the agent from a chat panel in the dashboard
- [x] **CHAT-02**: User sees conversation history with role-based styling (user right, assistant left)
- [x] **CHAT-03**: User sees streaming LLM responses appear token-by-token in real time
- [x] **CHAT-04**: Chat auto-scrolls to latest message unless user has scrolled up manually
- [x] **CHAT-05**: User can copy any message content to clipboard
- [x] **CHAT-06**: User can regenerate the last assistant response
- [x] **CHAT-07**: User sees Markdown-formatted responses with code block syntax highlighting

### Context Management

- [x] **CTX-01**: Runtime counts tokens per message using tiktoken-compatible library
- [x] **CTX-02**: Runtime automatically truncates oldest messages when approaching context window limit (preserving system message)
- [x] **CTX-03**: User can see current token usage and remaining context capacity
- [x] **CTX-04**: Chat events (message sent, response received) are published to EventBus for module integration

## Future Requirements

Deferred to future milestones. Tracked but not in current roadmap.

### Conversation Persistence

- **PERS-01**: Conversation history persists across application restarts
- **PERS-02**: User can browse and resume previous conversations

### Advanced Chat

- **ACHAT-01**: User can edit a previous message and regenerate from that point
- **ACHAT-02**: User can manage multiple concurrent conversations
- **ACHAT-03**: User can export conversation as JSON or Markdown file
- **ACHAT-04**: User can select from predefined system prompt templates

### Intelligent Context

- **ICTX-01**: Runtime summarizes old messages to compress context when approaching limit
- **ICTX-02**: User sees visual context window indicator (progress bar with color coding)


## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Tiered thinking loop | Deferred — build LLM foundation first, add triage layer in future milestone |
| Persistent conversation storage | Database complexity, in-memory only for v1.2 |
| Multi-conversation management | Validate single conversation first |
| Message editing with branching | Complex state management, defer to v2+ |
| Advanced prompt engineering UI (temperature, top_p sliders) | Niche power-user feature, use appsettings.json |
| Voice input/output | Not core to v1.2 goal |
| Image/multimodal support | Not core to v1.2 goal |
| Local model hosting (llama.cpp) | Cloud LLM only per PROJECT.md constraints |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| LLM-01 | Phase 8 | Complete |
| LLM-02 | Phase 8 | Complete |
| LLM-03 | Phase 8 | Complete |
| LLM-04 | Phase 8 | Complete |
| LLM-05 | Phase 8 | Complete |
| CHAT-01 | Phase 9 | Complete |
| CHAT-02 | Phase 9 | Complete |
| CHAT-03 | Phase 9 | Complete |
| CHAT-04 | Phase 9 | Complete |
| CHAT-05 | Phase 9 | Complete |
| CHAT-06 | Phase 9 | Complete |
| CHAT-07 | Phase 9 | Complete |
| CTX-01 | Phase 10 | Complete |
| CTX-02 | Phase 10 | Complete |
| CTX-03 | Phase 10 | Complete |
| CTX-04 | Phase 10 | Complete |

**Coverage:**
- v1.2 requirements: 16 total
- Mapped to phases: 16
- Unmapped: 0 ✓
- Complete: 12 (75%)

---
*Requirements defined: 2026-02-24*
*Last updated: 2026-02-25 after Phase 9 completion*
