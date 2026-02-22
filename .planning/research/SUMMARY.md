# Project Research Summary

**Project:** OpenAnima v1.1 WebUI Runtime Dashboard
**Domain:** Real-time monitoring dashboard for modular .NET agent platform
**Researched:** 2026-02-22
**Confidence:** HIGH

## Executive Summary

OpenAnima v1.1 adds a Blazor Server WebUI for real-time monitoring and control of the existing v1.0 modular runtime. The research shows this is a straightforward integration: Blazor Server's built-in SignalR transport provides real-time push updates, and in-process hosting allows direct access to runtime components (ModuleRegistry, EventBus, HeartbeatLoop) without requiring a separate API layer. The recommended approach uses service facades to wrap runtime internals, background services to push state changes via IHubContext, and event-driven updates to avoid polling.

The key architectural decision is keeping the runtime as singleton services while Blazor components use scoped lifetimes per circuit. This maintains the single-instance runtime model while supporting multiple browser connections. The main technical challenge is thread safety: background events from the 100ms heartbeat loop must be marshaled to the UI thread using InvokeAsync(StateHasChanged), and event subscriptions must be properly disposed to prevent memory leaks.

Critical risks center on cross-thread UI updates (causing silent failures or exceptions), heartbeat loop blocking on UI operations (breaking the 100ms tick requirement), and module lifecycle races when load/unload operations occur during active heartbeat ticks. These are all preventable with established Blazor Server patterns: InvokeAsync wrappers, fire-and-forget event publishing, and module state machine coordination. The hosting model transition from console app to WebApplication requires careful service registration to preserve existing v1.0 functionality.

## Key Findings

### Recommended Stack

The v1.1 milestone requires minimal stack additions to the existing .NET 8 runtime. Blazor Server and SignalR are built into ASP.NET Core 8.0, requiring only a project SDK change from Microsoft.NET.Sdk to Microsoft.NET.Sdk.Web. The only new package is Microsoft.Extensions.Hosting.WindowsServices 10.0.3 for background service hosting with auto-launch capabilities.

**Core technologies:**
- **ASP.NET Core 8.0 (built-in)**: Web host for Blazor Server — Kestrel self-hosting, no external dependencies
- **Blazor Server 8.0 (built-in)**: Real-time UI framework — SignalR transport included, pure C# full-stack, seamless integration with existing runtime
- **Microsoft.Extensions.Hosting.WindowsServices 10.0.3**: Windows Service hosting — enables UseWindowsService() for background service with browser auto-launch

**Existing stack (no changes):**
- .NET 8.0 runtime (LTS until Nov 2026)
- MediatR 12.* (EventBus integration)
- AssemblyLoadContext (module isolation)
- PeriodicTimer (heartbeat loop)
- ConcurrentDictionary (thread-safe registry)

The architecture avoids separate API layers, external WebSocket libraries, and Blazor WebAssembly complexity. Blazor Server's in-process model allows components to call ModuleRegistry.GetAll() directly and subscribe to MediatR events, with updates pushed via SignalR's built-in transport.

### Expected Features

**Must have (table stakes):**
- Module list display with status indicators — core purpose, users expect to see loaded modules
- Module metadata display (name, version, description, author) — context for what each module does
- Load/Unload module controls — basic lifecycle management at runtime
- Heartbeat status display (running/stopped) — show if runtime loop is active
- Start/Stop heartbeat controls — users expect to pause/resume the runtime
- Heartbeat tick counter — proof the loop is running, live counter updates
- Real-time updates via SignalR — modern dashboards auto-update, manual refresh feels broken
- Error display — when operations fail, users need to know why
- Responsive layout — works on different screen sizes
- Auto-launch browser on startup — desktop app experience

**Should have (competitive):**
- Heartbeat latency display — performance insight, shows if loop is keeping up with 100ms target
- Event bus activity monitor — real-time visibility into inter-module communication, valuable for debugging
- Module dependency visualization — helps users understand module relationships
- Heartbeat tick history chart — visual trend of loop performance over time
- Dark mode — user preference, reduces eye strain

**Defer (v2+):**
- Module configuration editor — complex, each module has different schema
- Historical data persistence — database complexity, not core to runtime monitoring
- Multi-runtime management — networking complexity, separate product scope
- Custom dashboard layouts — over-engineering for fixed set of metrics
- Alerting/notifications — infrastructure complexity, user monitors actively for v1.1

### Architecture Approach

The architecture uses a three-layer pattern: Blazor components for UI, service facades for abstraction, and singleton runtime components. Background services monitor runtime state and push updates to all connected clients via IHubContext<RuntimeHub>. Components subscribe to SignalR events in OnInitializedAsync and call InvokeAsync(StateHasChanged) to trigger re-renders when updates arrive.

**Major components:**
1. **Service Facades (RuntimeService, ModuleService)** — wrap HeartbeatLoop and PluginRegistry, expose only UI-relevant operations, prevent direct coupling to runtime internals
2. **SignalR Hub (RuntimeHub)** — bidirectional communication channel, handles client-to-server calls for control operations
3. **Background Service (RuntimeMonitorService)** — IHostedService that polls runtime state every 500ms and broadcasts to all clients via IHubContext
4. **Blazor Components (Dashboard, Modules, Heartbeat pages)** — UI rendering with lifecycle hooks, subscribe to SignalR events, marshal updates to UI thread
5. **Existing Runtime (HeartbeatLoop, PluginRegistry, EventBus)** — unchanged, registered as singletons, shared across all users

**Data flow patterns:**
- **User actions → Runtime**: Component @onclick → IRuntimeService → HeartbeatLoop → Background service detects change → SignalR broadcast → All clients update
- **Runtime → UI updates**: HeartbeatLoop ticks → RuntimeMonitorService polls → IHubContext.Clients.All.SendAsync → SignalR WebSocket → Component.On<T> → InvokeAsync(StateHasChanged) → Browser updates
- **Module operations**: UI calls IModuleService → PluginLoader → PluginRegistry → EventBus injection → Background service broadcasts → All clients refresh

### Critical Pitfalls

1. **Cross-thread UI updates without InvokeAsync** — Background services update component state directly, causing "Dispatcher" exceptions or silent failures. Always wrap StateHasChanged() in InvokeAsync(() => StateHasChanged()) when handling events from non-UI threads.

2. **Heartbeat loop blocking on UI operations** — Awaiting SignalR or InvokeAsync from heartbeat loop causes missed ticks and snowball delays. Use fire-and-forget pattern: publish events without awaiting UI response, let components subscribe and update independently.

3. **Scoped service lifetime confusion** — Registering runtime services as Scoped creates separate instances per browser circuit, breaking shared state. Keep ModuleRegistry, EventBus, HeartbeatLoop as Singleton; only UI-specific services should be Scoped.

4. **SignalR circuit disposal not cleaning up event subscriptions** — Components subscribe to MediatR events but don't unsubscribe in Dispose, causing memory leaks. Always implement IDisposable and unsubscribe in Dispose() method.

5. **Module loading/unloading during active operations** — User triggers unload while module is processing heartbeat tick, causing TypeLoadException or crashes. Implement module lifecycle state machine, heartbeat checks state before invoking Tick(), unload waits for current tick to complete.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Service Abstraction & Hosting Transition
**Rationale:** Foundation must be solid before adding UI. Hosting model change from console app to WebApplication is risky and must preserve all v1.0 functionality. Service facades establish clean boundaries between UI and runtime.

**Delivers:**
- OpenAnima.WebUI project with DTOs (ModuleDto, HeartbeatStatusDto)
- IRuntimeService and IModuleService interfaces with implementations
- WebApplication host with singleton runtime services
- Browser auto-launch on startup
- Validation that all v1.0 features work in web host

**Addresses:**
- Hosting model transition pitfall (Phase 1 critical)
- Service lifetime confusion (DI configuration must be correct from start)
- Foundation for all UI features

**Avoids:**
- Breaking existing runtime functionality
- Scoped service lifetime mistakes
- Direct runtime access from components

### Phase 2: Blazor UI with Static Display
**Rationale:** Build UI layer without real-time complexity first. Validates component structure, routing, and service injection before adding SignalR push updates.

**Delivers:**
- MainLayout and NavMenu components
- Dashboard, Modules, Heartbeat pages (static data)
- Module list display with status indicators
- Module metadata display
- Heartbeat status display (polling-based initially)

**Uses:**
- Blazor Server 8.0 (built-in)
- Service facades from Phase 1
- Responsive layout patterns

**Implements:**
- Component architecture
- Service injection pattern
- Basic error handling

**Avoids:**
- Cross-thread UI update complexity (no real-time yet)
- SignalR message size issues (static data first)

### Phase 3: SignalR Real-Time Updates
**Rationale:** Add real-time push after static UI is validated. This is where thread safety and event subscription patterns become critical.

**Delivers:**
- RuntimeHub for SignalR communication
- RuntimeMonitorService (IHostedService) for state broadcasting
- Real-time heartbeat tick counter
- Real-time module list updates
- Heartbeat latency display
- InvokeAsync pattern for all state updates

**Addresses:**
- Real-time updates feature (table stakes)
- Heartbeat tick counter (table stakes)
- Heartbeat latency display (differentiator)

**Avoids:**
- Cross-thread UI update pitfall (InvokeAsync wrappers mandatory)
- Heartbeat blocking pitfall (fire-and-forget publish pattern)
- SignalR circuit disposal leaks (IDisposable implementation)
- SignalR message size limits (throttle to 500ms, send deltas)

### Phase 4: Control Operations
**Rationale:** Add user-initiated operations after monitoring is stable. Module lifecycle coordination is complex and requires state machine implementation.

**Delivers:**
- Start/Stop heartbeat buttons
- Load module control (file picker)
- Unload module control (per-module button)
- Operation result broadcasting via SignalR
- Module lifecycle state machine
- Error toast notifications

**Addresses:**
- Load/Unload controls (table stakes)
- Start/Stop controls (table stakes)
- Error display (table stakes)

**Avoids:**
- Module lifecycle race conditions (state machine coordination)
- Heartbeat blocking during operations (async/await pattern)
- Concurrent modification exceptions (snapshot registry before operations)

### Phase 5: Polish & Validation
**Rationale:** Final UX improvements and comprehensive testing. Validates memory stability, performance under load, and error recovery.

**Delivers:**
- Loading states and spinners
- Confirmation dialogs for destructive operations
- Connection status indicator
- Auto-reconnect UI
- Keyboard shortcuts (optional)
- Memory leak testing (100 connect/disconnect cycles)
- Performance validation (20+ modules, sustained operation)

**Addresses:**
- Responsive layout (table stakes)
- Auto-launch browser (table stakes)
- UX polish (competitive)

**Avoids:**
- Browser auto-launch timing issues (health check wait)
- Poor UX (no loading states, no confirmation)
- Memory leaks (validation testing)

### Phase Ordering Rationale

- **Phase 1 first**: Hosting transition is foundational and risky. Must validate v1.0 functionality works in web host before building UI on top.
- **Phase 2 before 3**: Static UI validates component structure without thread safety complexity. Easier to debug layout/routing issues without real-time updates.
- **Phase 3 before 4**: Real-time monitoring must be stable before adding control operations. Control operations depend on real-time feedback to show operation results.
- **Phase 4 before 5**: Core functionality complete before polish. Module lifecycle coordination is complex and needs dedicated phase.
- **Phase 5 last**: Polish and validation after all features work. Memory leak testing and performance validation require complete system.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 4 (Control Operations)**: Module lifecycle state machine is complex, may need research on AssemblyLoadContext unload coordination patterns and cancellation token propagation.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Service Abstraction)**: Standard facade pattern, well-documented in DDD literature
- **Phase 2 (Blazor UI)**: Standard Blazor Server component structure, official Microsoft docs sufficient
- **Phase 3 (SignalR Real-Time)**: Standard IHostedService + IHubContext pattern, documented in ASP.NET Core guides
- **Phase 5 (Polish)**: Standard UX patterns, no novel technical challenges

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Blazor Server and SignalR are built into ASP.NET Core 8.0, well-documented, stable features. Version compatibility verified against existing OpenAnima.Core.csproj. |
| Features | HIGH | Feature research based on analysis of similar tools (Docker Desktop, VS Code Extensions, Windows Task Manager) and established dashboard patterns. Table stakes vs differentiators clearly identified. |
| Architecture | HIGH | Service facade, IHostedService + IHubContext, and InvokeAsync patterns are standard Blazor Server practices with extensive documentation and community examples. |
| Pitfalls | MEDIUM | Cross-thread updates, service lifetimes, and memory leaks are well-documented Blazor Server issues. Module lifecycle coordination is project-specific and may reveal additional pitfalls during implementation. |

**Overall confidence:** HIGH

### Gaps to Address

- **Module lifecycle state machine details**: Research identified the need for coordination between heartbeat loop and load/unload operations, but specific state transitions and timeout values need validation during Phase 4 implementation. Consider researching AssemblyLoadContext.Unload() behavior under concurrent access.

- **SignalR message size optimization**: Research recommends delta updates and throttling, but specific thresholds (how many modules before message size becomes an issue) need empirical testing. Monitor during Phase 3 with realistic module counts.

- **Browser auto-launch cross-platform behavior**: Research provides Windows/Linux/macOS patterns, but actual behavior on WSL2 (current development environment) needs validation. Test during Phase 1 to ensure browser opens correctly.

- **Event bus weak reference strategy**: Research suggests weak references for Blazor subscribers to prevent memory leaks, but implementation details (WeakReference<T> vs ConditionalWeakTable) need evaluation during Phase 3. May require EventBus modifications.

## Sources

### Primary (HIGH confidence)
- [ASP.NET Core Blazor hosting models](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models) — Blazor Server architecture
- [Host ASP.NET Core SignalR in background services](https://learn.microsoft.com/en-us/aspnet/core/signalr/background-services) — IHostedService + IHubContext pattern
- [Host ASP.NET Core in a Windows Service](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service) — UseWindowsService() hosting
- [Microsoft.Extensions.Hosting.WindowsServices 10.0.3](https://www.nuget.org/packages/Microsoft.Extensions.Hosting.WindowsServices) — NuGet package verification
- [ASP.NET Core Blazor SignalR guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/signalr) — SignalR integration patterns
- [ASP.NET Core Blazor performance best practices](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/) — Performance optimization

### Secondary (MEDIUM confidence)
- [How to Build Real-Time Dashboards with SignalR and Blazor](https://oneuptime.com/blog/post/2026-01-25-real-time-dashboards-signalr-blazor/view) — Real-time dashboard patterns
- [Real-Time Blazor Apps with SignalR and Blazorise Notifications](https://blazorise.com/blog/real-time-blazor-apps-signalr-and-blazorise-notifications) — Notification patterns
- [Background Service Communication with Blazor via SignalR](https://medium.com/it-dead-inside/lets-learn-blazor-background-service-communication-with-blazor-via-signalr-84abe2660fd6) — Background service patterns
- [Thread safety using InvokeAsync - Blazor University](https://blazor-university.com/components/multi-threaded-rendering/invokeasync) — InvokeAsync pattern
- [Pushing UI changes from Blazor Server to browser on server raised events](https://swimburger.net/blog/dotnet/pushing-ui-changes-from-blazor-server-to-browser-on-server-raised-events) — Event-driven push
- [StateHasChanged() vs InvokeAsync(StateHasChanged) in Blazor](https://stackoverflow.com/questions/65230621/statehaschanged-vs-invokeasyncstatehaschanged-in-blazor) — Thread safety explanation
- [How to Use Docker Desktop Dashboard Effectively](https://oneuptime.com/blog/post/2026-02-08-how-to-use-docker-desktop-dashboard-effectively/view) — Container management UI patterns
- [Use extensions in Visual Studio Code](https://code.visualstudio.com/docs/getstarted/extensions) — Extension manager UI patterns
- [Windows Task Manager: The Complete Guide](https://www.howtogeek.com/405806/windows-task-manager-the-complete-guide/) — Process monitoring patterns

### Tertiary (LOW confidence)
- [Losing my mind: Blazor, SignalR and Service Lifetimes - Reddit](https://www.reddit.com/r/dotnet/comments/1egxo89/losing_my_mind_blazor_signalr_and_service/) — Service lifetime pitfalls
- [Blazor Server-Side Memory Leak #18556 - GitHub](https://github.com/dotnet/aspnetcore/issues/18556) — Memory leak patterns
- [Issue with concurrent collections in blazor server app - Reddit](https://www.reddit.com/r/Blazor/comments/1ftyhbu/issue_with_concurrent_collections_in_blazor/) — ConcurrentDictionary usage

---
*Research completed: 2026-02-22*
*Ready for roadmap: yes*
