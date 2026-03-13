# Phase 31: HTTP Request Module - Context

**Gathered:** 2026-03-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can add an HttpRequest module to any Anima's wiring graph to make configurable HTTP calls — like curl/fetch as a wiring node. Built-in resilience pipeline (IHttpClientFactory), 10s default timeout, and SSRF private IP blocking from day one. No auth UI, no streaming, no URL allowlist — keep it minimal.

</domain>

<decisions>
## Implementation Decisions

### Configuration Fields
- **URL**: Single text field for full URL (no baseUrl + path split)
- **HTTP Method**: Dropdown menu — GET / POST / PUT / DELETE / PATCH (reuses existing EditorConfigSidebar dropdown support)
- **Headers**: Multi-line textarea, one header per line in `Key: Value` format (e.g., `Content-Type: application/json`)
- **Body**: Plain textarea — no {{variable}} template interpolation, sent as-is (curl/fetch philosophy — minimal module)
- Password field type for sensitive headers (e.g., Authorization) is deferred to HTTP-06 (v1.7+)

### Input Ports
- **body** (Text): Request body data, buffered until trigger fires. GET requests can leave this unconnected.
- **trigger** (Trigger): Fires the HTTP request using buffered body data + sidebar config (URL, method, headers)
- URL is sidebar-only — NOT overridable via input port (prevents LLM-injected SSRF via dynamic URL)

### Output Ports
- **body** (Text): Response body as string
- **statusCode** (Text): HTTP status code as string (e.g., "200", "404")
- **error** (Text): Structured JSON for network-layer errors only
- **Port routing logic**:
  - HTTP 2xx/3xx/4xx/5xx responses → body + statusCode ports fire (downstream decides if 4xx/5xx is an error)
  - Network errors (timeout, DNS failure, SSRF block, connection refused) → error port fires only
  - body/statusCode and error are mutually exclusive per trigger

### Error Output Format
- Structured JSON matching AnimaRouteModule pattern: `{"error":"Timeout","url":"https://example.com","timeout":10}`
- Error categories: Timeout, SsrfBlocked, ConnectionFailed, DnsResolutionFailed

### SSRF Protection
- Block before any network call: localhost, 127.x.x.x, ::1, 10.x.x.x, 172.16-31.x.x, 192.168.x.x, 169.254.x.x (link-local)
- URL parsed and host resolved to IP, then checked against blocked ranges
- No DNS rebinding protection (acceptable for v1.6 — single-user local tool)
- Both HTTP and HTTPS allowed (local dev often uses HTTP)

### Resilience
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

</decisions>

<specifics>
## Specific Ideas

- "This module just needs to do the basics — like fetch and curl" — keep it minimal, no template engine, no fancy features
- Follow AnimaRouteModule pattern: body input buffered, trigger fires execution, mutually exclusive output ports

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `AnimaRouteModule` (src/OpenAnima.Core/Modules/AnimaRouteModule.cs): Reference for body+trigger input pattern, mutually exclusive output ports, structured JSON error output
- `FixedTextModule` (src/OpenAnima.Core/Modules/FixedTextModule.cs): Reference for basic module with config reading from IAnimaModuleConfigService
- `EditorConfigSidebar`: Already supports text, textarea, password, dropdown field types — all needed for HttpRequestModule config
- `IAnimaModuleConfigService`: Existing key-value config persistence per Anima per module

### Established Patterns
- Module implements `IModuleExecutor` with InitializeAsync/ExecuteAsync/ShutdownAsync lifecycle
- Port declarations via `[InputPort]`/`[OutputPort]` attributes
- Event naming: `{ModuleName}.port.{portName}`
- Config via `_configService.GetConfig(animaId, Metadata.Name)` returns `Dictionary<string, string>`
- Default config initialization in `InitializeAsync` (AnimaRouteModule pattern)
- Structured JSON error via `JsonSerializer.Serialize(anonymousObject)`
- Singleton DI registration in `WiringServiceExtensions.AddWiringServices()`

### Integration Points
- `WiringServiceExtensions.AddWiringServices()`: Register HttpRequestModule as singleton
- `WiringInitializationService`: Register port metadata for HttpRequestModule
- `OpenAnima.Core.csproj`: Add Microsoft.Extensions.Http.Resilience 8.7.0 NuGet package
- `Program.cs` or `AnimaServiceExtensions`: Register IHttpClientFactory with resilience handler

</code_context>

<deferred>
## Deferred Ideas

- HTTP-06: First-class auth config fields (Bearer token, Basic auth) — v1.7+
- HTTP-07: HTTP response streaming to downstream modules — v1.7+
- HTTP-08: Full URL allowlist with user-configurable permitted domains — v1.7+
- Password field type for Authorization header — part of HTTP-06

</deferred>

---

*Phase: 31-http-request-module*
*Context gathered: 2026-03-13*
