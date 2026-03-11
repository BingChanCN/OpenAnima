# Technology Stack

**Project:** OpenAnima v1.6 Cross-Anima Routing + HTTP Request Module
**Researched:** 2026-03-11
**Confidence:** HIGH

## Executive Summary

For v1.6's cross-Anima routing and HTTP request module, **only one new NuGet package is needed** (`Microsoft.Extensions.Http.Resilience 8.7.0`). All other capabilities — correlation tracking, in-process messaging, async request-response patterns, JSON serialization — are fully covered by .NET 8's built-in BCL. The cross-Anima routing system requires no external message broker, no new protocol layer, and no serialization library; it is a pure in-process architecture built on `ConcurrentDictionary<Guid, TaskCompletionSource<string>>` + the existing `IEventBus`. The HTTP request module requires `IHttpClientFactory` (built-in via `Microsoft.Extensions.DependencyInjection`) with a resilience pipeline.

**Zero-dependency principle holds:** Cross-Anima routing = pure .NET constructs. HTTP = IHttpClientFactory + one resilience package.

## Context

OpenAnima v1.5 shipped with ~21,155 LOC using:

| Package | Version | Status |
|---------|---------|--------|
| .NET 8.0 | runtime | unchanged |
| Blazor Server + SignalR | 8.0.x | unchanged |
| OpenAI SDK | 2.8.0 | unchanged |
| SharpToken | 2.0.4 | unchanged |
| Markdig | 0.41.3 | unchanged |
| Markdown.ColorCode | 3.0.1 | unchanged |
| System.CommandLine | 2.0.0-beta4 | unchanged |

The existing `EventBus` is a lock-free, `ConcurrentDictionary`-based publish-subscribe system. `AnimaRuntime` gives each Anima its own isolated `EventBus`, `WiringEngine`, and `HeartbeatLoop`. The `AnimaRuntimeManager` singleton holds all `AnimaRuntime` instances, making it the natural coordination point for cross-Anima message delivery.

## Recommended Stack Additions

### New NuGet Package (Only Addition)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Microsoft.Extensions.Http.Resilience | 8.7.0 | HTTP retry/timeout pipeline for HTTP Request module | Official Microsoft package built on Polly v8; replaces deprecated Microsoft.Extensions.Http.Polly; pins to 8.x to stay on .NET 8 SDK train; provides `AddStandardResilienceHandler` which gives retry + circuit-breaker + timeout in one call |

### Built-In Capabilities (No New Packages)

| Capability | .NET 8 Built-In | Why Sufficient |
|-----------|-----------------|----------------|
| Correlation ID tracking | `Guid.NewGuid()` + `ConcurrentDictionary<Guid, TaskCompletionSource<string>>` | BCL, zero allocation overhead, thread-safe, exactly how .NET async RPC is done idiomatically |
| Cross-Anima request-response | `TaskCompletionSource<string>` with `CancellationToken` timeout | Standard .NET pattern for bridging async callbacks to awaitable Tasks; used in SignalR internals, RabbitMQ RPC, etc. |
| In-process message passing | Existing `IEventBus.PublishAsync<T>` + `IEventBus.SendAsync<TResponse>` | EventBus already has `SendAsync` targeting a module by ID; cross-Anima routing extends the same pattern at the `AnimaRuntimeManager` level |
| JSON serialization for routing payloads | `System.Text.Json` (built-in) | Already used throughout the project for config persistence; routing messages are plain `string` payloads so serialization is optional |
| HTTP client lifecycle | `IHttpClientFactory` (built-in via `Microsoft.Extensions.DependencyInjection`) | .NET 8 includes `IHttpClientFactory`; avoids socket exhaustion; handles DNS rotation; already registered via `builder.Services.AddHttpClient()` |
| Regex format detection | `System.Text.RegularExpressions.Regex` (built-in, source-generated) | LLM output routing markers are simple fixed patterns; compiled `Regex` with `[GeneratedRegex]` attribute is zero-allocation at call site |
| Prompt injection | `string.Format` / interpolation + `StringBuilder` | System prompt auto-injection is plain string concatenation; no template engine needed |
| Timeout / cancellation | `CancellationTokenSource` with `CancelAfter(TimeSpan)` | Standard .NET timeout pattern; pairs with `TaskCompletionSource` for clean cancellation of pending cross-Anima calls |

## Cross-Anima Routing Architecture Pattern

The routing system lives **above** the per-Anima `WiringEngine` layer, at the `AnimaRuntimeManager` level. This avoids coupling individual Anima runtimes to each other.

### Correlation ID Request-Response Pattern

```csharp
// CrossAnimaRouter singleton registered alongside AnimaRuntimeManager
public class CrossAnimaRouter
{
    private readonly IAnimaRuntimeManager _manager;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<string>> _pending = new();

    // Called by AnimaRoute module to send a request to another Anima's named service
    public async Task<string> SendRequestAsync(
        string targetAnimaId,
        string servicePortName,
        string payload,
        CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[correlationId] = tcs;

        // Register cancellation: remove pending entry, cancel the TCS
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30)); // configurable timeout
        cts.Token.Register(() =>
        {
            if (_pending.TryRemove(correlationId, out var pendingTcs))
                pendingTcs.TrySetCanceled();
        });

        var targetRuntime = _manager.GetRuntime(targetAnimaId);
        await targetRuntime.EventBus.PublishAsync(new ModuleEvent<CrossAnimaRequest>
        {
            EventName = $"AnimaInputPort.{servicePortName}.request",
            SourceModuleId = "CrossAnimaRouter",
            Payload = new CrossAnimaRequest(correlationId, payload)
        }, ct);

        return await tcs.Task;
    }

    // Called by AnimaOutputPort module when a response is ready
    public void CompleteRequest(Guid correlationId, string response)
    {
        if (_pending.TryRemove(correlationId, out var tcs))
            tcs.TrySetResult(response);
    }
}

public record CrossAnimaRequest(Guid CorrelationId, string Payload);
```

**Why `TaskCompletionSource` with `ConcurrentDictionary`:** This is the idiomatic .NET in-process async RPC pattern. It is used by SignalR's server-to-client call mechanism, RabbitMQ .NET client, and documented by Microsoft for async request-reply. It requires no external dependency, no serialization of correlation state, and integrates cleanly with `CancellationToken`. Confidence: HIGH (multiple authoritative sources confirm this pattern).

### LLM Output Format Detection Pattern

```csharp
// Source-generated regex — zero allocation, compiled at build time
public partial class RoutingPatternDetector
{
    // Matches: [ROUTE:service-name] ... payload ... [/ROUTE]
    [GeneratedRegex(@"\[ROUTE:(?<service>[^\]]+)\](?<payload>[\s\S]*?)\[/ROUTE\]",
        RegexOptions.Singleline)]
    private static partial Regex RouteTagRegex();

    public static IEnumerable<(string Service, string Payload)> FindRoutes(string llmOutput)
    {
        foreach (Match m in RouteTagRegex().Matches(llmOutput))
            yield return (m.Groups["service"].Value, m.Groups["payload"].Value.Trim());
    }
}
```

**Why `[GeneratedRegex]`:** .NET 8 source-generated regex avoids runtime regex compilation overhead and is zero-allocation on match success. The routing format tag is developer-defined and simple enough for regex; no external parser needed. Confidence: HIGH (official .NET docs).

### HTTP Request Module Pattern

```csharp
// Register typed client in Program.cs / DI setup
services.AddHttpClient<HttpRequestModuleClient>()
    .AddStandardResilienceHandler(); // retry + circuit breaker + timeout

// HttpRequestModule uses the typed client
public class HttpRequestModule : IModuleExecutor
{
    private readonly HttpRequestModuleClient _client;
    // ... port declarations, config fields (url, method, headers, body template)

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(_method, _resolvedUrl);
        if (_body != null)
            request.Content = new StringContent(_body, Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.response",
            Payload = responseBody
        }, ct);
    }
}
```

**Why `AddStandardResilienceHandler`:** Provides exponential-backoff retry (3 attempts), circuit breaker (opens after consecutive failures), and per-request timeout (10s default) in one call. Targets HTTP 5xx, 429, and 408. Replaces deprecated `Microsoft.Extensions.Http.Polly`. Confidence: HIGH (official Microsoft docs + NuGet).

## Installation

```bash
# Only new dependency for v1.6
cd src/OpenAnima.Core
dotnet add package Microsoft.Extensions.Http.Resilience --version 8.7.0

# Nothing else — all other capabilities are BCL built-ins
```

## New Types to Create (No External Dependencies)

| Type | Location | Purpose |
|------|----------|---------|
| `CrossAnimaRouter` | `Core/Anima/CrossAnimaRouter.cs` | Singleton coordinator for cross-Anima request-response with correlation tracking |
| `CrossAnimaRequest` | `Core/Anima/CrossAnimaRouter.cs` | Record carrying `(Guid CorrelationId, string Payload)` |
| `AnimaServiceRegistry` | `Core/Anima/AnimaServiceRegistry.cs` | Per-Anima dictionary of named input port services (service name → module ID), queried by `CrossAnimaRouter` for prompt injection |
| `AnimaInputPortModule` | `Core/Modules/AnimaInputPortModule.cs` | Declares a named service; subscribes to `AnimaInputPort.{name}.request`; routes payload into wiring |
| `AnimaOutputPortModule` | `Core/Modules/AnimaOutputPortModule.cs` | Collects wiring output; calls `CrossAnimaRouter.CompleteRequest()` with correlation ID |
| `AnimaRouteModule` | `Core/Modules/AnimaRouteModule.cs` | Selects target Anima + service, calls `CrossAnimaRouter.SendRequestAsync()`, forwards response to output port |
| `RoutingPatternDetector` | `Core/Services/RoutingPatternDetector.cs` | `[GeneratedRegex]` based detection of route tags in LLM output |
| `PromptInjectionService` | `Core/Services/PromptInjectionService.cs` | Queries `AnimaServiceRegistry` to build available-services suffix for system prompt |
| `HttpRequestModule` | `Core/Modules/HttpRequestModule.cs` | Configurable HTTP call module with typed `IHttpClientFactory` client |
| `HttpRequestModuleClient` | `Core/Modules/HttpRequestModule.cs` | Typed client wrapper for `IHttpClientFactory` DI registration |

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| Cross-Anima messaging | `ConcurrentDictionary<Guid, TaskCompletionSource<string>>` | `System.Threading.Channels` | Channels are for one-way producer-consumer flows; TCS is the right primitive for request-response correlation. Channels would add unnecessary complexity — you'd still need TCS to bridge the response back to the caller |
| Cross-Anima messaging | In-process `CrossAnimaRouter` | RabbitMQ / Redis pub-sub / gRPC | External message brokers are out-of-scope per downstream spec ("keep in-process"). Platform is local-first; external brokers add ops complexity and latency for zero benefit |
| HTTP resilience | `Microsoft.Extensions.Http.Resilience 8.7.0` | `Polly` directly | Resilience package is the official Polly wrapper with `IHttpClientFactory` integration and sane defaults. Using Polly directly requires more boilerplate and separate `Polly.Core` version pinning |
| HTTP resilience | `Microsoft.Extensions.Http.Resilience` | `Microsoft.Extensions.Http.Polly` | Polly extension is deprecated as of .NET 8. Use the resilience package |
| Format detection | `[GeneratedRegex]` | Pydantic / structured output libraries | Structured output libs are Python ecosystem. OpenAI SDK structured outputs constrain model-side generation and don't work for output inspection post-generation. The routing tag format is developer-defined; simple regex is correct tool |
| Correlation IDs | `Guid.NewGuid()` (32-char hex) | Short IDs / sequential IDs | Guid guarantees global uniqueness with zero coordination; used by existing Anima ID pattern (`Guid.NewGuid().ToString("N")[..8]`). Full Guid for correlation avoids collision risk under concurrent requests |
| Prompt injection | `string` interpolation in `PromptInjectionService` | Handlebars / Scriban | Template engines are over-engineering for injecting a list of 3-10 service names into a system prompt footer |

## What NOT to Add

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| RabbitMQ / Redis / Azure Service Bus | External broker, requires daemon process, violates local-first constraint | `CrossAnimaRouter` in-process with TCS correlation |
| MediatR | Already replaced by the custom `IEventBus`; adding MediatR creates two competing event systems | Extend existing `IEventBus` / `CrossAnimaRouter` |
| `Microsoft.Extensions.Http.Polly` | Deprecated since .NET 8 | `Microsoft.Extensions.Http.Resilience` |
| Newtonsoft.Json for routing messages | Legacy library, slower; routing payloads are plain strings anyway | `System.Text.Json` (already in use) or plain string passing |
| `System.Threading.Channels` for cross-Anima routing | Channels are producer-consumer, not request-response; would require wrapping with TCS anyway | `TaskCompletionSource<string>` with `ConcurrentDictionary` |
| External template engine (Handlebars, Scriban) | Over-engineering for prompt injection that adds 2-4 lines of service names | `string` interpolation / `StringBuilder` |
| OpenAI Structured Outputs API | Constrains server-side token generation; does not work for inspecting already-generated text in routing triggers | `[GeneratedRegex]` pattern detection on output |

## Integration Points with Existing Architecture

| Existing Component | Integration for v1.6 |
|--------------------|----------------------|
| `AnimaRuntimeManager` | Register `CrossAnimaRouter` alongside it (singleton); `CrossAnimaRouter` calls `_manager.GetRuntime(targetId).EventBus` |
| `IEventBus` (per-Anima) | `AnimaInputPortModule` subscribes to `AnimaInputPort.{name}.request` on its Anima's EventBus; `AnimaOutputPortModule` publishes via its EventBus |
| `IAnimaModuleConfigService` | `AnimaInputPortModule` reads `serviceName` config field; `AnimaRouteModule` reads `targetAnimaId` + `servicePortName` config fields |
| `LLMModule` | `PromptInjectionService` injects available-services list into system prompt before `ChatClient.CompleteChatAsync`; `RoutingPatternDetector` monitors output |
| `WiringEngine` | No change; routing modules plug in as standard modules with Text input/output ports; wiring engine executes them in topological order |
| `EditorConfigSidebar` | Config fields for routing modules use existing `text` field type; no sidebar changes needed |
| `ModuleMetadataRecord` | New modules use the existing `ModuleMetadataRecord` pattern |

## Version Compatibility

| Package | Version | Compatible With | Notes |
|---------|---------|-----------------|-------|
| Microsoft.Extensions.Http.Resilience | 8.7.0 | .NET 8.0 | 8.x series tracks .NET 8 SDK; 10.x requires .NET 10 |
| System.Text.RegularExpressions (BCL) | built-in | .NET 8.0 | `[GeneratedRegex]` attribute available since .NET 7 |
| System.Threading (BCL) | built-in | .NET 8.0 | `TaskCompletionSource`, `ConcurrentDictionary`, `CancellationTokenSource` all .NET 8 |
| IHttpClientFactory | built-in | .NET 8.0 | Available via `Microsoft.Extensions.DependencyInjection` (already a transitive dependency) |

## Confidence Assessment

| Area | Confidence | Rationale |
|------|------------|-----------|
| Cross-Anima routing (TCS + ConcurrentDictionary) | HIGH | Standard .NET in-process RPC pattern documented in official sources; used by SignalR internals |
| IHttpClientFactory + resilience package | HIGH | Official Microsoft documentation; package version confirmed on NuGet |
| `[GeneratedRegex]` for format detection | HIGH | Official .NET 8 docs; available since .NET 7 |
| Prompt injection via string interpolation | HIGH | No API surface uncertainty; pure string operations |
| Correlation ID via `Guid` | HIGH | Idiomatic .NET; matches existing Anima ID convention |
| Zero need for external message broker | HIGH | In-process constraint is explicit in spec; `AnimaRuntimeManager` already holds all runtimes |

## Sources

- [Microsoft Learn — IHttpClientFactory guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory) — IHttpClientFactory lifetime management, HIGH confidence
- [Microsoft Learn — Build resilient HTTP apps](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience) — `AddStandardResilienceHandler` API, HIGH confidence
- [NuGet — Microsoft.Extensions.Http.Resilience](https://www.nuget.org/packages/Microsoft.Extensions.Http.Resilience) — Version 8.7.0 confirmed, HIGH confidence
- [Microsoft Learn — Channels (.NET)](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) — System.Threading.Channels pattern analysis (considered but not selected), HIGH confidence
- [Microsoft Learn — .NET Regular Expressions](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expressions) — GeneratedRegex attribute, HIGH confidence
- [TaskCompletionSource in .NET](https://code-corner.dev/2024/01/19/NET-%E2%80%94-TaskCompletionSource-and-CancellationTokenSource/) — TCS + CancellationTokenSource async RPC pattern, MEDIUM confidence (confirmed by SignalR source and existing IEventBus.SendAsync pattern in this codebase)
- [Gigi Labs — RabbitMQ RPC with TaskCompletionSource](https://gigi.nullneuron.net/gigilabs/abstracting-rabbitmq-rpc-with-taskcompletionsource/) — Correlation dictionary pattern walkthrough, MEDIUM confidence

---
*Stack research for: v1.6 Cross-Anima Routing and HTTP Request Module*
*Researched: 2026-03-11*
*Confidence: HIGH*
