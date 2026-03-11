# Project Research Summary

**Project:** OpenAnima v1.6 — Cross-Anima Routing + HTTP Request Module
**Domain:** In-process multi-agent routing and HTTP tooling for a Blazor Server LLM wiring platform
**Researched:** 2026-03-11
**Confidence:** HIGH

## Executive Summary

OpenAnima v1.6 extends the existing per-Anima isolation model to enable controlled cross-agent communication. The v1.5 architecture is deliberately isolated — each Anima has its own `EventBus`, `WiringEngine`, and `HeartbeatLoop`. V1.6 adds a `CrossAnimaRouter` singleton that sits above all `AnimaRuntime` instances (alongside `AnimaRuntimeManager`) and acts as the only sanctioned bridge between them. Cross-Anima request-response is implemented via the standard .NET `TaskCompletionSource<string>` + `ConcurrentDictionary<string, TCS>` correlation pattern — a zero-dependency approach used by SignalR internals and RabbitMQ .NET clients. The HTTP Request module requires one new NuGet package (`Microsoft.Extensions.Http.Resilience 8.7.0`); all other capabilities are BCL built-ins.

The key design tension is that `AnimaRouteModule.ExecuteAsync` must `await` the cross-Anima response while suspended inside the calling Anima's `HeartbeatLoop` tick. This is architecturally correct — the WiringEngine needs the response before executing downstream modules — but means the calling Anima's heartbeat is blocked during cross-Anima LLM calls (typically 2–10 seconds). This is an accepted trade-off for v1.6. All four new modules (`AnimaInputPort`, `AnimaOutputPort`, `AnimaRoute`, `HttpRequest`) are standard `IModuleExecutor` implementations, so they plug into the wiring editor, config sidebar, and port system without any framework changes.

The highest-risk areas are: (1) isolation boundary integrity — the global `IEventBus` singleton (tech debt ANIMA-08) must never be used as the cross-Anima transport; (2) LLM format detection reliability — XML-tag or `@@ROUTE:..@@` markers achieve only 80–95% compliance via prompt engineering versus 99%+ with tool-call APIs; (3) HTTP module security — URL allowlists and SSRF prevention must be built in from day one, not retrofitted. With those risks addressed upfront, the implementation is straightforward: 9 new files in `Core/Routing/` and `Core/Modules/`, one modified file (`LLMModule.cs`), and two DI registration changes.

---

## Key Findings

### Recommended Stack

V1.6 requires minimal stack additions. One new NuGet package adds HTTP resilience: `Microsoft.Extensions.Http.Resilience 8.7.0`, which wraps Polly v8 with `AddStandardResilienceHandler()` (retry + circuit breaker + 10s timeout in one call). It replaces the deprecated `Microsoft.Extensions.Http.Polly`. Everything else — correlation tracking, async request-response, in-process messaging, format detection via regex, prompt string assembly — uses .NET 8 BCL with no additional dependencies. The zero-dependency principle holds for the routing layer entirely.

**Core technologies:**
- `.NET 8 BCL` (`TaskCompletionSource<string>` + `ConcurrentDictionary`): cross-Anima request-response correlation — idiomatic .NET in-process async RPC, used by SignalR internals
- `IHttpClientFactory` (built-in via `Microsoft.Extensions.DependencyInjection`): HTTP socket pooling — avoids socket exhaustion in heartbeat-driven execution
- `Microsoft.Extensions.Http.Resilience 8.7.0`: HTTP retry/timeout pipeline — official Polly wrapper, `.NET 8` train, `AddStandardResilienceHandler` one-liner
- `[GeneratedRegex]` (built-in, `.NET 8`): zero-allocation format detection — source-generated, no runtime compilation overhead
- Existing `IEventBus` / `AnimaRuntimeManager`: per-Anima and cross-Anima event delivery — already established, no changes needed to these components

### Expected Features

All seven v1.6 features are P1 (must-have). Six are required for cross-Anima routing to function end-to-end; the HTTP Request module is independent and parallelizable.

**Must have (table stakes):**
- `AnimaInputPortModule` — declares a named service on an Anima; registers with `CrossAnimaRouter` on init; single output port `request`
- `AnimaOutputPortModule` — returns cross-Anima response; calls `CrossAnimaRouter.CompleteRequest`; single input port `response`
- `AnimaRouteModule` — selects target Anima + port; generates correlation ID; awaits response via TCS; configurable timeout; input `request` / output `response`
- `CrossAnimaRouter` singleton — global correlation map; compound key addressing; timeout cleanup; `CancelPendingForAnima` on Anima deletion; `GetAllRegisteredPorts` for injection
- Prompt auto-injection in `LLMModule` — reads registered ports, appends token-budgeted service list to system prompt; skips if no routes registered
- Format detection in `LLMModule` — post-stream scan for route markers using rolling buffer; splits passthrough text from payload; dispatches routing calls fire-and-forget
- `HttpRequestModule` — URL/method/headers/body-template config; `IHttpClientFactory`; response + status code output ports; 10s default timeout; SSRF allowlist

**Should have (competitive, v1.7+):**
- Route error/timeout output port — lets wiring handle routing failures gracefully
- Streaming display with parallel detection — buffer copy for detection while streaming to chat
- HTTP auth fields (Bearer/Basic) — first-class config, not manual header entry
- `AnimaRoute` dynamic target — target Anima supplied via input port at runtime

**Defer (v2+):**
- Multi-hop routing chains — nested correlation IDs, timeout propagation
- Fan-out routing — scatter-gather correlation pattern
- HTTP response streaming to downstream modules

### Architecture Approach

The architecture adds one new directory (`Core/Routing/`) containing the singleton broker and stateless utilities, and four new module files in `Core/Modules/`. The `CrossAnimaRouter` is a peer to `AnimaRuntimeManager` — both are application-layer singletons. `CrossAnimaRouter` holds the `_ports` registry (compound key `"{animaId}::{portName}"`) and the `_pending` correlation map. `LLMModule` gains two lightweight additions: a `BuildSystemPrompt` helper that queries `CrossAnimaRouter.GetAllRegisteredPorts()`, and a `FormatDetector.Parse()` call post-response. `WiringEngine` and per-Anima `EventBus` instances are completely unchanged. Per-Anima isolation invariants are preserved by design.

**Major components:**
1. `CrossAnimaRouter` (`Core/Routing/CrossAnimaRouter.cs`) — singleton broker; port registry with compound key; correlation ID map with expiry; timeout enforcement; deletion cleanup via `CancelPendingForAnima`
2. `FormatDetector` (`Core/Routing/FormatDetector.cs`) — stateless parser; extracts routing calls from buffered LLM output; returns `FormatDetectorResult(PassthroughText, List<RoutingCall>)`
3. `AnimaInputPortModule` / `AnimaOutputPortModule` / `AnimaRouteModule` — standard `IModuleExecutor` modules; register/complete/send cross-Anima requests via `CrossAnimaRouter`; invisible to `WiringEngine`
4. `HttpRequestModule` (`Core/Modules/HttpRequestModule.cs`) — standard `IModuleExecutor`; typed `IHttpClientFactory` client with resilience handler; 10s timeout; error-to-output-port pattern
5. Modified `LLMModule` (`Core/Modules/LLMModule.cs`) — prompt injection before API call; `FormatDetector.Parse` after response; `CrossAnimaRouter` injected as nullable (optional, backward-compatible)

**Build order:** DTOs + `FormatDetector` + `CrossAnimaRouter` (Stage 1) → routing modules + `HttpRequestModule` (Stage 2) → `LLMModule` modifications (Stage 3) → DI registration + `AnimaRuntimeManager.DeleteAsync` hook (Stage 4).

### Critical Pitfalls

1. **Using the global `IEventBus` singleton for cross-Anima routing** — silently breaks per-Anima isolation (ANIMA-08 tech debt); all cross-Anima delivery MUST go through `AnimaRuntimeManager.GetRuntime(targetId)` → `CrossAnimaRouter`. Add an isolation integration test verifying Anima A events do NOT arrive at Anima B.

2. **`AnimaRouteModule` fire-and-forget instead of awaiting response** — WiringEngine downstream modules execute in the same tick with empty data; route response arrives after tick completes and is never delivered. Fix: `AnimaRouteModule.ExecuteAsync` MUST `await CrossAnimaRouter.RouteRequestAsync(...)`.

3. **LLM format non-compliance at runtime** — prompt-engineering-based format markers achieve only 80–95% compliance; temperature > 0, model version changes, and long context all reduce this. Use XML-style tags or `@@ROUTE:port|payload@@` (mapped to training-data patterns); implement lenient regex (case-insensitive, optional whitespace); document as best-effort.

4. **HTTP module SSRF via LLM-injected URLs** — the HTTP module's URL input is a direct execution path for LLM output. Default to an empty allowlist; block non-HTTPS schemes at parse time; reject private/loopback IP ranges after DNS resolution. Security cannot be added after the fact.

5. **`AnimaRuntime` deletion leaving in-flight TCS entries hanging** — `AnimaRuntimeManager.DeleteAsync` must broadcast an `AnimaDeleted` signal before disposing, so `CrossAnimaRouter` can call `CancelPendingForAnima(animaId)` and fail pending requests with a clean error, not a silent timeout.

6. **Short correlation IDs colliding under concurrent load** — use full `Guid.NewGuid().ToString("N")` (32-char), not truncated 8-char hex. Include expiry timestamps in pending entries; run periodic cleanup to prevent unbounded dictionary growth.

---

## Implications for Roadmap

Based on the dependency chain in ARCHITECTURE.md and the pitfall-to-phase mapping in PITFALLS.md, four phases are recommended.

### Phase 1: Cross-Anima Routing Infrastructure

**Rationale:** All routing modules depend on `CrossAnimaRouter` and the correlation ID addressing scheme. Building the broker and DTOs first means modules can be tested against a real implementation, not mocks. Critical isolation and addressing pitfalls (Pitfalls 1, 2, 3, 10, 11) must be locked down here — they cannot be retrofitted without breaking changes to the addressing data model.

**Delivers:** `CrossAnimaRouter` singleton with full lifecycle management; `InputPortDescriptor` DTO; `InputPortRegistration` record; compound key addressing (`animaId::portName`); correlation ID map with expiry timestamps; `CancelPendingForAnima` for deletion events; hook into `AnimaRuntimeManager.DeleteAsync`; isolation integration test.

**Addresses features from FEATURES.md:** Cross-Anima message router (global singleton), correlation ID tracking, timeout and orphan cleanup, thread-safe async delivery.

**Avoids pitfalls:** Global EventBus bypass (Pitfall 1), sync deadlock in tick (Pitfall 2), correlation ID collisions and orphans (Pitfall 3), service name collisions — compound key prevents (Pitfall 10), deletion with pending requests (Pitfall 11).

### Phase 2: Routing Modules

**Rationale:** Depends on `CrossAnimaRouter` from Phase 1. `AnimaInputPortModule`, `AnimaOutputPortModule`, and `AnimaRouteModule` are standard `IModuleExecutor` implementations — once the broker is stable, these are low-risk. The correlation ID passthrough design (text prefix vs. dedicated Trigger wire) must be decided and locked in before implementation; changing it retroactively requires re-wiring all existing graphs.

**Delivers:** Three new `IModuleExecutor` modules registered in the plugin system; config sidebar fields (service name, description, target Anima dropdown, target port dropdown, timeout); end-to-end cross-Anima request-response demonstrable in the wiring editor without LLM involvement.

**Addresses features from FEATURES.md:** AnimaInputPort, AnimaOutputPort, AnimaRoute modules (all P1).

**Avoids pitfalls:** AnimaRouteModule fire-and-forget anti-pattern (must await; Pitfall 2 / ARCHITECTURE.md Anti-Pattern 2); `RoutingEnvelope<T>` wrapper that breaks WiringEngine port type dispatch (Anti-Pattern 3); HttpClient-per-execution socket exhaustion (Anti-Pattern 4, though this applies to Phase 4).

### Phase 3: Prompt Auto-Injection and Format Detection

**Rationale:** Depends on `CrossAnimaRouter.GetAllRegisteredPorts()` (Phase 1) and live routing modules (Phase 2) so end-to-end testing is possible. Prompt injection and format detection must ship together — injection without detection is useless (LLM produces the marker but nothing consumes it); detection without injection means the LLM was never told the marker format. Both modify `LLMModule`, so they belong in the same phase. Token budget enforcement for injection must be built here, not retrofitted after context exhaustion is observed.

**Delivers:** Modified `LLMModule` with `BuildSystemPrompt` helper (token-budgeted, 200–300 token cap, one-line service descriptions); `FormatDetector` stateless parser with rolling buffer; `FormatDetectorResult` and `RoutingCall` records; post-stream format dispatch; passthrough text split; compliance tests at temperature 0, 0.5, 1.0.

**Addresses features from FEATURES.md:** Prompt auto-injection, format detection (both P1).

**Avoids pitfalls:** Prompt bloat exhausting context window (Pitfall 4 — 200–300 token budget mandatory from start); streaming format detection on partial chunks (Pitfall 5 — rolling buffer required, per-chunk regex explicitly rejected); LLM format non-compliance (Pitfall 6 — lenient regex, multi-temperature testing).

### Phase 4: HTTP Request Module

**Rationale:** Independent of all routing phases — no dependency on `CrossAnimaRouter`. Can be built in parallel with Phase 2 or Phase 3 if capacity allows, or sequentially after Phase 3. Security requirements (SSRF, credential masking) are critical and must be addressed before the module makes any live network calls; this cannot be deferred.

**Delivers:** `HttpRequestModule` with URL/method/headers/body-template config; `IHttpClientFactory` typed client with `AddStandardResilienceHandler`; SSRF allowlist (empty default, HTTPS-only, private IP block post-DNS-resolution); credential config fields using `type: password` sidebar rendering; 10s timeout default with heartbeat `CancellationToken` pass-through; response body and status code output ports; error-to-output-port handling.

**Addresses features from FEATURES.md:** HTTP Request module (P1); URL template, method dropdown, headers editor, body template, response/statusCode output ports.

**Uses from STACK.md:** `Microsoft.Extensions.Http.Resilience 8.7.0`, `IHttpClientFactory` (built-in).

**Avoids pitfalls:** SSRF via LLM-injected URLs (Pitfall 7 — allowlist-first, empty default); credentials leaking through output ports (Pitfall 8 — password field type + header stripping); HTTP timeout blocking heartbeat (Pitfall 9 — 10s default, pass heartbeat `CancellationToken`).

### Phase Ordering Rationale

- **Phase 1 before all routing work:** `CrossAnimaRouter` is the dependency root for the entire routing feature set. Building it first with full lifecycle management avoids retrofitting deletion cleanup and expiry — both are architecturally load-bearing (Pitfalls 3, 11) and require breaking changes if added later.
- **Phase 2 before Phase 3:** Routing modules must exist and be wired before prompt injection and format detection can be tested end-to-end. Without `AnimaInputPortModule` initialized, `GetAllRegisteredPorts()` returns empty and injection is untestable in integration.
- **Phase 4 is independent:** `HttpRequestModule` has zero dependency on `CrossAnimaRouter`. It can be scheduled in parallel with Phase 2 (conservative: wait until Phase 3; aggressive: build alongside Phase 2). Sequential placement after Phase 3 is the safe default.
- **No LLMModule changes before Phase 3:** Keeping `LLMModule` unmodified during Phases 1 and 2 reduces regression risk for the most-used module in the system.

### Research Flags

Phases requiring deeper research or empirical validation during planning:

- **Phase 3 (Format Detection):** Format compliance is MEDIUM confidence. The exact marker format (`@@ROUTE:port|payload@@` vs `<route service="...">...</route>`) has inconsistency across research files and needs alignment. Compliance across model versions, temperatures, and providers must be validated empirically before the format is locked into prompts shipped to users.
- **Phase 4 (HTTP Security — DNS rebinding):** SSRF prevention via post-resolution IP checking (blocking DNS rebinding) requires careful implementation. The allowlist UX — how users add permitted domains — needs design work that goes beyond the technical implementation.

Phases with standard, well-documented patterns (skip additional research phase):

- **Phase 1 (CrossAnimaRouter):** `TaskCompletionSource<string>` + `ConcurrentDictionary` is a canonical .NET pattern, documented by Microsoft and used in SignalR. Implementation confidence is HIGH.
- **Phase 2 (Routing Modules):** Standard `IModuleExecutor` pattern — follows identical structure to all existing v1.5 modules. No novel architectural patterns.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Only one new NuGet package; all other capabilities are BCL. Package version (8.7.0) confirmed on NuGet. Official Microsoft docs for all patterns. |
| Features | HIGH | Feature set derived from direct comparison with n8n, Node-RED, and AutoGen/LangGraph. Core routing features are well-precedented. Format detection compliance specifics are MEDIUM. |
| Architecture | HIGH | Based on direct source code analysis of the v1.5 codebase. All integration points verified against existing file structure and component interfaces. |
| Pitfalls | HIGH | 11 pitfalls identified across 6 domains. Security pitfalls (SSRF, credentials) sourced from OWASP and Microsoft documentation. Isolation pitfalls sourced from direct codebase inspection. |

**Overall confidence:** HIGH

### Gaps to Address

- **Format marker inconsistency:** ARCHITECTURE.md uses `@@ROUTE:portName|payload@@`; FEATURES.md uses `<route service="ServiceName">payload</route>`. These must be reconciled to a single format before Phase 3 begins. Recommendation: XML-style `<route service="ServiceName">payload</route>` — closest to Claude's native tool-call format and most reliably produced by modern LLMs trained on markup.
- **Correlation ID passthrough design:** ARCHITECTURE.md identifies two options — text prefix (`[CORR:{id}]\n{text}`) vs. dedicated Trigger wire. The text prefix approach risks corruption if intermediate modules transform the text. Recommendation: dedicated Trigger wire for cleanliness, at the cost of requiring an explicit wire in the graph. Decision must be made before Phase 2 implementation.
- **Prompt injection token budget calibration:** The 200–300 token cap is a reasonable starting point but should be validated against real usage with 3–10 configured routes before being hardcoded. Plan a calibration step in Phase 3 planning.
- **HTTP allowlist UX:** The allowlist design (empty default, how users add permitted domains in the config sidebar) needs UI design work in Phase 4 planning. The technical implementation is clear; the user-facing configuration flow is not yet specified.

---

## Sources

### Primary (HIGH confidence)
- OpenAnima source code — `AnimaRuntime`, `HeartbeatLoop`, `EventBus`, `LLMModule`, `WiringEngine`, `AnimaRuntimeManager` — direct codebase inspection
- OpenAnima `.planning/PROJECT.md` — ANIMA-08 tech debt, v1.6 target features, architectural decisions
- [Microsoft Learn — IHttpClientFactory guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory) — socket pooling, lifetime management
- [Microsoft Learn — Build resilient HTTP apps](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience) — `AddStandardResilienceHandler` API
- [NuGet — Microsoft.Extensions.Http.Resilience 8.7.0](https://www.nuget.org/packages/Microsoft.Extensions.Http.Resilience) — version confirmed
- [Microsoft Learn — .NET Regular Expressions](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expressions) — `[GeneratedRegex]` attribute
- [OWASP SSRF Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Server_Side_Request_Forgery_Prevention_Cheat_Sheet.html) — allowlist strategy, DNS rebinding prevention
- [Microsoft Correlation IDs Engineering Playbook](https://microsoft.github.io/code-with-engineering-playbook/observability/correlation-id/) — correlation ID design and orphaned request handling

### Secondary (MEDIUM confidence)
- [Correlation Identifier — Enterprise Integration Patterns](https://www.enterpriseintegrationpatterns.com/patterns/messaging/CorrelationIdentifier.html) — correlation ID tracking pattern
- [Node-RED HTTP Request Node — FlowFuse](https://flowfuse.com/node-red/core-nodes/http-request/) — reference UX for HTTP module config fields (canonical reference)
- [Building your first multi-agent system with n8n](https://medium.com/mitb-for-all/building-your-first-multi-agent-system-with-n8n-0c959d7139a1) — sub-workflow-as-tool pattern (closest analog to AnimaInputPort)
- [LLM Structured Output in 2026](https://dev.to/pockit_tools/llm-structured-output-in-2026-stop-parsing-json-with-regex-and-do-it-right-34pk) — format reliability levels: prompt engineering 80–95%, tool_call 99%+
- [Structured Output Streaming for LLMs](https://medium.com/@prestonblckbrn/structured-output-streaming-for-llms-a836fc0d35a2) — incremental parsing, rolling buffer approach
- [TaskCompletionSource in .NET](https://code-corner.dev/2024/01/19/NET-%E2%80%94-TaskCompletionSource-and-CancellationTokenSource/) — TCS + CancellationTokenSource async RPC pattern
- [Gigi Labs — RabbitMQ RPC with TaskCompletionSource](https://gigi.nullneuron.net/gigilabs/abstracting-rabbitmq-rpc-with-taskcompletionsource/) — correlation dictionary pattern walkthrough
- [C# HttpClient Security Pitfalls](https://xygeni.io/blog/c-httpclient-common-security-pitfalls-and-safe-practices/) — SSRF, SSL validation, timeout handling
- [How to Handle Timeout Exceptions in HttpClient](https://oneuptime.com/blog/post/2025-12-23-handle-httpclient-timeout-exceptions/view) — HttpClient.Timeout default (100s), short timeout best practices
- [Request-based Multi-Agent Reference Architecture](https://microsoft.github.io/multi-agent-reference-architecture/docs/agents-communication/Request-Based.html) — request-response patterns in multi-agent systems

### Tertiary (LOW confidence)
- [Achieving Tool Calling Functionality in LLMs Using Only Prompt Engineering Without Fine-Tuning](https://arxiv.org/html/2407.04997v1) — XML-tag format reliability (needs empirical validation for this specific format/model combination)
- [Effective Prompt Engineering: Mastering XML Tags](https://medium.com/@TechforHumans/effective-prompt-engineering-mastering-xml-tags-for-clarity-precision-and-security-in-llms-992cae203fdc) — XML-tag format guidance for LLMs
- [Every Way To Get Structured Output From LLMs — BAML Blog](https://boundaryml.com/blog/structured-output-from-llms) — comparison of prompt-based vs constrained decoding reliability

---
*Research completed: 2026-03-11*
*Ready for roadmap: yes*
