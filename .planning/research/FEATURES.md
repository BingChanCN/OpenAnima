# Feature Research: WebUI Runtime Dashboard

**Domain:** Runtime monitoring dashboard for modular agent platform
**Researched:** 2026-02-22
**Confidence:** HIGH
**Milestone:** v1.1 WebUI Runtime Dashboard (subsequent milestone)

## Context

This research focuses ONLY on features for the runtime monitoring dashboard. The core platform (module loading, event bus, heartbeat loop) is already built in v1.0. This milestone adds a Blazor Server WebUI for real-time monitoring and control.

**Existing runtime APIs (already available):**
- ModuleRegistry: GetAllModules(), GetModule(name)
- ModuleLoader: LoadModule(path), UnloadModule(name)
- HeartbeatLoop: Start(), Stop(), IsRunning, TickCount, LastTickDuration
- EventBus: Publish(), Subscribe()
- IModuleMetadata: Name, Version, Description, Author, Dependencies

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist in a runtime monitoring dashboard. Missing these = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|----------|
| Module list display | Core purpose — users need to see what's loaded | LOW | Read from ModuleRegistry.GetAllModules(), display in table/cards |
| Module status indicators | Visual confirmation of loaded/unloaded/error states | LOW | Color-coded badges (green=loaded, gray=unloaded, red=error), maps to LoadResult |
| Module metadata display | Users need context — what is this module? | LOW | Show Name, Version, Description, Author from IModuleMetadata |
| Load module control | Users expect to add modules at runtime | MEDIUM | File picker → ModuleLoader.LoadModule(path), handle LoadResult |
| Unload module control | Users expect to remove modules at runtime | LOW | Button per module → ModuleLoader.UnloadModule(name) |
| Heartbeat status display | Show if runtime loop is active | LOW | Display HeartbeatLoop.IsRunning as Running/Stopped badge |
| Start/Stop heartbeat controls | Users expect to pause/resume the runtime | LOW | Buttons calling HeartbeatLoop.Start()/Stop() |
| Heartbeat tick counter | Proof the loop is running — users expect a live counter | LOW | Display HeartbeatLoop.TickCount, updates in real-time |
| Real-time updates without refresh | Modern dashboards auto-update — manual refresh feels broken | MEDIUM | SignalR hub pushing updates on state changes (module load/unload, heartbeat tick) |
| Error display | When operations fail, users need to know why | LOW | Show LoadResult.Error messages in toast/alert, heartbeat exceptions |
| Responsive layout | Users expect dashboards to work on different screen sizes | LOW | Blazor responsive grid, mobile-friendly (even if Windows-first) |

### Differentiators (Competitive Advantage)

Features that set the dashboard apart. Not required, but valuable.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Heartbeat latency display | Performance insight — shows if loop is keeping up with 100ms target | LOW | Display HeartbeatLoop.LastTickDuration, color-code if >100ms (yellow warning, red critical) |
| Module dependency visualization | Helps users understand module relationships | MEDIUM | Parse IModuleMetadata.Dependencies, display as tree/graph |
| Event bus activity monitor | Real-time visibility into inter-module communication | MEDIUM | Subscribe to EventBus, display recent events (type, timestamp, source module) |
| Module load history | Audit trail of what was loaded/unloaded when | LOW | Log LoadModule/UnloadModule calls with timestamp, display in timeline |
| Heartbeat tick history chart | Visual trend of loop performance over time | MEDIUM | Store recent LastTickDuration values, display as line chart (last 100 ticks) |
| Module hot reload indicator | Show which modules support hot reload | LOW | Display IModuleMetadata flag, visual indicator on module card |
| Auto-launch on Windows startup | Desktop app experience — runtime starts with OS | MEDIUM | Windows Task Scheduler integration, browser auto-launch |
| System tray integration | Minimize to tray, quick access to dashboard | MEDIUM | NotifyIcon, context menu with Open Dashboard/Exit |
| Dark mode | User preference, reduces eye strain | LOW | CSS theme toggle, persisted in localStorage |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good but create problems for a runtime monitoring dashboard.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Module configuration editor | "I want to configure modules from UI" | Each module has different config schema, generic editor is complex | Document "modules configure via appsettings.json", defer to future milestone |
| Real-time event bus message inspection | "I want to see message payloads" | Privacy concern (sensitive data), performance impact (serialize everything), UI clutter | Show event types/counts only, add detailed logging to future milestone |
| Module code editor | "I want to edit module code in browser" | Scope creep into IDE territory, compilation complexity, debugging nightmare | Use VS Code/Visual Studio, hot reload handles updates |
| Historical data persistence | "I want to see last week's metrics" | Database complexity, storage growth, not core to runtime monitoring | Show current session only, defer analytics to future milestone |
| Multi-runtime management | "I want to monitor multiple agent instances" | Networking complexity, authentication, not v1.1 scope | Single runtime only, defer to future milestone |
| Module marketplace integration | "I want to browse/install modules from UI" | Marketplace doesn't exist yet, premature feature | Manual file picker for v1.1, marketplace is separate product |
| Custom dashboard layouts | "I want to rearrange widgets" | Complexity explosion, not needed for fixed set of metrics | Fixed layout, well-designed default is enough |
| Alerting/notifications | "I want alerts when modules fail" | Notification infrastructure, persistence, configuration UI | Show errors in dashboard, user monitors actively for v1.1 |

## Feature Dependencies

```
Blazor Server app
    └──requires──> SignalR hub (for real-time updates)
                       └──requires──> Runtime state change events

Module list display
    └──requires──> ModuleRegistry API (already exists)

Module load/unload controls
    └──requires──> ModuleLoader API (already exists)
    └──requires──> Error handling UI

Heartbeat monitoring
    └──requires──> HeartbeatLoop API (already exists)
    └──requires──> Real-time updates (SignalR)

Event bus activity monitor
    └──requires──> EventBus subscription API (already exists)
    └──enhances──> Module debugging

Heartbeat latency display
    └──requires──> Heartbeat tick counter (base feature)

Module dependency visualization
    └──requires──> Module metadata display (base feature)

Auto-launch on startup
    └──requires──> Background service hosting
    └──requires──> Browser auto-launch logic

System tray integration
    └──requires──> Background service hosting
    └──conflicts──> Console app (needs Windows Forms/WPF for NotifyIcon)
```

### Dependency Notes

- **SignalR hub is foundational:** Real-time updates are table stakes, so SignalR hub must be implemented early
- **Runtime APIs already exist:** All core functionality (module loading, heartbeat control) is available, dashboard is pure UI layer
- **System tray conflicts with console app:** Current runtime is console app, tray requires Windows Forms/WPF NotifyIcon (or third-party library)
- **Event bus monitoring enhances debugging:** Not required for basic monitoring, but valuable for understanding module interactions

## MVP Definition

### Launch With (v1.1)

Minimum viable dashboard — what's needed to monitor and control the runtime.

- [x] Module list display with status indicators
- [x] Module metadata display (name, version, description, author)
- [x] Load module control (file picker)
- [x] Unload module control (button per module)
- [x] Heartbeat status display (running/stopped)
- [x] Start/Stop heartbeat controls
- [x] Heartbeat tick counter (real-time)
- [x] Heartbeat latency display (performance insight)
- [x] Real-time updates via SignalR (no manual refresh)
- [x] Error display (toast notifications)
- [x] Responsive layout (works on different screen sizes)
- [x] Auto-launch browser when runtime starts

### Add After Validation (v1.2+)

Features to add once core dashboard is working and validated.

- [ ] Event bus activity monitor — valuable for debugging, not critical for launch
- [ ] Module dependency visualization — nice-to-have, not blocking
- [ ] Heartbeat tick history chart — visual trend analysis, defer until latency display is validated
- [ ] Module load history — audit trail, not urgent
- [ ] Dark mode — user preference, easy to add later
- [ ] System tray integration — desktop app polish, requires architecture change (Windows Forms/WPF)

### Future Consideration (v2+)

Features to defer until dashboard is proven and user feedback is gathered.

- [ ] Module configuration editor — complex, needs schema system
- [ ] Historical data persistence — database complexity, not core to monitoring
- [ ] Multi-runtime management — networking complexity, separate product
- [ ] Custom dashboard layouts — over-engineering for fixed metrics
- [ ] Alerting/notifications — infrastructure complexity

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority | Rationale |
|---------|------------|---------------------|----------|-----------|
| Module list display | HIGH | LOW | P1 | Core purpose of dashboard |
| Module status indicators | HIGH | LOW | P1 | Visual confirmation is essential |
| Load/Unload controls | HIGH | MEDIUM | P1 | Basic lifecycle management |
| Heartbeat status/controls | HIGH | LOW | P1 | Core monitoring functionality |
| Heartbeat tick counter | HIGH | LOW | P1 | Proof loop is running |
| Real-time updates (SignalR) | HIGH | MEDIUM | P1 | Modern UX expectation |
| Error display | HIGH | LOW | P1 | Users need failure feedback |
| Heartbeat latency display | MEDIUM | LOW | P1 | Performance insight, easy win |
| Responsive layout | MEDIUM | LOW | P1 | Basic web standard |
| Auto-launch browser | MEDIUM | LOW | P1 | Desktop app experience |
| Event bus activity monitor | MEDIUM | MEDIUM | P2 | Debugging value, not critical |
| Module dependency viz | LOW | MEDIUM | P2 | Nice-to-have, not urgent |
| Heartbeat history chart | LOW | MEDIUM | P2 | Visual polish, defer |
| Module load history | LOW | LOW | P2 | Audit trail, not urgent |
| Dark mode | LOW | LOW | P2 | User preference, easy later |
| System tray integration | MEDIUM | HIGH | P3 | Requires architecture change |
| Module config editor | LOW | HIGH | P3 | Complex, premature |
| Historical persistence | LOW | HIGH | P3 | Database complexity |

**Priority key:**
- P1: Must have for v1.1 launch (table stakes + easy differentiators)
- P2: Should have, add in v1.2+ (valuable but not blocking)
- P3: Nice to have, future consideration (complex or premature)

## Comparison: Runtime Monitoring Dashboard Patterns

Analyzed similar tools to understand common patterns:

| Feature | Windows Task Manager | Docker Desktop | VS Code Extensions | Kubernetes Dashboard | Our Approach |
|---------|---------------------|----------------|-------------------|---------------------|--------------|
| List view | Process list with columns | Container list with status | Extension list with enable/disable | Pod/service list | Module list with cards/table |
| Status indicators | Running/Suspended text | Green/red/yellow dots | Enabled/Disabled badges | Running/Pending/Failed colors | Color-coded badges (green/gray/red) |
| Real-time updates | Auto-refresh every 1s | SignalR push updates | Manual refresh | Auto-refresh configurable | SignalR push (no polling) |
| Control operations | End Task button | Start/Stop/Restart buttons | Enable/Disable/Uninstall | Scale/Delete buttons | Load/Unload/Start/Stop |
| Performance metrics | CPU/Memory graphs | CPU/Memory per container | None | CPU/Memory per pod | Heartbeat tick rate/latency |
| Detail view | Process details tab | Container logs/inspect | Extension details page | Pod logs/events | Module metadata panel |
| Search/filter | Search by name | Filter by status/name | Search by name/category | Filter by namespace/label | Search by name (future) |

**Key insights:**
- **Status indicators are universal:** Color-coded visual state is table stakes
- **Real-time updates vary:** Task Manager polls, Docker/K8s use WebSocket push — SignalR is right choice
- **Control operations are contextual:** Task Manager = kill, Docker = lifecycle, VS Code = enable/disable — we need load/unload + start/stop
- **Performance metrics differ:** Task Manager = system resources, Docker = container resources, we = heartbeat performance
- **Detail views are common:** All tools provide drill-down into individual items — we need module metadata panel

## Sources

- [How to Build Real-Time Dashboards with SignalR and Blazor](https://oneuptime.com/blog/post/2026-01-25-real-time-dashboards-signalr-blazor/view) — SignalR patterns for real-time updates
- [Real-Time Blazor Apps with SignalR and Blazorise Notifications](https://blazorise.com/blog/real-time-blazor-apps-signalr-and-blazorise-notifications) — Notification patterns
- [How to Use Docker Desktop Dashboard Effectively](https://oneuptime.com/blog/post/2026-02-08-how-to-use-docker-desktop-dashboard-effectively/view) — Container management UI patterns
- [Use extensions in Visual Studio Code](https://code.visualstudio.com/docs/getstarted/extensions) — Extension manager UI patterns
- [Windows Task Manager: The Complete Guide](https://www.howtogeek.com/405806/windows-task-manager-the-complete-guide/) — Process monitoring patterns
- [Status design pattern](https://ui-patterns.com/patterns/Status) — Status indicator best practices
- [4 Web Dashboard Mistakes to Avoid](https://www.caspio.com/blog/web-dashboard-mistakes/) — Dashboard anti-patterns
- PROJECT.md — Existing runtime APIs and constraints
- Training data on monitoring dashboards (Grafana, Prometheus, New Relic patterns)

---
*Feature research for: OpenAnima v1.1 WebUI Runtime Dashboard*
*Researched: 2026-02-22*
