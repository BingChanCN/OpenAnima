# Architecture Research: Blazor Server WebUI Integration

**Domain:** Blazor Server WebUI for .NET 8 Runtime Monitoring
**Researched:** 2026-02-22
**Confidence:** HIGH

## Standard Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Browser (SignalR Client)                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │  Dashboard   │  │   Modules    │  │  Heartbeat   │       │
│  │  Component   │  │  Component   │  │  Component   │       │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘       │
│         │                 │                 │                │
└─────────┼─────────────────┼─────────────────┼────────────────┘
          │ SignalR         │ SignalR         │ SignalR
          │ (WebSocket)     │ (WebSocket)     │ (WebSocket)
┌─────────┼─────────────────┼─────────────────┼────────────────┐
│         ↓                 ↓                 ↓                │
│  ┌──────────────────────────────────────────────────┐        │
│  │           ASP.NET Core + Blazor Server           │        │
│  │  ┌────────────┐  ┌────────────┐  ┌────────────┐ │        │
│  │  │ SignalR    │  │  Razor     │  │  Service   │ │        │
│  │  │ Hub        │  │ Components │  │  Layer     │ │        │
│  │  └─────┬──────┘  └─────┬──────┘  └─────┬──────┘ │        │
│  └────────┼───────────────┼───────────────┼────────┘        │
├───────────┼───────────────┼───────────────┼─────────────────┤
│           │               │               │                 │
│  ┌────────┴───────────────┴───────────────┴────────┐        │
│  │         Service Abstraction Layer                │        │
│  │  ┌──────────────┐  ┌──────────────┐             │        │
│  │  │  Runtime     │  │  Module      │             │        │
│  │  │  Service     │  │  Service     │             │        │
│  │  └──────┬───────┘  └──────┬───────┘             │        │
│  └─────────┼──────────────────┼─────────────────────┘        │
├────────────┼──────────────────┼──────────────────────────────┤
│            │                  │                              │
│  ┌─────────┴──────────────────┴─────────────────┐            │
│  │      Existing OpenAnima Runtime              │            │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐   │            │
│  │  │ Heartbeat│  │  Plugin  │  │ EventBus │   │            │
│  │  │   Loop   │  │ Registry │  │          │   │            │
│  │  └──────────┘  └──────────┘  └──────────┘   │            │
│  └───────────────────────────────────────────────┘            │
└─────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| **Blazor Components** | UI rendering, user interaction, real-time display | .razor files with @code blocks, lifecycle hooks |
| **SignalR Hub** | Real-time bidirectional communication | Hub class with methods for client-to-server calls |
| **Service Layer** | Abstraction between UI and runtime, command/query handling | Scoped/Singleton services injected into components |
| **Runtime Service** | Facade over HeartbeatLoop, exposes control operations | Singleton wrapping existing HeartbeatLoop |
| **Module Service** | Facade over PluginRegistry, exposes module queries | Singleton wrapping existing PluginRegistry |
| **Background Service** | Push runtime state changes to connected clients via IHubContext | IHostedService that monitors runtime and broadcasts |
| **Existing Runtime** | Core agent platform (unchanged) | HeartbeatLoop, PluginRegistry, EventBus |

## Recommended Project Structure

```
src/
├── OpenAnima.Contracts/       # Existing - no changes
├── OpenAnima.Core/            # Existing - minimal changes
│   ├── Events/
│   ├── Plugins/
│   ├── Runtime/
│   └── Program.cs             # MODIFY: Replace with WebApplication host
├── OpenAnima.WebUI/           # NEW PROJECT
│   ├── Components/            # Blazor components
│   │   ├── Pages/             # Routable pages
│   │   │   ├── Dashboard.razor
│   │   │   ├── Modules.razor
│   │   │   └── Heartbeat.razor
│   │   ├── Layout/            # Layout components
│   │   │   ├── MainLayout.razor
│   │   │   └── NavMenu.razor
│   │   └── Shared/            # Reusable components
│   │       ├── ModuleCard.razor
│   │       └── HeartbeatMonitor.razor
│   ├── Services/              # Service abstraction layer
│   │   ├── IRuntimeService.cs
│   │   ├── RuntimeService.cs
│   │   ├── IModuleService.cs
│   │   └── ModuleService.cs
│   ├── Hubs/                  # SignalR hubs
│   │   └── RuntimeHub.cs
│   ├── BackgroundServices/    # Background workers
│   │   └── RuntimeMonitorService.cs
│   ├── Models/                # DTOs for UI
│   │   ├── ModuleDto.cs
│   │   ├── HeartbeatDto.cs
│   │   └── RuntimeStatusDto.cs
│   ├── wwwroot/               # Static assets
│   │   ├── css/
│   │   └── js/
│   ├── Program.cs             # WebApplication entry point
│   └── _Imports.razor         # Global using directives
```

### Structure Rationale

- **OpenAnima.WebUI as separate project:** Clean separation between runtime and UI concerns, allows independent testing and deployment
- **Services/ folder:** Abstraction layer prevents Blazor components from directly coupling to runtime internals, enables easier testing with mocks
- **Hubs/ folder:** SignalR hubs for real-time communication, separate from components for clarity
- **BackgroundServices/ folder:** IHostedService implementations that push data to clients, runs alongside Blazor Server
- **Models/ folder:** DTOs prevent exposing internal runtime types to UI, allows versioning UI contracts independently

## Architectural Patterns

### Pattern 1: Service Facade

**What:** Wrap existing runtime components (HeartbeatLoop, PluginRegistry) in service interfaces that expose only UI-relevant operations.

**When to use:** When integrating UI with existing domain logic that wasn't designed for external consumption.

**Trade-offs:**
- **Pros:** Decouples UI from runtime internals, enables testing, prevents UI from breaking runtime invariants
- **Cons:** Additional layer of indirection, DTOs require mapping

**Example:**
```csharp
// Service abstraction
public interface IRuntimeService
{
    Task<HeartbeatStatusDto> GetHeartbeatStatusAsync();
    Task StartHeartbeatAsync();
    Task StopHeartbeatAsync();
    bool IsRunning { get; }
}

public class RuntimeService : IRuntimeService
{
    private readonly HeartbeatLoop _heartbeat;

    public RuntimeService(HeartbeatLoop heartbeat)
    {
        _heartbeat = heartbeat;
    }

    public Task<HeartbeatStatusDto> GetHeartbeatStatusAsync()
    {
        return Task.FromResult(new HeartbeatStatusDto
        {
            IsRunning = _heartbeat.IsRunning,
            TickCount = _heartbeat.TickCount,
            SkippedCount = _heartbeat.SkippedCount
        });
    }

    public Task StartHeartbeatAsync() => _heartbeat.StartAsync();
    public Task StopHeartbeatAsync() => _heartbeat.StopAsync();
    public bool IsRunning => _heartbeat.IsRunning;
}
```

### Pattern 2: Background Service + IHubContext Push

**What:** IHostedService monitors runtime state and pushes updates to all connected Blazor clients via IHubContext<THub>.

**When to use:** When backend state changes need to be broadcast to all connected clients without client polling.

**Trade-offs:**
- **Pros:** True real-time updates, no polling overhead, server-initiated push
- **Cons:** Requires careful lifetime management (singleton service accessing singleton hub context), potential memory leaks if not disposed properly

**Example:**
```csharp
public class RuntimeMonitorService : BackgroundService
{
    private readonly IHubContext<RuntimeHub> _hubContext;
    private readonly IRuntimeService _runtimeService;
    private readonly ILogger<RuntimeMonitorService> _logger;

    public RuntimeMonitorService(
        IHubContext<RuntimeHub> hubContext,
        IRuntimeService runtimeService,
        ILogger<RuntimeMonitorService> logger)
    {
        _hubContext = hubContext;
        _runtimeService = runtimeService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var status = await _runtimeService.GetHeartbeatStatusAsync();

                // Broadcast to all connected clients
                await _hubContext.Clients.All.SendAsync(
                    "HeartbeatUpdate",
                    status,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting heartbeat update");
            }
        }
    }
}
```

### Pattern 3: Component Lifecycle + SignalR Subscription

**What:** Blazor components subscribe to SignalR hub events in OnInitializedAsync and call StateHasChanged() to trigger re-render when updates arrive.

**When to use:** For real-time UI updates driven by server-side events.

**Trade-offs:**
- **Pros:** Automatic UI updates, no manual polling, clean component code
- **Cons:** Must properly dispose subscriptions to avoid memory leaks, StateHasChanged() must be called on UI thread

**Example:**
```csharp
@page "/heartbeat"
@inject NavigationManager Navigation
@implements IAsyncDisposable

<h3>Heartbeat Monitor</h3>
<p>Status: @(_status?.IsRunning == true ? "Running" : "Stopped")</p>
<p>Ticks: @_status?.TickCount</p>
<p>Skipped: @_status?.SkippedCount</p>

@code {
    private HubConnection? _hubConnection;
    private HeartbeatStatusDto? _status;

    protected override async Task OnInitializedAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/runtimeHub"))
            .Build();

        _hubConnection.On<HeartbeatStatusDto>("HeartbeatUpdate", status =>
        {
            _status = status;
            InvokeAsync(StateHasChanged); // Must invoke on UI thread
        });

        await _hubConnection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
```

### Pattern 4: Singleton Runtime + Scoped UI Services

**What:** Runtime components (HeartbeatLoop, PluginRegistry, EventBus) registered as singletons, service facades also singleton, but Blazor components use scoped lifetime per circuit.

**When to use:** When UI needs to interact with long-lived backend services.

**Trade-offs:**
- **Pros:** Single runtime instance shared across all users, efficient resource usage
- **Cons:** Must be thread-safe, state changes affect all users (appropriate for single-user desktop app)

**Example:**
```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Existing runtime components as singletons
builder.Services.AddSingleton<EventBus>();
builder.Services.AddSingleton<PluginRegistry>();
builder.Services.AddSingleton<HeartbeatLoop>();

// Service facades as singletons
builder.Services.AddSingleton<IRuntimeService, RuntimeService>();
builder.Services.AddSingleton<IModuleService, ModuleService>();

// Background service
builder.Services.AddHostedService<RuntimeMonitorService>();

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// SignalR (implicit with Blazor Server, but can configure)
builder.Services.AddSignalR();

var app = builder.Build();

// Initialize runtime on startup
var runtime = app.Services.GetRequiredService<IRuntimeService>();
await runtime.StartHeartbeatAsync();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHub<RuntimeHub>("/runtimeHub");

app.Run();
```

## Data Flow

### Request Flow (User Action → Runtime)

```
[User clicks "Start Heartbeat"]
    ↓
[Blazor Component] → @onclick handler
    ↓
[Inject IRuntimeService] → StartHeartbeatAsync()
    ↓
[RuntimeService] → _heartbeat.StartAsync()
    ↓
[HeartbeatLoop] → Starts PeriodicTimer
    ↓
[Background Service] → Detects state change
    ↓
[IHubContext] → Broadcasts to all clients
    ↓
[SignalR] → Pushes to browser
    ↓
[Component] → Receives event, calls StateHasChanged()
    ↓
[UI Updates] → Button changes to "Stop"
```

### Real-Time Update Flow (Runtime → UI)

```
[HeartbeatLoop] → Executes tick
    ↓
[RuntimeMonitorService] → Polls status every 500ms
    ↓
[IHubContext<RuntimeHub>] → Clients.All.SendAsync("HeartbeatUpdate", status)
    ↓
[SignalR WebSocket] → Pushes to all connected browsers
    ↓
[HubConnection.On<T>] → Component receives event
    ↓
[InvokeAsync(StateHasChanged)] → Triggers re-render on UI thread
    ↓
[Blazor Renderer] → Diffs virtual DOM, sends updates to browser
    ↓
[Browser] → Updates displayed tick count
```

### Module Control Flow

```
[User clicks "Load Module"]
    ↓
[Component] → Calls IModuleService.LoadModuleAsync(path)
    ↓
[ModuleService] → _loader.LoadModule(path)
    ↓
[PluginLoader] → Creates AssemblyLoadContext, loads assembly
    ↓
[PluginRegistry] → Registers module
    ↓
[EventBus] → Property injection into module
    ↓
[ModuleService] → Returns ModuleDto
    ↓
[Component] → Updates module list
    ↓
[Background Service] → Detects registry change
    ↓
[SignalR] → Broadcasts "ModuleLoaded" event
    ↓
[All Clients] → Refresh module list
```

### Key Data Flows

1. **Heartbeat monitoring:** Background service polls HeartbeatLoop every 500ms, broadcasts status via SignalR to all connected clients
2. **Module operations:** User actions invoke service methods, which delegate to existing runtime, then broadcast changes to all clients
3. **Event bus integration:** UI can subscribe to domain events via EventBus, display in real-time event log component

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| Single user (v1.1) | Current architecture is perfect - singleton runtime, single browser window, localhost-only |
| Multiple concurrent users | Not applicable - this is a desktop app, not a multi-tenant SaaS |
| Multiple agents per user | Future consideration - would need per-agent runtime instances, scoped services per circuit |

### Scaling Priorities

1. **First bottleneck:** SignalR broadcast frequency - if pushing updates every 100ms (heartbeat interval), may cause UI jank. Mitigation: Throttle broadcasts to 500ms or use client-side interpolation.
2. **Second bottleneck:** Module count - if 100+ modules loaded, registry queries may slow down. Mitigation: Cache module DTOs, only broadcast deltas instead of full list.

## Anti-Patterns

### Anti-Pattern 1: Direct Runtime Access from Components

**What people do:** Inject HeartbeatLoop or PluginRegistry directly into Blazor components.

**Why it's wrong:**
- Couples UI to runtime internals
- Exposes dangerous operations (e.g., direct registry manipulation)
- Makes testing difficult (can't mock concrete classes)
- Breaks encapsulation

**Do this instead:** Always use service facades (IRuntimeService, IModuleService) that expose only safe, UI-relevant operations.

### Anti-Pattern 2: Polling from Components

**What people do:** Use Timer or PeriodicTimer inside Blazor components to poll runtime state.

**Why it's wrong:**
- Each connected client creates its own timer (wasteful)
- Polling interval must balance responsiveness vs overhead
- Doesn't scale to multiple users
- Misses updates between polls

**Do this instead:** Use Background Service + IHubContext to push updates from server to all clients. Single timer, server-initiated push.

### Anti-Pattern 3: Scoped Runtime Services

**What people do:** Register HeartbeatLoop or PluginRegistry as scoped services.

**Why it's wrong:**
- Creates new runtime instance per Blazor circuit (per browser tab)
- Multiple heartbeat loops running simultaneously
- Module registry not shared across tabs
- Wastes resources, causes confusion

**Do this instead:** Register runtime components as singletons. This is a single-user desktop app - one runtime instance for the entire application.

### Anti-Pattern 4: Forgetting StateHasChanged() in SignalR Callbacks

**What people do:** Update component state in SignalR event handler without calling StateHasChanged().

**Why it's wrong:**
- UI doesn't update because Blazor doesn't know state changed
- SignalR callbacks execute on background thread, not UI thread
- Leads to "why isn't my UI updating?" debugging sessions

**Do this instead:** Always wrap state updates in InvokeAsync(StateHasChanged) to marshal to UI thread and trigger re-render.

### Anti-Pattern 5: Browser Auto-Launch with Process.Start(url)

**What people do:** Call Process.Start("http://localhost:5000") directly.

**Why it's wrong:**
- Throws PlatformNotSupportedException on Linux
- Requires UseShellExecute = true on Windows
- Doesn't handle default browser detection properly

**Do this instead:** Use platform-specific launcher:
```csharp
private static void OpenBrowser(string url)
{
    try
    {
        Process.Start(url);
    }
    catch
    {
        // Workaround for .NET Core on Windows/Linux
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (OperatingSystem.IsLinux())
        {
            Process.Start("xdg-open", url);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", url);
        }
    }
}
```

## Integration Points

### Existing Runtime → WebUI

| Integration Point | Pattern | Notes |
|-------------------|---------|-------|
| HeartbeatLoop | Service Facade (RuntimeService) | Wrap in IRuntimeService, expose Start/Stop/Status only |
| PluginRegistry | Service Facade (ModuleService) | Wrap in IModuleService, expose queries and load/unload operations |
| EventBus | Direct Injection | Can inject into services for event subscription, display in UI event log |
| PluginLoader | Via ModuleService | Don't expose directly, wrap load operations in service |

### WebUI → Browser

| Integration Point | Pattern | Notes |
|-------------------|---------|-------|
| Real-time updates | SignalR (built into Blazor Server) | Automatic WebSocket connection per circuit |
| Component rendering | Blazor Server render mode | Server-side rendering with SignalR sync |
| Static assets | wwwroot/ folder | CSS, JS, images served by ASP.NET Core static files middleware |

### Background Service → SignalR

| Integration Point | Pattern | Notes |
|-------------------|---------|-------|
| Broadcast to all clients | IHubContext<RuntimeHub> | Inject into IHostedService, call Clients.All.SendAsync() |
| Targeted updates | IHubContext with connection ID | Can send to specific clients if needed (future) |

### Hosting Model

| Component | Lifetime | Registration |
|-----------|----------|--------------|
| EventBus | Singleton | Shared across entire application |
| PluginRegistry | Singleton | Shared across entire application |
| HeartbeatLoop | Singleton | Single instance, started on app startup |
| RuntimeService | Singleton | Facade over singleton runtime |
| ModuleService | Singleton | Facade over singleton registry |
| RuntimeMonitorService | Singleton (IHostedService) | Background worker, started automatically |
| Blazor Components | Scoped (per circuit) | New instance per browser connection |
| HubConnection (client-side) | Per component | Created in OnInitializedAsync, disposed in DisposeAsync |

## Build Order Considering Dependencies

### Phase 1: Service Abstraction Layer (No UI Yet)
1. Create OpenAnima.WebUI project
2. Add Models/ folder with DTOs (ModuleDto, HeartbeatStatusDto, RuntimeStatusDto)
3. Create Services/IRuntimeService.cs interface
4. Implement Services/RuntimeService.cs wrapping HeartbeatLoop
5. Create Services/IModuleService.cs interface
6. Implement Services/ModuleService.cs wrapping PluginRegistry
7. **Validation:** Unit test services with mock runtime components

### Phase 2: ASP.NET Core Host (Console → Web)
1. Modify OpenAnima.Core/Program.cs to use WebApplicationBuilder
2. Register runtime components as singletons
3. Register service facades as singletons
4. Configure Kestrel to listen on localhost:5000
5. Add browser auto-launch on startup
6. **Validation:** App starts, browser opens, shows "Hello World" page

### Phase 3: Basic Blazor UI (Static Display)
1. Add Blazor Server packages to OpenAnima.WebUI
2. Create Components/Layout/MainLayout.razor
3. Create Components/Pages/Dashboard.razor (static, no real-time yet)
4. Create Components/Pages/Modules.razor (static list)
5. Create Components/Pages/Heartbeat.razor (static status)
6. Configure routing and render modes
7. **Validation:** Can navigate between pages, see static data

### Phase 4: SignalR Real-Time Updates
1. Create Hubs/RuntimeHub.cs
2. Create BackgroundServices/RuntimeMonitorService.cs
3. Register IHostedService in Program.cs
4. Update Dashboard.razor to subscribe to SignalR events
5. Update Heartbeat.razor to receive real-time tick updates
6. **Validation:** UI updates automatically when heartbeat ticks

### Phase 5: Control Operations
1. Add Start/Stop buttons to Heartbeat.razor
2. Wire buttons to IRuntimeService methods
3. Add Load/Unload module UI to Modules.razor
4. Wire module operations to IModuleService
5. Broadcast operation results via SignalR
6. **Validation:** Can control runtime from UI, all clients see updates

### Phase 6: Polish & Error Handling
1. Add loading states and spinners
2. Add error toast notifications
3. Add confirmation dialogs for destructive operations
4. Style with CSS (minimal, functional)
5. Add keyboard shortcuts (optional)
6. **Validation:** Smooth UX, graceful error handling

## Sources

- [ASP.NET Core Blazor hosting models](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models) (MEDIUM confidence - couldn't fetch, but standard Microsoft docs)
- [Task Scheduling and Background Services in Blazor Server](https://blazorise.com/blog/task-scheduling-and-background-services-in-blazor-server) (MEDIUM confidence - couldn't fetch)
- [How to Build Real-Time Dashboards with SignalR and Blazor](https://oneuptime.com/blog/post/2026-01-25-real-time-dashboards-signalr-blazor/view) (MEDIUM confidence - couldn't fetch, but recent 2026 article)
- [Background Service Communication with Blazor via SignalR](https://medium.com/it-dead-inside/lets-learn-blazor-background-service-communication-with-blazor-via-signalr-84abe2660fd6) (MEDIUM confidence - from search results)
- [Host ASP.NET Core SignalR in background services](https://learn.microsoft.com/en-us/aspnet/core/signalr/background-services) (HIGH confidence - official Microsoft pattern)
- [Integrating Blazor with Existing .NET Web Apps](https://visualstudiomagazine.com/articles/2024/08/07/integrating-blazor-with-existing-,-d-,net-web-apps.aspx) (MEDIUM confidence - from search results)
- [How do I host a WebApplication and a BackgroundService in the same application](https://stackoverflow.com/questions/74132325/how-do-i-host-a-webapplication-and-a-backgroundservice-in-the-same-application) (HIGH confidence - Stack Overflow pattern)

---
*Architecture research for: Blazor Server WebUI integration with existing .NET 8 runtime*
*Researched: 2026-02-22*
