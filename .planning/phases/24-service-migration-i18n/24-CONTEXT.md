# Phase 24: Service Migration & i18n - Context

**Gathered:** 2026-02-28
**Status:** Ready for planning

<domain>
## Phase Boundary

Migrate EventBus, HeartbeatLoop, and WiringEngine from global singletons to per-Anima isolated instances. Add Chinese/English UI language switching with persistent preference. Creating new modules or new Anima capabilities are separate phases.

</domain>

<decisions>
## Implementation Decisions

### Anima Runtime Isolation
- All Anima runtimes run in parallel — switching Anima only switches the UI view
- SignalR push must be filtered by active Anima ID (currently broadcasts globally)
- UI switches instantly to new Anima's state (heartbeat count, module status, chat history)
- New Anima starts with runtime stopped — user must manually start it
- Deleting a running Anima: auto-stop HeartbeatLoop, cleanup EventBus subscriptions, release WiringEngine resources
- If deleted Anima was active, auto-switch to next Anima in list

### Language Switching Interaction
- Language switcher lives in a Settings page (not inline in navbar)
- Settings page accessed via gear icon in top navigation bar
- Language switch takes effect immediately without page reload (Blazor StateHasChanged)
- Language preference is global (all Animas share same language setting)
- Language preference persists across sessions

### Translation Coverage
- Translate all static UI text (navigation, buttons, labels, tooltips, error messages, placeholders, dialog content)
- Do NOT translate dynamic content (user input, log messages, module runtime output)
- Chinese is the default language; missing translations fall back to Chinese
- Date/time/number formatting follows current language locale (中文: "2026年2月28日", English: "Feb 28, 2026")
- Built-in modules provide bilingual names and descriptions via metadata
- Third-party modules keep their original names

### Claude's Discretion
- Service isolation architecture pattern (factory, keyed services, etc.)
- Translation file format (.resx vs JSON — whatever fits Blazor best)
- Translation key naming convention
- Settings page layout and additional settings items
- How to store language preference (localStorage, config file, etc.)

</decisions>

<specifics>
## Specific Ideas

- Settings page should be extensible for future settings (not just language)
- Gear icon in top-right corner of navigation bar is the entry point
- Built-in module metadata should support a `displayName` dictionary for i18n

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `AnimaRuntimeManager` (src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs): CRUD for Anima descriptors, filesystem persistence — extend to manage per-Anima runtime instances
- `AnimaContext` (src/OpenAnima.Core/Anima/AnimaContext.cs): Tracks active Anima ID with `ActiveAnimaChanged` event — use to trigger UI view switching
- `EventBus` (src/OpenAnima.Core/Events/EventBus.cs): Thread-safe with ConcurrentDictionary — needs to become per-Anima instance
- `HeartbeatLoop` (src/OpenAnima.Core/Runtime/HeartbeatLoop.cs): Takes IEventBus + PluginRegistry in constructor — can be instantiated per-Anima
- `WiringEngine` (src/OpenAnima.Core/Wiring/WiringEngine.cs): Takes IEventBus + IPortRegistry — can be instantiated per-Anima
- `AnimaServiceExtensions` (src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs): DI registration — needs refactoring for per-Anima scoping

### Established Patterns
- Singleton DI registration for core services (Program.cs lines 24-39)
- SignalR hub for real-time push (RuntimeHub at /hubs/runtime) — currently broadcasts to all clients
- Blazor Server with interactive server components
- HostedService pattern for initialization (AnimaInitializationService, OpenAnimaHostedService)

### Integration Points
- `Program.cs`: Service registration must change from singleton EventBus/HeartbeatLoop to per-Anima factory
- `MainLayout.razor`: Add gear icon for settings page navigation
- `RuntimeHub`: Filter SignalR messages by active Anima ID
- All 25 .razor components: Extract hardcoded strings to localization resources
- `AnimaDescriptor`: No changes needed (runtime state is separate from descriptor)

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 24-service-migration-i18n*
*Context gathered: 2026-02-28*
