# Phase 4: Blazor UI with Static Display - Research

**Researched:** 2026-02-22
**Domain:** Blazor Server UI Components, Responsive Layout, CSS Styling
**Confidence:** HIGH

## Summary

Phase 4 builds on the existing Blazor Server foundation from Phase 3 to create a three-page dashboard with static display of module and heartbeat data. The project already has a working Blazor Server setup with pure CSS dark theme, sidebar navigation, and service facades (IModuleService, IHeartbeatService) that expose all necessary data.

The implementation requires creating two new pages (Modules, Heartbeat), expanding the navigation, building a reusable modal component for module details, and implementing responsive layout with a single mobile breakpoint. The existing CSS architecture uses CSS custom properties for theming and scoped CSS files (.razor.css) for component-specific styles.

**Primary recommendation:** Extend the existing pure CSS approach with CSS Grid for card layouts, use Blazor's built-in NavLink component for navigation state, and build a lightweight modal component using RenderFragment for content projection. Bootstrap integration should be minimal (CDN for grid utilities only) to maintain the lightweight shell philosophy.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Module List Display:**
- Card grid layout for modules
- Card front shows: name, version, status indicator (dot + text)
- Status indicators: green dot + "Loaded", red dot + "Error"
- Click card opens modal dialog with full details
- Modal shows: complete metadata (name, version, description, author) + runtime info (load time, file path)
- Cards sorted by load order (first loaded appears first)
- Empty state: icon + text prompt ("No modules loaded")

**Page Structure & Navigation:**
- Three-page structure: Dashboard overview, Modules page, Heartbeat page
- Sidebar navigation with three items: Dashboard, Modules, Heartbeat
- Dashboard overview shows numeric summary cards (module count, heartbeat state, tick count)
- Active navigation item highlighted for current page

**Heartbeat Status Display:**
- Prominent status card at top: large Running/Stopped with green/red visual treatment
- Statistics below status card in vertical layout (top-down)
- Stats shown: tick count, skipped count (data currently exposed by HeartbeatService)
- When heartbeat not started: static prompt, stats show 0 or --

**Responsive Layout Strategy:**
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

### Deferred Ideas (OUT OF SCOPE)

- Multi-Anima instance management — each Anima with its own modules and heartbeat, Dashboard showing all Anima or individual Anima data. Significant architectural change, belongs in a future milestone.

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MOD-06 | User can view a list of all loaded modules with status indicators (loaded/error) | IModuleService.GetAllModules() provides PluginRegistryEntry list; card grid layout pattern researched; status indicator CSS patterns established |
| MOD-07 | User can view each module's metadata (name, version, description, author) | PluginRegistryEntry exposes Module.Metadata (IModuleMetadata) and Manifest (PluginManifest); modal component pattern researched for detail view |
| UI-01 | Dashboard layout adapts to different screen sizes (responsive) | CSS Grid auto-fit pattern for cards; media query @768px breakpoint; sidebar collapse pattern researched |

</phase_requirements>

## Standard Stack

### Core (Already in Place)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Blazor Server | .NET 8.0 | Interactive web UI framework | Already integrated in Phase 3; SignalR built-in for future real-time updates |
| Pure CSS | CSS3 | Styling and theming | Project decision: lightweight shell, no component library overhead |
| CSS Custom Properties | CSS3 | Dark theme variables | Already established in app.css with full color palette |

### Supporting (Recommended)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Bootstrap 5 | 5.3.x (CDN) | Grid utilities and responsive helpers | User decision: "good Blazor ecosystem support"; use sparingly for grid classes only |
| CSS Grid | Native | Card layout and responsive grids | Primary layout mechanism; already used in Dashboard.razor.css |
| CSS Flexbox | Native | Component-level layout | Already used extensively in existing components |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Bootstrap CDN | MudBlazor / Radzen | User explicitly chose "pure CSS dark theme (no component library)" for lightweight shell; can add later if needed |
| Custom modal | Bootstrap Modal JS | Avoid JavaScript dependencies; Blazor-native approach preferred for state management |
| Media queries | CSS Container Queries | Container queries not widely supported in 2026; media queries more reliable |

**Installation:**

Bootstrap via CDN (no npm install needed):
```html
<!-- In App.razor <head> section -->
<link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" rel="stylesheet">
```

Note: Bootstrap JS not needed for grid/layout features. Avoid including bootstrap.bundle.js to prevent conflicts with Blazor's event handling.

## Architecture Patterns

### Recommended Project Structure

Current structure (from Phase 3):
```
src/OpenAnima.Core/
├── Components/
│   ├── App.razor                    # Root component
│   ├── Routes.razor                 # Router configuration
│   ├── _Imports.razor               # Global using directives
│   ├── Layout/
│   │   ├── MainLayout.razor         # Sidebar + main content
│   │   └── MainLayout.razor.css     # Layout-specific styles
│   └── Pages/
│       ├── Dashboard.razor          # Overview page (existing)
│       ├── Dashboard.razor.css
│       ├── Modules.razor            # NEW: Module list page
│       ├── Modules.razor.css
│       ├── Heartbeat.razor          # NEW: Heartbeat status page
│       └── Heartbeat.razor.css
├── Components/Shared/               # NEW: Reusable components
│   ├── ModuleDetailModal.razor      # Modal for module details
│   └── ModuleDetailModal.razor.css
├── wwwroot/
│   └── css/
│       └── app.css                  # Global styles and theme
└── Services/
    ├── IModuleService.cs            # Already exists
    └── IHeartbeatService.cs         # Already exists
```

### Pattern 1: Blazor Page Component with @page Directive

**What:** Routable page components that respond to URL navigation

**When to use:** For each top-level page (Dashboard, Modules, Heartbeat)

**Example:**
```razor
@page "/modules"
@using OpenAnima.Core.Services
@inject IModuleService ModuleService

<h1 class="page-title">Modules</h1>

<div class="module-grid">
    @foreach (var entry in ModuleService.GetAllModules())
    {
        <div class="module-card" @onclick="() => ShowDetails(entry)">
            <!-- Card content -->
        </div>
    }
</div>

@code {
    private void ShowDetails(PluginRegistryEntry entry)
    {
        // Show modal
    }
}
```

### Pattern 2: NavLink for Active Navigation State

**What:** Blazor's built-in component that automatically applies "active" class to current route

**When to use:** Sidebar navigation items

**Example:**
```razor
<nav class="sidebar-nav">
    <NavLink href="/" class="nav-item" Match="NavLinkMatch.All">
        <span class="nav-icon">⬛</span>
        <span class="nav-label">Dashboard</span>
    </NavLink>
    <NavLink href="/modules" class="nav-item">
        <span class="nav-icon">📦</span>
        <span class="nav-label">Modules</span>
    </NavLink>
    <NavLink href="/heartbeat" class="nav-item">
        <span class="nav-icon">💓</span>
        <span class="nav-label">Heartbeat</span>
    </NavLink>
</nav>
```

**Key detail:** NavLink automatically adds "active" class when href matches current URL. The existing MainLayout.razor.css already has `.nav-item.active` styles defined.

**Source:** [ASP.NET Core Blazor navigation](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/navigation)

### Pattern 3: Modal Component with RenderFragment

**What:** Reusable modal dialog that accepts arbitrary content via RenderFragment parameter

**When to use:** Module detail modal, future dialogs

**Example:**
```razor
<!-- ModuleDetailModal.razor -->
@if (IsVisible)
{
    <div class="modal-backdrop" @onclick="Close"></div>
    <div class="modal-dialog">
        <div class="modal-header">
            <h2>@Title</h2>
            <button @onclick="Close">×</button>
        </div>
        <div class="modal-body">
            @ChildContent
        </div>
    </div>
}

@code {
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private async Task Close()
    {
        await OnClose.InvokeAsync();
    }
}
```

**Usage:**
```razor
<ModuleDetailModal IsVisible="@showModal"
                   Title="@selectedModule?.Module.Metadata.Name"
                   OnClose="@(() => showModal = false)">
    <p><strong>Version:</strong> @selectedModule?.Module.Metadata.Version</p>
    <p><strong>Description:</strong> @selectedModule?.Module.Metadata.Description</p>
    <!-- More details -->
</ModuleDetailModal>
```

**Sources:**
- [Templating components with RenderFragments - Blazor University](https://blazor-university.com/templating-components-with-renderfragements)
- [Blazor Basics: Blazor Event Callbacks](https://www.telerik.com/blogs/blazor-basics-event-callbacks)

### Pattern 4: CSS Grid Auto-Fit for Responsive Cards

**What:** CSS Grid with auto-fit and minmax for responsive card layouts without media queries

**When to use:** Module card grid, dashboard summary cards

**Example:**
```css
.module-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    gap: 20px;
}
```

**Why it works:** `auto-fit` automatically collapses to fewer columns as viewport narrows. Cards naturally stack to single column on mobile without explicit media query.

**Note:** Dashboard.razor.css already uses this pattern: `grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));`

**Sources:**
- [Look Ma, No Media Queries! Responsive Layouts Using CSS Grid](https://css-tricks.com/look-ma-no-media-queries-responsive-layouts-using-css-grid/)
- [Fully responsive items with CSS grid and auto-fit minmax](https://stackoverflow.com/questions/47981690/fully-responsive-items-with-css-grid-and-auto-fit-minmax)

### Pattern 5: Responsive Sidebar with State Toggle

**What:** Sidebar that collapses on narrow screens, controlled by component state

**When to use:** MainLayout sidebar (already partially implemented)

**Example:**
```razor
<!-- MainLayout.razor -->
<div class="app-container">
    <aside class="sidebar @(SidebarCollapsed ? "collapsed" : "") @(IsMobile ? "mobile-hidden" : "")">
        <!-- Sidebar content -->
    </aside>

    @if (IsMobile && !SidebarCollapsed)
    {
        <div class="sidebar-overlay" @onclick="ToggleSidebar"></div>
    }

    <main class="main-content">
        @if (IsMobile)
        {
            <button class="hamburger-menu" @onclick="ToggleSidebar">☰</button>
        }
        @Body
    </main>
</div>

@code {
    private bool SidebarCollapsed { get; set; }
    private bool IsMobile { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Use JS interop to detect viewport width
            // Or use CSS media query with mobile-hidden class
        }
    }
}
```

**CSS:**
```css
@media (max-width: 768px) {
    .sidebar {
        position: fixed;
        left: 0;
        top: 0;
        height: 100vh;
        z-index: 1000;
        transform: translateX(-100%);
        transition: transform 0.3s ease;
    }

    .sidebar:not(.mobile-hidden) {
        transform: translateX(0);
    }

    .sidebar-overlay {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.5);
        z-index: 999;
    }
}
```

**Note:** Current MainLayout.razor has collapse toggle but not mobile-specific behavior. Need to add media query and overlay.

### Anti-Patterns to Avoid

- **Inline styles in Razor markup:** Use scoped CSS files (.razor.css) to maintain separation of concerns and leverage CSS isolation
- **JavaScript for layout:** Blazor's state management handles show/hide logic; avoid jQuery or vanilla JS for modal/sidebar toggling
- **Bootstrap component classes everywhere:** User chose pure CSS approach; use Bootstrap grid utilities only, not .btn, .card, .modal classes
- **Polling for data updates:** Phase 4 is static display only; don't add timers or polling (Phase 5 will add SignalR real-time updates)

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Active navigation highlighting | Manual route matching and class toggling | NavLink component with Match parameter | Blazor's NavLink automatically manages active state; handles edge cases like trailing slashes and query strings |
| Responsive grid breakpoints | Custom JavaScript resize listeners | CSS Grid auto-fit + media queries | CSS handles responsiveness natively; no JS overhead or re-render issues |
| Component communication | Custom event system or static state | EventCallback parameters | Blazor's EventCallback is optimized for component tree updates and async operations |
| Modal backdrop click-outside | Manual event propagation logic | @onclick on backdrop div with @onclick:stopPropagation on dialog | Blazor's event handling prevents bubbling correctly; simpler than manual coordinate checking |

**Key insight:** Blazor provides built-in solutions for common UI patterns. Custom implementations often miss edge cases (keyboard navigation, accessibility, async state updates) that framework components handle correctly.

## Common Pitfalls

### Pitfall 1: NavLink Active Class Not Applying

**What goes wrong:** NavLink doesn't highlight the current page, or highlights multiple items

**Why it happens:**
- Default Match is `NavLinkMatch.Prefix`, which matches partial URLs (e.g., "/" matches "/modules")
- Forgetting to include "active" in the class attribute alongside base classes

**How to avoid:**
- Use `Match="NavLinkMatch.All"` for root path ("/") to require exact match
- Always include base class in class attribute: `class="nav-item"` (NavLink appends "active", doesn't replace)

**Warning signs:** Multiple nav items highlighted, or root item always highlighted

**Example:**
```razor
<!-- WRONG: Root always matches -->
<NavLink href="/" class="nav-item">Dashboard</NavLink>

<!-- CORRECT: Exact match for root -->
<NavLink href="/" class="nav-item" Match="NavLinkMatch.All">Dashboard</NavLink>
```

**Source:** [Understanding NavLink in Blazor](https://www.c-sharpcorner.com/article/understanding-navlink-in-blazor/)

### Pitfall 2: Modal Z-Index Stacking Issues

**What goes wrong:** Modal appears behind sidebar or other elements

**Why it happens:** CSS stacking contexts and z-index conflicts between layout elements

**How to avoid:**
- Use high z-index for modal (1000+) and backdrop (999)
- Ensure modal is rendered at root level, not nested deep in component tree
- Use fixed positioning for modal and backdrop

**Warning signs:** Modal partially visible, sidebar overlapping modal content

**Example CSS:**
```css
.modal-backdrop {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.7);
    z-index: 999;
}

.modal-dialog {
    position: fixed;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    z-index: 1000;
    max-width: 600px;
    width: 90%;
}
```

### Pitfall 3: EventCallback Not Awaiting

**What goes wrong:** Parent component state doesn't update after child component event fires

**Why it happens:** EventCallback.InvokeAsync() returns a Task that must be awaited for state changes to propagate

**How to avoid:**
- Always await EventCallback.InvokeAsync() in child components
- Mark event handler methods as async Task, not void

**Warning signs:** Modal doesn't close, state changes don't reflect in UI

**Example:**
```razor
<!-- WRONG -->
@code {
    private void Close()
    {
        OnClose.InvokeAsync(); // Fire and forget
    }
}

<!-- CORRECT -->
@code {
    private async Task Close()
    {
        await OnClose.InvokeAsync(); // Wait for parent state update
    }
}
```

**Source:** [Blazor Basics: Blazor Event Callbacks](https://www.telerik.com/blogs/blazor-basics-event-callbacks)

### Pitfall 4: CSS Grid Minmax Too Small on Mobile

**What goes wrong:** Cards become unreadably narrow on mobile devices

**Why it happens:** Minmax minimum value (e.g., 280px) exceeds viewport width on small phones, causing horizontal scroll

**How to avoid:**
- Use minmax with smaller minimum (240px) or use 100% for single column on mobile
- Add explicit media query for mobile to override grid with single column

**Warning signs:** Horizontal scrollbar on mobile, cards cut off

**Example:**
```css
.module-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    gap: 20px;
}

@media (max-width: 768px) {
    .module-grid {
        grid-template-columns: 1fr; /* Force single column */
    }
}
```

### Pitfall 5: Forgetting to Handle Empty State

**What goes wrong:** Blank page or broken layout when no modules are loaded

**Why it happens:** Grid layout with zero items renders nothing; no visual feedback to user

**How to avoid:**
- Always check collection count before rendering grid
- Provide empty state message with icon/illustration

**Warning signs:** Blank page on fresh install, confusion about whether system is working

**Example:**
```razor
@if (ModuleService.Count == 0)
{
    <div class="empty-state">
        <span class="empty-icon">📦</span>
        <p>No modules loaded</p>
    </div>
}
else
{
    <div class="module-grid">
        @foreach (var entry in ModuleService.GetAllModules())
        {
            <!-- Cards -->
        }
    </div>
}
```

## Code Examples

Verified patterns from official sources and existing codebase:

### Accessing Module Metadata

```csharp
// From IModuleService (already implemented)
var modules = ModuleService.GetAllModules(); // Returns IReadOnlyList<PluginRegistryEntry>

foreach (var entry in modules)
{
    // From IModuleMetadata (via IModule.Metadata)
    string name = entry.Module.Metadata.Name;
    string version = entry.Module.Metadata.Version;
    string description = entry.Module.Metadata.Description;

    // From PluginManifest (additional metadata)
    string manifestName = entry.Manifest.Name;
    string manifestVersion = entry.Manifest.Version;
    string? manifestDescription = entry.Manifest.Description;

    // From PluginRegistryEntry
    DateTime loadedAt = entry.LoadedAt;
    string assemblyPath = entry.Context.Name; // Load context name
}
```

**Note:** IModuleMetadata doesn't include "author" field (user requirement mentions it). Need to either:
1. Add Author property to IModuleMetadata and PluginManifest (requires contract change)
2. Omit author from modal display (simpler, defer to future phase)

**Recommendation:** Omit author for Phase 4, add in Phase 6 when module operations are implemented.

### Status Indicator Component

```razor
<!-- Reusable status indicator -->
<span class="status-indicator @StatusClass">
    <span class="status-dot"></span>
    @StatusText
</span>

@code {
    [Parameter] public bool IsLoaded { get; set; }

    private string StatusClass => IsLoaded ? "loaded" : "error";
    private string StatusText => IsLoaded ? "Loaded" : "Error";
}
```

```css
.status-indicator {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    font-size: 0.875rem;
    font-weight: 500;
    padding: 4px 12px;
    border-radius: 12px;
}

.status-indicator.loaded {
    background-color: rgba(74, 222, 128, 0.12);
    color: var(--success-color);
}

.status-indicator.error {
    background-color: rgba(248, 113, 113, 0.12);
    color: var(--error-color);
}

.status-dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background-color: currentColor;
}
```

**Note:** Current implementation uses text-based indicators. Add `.status-dot` span for visual dot.

### Heartbeat Statistics Display

```razor
@page "/heartbeat"
@inject IHeartbeatService HeartbeatService

<h1 class="page-title">Heartbeat</h1>

<div class="heartbeat-status">
    <div class="status-card-large">
        <div class="status-indicator-large @(HeartbeatService.IsRunning ? "running" : "stopped")">
            @(HeartbeatService.IsRunning ? "Running" : "Stopped")
        </div>
    </div>

    <div class="stats-container">
        <div class="stat-card">
            <span class="stat-label">Tick Count</span>
            <span class="stat-value mono">@HeartbeatService.TickCount</span>
        </div>
        <div class="stat-card">
            <span class="stat-label">Skipped Count</span>
            <span class="stat-value mono">@HeartbeatService.SkippedCount</span>
        </div>
    </div>
</div>
```

**Data available from IHeartbeatService:**
- `bool IsRunning` - Running/Stopped state
- `long TickCount` - Total ticks executed
- `long SkippedCount` - Ticks skipped due to anti-snowball

**Not available (defer to Phase 5):**
- Per-tick latency (requires event tracking)
- Real-time tick counter updates (requires SignalR)

### Dashboard Summary Cards

```razor
@page "/"
@inject IModuleService ModuleService
@inject IHeartbeatService HeartbeatService

<h1 class="page-title">Runtime Dashboard</h1>

<div class="summary-grid">
    <div class="summary-card">
        <span class="summary-icon">📦</span>
        <div class="summary-content">
            <span class="summary-label">Modules</span>
            <span class="summary-value">@ModuleService.Count</span>
        </div>
    </div>

    <div class="summary-card">
        <span class="summary-icon">💓</span>
        <div class="summary-content">
            <span class="summary-label">Heartbeat</span>
            <span class="summary-value">@(HeartbeatService.IsRunning ? "Running" : "Stopped")</span>
        </div>
    </div>

    <div class="summary-card">
        <span class="summary-icon">⏱️</span>
        <div class="summary-content">
            <span class="summary-label">Ticks</span>
            <span class="summary-value mono">@HeartbeatService.TickCount</span>
        </div>
    </div>
</div>
```

**Note:** Current Dashboard.razor shows detailed cards. User wants "numeric summary cards" for overview. Refactor to summary-card pattern.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Bootstrap 4 | Bootstrap 5 | 2021 | Removed jQuery dependency; improved grid system; better CSS custom properties support |
| Blazor Server .NET 5 | Blazor Server .NET 8 | 2023 | Enhanced rendering modes; improved performance; better static SSR support |
| CSS Frameworks (Bootstrap/Tailwind everywhere) | Utility-first + Custom CSS | 2024-2025 | Trend toward lightweight custom CSS for dashboards; component libraries for complex apps |
| JavaScript-based modals | Blazor component modals | 2020+ | Blazor's state management eliminates need for JS modal libraries |

**Deprecated/outdated:**
- **Blazor WebAssembly for dashboards:** Blazor Server preferred for internal tools (faster initial load, no WASM download)
- **Bootstrap JavaScript components in Blazor:** Conflicts with Blazor's event handling; use Blazor-native components instead
- **CSS-in-JS for Blazor:** Blazor's scoped CSS (.razor.css) provides better isolation and tooling support

## Open Questions

1. **Module "author" field missing from contracts**
   - What we know: User requirement mentions "author" in metadata, but IModuleMetadata and PluginManifest don't have Author property
   - What's unclear: Should we add Author field to contracts, or omit from UI?
   - Recommendation: Omit author from Phase 4 modal display; add Author to contracts in Phase 6 when module operations are implemented (requires contract versioning consideration)

2. **Module error state detection**
   - What we know: User wants "red dot + Error" status indicator for failed modules
   - What's unclear: How to detect error state? PluginRegistry only stores successfully loaded modules
   - Recommendation: Phase 4 shows all modules as "Loaded" (green); Phase 6 will add error tracking when load/unload operations are implemented

3. **Bootstrap integration scope**
   - What we know: User chose Bootstrap for "good Blazor ecosystem support"
   - What's unclear: How much Bootstrap to use? Grid only, or utility classes too?
   - Recommendation: Minimal integration - CDN link for grid utilities, but continue pure CSS approach for components to maintain lightweight shell

## Sources

### Primary (HIGH confidence)

- **Existing codebase** - /home/user/OpenAnima/src/OpenAnima.Core/
  - Program.cs: Blazor Server setup, service registration
  - Services/IModuleService.cs, IHeartbeatService.cs: Data access interfaces
  - Plugins/PluginRegistry.cs, PluginManifest.cs: Module metadata structure
  - Contracts/IModule.cs, IModuleMetadata.cs: Module contracts
  - Components/Layout/MainLayout.razor: Sidebar navigation pattern
  - wwwroot/css/app.css: Dark theme CSS custom properties
  - Components/Pages/Dashboard.razor.css: CSS Grid pattern already in use

### Secondary (MEDIUM confidence)

- [ASP.NET Core Blazor navigation](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/navigation) - NavLink component documentation
- [Templating components with RenderFragments - Blazor University](https://blazor-university.com/templating-components-with-renderfragements) - Modal component pattern
- [Blazor Basics: Blazor Event Callbacks](https://www.telerik.com/blogs/blazor-basics-event-callbacks) - EventCallback pattern
- [Bootstrap 5 Get Started - W3Schools](https://www.w3schools.com/bootstrap5/bootstrap_get_started.php) - Bootstrap CDN integration
- [Grid system · Bootstrap v5.3](https://getbootstrap.com/docs/5.3/layout/grid/) - Bootstrap grid documentation

### Tertiary (LOW confidence)

- [Look Ma, No Media Queries! Responsive Layouts Using CSS Grid](https://css-tricks.com/look-ma-no-media-queries-responsive-layouts-using-css-grid/) - CSS Grid auto-fit pattern (verified with existing codebase)
- [Understanding NavLink in Blazor](https://www.c-sharpcorner.com/article/understanding-navlink-in-blazor/) - NavLink Match parameter (verified with Microsoft docs)
- [Breakpoints for Responsive Web Design in 2025](https://www.browserstack.com/guide/responsive-design-breakpoints) - 768px breakpoint standard (widely accepted)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Existing Blazor Server setup verified in codebase; Bootstrap decision from user
- Architecture: HIGH - Patterns verified in existing codebase (CSS Grid, scoped CSS, service injection)
- Pitfalls: MEDIUM - Based on common Blazor patterns and web search findings; not all verified in official docs

**Research date:** 2026-02-22
**Valid until:** ~30 days (stable technologies; Blazor .NET 8 LTS until Nov 2026)

---

*Phase: 04-blazor-ui-with-static-display*
*Research complete: 2026-02-22*
