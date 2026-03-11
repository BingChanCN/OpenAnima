# Architecture Research

**Domain:** Cross-Anima request-response routing and HTTP request module for OpenAnima v1.6
**Researched:** 2026-03-11
**Confidence:** HIGH — based on direct source code analysis of the v1.5 codebase

---

## Standard Architecture

### System Overview — v1.6 Additions in Context

```
┌───────────────────────────────────────────────────────────────────────────┐
│                       Blazor Server UI (SignalR)                           │
│  ┌────────────────────────────────────────────────────────────────────┐   │
│  │    Wiring Editor (SVG canvas, drag-drop, node inspector)            │   │
│  │    Shows: AnimaInputPortModule, AnimaOutputPortModule,              │   │
│  │           AnimaRouteModule, HttpRequestModule as standard nodes     │   │
│  └────────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────┬────────────────────────────────────────────┘
                               │ SignalR / Blazor circuit
┌──────────────────────────────▼────────────────────────────────────────────┐
│                 APPLICATION LAYER (Singleton Services)                     │
│                                                                            │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │              AnimaRuntimeManager (Singleton — unchanged)              │ │
│  │  GetAll() | GetById() | CreateAsync() | GetOrCreateRuntime()          │ │
│  └───────────────────────────┬──────────────────────────────────────────┘ │
│                              │ owns Dictionary<string, AnimaRuntime>       │
│  ┌───────────────────────────▼──────────────────────────────────────────┐ │
│  │           CrossAnimaRouter (NEW — Singleton)                          │ │
│  │  RegisterInputPort() | RouteRequestAsync() | CompleteRequest()        │ │
│  │  GetAllRegisteredPorts()                                              │ │
│  │  _ports: ConcurrentDictionary<"{animaId}::{portName}", Registration> │ │
│  │  _pending: ConcurrentDictionary<correlationId, TaskCompletionSource>  │ │
│  └──────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────┬────────────────────────────────────────────┘
                               │
┌──────────────────────────────▼────────────────────────────────────────────┐
│                  ANIMA RUNTIME LAYER (Per-Anima, unchanged)                │
│                                                                            │
│  Anima A (calling)                     Anima B (target)                   │
│  ┌──────────────────────────────┐      ┌──────────────────────────────┐   │
│  │  AnimaRuntime                │      │  AnimaRuntime                │   │
│  │  ├ EventBus (isolated)       │      │  ├ EventBus (isolated)       │   │
│  │  ├ WiringEngine              │      │  ├ WiringEngine              │   │
│  │  └ PluginRegistry            │      │  └ PluginRegistry            │   │
│  │                              │      │                              │   │
│  │  Module nodes in graph:      │      │  Module nodes in graph:      │   │
│  │  ┌──────────────────────┐    │      │  ┌──────────────────────┐    │   │
│  │  │ LLMModule (modified) │    │      │  │ AnimaInputPortModule  │    │   │
│  │  │ - prompt injection   │    │      │  │ (NEW — declares svc)  │    │   │
│  │  │ - FormatDetector     │◄───┼──────┼──│ registers w/ router   │    │   │
│  │  └──────────────────────┘    │      │  └────────────┬─────────┘    │   │
│  │  ┌──────────────────────┐    │      │               │ EventBus     │   │
│  │  │ AnimaRouteModule     │    │      │  ┌────────────▼─────────┐    │   │
│  │  │ (NEW — sends req)    │────┼──────┼──► [sub-graph: LLM etc] │    │   │
│  │  └──────────────────────┘    │      │  └────────────┬─────────┘    │   │
│  │  ┌──────────────────────┐    │      │  ┌────────────▼─────────┐    │   │
│  │  │ HttpRequestModule    │    │      │  │ AnimaOutputPortModule │    │   │
│  │  │ (NEW — HTTP tool)    │    │      │  │ (NEW — returns resp)  │────┼───┤
│  │  └──────────────────────┘    │      │  └──────────────────────┘    │   │
│  └──────────────────────────────┘      └──────────────────────────────┘   │
└───────────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

**Existing components — unchanged unless noted:**

| Component | File | Responsibility | v1.6 Change |
|-----------|------|----------------|-------------|
| `AnimaRuntimeManager` | `Core/Anima/AnimaRuntimeManager.cs` | Singleton factory for all AnimaRuntime instances | None |
| `AnimaRuntime` | `Core/Anima/AnimaRuntime.cs` | Per-Anima container: EventBus, PluginRegistry, HeartbeatLoop, WiringEngine | None |
| `EventBus` | `Core/Events/EventBus.cs` | Per-Anima pub/sub; typed `ModuleEvent<T>` routing | None |
| `WiringEngine` | `Core/Wiring/WiringEngine.cs` | Topological execution; EventBus subscriptions for port routing | None |
| `AnimaContext` | `Core/Anima/AnimaContext.cs` | Singleton identifying the active Anima for the UI | None |
| `IAnimaModuleConfigService` | `Core/Services/AnimaModuleConfigService.cs` | Per-Anima module config (key-value, JSON persistence) | None |
| `LLMModule` | `Core/Modules/LLMModule.cs` | LLM API call, publishes response to EventBus | **Modified** — prompt injection + FormatDetector call |

**New components (v1.6):**

| Component | File | Responsibility |
|-----------|------|----------------|
| `CrossAnimaRouter` | `Core/Routing/CrossAnimaRouter.cs` | Singleton broker above all runtimes; maps port keys to registrations; holds correlation ID map |
| `InputPortDescriptor` | `Core/Routing/InputPortDescriptor.cs` | DTO: animaId, animaName, portName, description — used for prompt injection |
| `FormatDetector` | `Core/Routing/FormatDetector.cs` | Stateless parser; extracts routing calls from LLM output text |
| `AnimaInputPortModule` | `Core/Modules/AnimaInputPortModule.cs` | Standard IModule; registers named service with CrossAnimaRouter on init |
| `AnimaOutputPortModule` | `Core/Modules/AnimaOutputPortModule.cs` | Standard IModule; completes pending CrossAnimaRouter request on execution |
| `AnimaRouteModule` | `Core/Modules/AnimaRouteModule.cs` | Standard IModule; configured with target Anima ID + port name; sends request, awaits response |
| `HttpRequestModule` | `Core/Modules/HttpRequestModule.cs` | Standard IModule; configurable HTTP call; uses IHttpClientFactory |

---

## Integration Points — New vs Modified

### New: CrossAnimaRouter

```csharp
// Core/Routing/CrossAnimaRouter.cs
// Registered as singleton in AnimaServiceExtensions.AddAnimaServices()

public class CrossAnimaRouter
{
    // Key: "{animaId}::{portName}" → registered handler + metadata
    private readonly ConcurrentDictionary<string, InputPortRegistration> _ports = new();

    // Key: correlationId → TaskCompletionSource<string>
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();

    private readonly IAnimaRuntimeManager _runtimeManager;

    // Called by AnimaInputPortModule.InitializeAsync
    public void RegisterInputPort(string animaId, string portName, string description,
        Func<string, string, CancellationToken, Task> handler);

    // Called by AnimaInputPortModule.ShutdownAsync
    public void UnregisterInputPort(string animaId, string portName);

    // Called by AnimaRouteModule.ExecuteAsync — suspends until CompleteRequest called
    public Task<string> RouteRequestAsync(string targetAnimaId, string portName,
        string payload, int timeoutMs, CancellationToken ct);

    // Called by AnimaOutputPortModule when its input port fires
    public void CompleteRequest(string correlationId, string responsePayload);

    // Called by LLMModule.BuildSystemPrompt — returns all registered services for injection
    public IReadOnlyList<InputPortDescriptor> GetAllRegisteredPorts();

    // Called by AnimaRuntimeManager on Anima deletion — cancels any pending requests
    public void CancelPendingForAnima(string animaId);
}
```

**Injection:** `CrossAnimaRouter` is registered as singleton in `AnimaServiceExtensions.AddAnimaServices()`. It receives `IAnimaRuntimeManager` to resolve Anima names for prompt descriptions (avoids circular dependency since both are singletons).

### New: AnimaInputPortModule

Standard `IModuleExecutor`. Config keys (via `IAnimaModuleConfigService`):
- `portName` — the service name other Animas reference
- `description` — displayed in LLM system prompt

On `InitializeAsync`:
1. Reads `portName` and `description` from config
2. Calls `CrossAnimaRouter.RegisterInputPort(animaId, portName, description, handler)`
3. The handler receives `(correlationId, payload)` and publishes to local EventBus with embedded prefix

Ports:
- Output: `request` (Text) — fires when a cross-Anima request arrives; payload has correlation prefix stripped for downstream modules

On `ShutdownAsync`: calls `CrossAnimaRouter.UnregisterInputPort`.

### New: AnimaOutputPortModule

Standard `IModuleExecutor`. Config keys:
- `inputPortModuleId` — the node ID of the paired `AnimaInputPortModule` in this Anima's graph (needed to recover the correlation ID)

Simpler approach: embed correlation ID in the text prefix `[CORR:{id}]\n{payload}` so `AnimaOutputPortModule` extracts it from whatever text arrives on its input port without needing an explicit module reference.

Ports:
- Input: `response` (Text) — receives processed response from the sub-graph; text contains correlation ID prefix

On receiving input via EventBus subscription:
1. Extracts correlation ID prefix from text
2. Strips prefix to get clean response text
3. Calls `CrossAnimaRouter.CompleteRequest(correlationId, cleanText)`

### New: AnimaRouteModule

Standard `IModuleExecutor`. Config keys:
- `targetAnimaId` — ID of the Anima to call
- `targetPortName` — name of the `AnimaInputPortModule` port
- `timeoutMs` — default 30000

Ports:
- Input: `request` (Text) — payload to send to target Anima
- Output: `response` (Text) — response received from target Anima

`ExecuteAsync` calls `CrossAnimaRouter.RouteRequestAsync(targetAnimaId, targetPortName, payload, timeoutMs, ct)` and `await`s the result. This naturally suspends the WiringEngine's level execution until the response arrives.

### New: FormatDetector

Stateless utility class — not an IModule, not injected directly. Called from `LLMModule.ExecuteAsync`.

```csharp
// Core/Routing/FormatDetector.cs

public static class FormatDetector
{
    // Parses LLM output for routing markers, returns split result
    public static FormatDetectorResult Parse(string llmOutput);
}

public record FormatDetectorResult(
    string PassthroughText,           // Text with routing markers removed
    IReadOnlyList<RoutingCall> Calls  // Extracted routing calls
);

public record RoutingCall(
    string PortName,   // Target port name
    string Payload     // Text to send to that port
);
```

**Format convention** — simple, unambiguous, LLM-friendly:
```
Regular response text here.
@@ROUTE:portName|message to send to the service@@
More response text continues here.
```

Multiple `@@ROUTE:..@@` markers in one response are all extracted. The `PassthroughText` has markers removed and whitespace normalized.

### Modified: LLMModule

Two additions to `ExecuteAsync`:

**1. Prompt injection (before LLM call):**
```csharp
private string BuildSystemPrompt(string basePrompt)
{
    var ports = _crossAnimaRouter?.GetAllRegisteredPorts() ?? Array.Empty<InputPortDescriptor>();
    if (ports.Count == 0)
        return basePrompt;

    var serviceLines = string.Join("\n", ports.Select(p =>
        $"  - {p.PortName} (from Anima '{p.AnimaName}'): {p.Description}"));

    return basePrompt + "\n\n" +
        "You have access to these services. To call a service, include in your response:\n" +
        "@@ROUTE:portName|your message to the service@@\n" +
        "Available services:\n" + serviceLines;
}
```

`_crossAnimaRouter` is nullable — if not injected (e.g., in tests), injection is skipped silently. LLMModule constructor gains an optional `CrossAnimaRouter? crossAnimaRouter = null` parameter.

**2. FormatDetector call (after LLM response, before publish):**
```csharp
// After receiving result.Content:
var detected = FormatDetector.Parse(result.Content);

// Dispatch routing calls (fire-and-forget with error logging)
foreach (var call in detected.Calls)
{
    _ = DispatchRoutingCallAsync(call, ct);
}

// Publish clean passthrough text to response port
await _eventBus.PublishAsync(new ModuleEvent<string>
{
    EventName = $"{Metadata.Name}.port.response",
    SourceModuleId = Metadata.Name,
    Payload = detected.PassthroughText
}, ct);
```

### New: HttpRequestModule

Standard `IModuleExecutor`. Receives `IHttpClientFactory` via constructor (registered in DI via `builder.Services.AddHttpClient()`).

Config keys:
- `method` — GET, POST, PUT, DELETE (default: GET)
- `url` — static URL (overridden by input port if wired)
- `headers` — JSON string of key-value pairs
- `bodyTemplate` — text with optional `{body}` placeholder

Ports:
- Input: `url` (Text, optional — overrides config URL if wired)
- Input: `body` (Text, optional — substituted into bodyTemplate or sent as-is)
- Output: `response` (Text — response body)
- Output: `statusCode` (Text — e.g., "200", "404", "500")

On error (network failure, timeout): publish error message to `response` port, set state to Error, continue execution (don't throw — consistent with LLMModule's inline error pattern).

---

## Recommended Project Structure — New Files

```
src/OpenAnima.Core/
├── Routing/                        # NEW directory
│   ├── CrossAnimaRouter.cs         # Singleton cross-Anima broker
│   ├── FormatDetector.cs           # Stateless LLM output parser
│   ├── FormatDetectorResult.cs     # Result record
│   ├── InputPortDescriptor.cs      # DTO for prompt injection
│   └── InputPortRegistration.cs    # Internal registration record
│
├── Modules/                        # Add new files; modify LLMModule
│   ├── AnimaInputPortModule.cs     # NEW — declares named service
│   ├── AnimaOutputPortModule.cs    # NEW — returns response via router
│   ├── AnimaRouteModule.cs         # NEW — sends cross-Anima request
│   ├── HttpRequestModule.cs        # NEW — HTTP tool node
│   ├── LLMModule.cs                # MODIFIED — prompt injection + FormatDetector
│   └── [existing modules, unchanged]
│
└── DependencyInjection/
    └── AnimaServiceExtensions.cs   # MODIFIED — register CrossAnimaRouter singleton
```

---

## Architectural Patterns

### Pattern 1: Cross-Anima Request-Response via Singleton Broker

**What:** `CrossAnimaRouter` acts as a message broker above all Anima runtimes. The calling Anima suspends via `TaskCompletionSource<string>` until the target Anima's `AnimaOutputPortModule` calls `CompleteRequest`. Per-Anima EventBus instances remain fully isolated — the router bridges them without merging them.

**When to use:** Whenever one Anima needs to call another and await a typed response.

**Why not bridge via EventBus directly:** Each Anima has its own `EventBus` instance (created inside `AnimaRuntime`). This is an intentional isolation invariant established in v1.5. Injecting a shared EventBus would collapse isolation. The `CrossAnimaRouter` singleton sits above the runtimes and can reach any Anima's EventBus — modules cannot.

**Trade-offs:**
- Pro: Anima runtimes remain completely isolated — existing guarantees intact
- Pro: Correlation tracking is explicit and observable for debugging
- Pro: `AnimaRuntimeManager` is already singleton — `CrossAnimaRouter` is a natural peer
- Con: `TaskCompletionSource` requires timeout to prevent deadlock if target Anima never responds
- Con: If target Anima is deleted while a request is in flight, router must cancel pending completions — handle in `CancelPendingForAnima`

**Example:**

```csharp
// In CrossAnimaRouter.RouteRequestAsync
public async Task<string> RouteRequestAsync(
    string targetAnimaId, string portName, string payload,
    int timeoutMs, CancellationToken ct)
{
    var correlationId = Guid.NewGuid().ToString("N")[..16];
    var tcs = new TaskCompletionSource<string>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    _pending[correlationId] = tcs;
    ct.Register(() => tcs.TrySetCanceled());

    var key = $"{targetAnimaId}::{portName}";
    if (!_ports.TryGetValue(key, out var registration))
    {
        _pending.TryRemove(correlationId, out _);
        throw new InvalidOperationException($"No input port registered: {key}");
    }

    // Deliver to target Anima's EventBus via handler stored at registration time
    await registration.Handler(correlationId, payload, ct);

    try
    {
        return await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct);
    }
    finally
    {
        _pending.TryRemove(correlationId, out _);
    }
}
```

### Pattern 2: Correlation ID as Text Prefix

**What:** When `CrossAnimaRouter` delivers a request to the target Anima, the payload includes an embedded prefix: `[CORR:{correlationId}]\n{actualPayload}`. `AnimaInputPortModule` strips this prefix before publishing to its `request` output port. `AnimaOutputPortModule` extracts it from whatever text arrives on its input port before calling `CompleteRequest`.

**When to use:** Wherever correlation state needs to travel through the Anima's sub-graph without the intermediate modules being aware of it.

**Why not pass correlation ID through a shared dictionary:** That would require module-to-module coupling (`AnimaOutputPortModule` knowing the ID of its paired `AnimaInputPortModule`). The prefix approach is stateless — no shared register needed.

**Trade-offs:**
- Pro: Intermediate modules (LLM, TextSplit, etc.) receive and pass clean text unmodified
- Pro: `AnimaInputPortModule` and `AnimaOutputPortModule` are stateless — no shared mutable state
- Con: If the sub-graph transforms the text (e.g., TextJoin merges it with other text), the prefix is lost — correlation fails. Mitigation: document that the response path must preserve the prefix token, or pass it on a separate Trigger port

**Recommendation:** Use a dedicated `correlationId` Trigger output port on `AnimaInputPortModule` and a `correlationId` Trigger input port on `AnimaOutputPortModule`. Wire them separately in the editor. This avoids prefix-in-text fragility at the cost of requiring an explicit wire in the graph.

### Pattern 3: Prompt Auto-Injection via System Prompt Construction

**What:** `LLMModule.ExecuteAsync` queries `CrossAnimaRouter.GetAllRegisteredPorts()` before building the message list. If any ports are registered, the service list is appended to the system prompt so the LLM knows available downstream services and the `@@ROUTE:..@@` format.

**When to use:** `CrossAnimaRouter` has at least one registered port (any Anima has an `AnimaInputPortModule` wired and initialized).

**Why in LLMModule, not in a separate component:** LLMModule already owns system prompt construction (it reads `systemPrompt` from `IAnimaModuleConfigService`). Adding service injection here is a minimal, contained change. No new event or subscription is needed.

**Trade-offs:**
- Pro: Automatic — services appear in context as soon as `AnimaInputPortModule` initializes
- Pro: No schema changes to LLMModule's config interface
- Con: Increases token cost slightly per registered route (minor — descriptors are short)
- Con: If `CrossAnimaRouter` is null (not injected), silently skips — correct behavior for environments without routing

### Pattern 4: Format Detection as Post-LLM Interceptor

**What:** After `LLMModule` receives the full LLM response, before publishing to the `response` EventBus port, it calls `FormatDetector.Parse(content)`. Routing calls are dispatched; passthrough text is published to the response port.

**When to use:** Always — `FormatDetector.Parse` returns original text unchanged when no markers are found.

**Why in LLMModule, not WiringEngine:** WiringEngine does not know routing semantics. LLMModule owns its output — modifying the publish step is the minimum change. No new subscriptions needed.

**Trade-offs:**
- Pro: Transparent to existing wiring — downstream modules receive clean text
- Pro: Works in non-streaming mode without architecture changes
- Con: Streaming UX: format detection requires the full response to be buffered before markers can be found. The current `LLMModule` does not stream to EventBus — it calls `CompleteAsync` and publishes the full result. No streaming change required for v1.6.
- Con: LLM hallucination risk — malformed markers (`@@ROUTE:` without closing `@@`) are silently ignored. Document that malformed markers are dropped with a warning log.

---

## Data Flow

### Cross-Anima Request-Response Cycle

```
[Calling Anima — WiringEngine tick]
         │
         ▼
  AnimaRouteModule.ExecuteAsync()
         │  config: targetAnimaId="b2c3d4e5", targetPortName="translate"
         │  _pendingRequest = text on input port
         ▼
  CrossAnimaRouter.RouteRequestAsync("b2c3d4e5", "translate", payload, 30000, ct)
         │  1. Generate correlationId = "a1b2c3d4e5f6g7h8"
         │  2. Store correlationId → TaskCompletionSource in _pending
         │  3. Look up registration for "b2c3d4e5::translate"
         │  4. Call registration.Handler(correlationId, payload, ct)
         ↓ (awaits TaskCompletionSource)

  [Target Anima "b2c3d4e5" — EventBus receives delivery]
         │
         ▼
  AnimaInputPortModule (registered handler fires)
         │  strips correlation prefix from delivery (or receives via separate mechanism)
         │  publishes to own EventBus:
         │    EventName = "AnimaInputPortModule.port.request"
         │    Payload = payload text
         ▼
  [Target Anima WiringEngine sees event, executes sub-graph]
  LLMModule → produces response → AnimaOutputPortModule
         │
         ▼
  AnimaOutputPortModule.ExecuteAsync()
         │  extracts correlationId (from Trigger wire or text prefix)
         │  calls CrossAnimaRouter.CompleteRequest("a1b2c3d4e5f6g7h8", cleanResponse)
         ↓ (resolves TaskCompletionSource)

  [CrossAnimaRouter.RouteRequestAsync — awaited by calling Anima]
         │  tcs.Task.Result = cleanResponse
         ▼
  AnimaRouteModule receives response text
         │  publishes to "AnimaRouteModule.port.response" on calling Anima's EventBus
         ▼
  [Downstream modules in calling Anima receive response]
```

### LLM Format Detection Flow

```
[LLMModule.ExecuteAsync — after LLM API call returns]
         │
         ▼
  FormatDetector.Parse(result.Content)
         │  Returns: PassthroughText + List<RoutingCall>
         │
         ├── If RoutingCalls.Count > 0:
         │     foreach RoutingCall in calls:
         │       CrossAnimaRouter.RouteRequestAsync(call.PortName, call.Payload)
         │       (fire-and-forget dispatched — not awaited by LLMModule)
         │
         └── Publish PassthroughText to "LLMModule.port.response"
                    │
                    ▼
             [Downstream modules receive clean text]
```

Note: Format-detected routing calls are fire-and-forget from LLMModule's perspective. The LLM response is delivered to the chat UI without waiting for routing completion. This keeps the chat interaction snappy. If blocking behavior is needed (wait for service response before continuing), use explicit `AnimaRouteModule` nodes in the wiring graph instead.

### HTTP Request Module Flow

```
[WiringEngine tick — HttpRequestModule node executes]
         │
         ▼
  HttpRequestModule.ExecuteAsync()
         │  1. Read config: method, staticUrl, headers, bodyTemplate
         │  2. Check input port "url" — override config URL if present
         │  3. Check input port "body" — substitute into bodyTemplate if present
         ▼
  IHttpClientFactory.CreateClient()
         │  Build HttpRequestMessage (method, url, headers, body)
         │  SendAsync(request, ct)
         │
         ├── Success (2xx-5xx):
         │     Publish response body → "HttpRequestModule.port.response"
         │     Publish status code string → "HttpRequestModule.port.statusCode"
         │
         └── Exception (network error, timeout):
               _state = Error
               Publish error message → "HttpRequestModule.port.response"
               Publish "0" → "HttpRequestModule.port.statusCode"
               Log error (do not throw — consistent with LLMModule error pattern)
```

### Prompt Injection Flow

```
[Application startup / AnimaInputPortModule.InitializeAsync]
         │
         ▼
  CrossAnimaRouter.RegisterInputPort(
      animaId, portName, description, handler)
         │  stored in _ports ConcurrentDictionary

  [Later — LLMModule.ExecuteAsync in any Anima]
         │
         ▼
  CrossAnimaRouter.GetAllRegisteredPorts()
         │  returns List<InputPortDescriptor>
         │  [{animaId, animaName, portName, description}, ...]
         ▼
  BuildSystemPrompt(basePrompt, ports)
         │  appends: "Available services:" block + @@ROUTE format instructions
         ▼
  LLM API call includes service list in system message
         │  LLM can now output @@ROUTE:portName|payload@@ markers
         ▼
  FormatDetector.Parse(response) extracts and dispatches routing calls
```

---

## Component Boundaries

### What Belongs Where

| Concern | Component | Rationale |
|---------|-----------|-----------|
| Cross-Anima message brokering | `CrossAnimaRouter` (singleton) | Must access all Anima runtimes — lives above them alongside AnimaRuntimeManager |
| Service registration | `AnimaInputPortModule.InitializeAsync` | Registration tied to module lifecycle; module owns its registration |
| Request dispatch | `AnimaRouteModule.ExecuteAsync` | Wiring-graph-native call — module is the natural encapsulation unit |
| Response completion | `AnimaOutputPortModule.ExecuteAsync` | Symmetric to input; module owns the completion call |
| Correlation ID map | `CrossAnimaRouter._pending` | Broker owns pending state — not tied to any single Anima |
| Format parsing | `FormatDetector` (static) | Stateless utility; no runtime state required |
| Format interception | `LLMModule.ExecuteAsync` | Post-response processing in the module that produces the text |
| Prompt injection | `LLMModule.BuildSystemPrompt` | System prompt is LLMModule's domain; reads from router |
| HTTP calls | `HttpRequestModule` | Standard IModule pattern; IHttpClientFactory handles socket pooling |
| Timeout enforcement | `CrossAnimaRouter.RouteRequestAsync` | Broker controls the wait — natural home for timeout and cleanup |

### What Must NOT Be Coupled

- `WiringEngine` must not know about `CrossAnimaRouter` — routing is a module-level concern. WiringEngine executes nodes; what they do is opaque.
- Per-Anima `EventBus` instances must remain isolated — `CrossAnimaRouter` bridges them by calling registration handlers directly, never by merging bus instances.
- `AnimaInputPortModule` must not reference calling Anima's runtime — it knows only its own Anima's EventBus.
- `FormatDetector` must not call `CrossAnimaRouter` directly — `LLMModule` orchestrates the pipeline.
- `HttpRequestModule` must not reference routing infrastructure — it is an independent tool node.

---

## Build Order — Dependency Chain

```
Stage 1: Foundation (no module work)
  ├── InputPortDescriptor.cs        (DTO, no dependencies)
  ├── FormatDetectorResult.cs       (DTO, no dependencies)
  ├── FormatDetector.cs             (static, no dependencies)
  └── CrossAnimaRouter.cs           (depends on IAnimaRuntimeManager — already exists)

Stage 2: New Modules (depend on CrossAnimaRouter)
  ├── AnimaInputPortModule.cs       (depends on CrossAnimaRouter, IAnimaModuleConfigService, IAnimaContext)
  ├── AnimaOutputPortModule.cs      (depends on CrossAnimaRouter, IAnimaModuleConfigService)
  ├── AnimaRouteModule.cs           (depends on CrossAnimaRouter, IAnimaModuleConfigService)
  └── HttpRequestModule.cs          (depends on IHttpClientFactory — DI built-in)

Stage 3: LLMModule Modifications (depend on Stage 1)
  ├── Add CrossAnimaRouter? parameter to constructor (nullable — optional injection)
  ├── Add BuildSystemPrompt() helper that calls GetAllRegisteredPorts()
  └── Add FormatDetector.Parse() call after LLM response received

Stage 4: DI and Integration
  ├── AnimaServiceExtensions.cs — add CrossAnimaRouter singleton registration
  ├── Program.cs — add builder.Services.AddHttpClient()
  ├── AnimaRuntimeManager.DeleteAsync — call CrossAnimaRouter.CancelPendingForAnima on delete
  └── End-to-end integration verification
```

**Why this order:**
- `CrossAnimaRouter` has no new dependencies — build it first so modules can reference it
- `FormatDetector` is stateless — build alongside `CrossAnimaRouter` as pure logic
- Port modules depend on `CrossAnimaRouter` being available for injection — build after Stage 1
- `LLMModule` modifications build last; they depend on both `CrossAnimaRouter` (prompt injection) and `FormatDetector` (output parsing) being stable
- DI registration is always last — validates the full injection chain compiles and resolves correctly

---

## Anti-Patterns

### Anti-Pattern 1: Bridging Anima EventBus Instances Directly

**What people do:** Inject both Anima A's EventBus and Anima B's EventBus into a bridging class, subscribe on one and publish to the other.

**Why it's wrong:** Each `EventBus` is created inside `AnimaRuntime` with no DI registration — they are not resolvable from the DI container. Retrieving them requires going through `AnimaRuntimeManager.GetRuntime(id).EventBus`. This creates tight coupling and makes it easy to introduce cross-Anima event leakage. The v1.5 architecture deliberately prevents this.

**Do this instead:** Use `CrossAnimaRouter`. The registration handler captures the target EventBus via closure when `AnimaInputPortModule` registers — only the module that owns the EventBus touches it.

### Anti-Pattern 2: Making AnimaRouteModule Fire-and-Forget

**What people do:** `AnimaRouteModule.ExecuteAsync` publishes a request event and completes immediately, expecting the response to "arrive later."

**Why it's wrong:** The WiringEngine executes in topological order. If `AnimaRouteModule` completes immediately without waiting for the response, downstream modules (which consume the response) execute in the same tick with empty data. The response arrives after the tick completes — the data is never delivered.

**Do this instead:** `AnimaRouteModule.ExecuteAsync` awaits `CrossAnimaRouter.RouteRequestAsync(...)`. The WiringEngine suspends the current level until the task completes. The tick-lock (`SemaphoreSlim _tickLock` in `HeartbeatLoop`) skips subsequent ticks if this one runs long — acceptable behavior for a cross-Anima call.

### Anti-Pattern 3: Embedding Correlation IDs in EventBus Payload Type

**What people do:** Define a `RoutingEnvelope<T>` wrapper with `CorrelationId` + `Payload` fields and route it through the Anima's internal EventBus. WiringEngine subscriptions see `RoutingEnvelope<string>` instead of `string`.

**Why it's wrong:** `WiringEngine.CreateRoutingSubscription` switches on `PortType.Text` to create `Subscribe<string>`. A new payload type breaks this dispatch. All downstream modules (LLM, TextSplit, TextJoin, etc.) expect `string` — they would break silently. Port type validation would reject connections. This change cascades through the entire port system.

**Do this instead:** Use the correlation ID as a text prefix (`[CORR:{id}]\n{text}`) that only the boundary modules know about, or use a dedicated Trigger wire to carry the correlation ID alongside the text. All intermediate modules remain unchanged.

### Anti-Pattern 4: Creating HttpClient Instances Per Execution

**What people do:** `new HttpClient(...)` inside `HttpRequestModule.ExecuteAsync` for each call.

**Why it's wrong:** `HttpClient` socket exhaustion is a well-documented .NET problem. Creating instances per execution leads to `TIME_WAIT` socket exhaustion under even moderate load. With a 100ms heartbeat firing HTTP requests, exhaustion is reachable within minutes.

**Do this instead:** Inject `IHttpClientFactory` via constructor (registered with `builder.Services.AddHttpClient()`). Call `_factory.CreateClient()` per execution. The factory manages underlying `HttpMessageHandler` pooling transparently.

### Anti-Pattern 5: Long Timeouts That Block the Heartbeat Tick

**What people do:** Set `AnimaRouteModule` timeout to 5+ minutes to handle slow LLM operations.

**Why it's wrong:** `AnimaRouteModule.ExecuteAsync` is awaited inside the WiringEngine, which is awaited by `HeartbeatLoop.ExecuteTickAsync`. The tick-lock guard (`_tickLock.Wait(0)`) skips subsequent ticks while one is running. A multi-minute cross-Anima call suspends the calling Anima's heartbeat entirely — no proactive behavior, no UI updates from that Anima.

**Do this instead:** Keep timeouts short (5–30 seconds). Document that cross-Anima calls block the calling Anima's tick. For long operations, design the target Anima to respond with an acknowledgment immediately, then push results back asynchronously using a separate `AnimaRouteModule` call in the reverse direction.

---

## Scaling Considerations

This is a single-user local application. Concerns are runtime performance within one process.

| Concern | At 5 Animas | At 20+ Animas |
|---------|-------------|---------------|
| Concurrent routing requests | No issue — `ConcurrentDictionary` handles parallel `TaskCompletionSource` entries | Add max-in-flight limit if `_pending` count grows unbounded |
| Prompt injection token cost | Minimal — registered ports are short descriptors | Add per-LLMModule opt-out config (`"disablePromptInjection": "true"`) |
| Cross-Anima latency | Target Anima's LLM call dominates; router overhead is negligible | Same — LLM latency is the only meaningful bottleneck |
| Heartbeat suspension | Calling Anima's tick suspends while awaiting; tick-lock prevents snowball | Accept behavior; document; consider async-over-sync only if it becomes a UX problem |
| Memory from pending requests | Negligible — `TaskCompletionSource<string>` is ~100 bytes | Automatic cleanup in `RouteRequestAsync` finally block and `CancelPendingForAnima` |
| Port registration growth | Global port list for prompt injection grows with more `AnimaInputPortModule` instances | No issue at local-user scale; add pagination if list exceeds ~50 entries |

---

## Sources

- Direct source code analysis: `/home/user/OpenAnima/src/OpenAnima.Core/` — HIGH confidence
- Existing architecture decisions in `.planning/PROJECT.md` — HIGH confidence
- `TaskCompletionSource<T>` for async request-response correlation: established .NET pattern — HIGH confidence
- `IHttpClientFactory` socket pooling: official Microsoft documentation — HIGH confidence
- Correlation ID pattern for request matching: industry standard — HIGH confidence

---

*Architecture research for: OpenAnima v1.6 Cross-Anima Routing and HTTP Request Module*
*Researched: 2026-03-11*
