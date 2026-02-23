# Phase 4: Blazor UI with Static Display - Context

**Gathered:** 2026-02-22
**Status:** Ready for planning

<domain>
## Phase Boundary

User can view module and heartbeat status via web dashboard. Delivers module list page, heartbeat status page, and dashboard overview with responsive layout. Real-time updates (Phase 5) and control operations (Phase 6) are out of scope — this phase is static display only.

</domain>

<decisions>
## Implementation Decisions

### Module List Display
- Card grid layout for modules
- Card front shows: name, version, status indicator (dot + text)
- Status indicators: green dot + "Loaded", red dot + "Error"
- Click card opens modal dialog with full details
- Modal shows: complete metadata (name, version, description, author) + runtime info (load time, file path)
- Cards sorted by load order (first loaded appears first)
- Empty state: icon + text prompt ("No modules loaded")

### Page Structure & Navigation
- Three-page structure: Dashboard overview, Modules page, Heartbeat page
- Sidebar navigation with three items: Dashboard, Modules, Heartbeat
- Dashboard overview shows numeric summary cards (module count, heartbeat state, tick count)
- Active navigation item highlighted for current page

### Heartbeat Status Display
- Prominent status card at top: large Running/Stopped with green/red visual treatment
- Statistics below status card in vertical layout (top-down)
- Stats shown: tick count, skipped count (data currently exposed by HeartbeatService)
- When heartbeat not started: static prompt, stats show 0 or --

### Responsive Layout Strategy
- Desktop-first approach (primary use case is desktop browser monitoring)
- Single breakpoint: desktop vs mobile (~768px)
- Narrow screen: sidebar hidden, hamburger menu button to toggle
- Module card grid collapses to single column on narrow screens
- CSS framework: Bootstrap (good Blazor ecosystem support)

### Claude's Discretion
- Exact Bootstrap version and integration approach
- Modal dialog styling details
- Summary card visual design on Dashboard
- Icon choices for navigation and empty states
- Exact hamburger menu animation/behavior

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

- Multi-Anima instance management — each Anima with its own modules and heartbeat, Dashboard showing all Anima or individual Anima data. Significant architectural change, belongs in a future milestone.

</deferred>

---

*Phase: 04-blazor-ui-with-static-display*
*Context gathered: 2026-02-22*
