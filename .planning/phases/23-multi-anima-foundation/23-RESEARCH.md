# Phase 23: Multi-Anima Foundation - Research

**Researched:** 2026-02-28
**Domain:** Blazor Server service architecture, per-instance DI management, filesystem persistence
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Anima 列表与侧边栏
- Anima 列表放在侧边栏中导航链接上方，Logo 区域下方
- 竖向卡片列表展示，每个卡片显示名称和状态指示器（运行中/停止）
- 当前活动 Anima 有高亮背景，类似 Discord 服务器列表
- 侧边栏收缩时，Anima 列表显示名称首字符的圆形头像，保持可切换
- 侧边栏展开时显示完整卡片
- 列表底部有 "+" 按钮作为创建新 Anima 的入口

#### Anima 创建与管理交互
- 创建新 Anima：点击 + 按钮后弹出模态对话框，输入名称后确认创建（可扩展现有 ConfirmDialog 组件）
- 管理操作（重命名、克隆、删除）：右键上下文菜单触发
- 删除确认：使用确认对话框，显示 Anima 名称和警告信息（复用 ConfirmDialog）
- 克隆行为：复制所有配置（模块布局、连接、设置），但运行时状态从零开始。新 Anima 自动命名为 "原名 (Copy)"
- 首次启动（无任何 Anima）时自动创建名为 "Default" 的 Anima 并进入
- 不迁移现有单例配置数据，新的 Default Anima 从空白开始
- 每个 Anima 有独立的子目录：data/animas/{anima-id}/，包含该 Anima 的所有配置文件
- 删除 Anima 时直接删除整个目录

### Claude's Discretion
- Anima 切换时的页面过渡效果
- Anima 卡片的具体视觉样式（间距、圆角、阴影）
- 右键上下文菜单的具体实现方式
- Anima ID 的生成策略（GUID vs 短 ID）
- AnimaRuntimeManager 和 AnimaContext 的具体接口设计
- IAsyncDisposable 的实现细节

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| ANIMA-01 | User can create new Anima with custom name | AnimaRuntimeManager.CreateAsync() + modal dialog extending ConfirmDialog |
| ANIMA-02 | User can view list of all Animas in global sidebar | AnimaListPanel component in MainLayout sidebar, reads from AnimaRuntimeManager |
| ANIMA-03 | User can switch between different Animas | AnimaContext.SetActiveAnima() + CascadingValue or service event to notify layout |
| ANIMA-04 | User can delete Anima | AnimaRuntimeManager.DeleteAsync() deletes directory + ConfirmDialog |
| ANIMA-05 | User can rename Anima | AnimaRuntimeManager.RenameAsync() updates anima.json metadata |
| ANIMA-06 | User can clone existing Anima | AnimaRuntimeManager.CloneAsync() copies directory, appends " (Copy)" to name |
| ANIMA-10 | Anima configuration persists across sessions | anima.json per-directory, loaded on startup by AnimaRuntimeManager |
| ARCH-01 | AnimaRuntimeManager manages all Anima instances | Singleton service, owns Dictionary<string, AnimaInstance> |
| ARCH-02 | AnimaContext identifies current Anima for scoped services | Singleton (or CascadingValue) holding active AnimaId, used by ConfigurationLoader |
| ARCH-05 | Configuration files stored per Anima in separate directories | data/animas/{anima-id}/ directory structure |
| ARCH-06 | Service disposal prevents memory leaks (IAsyncDisposable) | AnimaRuntimeManager implements IAsyncDisposable; AnimaInstance wraps disposable resources |
</phase_requirements>

## Summary

Phase 23 introduces the core Anima management layer on top of the existing Blazor Server + singleton service architecture. The work is primarily new service infrastructure (AnimaRuntimeManager, AnimaContext, AnimaInstance) plus UI changes to MainLayout's sidebar. No new NuGet packages are needed — the project already uses .NET 8 with System.Text.Json, Blazor Server interactive rendering, and xunit for tests.

The key architectural challenge is that the current codebase registers EventBus, HeartbeatLoop, WiringEngine, and ConfigurationLoader as singletons or scoped-per-circuit. Phase 23 must introduce a per-Anima directory abstraction without yet isolating those runtime services (that's Phase 24). This phase only needs: (1) a persistent Anima registry (anima.json files on disk), (2) an in-memory manager tracking all Anima instances, (3) an active-Anima context service, and (4) sidebar UI for CRUD operations.

The right pattern is: AnimaRuntimeManager as a singleton that owns a list of AnimaDescriptor objects (id, name, created date), persisted as individual anima.json files under data/animas/{id}/. AnimaContext is a singleton holding the currently active AnimaId, with a StateChanged event that Blazor components subscribe to for re-render. ConfigurationLoader's directory path will be updated in Phase 24 to be per-Anima; for now it just needs to be aware the directory structure exists.

**Primary recommendation:** Build AnimaRuntimeManager as a plain singleton with async file I/O (System.Text.Json), AnimaContext as a singleton with an event, and the sidebar UI as a new AnimaListPanel.razor component injected into MainLayout.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | .NET 8 built-in | Serialize/deserialize anima.json | Already used by ConfigurationLoader; no extra dependency |
| Microsoft.Extensions.DependencyInjection | .NET 8 built-in | Singleton registration of AnimaRuntimeManager, AnimaContext | Existing DI pattern in Program.cs |
| Blazor Server (Interactive) | .NET 8 | UI components, event callbacks, StateHasChanged | Existing render mode |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit | 2.9.3 | Unit tests for AnimaRuntimeManager | Already in test project |
| System.IO (Directory, File, Path) | .NET 8 built-in | Per-Anima directory creation/deletion | Matches ConfigurationLoader pattern |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Plain singleton AnimaContext with event | CascadingValue<AnimaContext> | CascadingValue requires layout re-render on every change; singleton event is more targeted |
| GUID for Anima ID | Short nanoid-style ID | GUID is zero-dependency and already used in ConfigurationLoaderTests; discretion area |
| Individual anima.json per directory | Single registry.json | Per-directory is more resilient to partial corruption and matches the locked decision |

**Installation:** No new packages required.

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Anima/                        # NEW: all Anima management
│   ├── AnimaDescriptor.cs        # record: Id, Name, CreatedAt
│   ├── IAnimaRuntimeManager.cs   # interface
│   ├── AnimaRuntimeManager.cs    # singleton, owns disk + in-memory state
│   ├── IAnimaContext.cs          # interface: ActiveAnimaId, event
│   └── AnimaContext.cs           # singleton, holds active selection
├── Components/
│   └── Shared/
│       ├── AnimaListPanel.razor  # NEW: sidebar Anima list
│       ├── AnimaListPanel.razor.css
│       ├── AnimaCreateDialog.razor  # NEW: name-input modal (extends ConfirmDialog pattern)
│       └── AnimaContextMenu.razor   # NEW: right-click rename/clone/delete
└── DependencyInjection/
    └── AnimaServiceExtensions.cs # NEW: AddAnimaServices()
```

### Pattern 1: AnimaDescriptor — Immutable Record
**What:** A plain record holding Anima metadata, serialized to anima.json inside each Anima's directory.
**When to use:** Any time Anima identity or display info is needed.
**Example:**
```csharp
// Source: project pattern — matches WiringConfiguration record style
namespace OpenAnima.Core.Anima;

public record AnimaDescriptor
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }
}
```

### Pattern 2: AnimaRuntimeManager — Singleton with Async File I/O
**What:** Singleton service that loads all Anima descriptors on startup, exposes CRUD, and fires StateChanged.
**When to use:** Any component or service that needs to enumerate or mutate Animas.
**Example:**
```csharp
// Source: project pattern — mirrors ConfigurationLoader async file I/O
public class AnimaRuntimeManager : IAnimaRuntimeManager, IAsyncDisposable
{
    private readonly string _animasRoot;  // data/animas/
    private readonly Dictionary<string, AnimaDescriptor> _animas = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Action? StateChanged;

    public IReadOnlyList<AnimaDescriptor> GetAll() =>
        _animas.Values.OrderBy(a => a.CreatedAt).ToList();

    public async Task<AnimaDescriptor> CreateAsync(string name, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N")[..8]; // short ID — discretion area
        var descriptor = new AnimaDescriptor { Id = id, Name = name, CreatedAt = DateTimeOffset.UtcNow };
        var dir = Path.Combine(_animasRoot, id);
        Directory.CreateDirectory(dir);
        await SaveDescriptorAsync(descriptor, ct);
        await _lock.WaitAsync(ct);
        try { _animas[id] = descriptor; }
        finally { _lock.Release(); }
        StateChanged?.Invoke();
        return descriptor;
    }

    public async ValueTask DisposeAsync()
    {
        _lock.Dispose();
        await ValueTask.CompletedTask;
    }
}
```

### Pattern 3: AnimaContext — Singleton Active-Selection Holder
**What:** Singleton that holds the currently active AnimaId and fires StateChanged when it changes. Blazor components subscribe to re-render.
**When to use:** Any component that needs to know which Anima is active (sidebar highlight, page title, ConfigurationLoader path in Phase 24).
**Example:**
```csharp
// Source: project pattern — mirrors EventBusService facade pattern
public class AnimaContext : IAnimaContext
{
    private string? _activeAnimaId;
    public event Action? ActiveAnimaChanged;

    public string? ActiveAnimaId => _activeAnimaId;

    public void SetActive(string animaId)
    {
        if (_activeAnimaId == animaId) return;
        _activeAnimaId = animaId;
        ActiveAnimaChanged?.Invoke();
    }
}
```

### Pattern 4: Blazor Component Subscribing to Service Events
**What:** Components subscribe to service events in OnInitializedAsync and unsubscribe in DisposeAsync to trigger StateHasChanged.
**When to use:** AnimaListPanel needs to re-render when AnimaRuntimeManager.StateChanged fires.
**Example:**
```csharp
// Source: project pattern — mirrors Monitor.razor.cs IAsyncDisposable pattern
@implements IAsyncDisposable
@inject IAnimaRuntimeManager AnimaManager
@inject IAnimaContext AnimaContext

@code {
    protected override void OnInitialized()
    {
        AnimaManager.StateChanged += OnStateChanged;
        AnimaContext.ActiveAnimaChanged += OnStateChanged;
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    public ValueTask DisposeAsync()
    {
        AnimaManager.StateChanged -= OnStateChanged;
        AnimaContext.ActiveAnimaChanged -= OnStateChanged;
        return ValueTask.CompletedTask;
    }
}
```

### Pattern 5: Startup Auto-Create Default Anima
**What:** AnimaRuntimeManager.InitializeAsync() called from a hosted service or Program.cs startup. If no Animas exist, creates "Default" and sets it active.
**When to use:** Application startup, before any Blazor circuit connects.
**Example:**
```csharp
// Called from OpenAnimaHostedService.StartAsync or a new AnimaInitializationService
public async Task InitializeAsync(CancellationToken ct = default)
{
    await LoadAllFromDiskAsync(ct);
    if (_animas.Count == 0)
    {
        var defaultAnima = await CreateAsync("Default", ct);
        // AnimaContext will be set by the first circuit that connects
    }
}
```

### Pattern 6: Right-Click Context Menu in Blazor
**What:** Pure CSS + Blazor event handling for a context menu. No JS interop needed for basic positioning.
**When to use:** Anima card right-click for rename/clone/delete.
**Example:**
```razor
@* Discretion area — simplest approach: absolute-positioned div shown on @oncontextmenu *@
<div class="anima-card @(IsActive ? "active" : "")"
     @oncontextmenu="ShowContextMenu"
     @oncontextmenu:preventDefault="true">
    ...
</div>

@if (_contextMenuVisible)
{
    <div class="context-menu" style="top:@(_menuY)px; left:@(_menuX)px">
        <button @onclick="Rename">Rename</button>
        <button @onclick="Clone">Clone</button>
        <button class="danger" @onclick="Delete">Delete</button>
    </div>
    <div class="context-menu-backdrop" @onclick="HideContextMenu"></div>
}
```

### Anti-Patterns to Avoid
- **Registering AnimaRuntimeManager as Scoped:** It must be Singleton — it owns cross-circuit state (the list of all Animas). Scoped would give each Blazor circuit its own instance.
- **Storing active AnimaId in Blazor circuit state only:** AnimaContext must be Singleton so the active selection survives page navigation within the same app session.
- **Blocking file I/O in Blazor render thread:** All disk operations must be async (async Task, not .Result or .Wait()).
- **Deleting Anima directory synchronously:** Use Task.Run(() => Directory.Delete(dir, true)) to avoid blocking the render thread, matching the existing ConfigurationLoader.DeleteAsync pattern.
- **Forgetting to unsubscribe events:** Components that subscribe to AnimaManager.StateChanged must unsubscribe in DisposeAsync to prevent memory leaks (ARCH-06).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON serialization of AnimaDescriptor | Custom text serializer | System.Text.Json (already used) | Handles null, encoding, indentation; already proven in ConfigurationLoader |
| Thread-safe dictionary access | Manual lock everywhere | SemaphoreSlim for async critical sections | Async-compatible; ConcurrentDictionary is fine for reads but write sequences need coordination |
| Modal dialog for name input | New modal from scratch | Extend ConfirmDialog.razor pattern (add input field) | ConfirmDialog already has backdrop, cancel, confirm button, CSS — add an `<input>` child |
| Directory copy for clone | Manual file enumeration | Directory.GetFiles + File.Copy in a loop | Simple enough; no library needed for flat config directory |

**Key insight:** This phase is pure .NET 8 + Blazor — no new packages. The complexity is in service design and event wiring, not in library selection.

## Common Pitfalls

### Pitfall 1: AnimaContext Not Notifying Components After Switch
**What goes wrong:** User clicks a different Anima in the sidebar, AnimaContext.SetActive() is called, but the sidebar highlight and page content don't update.
**Why it happens:** Blazor components don't automatically re-render when a singleton service changes. StateHasChanged must be called explicitly via InvokeAsync.
**How to avoid:** Every component that displays active-Anima state must subscribe to AnimaContext.ActiveAnimaChanged in OnInitialized and call InvokeAsync(StateHasChanged) in the handler.
**Warning signs:** Sidebar shows wrong highlight after switching; page title still shows old Anima name.

### Pitfall 2: Race Condition on Concurrent Create/Delete
**What goes wrong:** Two browser tabs create an Anima simultaneously; both get the same short ID or one overwrites the other's directory.
**Why it happens:** Dictionary write + Directory.CreateDirectory is not atomic.
**How to avoid:** Use SemaphoreSlim(1,1) around the create/delete critical section in AnimaRuntimeManager. GUID-based IDs make collision essentially impossible even without locking, but the dictionary mutation still needs protection.
**Warning signs:** Missing Animas after rapid create operations; FileNotFoundException on load.

### Pitfall 3: Sidebar Layout Break on Collapse
**What goes wrong:** Anima list cards overflow or misalign when sidebar is in collapsed state (showing avatar initials only).
**Why it happens:** The existing sidebar CSS uses fixed widths; adding a new section without matching the collapsed/expanded CSS classes breaks layout.
**How to avoid:** Mirror the existing `@if (!SidebarCollapsed)` pattern in MainLayout.razor. In collapsed state, render only a circular avatar div with the first character of the Anima name.
**Warning signs:** Sidebar wider than expected in collapsed mode; cards visible when they should be hidden.

### Pitfall 4: Startup Timing — AnimaContext Has No Active Anima
**What goes wrong:** A Blazor component renders before AnimaRuntimeManager.InitializeAsync() completes, reads AnimaContext.ActiveAnimaId as null, and crashes or shows blank state.
**Why it happens:** Blazor circuits can connect before hosted services finish startup.
**How to avoid:** Components must handle null ActiveAnimaId gracefully (show loading state or "no Anima selected"). AnimaRuntimeManager.InitializeAsync should be called from OpenAnimaHostedService.StartAsync (which runs before circuits connect) or a new AnimaInitializationService.
**Warning signs:** NullReferenceException on first page load; blank sidebar on startup.

### Pitfall 5: Clone Copies Runtime State
**What goes wrong:** Cloned Anima inherits chat history or heartbeat tick count from the source.
**Why it happens:** If runtime state is accidentally written to the config directory (e.g., a state.json file), the directory copy picks it up.
**How to avoid:** Only copy known config files (anima.json, wiring config files). Do not copy any runtime-state files. Document which files are "config" vs "runtime" from the start.
**Warning signs:** Cloned Anima shows messages from original Anima's chat.

## Code Examples

Verified patterns from official sources and existing codebase:

### AnimaDescriptor JSON Round-Trip (matches ConfigurationLoader pattern)
```csharp
// Source: existing ConfigurationLoader.cs — same JsonSerializerOptions pattern
private static readonly JsonSerializerOptions JsonOptions = new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

private async Task SaveDescriptorAsync(AnimaDescriptor descriptor, CancellationToken ct)
{
    var dir = Path.Combine(_animasRoot, descriptor.Id);
    var path = Path.Combine(dir, "anima.json");
    await using var stream = File.Create(path);
    await JsonSerializer.SerializeAsync(stream, descriptor, JsonOptions, ct);
}

private async Task<AnimaDescriptor?> LoadDescriptorAsync(string animaDir, CancellationToken ct)
{
    var path = Path.Combine(animaDir, "anima.json");
    if (!File.Exists(path)) return null;
    await using var stream = File.OpenRead(path);
    return await JsonSerializer.DeserializeAsync<AnimaDescriptor>(stream, JsonOptions, ct);
}
```

### Load All Animas on Startup
```csharp
// Source: project pattern — mirrors ConfigurationLoader.ListConfigurations()
private async Task LoadAllFromDiskAsync(CancellationToken ct)
{
    if (!Directory.Exists(_animasRoot))
    {
        Directory.CreateDirectory(_animasRoot);
        return;
    }

    foreach (var dir in Directory.GetDirectories(_animasRoot))
    {
        var descriptor = await LoadDescriptorAsync(dir, ct);
        if (descriptor != null)
            _animas[descriptor.Id] = descriptor;
    }
}
```

### Delete Anima (async directory removal)
```csharp
// Source: project pattern — mirrors ConfigurationLoader.DeleteAsync Task.Run pattern
public async Task DeleteAsync(string animaId, CancellationToken ct = default)
{
    var dir = Path.Combine(_animasRoot, animaId);
    await _lock.WaitAsync(ct);
    try
    {
        _animas.Remove(animaId);
        if (Directory.Exists(dir))
            await Task.Run(() => Directory.Delete(dir, recursive: true), ct);
    }
    finally
    {
        _lock.Release();
    }
    StateChanged?.Invoke();
}
```

### DI Registration (AddAnimaServices extension)
```csharp
// Source: project pattern — mirrors WiringServiceExtensions.cs
public static IServiceCollection AddAnimaServices(
    this IServiceCollection services,
    string? dataRoot = null)
{
    dataRoot ??= Path.Combine(AppContext.BaseDirectory, "data");
    var animasRoot = Path.Combine(dataRoot, "animas");
    Directory.CreateDirectory(animasRoot);

    services.AddSingleton<IAnimaRuntimeManager>(sp =>
        new AnimaRuntimeManager(
            animasRoot,
            sp.GetRequiredService<ILogger<AnimaRuntimeManager>>()));

    services.AddSingleton<IAnimaContext, AnimaContext>();

    return services;
}
```

### AnimaListPanel Sidebar Integration (MainLayout.razor change)
```razor
@* Insert between logo-area and sidebar-nav in MainLayout.razor *@
<div class="anima-list-section">
    <AnimaListPanel />
</div>
```

### xunit Test Pattern for AnimaRuntimeManager
```csharp
// Source: existing ConfigurationLoaderTests.cs — IDisposable + temp directory pattern
public class AnimaRuntimeManagerTests : IAsyncDisposable
{
    private readonly string _tempRoot;
    private readonly AnimaRuntimeManager _manager;

    public AnimaRuntimeManagerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"anima-test-{Guid.NewGuid()}");
        _manager = new AnimaRuntimeManager(_tempRoot, NullLogger<AnimaRuntimeManager>.Instance);
    }

    [Fact]
    public async Task CreateAsync_PersistsDescriptorToDisk()
    {
        var descriptor = await _manager.CreateAsync("TestAnima");
        var path = Path.Combine(_tempRoot, descriptor.Id, "anima.json");
        Assert.True(File.Exists(path));
    }

    public async ValueTask DisposeAsync()
    {
        await _manager.DisposeAsync();
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single global EventBus singleton | Per-Anima EventBus (Phase 24) | Phase 24 | Phase 23 keeps existing singleton; no change yet |
| Single wiring-configs/ directory | Per-Anima data/animas/{id}/ | Phase 23 | ConfigurationLoader path will be parameterized in Phase 24 |
| No Anima concept | AnimaDescriptor + AnimaRuntimeManager | Phase 23 | Foundation for all v1.5 multi-instance work |

**Deprecated/outdated:**
- Single `wiring-configs/` root directory: Will be superseded by `data/animas/{id}/` in Phase 24 when ConfigurationLoader becomes per-Anima scoped.

## Open Questions

1. **Where does AnimaRuntimeManager.InitializeAsync() get called?**
   - What we know: OpenAnimaHostedService.StartAsync() is the existing startup hook; it already calls module scan and heartbeat start.
   - What's unclear: Should Anima initialization be added to OpenAnimaHostedService, or should a new AnimaInitializationService hosted service be created?
   - Recommendation: Add a new `AnimaInitializationService : IHostedService` to keep concerns separated. Register it after `OpenAnimaHostedService` in Program.cs.

2. **Should AnimaContext.ActiveAnimaId be persisted across app restarts?**
   - What we know: The locked decisions say Anima configuration persists (ANIMA-10), but don't specify whether the last-active Anima is remembered.
   - What's unclear: Is it acceptable to always start with the first Anima (by creation date) on restart?
   - Recommendation: Persist last-active Anima ID in a `data/state.json` file. Simple to implement, good UX. If not desired, default to first Anima alphabetically.

3. **Anima ID format (discretion area)**
   - What we know: GUID is zero-dependency and collision-proof. Short IDs (8 hex chars from GUID) are more readable in directory names.
   - Recommendation: Use `Guid.NewGuid().ToString("N")[..8]` — short, readable, effectively collision-free for single-user app, matches existing test patterns that already use Guid.NewGuid() for temp directories.

## Sources

### Primary (HIGH confidence)
- Existing codebase: `/home/user/OpenAnima/src/OpenAnima.Core/Wiring/ConfigurationLoader.cs` — async file I/O pattern, JsonSerializerOptions, Task.Run for sync file ops
- Existing codebase: `/home/user/OpenAnima/src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` — AddXxx extension method pattern for DI registration
- Existing codebase: `/home/user/OpenAnima/src/OpenAnima.Core/Components/Pages/Monitor.razor.cs` — IAsyncDisposable pattern, event subscription/unsubscription in Blazor components
- Existing codebase: `/home/user/OpenAnima/src/OpenAnima.Core/Components/Layout/MainLayout.razor` — sidebar structure to modify
- Existing codebase: `/home/user/OpenAnima/src/OpenAnima.Core/Components/Shared/ConfirmDialog.razor` — reusable modal pattern
- Existing codebase: `/home/user/OpenAnima/tests/OpenAnima.Tests/Unit/ConfigurationLoaderTests.cs` — IDisposable + temp directory test pattern

### Secondary (MEDIUM confidence)
- .NET 8 docs: SemaphoreSlim for async-compatible locking in singleton services
- Blazor Server docs: InvokeAsync(StateHasChanged) required for cross-thread UI updates from service events

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries are already in the project; no new dependencies
- Architecture: HIGH — patterns directly derived from existing codebase (ConfigurationLoader, Monitor, WiringServiceExtensions)
- Pitfalls: HIGH — derived from known Blazor Server patterns and existing code structure

**Research date:** 2026-02-28
**Valid until:** 2026-03-30 (stable .NET 8 + Blazor Server stack)
