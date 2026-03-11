# Pitfalls Research

**Domain:** Cross-Anima Routing, HTTP Request Module, LLM Format Detection
**Researched:** 2026-03-11
**Confidence:** HIGH

## Context

This document covers pitfalls specific to adding cross-Anima request-response routing and an
HTTP Request module to OpenAnima v1.6. The system has enforced strict per-Anima isolation since
v1.5 (separate EventBus, HeartbeatLoop, WiringEngine per Anima). These features deliberately
pierce that isolation boundary in a controlled way, which is the central tension driving most
pitfalls here.

The six primary pitfall domains for v1.6:

1. Isolation boundary — breaking guarantees while enabling controlled communication
2. Async request-response — correlation ID tracking in a heartbeat-driven system
3. Deadlock — synchronous-looking request-response between agents with shared runtime
4. LLM format injection — prompt bloat and format reliability for service awareness
5. Format detection in streaming LLM output — regex fragility and partial token spans
6. HTTP Request module — SSRF, credential exposure, timeout propagation

---

## Critical Pitfalls

### Pitfall 1: Cross-Anima Routing Leaking Through the Shared EventBus Singleton

**What goes wrong:**
AnimaRoute/AnimaInputPort modules are registered per-Anima, but the global IEventBus singleton
(kept for DI compatibility per tech debt ANIMA-08) is still accessible to all module constructors.
A developer implementing cross-Anima routing uses the singleton EventBus to route messages between
Animas because it is visible from all contexts. This appears to work but silently breaks per-Anima
isolation: any Anima can now receive events from any other Anima by knowing an event name, and the
cross-Anima channel bypasses all correlation ID tracking.

**Why it happens:**
The v1.5 tech debt item ANIMA-08 explicitly states that a global IEventBus singleton is kept for
DI compatibility. When building AnimaRoute, the temptation is to use this visible singleton as the
cross-Anima transport since it already spans all Animas. The per-Anima EventBus instances inside
AnimaRuntime are the correct transport for within-Anima messages; only the cross-Anima routing
layer (AnimaRuntimeManager or a new CrossAnimaRouter service) should bridge between them.

**How to avoid:**
- Cross-Anima routing MUST go through AnimaRuntimeManager, not through any EventBus instance
- AnimaRuntimeManager.GetRuntime(targetAnimaId) is the only sanctioned cross-boundary call
- AnimaInputPort registers its handler on its own Anima's per-Anima EventBus only
- AnimaRoute delivers to the target Anima by calling the target AnimaRuntime's EventBus directly
  via AnimaRuntimeManager — never via the global singleton
- Add an integration test: verify that an event published to Anima A's EventBus does NOT arrive
  at Anima B's subscribers

**Warning signs:**
- AnimaRoute module constructor receives IEventBus via DI (receives the singleton, not per-Anima)
- Cross-Anima test passes even when AnimaRuntimeManager lookup is bypassed
- Events arriving at AnimaInputPort without a correlation ID in headers
- Multiple Animas receiving the same event when only one was targeted

**Phase to address:**
Cross-Anima Routing Infrastructure phase — routing boundary design must be explicit before any
AnimaRoute/AnimaInputPort implementation begins.

---

### Pitfall 2: Deadlock from Synchronous-Style Request-Response Inside the Heartbeat Tick

**What goes wrong:**
AnimaRoute sends a request to another Anima and awaits a response within the same heartbeat tick
execution. The target Anima's AnimaInputPort processes the request and publishes the response via
AnimaOutputPort — but the response is routed back through the caller's per-Anima EventBus. If the
caller's HeartbeatLoop is blocked on `await` inside the tick, the tick's SemaphoreSlim
(_tickLock) is still held. The response handler on the calling Anima's EventBus tries to execute,
but it is also invoked from within the same tick context. This does not deadlock on the
SemaphoreSlim (events execute inline via Task.WhenAll inside the tick), but it CAN deadlock if
the response is routed through an intermediate layer that also awaits the calling Anima's
heartbeat to advance.

**Why it happens:**
The HeartbeatLoop's `_tickLock` uses `SemaphoreSlim(1, 1)` with a non-blocking check
(`Wait(0)`) to skip ticks, not to block on re-entry. However, the tick executes module tasks via
`Task.WhenAll`, which means all module tasks within a tick run concurrently on the same thread
pool. If AnimaRoute's `ExecuteAsync` calls into the target Anima synchronously (via
`AnimaRuntimeManager.GetRuntime(targetId).EventBus.PublishAsync(...)`) AND the target Anima is
not running its own heartbeat (e.g., because it is also mid-tick), then `PublishAsync` on the
target returns immediately (it fans out handlers as tasks). The response arrives back as an event
on the source Anima — but the source Anima is still inside `Task.WhenAll` waiting for AnimaRoute
to finish. The response handler fires as a new task inside the ongoing `Task.WhenAll` fan-out.
This creates a logical deadlock: AnimaRoute awaits `TaskCompletionSource<string>` for the
response, but the response handler that would complete that TCS is blocked waiting for the
original `Task.WhenAll` to settle, which cannot settle until AnimaRoute completes.

**How to avoid:**
- NEVER await a cross-Anima response within the same synchronous-style execution path
- Use fire-and-forget for the outbound request; the inbound response must arrive in a FUTURE tick
  or via a separate callback that is NOT nested in the originating await chain
- Use `TaskCompletionSource<T>` with a timeout and register the completion callback as a separate
  EventBus subscription that fires independently of the requesting module's ExecuteAsync
- The requesting module stores the pending TCS in a dictionary keyed by correlationId
- In a SUBSEQUENT tick or callback, the response arrives, the dictionary lookup completes the TCS
- Always set a timeout on the TCS: `CancellationTokenSource` with 5–30 second default
- Document clearly: AnimaRoute is asynchronous — callers receive a "request submitted" signal,
  not a synchronous response

**Warning signs:**
- AnimaRoute.ExecuteAsync hangs indefinitely during testing
- HeartbeatLoop tick latency spikes to thousands of milliseconds
- Multiple ticks skipped consecutively after a cross-Anima call
- Response event arrives but pending request dictionary lookup finds no entry (TCS already
  abandoned due to timeout)

**Phase to address:**
Cross-Anima Routing Infrastructure — correlation ID and async callback design must be decided
before module implementation.

---

### Pitfall 3: Correlation ID Collisions and Orphaned Pending Requests

**What goes wrong:**
AnimaRoute generates a correlation ID (e.g., `Guid.NewGuid().ToString("N")[..8]`) and registers a
`TaskCompletionSource` in a dictionary keyed by that ID. The response from the target Anima comes
back on a different EventBus with the same correlation ID in the payload. If the correlation ID
is not globally unique enough, two concurrent requests from different Animas to the same target
produce the same ID, causing response misrouting. More dangerously, if the target Anima is
deleted or crashes before responding, the pending TCS entry is never removed. Over time, the
pending requests dictionary grows unboundedly, holding references to module state and preventing
garbage collection.

**Why it happens:**
8-character hex IDs (the pattern used for Anima IDs) produce 16^8 = ~4 billion possibilities,
which is sufficient for Anima names but low for concurrent in-flight requests under high load.
More critically, there is no eviction mechanism for completed or abandoned requests because
heartbeat-driven systems don't have a natural request lifecycle with guaranteed cleanup.

**How to avoid:**
- Use `Guid.NewGuid().ToString("N")` (full 32-char hex) for correlation IDs — not truncated
- Maintain a concurrent dictionary: `ConcurrentDictionary<string, (TaskCompletionSource<string>,
  DateTime expiry)>` for pending requests
- Run a periodic cleanup scan (every N ticks or on a timer) to remove entries past their expiry
- On AnimaRuntime disposal, cancel and remove ALL pending requests belonging to that Anima
- When target Anima is deleted, publish a "routing-failed" event to the source Anima's EventBus
  for all pending correlation IDs associated with that target
- Log all orphaned correlation IDs at WARN level with their age for debugging

**Warning signs:**
- Pending request count grows monotonically and never decreases
- Memory usage climbs over hours of operation
- AnimaRuntime disposal hangs because pending TCS callbacks reference disposed objects
- Duplicate correlation IDs appearing in logs

**Phase to address:**
Cross-Anima Routing Infrastructure — cleanup and expiry design must be built alongside the
correlation ID mechanism, not added later.

---

### Pitfall 4: Prompt Auto-Injection Bloating the LLM Context Window

**What goes wrong:**
When an Anima has AnimaRoute modules configured, the system injects descriptions of available
downstream services into the LLM's system prompt. As the number of configured routes grows, the
injected service descriptions consume an increasing proportion of the context window. With 10
routes, each with a description, available arguments, and example usage, the injection can consume
500–2000 tokens. This degrades LLM performance (less context for conversation history), increases
cost, and can push long conversations past the context limit.

**Why it happens:**
Naive prompt injection concatenates all service descriptions unconditionally into the system
prompt on every request. This is common because it is simple and deterministic. The problem
compounds when service descriptions include examples, argument schemas, and natural-language
explanations. The LLMModule currently sends a single user message with no system prompt; adding
injected service descriptions requires extending the message list, and there is no budget
enforcement to prevent unconstrained growth.

**How to avoid:**
- Enforce a hard token budget for the service-awareness injection: max 200–300 tokens total
  regardless of how many routes are configured
- Use concise, structured descriptions: one line per service, not paragraphs
  Example format: `[ServiceName]: <one-sentence description>. Call with: {input field}`
- If token budget is exceeded, truncate with "... and N more services" rather than silently
  overflowing
- Inject service descriptions as a separate system message, not appended to user content
- Consider lazy injection: only inject services when conversation context suggests a routing
  decision is needed (e.g., detect question patterns that match service capabilities)
- Track injected token cost in ChatContextManager so it counts against the 90% send-block
  threshold alongside conversation history

**Warning signs:**
- Context capacity percentage jumps significantly when routing modules are configured
- Chat send blocking triggered earlier than expected in conversations with many routes
- LLM responses degrade in quality or become less responsive to conversation content
- Per-message token cost significantly higher than baseline for same conversation length

**Phase to address:**
Prompt Auto-Injection phase — token budget enforcement must be built in from the start, not
retrofitted after observing context exhaustion.

---

### Pitfall 5: LLM Format Detection Breaking on Partial Tokens During Streaming

**What goes wrong:**
The format detector monitors the LLM output stream for a routing trigger format (e.g.,
`[ROUTE:ServiceName|payload]`). The LLM streams tokens incrementally. The format marker spans
multiple token boundaries — e.g., `[ROUTE` arrives in one chunk, `:ServiceName` in another,
`|payload]` in a third. A simple regex applied to each chunk independently misses the match
because no single chunk contains the complete pattern. Worse, the closing `]` token may contain
conversational text that follows the route command, causing the detector to silently discard the
routing instruction or misparse the payload.

**Why it happens:**
The current LLMModule calls `CompleteAsync` (non-streaming, buffered response) so this is not
currently an issue. But the current architecture comment in ChatOutputModule and LLMModule shows
that streaming is a stated capability. If format detection is layered on top of the streaming path,
and the implementer applies the regex to each streaming chunk rather than to a rolling buffer, the
split-token problem is guaranteed to manifest. This is a known pitfall in LLM structured output
parsing: tokens do not align with string boundaries meaningful to parsers.

**How to avoid:**
- Maintain a rolling character buffer across all streaming chunks; apply the detection regex to
  the full buffer, not to individual chunks
- Only emit buffered content downstream AFTER confirming it does not start an incomplete pattern
  (i.e., use a "lookahead flush" approach: flush content only when the buffer prefix cannot be
  the start of a trigger pattern)
- Use a deterministic bracket-counting state machine rather than a pure regex for detection:
  track whether a `[ROUTE:` prefix has been seen and buffer until the closing `]` arrives
- Split the output into two streams: normal text output (to ChatOutput port) and routing
  instructions (to AnimaRoute), emitting from the buffer as each section is fully confirmed
- Test with models that produce verbose streaming (many small chunks) and terse models (few large
  chunks) to verify buffer behavior at both extremes

**Warning signs:**
- Route instructions intermittently fail to trigger when LLM responds with them
- Routing works reliably in unit tests (which use mock full-response returns) but fails in
  integration with real LLM streaming
- Format detector fires on partial matches, triggering routing with incomplete payloads
- Conversational text is accidentally consumed as part of a route payload

**Phase to address:**
Format Detection phase — the rolling buffer approach must be the only implementation path;
chunk-by-chunk regex must be explicitly rejected in the design document.

---

### Pitfall 6: Regex Fragility from LLM Format Non-Compliance

**What goes wrong:**
The routing trigger format chosen (e.g., `[ROUTE:ServiceName|payload]`) assumes the LLM reliably
produces this exact syntax. In practice, LLMs trained on general text have never seen this custom
format in training data. Under variations in temperature, context, model version, or instruction
phrasing, the LLM produces near-misses: `[Route:ServiceName|payload]` (wrong casing), `[ROUTE:
ServiceName|payload]` (space after colon), `ROUTE: ServiceName - payload` (no brackets, dash
separator), or `I'll route to ServiceName with payload` (no format at all). The regex matches
none of these, silently dropping the routing instruction.

**Why it happens:**
LLMs do not follow format instructions with 100% reliability — research shows prompt-based format
compliance at 80–95%, not 100%. This is especially true for custom non-standard formats with no
training-data precedent. The closer the format is to patterns the model has seen (like JSON, or
markdown), the more reliable compliance becomes. Novel bracket syntax with pipe separators is
uncommon enough that model compliance degrades under pressure (long context, complex task, high
temperature).

**How to avoid:**
- Choose a format that maps to a training-data pattern the model knows well: structured JSON
  inline in the output (`{"route": "ServiceName", "payload": "..."}`) is far more reliable than
  novel bracket syntax
- Alternatively, use function calling / tool use APIs (e.g., OpenAI's tool_call feature) which
  enforce structured output at the token generation level — this is the highest-reliability path
- If using a custom format, write the detection regex with leniency:
  case-insensitive, optional whitespace, multiple delimiter alternatives
- Implement fuzzy match fallback: if no exact match but output starts with recognizable prefix,
  log and attempt recovery
- Empirically test format compliance across temperature=0, 0.5, 1.0 and record failure rates
  before committing to a format
- Document that routing is "best effort" when using prompt-injection approach; tool_call approach
  is required for production-reliability routing

**Warning signs:**
- Format detection works in deterministic tests (temperature=0) but fails intermittently in
  production (temperature=0.7+)
- Different LLM providers (OpenAI vs local models) produce significantly different compliance rates
- LLM output contains routing intent in natural language but not in the expected format
- Format compliance drops after model version upgrades with no code changes

**Phase to address:**
Format Detection phase — format choice is a critical decision that must be made before implementing
the detection layer; revisit in integration testing with real model calls.

---

### Pitfall 7: HTTP Request Module SSRF via User-Controlled URLs

**What goes wrong:**
The HTTP Request module accepts a URL from its input port (wired from LLM output or user
configuration). A malicious input — or a compromised LLM response — supplies a URL that targets
internal services: `http://localhost:5050/admin`, `http://169.254.169.254/latest/meta-data/` (AWS
metadata endpoint), or `file:///C:/Windows/System32/drivers/etc/hosts`. The module faithfully
executes the request, leaking internal service responses or local file contents to the LLM output
stream or through the OutputPort.

**Why it happens:**
SSRF is OWASP Top 10 and occurs when a server makes HTTP requests to attacker-controlled
destinations. In OpenAnima, the attack surface is LLM output → HTTP module input. If the LLM is
manipulated via prompt injection to generate a malicious URL, the HTTP Request module becomes the
attack's execution vector. The application runs as a local process on Windows, so internal
Windows services (IIS, SQL Server, local APIs) and file URLs are all reachable.

**How to avoid:**
- Validate all URLs before execution against a configurable allowlist (the MOST effective defense)
- Default to allowlist-empty: no URLs permitted unless the user explicitly adds them in module
  configuration
- Block all non-HTTPS schemes: reject `http://`, `file://`, `ftp://`, `dict://` at parse time
- Reject private/loopback IP ranges after DNS resolution: 127.x.x.x, 10.x.x.x, 172.16-31.x.x,
  192.168.x.x, ::1, fc00::/7
- Re-resolve hostname after initial resolution to prevent DNS rebinding attacks
- Never log full request/response bodies by default — only log URL, status code, and duration
- Add a `requireHttps` module config field defaulting to `true`

**Warning signs:**
- HTTP module accepts URLs containing `localhost`, `127.0.0.1`, or private IP ranges
- Module can request `file://` or `ftp://` scheme URLs
- URL validation only checks for obvious bad patterns without DNS resolution check
- LLM-generated URLs are accepted without allowlist verification

**Phase to address:**
HTTP Request Module phase — URL validation and allowlist enforcement must be implemented before
the module can make any real network calls; security cannot be bolted on after.

---

### Pitfall 8: API Keys and Credentials Leaking Through HTTP Module Outputs

**What goes wrong:**
The HTTP Request module is configured with bearer tokens, API keys, or basic auth credentials.
The response (which may include these credentials echoed back, or the request details in error
responses) flows through the wiring OutputPort as plain text. It then enters the LLM's context
window or the chat display. LLM error descriptions, verbose API responses, or debugging logs can
expose credentials in logs or in the UI visible to all users.

**Why it happens:**
The module configuration stores credentials (e.g., Authorization header value) in the same
`Dictionary<string, string>` config store used by LLMModule. The config store does not
differentiate between safe-to-display and sensitive fields. The LLMModule already has this issue
but masks the API key in log output (4 chars + `***`). The HTTP module's output flows as raw text
into downstream modules, including potentially ChatOutputModule which renders it in the browser.

**How to avoid:**
- Mark credential config fields as `type: "password"` in the config schema — the existing
  EditorConfigSidebar already renders password fields as `<input type="password">` and suppresses
  display; extend this to HTTP module config
- Headers containing Authorization, API-Key, or X-*-Token patterns must never appear in
  OutputPort payloads — strip them before forwarding response content
- Never log full request headers; the existing mask pattern (`key[..4] + "***"`) must apply to
  all credential-type fields in HTTP module
- Response bodies that echo back request headers (common in debugging endpoints) should be
  configurable to strip before forwarding downstream
- Add a "sanitize response" config option that removes any line containing known header patterns

**Warning signs:**
- HTTP module response OutputPort contains `Authorization:` or `Bearer ` prefixes
- Module configuration editor shows API keys in plain text in the browser
- Logs contain full Authorization header values
- LLM context window shows API key values in conversation history

**Phase to address:**
HTTP Request Module phase — credential handling must use the existing password field type from
day one; never store or display credentials as plain text.

---

### Pitfall 9: HTTP Request Module Timeout Blocking the Heartbeat Tick

**What goes wrong:**
The HTTP Request module uses `HttpClient` with no configured timeout (or a long default). When
the target endpoint is slow or unresponsive, the module's `ExecuteAsync` method awaits the HTTP
call. This blocks the module's task slot in the HeartbeatLoop's `Task.WhenAll`. Since the
heartbeat tick holds `_tickLock` while all module tasks run, a blocked HTTP call delays all
subsequent ticks. With the default `HttpClient` timeout of 100 seconds, a single slow HTTP call
stalls the entire Anima's heartbeat for nearly two minutes.

**Why it happens:**
`HttpClient.Timeout` defaults to 100 seconds if not explicitly set. This is appropriate for
user-initiated requests where the user is waiting, but disastrous for a module running inside a
heartbeat loop that must complete within 100ms to maintain tempo. The HeartbeatLoop already has a
tick-skip mechanism (`_tickLock.Wait(0)`), but this only skips if the PREVIOUS tick is still
running — it does not interrupt an in-progress slow module.

**How to avoid:**
- Set `HttpClient.Timeout` to 10 seconds maximum for HTTP Request modules; expose this as a
  configurable field with a 10s default and a 30s maximum
- Use `CancellationToken` from the heartbeat tick's `ct` parameter to cancel HTTP calls when the
  heartbeat is stopping: pass `ct` to `HttpClient.SendAsync(request, ct)`
- Add a per-request timeout using `CancellationTokenSource.CreateLinkedTokenSource(ct,
  CancellationTokenSource.CreateLinkedTokenSource(TimeSpan).Token)` to enforce a hard deadline
- Log a warning if HTTP call latency exceeds 5 seconds: this indicates the endpoint is slow and
  may cause tick accumulation
- Consider making HTTP calls fire-and-forget with response delivered in a subsequent tick rather
  than blocking the initiating tick

**Warning signs:**
- HeartbeatLoop shows high LastTickLatencyMs (>100ms) correlated with HTTP module execution
- SkippedCount grows rapidly when HTTP module is active
- Application becomes unresponsive to user interactions during HTTP module execution
- No `HttpClient.Timeout` set on the HTTP module's client instance

**Phase to address:**
HTTP Request Module phase — timeout configuration must be set in the constructor, not added as
a followup; default should be short (10s), not the HttpClient default (100s).

---

### Pitfall 10: AnimaInputPort Name Collisions Across Animas

**What goes wrong:**
Two different Animas both declare an AnimaInputPort with the name "Summarizer". AnimaRoute in a
third Anima is configured to route to "Summarizer" (by service name) without specifying the
target Anima ID. The routing layer resolves this ambiguously — it may route to either the first
or second "Summarizer" depending on registration order, non-deterministically. When one of the
Animas is deleted or restarted, routing silently shifts to the other without any notification.

**Why it happens:**
If the routing system uses a global service registry (service name → target Anima ID), name
conflicts are possible when multiple Animas declare ports with the same name. The system may
resolve by "last write wins" in a ConcurrentDictionary, silently overwriting earlier
registrations. This is the same class of problem as global event name collisions in a shared
EventBus.

**How to avoid:**
- Route addressing MUST use both Anima ID and service name: `{animaId}/{serviceName}` as a
  compound key — never just service name alone
- The service registry (if one exists) should store entries as `(animaId, serviceName) →
  AnimaRuntime` with the animaId as the primary discriminator
- AnimaRoute configuration in the editor must require selecting the TARGET ANIMA explicitly
  before the service name dropdown is populated from that Anima's registered ports
- Log a warning (not an error) if two Animas register ports with the same service name; allow it
  but require full qualified addressing
- Display the full `{AnimaName}/{ServiceName}` label in the editor, not just the service name

**Warning signs:**
- Routing works but sporadically delivers to the wrong Anima
- Deleting an Anima causes routing from other Animas to silently reroute to a different target
- Service registry entry count is less than the sum of InputPort registrations across all Animas
  (entries are being overwritten)

**Phase to address:**
Cross-Anima Routing Infrastructure — addressing scheme (animaId + serviceName) must be the data
model for the registry from the start; retrofitting compound keys later requires breaking changes.

---

### Pitfall 11: AnimaRuntime Disposal Leaving Pending Cross-Anima Requests Hanging

**What goes wrong:**
Anima A has an in-flight request to Anima B (pending TCS with correlation ID). The user deletes
Anima B. AnimaRuntimeManager calls `runtime.DisposeAsync()` on Anima B's runtime — this stops
the heartbeat and disposes the EventBus. Anima A's pending request TCS is never completed; it
sits in the pending dictionary until the configured timeout (5–30 seconds). During this window,
Anima A's heartbeat ticks accumulate pending entries, and if AnimaRoute is triggered again (next
heartbeat tick), another request is submitted to a now-deleted Anima, creating more orphaned TCS
entries.

**Why it happens:**
`AnimaRuntimeManager.DeleteAsync` disposes the runtime but has no mechanism to notify other
Animas that were communicating with the deleted Anima. The correlation ID tracking in the source
Anima is not aware of the target's lifecycle. This is the distributed systems problem of
"subscriber departure without notification" applied to in-process agents.

**How to avoid:**
- AnimaRuntimeManager.DeleteAsync must broadcast a `AnimaDeleted` event BEFORE disposing the
  runtime, so other Animas have a chance to cancel pending requests
- All Animas with pending requests to the deleted Anima must receive this notification and call
  `TCS.TrySetException(new AnimaUnavailableException(...))` to fail pending requests cleanly
- AnimaRoute should catch `AnimaUnavailableException` and surface a routing failure on its
  output port (a text error payload or a dedicated error output port)
- Consider adding an `OnAnimaDeleted` event to IAnimaRuntimeManager so any module can subscribe
  and clean up cross-Anima state

**Warning signs:**
- Deleting an Anima causes the source Anima's heartbeat to show increased tick latency for up to
  the timeout duration afterward
- Pending request dictionary holds entries for Anima IDs that no longer exist
- AnimaRoute error output never fires when the target Anima is unavailable

**Phase to address:**
Cross-Anima Routing Infrastructure — deletion notification must be designed alongside creation;
cannot be an afterthought.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Using global IEventBus singleton for cross-Anima routing | Avoids implementing CrossAnimaRouter | Silent isolation breakage; impossible to audit cross-Anima traffic | Never |
| Applying format detection regex per-chunk | Simpler implementation | Misses patterns split across token boundaries | Never — use rolling buffer |
| No URL allowlist on HTTP module (validate input only) | Faster to ship | SSRF via LLM-injected URLs; internal service exposure | Never — allowlist-first |
| Blocking HTTP call inside heartbeat tick with long timeout | Simple await pattern | Heartbeat stall; tick accumulation; unresponsive UI | Never — use short timeout + cancellation |
| Using short correlation IDs (8 hex chars) | Matches existing Anima ID pattern | ID collision under concurrent load | Acceptable only if max concurrent requests guaranteed < 100 |
| Injecting full service descriptions without token budget | Complete service context for LLM | Context window exhaustion; increased cost; send blocking | Never — enforce token budget from start |
| Storing HTTP credentials in plain-text config values | Consistent with existing config store | Credentials visible in editor UI and logs | Never — use password field type |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| AnimaRoute → AnimaInputPort | Route using global EventBus singleton | Route via AnimaRuntimeManager.GetRuntime(targetId).EventBus |
| Correlation ID tracking | Never expire pending TCS entries | Periodic cleanup with expiry timestamps; cancel on AnimaRuntime disposal |
| Format detection on streaming output | Apply regex to each chunk independently | Maintain rolling buffer; apply regex to cumulative text |
| HTTP module timeout | Use HttpClient default (100s) | Configure 10s timeout; pass heartbeat CancellationToken |
| Service name addressing | Route to service by name only | Route by animaId + serviceName compound key |
| LLM service injection | Inject all service descriptions unconditionally | Enforce 200–300 token budget; use concise one-line descriptions |
| HTTP response forwarding | Forward raw response body including headers | Strip credential-pattern headers before emitting on OutputPort |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| HTTP calls inside heartbeat tick with long timeout | Tick latency >100ms; skipped ticks accumulate | Short timeout (10s); fire-and-forget with response-on-next-tick | Any HTTP endpoint with >100ms response time |
| Unbounded pending request dictionary | Memory grows over hours; GC pressure | Periodic expiry scan; hard limit on concurrent pending requests per Anima | >100 concurrent cross-Anima requests |
| Full service description injection per LLM call | Context window fills early; send blocking | Token budget cap (200–300 tokens); one-line descriptions | >5 configured routes with verbose descriptions |
| Rolling buffer accumulating without flush | Memory grows during long LLM responses | Flush confirmed-clean portions; set max buffer size | LLM responses >10KB without triggering format patterns |
| Cross-Anima routing on every heartbeat tick | Excessive inter-Anima traffic; EventBus churn | Only route when explicitly triggered; don't poll via heartbeat | More than 2 Animas with always-on routes |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| HTTP module fetches user-supplied or LLM-supplied URLs without allowlist | SSRF to internal services, AWS metadata, local files | Allowlist-only with empty default; block private IP ranges after DNS resolution |
| HTTP module accepts `file://` or `http://` schemes | Local file read; unencrypted credential transmission | Reject all non-HTTPS schemes at URL parse time |
| Forwarding API keys / Authorization headers in HTTP response body downstream | Credential exposure in LLM context window and UI | Strip credential-pattern headers from response before emitting OutputPort |
| LLM format injection revealing system prompt structure | Attack surface discovery; prompt injection amplification | Never inject internal system details; describe services without revealing implementation |
| No HTTPS validation on HTTP module | Man-in-the-middle on LAN | Never disable SSL validation; use system certificate store |
| Logging full HTTP request/response bodies | Credential and PII leakage in log files | Log only URL, status code, duration; never log Authorization headers |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Routing failure appears as silence (no response) | User thinks nothing happened; submits duplicate requests | AnimaRoute must output an error text payload on timeout or unavailable target |
| Service injection changes wiring behavior invisibly | User adds a route module; LLM starts routing to services without realizing why | Show injected service count in module status; allow preview of injected prompt text |
| HTTP module request shows no progress | User adds HTTP module to wiring; clicks execute; waits silently | Add module status: "Requesting..." with elapsed seconds, "Response received" on completion |
| Credentials in module config visible in wiring editor | Any user at the machine can see API keys | Config fields marked `type: password` render masked in the editor |
| Deleting an Anima leaves other Animas in ambiguous state | Routing from Anima A fails after Anima B deleted, no explanation | Show routing error in Anima A: "Target Anima B was deleted" |

## "Looks Done But Isn't" Checklist

- [ ] **Cross-Anima routing:** Looks done when messages reach target. Verify: isolation still
  holds — Anima A's non-routed events do NOT arrive at Anima B. Run isolation integration test.
- [ ] **Correlation ID tracking:** Looks done when requests complete. Verify: pending dictionary
  is empty after all responses received; no entries accumulate over extended operation.
- [ ] **Format detection:** Looks done when unit tests pass with mock responses. Verify: detection
  works with real streaming LLM output where pattern spans multiple chunks.
- [ ] **HTTP module SSRF:** Looks done when localhost URLs fail. Verify: `http://169.254.169.254`
  (cloud metadata) and DNS-resolved private IPs are also blocked.
- [ ] **HTTP timeout:** Looks done when fast endpoints work. Verify: slow endpoint (>10s) causes
  controlled error, NOT heartbeat stall; tick latency stays normal.
- [ ] **Credential masking:** Looks done when editor shows `***`. Verify: logs contain no
  Authorization header values; HTTP response OutputPort contains no credential patterns.
- [ ] **AnimaRuntime deletion with pending requests:** Looks done when deletion works. Verify:
  source Anima receives routing failure notification within timeout period, not after timeout.
- [ ] **Service name collision:** Looks done with single-Anima routing tests. Verify: two Animas
  with identically-named ports route to the correct target when addressed by animaId.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Global EventBus used for cross-Anima routing | HIGH | Refactor routing layer to use AnimaRuntimeManager; audit all AnimaRoute/InputPort code paths for IEventBus singleton use; re-test isolation guarantees |
| Deadlock from sync request-response in tick | HIGH | Convert AnimaRoute.ExecuteAsync to fire-and-forget; implement TCS-based async response callback; add integration test for round-trip timing |
| Orphaned pending requests | MEDIUM | Add expiry field to pending entry; add periodic cleanup task; add AnimaRuntime disposal notification |
| Prompt bloat exhausting context window | MEDIUM | Add token counting to injected service descriptions; implement budget enforcement; trim descriptions to one-line format |
| Chunk-by-chunk format detection misses split patterns | MEDIUM | Replace chunk regex with rolling buffer state machine; add streaming integration test with chunked mock responses |
| HTTP module SSRF | HIGH | Implement URL allowlist before enabling any network calls; add DNS resolution check; write security test cases |
| HTTP timeout stalling heartbeat | MEDIUM | Set HttpClient.Timeout to 10s; pass CancellationToken from heartbeat; add tick latency alert |
| Credential exposure in logs | MEDIUM | Audit all logging statements in HTTP module; apply mask pattern; verify with log search for `Authorization:` |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Global EventBus routing bypass | Cross-Anima Routing Infrastructure | Integration test: Anima A event does NOT arrive at Anima B |
| Deadlock in sync request-response | Cross-Anima Routing Infrastructure | Timing test: AnimaRoute.ExecuteAsync returns within one tick cycle |
| Orphaned correlation IDs | Cross-Anima Routing Infrastructure | 30-minute soak test: pending dictionary count stays bounded |
| Prompt bloat | Prompt Auto-Injection | Token budget test: injected text never exceeds 300 tokens |
| Streaming format detection failure | Format Detection | Streaming test: pattern detected when split across 3+ chunks |
| Regex fragility | Format Detection | Compliance test: detection succeeds across temperature=0, 0.5, 1.0 |
| SSRF via HTTP module | HTTP Request Module | Security test: localhost, 169.254.x.x, file:// all rejected |
| Credential exposure | HTTP Request Module | Log audit: no Authorization values in output; editor shows masked fields |
| HTTP timeout blocking heartbeat | HTTP Request Module | Slow endpoint test: tick latency stays <200ms during 10s+ HTTP call |
| Service name collisions | Cross-Anima Routing Infrastructure | Multi-Anima test: two Animas with same port name route correctly when addressed by ID |
| Deletion with pending requests | Cross-Anima Routing Infrastructure | Delete test: source Anima receives failure notification within timeout |

## Sources

- [LLM Structured Output in 2026: Stop Parsing JSON with Regex](https://dev.to/pockit_tools/llm-structured-output-in-2026-stop-parsing-json-with-regex-and-do-it-right-34pk) — Format reliability levels: prompt engineering 80-95%, tool_call 99%+ (MEDIUM confidence)
- [OWASP SSRF Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Server_Side_Request_Forgery_Prevention_Cheat_Sheet.html) — Allowlist strategy, DNS rebinding prevention (HIGH confidence)
- [C# HttpClient Security Pitfalls](https://xygeni.io/blog/c-httpclient-common-security-pitfalls-and-safe-practices/) — SSRF in HttpClient, SSL validation, timeout handling (HIGH confidence)
- [Fixing deadlock in request-response pattern — Rust Forum](https://users.rust-lang.org/t/fixing-deadlock-in-request-response-pattern/115229) — Pattern applicable to any async request-response system (MEDIUM confidence)
- [Structured Output Streaming for LLMs](https://medium.com/@prestonblckbrn/structured-output-streaming-for-llms-a836fc0d35a2) — Incremental parsing, rolling buffer approach (MEDIUM confidence)
- [Prompt Injection Attacks: Complete Guide 2026](https://www.getastra.com/blog/ai-security/prompt-injection-attacks/) — Service discovery via injection, attack surface exposure (HIGH confidence)
- [Multi-Agent System Patterns 2025](https://medium.com/@mjgmario/multi-agent-system-patterns-a-unified-guide-to-designing-agentic-architectures-04bb31ab9c41) — Isolation vs communication tradeoffs, synthesis failure patterns (MEDIUM confidence)
- [Microsoft Correlation IDs Engineering Playbook](https://microsoft.github.io/code-with-engineering-playbook/observability/correlation-id/) — Correlation ID design, orphaned request handling (HIGH confidence)
- [How to Handle Timeout Exceptions in HttpClient](https://oneuptime.com/blog/post/2025-12-23-handle-httpclient-timeout-exceptions/view) — HttpClient.Timeout default 100s, retry strategies (HIGH confidence)
- [Every Way To Get Structured Output From LLMs — BAML Blog](https://boundaryml.com/blog/structured-output-from-llms) — Comparison of prompt-based vs constrained decoding reliability (MEDIUM confidence)
- OpenAnima source code — AnimaRuntime, HeartbeatLoop, EventBus, LLMModule, WiringEngine (HIGH confidence — direct inspection)
- OpenAnima PROJECT.md — ANIMA-08 tech debt, v1.6 target features, architectural decisions (HIGH confidence)

---
*Pitfalls research for: Cross-Anima Routing and HTTP Request Module — OpenAnima v1.6*
*Researched: 2026-03-11*
