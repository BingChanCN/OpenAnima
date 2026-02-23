# Phase 6: Control Operations - Research

**Researched:** 2026-02-22
**Domain:** Blazor Server interactive UI, SignalR client-to-server RPC, module lifecycle management
**Confidence:** HIGH

## Summary

Phase 6 adds interactive control capabilities to the dashboard: loading/unloading modules and starting/stopping the heartbeat loop. The implementation leverages Blazor Server's built-in SignalR connection for bidirectional communication, enabling clients to invoke server-side Hub methods. Module loading uses the existing PluginLoader/PluginRegistry infrastructure with UI-triggered operations. Unloading requires careful handling of AssemblyLoadContext lifecycle and registry removal. Error handling uses simple inline error messages (no toast library needed for v1.1 scope).

**Primary recommendation:** Use SignalR Hub methods for control operations (LoadModuleAsync, UnloadModuleAsync, StartHeartbeatAsync, StopHeartbeatAsync), implement button-level loading states with disabled flags, and display errors inline near action buttons.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**模块加载交互**
- 采用「列表内逐项加载」：在模块列表中对目标模块直接点击加载。
- 加载成功后，立即更新该行状态并给出成功提示。
- 加载进行中使用按钮级加载态（按钮禁用并显示进行中状态）。
- 并发策略为「先串行后扩展」：当前版本一次只处理一个加载操作，后续可再扩展并行。

**模块卸载与错误展示**
- 卸载入口使用每项内联按钮（在模块项内直接可操作）。
- 加载/卸载采用互斥按钮关系：按当前状态显示对应动作。
- 模块操作失败通过 Toast 展示错误。
- 错误信息以简短描述为主，不展示完整技术细节。
- 卸载后从「已启用模块」列表移除（回到未启用集合）。

**心跳控制方式**
- 使用单按钮切换模式（根据状态显示"启动心跳/停止心跳"）。
- 点击后按钮进入加载态并暂时禁用，完成后恢复。
- 若"停止心跳"失败，保持当前运行状态与按钮语义一致（不提前切换为"启动心跳"）。

### Claude's Discretion
- 心跳控制按钮的具体页面位置（仅 Heartbeat 页面，或同时在总览区域提供）由 Claude 按现有页面结构决定。
- 心跳控制按钮的最终文案与视觉细节由 Claude 结合现有 UI 风格确定。

### Deferred Ideas (OUT OF SCOPE)
- 在线模块市场下载并接入仪表盘加载流程（模块市场集成能力）。
- "组装实例 / 当前实例"导向的多实例模块切换与装配能力。
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MOD-08 | User can load a new module via file picker from the dashboard | Blazor InputFile component for directory selection, SignalR Hub method for server-side loading, existing PluginLoader infrastructure |
| MOD-09 | User can unload a loaded module via button click from the dashboard | SignalR Hub method for unload, PluginRegistry removal, AssemblyLoadContext unloading patterns |
| MOD-10 | User sees error message when a module operation fails | Inline error display patterns, ModuleOperationResult error handling |
| BEAT-02 | User can start and stop the heartbeat loop from the dashboard | SignalR Hub methods invoking IHeartbeatService, button loading states, state synchronization |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Blazor Server | .NET 8.0 | Interactive UI framework | Already in use, SignalR built-in for bidirectional RPC |
| SignalR | ASP.NET Core 8.0 | Client-to-server method invocation | Native Blazor Server transport, no additional setup needed |
| InputFile | Blazor built-in | File/directory selection | Standard Blazor component for file operations |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Pure CSS | N/A | Button loading states, error display | Consistent with Phase 4 decision (no component library) |
| JavaScript Interop | Blazor built-in | Directory picker (if needed) | Browser file API limitations may require JS fallback |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Pure CSS loading states | MudBlazor/Blazorise | User decided "no component library for v1.1", can add later if needed |
| Inline error messages | Blazored.Toast | Toast library adds dependency, inline errors simpler for v1.1 scope |
| SignalR Hub methods | REST API endpoints | Hub methods reuse existing connection, no new HTTP endpoints needed |

**Installation:**
No new packages required. All capabilities available in current stack.

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Hubs/
│   ├── RuntimeHub.cs           # Add client-to-server methods here
│   └── IRuntimeClient.cs       # Server-to-client interface (existing)
├── Services/
│   ├── IModuleService.cs       # Add UnloadModule method
│   └── ModuleService.cs        # Implement unload logic
├── Components/Pages/
│   ├── Modules.razor           # Add load/unload buttons per module
│   └── Heartbeat.razor         # Add start/stop button
└── wwwroot/css/
    └── app.css                 # Add button loading state styles
```

### Pattern 1: SignalR Hub Methods for Control Operations

**What:** Define public async methods in RuntimeHub that clients can invoke via `hubConnection.InvokeAsync()`.

**When to use:** Any user-initiated control operation that modifies server state (load/unload modules, start/stop heartbeat).

**Example:**
```csharp
// Source: Existing codebase pattern + ASP.NET Core SignalR documentation
// File: Hubs/RuntimeHub.cs
public class RuntimeHub : Hub<IRuntimeClient>
{
    private readonly IModuleService _moduleService;
    private readonly IHeartbeatService _heartbeatService;

    public RuntimeHub(IModuleService moduleService, IHeartbeatService heartbeatService)
    {
        _moduleService = moduleService;
        _heartbeatService = heartbeatService;
    }

    // Client invokes: await hubConnection.InvokeAsync<ModuleOperationResult>("LoadModule", path)
    public async Task<ModuleOperationResult> LoadModule(string modulePath)
    {
        var result = _moduleService.LoadModule(modulePath);

        if (result.Success)
        {
            // Notify all clients of module count change
            await Clients.All.ReceiveModuleCountChanged(_moduleService.Count);
        }

        return result;
    }

    public async Task<ModuleOperationResult> UnloadModule(string moduleName)
    {
        var result = _moduleService.UnloadModule(moduleName);

        if (result.Success)
        {
            await Clients.All.ReceiveModuleCountChanged(_moduleService.Count);
        }

        return result;
    }

    public async Task<bool> StartHeartbeat()
    {
        try
        {
            await _heartbeatService.StartAsync();
            await Clients.All.ReceiveHeartbeatStateChanged(true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> StopHeartbeat()
    {
        try
        {
            await _heartbeatService.StopAsync();
            await Clients.All.ReceiveHeartbeatStateChanged(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

### Pattern 2: Button Loading States with Pure CSS

**What:** Disable button and show loading indicator during async operations using component state and CSS classes.

**When to use:** Any button that triggers async Hub method invocation.

**Example:**
```razor
@* Source: Existing codebase pattern (Phase 4 modal patterns) *@
@* File: Components/Pages/Heartbeat.razor *@

<button class="btn btn-primary @(isLoading ? "loading" : "")"
        disabled="@isLoading"
        @onclick="ToggleHeartbeat">
    @if (isLoading)
    {
        <span class="spinner"></span>
    }
    @(HeartbeatService.IsRunning ? "Stop Heartbeat" : "Start Heartbeat")
</button>

@if (!string.IsNullOrEmpty(errorMessage))
{
    <div class="error-message">@errorMessage</div>
}

@code {
    private bool isLoading = false;
    private string? errorMessage = null;

    [Inject] private IHeartbeatService HeartbeatService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private HubConnection? hubConnection;

    protected override async Task OnInitializedAsync()
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/runtimehub"))
            .Build();

        await hubConnection.StartAsync();
    }

    private async Task ToggleHeartbeat()
    {
        isLoading = true;
        errorMessage = null;
        StateHasChanged();

        try
        {
            bool success = HeartbeatService.IsRunning
                ? await hubConnection!.InvokeAsync<bool>("StopHeartbeat")
                : await hubConnection!.InvokeAsync<bool>("StartHeartbeat");

            if (!success)
            {
                errorMessage = "Operation failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }
    }
}
```

**CSS:**
```css
/* Source: Existing app.css patterns */
.btn {
    padding: 8px 16px;
    border: none;
    border-radius: 6px;
    font-size: 14px;
    cursor: pointer;
    transition: all 0.2s;
}

.btn-primary {
    background-color: var(--accent-color);
    color: var(--text-primary);
}

.btn-primary:hover:not(:disabled) {
    background-color: var(--accent-hover);
}

.btn:disabled {
    opacity: 0.5;
    cursor: not-allowed;
}

.btn.loading {
    position: relative;
    color: transparent;
}

.spinner {
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    width: 16px;
    height: 16px;
    border: 2px solid var(--text-primary);
    border-top-color: transparent;
    border-radius: 50%;
    animation: spin 0.6s linear infinite;
}

@keyframes spin {
    to { transform: translate(-50%, -50%) rotate(360deg); }
}

.error-message {
    margin-top: 8px;
    padding: 8px 12px;
    background-color: rgba(248, 113, 113, 0.1);
    border-left: 3px solid var(--error-color);
    color: var(--error-color);
    font-size: 13px;
    border-radius: 4px;
}
```

### Pattern 3: Module Discovery Without Browser File Picker

**What:** Scan modules directory on server side and present available modules in UI for loading.

**When to use:** Blazor Server cannot access client filesystem. Module files must be placed in server's modules directory.

**Why this approach:** Browser security prevents web apps from accessing arbitrary filesystem paths. User must manually copy module folders to server's modules directory, then use UI to load them into runtime.

**Example:**
```csharp
// Source: Existing PluginLoader.ScanDirectory pattern
// File: Services/IModuleService.cs
public interface IModuleService
{
    // Existing methods...

    /// <summary>
    /// Gets list of available modules in modules directory (not yet loaded).
    /// </summary>
    IReadOnlyList<string> GetAvailableModules(string modulesPath);
}

// File: Services/ModuleService.cs
public IReadOnlyList<string> GetAvailableModules(string modulesPath)
{
    if (!Directory.Exists(modulesPath))
        return Array.Empty<string>();

    var loadedNames = _registry.GetAllModules()
        .Select(m => m.Manifest.Name)
        .ToHashSet();

    return Directory.GetDirectories(modulesPath)
        .Select(Path.GetFileName)
        .Where(name => !loadedNames.Contains(name))
        .ToList()!;
}
```

```razor
@* File: Components/Pages/Modules.razor *@
<h2>Available Modules</h2>
@foreach (var moduleName in availableModules)
{
    <div class="module-item">
        <span>@moduleName</span>
        <button @onclick="() => LoadModule(moduleName)"
                disabled="@loadingModules.Contains(moduleName)">
            @(loadingModules.Contains(moduleName) ? "Loading..." : "Load")
        </button>
    </div>
}

<h2>Loaded Modules</h2>
@foreach (var entry in ModuleService.GetAllModules())
{
    <div class="module-item">
        <span>@entry.Module.Metadata.Name</span>
        <button @onclick="() => UnloadModule(entry.Manifest.Name)"
                disabled="@loadingModules.Contains(entry.Manifest.Name)">
            @(loadingModules.Contains(entry.Manifest.Name) ? "Unloading..." : "Unload")
        </button>
    </div>
}

@code {
    private List<string> availableModules = new();
    private HashSet<string> loadingModules = new();
}
```

### Pattern 4: Module Unloading with AssemblyLoadContext

**What:** Remove module from registry and unload its AssemblyLoadContext to free memory.

**When to use:** User clicks "Unload" button on a loaded module.

**Important:** Current PluginLoadContext uses `isCollectible: false`, which prevents unloading. Must change to `isCollectible: true` to enable unloading.

**Example:**
```csharp
// Source: .NET AssemblyLoadContext documentation + existing PluginRegistry pattern
// File: Plugins/PluginLoadContext.cs
public PluginLoadContext(string pluginPath) : base(isCollectible: true) // Changed from false
{
    _resolver = new AssemblyDependencyResolver(pluginPath);
}

// File: Plugins/PluginRegistry.cs
public class PluginRegistry
{
    // Add unregister method
    public bool Unregister(string moduleId)
    {
        if (_modules.TryRemove(moduleId, out var entry))
        {
            // Unload the assembly load context
            entry.Context.Unload();
            return true;
        }
        return false;
    }
}

// File: Services/ModuleService.cs
public ModuleOperationResult UnloadModule(string moduleName)
{
    try
    {
        if (!_registry.IsRegistered(moduleName))
        {
            return new ModuleOperationResult(moduleName, false, "Module not found");
        }

        bool removed = _registry.Unregister(moduleName);

        if (removed)
        {
            _logger.LogInformation("Unloaded module: {Name}", moduleName);
            _ = _hubContext.Clients.All.ReceiveModuleCountChanged(_registry.Count);
            return new ModuleOperationResult(moduleName, true);
        }

        return new ModuleOperationResult(moduleName, false, "Failed to unregister");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to unload module {Name}", moduleName);
        return new ModuleOperationResult(moduleName, false, ex.Message);
    }
}
```

### Anti-Patterns to Avoid

- **Don't use browser file picker for server-side file access:** Blazor Server runs on server, cannot access client filesystem. User must manually place modules in server directory.
- **Don't forget to disable buttons during async operations:** Prevents double-clicks and race conditions.
- **Don't show full exception stack traces in UI:** User constraint specifies "简短描述为主，不展示完整技术细节".
- **Don't use `isCollectible: false` if you need unloading:** Current code has this set to false, must change to true for Phase 6.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Toast notifications | Custom toast system | Inline error messages (Phase 6), defer toast library to later | User constraint says "Toast 展示错误" but no toast library in current stack. Inline errors sufficient for v1.1. |
| File upload UI | Custom file picker | InputFile component + server directory scanning | Browser security prevents arbitrary filesystem access in web apps. |
| Button loading states | JavaScript-based spinners | Pure CSS with disabled state | Consistent with Phase 4 "no component library" decision. |
| SignalR connection management | Manual WebSocket handling | Blazor's built-in HubConnection | Blazor Server already uses SignalR, reuse existing connection. |

**Key insight:** Blazor Server's architecture means UI runs on server with SignalR transport. Don't fight this model by trying to access client filesystem or build custom real-time protocols.

## Common Pitfalls

### Pitfall 1: Forgetting AssemblyLoadContext Collectibility

**What goes wrong:** Calling `Unload()` on a non-collectible AssemblyLoadContext does nothing. Module stays in memory.

**Why it happens:** Current code has `isCollectible: false` in PluginLoadContext constructor (line 19 of PluginLoadContext.cs).

**How to avoid:** Change to `isCollectible: true` when implementing unload feature.

**Warning signs:** Memory usage doesn't decrease after unloading modules. Assembly still appears in debugger after unload.

### Pitfall 2: Race Conditions on Button Clicks

**What goes wrong:** User double-clicks "Load" button, triggering two simultaneous load operations. Second load fails with "already registered" error.

**Why it happens:** Button not disabled during async operation.

**How to avoid:** Set `isLoading = true` immediately in click handler, disable button with `disabled="@isLoading"`, reset in finally block.

**Warning signs:** Intermittent "already registered" errors, duplicate operations in logs.

### Pitfall 3: Not Handling Hub Connection Failures

**What goes wrong:** Hub method invocation throws exception if SignalR connection drops. UI shows cryptic error.

**Why it happens:** Network issues, server restart, connection timeout.

**How to avoid:** Wrap `InvokeAsync` calls in try-catch, show user-friendly error message like "Connection lost. Please refresh the page."

**Warning signs:** Unhandled exceptions in browser console, blank error messages in UI.

### Pitfall 4: Forgetting to Notify Other Clients

**What goes wrong:** User loads module in one browser tab, other tabs don't see the change until manual refresh.

**Why it happens:** Hub method only updates calling client, doesn't broadcast to `Clients.All`.

**How to avoid:** After successful operation, call `await Clients.All.ReceiveModuleCountChanged()` to push update to all connected clients.

**Warning signs:** Multi-tab testing shows stale data, manual refresh required to see changes.

### Pitfall 5: Module Unload Doesn't Clean Up Event Subscriptions

**What goes wrong:** Unloaded module still receives events, causing memory leaks or errors.

**Why it happens:** EventBus holds references to module's event handlers.

**How to avoid:** Add cleanup logic to unload process: call module's cleanup method (if exists), remove event subscriptions before unloading context.

**Warning signs:** Memory usage grows over load/unload cycles, exceptions from unloaded modules in logs.

## Code Examples

Verified patterns from existing codebase and official sources:

### Blazor Hub Connection Setup
```csharp
// Source: Existing Monitor.razor.cs pattern
// File: Components/Pages/Heartbeat.razor.cs
public partial class Heartbeat : IAsyncDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IHeartbeatService HeartbeatService { get; set; } = default!;

    private HubConnection? hubConnection;
    private bool isLoading = false;
    private string? errorMessage = null;

    protected override async Task OnInitializedAsync()
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/runtimehub"))
            .Build();

        // Listen for state changes from server
        hubConnection.On<bool>("ReceiveHeartbeatStateChanged", (isRunning) =>
        {
            InvokeAsync(StateHasChanged);
        });

        await hubConnection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }
    }
}
```

### Module Load/Unload UI Pattern
```razor
@* Source: Existing Modules.razor pattern + new control operations *@
@page "/modules"
@inject IModuleService ModuleService
@inject NavigationManager Navigation
@implements IAsyncDisposable

<h1 class="page-title">Modules</h1>

@if (!string.IsNullOrEmpty(globalError))
{
    <div class="error-message">@globalError</div>
}

<div class="modules-section">
    <h2>Available Modules</h2>
    @if (availableModules.Count == 0)
    {
        <p class="text-muted">No modules available to load. Place module folders in the modules directory.</p>
    }
    else
    {
        <div class="module-list">
            @foreach (var moduleName in availableModules)
            {
                <div class="module-item card">
                    <div class="module-info">
                        <span class="module-name">@moduleName</span>
                        <span class="status-indicator available">
                            <span class="status-dot"></span>Available
                        </span>
                    </div>
                    <button class="btn btn-primary @(loadingModules.Contains(moduleName) ? "loading" : "")"
                            disabled="@loadingModules.Contains(moduleName)"
                            @onclick="() => LoadModule(moduleName)">
                        @if (loadingModules.Contains(moduleName))
                        {
                            <span class="spinner"></span>
                        }
                        Load
                    </button>
                    @if (moduleErrors.TryGetValue(moduleName, out var error))
                    {
                        <div class="error-message-inline">@error</div>
                    }
                </div>
            }
        </div>
    }
</div>

<div class="modules-section">
    <h2>Loaded Modules</h2>
    @if (ModuleService.Count == 0)
    {
        <p class="text-muted">No modules loaded.</p>
    }
    else
    {
        <div class="module-list">
            @foreach (var entry in ModuleService.GetAllModules())
            {
                var moduleName = entry.Manifest.Name;
                <div class="module-item card">
                    <div class="module-info">
                        <span class="module-name">@moduleName</span>
                        <span class="status-indicator loaded">
                            <span class="status-dot"></span>Loaded
                        </span>
                    </div>
                    <button class="btn btn-danger @(loadingModules.Contains(moduleName) ? "loading" : "")"
                            disabled="@loadingModules.Contains(moduleName)"
                            @onclick="() => UnloadModule(moduleName)">
                        @if (loadingModules.Contains(moduleName))
                        {
                            <span class="spinner"></span>
                        }
                        Unload
                    </button>
                    @if (moduleErrors.TryGetValue(moduleName, out var error))
                    {
                        <div class="error-message-inline">@error</div>
                    }
                </div>
            }
        </div>
    }
</div>

@code {
    private HubConnection? hubConnection;
    private List<string> availableModules = new();
    private HashSet<string> loadingModules = new();
    private Dictionary<string, string> moduleErrors = new();
    private string? globalError = null;

    protected override async Task OnInitializedAsync()
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/runtimehub"))
            .Build();

        hubConnection.On<int>("ReceiveModuleCountChanged", async (count) =>
        {
            await RefreshModuleLists();
            await InvokeAsync(StateHasChanged);
        });

        await hubConnection.StartAsync();
        await RefreshModuleLists();
    }

    private async Task RefreshModuleLists()
    {
        // Get available modules from server
        availableModules = await hubConnection!.InvokeAsync<List<string>>("GetAvailableModules");
    }

    private async Task LoadModule(string moduleName)
    {
        loadingModules.Add(moduleName);
        moduleErrors.Remove(moduleName);
        StateHasChanged();

        try
        {
            var result = await hubConnection!.InvokeAsync<ModuleOperationResult>("LoadModule", moduleName);

            if (!result.Success)
            {
                moduleErrors[moduleName] = result.Error ?? "Load failed";
            }
        }
        catch (Exception ex)
        {
            moduleErrors[moduleName] = $"Error: {ex.Message}";
        }
        finally
        {
            loadingModules.Remove(moduleName);
            StateHasChanged();
        }
    }

    private async Task UnloadModule(string moduleName)
    {
        loadingModules.Add(moduleName);
        moduleErrors.Remove(moduleName);
        StateHasChanged();

        try
        {
            var result = await hubConnection!.InvokeAsync<ModuleOperationResult>("UnloadModule", moduleName);

            if (!result.Success)
            {
                moduleErrors[moduleName] = result.Error ?? "Unload failed";
            }
        }
        catch (Exception ex)
        {
            moduleErrors[moduleName] = $"Error: {ex.Message}";
        }
        finally
        {
            loadingModules.Remove(moduleName);
            StateHasChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| REST APIs for control operations | SignalR Hub methods | ASP.NET Core 3.0+ | Reuses existing connection, no new HTTP endpoints |
| JavaScript file picker | Server-side directory scanning | Blazor Server architecture | Browser security prevents filesystem access |
| Component libraries for UI | Pure CSS | Phase 4 decision | Lightweight, no dependencies, can add library later |
| `isCollectible: false` | `isCollectible: true` | Phase 6 requirement | Enables proper module unloading and memory cleanup |

**Deprecated/outdated:**
- Browser file/directory picker APIs in Blazor Server: Not applicable due to server-side execution model
- Manual SignalR connection management: Blazor Server handles this automatically

## Open Questions

1. **Toast notification library selection**
   - What we know: User constraint mentions "Toast 展示错误" but no toast library in current stack
   - What's unclear: Should we add a toast library now or defer to later phase?
   - Recommendation: Use inline error messages for Phase 6 (simpler, no new dependency). Add toast library in future phase if user requests it.

2. **Module cleanup hooks**
   - What we know: Modules may subscribe to EventBus events
   - What's unclear: Does IModule interface need a Dispose/Cleanup method?
   - Recommendation: Add optional cleanup to unload process. Check if module implements IDisposable, call Dispose before unloading context.

3. **Concurrent load/unload operations**
   - What we know: User constraint says "先串行后扩展：当前版本一次只处理一个加载操作"
   - What's unclear: Should we enforce this at UI level or service level?
   - Recommendation: UI level is sufficient for Phase 6 (disable all load/unload buttons while any operation is in progress). Service-level locking can be added later if needed.

## Sources

### Primary (HIGH confidence)
- Existing codebase: /home/user/OpenAnima/src/OpenAnima.Core/
  - Services/ModuleService.cs - Module loading patterns
  - Services/HeartbeatService.cs - Heartbeat control interface
  - Hubs/RuntimeHub.cs - SignalR Hub structure
  - Plugins/PluginLoadContext.cs - Assembly loading (currently non-collectible)
  - Components/Pages/Monitor.razor.cs - Hub connection pattern
- Context7: /websites/mudblazor - File upload patterns, button loading states
- Context7: /websites/npmjs_package_microsoft_signalr - Client-to-server invocation patterns

### Secondary (MEDIUM confidence)
- [Blazor button loading states](https://stackoverflow.com/questions/76014055/blazor-server-button-refresh-while-waiting)
- [ASP.NET Core SignalR Hubs](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs?view=aspnetcore-10.0)
- [Blazor file uploads](https://learn.microsoft.com/en-us/aspnet/core/blazor/file-uploads?view=aspnetcore-10.0)

### Tertiary (LOW confidence)
- [AssemblyLoadContext unloading](https://jordansrowles.medium.com/real-plugin-systems-in-net-assemblyloadcontext-unloadability-and-reflection-free-discovery-81f920c83644) - Needs verification with official docs
- [Blazor directory picker limitations](https://github.com/microsoft/fluentui-blazor/discussions/1072) - Community discussion, not official guidance

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All components already in use (Blazor Server, SignalR, Pure CSS)
- Architecture: HIGH - Patterns verified in existing codebase (Hub methods, button states, error handling)
- Pitfalls: MEDIUM - AssemblyLoadContext unloading needs testing, event cleanup patterns need verification

**Research date:** 2026-02-22
**Valid until:** 2026-03-24 (30 days - stable technologies)

