# Feature Research

**Domain:** Cross-Anima Routing and HTTP Request Module — v1.6
**Researched:** 2026-03-11
**Confidence:** HIGH (core patterns well-established; format detection specifics MEDIUM)

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist in a multi-agent wiring platform. Missing these makes the routing concept feel broken.

#### AnimaInputPort Module

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Declare a named service on an Anima | Any "service provider" concept requires a registration point | LOW | Config: service name (string), service description (string for LLM injection) |
| Service name uniqueness per Anima | Two ports with the same name would create ambiguous routing | LOW | Validate on save; show error in config sidebar |
| Service description field | LLM must understand what the service does to route to it | LOW | Free-text; injected verbatim into system prompt |
| Output port for incoming request payload | The received message must flow into the wiring graph | LOW | Single Text output port: `request` |
| Correlation ID passthrough | Response must be routed back to the correct caller | MEDIUM | Store correlation ID in module state while request is in-flight; emit alongside response |

#### AnimaOutputPort Module

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Paired to AnimaInputPort by name | Response must return to the same channel that received the request | LOW | Config: port name (must match an AnimaInputPort name on the same Anima) |
| Input port for response text | Accepts the wired response and sends it back to the caller | LOW | Single Text input port: `response` |
| Correlation ID automatically matched | Output port must route reply to the original requester, not broadcast | MEDIUM | Reads correlation ID from sibling AnimaInputPort state; router matches on it |

#### AnimaRoute Module

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Select target Anima | User must be able to designate which Anima handles this request | LOW | Config: dropdown of all known Animas (populated from AnimaRuntimeManager) |
| Select target input port | Multiple services may be registered on target Anima | LOW | Config: dropdown of AnimaInputPort names registered on the selected Anima |
| Input port for request text | Accepts the text payload to forward | LOW | Single Text input port: `request` |
| Output port for received response | The caller needs the response back in its wiring graph | LOW | Single Text output port: `response` |
| Correlation ID generated per call | Each request needs a unique ID to match against async response | MEDIUM | Generate `Guid.NewGuid().ToString("N")[..8]` on each execution |
| Configurable timeout | Prevent indefinite blocking when target Anima fails to respond | MEDIUM | Config: timeout in seconds (default 30); return error text on expiry |

#### Cross-Anima Message Router (Infrastructure)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Global routing registry accessible to all Animas | AnimaRoute must reach AnimaInputPort across runtime isolation boundaries | MEDIUM | Singleton `AnimaRouterService` registered in the DI container; holds a `ConcurrentDictionary<correlationId, TaskCompletionSource>` |
| Correlation ID tracking | Request-response matching is impossible without it | MEDIUM | Standard enterprise integration pattern; key in router's pending-request map |
| Timeout and orphan response cleanup | Late responses after timeout must be silently discarded | MEDIUM | `CancellationTokenSource` per pending request; remove entry from map on expiry or completion |
| Thread-safe async delivery | Multiple Animas may send/receive simultaneously | MEDIUM | `TaskCompletionSource<string>` per request; `await` on caller side; `SetResult` on responder side |

#### Prompt Auto-Injection

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| LLM automatically knows available services | Without injection, LLM has no basis for routing decisions | MEDIUM | LLMModule reads all AnimaRoute modules in current Anima's wiring; assembles service list from target Anima's AnimaInputPort descriptions |
| Service list format in system prompt | LLM needs a clear, parseable format to produce routing markers | LOW | Block appended to system prompt: `# Available Services\n- ServiceName: Description\n...` |
| Injection only when routes exist | Injecting an empty block when no routes are wired is noise | LOW | Skip injection if no AnimaRoute modules are configured |
| Update injection when wiring changes | Stale service list leads to routing errors | MEDIUM | Re-read wiring on each LLM execution; injection is stateless (computed per call) |

#### Format Detection (Output Monitoring)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Detect routing marker in LLM output | LLM must signal when it wants to route | MEDIUM | Scan full output text for marker pattern after streaming completes |
| Split: routing payload vs. display text | User sees the conversational reply; routing payload goes to the wire | MEDIUM | Marker block is removed from displayed text; extracted payload is forwarded |
| Defined marker format | Ambiguous detection leads to false positives | LOW | XML-style tags are the established pattern: `<route service="ServiceName">payload</route>` — widely used in LLM structured output |
| No marker = normal response | Non-routing responses must pass through unchanged | LOW | If marker absent, emit full text to chat output port as before |

### Differentiators (Competitive Advantage)

Features that set OpenAnima apart from simple HTTP-connected agents or static pipelines.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Service descriptions in prompt auto-injection | LLM understands *what* each service does, not just that it exists; enables semantic routing without hardcoded rules | LOW | Free-text description field on AnimaInputPort config is the lever |
| Wiring-native routing (not code) | Users configure routing in the visual editor, not code; consistent with existing module philosophy | MEDIUM | AnimaInputPort/OutputPort/Route are first-class modules, not a special API |
| Correlation ID via wiring state (not HTTP session) | Stateless modules pass correlation context through the routing system without exposing it to users | MEDIUM | Routing infrastructure is invisible to the wiring graph; users see clean input/output ports |
| Format detection preserves streaming display | User sees the LLM's conversational response even when a routing action is embedded in it | HIGH | Requires buffering stream, detecting marker after completion, then splitting before forwarding |
| Timeout as a config field | Users control failure behavior without writing code | LOW | Simple timeout dropdown in AnimaRoute config sidebar |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Bidirectional/broadcast routing | "Let Anima A talk to multiple Animas simultaneously" | Correlation ID tracking breaks with fan-out; response ordering becomes non-deterministic | Keep point-to-point for v1.6; fan-out can be composed with multiple AnimaRoute nodes |
| LLM-initiated multi-hop routing chains | "Let the LLM chain service calls automatically" | Recursive correlation IDs, nested timeouts, hard to debug, easy to create infinite loops | Single-hop only for v1.6; manual wiring chains are explicit and deterministic |
| Streaming HTTP request body | "Stream large payloads to HTTP endpoints" | Significantly complicates the module lifecycle; blocks execution level until stream ends | Use buffered body; streaming can be added later if demand exists |
| Dynamic route discovery at runtime | "Auto-discover what services Animas expose" | AnimaRuntimeManager already has this data; the complication is caching + invalidation when Animas are deleted | Read live from AnimaRuntimeManager on each execution |
| HTTP response streaming to downstream modules | "Get tokens as they arrive from external API" | Downstream modules in the wiring graph are synchronous execution nodes; they cannot consume a stream | Buffer full response, emit as single Text payload |
| Global system prompt for all Animas | "Configure routing instructions once" | Violates per-Anima isolation; one Anima's service list polluting another's context | Per-Anima injection computed from that Anima's wiring only |

---

## Feature Dependencies

```
[AnimaRoute Module]
    ├──requires──> [AnimaInputPort on target Anima] (target must declare service)
    ├──requires──> [AnimaRouterService (global singleton)] (cross-runtime delivery)
    └──requires──> [Correlation ID infrastructure] (request-response matching)

[AnimaOutputPort Module]
    ├──requires──> [AnimaInputPort (same Anima, matching name)] (paired by config)
    └──requires──> [AnimaRouterService] (to resolve and deliver response)

[Prompt Auto-Injection]
    ├──requires──> [AnimaRoute modules exist in wiring] (source of service list)
    ├──requires──> [AnimaInputPort on target Anima] (source of service description)
    └──enhances──> [LLMModule] (appends to system prompt at call time)

[Format Detection]
    ├──requires──> [Prompt Auto-Injection] (LLM must know the marker format to produce it)
    ├──requires──> [AnimaRoute Module] (target to forward extracted payload to)
    └──requires──> [LLMModule streaming output] (must buffer stream before splitting)

[HTTP Request Module]
    └──requires──> [Module config sidebar] (already exists v1.5) (URL, method, headers, body template fields)

[AnimaRouterService]
    └──requires──> [AnimaRuntimeManager] (to resolve target Anima runtime)
```

### Dependency Notes

- **AnimaRoute requires AnimaInputPort on the target:** The route's dropdown can only show services that have been declared. If a user selects a target Anima with no declared services, the dropdown is empty — handle gracefully with a "No services available" placeholder.
- **AnimaOutputPort requires a paired AnimaInputPort by name:** Enforce this at config-save time. If no matching AnimaInputPort exists on the same Anima, show a validation error. This prevents silent routing failures.
- **Prompt Auto-Injection requires Format Detection:** Injection without detection is useless — the LLM will produce the marker but nothing will consume it. These two features must ship together in the same phase.
- **Format Detection requires buffered streaming:** The current LLMModule streams tokens. Detection must run on the complete output. Two approaches: (1) Buffer the full text, detect, then emit — delays display but is simple. (2) Stream to chat display while buffering a copy for detection — preserves streaming UX but is higher complexity. Start with (1); upgrade to (2) as a differentiator.
- **HTTP Request Module is independent:** No dependency on cross-Anima routing. Can be implemented in a parallel phase.

---

## MVP Definition

### Launch With (v1.6)

Minimum set that makes cross-Anima routing usable end-to-end.

- [ ] **AnimaInputPort module** — Service declaration with name + description config; output port `request`
- [ ] **AnimaOutputPort module** — Paired-by-name response return; input port `response`
- [ ] **AnimaRoute module** — Target Anima + port dropdowns; correlation ID generation; configurable timeout; input port `request`, output port `response`
- [ ] **AnimaRouterService (global singleton)** — Thread-safe correlation ID map; pending request tracking; timeout cleanup; orphan response discard
- [ ] **Prompt Auto-Injection in LLMModule** — Compute service list from current Anima's AnimaRoute nodes; append to system prompt block
- [ ] **Format Detection in LLMModule** — Post-stream scan for `<route service="...">...</route>` marker; split output; forward payload to AnimaRoute input port
- [ ] **HTTP Request module** — URL (static or template), Method (GET/POST/PUT/DELETE/PATCH), Headers (key-value pairs), Body template, response body output port, status code output port

### Add After Validation (v1.x)

- [ ] **Streaming display with parallel detection** — Buffer a copy for detection while streaming to chat; eliminates display latency during routing calls
- [ ] **Route error port** — Second output port on AnimaRoute for timeout/failure paths; lets wiring handle errors gracefully
- [ ] **AnimaRoute dynamic target** — Allow target Anima to be supplied via input port at runtime, not only config; enables dynamic dispatch
- [ ] **HTTP response headers output port** — Second output on HTTP Request module for header inspection
- [ ] **HTTP Request authentication fields** — Bearer token, Basic auth as first-class config fields (not manual header entry)

### Future Consideration (v2+)

- [ ] **Multi-hop routing chains** — Routing through more than one Anima; requires nested correlation ID tracking and timeout propagation
- [ ] **Fan-out routing** — Single request broadcast to multiple target Animas; requires scatter-gather correlation pattern
- [ ] **HTTP Request streaming response** — Progressive token delivery from HTTP endpoint to downstream modules
- [ ] **Service versioning on AnimaInputPort** — Declare version alongside service name; AnimaRoute selects by version constraint

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| AnimaInputPort module | HIGH | LOW | P1 |
| AnimaOutputPort module | HIGH | LOW | P1 |
| AnimaRoute module | HIGH | MEDIUM | P1 |
| AnimaRouterService (global) | HIGH | MEDIUM | P1 |
| Prompt Auto-Injection | HIGH | MEDIUM | P1 |
| Format Detection (post-stream) | HIGH | MEDIUM | P1 |
| HTTP Request module | HIGH | MEDIUM | P1 |
| Route error/timeout output port | MEDIUM | LOW | P2 |
| Streaming display + parallel detection | MEDIUM | HIGH | P2 |
| HTTP auth fields (Bearer/Basic) | MEDIUM | LOW | P2 |
| HTTP response headers port | LOW | LOW | P2 |
| AnimaRoute dynamic target (runtime) | MEDIUM | MEDIUM | P2 |
| Multi-hop routing chains | LOW | HIGH | P3 |
| Fan-out routing | LOW | HIGH | P3 |

**Priority key:**
- P1: Must have for v1.6 launch
- P2: Should have, add in v1.7+
- P3: Future consideration

---

## Implementation Detail Notes

### Cross-Anima Router Lifecycle

The `AnimaRouterService` must be a global singleton (registered in the root DI container, not per-Anima). Each `AnimaRuntime` has its own `EventBus` — cross-Anima messages cannot use the EventBus. The router holds:

```csharp
// Conceptual structure
ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests;

Task<string> SendRequestAsync(string targetAnimaId, string inputPortName, string payload, TimeSpan timeout, CancellationToken ct)
// AnimaRoute calls this; awaits the TCS result

void DeliverResponse(string correlationId, string responseText)
// AnimaOutputPort calls this; sets TCS result
```

Orphan responses (after timeout) call `SetResult` on a TCS that has already been removed from the map — they are silently no-ops. This is safe because `ConcurrentDictionary.TryGetValue` returns false.

Correlation IDs are short hex strings (same pattern as Anima IDs: `Guid.NewGuid().ToString("N")[..8]`). They are only live for the duration of a single request-response cycle.

### Prompt Auto-Injection Format

The injected system prompt block (appended, not prepended — preserve user-written system prompt):

```
# Available Services
You can route requests to the following services by including a route marker in your response.
Format: <route service="ServiceName">payload to send</route>

Services:
- ServiceName: Description of what this service does and when to use it
- AnotherService: Description...

When using a route marker, include your conversational response before the marker.
```

This format follows the XML-tag convention that Claude, GPT-4o, and most models handle reliably for structured output detection (HIGH confidence — documented in LLM structured output literature).

### Format Detection Marker

Use XML-style tags because:
- Models are trained on XML/HTML and produce it reliably
- Tags are visually distinct and unlikely to appear in normal prose
- Easy to detect with a single `string.Contains` + `Regex.Match` in C#
- Consistent with how Claude's own tool-use format works

Pattern for detection (C# Regex):
```csharp
@"<route\s+service=""([^""]+)"">([^<]*)</route>"
```

Split strategy for v1.6 (simple, post-stream):
1. Buffer full LLM output after streaming completes
2. Match regex against buffer
3. If match found: emit `buffer[..match.Index]` as chat display text; emit `match.Groups[2].Value` as route payload; identify `match.Groups[1].Value` as target service name
4. If no match: emit full buffer as chat display text unchanged

### HTTP Request Module Configuration Fields

Based on Node-RED's HTTP Request node (the canonical reference for this pattern), the module should expose:

| Config Field | Type | Description | Notes |
|--------------|------|-------------|-------|
| URL | text | Full URL (static) or template with `{{portName}}` placeholders | Template resolution at execute time from upstream port values |
| Method | dropdown | GET, POST, PUT, DELETE, PATCH | Default: GET |
| Headers | key-value list | Static header pairs (Content-Type, Authorization, etc.) | UI: row-based key/value editor |
| Body Template | textarea | Request body with `{{portName}}` placeholders for Text input ports | Only shown/relevant for POST/PUT/PATCH |
| Response Format | dropdown | Text (raw), JSON (parsed) | JSON mode emits the response body as a JSON string |
| Timeout (seconds) | number | Request timeout | Default: 30 |

Port declarations:
- Input: `trigger` (Trigger type) to initiate a GET request with no body, OR `body` (Text type) for POST/PUT body
- Output: `response` (Text type) — response body
- Output: `statusCode` (Text type) — HTTP status code as string

Key decision: keep headers as static key-value config (not template-expanded), same as Node-RED. Dynamic headers can be handled by upstream TextJoin modules for simple cases.

---

## Competitor Feature Analysis

| Feature | n8n | Node-RED | AutoGen/LangGraph | Our Approach |
|---------|-----|----------|-------------------|--------------|
| Agent-to-agent routing | Sub-workflow as tool; Switch node for dispatch | No native agent routing; HTTP in/out nodes | Native multi-agent orchestration (code-level) | Wiring modules (AnimaInputPort/Route); visual, no code |
| Service declaration | Sub-workflow exposed as tool | HTTP endpoint declared as server | Agent registered in orchestrator code | AnimaInputPort with service description field |
| Prompt auto-injection | Manual: user writes system prompt listing tools | N/A | Framework injects tool schema automatically | Automatic: LLMModule reads wiring and injects |
| Format detection | n8n parses LLM tool-call JSON response natively | Not applicable | Framework-level structured output parsing | Custom XML-tag marker + post-stream regex scan |
| HTTP request module | HTTP Request node with full config | HTTP Request node (canonical reference) | HTTP tool via LangChain/custom code | Module with URL/method/headers/body template config |
| Correlation ID | Workflow execution ID, transparent to user | Not applicable | Task ID within agent runtime | Short hex ID in AnimaRouterService, transparent to user |
| Timeout handling | Configurable per node | Configurable per HTTP request node | Framework-level retry config | Configurable per AnimaRoute and HTTP Request module |

**Key Insights:**
- **n8n's Sub-workflow-as-tool pattern** is the closest analog to AnimaInputPort — validated, widely used
- **Node-RED HTTP Request node** is the definitive reference for HTTP module UX: URL template + method dropdown + headers + body
- **LangGraph/AutoGen** inject tool schemas automatically — our prompt auto-injection should match this behavior for non-developer users
- **XML-tag format detection** is the approach Claude uses natively for tool calls — high reliability with OpenAI-compatible models

---

## Sources

- [Developer's guide to multi-agent patterns in ADK - Google Developers Blog](https://developers.googleblog.com/developers-guide-to-multi-agent-patterns-in-adk/)
- [Request-based - Multi-agent Reference Architecture](https://microsoft.github.io/multi-agent-reference-architecture/docs/agents-communication/Request-Based.html)
- [Correlation Identifier - Enterprise Integration Patterns](https://www.enterpriseintegrationpatterns.com/patterns/messaging/CorrelationIdentifier.html)
- [Node-RED HTTP Request Node - FlowFuse](https://flowfuse.com/node-red/core-nodes/http-request/)
- [Set the URL of a request using a template - Node-RED Cookbook](https://cookbook.nodered.org/http/set-request-url-template)
- [Set a request header - Node-RED Cookbook](https://cookbook.nodered.org/http/set-request-header)
- [Achieving Tool Calling Functionality in LLMs Using Only Prompt Engineering Without Fine-Tuning](https://arxiv.org/html/2407.04997v1)
- [Effective Prompt Engineering: Mastering XML Tags for Clarity, Precision, and Security in LLMs](https://medium.com/@TechforHumans/effective-prompt-engineering-mastering-xml-tags-for-clarity-precision-and-security-in-llms-992cae203fdc)
- [Building your first multi-agent system with n8n](https://medium.com/mitb-for-all/building-your-first-multi-agent-system-with-n8n-0c959d7139a1)
- [Building Production-Ready Multi-Agent Systems: Architecture Patterns and Best Practices](https://www.getmaxim.ai/articles/best-practices-for-building-production-ready-multi-agent-systems/)
- [Taming LLM Outputs: Your Guide to Structured Text Generation](https://www.dataiku.com/stories/blog/your-guide-to-structured-text-generation)
- [How to Implement Request-Reply Pattern in RabbitMQ](https://oneuptime.com/blog/post/2026-01-27-rabbitmq-request-reply/view)

---

*Feature research for: OpenAnima v1.6 Cross-Anima Routing + HTTP Request Module*
*Researched: 2026-03-11*
