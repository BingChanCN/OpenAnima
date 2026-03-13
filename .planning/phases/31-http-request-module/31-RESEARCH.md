# Phase 31: HTTP Request Module - Research

**Researched:** 2026-03-13
**Domain:** IHttpClientFactory + resilience pipeline, SSRF protection, module pattern
**Confidence:** HIGH

## Summary

Phase 31 adds HttpRequestModule as a first-class wiring node — a configurable HTTP client module that follows the AnimaRouteModule trigger/buffer pattern exactly. The module uses IHttpClientFactory with a named client registered in Program.cs via `AddStandardResilienceHandler`, eliminating socket exhaustion under heartbeat-driven repeated calls. SSRF protection is enforced synchronously before any network call using .NET 8's `IPNetwork.Contains` API (no DNS resolution, pure IP byte check). Timeout is applied with a linked `CancellationTokenSource` combining the heartbeat CancellationToken with a 10-second cap.

The EditorConfigSidebar handles config field rendering by key name — each new key needs a matching `else if (kvp.Key == "...")` branch for fields that need non-text-input rendering (dropdown, textarea). The three new config keys that need special handling are: `method` (dropdown), `headers` (textarea), and `body` (textarea). The `url` key falls through to the default `<input type="text">` branch — no change needed.

**Primary recommendation:** Wire HttpRequestModule strictly from the AnimaRouteModule template. Register a named `"HttpRequest"` client in Program.cs with `AddStandardResilienceHandler()`. Add SSRF check as a static utility class. Add three `else if` branches in EditorConfigSidebar for `method`, `headers`, and `body`.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Configuration Fields**
- URL: Single text field for full URL (no baseUrl + path split)
- HTTP Method: Dropdown menu — GET / POST / PUT / DELETE / PATCH (reuses existing EditorConfigSidebar dropdown support)
- Headers: Multi-line textarea, one header per line in `Key: Value` format (e.g., `Content-Type: application/json`)
- Body: Plain textarea — no {{variable}} template interpolation, sent as-is (curl/fetch philosophy — minimal module)
- Password field type for sensitive headers (e.g., Authorization) is deferred to HTTP-06 (v1.7+)

**Input Ports**
- body (Text): Request body data, buffered until trigger fires. GET requests can leave this unconnected.
- trigger (Trigger): Fires the HTTP request using buffered body data + sidebar config (URL, method, headers)
- URL is sidebar-only — NOT overridable via input port (prevents LLM-injected SSRF via dynamic URL)

**Output Ports**
- body (Text): Response body as string
- statusCode (Text): HTTP status code as string (e.g., "200", "404")
- error (Text): Structured JSON for network-layer errors only
- Port routing logic:
  - HTTP 2xx/3xx/4xx/5xx responses -> body + statusCode ports fire (downstream decides if 4xx/5xx is an error)
  - Network errors (timeout, DNS failure, SSRF block, connection refused) -> error port fires only
  - body/statusCode and error are mutually exclusive per trigger

**Error Output Format**
- Structured JSON matching AnimaRouteModule pattern: `{"error":"Timeout","url":"https://example.com","timeout":10}`
- Error categories: Timeout, SsrfBlocked, ConnectionFailed, DnsResolutionFailed

**SSRF Protection**
- Block before any network call: localhost, 127.x.x.x, ::1, 10.x.x.x, 172.16-31.x.x, 192.168.x.x, 169.254.x.x (link-local)
- URL parsed and host resolved to IP, then checked against blocked ranges
- No DNS rebinding protection (acceptable for v1.6 — single-user local tool)
- Both HTTP and HTTPS allowed (local dev often uses HTTP)

**Resilience**
- Use IHttpClientFactory with Microsoft.Extensions.Http.Resilience standard resilience handler
- Default timeout: 10 seconds (via CancellationToken, passthrough from heartbeat)
- No custom retry policy — rely on standard resilience handler defaults
- New NuGet dependency: Microsoft.Extensions.Http.Resilience 8.7.0

### Claude's Discretion
- Internal HttpRequestModule class structure and helper methods
- SSRF checker implementation (static utility vs injected service)
- Header parsing implementation (string splitting strategy)
- DI registration in WiringServiceExtensions
- Exact resilience handler configuration
- Logging strategy and log levels

### Deferred Ideas (OUT OF SCOPE)
- HTTP-06: First-class auth config fields (Bearer token, Basic auth) — v1.7+
- HTTP-07: HTTP response streaming to downstream modules — v1.7+
- HTTP-08: Full URL allowlist with user-configurable permitted domains — v1.7+
- Password field type for Authorization header — part of HTTP-06
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| HTTP-01 | User can add HttpRequest module with configurable URL, HTTP method, headers, and body template | EditorConfigSidebar key-based rendering: `url` (text), `method` (dropdown), `headers` (textarea), `body` (textarea). Default config initialized in InitializeAsync. |
| HTTP-02 | HttpRequest module uses IHttpClientFactory with resilience pipeline | Named client `"HttpRequest"` registered in Program.cs via `services.AddHttpClient("HttpRequest").AddStandardResilienceHandler()`. Injected into module constructor. |
| HTTP-03 | HttpRequest module outputs response body and status code via separate output ports | Two output ports: `body` (Text) and `statusCode` (Text). Both published on HTTP success (any status code). Error port is mutually exclusive. |
| HTTP-04 | HttpRequest module enforces 10s default timeout with heartbeat CancellationToken passthrough | `CancellationTokenSource.CreateLinkedTokenSource(heartbeatCt, timeoutCts.Token)` — 10s cap applied per-request, linked to incoming heartbeat token. |
| HTTP-05 | HttpRequest module blocks requests to localhost and private IP ranges | `SsrfGuard.IsBlocked(url)` — static class using `IPNetwork.Contains` (.NET 8 built-in). Called before `CreateClient()`. Covers 127.x, ::1, 10.x, 172.16-31.x, 192.168.x, 169.254.x. |
</phase_requirements>

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.Extensions.Http.Resilience | 8.7.0 | Standard resilience pipeline for HttpClient | User-locked. Provides retry + circuit breaker + timeout via `AddStandardResilienceHandler()` |
| System.Net.Http (built-in) | net8.0 | HttpClient, HttpRequestMessage | Platform built-in via IHttpClientFactory |
| System.Net (built-in) | net8.0 | IPAddress, IPNetwork, Dns.GetHostAddresses | Platform built-in; `IPNetwork` added in .NET 8 |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Text.Json (built-in) | net8.0 | Structured JSON error serialization | Already used by AnimaRouteModule for `PublishErrorAsync` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `IPNetwork.Parse(...).Contains(addr)` | Custom byte-masking | `IPNetwork` is .NET 8 built-in — use it; no custom CIDR math needed |
| Named HttpClient | Typed client | Typed clients cannot be injected into singletons safely; named client + `CreateClient()` per-request is the correct pattern |

**Installation:**
```bash
# In OpenAnima.Core.csproj
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.7.0" />
```

---

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Modules/
│   └── HttpRequestModule.cs        # New module
├── Http/
│   └── SsrfGuard.cs                # SSRF IP check utility (static or internal)
├── DependencyInjection/
│   └── WiringServiceExtensions.cs  # Add HttpRequestModule singleton
├── Components/Shared/
│   └── EditorConfigSidebar.razor   # Add else-if branches for method/headers/body
└── Program.cs                      # Register named HttpClient with resilience handler
```

### Pattern 1: AnimaRouteModule Body+Trigger Pattern (apply verbatim)

**What:** Subscribe to `body` input port to buffer payload. Subscribe to `trigger` input port to kick off HTTP call. On completion, publish to output ports. body/statusCode and error are mutually exclusive per trigger.

**When to use:** All trigger-driven modules that buffer data from an input port before acting.

**Example (from AnimaRouteModule.cs, adapted for HttpRequestModule):**
```csharp
// Source: src/OpenAnima.Core/Modules/AnimaRouteModule.cs
var bodySub = _eventBus.Subscribe<string>(
    $"{Metadata.Name}.port.body",
    (evt, ct) =>
    {
        _lastBodyPayload = evt.Payload;
        return Task.CompletedTask;
    });

var triggerSub = _eventBus.Subscribe<string>(
    $"{Metadata.Name}.port.trigger",
    async (evt, ct) => await HandleTriggerAsync(ct));
```

### Pattern 2: Named HttpClient + Standard Resilience Handler

**What:** Register a named client in Program.cs (or an extension method). Inject `IHttpClientFactory` into the singleton module. Call `CreateClient("HttpRequest")` per-request inside `HandleTriggerAsync` — never cache the returned `HttpClient`.

**When to use:** Any singleton service that makes HTTP calls.

**Example:**
```csharp
// Source: Microsoft.Extensions.Http.Resilience docs (verified)
// In Program.cs / AddWiringServices extension
services.AddHttpClient("HttpRequest")
    .AddStandardResilienceHandler();
```

```csharp
// In HttpRequestModule constructor
public HttpRequestModule(
    IEventBus eventBus,
    IHttpClientFactory httpClientFactory,
    IAnimaModuleConfigService configService,
    IAnimaContext animaContext,
    ILogger<HttpRequestModule> logger)
```

```csharp
// In HandleTriggerAsync — create fresh client per request
var client = _httpClientFactory.CreateClient("HttpRequest");
```

### Pattern 3: Linked CancellationToken for 10-Second Timeout

**What:** Create a per-request `CancellationTokenSource` capped at 10 seconds, linked to the incoming heartbeat `ct`. If either fires, the request is cancelled. Check `timeoutCts.IsCancellationRequested` to distinguish timeout from upstream cancellation.

**Example:**
```csharp
// Source: Microsoft.Extensions.Http.Resilience + IHttpClientFactory docs (verified)
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
try
{
    var response = await client.SendAsync(request, linkedCts.Token);
    // ...
}
catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
{
    // Timeout error -> publish to error port
}
catch (OperationCanceledException)
{
    // Heartbeat cancelled -> swallow or re-throw
}
```

### Pattern 4: SSRF Guard Using IPNetwork.Contains (.NET 8)

**What:** Parse the URL's `Host` as an `IPAddress` or resolve it via `Dns.GetHostAddresses`. Check every resolved IP against blocked CIDR ranges using `IPNetwork.Contains`. Also check `IPAddress.IsLoopback` and `addr.IsIPv6LinkLocal`.

**When to use:** Before any outbound HTTP call where the URL is user-supplied or LLM-sourced.

**Example:**
```csharp
// Source: .NET 8 IPNetwork API (verified via WebSearch)
public static class SsrfGuard
{
    private static readonly IPNetwork[] BlockedRanges =
    {
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("169.254.0.0/16"),  // link-local
        IPNetwork.Parse("fc00::/7"),         // IPv6 Unique Local
        IPNetwork.Parse("fe80::/10"),        // IPv6 Link-Local
    };

    public static bool IsBlocked(string url, out string reason)
    {
        reason = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            reason = "InvalidUrl";
            return true;
        }

        // If host is already an IP literal, parse directly
        if (IPAddress.TryParse(uri.Host, out var directIp))
        {
            if (IsPrivateOrLoopback(directIp))
            {
                reason = $"SsrfBlocked:{directIp}";
                return true;
            }
            return false;
        }

        // Resolve hostname -> check all resolved IPs
        try
        {
            var addresses = Dns.GetHostAddresses(uri.Host);
            foreach (var addr in addresses)
            {
                if (IsPrivateOrLoopback(addr))
                {
                    reason = $"SsrfBlocked:{addr}";
                    return true;
                }
            }
        }
        catch (Exception)
        {
            reason = "DnsResolutionFailed";
            return true;
        }
        return false;
    }

    private static bool IsPrivateOrLoopback(IPAddress addr)
    {
        if (IPAddress.IsLoopback(addr)) return true;
        if (addr.IsIPv6LinkLocal) return true;
        return Array.Exists(BlockedRanges, net => net.Contains(addr));
    }
}
```

### Pattern 5: EditorConfigSidebar Key-Based Rendering

**What:** The sidebar renders config values by key name using a cascaded `if/else if` chain. New fields requiring non-text-input rendering need new `else if` branches. The `url` key already falls through to default `<input type="text">` — no change needed.

**Keys requiring new branches:**
- `method` → `<select>` with options GET/POST/PUT/DELETE/PATCH
- `headers` → `<textarea rows="4">`
- `body` → `<textarea rows="6">`

**Example (follows existing textarea pattern):**
```razor
// Source: src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor
else if (kvp.Key == "method")
{
    <select value="@kvp.Value"
            @onchange="@(e => HandleConfigChanged(kvp.Key, e.Value?.ToString() ?? "GET"))">
        @foreach (var m in new[] { "GET", "POST", "PUT", "DELETE", "PATCH" })
        {
            <option value="@m" selected="@(kvp.Value == m)">@m</option>
        }
    </select>
}
else if (kvp.Key == "headers")
{
    <textarea rows="4"
              @oninput="@(e => HandleConfigChanged(kvp.Key, e.Value?.ToString() ?? string.Empty))">@kvp.Value</textarea>
}
else if (kvp.Key == "body")
{
    <textarea rows="6"
              @oninput="@(e => HandleConfigChanged(kvp.Key, e.Value?.ToString() ?? string.Empty))">@kvp.Value</textarea>
}
```

### Anti-Patterns to Avoid

- **Caching `HttpClient` from factory in a field:** `_httpClientFactory.CreateClient(...)` must be called inside `HandleTriggerAsync`, not stored in `_httpClient` at construction time — defeats socket recycling.
- **Raw `new HttpClient()`:** Never instantiate directly; use IHttpClientFactory. Socket exhaustion under heartbeat-driven repeated calls is the exact failure mode.
- **SSRF check after `CreateClient`:** The SSRF guard MUST run before calling `CreateClient` or sending any request.
- **Catching only `TaskCanceledException` for timeout:** Use `OperationCanceledException` (the base type) with `.when (timeoutCts.IsCancellationRequested)` to distinguish timeout from heartbeat cancellation.
- **Empty `url` not guarded:** If `url` config is empty/missing, publish to error port immediately — do not proceed to HTTP call.
- **Validation error on `body` key:** The existing `HandleConfigChanged` validation rejects empty values for all keys except `template`. The `body` and `headers` keys should be allowed to be empty (GET requests have no body). Add `body` and `headers` to the empty-allowed list alongside `template`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Socket exhaustion | Custom HttpClient pool | `IHttpClientFactory` + named client | Handler lifetime management, DNS refresh, socket recycling — all built-in |
| Retry + circuit breaker | Custom Polly pipeline | `AddStandardResilienceHandler()` | Standard handler ships with retry, circuit breaker, attempt timeout, total timeout pre-configured |
| CIDR range math | Custom byte-masking | `IPNetwork.Parse(...).Contains(addr)` | `IPNetwork` is .NET 8 built-in — exact semantics, no edge cases |
| Loopback detection | Custom 127.x check | `IPAddress.IsLoopback(addr)` | Handles both IPv4 loopback range and `::1` |
| IPv6 link-local | Custom fe80:: prefix check | `addr.IsIPv6LinkLocal` | Built-in property, handles `fe80::/10` prefix |

**Key insight:** The SSRF blocking, socket management, and resilience problems are all solved by .NET 8 built-ins + the resilience NuGet package. There is nothing to build from scratch.

---

## Common Pitfalls

### Pitfall 1: Caching HttpClient from IHttpClientFactory
**What goes wrong:** Module stores `_httpClient = _httpClientFactory.CreateClient("HttpRequest")` in a field at construction time. The underlying `HttpMessageHandler` never rotates; DNS changes are ignored; socket exhaustion risk remains.
**Why it happens:** Looks like you're avoiding repeated factory calls.
**How to avoid:** Call `_httpClientFactory.CreateClient("HttpRequest")` inside `HandleTriggerAsync`. The factory itself is a singleton; clients are cheap to create.
**Warning signs:** `_httpClient` field with type `HttpClient` on a singleton module.

### Pitfall 2: SSRF Check on Hostname Only (No IP Parsing)
**What goes wrong:** Checking `url.Contains("localhost")` or `host == "127.0.0.1"` is bypassable with `http://127.1/`, `http://[::1]/`, or `http://2130706433/` (decimal IP).
**Why it happens:** String matching feels sufficient.
**How to avoid:** Always call `IPAddress.TryParse(uri.Host, ...)` first, and fall back to `Dns.GetHostAddresses` for hostnames. Use `IPNetwork.Contains` for range checks.
**Warning signs:** Any string-based SSRF check.

### Pitfall 3: Validation Rejecting Empty `body` and `headers`
**What goes wrong:** The existing `HandleConfigChanged` in EditorConfigSidebar rejects empty values for all keys except `template`. If `body` and `headers` are subject to the same rule, GET requests (which have no body) trigger a validation error and the config won't save.
**Why it happens:** The sidebar's validation logic is key-agnostic.
**How to avoid:** Add `"body"` and `"headers"` to the list of keys that allow empty values, alongside `"template"`.
**Warning signs:** User cannot save config for a GET request (body field empty).

### Pitfall 4: Timeout CTS Started Too Early
**What goes wrong:** `new CancellationTokenSource(TimeSpan.FromSeconds(10))` is created before `SendAsync` is called. If header parsing or SSRF check takes >10 seconds (unlikely but possible), the token expires before the HTTP call begins.
**Why it happens:** The timer starts at CTS construction, not at `SendAsync`.
**How to avoid:** Create the `CancellationTokenSource` immediately before calling `SendAsync`. Do SSRF check before creating the CTS.
**Warning signs:** Timeout triggered on requests that never hit the network.

### Pitfall 5: Header Parsing Edge Cases
**What goes wrong:** Header line `Authorization: Bearer abc:def` splits on first `:` — gives `Authorization` and ` Bearer abc:def`. But splitting on ALL `:` gives `Authorization`, ` Bearer abc`, `def` (wrong).
**Why it happens:** `String.Split(':')` splits on every colon; auth tokens can contain colons.
**How to avoid:** Split on `": "` (colon + space, case-insensitive) or use `IndexOf(':')` to get only the first colon. Skip lines that don't contain `:`.
**Warning signs:** Auth headers corrupted when token contains colons.

### Pitfall 6: Two Output Ports Publishing in Same Trigger
**What goes wrong:** On HTTP 4xx/5xx, code publishes both `body`+`statusCode` AND `error`. Downstream modules receive conflicting signals.
**Why it happens:** Confusion between "HTTP error response" (4xx/5xx — valid response) and "network error" (no response at all).
**How to avoid:** Follow the port routing rule strictly: any HTTP response (even 500) -> `body` + `statusCode` only. Only `OperationCanceledException`, `HttpRequestException`, `SsrfBlocked` -> `error` port.
**Warning signs:** `error` port published for a 404 response.

---

## Code Examples

Verified patterns from official sources or codebase:

### Module Port Declaration
```csharp
// Source: AnimaRouteModule.cs pattern (adapted)
[InputPort("body", PortType.Text)]
[InputPort("trigger", PortType.Trigger)]
[OutputPort("body", PortType.Text)]
[OutputPort("statusCode", PortType.Text)]
[OutputPort("error", PortType.Text)]
public class HttpRequestModule : IModuleExecutor
```

### Default Config Initialization (InitializeAsync)
```csharp
// Source: AnimaRouteModule.cs pattern
if (animaId != null)
{
    var existing = _configService.GetConfig(animaId, Metadata.Name);
    if (existing.Count == 0)
    {
        _ = _configService.SetConfigAsync(animaId, Metadata.Name,
            new Dictionary<string, string>
            {
                ["url"]     = "",
                ["method"]  = "GET",
                ["headers"] = "",
                ["body"]    = ""
            });
    }
}
```

### Structured Error Publish (matches AnimaRouteModule pattern)
```csharp
// Source: AnimaRouteModule.cs PublishErrorAsync pattern
private async Task PublishErrorAsync(object errorObj, CancellationToken ct)
{
    var json = JsonSerializer.Serialize(errorObj);
    await _eventBus.PublishAsync(new ModuleEvent<string>
    {
        EventName = $"{Metadata.Name}.port.error",
        SourceModuleId = Metadata.Name,
        Payload = json
    }, ct);
}
```

### Header Parsing (colon-safe)
```csharp
// Planner-recommended pattern (verified logic)
private static IEnumerable<(string Name, string Value)> ParseHeaders(string raw)
{
    if (string.IsNullOrWhiteSpace(raw)) yield break;
    foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
        var trimmed = line.Trim();
        var colonIdx = trimmed.IndexOf(':');
        if (colonIdx <= 0) continue;
        var name = trimmed[..colonIdx].Trim();
        var value = trimmed[(colonIdx + 1)..].Trim();
        if (!string.IsNullOrEmpty(name))
            yield return (name, value);
    }
}
```

### NuGet Registration
```xml
<!-- Source: NuGet Gallery — verified -->
<!-- In OpenAnima.Core.csproj -->
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.7.0" />
```

### DI Registration (Program.cs)
```csharp
// Source: Microsoft.Extensions.Http.Resilience docs (verified)
// In Program.cs, after builder.Services.AddWiringServices()
builder.Services.AddHttpClient("HttpRequest")
    .AddStandardResilienceHandler();
```

### DI Registration (WiringServiceExtensions)
```csharp
// Source: WiringServiceExtensions.cs pattern
services.AddSingleton<HttpRequestModule>();
```

### WiringInitializationService Arrays
```csharp
// Source: WiringInitializationService.cs pattern
// Add to both PortRegistrationTypes and AutoInitModuleTypes
typeof(HttpRequestModule),
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `new HttpClient()` | `IHttpClientFactory.CreateClient()` | .NET Core 2.1 | Socket exhaustion eliminated; DNS rotation ensured |
| Custom Polly pipeline | `AddStandardResilienceHandler()` | .NET 8 / Microsoft.Extensions.Http.Resilience | Pre-configured retry + circuit breaker + timeout; no manual configuration |
| Custom CIDR math | `IPNetwork.Parse(...).Contains(addr)` | .NET 8 | First-class CIDR support; eliminates custom bit-masking |

**Current as of:** 2026-03-13

---

## Open Questions

1. **`AddHttpClient` placement: Program.cs vs WiringServiceExtensions**
   - What we know: `AddHttpClient` must be called on `IServiceCollection` before `BuildServiceProvider`. Both Program.cs and AddWiringServices have access.
   - What's unclear: Whether to put it in `AddWiringServices()` (cohesion) or Program.cs (explicit, consistent with other HTTP client registrations).
   - Recommendation: Add inside `AddWiringServices()` for cohesion — keeps all wiring-related registrations together. This is Claude's discretion per CONTEXT.md.

2. **`AddStandardResilienceHandler` retry behavior on POST requests**
   - What we know: Standard handler retries on transient failures. POST is not idempotent — retrying a POST can cause duplicate operations on the target server.
   - What's unclear: Whether the user's use case involves non-idempotent POST calls that should not be retried.
   - Recommendation: Accept default for v1.6 (CONTEXT.md says "no custom retry policy"). Document as known behavior.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | none (inferred from .csproj) |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --filter "Category=HttpRequest" -x` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/ -x` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| HTTP-01 | Module initializes with default config (url/method/headers/body) | Unit | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |
| HTTP-01 | Sidebar dropdown for method renders GET/POST/PUT/DELETE/PATCH | Manual (Blazor UI) | n/a — visual check | n/a |
| HTTP-02 | Module uses IHttpClientFactory (not raw HttpClient) | Unit/Integration | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |
| HTTP-03 | Successful HTTP response publishes body + statusCode ports | Integration | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |
| HTTP-03 | body and error ports are mutually exclusive on HTTP success | Integration | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |
| HTTP-04 | Request exceeding 10s fires timeout -> error port with Timeout JSON | Integration | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |
| HTTP-04 | Upstream CancellationToken cancellation is respected | Unit | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |
| HTTP-05 | localhost URL is blocked before any network call | Unit | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |
| HTTP-05 | 10.x.x.x URL is blocked | Unit | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |
| HTTP-05 | 172.16.x.x URL is blocked | Unit | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |
| HTTP-05 | 192.168.x.x URL is blocked | Unit | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |
| HTTP-05 | 169.254.x.x URL is blocked | Unit | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |
| HTTP-05 | ::1 (IPv6 loopback) URL is blocked | Unit | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |
| HTTP-05 | Public IP (e.g., 1.1.1.1) passes SSRF check | Unit | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |
| HTTP-05 | SSRF-blocked request -> error port fires (not network call) | Unit | `dotnet test --filter "Category=HttpRequest" -x` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ --filter "Category=HttpRequest" -x`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/ -x`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Modules/HttpRequestModuleTests.cs` — covers HTTP-01, HTTP-02, HTTP-03, HTTP-04, HTTP-05 (unit tests with fake HttpMessageHandler)
- [ ] `tests/OpenAnima.Tests/Unit/SsrfGuardTests.cs` — covers HTTP-05 IP range blocking logic in isolation

*(No framework install needed — xunit already present)*

---

## Sources

### Primary (HIGH confidence)
- NuGet Gallery — Microsoft.Extensions.Http.Resilience 8.7.0 (verified package exists at exact version)
- .NET 8 `IPNetwork` API — verified via WebSearch cross-referencing dotnet/docs
- `IPAddress.IsLoopback`, `IsIPv6LinkLocal` — .NET built-in, HIGH confidence
- `src/OpenAnima.Core/Modules/AnimaRouteModule.cs` — AnimaRouteModule pattern, read directly
- `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor` — sidebar field rendering, read directly
- `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` — DI pattern, read directly
- `src/OpenAnima.Core/Hosting/WiringInitializationService.cs` — port registration pattern, read directly

### Secondary (MEDIUM confidence)
- `AddStandardResilienceHandler` API shape and behavior — documented via WebSearch + Microsoft Learn URL confirmed
- `CancellationTokenSource.CreateLinkedTokenSource` timeout pattern — verified via multiple official .NET sources
- Header parsing colon-safety issue — engineering reasoning, well-known edge case

### Tertiary (LOW confidence)
- `AddHttpClient` placement in `AddWiringServices` vs `Program.cs` — judgment call, not tested in codebase yet

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — package exists at 8.7.0, IHttpClientFactory pattern is well-established .NET
- Architecture: HIGH — directly mirrors AnimaRouteModule which is already proven in the codebase
- Pitfalls: HIGH (SSRF, socket exhaustion, timeout), MEDIUM (validation empty-allowed keys)
- SSRF implementation: HIGH — `IPNetwork.Contains` is .NET 8 built-in, verified

**Research date:** 2026-03-13
**Valid until:** 2026-06-13 (stable APIs — 90 days is safe)
