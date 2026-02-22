# Plan 03-02 Summary: Blazor Layout Shell & Browser Auto-Launch

**Completed:** 2026-02-22
**Duration:** ~4 min

## What Changed

Created the Blazor Server component infrastructure: root App.razor, Routes.razor, _Imports.razor. Built a dark-themed layout with collapsible sidebar navigation. Added a placeholder Dashboard page at "/" that displays heartbeat status and loaded modules. Implemented browser auto-launch on startup with --no-browser CLI flag support.

## Key Decisions

- **Dark theme via CSS variables** — monitoring tool aesthetic, easy to maintain
- **Collapsible sidebar** — screen real estate flexibility per user preference
- **Dashboard shows live data immediately** — injects IModuleService and IHeartbeatService
- **Process.Start for browser launch** — cross-platform, uses system default browser
- **IServerAddressesFeature** — resolves actual bound URL after Kestrel starts

## Files

### New (7)
- `Components/App.razor` — root HTML document with Blazor script
- `Components/Routes.razor` — router configuration
- `Components/_Imports.razor` — global using directives
- `Components/Layout/MainLayout.razor` — dark sidebar layout
- `Components/Layout/MainLayout.razor.css` — layout scoped styles
- `Components/Pages/Dashboard.razor` — placeholder dashboard
- `Components/Pages/Dashboard.razor.css` — dashboard scoped styles
- `wwwroot/css/app.css` — global dark theme and CSS variables

### Modified (1)
- `Program.cs` — added browser auto-launch logic
