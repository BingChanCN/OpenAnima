# Phase 5: SignalR Real-Time Updates - Research

**Researched:** 2026-02-22
**Domain:** SignalR Hub, Real-Time Push, Blazor Server Components
**Confidence:** HIGH

## Summary

Phase 5 adds real-time data push from the OpenAnima runtime to the browser dashboard. The project already runs as a Blazor Server app (Phase 3) with static display pages (Phase 4). Blazor Server uses SignalR internally for its circuit, but for server-initiated push from background services (HeartbeatLoop), we need a dedicated SignalR Hub.

The core pattern: create a strongly-typed SignalR Hub (`RuntimeHub`), inject `IHubContext<RuntimeHub, IRuntimeClient>` into the HeartbeatLoop/HostedService to push tick data on every heartbeat tick, and have Blazor components connect to the Hub via `HubConnection` to receive updates and call `InvokeAsync(StateHasChanged)`.

**Primary recommendation:** Use a strongly-typed SignalR Hub with `IHubContext` injection into the existing HeartbeatLoop. Push tick count + latency on every tick. Blazor components subscribe via `HubConnection` and update UI reactively. Per-tick latency requires adding timing measurement to `ExecuteTickAsync`.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Real-time data display:**
- New dedicated monitoring page for all real-time data
- Card-based layout, one card per metric (tick count, latency, heartbeat status, etc.)
- Numbers + sparkline charts showing recent trends
- Rolling number animation for value updates

**Latency warning behavior:**
- Color-only warnings (no icons or text)
- Instant reaction: single tick over threshold changes color, recovery is immediate
- Three-tier: normal (green), caution (50-100ms yellow), warning (>100ms red)
- Sparkline chart shows 100ms baseline for visual comparison

**Connection state and disconnect handling:**
- Status indicator dot (green/red) showing SignalR connection state
- Auto-reconnect on disconnect with "Reconnecting..." status
- On reconnect, pull latest state for seamless continuation

### Claude's Discretion

- Visual treatment of real-time data area during disconnect (freeze/grey out etc.)
- Sparkline implementation approach and data point count
- Card spacing, ordering, and layout details
- SignalR Hub architecture and push frequency

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| INFRA-02 | Runtime state changes push to browser in real-time via SignalR without manual refresh | SignalR Hub with IHubContext injection into HeartbeatLoop; Clients.All.SendAsync on each tick |
| BEAT-01 | User can view heartbeat loop running state (Running/Stopped) | Push IsRunning state via Hub on start/stop events; display on monitoring page |
| BEAT-03 | User can see a live tick counter updating in real-time | Push TickCount on every heartbeat tick via Hub; rolling number animation in UI |
| BEAT-04 | User can see per-tick latency with warning when exceeding 100ms target | Add Stopwatch timing to ExecuteTickAsync; push latency value via Hub; three-tier color coding |
</phase_requirements>

## Standard Stack

### Core (Already in Place)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Blazor Server | .NET 8.0 | Interactive web UI framework | Already integrated; SignalR built-in |
| ASP.NET Core SignalR | .NET 8.0 | Real-time server-to-client push | Included in Microsoft.NET.Sdk.Web; no extra NuGet needed for server-side |
| Pure CSS | CSS3 | Styling and animations | Project decision: lightweight, no component library |

### Supporting (New for Phase 5)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.AspNetCore.SignalR.Client | 8.0.x | HubConnection in Blazor components | Blazor Server components need HubConnection to receive server-push events |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| SignalR Hub + HubConnection | In-process event/delegate pattern | Simpler but doesn't satisfy INFRA-02 "via SignalR" requirement; also doesn't support future multi-client scenarios |
| Custom sparkline SVG | Chart.js / Blazor chart library | User wants lightweight; inline SVG sparklines are ~30 lines of code, no JS dependency |
| CSS @keyframes for number animation | JS counter library | Pure CSS approach aligns with project philosophy; CSS counters with transitions handle rolling numbers |

**Installation:**
```bash
dotnet add src/OpenAnima.Core/OpenAnima.Core.csproj package Microsoft.AspNetCore.SignalR.Client
```

Note: The server-side SignalR (Hub, IHubContext) is already included in `Microsoft.NET.Sdk.Web`. Only the client package is needed for `HubConnection` in Blazor components.

## Architecture Patterns

### Recommended Project Structure

```
src/OpenAnima.Core/
├── Hubs/
│   ├── RuntimeHub.cs              # NEW: SignalR Hub for runtime data
│   └── IRuntimeClient.cs          # NEW: Strongly-typed client interface
├── Components/
│   ├── Pages/
│   │   └── Monitor.razor          # NEW: Real-time monitoring page
│   │   └── Monitor.razor.css
│   ├── Shared/
│   │   └── Sparkline.razor        # NEW: SVG sparkline component
│   │   └── Sparkline.razor.css
│   └── Layout/
│       └── MainLayout.razor       # UPDATE: Add Monitor nav item
├── Runtime/
│   └── HeartbeatLoop.cs           # UPDATE: Add latency tracking + Hub push
├── Services/
│   └── IHeartbeatService.cs       # UPDATE: Add LastTickLatencyMs property
│   └── HeartbeatService.cs        # UPDATE: Expose latency
└── Program.cs                     # UPDATE: AddSignalR + MapHub
```

### Pattern 1: Strongly-Typed SignalR Hub

**What:** Hub<T> with interface defining all client methods — compile-time safety for push messages.

**When to use:** Always for production SignalR hubs. Prevents string-based method name errors.

**Example:**
```csharp
// IRuntimeClient.cs — defines what the server can push to clients
public interface IRuntimeClient
{
    Task ReceiveHeartbeatTick(long tickCount, double latencyMs);
    Task ReceiveHeartbeatStateChanged(bool isRunning);
    Task ReceiveModuleListChanged(int moduleCount);
}

// RuntimeHub.cs — the Hub itself (mostly empty for server-push)
public class RuntimeHub : Hub<IRuntimeClient>
{
    // Hub methods are for client-to-server calls (none needed for Phase 5)
    // Server pushes via IHubContext<RuntimeHub, IRuntimeClient>
}
```

**Source:** [Microsoft Docs - Strongly typed hubs](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs?view=aspnetcore-8.0#strongly-typed-hubs)

### Pattern 2: IHubContext Injection into Background Service

**What:** Inject `IHubContext<THub, T>` into IHostedService or any DI-resolved class to push messages without being inside the Hub.

**When to use:** When background services (HeartbeatLoop) need to push data to connected clients.

**Example:**
```csharp
// In HeartbeatLoop or OpenAnimaHostedService
private readonly IHubContext<RuntimeHub, IRuntimeClient> _hubContext;

// During tick execution:
await _hubContext.Clients.All.ReceiveHeartbeatTick(tickCount, latencyMs);
```

**Source:** [Microsoft Docs - SignalR in background services](https://learn.microsoft.com/en-us/aspnet/core/signalr/background-services?view=aspnetcore-8.0)

### Pattern 3: HubConnection in Blazor Server Components

**What:** Blazor Server components create a HubConnection to the same server's Hub endpoint to receive push events.

**When to use:** When Blazor Server components need to receive server-initiated push messages.

**Example:**
```razor
@inject NavigationManager Navigation
@implements IAsyncDisposable

@code {
    private HubConnection? hubConnection;

    protected override async Task OnInitializedAsync()
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/hubs/runtime"))
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<long, double>("ReceiveHeartbeatTick", (tick, latency) =>
        {
            // Update component state
            InvokeAsync(StateHasChanged);
        });

        await hubConnection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
            await hubConnection.DisposeAsync();
    }
}
```

**Key detail:** `WithAutomaticReconnect()` handles the user's requirement for auto-reconnect. `InvokeAsync(StateHasChanged)` is required because Hub callbacks run on a non-UI thread.

### Pattern 4: SVG Sparkline Component

**What:** Lightweight inline SVG polyline for trend visualization.

**When to use:** Displaying recent data trends (last N values) without a charting library.

**Example:**
```razor
<!-- Sparkline.razor -->
@if (Values.Count > 1)
{
    <svg class="sparkline" viewBox="0 0 @Width @Height" preserveAspectRatio="none">
        @if (BaselineY.HasValue)
        {
            <line x1="0" y1="@BaselineY" x2="@Width" y2="@BaselineY"
                  class="sparkline-baseline" />
        }
        <polyline points="@GetPoints()" class="sparkline-line" fill="none" />
    </svg>
}

@code {
    [Parameter] public List<double> Values { get; set; } = new();
    [Parameter] public int Width { get; set; } = 120;
    [Parameter] public int Height { get; set; } = 40;
    [Parameter] public double? BaselineValue { get; set; }

    private double? BaselineY => BaselineValue.HasValue
        ? Height - (BaselineValue.Value / Values.Max()) * Height
        : null;

    private string GetPoints()
    {
        if (Values.Count < 2) return "";
        var max = Values.Max();
        if (max == 0) max = 1;
        var step = (double)Width / (Values.Count - 1);
        return string.Join(" ", Values.Select((v, i) =>
            $"{i * step},{Height - (v / max) * Height}"));
    }
}
```

### Pattern 5: CSS Rolling Number Animation

**What:** CSS transition on numeric values for smooth visual updates.

**When to use:** Tick counter and latency display updates.

**Approach:** Use CSS `transition` on opacity/transform for a subtle fade-slide effect when values change. Pure CSS — no JavaScript counter libraries needed.

```css
.metric-value {
    transition: opacity 0.15s ease, transform 0.15s ease;
}
.metric-value.updating {
    opacity: 0.7;
    transform: translateY(-2px);
}
```

Note: True digit-rolling animation requires JS or complex CSS. A subtle fade-slide is more appropriate for 100ms update frequency and aligns with the project's pure CSS philosophy.

### Anti-Patterns to Avoid

- **Creating HubConnection per render:** Build HubConnection in `OnInitializedAsync`, not `OnParametersSet`. Dispose in `IAsyncDisposable`.
- **Forgetting InvokeAsync(StateHasChanged):** Hub callbacks run on non-UI thread. Always wrap state updates in `InvokeAsync`.
- **Pushing too much data:** Push only changed values (tick count + latency), not full state snapshots.
- **Not disposing HubConnection:** Memory leak if component doesn't implement `IAsyncDisposable`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Auto-reconnect logic | Custom retry loop with timers | `WithAutomaticReconnect()` on HubConnectionBuilder | Handles exponential backoff, state management, reconnection events |
| Connection state tracking | Manual bool flags | `hubConnection.State` and `Reconnecting`/`Reconnected` events | Built-in state machine handles all edge cases |
| Chart library | Full charting framework | Inline SVG sparkline (~30 lines) | User wants lightweight sparklines, not full charts |
| Real-time data buffer | Custom circular buffer | `Queue<double>` with capacity check | Simple, built-in, sufficient for sparkline data points |

**Key insight:** SignalR's `HubConnectionBuilder` provides auto-reconnect, state tracking, and connection lifecycle management out of the box. Don't replicate these.

## Common Pitfalls

### Pitfall 1: Port Exhaustion from HubConnection in Blazor Server

**What goes wrong:** Each Blazor Server component creates a HubConnection back to the same server, potentially exhausting ports.

**Why it happens:** HubConnection creates a new HTTP connection. With many concurrent users, this doubles connection count.

**How to avoid:** For a local-first single-user app like OpenAnima, this is not a concern. One user = one HubConnection. If scaling later, consider using the Blazor circuit's existing SignalR connection instead.

**Warning signs:** Connection refused errors, port exhaustion in logs.

### Pitfall 2: StateHasChanged on Disposed Component

**What goes wrong:** Hub callback fires after component is disposed, causing ObjectDisposedException.

**Why it happens:** HubConnection callbacks are not automatically unregistered when component disposes.

**How to avoid:** Use a `CancellationTokenSource` or `bool _disposed` flag. Check before calling `InvokeAsync(StateHasChanged)`. Always dispose HubConnection in `DisposeAsync`.

**Warning signs:** ObjectDisposedException in logs, especially during navigation.

**Example:**
```csharp
private bool _disposed;

public async ValueTask DisposeAsync()
{
    _disposed = true;
    if (hubConnection is not null)
        await hubConnection.DisposeAsync();
}

// In hub callback:
if (!_disposed)
    await InvokeAsync(StateHasChanged);
```

### Pitfall 3: Sparkline Performance with High-Frequency Updates

**What goes wrong:** Re-rendering SVG sparkline on every 100ms tick causes UI jank.

**Why it happens:** Blazor re-renders the entire component tree on StateHasChanged.

**How to avoid:** Throttle UI updates to ~500ms-1s intervals even though ticks arrive at 100ms. Buffer tick data and update display periodically. Use `ShouldRender()` override or a timer-based approach.

**Warning signs:** Browser tab becomes sluggish, high CPU usage.

### Pitfall 4: Missing Latency Data in HeartbeatLoop

**What goes wrong:** No per-tick latency data available for display.

**Why it happens:** Current HeartbeatLoop calculates duration but only logs warnings — doesn't expose the value.

**How to avoid:** Add a `LastTickLatencyMs` property to HeartbeatLoop and expose via IHeartbeatService. Use `Stopwatch` for precise timing (not DateTime subtraction).

**Warning signs:** Latency card shows 0 or N/A.

## Code Examples

### Registering SignalR Hub in Program.cs

```csharp
// In Program.cs, after existing service registration:
builder.Services.AddSignalR();

// After app.UseAntiforgery():
app.MapHub<RuntimeHub>("/hubs/runtime");
```

Note: `AddSignalR()` is separate from the Blazor Server SignalR circuit. Blazor's `AddInteractiveServerComponents()` handles its own circuit. The Hub endpoint is additional.

### Pushing from HeartbeatLoop via IHubContext

```csharp
// HeartbeatLoop constructor addition:
private readonly IHubContext<RuntimeHub, IRuntimeClient>? _hubContext;

// In ExecuteTickAsync, after tick execution:
var sw = Stopwatch.StartNew();
// ... existing tick logic ...
sw.Stop();
var latencyMs = sw.Elapsed.TotalMilliseconds;
_lastTickLatencyMs = latencyMs;

if (_hubContext != null)
{
    _ = _hubContext.Clients.All.ReceiveHeartbeatTick(_tickCount, latencyMs);
}
```

### Connection State Indicator

```razor
<span class="connection-dot @ConnectionClass"></span>

@code {
    private string ConnectionClass => hubConnection?.State switch
    {
        HubConnectionState.Connected => "connected",
        HubConnectionState.Reconnecting => "reconnecting",
        _ => "disconnected"
    };
}
```

```css
.connection-dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    display: inline-block;
}
.connection-dot.connected { background-color: var(--success-color); }
.connection-dot.disconnected { background-color: var(--error-color); }
.connection-dot.reconnecting {
    background-color: var(--warning-color);
    animation: pulse 1s infinite;
}
@keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.4; }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Polling with setInterval | SignalR server push | 2018+ | Eliminates unnecessary requests; true real-time |
| Untyped Hub (SendAsync strings) | Strongly-typed Hub<T> | .NET Core 2.1+ | Compile-time safety for client method names |
| Manual reconnect logic | WithAutomaticReconnect() | .NET Core 3.0+ | Built-in exponential backoff and state management |
| JavaScript chart libraries | Inline SVG sparklines | Trend | Lightweight, no JS dependency, sufficient for simple trends |

**Deprecated/outdated:**
- **Timer-based polling in Blazor components:** Replace with SignalR push for real-time data
- **`@implements IDisposable` for HubConnection:** Use `IAsyncDisposable` — HubConnection.DisposeAsync() is async

## Open Questions

1. **UI update throttling strategy**
   - What we know: Heartbeat ticks at 100ms, but rendering at 100ms may cause jank
   - What's unclear: Optimal UI refresh rate for smooth experience
   - Recommendation: Buffer ticks, update UI every ~500ms (every 5th tick). Push every tick to Hub but throttle component re-renders.

2. **Sparkline data point count**
   - What we know: User wants sparkline showing recent trends
   - What's unclear: How many data points to retain
   - Recommendation: Keep last 60 data points (~30 seconds at 500ms display rate). Enough for trend visibility without memory concern.

3. **Module list change notification**
   - What we know: Success criteria says "Module list updates automatically when modules load/unload"
   - What's unclear: How to detect module changes from HeartbeatLoop
   - Recommendation: Push module count change from ModuleService when LoadModule is called. Use IHubContext in ModuleService or raise an event that HostedService forwards to Hub.

## Sources

### Primary (HIGH confidence)

- [Microsoft Docs - SignalR Hubs](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs?view=aspnetcore-8.0) - Hub<T>, strongly-typed hubs, DI injection
- [Microsoft Docs - SignalR HubContext](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubcontext?view=aspnetcore-8.0) - IHubContext injection pattern
- [Microsoft Docs - SignalR in background services](https://learn.microsoft.com/en-us/aspnet/core/signalr/background-services?view=aspnetcore-8.0) - IHostedService + IHubContext pattern
- [Microsoft Docs - Blazor SignalR tutorial](https://learn.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor?view=aspnetcore-8.0) - HubConnection in Blazor components

### Secondary (MEDIUM confidence)

- Existing codebase: HeartbeatLoop.cs, Program.cs, IHeartbeatService.cs — verified current architecture
- [Microsoft Docs - Blazor SignalR guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/signalr?view=aspnetcore-8.0) - Blazor Server SignalR circuit details

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - SignalR is built into ASP.NET Core; patterns verified in official docs
- Architecture: HIGH - IHubContext + background service pattern is the documented approach
- Pitfalls: HIGH - Common issues well-documented; existing codebase patterns understood

**Research date:** 2026-02-22
**Valid until:** ~60 days (stable .NET 8 LTS; SignalR API stable since .NET Core 3.0)

---

*Phase: 05-signalr-real-time-updates*
*Research complete: 2026-02-22*
