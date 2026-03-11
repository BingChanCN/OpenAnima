# Requirements: OpenAnima v1.6 Cross-Anima Routing

**Defined:** 2026-03-11
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

## v1.6 Requirements

Requirements for this milestone. Each maps to roadmap phases.

### Cross-Anima Routing Infrastructure (ROUTE)

- [ ] **ROUTE-01**: CrossAnimaRouter singleton manages port registry with compound-key addressing (animaId::portName)
- [ ] **ROUTE-02**: Cross-Anima requests use full Guid correlation IDs with expiry timestamps
- [ ] **ROUTE-03**: CrossAnimaRouter enforces configurable timeout on pending requests (default 30s)
- [ ] **ROUTE-04**: Periodic cleanup removes expired correlation entries from pending map
- [ ] **ROUTE-05**: Anima deletion triggers CancelPendingForAnima to fail pending requests cleanly
- [ ] **ROUTE-06**: CrossAnimaRouter hooks into AnimaRuntimeManager.DeleteAsync lifecycle

### Routing Modules (RMOD)

- [ ] **RMOD-01**: User can add AnimaInputPort module to declare a named service on an Anima
- [ ] **RMOD-02**: AnimaInputPort registers with CrossAnimaRouter on initialization with service name and description
- [ ] **RMOD-03**: User can add AnimaOutputPort module paired by name with AnimaInputPort for response return
- [ ] **RMOD-04**: AnimaOutputPort completes cross-Anima request via correlation ID through CrossAnimaRouter
- [ ] **RMOD-05**: User can add AnimaRoute module and select target Anima via dropdown
- [ ] **RMOD-06**: User can select target remote input port via second dropdown (populated from selected Anima's registered ports)
- [ ] **RMOD-07**: AnimaRoute sends request and awaits response synchronously within wiring tick
- [ ] **RMOD-08**: AnimaRoute exposes error/timeout output port for routing failure handling in wiring

### Prompt Auto-Injection (PROMPT)

- [ ] **PROMPT-01**: LLMModule system prompt auto-includes descriptions of available cross-Anima services
- [ ] **PROMPT-02**: Prompt injection respects token budget cap (200-300 tokens) to prevent context exhaustion
- [ ] **PROMPT-03**: Prompt injection includes format instructions for LLM to trigger routing
- [ ] **PROMPT-04**: Prompt injection skips when no routes are configured for current Anima

### Format Detection (FMTD)

- [ ] **FMTD-01**: FormatDetector scans LLM output for routing markers after response completes
- [ ] **FMTD-02**: FormatDetector splits passthrough text from routing payload
- [ ] **FMTD-03**: FormatDetector dispatches extracted routing calls to CrossAnimaRouter
- [ ] **FMTD-04**: Format detection handles near-miss and malformed markers gracefully (no crash)

### HTTP Request Module (HTTP)

- [ ] **HTTP-01**: User can add HttpRequest module with configurable URL, HTTP method, headers, and body template
- [ ] **HTTP-02**: HttpRequest module uses IHttpClientFactory with resilience pipeline
- [ ] **HTTP-03**: HttpRequest module outputs response body and status code via separate output ports
- [ ] **HTTP-04**: HttpRequest module enforces 10s default timeout with heartbeat CancellationToken passthrough
- [ ] **HTTP-05**: HttpRequest module blocks requests to localhost and private IP ranges

## v1.7+ Requirements

Deferred to future releases. Tracked but not in current roadmap.

### Routing Enhancements

- **RMOD-09**: AnimaRoute supports dynamic target — target Anima supplied via input port at runtime
- **RMOD-10**: Multi-hop routing chains with nested correlation IDs and timeout propagation
- **RMOD-11**: Fan-out routing with scatter-gather correlation pattern

### LLM Integration Enhancements

- **FMTD-05**: Streaming display with parallel format detection (buffer copy for detection while streaming to chat)
- **PROMPT-05**: Service description customization per-route (user-authored descriptions override auto-generated)

### HTTP Enhancements

- **HTTP-06**: First-class auth config fields (Bearer token, Basic auth) instead of manual header entry
- **HTTP-07**: HTTP response streaming to downstream modules
- **HTTP-08**: Full URL allowlist with user-configurable permitted domains

### Advanced Routing

- **ROUTE-07**: Tiered thinking loop integration — routing decisions inform agent reasoning depth
- **ROUTE-08**: Agent memory persistence across routing calls

## Out of Scope

| Feature | Reason |
|---------|--------|
| External message broker (RabbitMQ, etc.) | In-process routing is sufficient; keeps deployment simple |
| Language-agnostic module protocol | Separate concern, not related to routing |
| Module marketplace/store | UI polish, not architectural |
| Async fire-and-forget routing | Breaks topological execution; defer until async execution model exists |
| Full SSRF allowlist UX | URLs are user-configured in sidebar; minimal private IP blocking sufficient for v1.6 |
| Background execution / long-running tasks | Requires separate execution model; out of scope for routing |

## Traceability

Populated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| ROUTE-01 | — | Pending |
| ROUTE-02 | — | Pending |
| ROUTE-03 | — | Pending |
| ROUTE-04 | — | Pending |
| ROUTE-05 | — | Pending |
| ROUTE-06 | — | Pending |
| RMOD-01 | — | Pending |
| RMOD-02 | — | Pending |
| RMOD-03 | — | Pending |
| RMOD-04 | — | Pending |
| RMOD-05 | — | Pending |
| RMOD-06 | — | Pending |
| RMOD-07 | — | Pending |
| RMOD-08 | — | Pending |
| PROMPT-01 | — | Pending |
| PROMPT-02 | — | Pending |
| PROMPT-03 | — | Pending |
| PROMPT-04 | — | Pending |
| FMTD-01 | — | Pending |
| FMTD-02 | — | Pending |
| FMTD-03 | — | Pending |
| FMTD-04 | — | Pending |
| HTTP-01 | — | Pending |
| HTTP-02 | — | Pending |
| HTTP-03 | — | Pending |
| HTTP-04 | — | Pending |
| HTTP-05 | — | Pending |

**Coverage:**
- v1.6 requirements: 27 total
- Mapped to phases: 0 (pending roadmap)
- Unmapped: 27 ⚠️

---
*Requirements defined: 2026-03-11*
*Last updated: 2026-03-11 after initial definition*
