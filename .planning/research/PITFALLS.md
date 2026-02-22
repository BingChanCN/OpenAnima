# Pitfalls Research

**Domain:** Adding Blazor Server WebUI to Existing .NET 8 Modular Runtime
**Researched:** 2026-02-22
**Confidence:** MEDIUM

## Critical Pitfalls

### Pitfall 1: Cross-Thread UI Updates Without InvokeAsync

**What goes wrong:**
Background services (like the existing PeriodicTimer heartbeat loop) directly update component state or call StateHasChanged() from non-UI threads, causing "The current thread is not associated with the Dispatcher" exceptions or silent failures where UI doesn't update.

**Why it happens:**
Blazor Server components run on a synchronization context tied to the SignalR circuit. When the existing heartbeat loop publishes events via MediatR, subscribers in Blazor components receive those events on the background thread, not the UI thread. Developers forget that every UI update must be marshaled through InvokeAsync().

**How to avoid:**
- Wrap ALL StateHasChanged() calls in InvokeAsync(() => StateHasChanged())
- When subscribing to MediatR events in components, always use InvokeAsync for state updates
- Create a base component with SafeStateHasChanged() helper that wraps InvokeAsync
- Never call component methods directly from background threads

**Warning signs:**
- Intermittent "Dispatcher" exceptions in logs
- UI not updating despite events firing (check with breakpoints)
- Race conditions that only appear under load
- Components showing stale data after events

**Phase to address:**
Phase 1 (Blazor Integration) — establish pattern immediately, create base component with helpers

---

### Pitfall 2: Heartbeat Loop Blocking on UI Operations

**What goes wrong:**
The existing 100ms PeriodicTimer heartbeat loop blocks waiting for UI operations to complete, causing the loop to miss ticks, snowball delays, or complete failure. The anti-snowball guard (skip if previous tick still running) triggers constantly.

**Why it happens:**
When heartbeat data is streamed to UI, developers await SignalR operations or InvokeAsync calls from within the heartbeat loop itself. SignalR can have network delays, and InvokeAsync queues work on the circuit's synchronization context which may be busy. The heartbeat loop should never wait for UI.

**How to avoid:**
- Use fire-and-forget pattern: publish events without awaiting UI response
- Heartbeat loop publishes to MediatR event bus (already exists), UI subscribes
- UI components buffer/throttle updates (e.g., only update every 200ms even if events arrive faster)
- Never await InvokeAsync from the heartbeat loop thread
- Keep heartbeat loop completely decoupled from UI lifecycle

**Warning signs:**
- Heartbeat tick count not incrementing at expected rate
- Anti-snowball guard logging "skipped tick" frequently
- Latency metrics showing >100ms when UI is connected
- Heartbeat stops entirely when browser disconnects

**Phase to address:**
Phase 1 (Blazor Integration) — architecture decision, must be correct from start

---

### Pitfall 3: Scoped Service Lifetime Confusion

**What goes wrong:**
Runtime services (ModuleRegistry, EventBus, HeartbeatLoop) registered as Singleton in console app, but Blazor Server creates per-circuit scopes. Developers accidentally register runtime services as Scoped, creating separate instances per user, breaking shared state. Or they inject Scoped services into Singletons, causing "Cannot consume scoped service from singleton" exceptions.

**Why it happens:**
Blazor Server's service lifetime model differs from console apps. Each SignalR circuit gets its own scope, and "Scoped" means "per circuit" (per user session), not "per request". The existing runtime expects singleton services shared across all modules. Mixing lifetimes causes either duplicate state or DI exceptions.

**How to avoid:**
- Keep runtime services (ModuleRegistry, EventBus, HeartbeatLoop) as Singleton
- UI-specific services (component state, user preferences) should be Scoped
- Never inject Scoped services into Singleton services
- Use IServiceProvider with CreateScope() if Singleton needs temporary Scoped access
- Document lifetime for every service in DI registration

**Warning signs:**
- "Cannot consume scoped service from singleton" exceptions
- Multiple ModuleRegistry instances (check with logging in constructor)
- Modules loaded in one browser tab not visible in another
- Heartbeat state different per user

**Phase to address:**
Phase 1 (Blazor Integration) — DI configuration must be correct from start

---

### Pitfall 4: SignalR Circuit Disposal Not Cleaning Up Event Subscriptions

**What goes wrong:**
Blazor components subscribe to MediatR events in OnInitialized but don't unsubscribe in Dispose, causing memory leaks. The EventBus holds references to disposed components, preventing GC. Over time, memory grows and performance degrades as events are delivered to dead circuits.

**Why it happens:**
The existing EventBus uses ConcurrentBag for subscribers with lazy cleanup. When Blazor circuits disconnect (user closes browser), components are disposed but subscriptions remain in the EventBus. The lazy cleanup (every 100 publishes) may not trigger frequently enough, and weak references aren't used.

**How to avoid:**
- Implement IDisposable in all components that subscribe to events
- Unsubscribe in Dispose() method
- Consider weak references in EventBus for Blazor subscribers
- Add circuit disposal logging to detect leaks early
- Monitor memory growth during testing (connect/disconnect repeatedly)

**Warning signs:**
- Memory usage grows with each browser connection/disconnection
- Event handlers firing for disconnected users (check logs)
- Performance degradation over time
- GC not reclaiming component memory

**Phase to address:**
Phase 1 (Blazor Integration) — EventBus may need modification, component pattern must be established

---

### Pitfall 5: Module Loading/Unloading While UI Is Connected

**What goes wrong:**
User triggers module unload from UI while that module is processing a heartbeat tick or handling an event. AssemblyLoadContext unloads the assembly, causing TypeLoadException, NullReferenceException, or crashes. UI shows stale module list or incorrect status.

**Why it happens:**
The existing runtime doesn't coordinate module lifecycle with active operations. When UI adds control operations (load/unload buttons), race conditions emerge: UI thread requests unload while heartbeat thread invokes module's Tick() method. AssemblyLoadContext.Unload() doesn't wait for in-flight operations.

**How to avoid:**
- Implement module lifecycle state machine (Loading → Loaded → Unloading → Unloaded)
- Heartbeat loop checks module state before invoking Tick(), skips if Unloading
- Unload operation waits for current tick to complete (timeout after 200ms)
- UI disables unload button while module is active in heartbeat
- Add CancellationToken to module operations for graceful shutdown

**Warning signs:**
- TypeLoadException during module unload
- Heartbeat loop crashes when modules change
- UI shows "module loaded" but runtime shows "unloaded"
- Race condition exceptions under rapid load/unload

**Phase to address:**
Phase 2 (Control Operations) — requires runtime changes before UI can safely trigger operations

---

### Pitfall 6: Hosting Model Transition Breaking Existing Functionality

**What goes wrong:**
Migrating from console app (Host.CreateDefaultBuilder) to web app (WebApplication.CreateBuilder) changes service registration order, configuration loading, logging setup, or hosted service lifecycle. The existing runtime fails to start, modules don't load, or heartbeat doesn't run.

**Why it happens:**
WebApplicationBuilder has different defaults than HostBuilder. Middleware pipeline, Kestrel configuration, and hosted service startup order differ. The existing HeartbeatLoop as IHostedService may start before modules are loaded, or configuration files aren't found because working directory changes.

**How to avoid:**
- Keep existing runtime initialization separate from web host setup
- Use WebApplication.CreateBuilder but manually configure services to match console app
- Start HeartbeatLoop explicitly after modules load, not via IHostedService auto-start
- Test that all v1.0 functionality works before adding UI
- Use integration tests to verify module loading, event bus, heartbeat in web host

**Warning signs:**
- Runtime starts but modules don't load
- Configuration files not found (path issues)
- Heartbeat doesn't start or starts before modules load
- Logging output changes or disappears

**Phase to address:**
Phase 1 (Blazor Integration) — hosting transition is foundational, must work before UI

---

### Pitfall 7: SignalR Message Size Limits on Heartbeat Data Streaming

**What goes wrong:**
Streaming full module state, event history, or large heartbeat payloads exceeds SignalR's default 32KB message size limit. SignalR disconnects the circuit with "Maximum message size exceeded" error. UI shows connection lost, no data updates.

**Why it happens:**
Developers serialize entire ModuleRegistry state or full event history and push to UI every heartbeat tick. With many modules or verbose metadata, messages grow large. SignalR has conservative defaults to prevent DoS attacks. Streaming at 100ms intervals amplifies the problem.

**How to avoid:**
- Send deltas, not full state (only changed modules, incremental tick count)
- Increase SignalR MaximumReceiveMessageSize if needed (but prefer smaller messages)
- Throttle UI updates (update every 200-500ms, not every 100ms tick)
- Paginate large data (module list, event history)
- Use efficient serialization (System.Text.Json, not verbose formats)

**Warning signs:**
- "Maximum message size exceeded" in browser console
- SignalR circuit disconnects under load
- Network tab shows large WebSocket frames
- UI updates stop after initial connection

**Phase to address:**
Phase 1 (Blazor Integration) — data streaming design must account for limits

---

### Pitfall 8: Browser Auto-Launch Timing Issues

**What goes wrong:**
Runtime launches browser before Kestrel finishes starting, resulting in "connection refused" error page. Or browser launches but SignalR circuit fails to establish because services aren't ready. User sees error instead of dashboard.

**Why it happens:**
Developers call Process.Start() to launch browser immediately after WebApplication.RunAsync(), but Kestrel startup is asynchronous. The browser request arrives before the server is listening. Or DI services (ModuleRegistry, HeartbeatLoop) aren't initialized when first circuit connects.

**How to avoid:**
- Use IHostApplicationLifetime.ApplicationStarted event to launch browser
- Ensure all runtime services initialize before web host starts
- Add health check endpoint, wait for it to respond before launching browser
- Handle connection failures gracefully in UI (retry with exponential backoff)
- Provide manual URL in console if auto-launch fails

**Warning signs:**
- Browser shows "connection refused" on first launch
- Intermittent startup failures (race condition)
- SignalR circuit fails to establish on first page load
- Console shows "listening on http://..." after browser already opened

**Phase to address:**
Phase 3 (Background Service) — auto-launch is final integration step

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Polling UI for updates instead of push | Simpler implementation, no InvokeAsync complexity | Increased latency, wasted CPU, poor UX | Never — defeats purpose of Blazor Server real-time |
| Singleton services for UI state | Avoids scoped lifetime confusion | Shared state across users, security risk | Never — breaks multi-user scenarios |
| Disabling AssemblyLoadContext isolation for UI | Easier DI integration | Loses module isolation, version conflicts | Never — core architecture principle |
| Blocking heartbeat loop to wait for UI | Simpler synchronous code | Heartbeat delays, missed ticks, poor performance | Never — violates 100ms requirement |
| Global exception handler without circuit-specific handling | Catches all errors | Can't distinguish runtime vs UI errors, poor diagnostics | Acceptable for MVP, refine in Phase 2 |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| MediatR EventBus → Blazor | Subscribing in OnInitialized without unsubscribing in Dispose | Always implement IDisposable, unsubscribe in Dispose() |
| PeriodicTimer → UI updates | Awaiting InvokeAsync from heartbeat loop | Fire-and-forget publish to EventBus, UI subscribes and updates independently |
| AssemblyLoadContext → DI | Trying to inject module types directly into Blazor components | Use duck-typing or shared interfaces, never cross context boundaries |
| ConcurrentDictionary → UI display | Enumerating during modification (race condition) | Snapshot to array before rendering: registry.ToArray() |
| FileSystemWatcher → UI notifications | Flooding UI with file change events | Debounce events (e.g., 500ms delay), batch updates |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Updating UI every heartbeat tick (100ms) | High CPU, SignalR bandwidth saturation | Throttle to 200-500ms, send deltas only | >5 concurrent users |
| Serializing full ModuleRegistry on every update | Large SignalR messages, slow rendering | Send changed modules only, use efficient serialization | >10 modules loaded |
| Synchronous module operations in UI thread | UI freezes, poor responsiveness | Always use async/await, offload to background | Any CPU-intensive operation |
| No pagination on module/event lists | Slow initial render, memory growth | Paginate or virtualize lists | >50 items |
| Logging every heartbeat tick to console | I/O bottleneck, log file growth | Log at Debug level, use structured logging with sampling | Continuous operation |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Exposing module file paths in UI | Information disclosure, path traversal | Show module name only, sanitize paths |
| Allowing arbitrary module loading from UI | Code execution vulnerability | Whitelist allowed module directories, validate signatures |
| No authentication on WebUI | Unauthorized access to runtime control | Add authentication (Windows auth for local, or simple token) |
| Leaking exception details to UI | Information disclosure | Sanitize exceptions, log full details server-side only |
| No rate limiting on control operations | DoS via rapid load/unload | Throttle operations, require confirmation for destructive actions |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|--------------------|
| No loading state during module operations | User clicks button, nothing happens, clicks again | Show spinner, disable button, provide feedback |
| Heartbeat stops but UI doesn't indicate | User thinks system is working when it's not | Show "heartbeat stopped" warning, last tick timestamp |
| SignalR disconnect without reconnect UI | UI shows stale data, user doesn't know connection lost | Show connection status, auto-reconnect with visual feedback |
| No confirmation for destructive operations | User accidentally unloads critical module | Require confirmation for unload, show impact (e.g., "3 modules depend on this") |
| Overwhelming real-time updates | User can't read data, information overload | Throttle updates, allow pause/resume, highlight changes |

## "Looks Done But Isn't" Checklist

- [ ] **Real-time updates:** Often missing InvokeAsync wrapper — verify StateHasChanged() always wrapped
- [ ] **Event subscriptions:** Often missing Dispose/unsubscribe — verify IDisposable implemented
- [ ] **Module operations:** Often missing state coordination — verify heartbeat skips modules during unload
- [ ] **SignalR reconnection:** Often missing reconnect UI — verify connection status shown, auto-reconnect works
- [ ] **Error handling:** Often missing circuit-specific handling — verify errors don't crash other users' circuits
- [ ] **Memory cleanup:** Often missing weak references or disposal — verify memory doesn't grow with connect/disconnect cycles
- [ ] **Hosting transition:** Often missing service registration verification — verify all v1.0 features work in web host
- [ ] **Browser auto-launch:** Often missing startup synchronization — verify browser opens after server ready

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Cross-thread UI updates | LOW | Add InvokeAsync wrappers, test thoroughly |
| Heartbeat blocking | MEDIUM | Refactor to fire-and-forget, add buffering in UI |
| Service lifetime issues | MEDIUM | Fix DI registrations, may require service refactoring |
| Memory leaks from subscriptions | LOW | Add Dispose implementations, test with repeated connect/disconnect |
| Module lifecycle races | HIGH | Implement state machine, add coordination logic, extensive testing |
| Hosting model breaks runtime | MEDIUM | Revert to console app, incrementally migrate services |
| SignalR message size exceeded | LOW | Implement delta updates, increase limits if needed |
| Browser launch timing | LOW | Add health check wait, implement retry logic |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Cross-thread UI updates | Phase 1 | All UI updates use InvokeAsync, no Dispatcher exceptions in logs |
| Heartbeat blocking | Phase 1 | Heartbeat maintains 100ms tick rate with UI connected |
| Service lifetime confusion | Phase 1 | Single ModuleRegistry instance, no DI exceptions |
| Event subscription leaks | Phase 1 | Memory stable after 100 connect/disconnect cycles |
| Module lifecycle races | Phase 2 | Load/unload operations safe during heartbeat, no exceptions |
| Hosting model transition | Phase 1 | All v1.0 features work: module loading, event bus, heartbeat |
| SignalR message size | Phase 1 | No "message size exceeded" errors with 20 modules loaded |
| Browser auto-launch | Phase 3 | Browser opens to working dashboard 100% of time |

## Sources

- [ASP.NET Core Blazor SignalR guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/signalr?view=aspnetcore-10.0)
- [ASP.NET Core Blazor performance best practices](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/?view=aspnetcore-10.0)
- [Background tasks with hosted services in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0)
- [Thread safety using InvokeAsync - Blazor University](https://blazor-university.com/components/multi-threaded-rendering/invokeasync)
- [Losing my mind: Blazor, SignalR and Service Lifetimes - Reddit](https://www.reddit.com/r/dotnet/comments/1egxo89/losing_my_mind_blazor_signalr_and_service/)
- [StateHasChanged() vs InvokeAsync(StateHasChanged) in Blazor - Stack Overflow](https://stackoverflow.com/questions/65230621/statehaschanged-vs-invokeasyncstatehaschanged-in-blazor)
- [Blazor Server-Side Memory Leak #18556 - GitHub](https://github.com/dotnet/aspnetcore/issues/18556)
- [Background Service Communication with Blazor via SignalR - Medium](https://medium.com/it-dead-inside/lets-learn-blazor-background-service-communication-with-blazor-via-signalr-84abe2660fd6)
- [Hosted service prevents app to start completely - GitHub](https://github.com/dotnet/aspnetcore/issues/38698)
- [Issue with concurrent collections in blazor server app - Reddit](https://www.reddit.com/r/Blazor/comments/1ftyhbu/issue_with_concurrent_collections_in_blazor/)

---
*Pitfalls research for: Adding Blazor Server WebUI to Existing .NET 8 Modular Runtime*
*Researched: 2026-02-22*
