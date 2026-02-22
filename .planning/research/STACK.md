# Stack Research

**Domain:** Blazor Server WebUI Runtime Monitoring Dashboard (v1.1 milestone additions)
**Researched:** 2026-02-22
**Confidence:** HIGH

## Context

This research focuses ONLY on stack additions needed for v1.1 WebUI dashboard milestone. The existing v1.0 runtime already has:
- .NET 8.0 runtime ✓
- AssemblyLoadContext module isolation ✓
- ConcurrentDictionary-based module registry ✓
- MediatR event bus ✓
- PeriodicTimer heartbeat loop ✓
- FileSystemWatcher module discovery ✓

**What's NEW in v1.1:** Blazor Server WebUI for real-time runtime monitoring and control.

## Recommended Stack Additions

### Core Technologies (NEW)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| ASP.NET Core Web SDK | 8.0 (built-in) | Web host for Blazor Server | Already in .NET 8 runtime, change project SDK to Microsoft.NET.Sdk.Web, Kestrel built-in for self-hosting |
| Blazor Server | 8.0 (built-in) | Real-time UI framework | SignalR built-in for push updates, pure C# full-stack, seamless integration with existing runtime, no separate API layer needed |
| Microsoft.Extensions.Hosting.WindowsServices | 10.0.3 | Windows Service hosting | Enables background service with UseWindowsService(), compatible with .NET 8 runtime, forward-compatible packages |

### Supporting Libraries (NEW)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| (none required) | - | Blazor Server includes SignalR | All real-time push capabilities built-in to ASP.NET Core 8.0 |

### Existing Stack (NO CHANGES)

| Technology | Version | Status |
|------------|---------|--------|
| .NET Runtime | 8.0 | ✓ Keep (LTS until Nov 2026) |
| MediatR | 12.* | ✓ Keep (already integrated with EventBus) |
| Microsoft.Extensions.Logging | 10.0.3 | ✓ Keep (already in use) |
| AssemblyLoadContext | Built-in | ✓ Keep (module isolation working) |
| PeriodicTimer | Built-in | ✓ Keep (heartbeat loop working) |
| FileSystemWatcher | Built-in | ✓ Keep (module discovery working) |

## Integration Points

| Component | Integration Method | Notes |
|-----------|-------------------|-------|
| Existing ModuleRegistry | Direct reference from Blazor components | ConcurrentDictionary is thread-safe, read directly from UI thread |
| Existing EventBus | Subscribe in Blazor component lifecycle | Use InvokeAsync(StateHasChanged) to push updates to browser |
| Existing HeartbeatLoop | Expose tick events via EventBus | Publish HeartbeatTickEvent, subscribe in dashboard component |
| Browser auto-launch | Process.Start with UseShellExecute=true | Launch default browser on service start, cross-platform pattern |

## Installation

```bash
# 1. Change OpenAnima.Core project SDK (edit .csproj)
# FROM: <Project Sdk="Microsoft.NET.Sdk">
# TO:   <Project Sdk="Microsoft.NET.Sdk.Web">

# 2. Add Windows Service hosting to OpenAnima.Core
dotnet add src/OpenAnima.Core/OpenAnima.Core.csproj package Microsoft.Extensions.Hosting.WindowsServices --version 10.0.3

# 3. No other packages needed - Blazor Server is built into ASP.NET Core 8.0
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| Blazor Server | Blazor WebAssembly | If offline-first or static hosting required (not needed for local runtime dashboard) |
| Blazor Server | ASP.NET Core MVC + SignalR | If team unfamiliar with Blazor (adds complexity, separate API layer needed) |
| Built-in SignalR | External WebSocket library | Never for Blazor Server (SignalR is the native transport) |
| Windows Service | Console app | Development only (service provides proper background hosting) |
| Process.Start | Electron/WebView2 | If embedded browser required (adds 100+ MB, unnecessary for dashboard) |
| In-process hosting | Separate WebUI process | If runtime crashes shouldn't affect UI (adds IPC complexity, not needed for v1.1) |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Blazor WebAssembly | Requires separate API layer, no direct access to runtime objects, larger payload | Blazor Server (direct C# access to ModuleRegistry/EventBus) |
| SignalR client library | Already included in Blazor Server, redundant | Built-in Blazor Server SignalR transport |
| IHostedService for web host | Mixing concerns, complicates lifecycle | Separate WebApplication.CreateBuilder() with UseWindowsService() |
| Timer-based polling | Inefficient, 100ms heartbeat would spam browser | Event-driven push via InvokeAsync(StateHasChanged) |
| Third-party real-time libraries (Socket.IO, etc.) | Incompatible with Blazor Server transport | Built-in SignalR (automatic reconnection, backpressure) |
| Separate API project | Unnecessary serialization, latency, complexity | Direct method calls from Blazor components to runtime services |
| .NET 9 upgrade | Unnecessary risk, .NET 8 LTS supported until Nov 2026 | Stay on .NET 8 (validated, stable) |

## Stack Patterns by Variant

**For real-time updates from background services:**
- Subscribe to EventBus in Blazor component OnInitializedAsync
- Use InvokeAsync(StateHasChanged) to marshal updates to UI thread
- Unsubscribe in IDisposable.Dispose to prevent memory leaks
- Because Blazor components run on SignalR circuit thread, background events need marshaling

**For module control operations:**
- Call ModuleRegistry methods directly from button click handlers
- No async needed if operations are synchronous (ConcurrentDictionary is thread-safe)
- Return operation results immediately to UI
- Because Blazor Server runs in-process, no API layer or serialization needed

**For Windows Service hosting:**
- Use WebApplication.CreateBuilder() not Host.CreateDefaultBuilder()
- Call builder.Services.AddWindowsService() before Build()
- Launch browser after app.RunAsync() starts (non-blocking)
- Because ASP.NET Core 8.0 uses WebApplicationBuilder pattern, not generic host

**For browser auto-launch:**
```csharp
var psi = new ProcessStartInfo
{
    FileName = "http://localhost:5000",
    UseShellExecute = true
};
Process.Start(psi);
```
- UseShellExecute=true launches default browser
- Works cross-platform (Windows, Linux, macOS)
- Non-blocking, fire-and-forget

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| ASP.NET Core 8.0 | .NET 8.0 runtime | Already present, no additional install |
| Microsoft.Extensions.Hosting.WindowsServices 10.0.3 | .NET 8.0 | Forward-compatible, uses .NET 10 packages but targets netstandard2.1 |
| MediatR 12.* | ASP.NET Core 8.0 DI | Already in use, compatible with Blazor Server DI |
| Blazor Server 8.0 | SignalR 8.0 | Both built into ASP.NET Core 8.0, version-matched |

## Architecture Notes

### Why Blazor Server (not WebAssembly)

1. **Direct runtime access**: Components can reference ModuleRegistry/EventBus directly, no API layer
2. **Real-time push built-in**: SignalR transport handles reconnection, backpressure automatically
3. **Zero serialization**: C# objects stay in memory, no JSON marshaling for every update
4. **Smaller payload**: ~500KB initial load vs 2-3MB for WebAssembly
5. **Instant updates**: StateHasChanged pushes DOM diffs over SignalR, no polling

### Why NOT separate API layer

- Blazor Server runs in same process as runtime
- Components can call ModuleRegistry.GetAll() directly
- EventBus subscriptions push updates via InvokeAsync(StateHasChanged)
- Adding REST/gRPC API adds latency, serialization overhead, complexity
- Only needed if: remote access required (not in v1.1 scope)

### Integration Pattern

```
HeartbeatLoop (PeriodicTimer)
  └─> Publishes HeartbeatTickEvent via EventBus
       └─> Dashboard.razor subscribes in OnInitializedAsync
            └─> Calls InvokeAsync(StateHasChanged) on event
                 └─> SignalR pushes DOM diff to browser
```

No polling, no timers in UI, event-driven push from 100ms heartbeat.

### Project Structure

```
OpenAnima.Core/
  ├─ Program.cs (add WebApplication setup)
  ├─ Pages/ (Blazor components)
  │   ├─ Dashboard.razor
  │   ├─ Modules.razor
  │   └─ Heartbeat.razor
  ├─ wwwroot/ (static assets)
  └─ _Imports.razor (Blazor using statements)
```

Single project, no separate WebUI project needed. Blazor Server runs in same process as runtime.

## Performance Considerations

| Concern | Solution | Rationale |
|---------|----------|-----------|
| SignalR circuit memory | Dispose subscriptions in IDisposable.Dispose | Prevents memory leaks when browser disconnects |
| Heartbeat spam | Only call StateHasChanged when data changes | SignalR has built-in backpressure, but avoid unnecessary renders |
| Module list updates | Use @key directive in @foreach loops | Blazor diffs by key, avoids re-rendering unchanged items |
| Browser reconnection | Built-in SignalR reconnection UI | No custom code needed, automatic reconnection after network issues |

## Sources

- [Use ASP.NET Core SignalR with Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor?view=aspnetcore-10.0) — Blazor Server SignalR integration patterns (MEDIUM confidence, WebFetch blocked but URL verified)
- [Host ASP.NET Core in a Windows Service](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0) — Windows Service hosting with UseWindowsService() (MEDIUM confidence, WebFetch blocked but URL verified)
- [Microsoft.Extensions.Hosting.WindowsServices 10.0.3](https://www.nuget.org/packages/Microsoft.Extensions.Hosting.WindowsServices) — NuGet package page (HIGH confidence, official source)
- [Thread safety using InvokeAsync](https://blazor-university.com/components/multi-threaded-rendering/invokeasync) — InvokeAsync pattern for background thread updates (MEDIUM confidence, community resource)
- [Pushing UI changes from Blazor Server to browser on server raised events](https://swimburger.net/blog/dotnet/pushing-ui-changes-from-blazor-server-to-browser-on-server-raised-events) — Event-driven push pattern (MEDIUM confidence, blog post)
- [StateHasChanged() vs InvokeAsync(StateHasChanged) in Blazor](https://stackoverflow.com/questions/65230621/statehaschanged-vs-invokeasyncstatehaschanged-in-blazor) — Thread safety explanation (MEDIUM confidence, Stack Overflow)
- [Process.Start for URLs on .NET Core](https://brockallen.com/2016/09/24/process-start-for-urls-on-net-core/) — Cross-platform browser launch pattern (MEDIUM confidence, blog post)
- Training data on .NET 8 Blazor Server architecture — Built-in SignalR, component lifecycle, Windows Service hosting (HIGH confidence, core .NET features)

**Confidence assessment:**
- Blazor Server integration: HIGH (core ASP.NET Core 8.0 feature, well-documented)
- Windows Service hosting: HIGH (standard .NET pattern, official package)
- InvokeAsync pattern: HIGH (documented Blazor thread safety requirement)
- Browser auto-launch: MEDIUM (community pattern, works but not officially documented)
- Version numbers: HIGH (verified against existing OpenAnima.Core.csproj, NuGet.org)

---
*Stack research for: OpenAnima v1.1 WebUI Runtime Monitoring Dashboard*
*Researched: 2026-02-22*
*Focus: Minimal additions to existing .NET 8 runtime for Blazor Server dashboard*
