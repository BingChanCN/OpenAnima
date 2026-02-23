# Phase 3: Service Abstraction & Hosting - Research

**Researched:** 2026-02-22
**Status:** Complete

## Blazor Server Hosting Model

### How It Works
- Blazor Server runs on ASP.NET Core, serving Razor components over a SignalR circuit
- UI events travel from browser to server; server renders diffs and sends them back
- Single process hosts both the runtime logic and the web UI — ideal for OpenAnima
- `WebApplication.CreateBuilder()` replaces the console app entry point
- `builder.Services.AddRazorComponents().AddInteractiveServerComponents()` enables Blazor Server (.NET 8 unified model)

### Project SDK Change
- Current: `Microsoft.NET.Sdk` with `OutputType: Exe`
- Target: `Microsoft.NET.Sdk.Web` (includes Kestrel, static files, Razor compilation)
- Removing `OutputType: Exe` — Web SDK defaults to executable
- No separate web project needed; OpenAnima.Core becomes the web host directly

### Key NuGet Packages
- No additional packages needed — `Microsoft.NET.Sdk.Web` includes everything:
  - Kestrel web server
  - Razor component compilation
  - Static file serving
  - SignalR (for Blazor Server circuits)

## Service Facade Pattern

### Why Facades
- Current code directly instantiates PluginRegistry, EventBus, HeartbeatLoop in Program.cs
- Blazor components need access to these via DI (constructor injection)
- Facades provide clean interfaces that decouple UI from runtime internals
- Enables future testing and alternative implementations

### Facade Design
Three service interfaces in OpenAnima.Core:

1. **IModuleService** — wraps PluginRegistry + PluginLoader
   - GetAllModules(), GetModule(id), LoadModule(path), UnloadModule(id)
   - Exposes module state without leaking PluginLoadContext details

2. **IHeartbeatService** — wraps HeartbeatLoop
   - StartAsync(), StopAsync(), IsRunning, TickCount, SkippedCount
   - Clean async API for UI consumption

3. **IEventBusService** — wraps EventBus (thin wrapper for now)
   - Primarily for future phases (event monitoring in Phase 5)
   - Registered as singleton alongside the actual EventBus

### DI Registration
- All three services registered as singletons (single runtime instance)
- Underlying components (EventBus, PluginRegistry, etc.) also singleton
- HeartbeatLoop auto-starts via IHostedService pattern

## Browser Auto-Launch

### Implementation
- Use `IHostApplicationLifetime.ApplicationStarted` callback
- Call `Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true })`
- Works cross-platform (opens system default browser)
- Respect `--no-browser` CLI flag via `args` check

### Port Selection
- Default: `http://localhost:5000`
- Kestrel configuration via `builder.WebHost.UseUrls()`
- Port fallback: catch `AddressInUseException`, increment port, retry

## Dark Theme Layout

### Approach: Pure CSS (no component library)
- MudBlazor/Radzen add significant bundle size and learning curve
- Phase 3 only needs a layout shell — no complex components yet
- Pure CSS with CSS variables for theming is lightweight and sufficient
- Can add a component library later if Phase 4-6 complexity warrants it

### Layout Structure
- `MainLayout.razor` — full-page layout with sidebar + content area
- Sidebar: collapsible via CSS toggle, navigation links
- Top bar: logo placeholder, app title
- Content area: `@Body` renders routed pages
- Dark theme: CSS variables for background, text, accent colors

## Startup Sequence

### Current (Console)
1. Create LoggerFactory, EventBus
2. Create PluginRegistry, PluginLoader
3. Scan and load modules
4. Inject EventBus into modules
5. Start HeartbeatLoop
6. Start ModuleDirectoryWatcher
7. Console.ReadLine() blocks until exit
8. Cleanup

### Target (Blazor Server)
1. `WebApplication.CreateBuilder(args)`
2. Register services (EventBus, PluginRegistry, PluginLoader, facades)
3. Add Blazor Server services
4. `app.Build()`
5. Configure middleware (static files, routing, Blazor)
6. `OpenAnimaHostedService` (IHostedService) handles:
   - Module scanning and loading
   - EventBus injection
   - HeartbeatLoop start
   - ModuleDirectoryWatcher start
7. Browser auto-launch on ApplicationStarted
8. `app.Run()` — Kestrel serves until Ctrl+C
9. IHostedService.StopAsync handles cleanup

---

*Phase: 03-service-abstraction-hosting*
*Research completed: 2026-02-22*
