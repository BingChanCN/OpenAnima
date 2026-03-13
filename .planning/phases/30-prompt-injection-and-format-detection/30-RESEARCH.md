# Phase 30: Prompt Injection and Format Detection - Research

**Researched:** 2026-03-13
**Domain:** LLM prompt engineering, regex-based format detection, C# pattern design
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- XML-style routing marker: `<route service="portName">payload</route>` — tag name fixed as `<route>`, single `service` attribute
- Service name matching is case-insensitive
- Multiple `<route>` markers per LLM response supported — each dispatched independently
- Passthrough text (outside markers) separated and delivered normally
- Inject into system message (highest-priority position)
- Current LLMModule has no system message — add one when routing modules are configured
- When no AnimaRoute modules are configured, system prompt is unchanged
- Service list data source: current Anima's AnimaRoute module targets only, not global registry
  - Query each AnimaRoute module's `targetAnimaId` + `targetPortName` config, look up port description from CrossAnimaRouter registry
- Prompt template language: English template + user-authored service descriptions verbatim
- No token budget cap — inject all configured services regardless of count
- Post-stream whole-response detection (not per-chunk)
- FormatDetector is an independent class (not inline in LLMModule) for separation of concerns and testability
- LLMModule calls FormatDetector after full LLM response is collected
- FormatDetector extracts: passthrough text + list of `(serviceName, payload)` tuples
- Dispatch: FormatDetector result -> LLMModule publishes payload to matching AnimaRoute module's `request` input port -> triggers AnimaRoute -> AnimaRoute calls CrossAnimaRouter
- Passthrough text published to LLMModule's response output port (markers stripped)
- Malformed markers trigger a self-correction cycle: FormatDetector identifies error reason, sends back to same LLM with original context + error, LLM re-generates
- Retry limit: 2 attempts (original + 2 retries = 3 total passes maximum)
- After 2 retries still failing: error bubbled up to upstream caller via error output port
- Unrecognized service names follow the same self-correction flow

### Claude's Discretion
- Exact system prompt template wording and few-shot examples
- Regex pattern design for lenient XML marker parsing (case-insensitive, optional whitespace)
- Internal structure of FormatDetector (interface design, return types)
- How to wire the self-correction loop through LLMModule's execute cycle
- Logging verbosity for format detection events

### Deferred Ideas (OUT OF SCOPE)
- Streaming format detection (detect markers in real-time as tokens arrive) — future FMTD-05 requirement
- Token budget enforcement for injected prompt (revisit if prompt bloat becomes an issue)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PROMPT-01 | LLMModule system prompt auto-includes descriptions of available cross-Anima services | Injection design + `IAnimaModuleConfigService.GetConfig` for AnimaRoute discovery |
| PROMPT-02 | Prompt injection respects token budget cap (200-300 tokens) | LOCKED: No cap — inject all services per user decision |
| PROMPT-03 | Prompt injection includes format instructions for LLM to trigger routing | System prompt template design with XML marker format instructions |
| PROMPT-04 | Prompt injection skips when no routes are configured for current Anima | Conditional check: count of AnimaRoute modules with valid targetAnimaId + targetPortName config |
| FMTD-01 | FormatDetector scans LLM output for routing markers after response completes | Regex-based whole-response scan post-`CompleteAsync` in LLMModule |
| FMTD-02 | FormatDetector splits passthrough text from routing payload | Regex replacement — strip matched markers, return cleaned text + extracted tuples |
| FMTD-03 | FormatDetector dispatches extracted routing calls to CrossAnimaRouter | LLMModule publishes to `AnimaRouteModule.port.request` then `AnimaRouteModule.port.trigger` |
| FMTD-04 | Format detection handles near-miss and malformed markers gracefully (no crash) | Self-correction loop + retry limit + final error port publication |
</phase_requirements>

## Summary

Phase 30 connects two subsystems built in Phase 29: the LLMModule (which calls the LLM) and AnimaRouteModule (which dispatches cross-Anima calls). The work splits cleanly into two parts: (1) prompt injection — at LLMModule initialization time, discover which AnimaRoute modules are configured for the current Anima and build a system message describing available services and the `<route>` marker format; (2) format detection — after the LLM returns a complete response, scan for `<route service="...">...</route>` patterns, strip them from the passthrough text, and trigger AnimaRoute for each extracted payload.

The self-correction loop is the most novel design element: when FormatDetector finds a malformed marker, LLMModule re-invokes the LLM with the original prompt plus an error feedback message, up to 2 retries. After exhausting retries, it publishes the error to a new `error` output port on LLMModule. This design keeps error propagation explicit and consistent with the project philosophy that errors always travel upward.

The entire implementation is contained within `LLMModule.cs` (extended) and a new `FormatDetector.cs` class in `OpenAnima.Core/Modules/` or a dedicated subdirectory. No new DI registrations are needed beyond adding the `error` output port to LLMModule.

**Primary recommendation:** Implement FormatDetector as a static or instance class with a single method `Detect(string response, IReadOnlyList<string> knownServiceNames) -> FormatDetectionResult`. LLMModule orchestrates: inject system message -> call LLM -> call FormatDetector -> dispatch routes or retry.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Text.RegularExpressions.Regex` | .NET built-in | Parse `<route>` markers from LLM output | Built-in, zero deps, supports case-insensitive compiled regex |
| `OpenAI.Chat` (via OpenAI NuGet) | Already in use | Passing system message as `SystemChatMessage` | Already used in `LLMModule.CompleteWithCustomClientAsync` |
| xUnit | 2.9.3 (existing) | Unit tests for FormatDetector, integration tests for LLMModule | Project-standard test framework |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.Extensions.Logging` | .NET built-in | FormatDetector logging | FormatDetector receives `ILogger` via constructor injection |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Regex | `System.Xml` XML parser | XML parser is strict and crashes on malformed XML — exactly the failure mode we need to handle gracefully. Regex is correct here. |
| Regex | `HtmlAgilityPack` | Overkill, adds a dependency, same problem with malformed input |

**Installation:** No new packages required. Everything is already in the project.

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Modules/
│   ├── LLMModule.cs          # Extended: system message injection + FormatDetector calls + retry loop
│   └── FormatDetector.cs     # New: XML marker parsing, passthrough splitting
```

### Pattern 1: FormatDetector as a Pure Detection Class
**What:** `FormatDetector` holds the regex, takes a response string and known service names, returns a typed result. No EventBus knowledge, no routing knowledge.
**When to use:** Always. Separation of concerns: detection logic is testable in isolation, LLMModule handles dispatch.
**Example:**
```csharp
// FormatDetector.cs
public record RouteExtraction(string ServiceName, string Payload);

public record FormatDetectionResult(
    string PassthroughText,         // LLM response with all <route> markers stripped
    IReadOnlyList<RouteExtraction> Routes,  // Ordered list of extracted (serviceName, payload)
    string? MalformedMarkerError    // Non-null if malformed marker detected; null if clean
);

public class FormatDetector
{
    // Lenient: case-insensitive, optional whitespace around attributes and content
    private static readonly Regex RouteMarkerRegex = new(
        @"<route\s+service\s*=\s*""([^""]*)""\s*>(.*?)</route>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // Detect unclosed markers — a near-miss check
    private static readonly Regex UnclosedMarkerRegex = new(
        @"<route\b[^>]*>(?!.*</route>)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public FormatDetectionResult Detect(
        string response,
        IReadOnlySet<string> knownServiceNames)  // case-insensitive set
    {
        // 1. Check for unclosed markers
        if (UnclosedMarkerRegex.IsMatch(response))
            return new(response, [], "Unclosed <route> tag detected");

        var routes = new List<RouteExtraction>();
        string? error = null;

        var cleaned = RouteMarkerRegex.Replace(response, match =>
        {
            var service = match.Groups[1].Value;
            var payload = match.Groups[2].Value.Trim();

            // Case-insensitive service lookup
            var matched = knownServiceNames.FirstOrDefault(
                n => string.Equals(n, service, StringComparison.OrdinalIgnoreCase));

            if (matched == null)
            {
                error = $"Service '{service}' not found in configured routes";
                return match.Value; // Leave in text if unrecognized
            }

            routes.Add(new RouteExtraction(matched, payload));
            return string.Empty; // Strip from passthrough
        });

        return new(cleaned.Trim(), routes, error);
    }
}
```

### Pattern 2: System Message Injection in LLMModule
**What:** At `ExecuteAsync` time, LLMModule queries which AnimaRoute modules have valid config and builds a `system` message prepended to the message list.
**When to use:** Every LLM call. The query is fast (in-memory config lookup).
**Example:**
```csharp
// In LLMModule.ExecuteAsync — build messages list
var messages = new List<ChatMessageInput>();

var systemMessage = BuildSystemMessage();  // returns null if no routes configured
if (systemMessage != null)
    messages.Add(new ChatMessageInput("system", systemMessage));

messages.Add(new ChatMessageInput("user", _pendingPrompt));
```

**Discovering configured AnimaRoute modules:**
```csharp
private string? BuildSystemMessage()
{
    var animaId = _animaContext.ActiveAnimaId;
    if (animaId == null) return null;

    // AnimaRouteModule stores targetAnimaId and targetPortName in its config
    var routeConfig = _configService.GetConfig(animaId, "AnimaRouteModule");
    if (!routeConfig.TryGetValue("targetAnimaId", out var targetAnimaId)
        || !routeConfig.TryGetValue("targetPortName", out var targetPortName)
        || string.IsNullOrWhiteSpace(targetAnimaId)
        || string.IsNullOrWhiteSpace(targetPortName))
        return null;  // PROMPT-04: no routes configured -> no injection

    // Look up port description from CrossAnimaRouter registry
    var ports = _router.GetPortsForAnima(targetAnimaId);
    var port = ports.FirstOrDefault(p =>
        string.Equals(p.PortName, targetPortName, StringComparison.OrdinalIgnoreCase));

    if (port == null) return null;

    return BuildPromptTemplate(new[] { port });
}
```

**IMPORTANT: Multiple AnimaRouteModule instances per Anima**
The current module system registers a single `AnimaRouteModule` instance. If the user adds multiple AnimaRoute modules, each gets the same DI-resolved instance. Verify whether multiple named instances are possible. If they are, the discovery logic must enumerate all configured AnimaRoute modules rather than calling `GetConfig(animaId, "AnimaRouteModule")` once.

Looking at the existing code: `WiringInitializationService` uses `_serviceProvider.GetRequiredService(typeof(AnimaRouteModule))` — this resolves a single instance. The WiringEngine can have multiple module instances by name but DI gives one. For Phase 30, the safe approach is: query `GetConfig(animaId, "AnimaRouteModule")` for the single registered instance. If multiple routes need to be discovered, that requires a module enumeration API that does not currently exist — this is a scope boundary to confirm with the planner.

**Resolution:** The CONTEXT.md says "Query each AnimaRoute module's targetAnimaId + targetPortName config." Given the current DI pattern, only one AnimaRouteModule instance is available. The implementation should handle this correctly for the single-instance case and flag multi-instance as a deferred enhancement.

### Pattern 3: Self-Correction Loop in LLMModule
**What:** After FormatDetector finds a malformed marker, LLMModule appends error feedback to the conversation and re-calls the LLM.
**When to use:** When FormatDetector returns a non-null `MalformedMarkerError`.
**Example:**
```csharp
private const int MaxRetries = 2;

public async Task ExecuteAsync(CancellationToken ct = default)
{
    var messages = BuildMessages();  // includes system message if routes configured
    var attempt = 0;

    while (attempt <= MaxRetries)
    {
        var result = await CallLLMAsync(messages, ct);
        if (!result.Success) { /* publish error, return */ }

        var detectionResult = _formatDetector.Detect(result.Content!, _knownServiceNames);

        if (detectionResult.MalformedMarkerError == null)
        {
            // Clean response — dispatch routes and publish passthrough
            await DispatchRoutesAsync(detectionResult.Routes, ct);
            await PublishPassthroughAsync(detectionResult.PassthroughText, ct);
            return;
        }

        if (attempt >= MaxRetries)
        {
            // Exhausted retries — bubble up error
            await PublishErrorAsync(detectionResult.MalformedMarkerError, ct);
            return;
        }

        // Build correction feedback and retry
        messages.Add(new ChatMessageInput("assistant", result.Content!));
        messages.Add(new ChatMessageInput("user",
            $"Your previous response contained a routing format error: {detectionResult.MalformedMarkerError}. " +
            "Please rewrite your response with correct routing markers."));

        attempt++;
    }
}
```

### Pattern 4: Dispatching Extracted Routes
**What:** For each `RouteExtraction`, LLMModule publishes to `AnimaRouteModule.port.request` then `AnimaRouteModule.port.trigger`. The AnimaRouteModule buffers the payload on `request`, then on `trigger` calls `RouteRequestAsync`.
**When to use:** After FormatDetector returns a clean result with non-empty Routes.
**Example:**
```csharp
private async Task DispatchRoutesAsync(
    IReadOnlyList<RouteExtraction> routes, CancellationToken ct)
{
    foreach (var route in routes)
    {
        // Buffer payload on AnimaRouteModule's request port
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.request",
            SourceModuleId = Metadata.Name,
            Payload = route.Payload
        }, ct);

        // Fire trigger — AnimaRouteModule will call RouteRequestAsync
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "AnimaRouteModule.port.trigger",
            SourceModuleId = Metadata.Name,
            Payload = "trigger"
        }, ct);
    }
}
```

**Caveat:** AnimaRouteModule's trigger handler is async and subscribed to the EventBus. The EventBus `PublishAsync` awaits all subscribers before returning. This means the routing call is awaited inline, which is correct per Phase 29 decision "MUST await RouteRequestAsync."

### System Prompt Template Design
```
You have access to the following services. To use a service, include a routing marker in your response.

Available services:
- {portName}: {description}

Routing marker format:
<route service="{portName}">your request to the service</route>

You may include multiple routing markers in a single response. Any text outside markers is delivered to the user normally.
```

**Few-shot example to include (Claude's discretion):**
```
Example: If asked to summarize a document, you might respond:
"I'll have that summarized for you.
<route service="summarize">Please summarize: [document content]</route>"
```

### Anti-Patterns to Avoid
- **Per-chunk marker detection:** Rejected by Roadmap and CONTEXT.md. Partial tokens break regex. Always scan the full collected response.
- **Crashing on invalid XML:** Never use `XmlDocument.Parse()` or similar. Regex on raw string handles malformed cases gracefully.
- **Swallowing errors:** Malformed markers after max retries must reach the error output port, not be silently dropped.
- **Modifying `_pendingPrompt` during retry:** Keep the original prompt immutable; build a new `messages` list with appended correction turns.
- **Discovering routes at initialization time:** Route config can change while the module is running. Query `GetConfig` on every `ExecuteAsync` call (it's fast, in-memory).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| XML/HTML parsing of `<route>` tags | Custom parser | `Regex` with `RegexOptions.Singleline | IgnoreCase` | LLM output is not guaranteed valid XML. Regex handles malformed cases without throwing. |
| Case-insensitive string comparison | Manual `.ToLower()` | `StringComparison.OrdinalIgnoreCase` | Already used throughout the codebase; correct for port name matching |
| Message list construction | Mutable global state | New `List<ChatMessageInput>` per `ExecuteAsync` call | LLMModule already constructs a fresh list per call (see existing code line 69) |
| Retry counter | Thread-safe counter | Local `int attempt` variable | `ExecuteAsync` is not concurrent per module instance; local variable is correct |

**Key insight:** The existing `CompleteWithCustomClientAsync` already correctly handles `"system"` role in the `ChatMessage` switch — no new mapping code is needed.

## Common Pitfalls

### Pitfall 1: Single vs. Multiple AnimaRoute Module Instances
**What goes wrong:** The implementation calls `GetConfig(animaId, "AnimaRouteModule")` expecting a single route, but the user has added multiple AnimaRoute modules (each with different target configs). Only one route is discovered and injected.
**Why it happens:** DI resolves a single `AnimaRouteModule` instance. The module's config key is always `"AnimaRouteModule"`. The current config service stores one config dict per `(animaId, moduleId)` pair, so multiple instances share the same config key.
**How to avoid:** Document this as a known limitation in Phase 30. The single-instance case is the designed scope. Multi-instance enumeration requires a future module registry API.
**Warning signs:** User configures two AnimaRoute modules but only one service appears in the system prompt.

### Pitfall 2: Passthrough Text Has Leading/Trailing Whitespace After Marker Stripping
**What goes wrong:** After replacing `<route>...</route>` with `string.Empty`, the passthrough text has orphaned newlines and spaces around the removed markers.
**Why it happens:** LLMs often place routing markers on their own lines: `"Here is my reply.\n\n<route service="svc">payload</route>\n\nMore text."` → after stripping → `"Here is my reply.\n\n\n\nMore text."`
**How to avoid:** In the Regex replacement callback, consider replacing `match.Value` with `string.Empty` and then calling `.Trim()` on the final cleaned string. Or normalize multiple consecutive newlines to double-newline.
**Warning signs:** Chat display shows extra blank lines between sentences.

### Pitfall 3: LLM Re-Generates with Same Malformed Marker
**What goes wrong:** The correction prompt doesn't give enough context for the LLM to fix the error. The same malformed output is produced on retry 1 and retry 2, wasting tokens.
**Why it happens:** The correction message is too vague ("format error") without showing what correct format looks like.
**How to avoid:** Include the routing format example in the correction message (not just the error reason). E.g., "Expected format: `<route service=\"portName\">payload</route>`."
**Warning signs:** All 3 attempts produce identical malformed output.

### Pitfall 4: EventBus Publish Order for Request + Trigger
**What goes wrong:** `trigger` is published before `request` buffer is set, causing AnimaRouteModule to log "trigger fired with no request data buffered" and publish an error.
**Why it happens:** The order of `PublishAsync` calls is wrong (trigger before request).
**How to avoid:** Always publish `request` first, then `trigger`. The EventBus awaits each subscriber synchronously before returning — buffer is guaranteed to be set before trigger handler runs.
**Warning signs:** AnimaRouteModule error port fires with "NoRequestData" immediately after a route is detected.

### Pitfall 5: `knownServiceNames` Set Built from Stale Config
**What goes wrong:** `FormatDetector` receives a set of service names that was built at initialization time. If AnimaRoute config changes at runtime, the set is stale and valid service names are rejected as unknown.
**Why it happens:** Building the known-names set once at `InitializeAsync` time instead of per-call.
**How to avoid:** Build `knownServiceNames` inside `ExecuteAsync` on each call. `GetConfig` is O(1) dictionary lookup — no performance concern.

### Pitfall 6: LLMModule Needs ICrossAnimaRouter Injected
**What goes wrong:** LLMModule needs to call `_router.GetPortsForAnima(targetAnimaId)` to look up port descriptions for the system message, but `ICrossAnimaRouter` is not currently injected into LLMModule.
**Why it happens:** Phase 29 left LLMModule without router awareness — it was not needed before Phase 30.
**How to avoid:** Add `ICrossAnimaRouter` as a constructor parameter to LLMModule. Register it in DI (already registered as singleton from Phase 28-29). The optional parameter pattern (`ICrossAnimaRouter? router = null`) keeps backward compatibility with tests.
**Warning signs:** NullReferenceException in `BuildSystemMessage()`.

## Code Examples

### Regex Pattern for Lenient `<route>` Detection
```csharp
// Source: .NET Regex documentation + project decision (case-insensitive, singleline, compiled)
private static readonly Regex RouteMarkerRegex = new(
    @"<route\s+service\s*=\s*""([^""]*)""\s*>(.*?)</route>",
    RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
```

- `\s+` after `route` — requires at least one space (standard XML)
- `\s*=\s*` — optional spaces around `=`
- `([^""]*)` — captures service name (stops at closing quote)
- `\s*` — optional whitespace before `>`
- `(.*?)` — non-greedy capture of payload (stops at first `</route>`)
- `RegexOptions.Singleline` — `.` matches newlines (payload may be multiline)
- `RegexOptions.Compiled` — amortizes compilation cost since this runs on every LLM response

### Building Known Service Names Set
```csharp
// In LLMModule.ExecuteAsync — build the set of known service names for FormatDetector
private HashSet<string> BuildKnownServiceNames(string animaId)
{
    var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var config = _configService.GetConfig(animaId, "AnimaRouteModule");
    if (config.TryGetValue("targetAnimaId", out var tId)
        && config.TryGetValue("targetPortName", out var tPort)
        && !string.IsNullOrWhiteSpace(tId)
        && !string.IsNullOrWhiteSpace(tPort))
    {
        result.Add(tPort);  // port name IS the service name used in <route service="...">
    }
    return result;
}
```

### LLMModule Constructor Extension
```csharp
// Add ICrossAnimaRouter to LLMModule constructor (optional for backward compat)
public LLMModule(
    ILLMService llmService,
    IEventBus eventBus,
    ILogger<LLMModule> logger,
    IAnimaModuleConfigService configService,
    IAnimaContext animaContext,
    ICrossAnimaRouter? router = null)   // NEW: optional for backward compatibility
```

### New LLMModule Output Port
```csharp
// Add error output port to LLMModule class declaration
[InputPort("prompt", PortType.Text)]
[OutputPort("response", PortType.Text)]
[OutputPort("error", PortType.Text)]     // NEW: for format detection failures after max retries
public class LLMModule : IModuleExecutor
```

### Unit Test Pattern for FormatDetector
```csharp
// Source: existing test pattern from RoutingModulesTests.cs
[Fact]
public void FormatDetector_CleanResponse_ReturnsPassthroughAndRoutes()
{
    var detector = new FormatDetector();
    var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "summarize" };
    var input = @"Here is my analysis.
<route service=""summarize"">Please summarize this document.</route>
Let me know if you need more.";

    var result = detector.Detect(input, known);

    Assert.Null(result.MalformedMarkerError);
    Assert.Single(result.Routes);
    Assert.Equal("summarize", result.Routes[0].ServiceName);
    Assert.Equal("Please summarize this document.", result.Routes[0].Payload);
    Assert.DoesNotContain("<route", result.PassthroughText);
    Assert.Contains("Here is my analysis.", result.PassthroughText);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No system message in LLMModule | System message added when routes configured | Phase 30 | LLM learns available services automatically |
| LLM response passed directly to output port | Response filtered through FormatDetector first | Phase 30 | Markers stripped, routes dispatched |
| LLMModule has single output port (`response`) | Two output ports: `response` + `error` | Phase 30 | Format detection failures surfaced to wiring |
| LLMModule message list: `[user]` | Message list: `[system?, user, assistant?, user?]` | Phase 30 | System injection + retry conversation |

**Deprecated/outdated:**
- Phase 30 makes the `messages` construction in `ExecuteAsync` more complex; the existing single-line `new List<ChatMessageInput> { new("user", _pendingPrompt) }` is replaced.

## Open Questions

1. **Multiple AnimaRoute modules per Anima**
   - What we know: DI resolves one `AnimaRouteModule` instance per Anima. `IAnimaModuleConfigService` stores config per `(animaId, moduleId)` — one entry for `"AnimaRouteModule"`.
   - What's unclear: Can a user add multiple AnimaRoute modules (each pointing to a different target)? If so, Phase 30 only injects one service into the system prompt.
   - Recommendation: Implement for single-instance (the only currently supported case). Document the limitation. Multi-instance enumeration is a v1.7+ concern.

2. **`error` output port registration in WiringInitializationService**
   - What we know: `WiringInitializationService.PortRegistrationTypes` lists module types for port discovery. Adding `[OutputPort("error", PortType.Text)]` to LLMModule will be discovered automatically.
   - What's unclear: Is there any test that asserts the exact list of LLMModule ports? Could break if a test checks port count.
   - Recommendation: Verify by grepping existing tests before adding the port attribute.

3. **Self-correction loop and ChatContextManager**
   - What we know: LLMModule does not currently use `ChatContextManager` — it sends a fresh single-turn message each time. The correction loop builds a multi-turn conversation within a single `ExecuteAsync` call.
   - What's unclear: Should the correction turns be persisted to `ChatContextManager` (conversation history) or treated as ephemeral retry scaffolding?
   - Recommendation: Keep correction turns ephemeral (not persisted). The user should see the final clean response, not the retry scaffolding.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none (implicit discovery) |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --filter "Category=Routing|FormatDetector" --no-build` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/ --no-build` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PROMPT-01 | LLMModule system message contains service descriptions when routes configured | Unit | `dotnet test --filter "FormatDetector|LLMModule" --no-build` | Wave 0 |
| PROMPT-03 | System message includes `<route>` format instructions | Unit | same | Wave 0 |
| PROMPT-04 | No system message injected when no routes configured | Unit | same | Wave 0 |
| FMTD-01 | FormatDetector.Detect finds `<route>` markers in response | Unit | `dotnet test --filter "FormatDetector" --no-build` | Wave 0 |
| FMTD-02 | FormatDetector strips markers from passthrough text | Unit | same | Wave 0 |
| FMTD-03 | LLMModule dispatches extracted routes to AnimaRouteModule ports | Integration | `dotnet test --filter "Category=Routing" --no-build` | Wave 0 |
| FMTD-04 | Malformed marker triggers self-correction; does not crash | Unit + Integration | same | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ --filter "FormatDetector|PromptInjection" --no-build`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/ --no-build`
- **Phase gate:** Full suite green (with known 3 pre-existing failures excluded) before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Modules/FormatDetectorTests.cs` — covers FMTD-01, FMTD-02, FMTD-04
- [ ] `tests/OpenAnima.Tests/Integration/PromptInjectionIntegrationTests.cs` — covers PROMPT-01, PROMPT-03, PROMPT-04, FMTD-03

*(No new framework install needed — xUnit and project infrastructure already in place)*

## Sources

### Primary (HIGH confidence)
- Codebase direct inspection: `src/OpenAnima.Core/Modules/LLMModule.cs` — confirmed current message construction and `CompleteWithCustomClientAsync` system message support
- Codebase direct inspection: `src/OpenAnima.Core/Modules/AnimaRouteModule.cs` — confirmed `request` and `trigger` port event names
- Codebase direct inspection: `src/OpenAnima.Core/Routing/ICrossAnimaRouter.cs` + `CrossAnimaRouter.cs` — confirmed `GetPortsForAnima` API and `PortRegistration` fields
- Codebase direct inspection: `src/OpenAnima.Core/Services/IAnimaModuleConfigService.cs` — confirmed `GetConfig(animaId, moduleId)` API
- Codebase direct inspection: `.planning/phases/30-prompt-injection-and-format-detection/30-CONTEXT.md` — all locked decisions

### Secondary (MEDIUM confidence)
- .NET documentation (training data): `System.Text.RegularExpressions.Regex` compiled option, `RegexOptions.Singleline` for cross-line matching, `RegexOptions.IgnoreCase`
- .NET documentation (training data): `HashSet<string>(StringComparer.OrdinalIgnoreCase)` for case-insensitive service name lookup

### Tertiary (LOW confidence)
- LLM format compliance estimate (80-95%) sourced from CONTEXT.md `<specifics>` section (user-provided domain knowledge, not independently verified)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages required; all libraries confirmed present in codebase
- Architecture: HIGH — FormatDetector design, LLMModule extension, and dispatch pattern all derived from direct codebase analysis
- Pitfalls: HIGH — all pitfalls derived from actual code inspection (e.g., single DI instance, EventBus publish order from AnimaRouteModule source)

**Research date:** 2026-03-13
**Valid until:** 2026-04-13 (30 days — codebase is stable, no fast-moving dependencies)
