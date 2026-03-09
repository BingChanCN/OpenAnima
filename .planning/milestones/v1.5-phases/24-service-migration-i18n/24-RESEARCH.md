# Phase 24: Service Migration & i18n - Research

**Researched:** 2026-02-28
**Domain:** .NET per-instance service isolation + Blazor Server localization
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- All Anima runtimes run in parallel — switching Anima only switches the UI view
- SignalR push must be filtered by active Anima ID (currently broadcasts globally)
- UI switches instantly to new Anima's state (heartbeat count, module status, chat history)
- New Anima starts with runtime stopped — user must manually start it
- Deleting a running Anima: auto-stop HeartbeatLoop, cleanup EventBus subscriptions, release WiringEngine resources
- If deleted Anima was active, auto-switch to next Anima in list
- Language switcher lives in a Settings page (not inline in navbar)
- Settings page accessed via gear icon in top navigation bar
- Language switch takes effect immediately without page reload (Blazor StateHasChanged)
- Language preference is global (all Animas share same language setting)
- Language preference persists across sessions
- Translate all static UI text (navigation, buttons, labels, tooltips, error messages, placeholders, dialog content)
- Do NOT translate dynamic content (user input, log messages, module runtime output)
- Chinese is the default language; missing translations fall back to Chinese
- Date/time/number formatting follows current language locale
- Built-in modules provide bilingual names and descriptions via metadata
- Third-party modules keep their original names

### Claude's Discretion

- Service isolation architecture pattern (factory, keyed services, etc.)
- Translation file format (.resx vs JSON — whatever fits Blazor best)
- Translation key naming convention
- Settings page layout and additional settings items
- How to store language preference (localStorage, config file, etc.)

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| ANIMA-07 | Each Anima has independent heartbeat loop | Per-Anima runtime container pattern; HeartbeatLoop already takes IEventBus + PluginRegistry in constructor — instantiate one per Anima |
| ANIMA-08 | Each Anima has independent module instances | PluginRegistry is currently a global singleton; needs per-Anima instance or per-Anima module state isolation |
| I18N-01 | User can switch UI language between Chinese and English | Blazor Server IStringLocalizer + CultureInfo.CurrentUICulture; custom LanguageService singleton for no-reload switching |
| I18N-02 | All UI text displays in selected language | .resx resource files per component or shared; IStringLocalizer<T> injection in every .razor |
| I18N-03 | Language preference persists across sessions | localStorage via IJSRuntime (already used in project); read on app init, write on change |
| I18N-04 | Missing translations fall back to English | IStringLocalizer built-in fallback; default culture set to zh-CN, English .resx as fallback |
| ARCH-03 | Each Anima has isolated EventBus instance | EventBus constructor takes ILogger<EventBus> only — trivially instantiable per Anima |
| ARCH-04 | Each Anima has isolated WiringEngine instance | WiringEngine constructor takes IEventBus + IPortRegistry + ILogger + optional IHubContext — instantiable per Anima |
</phase_requirements>

## Summary

Phase 24 has two independent workstreams: (1) migrating EventBus/HeartbeatLoop/WiringEngine from global singletons to per-Anima instances managed by AnimaRuntimeManager, and (2) adding Chinese/English UI language switching with localStorage persistence.

For service isolation, the cleanest pattern for this codebase is a **per-Anima runtime container** — a new `AnimaRuntime` class that owns one EventBus, one HeartbeatLoop, and one WiringEngine, with lifecycle (start/stop/dispose) managed by AnimaRuntimeManager. This avoids keyed DI services (which would require .NET 8 `IKeyedServiceProvider` and complicate the existing DI setup) and avoids factory patterns that scatter ownership. The existing constructors already accept their dependencies directly, so instantiation is straightforward.

For i18n, Blazor Server's standard approach requires `AddLocalization()` + `.resx` resource files + `IStringLocalizer<T>` injection. However, the user requires **no page reload** on language switch. The standard ASP.NET Core approach (cookie + `forceLoad: true`) does cause a reload. The no-reload path requires a custom `LanguageService` singleton that holds the current `CultureInfo`, fires a change event, and components subscribe to re-render. `IStringLocalizer` still works because it reads `CultureInfo.CurrentUICulture` at call time — the trick is setting `CultureInfo.CurrentUICulture` on the thread before rendering. In Blazor Server this is done by overriding `OnInitialized` / `OnParametersSet` to set the thread culture from the service, or by using a custom `IStringLocalizer` wrapper that reads from the service directly.

**Primary recommendation:** Use `AnimaRuntime` value object owned by `AnimaRuntimeManager` for service isolation. Use `.resx` files + `IStringLocalizer<T>` + a custom `LanguageService` singleton for no-reload i18n.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.Extensions.Localization | (built into .NET 8 ASP.NET Core) | IStringLocalizer, resource lookup | Official ASP.NET Core i18n mechanism; no extra package needed |
| System.Resources (.resx) | (built into .NET) | Translation key-value storage | Compile-time checked, IDE tooling, standard .NET approach |
| IJSRuntime | (built into Blazor) | localStorage read/write for language persistence | Already used in project (ChatPanel, EditorCanvas, ChatMessage) |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.AspNetCore.Localization | (built into ASP.NET Core) | RequestLocalizationMiddleware, CookieRequestCultureProvider | Needed for middleware pipeline setup even in no-reload scenario |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| .resx files | JSON translation files | JSON is simpler to edit but requires a third-party library (e.g., My.Extensions.Localization.Json); .resx is built-in and IDE-supported |
| Custom LanguageService | Cookie + forceLoad:true | Cookie approach is simpler but causes page reload — violates user decision |
| Per-Anima runtime container | Keyed DI services (.NET 8) | Keyed services require IKeyedServiceProvider plumbing throughout; runtime container is self-contained and easier to test |

**Installation:** No new packages required. All needed libraries are already in the .NET 8 SDK and ASP.NET Core.

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Anima/
│   ├── AnimaRuntime.cs          # NEW: owns EventBus + HeartbeatLoop + WiringEngine per Anima
│   ├── AnimaRuntimeManager.cs   # EXTEND: add runtime dictionary, start/stop/delete runtime
│   └── AnimaContext.cs          # UNCHANGED
├── Services/
│   └── LanguageService.cs       # NEW: singleton, holds CultureInfo, fires LanguageChanged event
├── Components/
│   ├── Pages/
│   │   └── Settings.razor       # NEW: settings page with language switcher
│   └── Layout/
│       └── MainLayout.razor     # EXTEND: add gear icon nav link to /settings
└── Resources/
    ├── SharedResources.zh-CN.resx   # NEW: Chinese (default)
    └── SharedResources.en-US.resx   # NEW: English
```

### Pattern 1: AnimaRuntime Container
**What:** A class that owns one instance each of EventBus, HeartbeatLoop, and WiringEngine for a single Anima. AnimaRuntimeManager creates/destroys these alongside AnimaDescriptors.
**When to use:** When multiple independent instances of the same service type must coexist without DI container involvement.
**Example:**
```csharp
// AnimaRuntime.cs
public sealed class AnimaRuntime : IAsyncDisposable
{
    public string AnimaId { get; }
    public EventBus EventBus { get; }
    public HeartbeatLoop HeartbeatLoop { get; }
    public WiringEngine WiringEngine { get; }
    public bool IsRunning => HeartbeatLoop.IsRunning;

    public AnimaRuntime(string animaId, ILoggerFactory loggerFactory,
        IHubContext<RuntimeHub, IRuntimeClient>? hubContext = null)
    {
        AnimaId = animaId;
        EventBus = new EventBus(loggerFactory.CreateLogger<EventBus>());
        // PluginRegistry per-Anima: new instance, modules registered separately
        var registry = new PluginRegistry();
        HeartbeatLoop = new HeartbeatLoop(EventBus, registry,
            TimeSpan.FromMilliseconds(100),
            loggerFactory.CreateLogger<HeartbeatLoop>(),
            hubContext);
        WiringEngine = new WiringEngine(EventBus, /* portRegistry */ ...,
            loggerFactory.CreateLogger<WiringEngine>(), hubContext);
    }

    public async ValueTask DisposeAsync()
    {
        await HeartbeatLoop.StopAsync();
        HeartbeatLoop.Dispose();
        WiringEngine.UnloadConfiguration();
    }
}
```

### Pattern 2: LanguageService for No-Reload Switching
**What:** A singleton service that holds the current `CultureInfo` and fires a `LanguageChanged` event. Components subscribe and call `StateHasChanged`. `IStringLocalizer` reads `CultureInfo.CurrentUICulture` at render time.
**When to use:** When language must switch without page reload in Blazor Server.
**Example:**
```csharp
// LanguageService.cs
public class LanguageService
{
    private CultureInfo _current = new CultureInfo("zh-CN");
    public CultureInfo Current => _current;
    public event Action? LanguageChanged;

    public void SetLanguage(string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        if (_current.Name == culture.Name) return;
        _current = culture;
        // Set thread culture so IStringLocalizer picks it up
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        LanguageChanged?.Invoke();
    }
}
```

```razor
@* In components that need to react to language changes *@
@implements IDisposable
@inject LanguageService LangSvc
@inject IStringLocalizer<SharedResources> L

protected override void OnInitialized()
{
    LangSvc.LanguageChanged += OnLanguageChanged;
}

private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

public void Dispose() => LangSvc.LanguageChanged -= OnLanguageChanged;
```

### Pattern 3: localStorage Persistence via IJSRuntime
**What:** Read language preference from localStorage on app init; write on change. IJSRuntime is already used in the project.
**When to use:** For persisting user preferences without server-side storage.
**Example:**
```csharp
// In Settings.razor or App.razor OnAfterRenderAsync
var saved = await JS.InvokeAsync<string?>("localStorage.getItem", "openanima-language");
if (!string.IsNullOrEmpty(saved))
    LangSvc.SetLanguage(saved);

// On language change
await JS.InvokeVoidAsync("localStorage.setItem", "openanima-language", cultureName);
```

### Pattern 4: SignalR Filtering by Anima ID
**What:** HeartbeatLoop and WiringEngine currently broadcast to `Clients.All`. With per-Anima runtimes, push must be filtered so only the active Anima's events reach the UI.
**When to use:** When multiple runtimes share one SignalR hub.
**Example:**
```csharp
// Option A: Pass animaId in SignalR message payload
await _hubContext.Clients.All.ReceiveHeartbeatTick(animaId, _tickCount, latencyMs);
// Client-side JS/Blazor filters by active animaId

// Option B: Use SignalR groups per Anima
await _hubContext.Clients.Group(animaId).ReceiveHeartbeatTick(_tickCount, latencyMs);
// Client joins group for active animaId, leaves on switch
```
Option A (payload filtering) is simpler for Blazor Server since group management requires connection tracking. Option B is cleaner for scale but adds complexity. **Recommend Option A** for this phase.

### Anti-Patterns to Avoid
- **Registering per-Anima services in the DI container:** DI container is not designed for dynamic keyed instances that come and go at runtime. Use the AnimaRuntime container pattern instead.
- **Setting `CultureInfo.CurrentCulture` only on one thread:** In Blazor Server, rendering happens on thread pool threads. Use `CultureInfo.DefaultThreadCurrentUICulture` (process-wide) since this is a single-user app.
- **Sharing PluginRegistry across Animas:** The current `PluginRegistry` holds module instances. For true isolation (ANIMA-08), each Anima needs its own registry. However, module DLLs are loaded once via `PluginLoader` — the registry just holds references. A per-Anima registry with shared module instances is the right balance.
- **Translating dynamic content:** Log messages, user chat input, and module runtime output must NOT go through IStringLocalizer — only static UI strings.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Resource file lookup | Custom JSON translation loader | .resx + IStringLocalizer | Built-in fallback chain, compile-time resource embedding, IDE support |
| Culture fallback | Manual fallback logic | IStringLocalizer built-in fallback | IStringLocalizer automatically falls back to neutral culture then invariant |
| localStorage interop | Custom JS module | Direct `localStorage.getItem/setItem` via IJSRuntime | Already used in project; no module needed for simple key-value |

**Key insight:** The .NET localization stack handles fallback, culture hierarchy, and resource caching automatically. The only custom code needed is the `LanguageService` singleton to bridge the no-reload requirement.

## Common Pitfalls

### Pitfall 1: IStringLocalizer Does Not React to Runtime Culture Changes Automatically
**What goes wrong:** Components render with the old language after `SetLanguage()` is called, even though `CultureInfo.DefaultThreadCurrentUICulture` was updated.
**Why it happens:** Blazor Server components only re-render when `StateHasChanged()` is called. IStringLocalizer reads the culture at render time, but the component doesn't know the culture changed.
**How to avoid:** Subscribe to `LanguageService.LanguageChanged` in every component that displays translated text, and call `InvokeAsync(StateHasChanged)` in the handler.
**Warning signs:** Language switch updates some components but not others — those missing the subscription.

### Pitfall 2: HeartbeatLoop Disposal Race Condition
**What goes wrong:** Deleting an Anima while its HeartbeatLoop is running causes `ObjectDisposedException` or orphaned background tasks.
**Why it happens:** `HeartbeatLoop.StopAsync()` is async; if `Dispose()` is called before the loop task completes, the `_loopTask` may still be running.
**How to avoid:** Always `await StopAsync()` before `Dispose()` in `AnimaRuntime.DisposeAsync()`. The existing `HeartbeatLoop.Dispose()` already calls `StopAsync().GetAwaiter().GetResult()` — use `DisposeAsync` pattern to avoid blocking.
**Warning signs:** Exceptions logged from HeartbeatLoop after Anima deletion.

### Pitfall 3: SignalR Broadcasts Reaching Wrong Anima's UI
**What goes wrong:** Heartbeat ticks from Anima B appear in Anima A's UI when the user switches active Anima.
**Why it happens:** Current `Clients.All.ReceiveHeartbeatTick()` broadcasts to all connected clients without Anima context.
**How to avoid:** Include `animaId` in all SignalR push payloads. UI components filter by `AnimaContext.ActiveAnimaId` before updating state.
**Warning signs:** Heartbeat counter jumps or resets unexpectedly when switching Animas.

### Pitfall 4: .resx File Not Found at Runtime
**What goes wrong:** `IStringLocalizer` returns the key name instead of translated text; no exception thrown.
**Why it happens:** .resx files must be set to `EmbeddedResource` build action and the namespace/path must match the `IStringLocalizer<T>` type parameter.
**How to avoid:** Verify `.csproj` includes `<EmbeddedResource Include="Resources\*.resx" />`. Use a shared `SharedResources` marker class in the `Resources/` folder so all components can use `IStringLocalizer<SharedResources>` instead of per-component resource files.
**Warning signs:** All strings display as their key names (e.g., "Nav.Dashboard" instead of "仪表盘").

### Pitfall 5: PluginRegistry Isolation vs Module Singleton Conflict
**What goes wrong:** Per-Anima PluginRegistry instances each try to hold their own module instances, but modules are registered as singletons in DI.
**Why it happens:** Current `WiringServiceExtensions` registers `LLMModule`, `ChatInputModule`, etc. as singletons. A per-Anima registry referencing the same singleton module instances means module state (e.g., LLM conversation context) is shared across Animas.
**How to avoid:** For Phase 24, create per-Anima PluginRegistry instances that reference the same module singleton instances (shared execution, isolated event routing). True module instance isolation is a future concern (ANIMA-08 full implementation may need per-Anima module instances in a later phase).
**Warning signs:** Module state bleeds between Animas (e.g., chat history from Anima A appears in Anima B).

## Code Examples

Verified patterns from official sources:

### AddLocalization Setup in Program.cs
```csharp
// Source: https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/blazor/globalization-localization.md
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddSingleton<LanguageService>();

var supportedCultures = new[] { "zh-CN", "en-US" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("zh-CN")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);
```

### SharedResources Marker Class Pattern
```csharp
// Resources/SharedResources.cs — marker class, no members needed
namespace OpenAnima.Core.Resources;
public class SharedResources { }
```

```
// Resources/SharedResources.zh-CN.resx  (default — Chinese)
// Key: Nav.Dashboard   Value: 仪表盘
// Key: Nav.Modules     Value: 模块
// Key: Nav.Heartbeat   Value: 心跳
// Key: Nav.Monitor     Value: 监控
// Key: Nav.Editor      Value: 编辑器
// Key: Nav.Settings    Value: 设置
// Key: Anima.Create    Value: 创建 Anima
// Key: Anima.Delete    Value: 删除
// Key: Anima.Rename    Value: 重命名
// Key: Anima.Clone     Value: 克隆

// Resources/SharedResources.en-US.resx  (English)
// Key: Nav.Dashboard   Value: Dashboard
// Key: Nav.Modules     Value: Modules
// ... etc
```

### IStringLocalizer Injection in _Imports.razor
```razor
@* Source: https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/blazor/globalization-localization.md *@
@using Microsoft.Extensions.Localization
@using OpenAnima.Core.Resources
```

### AnimaRuntimeManager Extension for Runtime Lifecycle
```csharp
// Extend AnimaRuntimeManager to own per-Anima runtimes
private readonly Dictionary<string, AnimaRuntime> _runtimes = new();

public AnimaRuntime GetOrCreateRuntime(string animaId)
{
    if (!_runtimes.TryGetValue(animaId, out var runtime))
    {
        runtime = new AnimaRuntime(animaId, _loggerFactory, _hubContext);
        _runtimes[animaId] = runtime;
    }
    return runtime;
}

public async Task DeleteAsync(string id, CancellationToken ct = default)
{
    // Stop and dispose runtime before removing descriptor
    if (_runtimes.TryGetValue(id, out var runtime))
    {
        await runtime.DisposeAsync();
        _runtimes.Remove(id);
    }
    // ... existing descriptor removal
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Cookie + forceLoad:true for culture switch | Custom LanguageService singleton + StateHasChanged | ASP.NET Core 8+ | No page reload; requires manual component subscription |
| Per-component .resx files | Shared SharedResources marker class | Best practice evolution | Single injection point; simpler key management |
| IHostedService for global heartbeat | Per-Anima AnimaRuntime container | This phase | Heartbeat lifecycle tied to Anima, not app startup |

**Deprecated/outdated:**
- `_Host.cshtml` culture cookie pattern: Only applies to Blazor Server pre-.NET 8 with Pages hosting model. OpenAnima uses `.NET 8` with `MapRazorComponents` (not Pages), so this pattern does not apply.
- Global singleton `HeartbeatLoop` in `Program.cs`: Will be replaced by per-Anima instances in `AnimaRuntime`.

## Open Questions

1. **PluginRegistry per-Anima vs shared**
   - What we know: Current `PluginRegistry` holds module instances registered as DI singletons. HeartbeatLoop ticks all modules in the registry.
   - What's unclear: If each Anima has its own PluginRegistry with the same module singleton references, HeartbeatLoop for Anima A will tick the same `LLMModule` instance as Anima B's loop — module state is shared.
   - Recommendation: For Phase 24, create per-Anima PluginRegistry instances that reference the same module singletons. Document this as a known limitation. Full module instance isolation is ANIMA-08 scope and may require deeper changes in a follow-up phase.

2. **IRuntimeClient interface changes for animaId filtering**
   - What we know: `IRuntimeClient` currently has `ReceiveHeartbeatTick(long tickCount, double latencyMs)` — no animaId parameter.
   - What's unclear: Adding animaId to the interface signature will require updating all SignalR client-side handlers in .razor components.
   - Recommendation: Add `animaId` as first parameter to all `IRuntimeClient` push methods. Update all Blazor components that subscribe to these events to filter by `AnimaContext.ActiveAnimaId`.

3. **Settings page scope**
   - What we know: User wants a Settings page with gear icon in nav, extensible for future settings.
   - What's unclear: Whether other settings (e.g., heartbeat interval, LLM defaults) should be stubbed in this phase.
   - Recommendation: Create Settings page with language switcher only. Add a clear section structure so future settings can be added without redesign.

## Validation Architecture

> nyquist_validation not present in config.json — skipping this section.

## Sources

### Primary (HIGH confidence)
- `/dotnet/aspnetcore.docs` (Context7) — Blazor Server localization, IStringLocalizer, AddLocalization, RequestLocalizationOptions, localStorage culture persistence
- Codebase inspection — EventBus, HeartbeatLoop, WiringEngine, AnimaRuntimeManager, AnimaContext, Program.cs, WiringServiceExtensions, HeartbeatService constructors and DI registrations

### Secondary (MEDIUM confidence)
- ASP.NET Core docs (via Context7): `CultureInfo.DefaultThreadCurrentUICulture` for process-wide culture setting in Blazor Server no-reload scenario

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries are built-in .NET 8 / ASP.NET Core; no third-party packages needed
- Architecture: HIGH — AnimaRuntime container pattern derived directly from existing constructor signatures; LanguageService pattern derived from official Blazor docs
- Pitfalls: HIGH — disposal race condition and SignalR broadcast issues derived from direct code inspection; IStringLocalizer fallback behavior from official docs

**Research date:** 2026-02-28
**Valid until:** 2026-03-28 (stable .NET 8 APIs)
