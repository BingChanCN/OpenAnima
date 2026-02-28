# Technology Stack

**Project:** OpenAnima v1.5 Multi-Anima Architecture
**Researched:** 2026-02-28
**Confidence:** HIGH

## Executive Summary

For v1.5's multi-Anima architecture, internationalization, and module configuration persistence, **minimal new dependencies are required**. The stack leverages built-in .NET 8.0 capabilities:

- **Internationalization:** Microsoft.Extensions.Localization 8.0.* + custom JSON localizer (no .resx files)
- **Configuration persistence:** System.Text.Json (built-in) for reading/writing JSON config files
- **Multi-instance state:** Blazor Server scoped services (built-in) for per-circuit Anima isolation
- **State management:** State container pattern (20 LOC, no dependencies)

This approach maintains the project's "minimal dependencies" philosophy while adding only one well-supported library for i18n.

## Context

OpenAnima v1.4 shipped with ~14,500 LOC using .NET 8.0, Blazor Server, SignalR 8.0.*, OpenAI SDK 2.8.0, SharpToken 2.0.4, Markdig 0.41.3, System.CommandLine 2.0.3. v1.5 adds multi-Anima architecture and i18n without changing the core runtime stack.

**Existing validated stack (UNCHANGED):**
- .NET 8.0 runtime
- Blazor Server with SignalR 8.0.x
- Custom EventBus (lock-free, ConcurrentDictionary-based)
- AssemblyLoadContext module isolation
- OpenAI SDK 2.8.0
- SharpToken 2.0.4
- Markdig 0.41.3 + Markdown.ColorCode 3.0.1
- System.CommandLine 2.0.3 (CLI tool)
- Pure CSS dark theme
- xUnit test suite

## Recommended Stack Additions

### Internationalization (i18n)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Microsoft.Extensions.Localization | 8.0.* | Core localization infrastructure | Official .NET localization with IStringLocalizer support for Blazor Server; confirmed supported in official docs |
| Microsoft.Extensions.Localization.Abstractions | 8.0.* | Localization interfaces | Required for IStringLocalizer<T> injection in Razor components |
| Custom JSON localizer | N/A | JSON-based resource files | More flexible than .resx for non-technical translators, easier to edit and version control, better for modern web apps |

### Configuration Persistence

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| System.Text.Json | Built-in (.NET 8) | JSON serialization/deserialization | Already included in .NET 8, zero additional dependencies, high performance, officially recommended |
| Microsoft.Extensions.Configuration.Json | Built-in (.NET 8) | JSON configuration provider | Already in use for appsettings.json, no new dependency needed |

### Multi-Instance State Management

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Scoped services (built-in) | N/A | Per-circuit state isolation | Blazor Server scoped services live for circuit lifetime, perfect for per-Anima state isolation |
| State container pattern | N/A | Reactive state management | Lightweight pattern (20 LOC) using scoped services + Action<> events for cross-component updates |

## Installation

```bash
# Add localization support (only new dependencies)
cd src/OpenAnima.Core
dotnet add package Microsoft.Extensions.Localization --version 8.0.*
dotnet add package Microsoft.Extensions.Localization.Abstractions --version 8.0.*

# No additional packages needed for:
# - System.Text.Json (built into .NET 8)
# - Configuration persistence (already using Microsoft.Extensions.Configuration.Json)
# - State management (built-in Blazor Server DI scopes)
```

## Architecture Integration

### Existing Architecture (v1.4)
- **Singleton services:** PluginRegistry, PluginLoader, EventBus, HeartbeatLoop (global runtime)
- **Scoped services:** ChatSessionState (per-circuit)
- **SignalR Hub:** RuntimeHub for real-time push to clients

### New Architecture (v1.5)
- **Scoped services:** AnimaManager (per-circuit), holds collection of Anima instances
- **Each AnimaInstance:** Independent HeartbeatLoop, PluginRegistry, EventBus, ChatSessionState
- **Configuration files:** JSON files in `config/animas/{id}.json`, `config/modules/{moduleId}.json`, `config/user-preferences.json`
- **Localization files:** JSON files in `Resources/Localization.{culture}.json`

### Migration Strategy
1. **Phase 1:** Add localization infrastructure (IStringLocalizer, JSON files)
2. **Phase 2:** Refactor singleton services to be instantiable (remove static dependencies)
3. **Phase 3:** Create AnimaManager scoped service, instantiate Anima instances
4. **Phase 4:** Add configuration persistence layer (save/load JSON files)
5. **Phase 5:** Build module management UI with install/uninstall/enable/disable

## Implementation Patterns

### Pattern 1: Per-Anima Scoped State

**What:** Each Anima instance is a scoped service with its own heartbeat, modules, and chat state.

**How:**
```csharp
// Register per-circuit Anima manager
builder.Services.AddScoped<AnimaManager>();

// AnimaManager holds collection of Anima instances
public class AnimaManager
{
    private readonly Dictionary<Guid, AnimaInstance> _animas = new();
    public event Action? OnChange;

    public AnimaInstance CreateAnima(string name)
    {
        var anima = new AnimaInstance(Guid.NewGuid(), name);
        _animas[anima.Id] = anima;
        NotifyStateChanged();
        return anima;
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

// Each AnimaInstance has independent state
public class AnimaInstance
{
    public Guid Id { get; }
    public string Name { get; set; }
    public HeartbeatLoop Heartbeat { get; }
    public List<IModule> Modules { get; }
    public ChatSessionState ChatState { get; }
}
```

**Why:** Scoped services in Blazor Server live for the circuit lifetime, providing automatic per-user isolation without manual session management.

### Pattern 2: JSON-Based Localization

**What:** Store translations in JSON files, load based on culture, inject into components.

**How:**
```csharp
// Resources/Localization.en.json
{
  "AppTitle": "OpenAnima",
  "CreateAnima": "Create Anima",
  "ModuleManagement": "Module Management"
}

// Resources/Localization.zh.json
{
  "AppTitle": "OpenAnima",
  "CreateAnima": "创建 Anima",
  "ModuleManagement": "模块管理"
}

// Custom JSON localizer
public class JsonStringLocalizer : IStringLocalizer
{
    private readonly Dictionary<string, string> _localizations;

    public JsonStringLocalizer(string cultureName)
    {
        var json = File.ReadAllText($"Resources/Localization.{cultureName}.json");
        _localizations = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
    }

    public LocalizedString this[string name] =>
        new LocalizedString(name, _localizations.TryGetValue(name, out var value) ? value : name);
}

// Register in Program.cs
builder.Services.AddLocalization();
builder.Services.AddScoped<IStringLocalizer>(sp =>
{
    var culture = CultureInfo.CurrentCulture.Name;
    return new JsonStringLocalizer(culture);
});

// Use in components
@inject IStringLocalizer Localizer
<h1>@Localizer["AppTitle"]</h1>
```

**Why:** JSON files are easier to edit than .resx, better for version control, and can be edited by non-developers. IStringLocalizer provides standard .NET localization interface.

### Pattern 3: Configuration Persistence

**What:** Save Anima configurations, module settings, and user preferences to JSON files.

**How:**
```csharp
public class ConfigurationPersistence
{
    private readonly string _configPath;

    public async Task SaveAnimaConfigAsync(AnimaConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync($"{_configPath}/animas/{config.Id}.json", json);
    }

    public async Task<AnimaConfig?> LoadAnimaConfigAsync(Guid id)
    {
        var path = $"{_configPath}/animas/{id}.json";
        if (!File.Exists(path)) return null;

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<AnimaConfig>(json);
    }
}

// AnimaConfig model
public record AnimaConfig(
    Guid Id,
    string Name,
    List<ModuleConnection> Connections,
    Dictionary<string, object> ModuleSettings
);
```

**Why:** System.Text.Json is built-in, fast, and handles all serialization needs. File-based storage is simple, debuggable, and sufficient for local-first architecture.

### Pattern 4: Language Preference Persistence

**What:** Save user's language choice and restore on next session.

**How:**
```csharp
// Save to user preferences file
public class UserPreferences
{
    public string Language { get; set; } = "en";
}

// In language switcher component
private async Task SetLanguageAsync(string cultureName)
{
    var prefs = new UserPreferences { Language = cultureName };
    await _configPersistence.SaveUserPreferencesAsync(prefs);

    // Set culture for current request
    var culture = new CultureInfo(cultureName);
    CultureInfo.CurrentCulture = culture;
    CultureInfo.CurrentUICulture = culture;

    // Reload page to apply new culture
    _navigationManager.NavigateTo(_navigationManager.Uri, forceLoad: true);
}

// On app startup, load saved preference
protected override async Task OnInitializedAsync()
{
    var prefs = await _configPersistence.LoadUserPreferencesAsync();
    if (prefs != null)
    {
        var culture = new CultureInfo(prefs.Language);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
```

**Why:** Simple file-based persistence, no database overhead, works perfectly for local-first architecture.

### Pattern 5: State Container with Notifications

**What:** Scoped service that notifies components when state changes.

**How:**
```csharp
public class StateContainer
{
    private string? _savedString;

    public string Property
    {
        get => _savedString ?? string.Empty;
        set
        {
            _savedString = value;
            NotifyStateChanged();
        }
    }

    public event Action? OnChange;

    private void NotifyStateChanged() => OnChange?.Invoke();
}

// Register as scoped
builder.Services.AddScoped<StateContainer>();

// Use in components
@inject StateContainer StateContainer
@implements IDisposable

protected override void OnInitialized()
{
    StateContainer.OnChange += StateHasChanged;
}

public void Dispose()
{
    StateContainer.OnChange -= StateHasChanged;
}
```

**Why:** Lightweight pattern (20 LOC) that enables reactive UI updates without external dependencies. Scoped lifetime ensures per-circuit isolation.

## Alternatives Considered

| Category | Recommended | Alternative | Why Not Alternative |
|----------|-------------|-------------|---------------------|
| i18n | IStringLocalizer + JSON | .resx files | JSON is easier for non-developers to edit, better for version control, more flexible |
| i18n | Custom JSON localizer | Third-party libraries (Toolbelt.Blazor.I18nText) | Adds dependency for simple use case, built-in IStringLocalizer is sufficient |
| Config persistence | System.Text.Json | Newtonsoft.Json | System.Text.Json is built-in, faster, and officially recommended for .NET 8+ |
| Config persistence | Direct JSON file writes | Database (SQLite, LiteDB) | Over-engineering for simple config storage, adds complexity and dependencies |
| State management | Scoped services | Fluxor/Redux | Over-engineering for per-instance state, scoped services are simpler and sufficient |
| State management | State container pattern | Third-party state libraries | Adds dependencies for pattern that's trivial to implement (20 lines of code) |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| IHtmlLocalizer | Not supported in Blazor (MVC-only) | IStringLocalizer |
| IViewLocalizer | Not supported in Blazor (MVC-only) | IStringLocalizer |
| Newtonsoft.Json | Legacy library, slower than System.Text.Json | System.Text.Json (built-in) |
| Singleton services for Anima instances | Shared across all users/circuits | Scoped services (per-circuit isolation) |
| ProtectedBrowserStorage | Async overhead, not needed for server-side state | Scoped services + JSON file persistence |
| Database for configuration | Over-engineering, adds complexity | JSON files with System.Text.Json |
| Third-party state management (Fluxor, Redux) | Over-engineering for simple per-instance state | Scoped services + state container pattern |

## Version Compatibility

| Package | Version | Compatible With | Notes |
|---------|---------|-----------------|-------|
| Microsoft.Extensions.Localization | 8.0.* | .NET 8.0 | Must match .NET runtime version |
| Microsoft.Extensions.Localization.Abstractions | 8.0.* | .NET 8.0 | Must match .NET runtime version |
| System.Text.Json | Built-in (.NET 8) | .NET 8.0 | Built into runtime, no version conflicts |
| SignalR | 8.0.* | .NET 8.0 | Critical: must match runtime version to avoid circuit crashes |

## Integration Points with Existing Stack

| Component | Integration |
|-----------|-------------|
| HeartbeatLoop | Refactor to be instantiable (not singleton); each Anima gets its own instance |
| PluginRegistry | Refactor to be instantiable; each Anima gets its own registry |
| EventBus | Refactor to be instantiable; each Anima gets its own event bus |
| ChatSessionState | Already scoped; move to AnimaInstance |
| SignalR RuntimeHub | Add AnimaId parameter to hub methods for multi-instance push |
| WiringEngine | Add per-Anima wiring configurations |

## Files to Create

| File Path | Purpose |
|-----------|---------|
| `src/OpenAnima.Core/Services/AnimaManager.cs` | Scoped service managing Anima instances |
| `src/OpenAnima.Core/Services/AnimaInstance.cs` | Per-Anima state container |
| `src/OpenAnima.Core/Services/ConfigurationPersistence.cs` | JSON file read/write for configs |
| `src/OpenAnima.Core/Localization/JsonStringLocalizer.cs` | Custom JSON-based localizer |
| `src/OpenAnima.Core/Models/AnimaConfig.cs` | Anima configuration model |
| `src/OpenAnima.Core/Models/UserPreferences.cs` | User preferences model |
| `src/OpenAnima.Core/Resources/Localization.en.json` | English translations |
| `src/OpenAnima.Core/Resources/Localization.zh.json` | Chinese translations |
| `config/animas/{id}.json` | Per-Anima configuration files (runtime) |
| `config/modules/{moduleId}.json` | Per-module configuration files (runtime) |
| `config/user-preferences.json` | User preferences file (runtime) |

## Confidence Assessment

| Area | Confidence | Rationale |
|------|------------|-----------|
| Localization | HIGH | Official Microsoft docs confirm IStringLocalizer support in Blazor Server |
| JSON config persistence | HIGH | System.Text.Json is built-in, well-documented, widely used |
| Scoped services | HIGH | Core Blazor Server feature, official docs confirm per-circuit lifetime |
| State container pattern | HIGH | Industry best practice, documented by Microsoft and community experts |
| Multi-instance architecture | MEDIUM | Pattern is sound, but refactoring singleton services requires careful testing |

## Sources

**HIGH confidence:**
- [ASP.NET Core Blazor globalization and localization](https://learn.microsoft.com/en-us/aspnet/core/blazor/globalization-localization) — Official Microsoft documentation confirming IStringLocalizer support
- [ASP.NET Core Blazor state management overview](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/) — Official guidance on scoped services and state container pattern
- [Blazor 8 State Management](https://blog.lhotka.net/2023/10/12/Blazor-8-State-Management) — Expert analysis of state management patterns
- [State Management - AppState pattern](https://www.ssw.com.au/rules/blazor-basic-appstate-pattern/) — Industry best practice for scoped state

**MEDIUM confidence:**
- [Implementing Custom JSON Localization in ASP.NET Core](https://gauravm.dev/articles/implementing-custom-json-localization-in-aspnet-core-web-api/) — Community implementation pattern for JSON-based localization
- [What does scoped lifetime for a service mean in Blazor (server)?](https://stackoverflow.com/questions/76195106/what-does-scoped-lifetime-for-a-service-mean-in-blazor-server) — Community discussion on scoped service lifetime

---
*Stack research for: Multi-Anima architecture, i18n, and module configuration persistence*
*Researched: 2026-02-28*
*Confidence: HIGH*
