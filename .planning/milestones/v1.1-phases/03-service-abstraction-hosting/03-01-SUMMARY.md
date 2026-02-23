# Plan 03-01 Summary: Service Facades & Web Host

**Completed:** 2026-02-22
**Duration:** ~5 min

## What Changed

Converted OpenAnima.Core from console application to Blazor Server web host. Created three service facade interfaces (IModuleService, IHeartbeatService, IEventBusService) with implementations that wrap existing runtime components. Added OpenAnimaHostedService to manage the runtime lifecycle (module loading, heartbeat, directory watching) within the ASP.NET Core hosting model.

## Key Decisions

- **Web SDK directly on Core project** — no separate web project; OpenAnima.Core becomes the host
- **Pure CSS, no component library** — lightweight for Phase 3 shell; can add MudBlazor later if needed
- **Removed MediatR package** — was unused tech debt from v1.0 (custom EventBus used instead)
- **IHostedService for lifecycle** — clean integration with ASP.NET Core startup/shutdown

## Files

### New (7)
- `Services/IModuleService.cs` — module operations interface
- `Services/IHeartbeatService.cs` — heartbeat operations interface
- `Services/IEventBusService.cs` — event bus wrapper interface
- `Services/ModuleService.cs` — module facade implementation
- `Services/HeartbeatService.cs` — heartbeat facade implementation
- `Services/EventBusService.cs` — event bus facade implementation
- `Hosting/OpenAnimaHostedService.cs` — runtime lifecycle management

### Modified (2)
- `OpenAnima.Core.csproj` — SDK changed to Web, removed MediatR
- `Program.cs` — rewritten for WebApplication pipeline
