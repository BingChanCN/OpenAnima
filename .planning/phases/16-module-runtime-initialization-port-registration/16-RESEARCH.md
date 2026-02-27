# Phase 16: Module Runtime Initialization & Port Registration - Research

**Researched:** 2026-02-27
**Domain:** .NET IHostedService startup orchestration, port discovery/registration, Blazor module palette
**Confidence:** HIGH — all findings are from direct codebase inspection of existing, working patterns

## Summary

Phase 16 is a targeted wiring task, not a new feature build. All the infrastructure already exists: `PortDiscovery`, `IPortRegistry`, `WiringInitializationService`, and all four concrete modules (`LLMModule`, `ChatInputModule`, `ChatOutputModule`, `HeartbeatModule`) are already registered as singletons in DI. The gap is that `WiringInitializationService.StartAsync()` only loads the last saved wiring configuration — it never calls `PortDiscovery.DiscoverPorts()` or `IPortRegistry.RegisterPorts()` for the concrete modules, and never calls `InitializeAsync()` on them.

The editor's `ModulePalette` reads from `IPortRegistry.GetAllPorts()` to build its module list. Because no real modules are registered at startup, the editor falls back to `RegisterDemoModules()` in `Editor.razor` (the `if (_portRegistry.GetAllPorts().Count == 0)` guard). Removing that fallback and ensuring real modules are registered at startup is the complete fix for EDIT-01 and PORT-04.

The work is entirely in `WiringInitializationService.StartAsync()` and `Editor.razor`. No new classes, no new interfaces, no new DI registrations are needed.

**Primary recommendation:** Extend `WiringInitializationService.StartAsync()` to discover and register ports for all four concrete modules and call `InitializeAsync()` on each, then remove the `RegisterDemoModules()` fallback from `Editor.razor`.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PORT-04 | Modules declare input/output ports via typed interface, discoverable at load time | `PortDiscovery.DiscoverPorts(Type)` already works via reflection on `[InputPort]`/`[OutputPort]` attributes. `IPortRegistry.RegisterPorts(moduleName, ports)` already stores them. Gap: nobody calls these at startup for the concrete modules. |
| RMOD-01 | LLM service refactored into LLMModule with typed input/output ports | `LLMModule` already exists with `[InputPort("prompt", PortType.Text)]` and `[OutputPort("response", PortType.Text)]`. `InitializeAsync()` sets up EventBus subscription. Gap: `InitializeAsync()` is never called at runtime. |
| RMOD-02 | Chat input refactored into ChatInputModule with output port | `ChatInputModule` already exists with `[OutputPort("userMessage", PortType.Text)]`. `InitializeAsync()` is a no-op. Gap: ports not registered at startup. |
| RMOD-03 | Chat output refactored into ChatOutputModule with input port | `ChatOutputModule` already exists with `[InputPort("displayText", PortType.Text)]`. `InitializeAsync()` sets up EventBus subscription. Gap: `InitializeAsync()` is never called at runtime. |
| RMOD-04 | Heartbeat refactored into HeartbeatModule with trigger port | `HeartbeatModule` already exists with `[OutputPort("tick", PortType.Trigger)]`. `InitializeAsync()` is a no-op. Gap: ports not registered at startup. |
| EDIT-01 | User can drag modules from palette onto canvas to place them | `ModulePalette` reads `IPortRegistry.GetAllPorts()`. Once real modules are registered, palette shows them. `Editor.razor` currently falls back to demo modules when registry is empty — that fallback must be removed. |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.Extensions.Hosting` | .NET 8.0 built-in | `IHostedService` for startup orchestration | Already used by `WiringInitializationService` |
| `Microsoft.Extensions.DependencyInjection` | .NET 8.0 built-in | Resolve singleton module instances | Already used throughout |
| `OpenAnima.Core.Ports.PortDiscovery` | project | Reflect `[InputPort]`/`[OutputPort]` attributes from module type | Already works, tested in `PortDiscoveryTests.cs` |
| `OpenAnima.Core.Ports.IPortRegistry` | project | Store port metadata keyed by module name | Already singleton, already injected into `WiringInitializationService` |

### No New Dependencies
This phase requires zero new packages. All needed types are already in the project.

## Architecture Patterns

### Existing Pattern: WiringInitializationService Startup
`WiringInitializationService` already holds `IServiceProvider` and creates a scope to resolve scoped services. The module singletons (`LLMModule`, etc.) are registered as singletons in `WiringServiceExtensions.AddWiringServices()` and can be resolved directly from `_serviceProvider` (no scope needed for singletons).

```csharp
// Current StartAsync — only loads config, never touches modules
public async Task StartAsync(CancellationToken cancellationToken)
{
    // ... reads .lastconfig and calls configLoader.LoadAsync() ...
}
```

### Pattern to Add: Port Discovery + Module Init at Startup
```csharp
// Source: existing PortDiscovery.DiscoverPorts() + IPortRegistry.RegisterPorts() pattern
// from WiringDIIntegrationTests.cs and PortSystemIntegrationTests.cs

private void RegisterModulePorts()
{
    var portDiscovery = _serviceProvider.GetRequiredService<PortDiscovery>();
    var portRegistry = _serviceProvider.GetRequiredService<IPortRegistry>();

    var moduleTypes = new[]
    {
        typeof(LLMModule),
        typeof(ChatInputModule),
        typeof(ChatOutputModule),
        typeof(HeartbeatModule)
    };

    foreach (var moduleType in moduleTypes)
    {
        try
        {
            var ports = portDiscovery.DiscoverPorts(moduleType);
            var moduleName = moduleType.Name; // "LLMModule", "ChatInputModule", etc.
            portRegistry.RegisterPorts(moduleName, ports);
            _logger.LogInformation("Registered {Count} ports for {Module}", ports.Count, moduleName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register ports for {Module}, skipping", moduleType.Name);
        }
    }
}
```

### Pattern to Add: Module InitializeAsync at Startup
```csharp
// LLMModule and ChatOutputModule have meaningful InitializeAsync (set up EventBus subscriptions)
// ChatInputModule and HeartbeatModule have no-op InitializeAsync — safe to call either way

private async Task InitializeModulesAsync(CancellationToken cancellationToken)
{
    var modules = new IModuleExecutor[]
    {
        _serviceProvider.GetRequiredService<LLMModule>(),
        _serviceProvider.GetRequiredService<ChatInputModule>(),
        _serviceProvider.GetRequiredService<ChatOutputModule>(),
        _serviceProvider.GetRequiredService<HeartbeatModule>()
    };

    foreach (var module in modules)
    {
        try
        {
            await module.InitializeAsync(cancellationToken);
            _logger.LogInformation("Initialized module: {Module}", module.Metadata.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize module {Module}, skipping", module.Metadata.Name);
        }
    }
}
```

### Pattern to Remove: Demo Module Fallback in Editor.razor
```csharp
// REMOVE this block from Editor.razor OnInitialized():
if (_portRegistry.GetAllPorts().Count == 0)
{
    RegisterDemoModules();
}

// REMOVE the entire RegisterDemoModules() method
```

The `ModulePalette` already reads from `IPortRegistry.GetAllPorts()` — once real modules are registered at startup, the palette will show them automatically. No changes needed to `ModulePalette.razor`.

### Module Name Convention
All four modules use `moduleType.Name` as their module name (e.g., `"LLMModule"`). This is confirmed by:
- `ModuleMetadataRecord` in each module: `new ModuleMetadataRecord("LLMModule", ...)`, `new ModuleMetadataRecord("ChatInputModule", ...)`, etc.
- `PortMetadata` constructor: `new PortMetadata(attr.Name, attr.Type, PortDirection.Input, moduleName)` where `moduleName = moduleType.Name`
- `IPortRegistry` is keyed by this same string

This is consistent — `moduleType.Name` == `module.Metadata.Name` for all four modules.

### Anti-Patterns to Avoid
- **Resolving singletons via scope:** Module singletons should be resolved directly from `_serviceProvider`, not via `CreateScope()`. Scoped resolution of singletons works but is misleading.
- **Calling InitializeAsync multiple times:** `LLMModule` and `ChatOutputModule` add EventBus subscriptions in `InitializeAsync`. Calling it twice would double-subscribe. The startup service must call it exactly once. Since modules are singletons, this is naturally safe as long as `StartAsync` is only called once (guaranteed by `IHostedService`).
- **Keeping demo module fallback:** The `RegisterDemoModules()` guard in `Editor.razor` must be removed. If left in, it will never fire (registry is now populated at startup), but it's dead code that misleads future readers.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Port attribute scanning | Custom reflection loop | `PortDiscovery.DiscoverPorts(Type)` | Already handles `InputPortAttribute` + `OutputPortAttribute`, tested |
| Module name resolution | Custom naming logic | `moduleType.Name` | Matches `ModuleMetadataRecord` convention used in all four modules |
| Startup ordering | Custom startup coordinator | `IHostedService.StartAsync` | ASP.NET Core guarantees single call, correct lifecycle |

## Common Pitfalls

### Pitfall 1: InitializeAsync Called Before EventBus Is Ready
**What goes wrong:** If `InitializeAsync()` is called before the EventBus singleton is fully constructed, subscriptions fail silently.
**Why it happens:** DI construction order is not guaranteed for singletons resolved lazily.
**How to avoid:** Resolve modules inside `StartAsync()` (not in constructor). By the time `StartAsync` runs, all singletons are constructed.
**Warning signs:** `LLMModule` or `ChatOutputModule` not responding to events despite being initialized.

### Pitfall 2: ModulePalette Loads Before WiringInitializationService Runs
**What goes wrong:** `ModulePalette.OnInitialized()` calls `LoadAvailableModules()` which reads `IPortRegistry.GetAllPorts()`. If the Blazor circuit initializes before `WiringInitializationService.StartAsync()` completes, the palette sees an empty registry.
**Why it happens:** `IHostedService` runs at app startup, but Blazor circuits can connect before hosted services finish.
**How to avoid:** `WiringInitializationService` is synchronous for port registration (no I/O needed). Port registration happens before config loading. The palette's `OnInitialized` runs after the app is fully started in practice. If this becomes an issue, `ModulePalette` can subscribe to a registry-changed event or reload on `OnAfterRenderAsync`.
**Warning signs:** Palette shows "No modules loaded" on first page load but refreshing fixes it.

### Pitfall 3: Forgetting to Remove Demo Module Fallback
**What goes wrong:** Demo modules (`TextInput`, `LLMProcessor`, `TextOutput`, `TriggerButton`) remain in the registry alongside real modules.
**Why it happens:** The `if (_portRegistry.GetAllPorts().Count == 0)` guard in `Editor.razor` only fires when registry is empty. Once real modules are registered at startup, the guard never fires — but the method still exists.
**How to avoid:** Delete `RegisterDemoModules()` and the guard block entirely from `Editor.razor`.

### Pitfall 4: Port Registration Order vs Config Load Order
**What goes wrong:** `WiringInitializationService` currently loads the last config after port registration. `ConfigurationLoader.ValidateConfiguration()` (fixed in Phase 15) looks up ports by `ModuleName`. If port registration happens after config load, validation fails.
**Why it happens:** Wrong ordering in `StartAsync`.
**How to avoid:** Always register ports first, then load config. The plan must sequence: (1) register ports, (2) initialize modules, (3) load last config.

## Code Examples

### Current WiringInitializationService.StartAsync (full context)
```csharp
// /src/OpenAnima.Core/Hosting/WiringInitializationService.cs
public async Task StartAsync(CancellationToken cancellationToken)
{
    var lastConfigPath = Path.Combine(_configDirectory, ".lastconfig");
    if (!File.Exists(lastConfigPath)) { ... return; }
    var lastConfigName = await File.ReadAllTextAsync(lastConfigPath, cancellationToken);
    // ... creates scope, loads config, calls wiringEngine.LoadConfiguration(config)
}
```

### Current DI Registration (WiringServiceExtensions.cs)
```csharp
// Modules already registered as singletons — no changes needed here
services.AddSingleton<LLMModule>();
services.AddSingleton<ChatInputModule>();
services.AddSingleton<ChatOutputModule>();
services.AddSingleton<HeartbeatModule>();
```

### Current Editor.razor Demo Fallback (to be removed)
```csharp
// Editor.razor OnInitialized — remove this block
if (_portRegistry.GetAllPorts().Count == 0)
{
    RegisterDemoModules();
}
// Remove RegisterDemoModules() method entirely
```

### PortDiscovery Usage (from existing tests)
```csharp
// Source: tests/OpenAnima.Tests/Unit/PortDiscoveryTests.cs pattern
var discovery = new PortDiscovery();
var ports = discovery.DiscoverPorts(typeof(LLMModule));
// Returns: [PortMetadata("prompt", Text, Input, "LLMModule"), PortMetadata("response", Text, Output, "LLMModule")]
```

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (net10.0) |
| Config file | none — convention-based |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ -q --filter "Category=Integration"` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/ -q` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PORT-04 | PortDiscovery discovers ports for all 4 concrete modules at startup | integration | `dotnet test tests/OpenAnima.Tests/ -q --filter "FullyQualifiedName~ModuleRuntimeInitialization"` | ❌ Wave 0 |
| RMOD-01 | LLMModule ports registered in IPortRegistry at startup | integration | same | ❌ Wave 0 |
| RMOD-02 | ChatInputModule ports registered in IPortRegistry at startup | integration | same | ❌ Wave 0 |
| RMOD-03 | ChatOutputModule ports registered in IPortRegistry at startup | integration | same | ❌ Wave 0 |
| RMOD-04 | HeartbeatModule ports registered in IPortRegistry at startup | integration | same | ❌ Wave 0 |
| EDIT-01 | IPortRegistry has real modules after startup (palette can show them) | integration | same | ❌ Wave 0 |
| RMOD-01/03 | LLMModule and ChatOutputModule InitializeAsync sets up EventBus subscriptions | unit | `dotnet test tests/OpenAnima.Tests/ -q --filter "FullyQualifiedName~ModuleTests"` | ✅ exists |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ -q --filter "FullyQualifiedName~ModuleRuntimeInitialization OR FullyQualifiedName~ModuleTests"`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/ -q`
- **Phase gate:** Full suite green (79 passing, 2 pre-existing failures excluded) before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs` — covers PORT-04, RMOD-01 through RMOD-04, EDIT-01
  - Test: `WiringInitializationService_RegistersAllModulePorts` — verifies all 4 modules have ports in registry after `StartAsync`
  - Test: `WiringInitializationService_InitializesModules_EventBusSubscriptionsActive` — verifies LLMModule and ChatOutputModule respond to events after startup
  - Test: `PortRegistry_HasRealModules_NotDemoModules` — verifies registry contains `LLMModule`, `ChatInputModule`, `ChatOutputModule`, `HeartbeatModule` (not `TextInput`, `LLMProcessor`, etc.)

## Open Questions

1. **ModulePalette timing race**
   - What we know: `IHostedService.StartAsync` runs before the app accepts HTTP requests in ASP.NET Core
   - What's unclear: Whether Blazor Server circuits can connect before hosted services complete in edge cases
   - Recommendation: Proceed without a fix; add a note in the plan to monitor. If palette shows empty on first load, `ModulePalette` can call `LoadAvailableModules()` in `OnAfterRenderAsync(firstRender: true)` as a fallback.

2. **HeartbeatModule and HeartbeatLoop relationship**
   - What we know: `HeartbeatModule` implements `ITickable`. `HeartbeatLoop` calls `TickAsync()` on registered tickables. `HeartbeatModule.InitializeAsync()` is a no-op.
   - What's unclear: Whether `HeartbeatModule` needs to be registered with `HeartbeatLoop` as part of this phase, or whether that is Phase 17 scope.
   - Recommendation: Phase 16 only calls `InitializeAsync()` (no-op for HeartbeatModule). HeartbeatLoop integration is Phase 17 scope (E2E-01).

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection — all findings verified against actual source files
  - `/src/OpenAnima.Core/Hosting/WiringInitializationService.cs` — current startup behavior
  - `/src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` — DI registrations
  - `/src/OpenAnima.Core/Ports/PortDiscovery.cs` — discovery API
  - `/src/OpenAnima.Core/Ports/IPortRegistry.cs` + `PortRegistry.cs` — registry API
  - `/src/OpenAnima.Core/Modules/LLMModule.cs`, `ChatInputModule.cs`, `ChatOutputModule.cs`, `HeartbeatModule.cs` — module implementations
  - `/src/OpenAnima.Core/Components/Pages/Editor.razor` — demo fallback location
  - `/src/OpenAnima.Core/Components/Shared/ModulePalette.razor` — palette reads from registry
  - `/tests/OpenAnima.Tests/Modules/ModuleTests.cs` — existing module unit tests
  - `/tests/OpenAnima.Tests/Integration/WiringDIIntegrationTests.cs` — DI integration patterns

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all types exist and are tested
- Architecture: HIGH — patterns directly observed in working code
- Pitfalls: HIGH — derived from reading actual code paths and test patterns

**Research date:** 2026-02-27
**Valid until:** 2026-03-27 (stable codebase, no fast-moving dependencies)
