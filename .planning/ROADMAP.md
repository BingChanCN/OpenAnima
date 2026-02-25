# Roadmap: OpenAnima

## Milestones

- ✅ **v1.0 Core Platform Foundation** — Phases 1-2 (shipped 2026-02-21)
- ✅ **v1.1 WebUI Runtime Dashboard** — Phases 3-7 (shipped 2026-02-23)
- 🚧 **v1.2 LLM Integration** — Phases 8-10 (in progress)

## Phases

<details>
<summary>✅ v1.0 Core Platform Foundation (Phases 1-2) — SHIPPED 2026-02-21</summary>

- [x] Phase 1: Core Plugin System (3/3 plans) — completed 2026-02-21
- [x] Phase 2: Event Bus & Heartbeat Loop (2/2 plans) — completed 2026-02-21

See: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md) for full details.

</details>

<details>
<summary>✅ v1.1 WebUI Runtime Dashboard (Phases 3-7) — SHIPPED 2026-02-23</summary>

- [x] Phase 3: Service Abstraction & Hosting (2/2 plans) — completed 2026-02-22
- [x] Phase 4: Blazor UI with Static Display (2/2 plans) — completed 2026-02-22
- [x] Phase 5: SignalR Real-Time Updates (2/2 plans) — completed 2026-02-22
- [x] Phase 6: Control Operations (2/2 plans) — completed 2026-02-22
- [x] Phase 7: Polish & Validation (2/2 plans) — completed 2026-02-23

See: [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md) for full details.

</details>

<details open>
<summary>🚧 v1.2 LLM Integration (Phases 8-10) — IN PROGRESS</summary>

- [x] **Phase 8: API Client Setup & Configuration** - LLM API integration with streaming, error handling, and retry logic
- [x] **Phase 9: Chat UI with Streaming** - Real-time chat interface with streaming responses and conversation history
- [ ] **Phase 10: Context Management & Token Counting** - Token tracking and automatic context window management

</details>

## Phase Details

### Phase 8: API Client Setup & Configuration
**Goal**: Runtime can call LLM APIs with proper configuration, error handling, and retry logic
**Depends on**: Nothing (first phase of v1.2)
**Requirements**: LLM-01, LLM-02, LLM-03, LLM-04, LLM-05
**Success Criteria** (what must be TRUE):
  1. User can configure LLM endpoint, API key, and model via appsettings.json and see successful connection
  2. User can send a message and receive a complete LLM response
  3. User sees streaming tokens appear in real-time during LLM response
  4. User sees clear error messages when API calls fail (auth, rate limit, network errors)
  5. User observes automatic retry on transient failures without manual intervention
**Plans:** 2/2 plans complete
Plans:
- [x] 08-01-PLAN.md — Configuration model, appsettings.json, ILLMService interface, LLMService with error handling
- [x] 08-02-PLAN.md — Streaming implementation, DI registration, SignalR timeout configuration

### Phase 9: Chat UI with Streaming
**Goal**: Users can have real-time conversations with streaming LLM responses
**Depends on**: Phase 8
**Requirements**: CHAT-01, CHAT-02, CHAT-03, CHAT-04, CHAT-05, CHAT-06, CHAT-07
**Success Criteria** (what must be TRUE):
  1. User can type and send messages from chat panel in dashboard
  2. User sees conversation history with user messages on right, assistant on left
  3. User sees LLM responses stream token-by-token in real-time
  4. Chat auto-scrolls to latest message unless user has scrolled up
  5. User can copy any message content to clipboard
  6. User can regenerate the last assistant response
  7. User sees Markdown-formatted responses with syntax-highlighted code blocks
**Gap Closure:** Closes CHAT-01 through CHAT-07 from v1.2 audit
**Plans:** 2/2 plans complete
Plans:
- [x] 09-01-PLAN.md — Core chat UI with streaming (ChatPanel, ChatMessage, ChatInput, JS helpers, Dashboard integration)
- [x] 09-02-PLAN.md — Markdown rendering, copy-to-clipboard, regenerate, human verification

### Phase 10: Context Management & Token Counting
**Goal**: Conversations stay within context limits with accurate token tracking
**Depends on**: Phase 9
**Requirements**: CTX-01, CTX-02, CTX-03, CTX-04
**Success Criteria** (what must be TRUE):
  1. User can see current token count and remaining context capacity
  2. User can have multi-turn conversations (20+ messages) without hitting context limit errors
  3. User observes oldest messages automatically removed when approaching context limit
  4. User sees chat events published to EventBus (visible in module logs or future modules)
**Gap Closure:** Closes CTX-01 through CTX-04 from v1.2 audit
**Plans:** 1/2 plans executed
Plans:
- [ ] 10-01-PLAN.md — Backend services: TokenCounter, ChatContextManager, ChatEvents, LLMService usage capture
- [ ] 10-02-PLAN.md — UI integration: TokenUsageDisplay, ChatPanel context management, send blocking, EventBus publishing

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Core Plugin System | v1.0 | 3/3 | Complete | 2026-02-21 |
| 2. Event Bus & Heartbeat Loop | v1.0 | 2/2 | Complete | 2026-02-21 |
| 3. Service Abstraction & Hosting | v1.1 | 2/2 | Complete | 2026-02-22 |
| 4. Blazor UI with Static Display | v1.1 | 2/2 | Complete | 2026-02-22 |
| 5. SignalR Real-Time Updates | v1.1 | 2/2 | Complete | 2026-02-22 |
| 6. Control Operations | v1.1 | 2/2 | Complete | 2026-02-22 |
| 7. Polish & Validation | v1.1 | 2/2 | Complete | 2026-02-23 |
| 8. API Client Setup & Configuration | v1.2 | 2/2 | Complete | 2026-02-24 |
| 9. Chat UI with Streaming | v1.2 | 2/2 | Complete | 2026-02-25 |
| 10. Context Management & Token Counting | 1/2 | In Progress|  | - |
