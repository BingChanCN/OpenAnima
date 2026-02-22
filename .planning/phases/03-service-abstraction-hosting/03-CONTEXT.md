# Phase 3: Service Abstraction & Hosting - Context

**Gathered:** 2026-02-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Runtime transitions from console application to Blazor Server web host. Launches as web application on localhost with browser auto-open. All v1.0 functionality (module loading, event bus, heartbeat) continues working through service facades. No new user-facing features — this is the hosting foundation for Phases 4-7.

</domain>

<decisions>
## Implementation Decisions

### Runtime mode
- Web-only mode — no console fallback, fully commit to Blazor Server hosting
- Console window stays visible but minimal output: show port number and how to close
- Shutdown via Ctrl+C only (no in-browser shutdown button)
- System tray integration deferred to v1.2 (DESK-02)

### Browser launch behavior
- Default port (e.g. 5000) with automatic fallback to next available port if occupied
- Auto-open system default browser immediately when web service is ready
- Provide --no-browser CLI flag to disable auto-open (for CI/server scenarios)
- Use system default browser (Process.Start URL approach)

### Landing page & layout
- Layout framework with placeholder content — Phase 4 fills in actual dashboard
- Logo in top-left corner
- Collapsible sidebar navigation on the left side
- Dark theme (dark background + light text, monitoring tool aesthetic)

### Claude's Discretion
- UI component library choice (MudBlazor, Radzen, pure CSS, etc.)
- Exact port number for default
- Service facade granularity and interface design
- Startup sequence and initialization order
- Error page design

</decisions>

<specifics>
## Specific Ideas

- User wants the feel of a monitoring/control tool — dark theme fits this
- Collapsible sidebar suggests they want screen real estate flexibility
- Minimal console output: just enough to know it's running and how to stop it

</specifics>

<deferred>
## Deferred Ideas

- System tray icon with minimize-to-tray — DESK-02, planned for v1.2

</deferred>

---

*Phase: 03-service-abstraction-hosting*
*Context gathered: 2026-02-22*
