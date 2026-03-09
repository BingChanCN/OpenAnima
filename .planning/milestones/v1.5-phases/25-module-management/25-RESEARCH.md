# Phase 25: Module Management - Research

**Researched:** 2026-03-01
**Domain:** Blazor Server UI for module lifecycle management with per-Anima state
**Confidence:** HIGH

## Summary

Phase 25 implements a module management UI that allows users to install .oamod packages, uninstall modules, enable/disable modules per Anima, view metadata, and search/filter modules. The existing codebase provides strong foundations: OamodExtractor handles .oamod extraction, ModuleService manages global module loading, and AnimaRuntime provides per-Anima isolation. The key challenge is bridging global module installation (shared across all Animas) with per-Anima enable/disable state.

The architecture follows established patterns from AnimaListPanel (card-based UI), ModulePalette (search filtering), and the existing Modules.razor page (module operations). Blazor Server's InputFile component handles .oamod uploads with IBrowserFile, and per-Anima module state requires extending AnimaRuntime or creating a new AnimaModuleStateService.

**Primary recommendation:** Extend existing Modules.razor with card layout, add InputFile for .oamod installation, create AnimaModuleStateService for per-Anima enable/disable tracking, and implement sidebar detail panel for metadata display.


<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### 安装流程
- 使用文件选择器让用户选择 .oamod 文件进行安装
- 安装过程中显示简单加载提示（"正在安装..."）和加载动画
- 安装失败时显示错误弹窗，包含失败原因和建议操作
- 安装成功后显示成功提示消息
- 新安装的模块默认对所有 Anima 禁用，需要用户手动启用

#### 启用/禁用机制
- 通过右键菜单启用或禁用模块
- 使用彩色徽章显示状态（绿色=已启用，灰色=已禁用）
- 模块列表显示当前活跃 Anima 的模块状态
- 切换 Anima 时，列表自动更新显示新 Anima 的模块状态
- 每个 Anima 独立管理模块启用状态

#### 模块列表界面
- 使用卡片式布局，类似 AnimaListPanel 的风格
- 每个模块卡片显示：模块名称、版本号、状态徽章
- 提供搜索框，支持按模块名称搜索（类似 ModulePalette）
- 空状态显示友好提示："暂无已安装的模块，点击安装按钮开始"，并显示安装按钮

#### 模块元数据展示
- 点击模块卡片在右侧打开侧边栏显示详细信息
- 侧边栏显示内容：
  - 基本信息：名称、版本、作者、描述
  - 端口信息：输入/输出端口列表和说明
  - 安装信息：安装时间、文件大小
  - 使用情况：模块在哪些 Anima 中启用了
- 侧边栏支持直接操作：启用/禁用、卸载等
- 卸载模块时显示确认对话框，类似 AnimaListPanel 的删除确认

### Claude's Discretion
- 卡片的具体样式和间距
- 加载动画的具体实现
- 错误消息的具体文案
- 侧边栏的宽度和动画效果

### Deferred Ideas (OUT OF SCOPE)
无 — 讨论保持在阶段范围内

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MODMGMT-01 | User can view list of all installed modules | PluginRegistry.GetAllModules() provides installed modules; card layout pattern from AnimaListPanel |
| MODMGMT-02 | User can install module from .oamod package | Blazor InputFile + OamodExtractor.Extract() + ModuleService.LoadModule(); existing pattern in Modules.razor |
| MODMGMT-03 | User can uninstall module | ModuleService.UnloadModule() + Directory.Delete() for .extracted/ cleanup; ConfirmDialog for confirmation |
| MODMGMT-04 | User can enable/disable module per Anima | Requires new AnimaModuleStateService to track per-Anima enabled modules; AnimaContext provides active Anima |
| MODMGMT-05 | User can view module information | PluginManifest provides name/version/description; PluginRegistryEntry.LoadedAt for install time; PortDiscovery for ports |
| MODMGMT-06 | User can search and filter modules by name | ModulePalette pattern: @bind:event="oninput" with LINQ Where() filtering |

</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Blazor Server | .NET 10.0 | UI framework | Project standard, existing pages use Blazor Server |
| InputFile | Built-in | File upload | Native Blazor component for file selection |
| System.IO.Compression | Built-in | .oamod extraction | Already used by OamodExtractor |
| IStringLocalizer | Built-in | i18n | Phase 24 established pattern for all UI text |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| SignalR | Built-in | Real-time updates | Module count changes, state synchronization |
| System.Text.Json | Built-in | State persistence | Per-Anima module state serialization |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| InputFile | JavaScript file picker | InputFile is native Blazor, no JS interop needed |
| Card layout | Table/grid | Cards match AnimaListPanel style, better for metadata display |

**Installation:**
No new packages required — all components are .NET built-ins or existing project infrastructure.

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Components/
│   ├── Pages/
│   │   └── Modules.razor           # Enhanced with card layout + install
│   └── Shared/
│       ├── ModuleCard.razor        # Individual module card component
│       ├── ModuleDetailSidebar.razor  # Right sidebar for metadata
│       └── ModuleContextMenu.razor # Right-click menu for enable/disable
├── Services/
│   └── AnimaModuleStateService.cs  # Per-Anima enable/disable tracking
└── Anima/
    └── AnimaDescriptor.cs          # May need EnabledModules property
```

### Pattern 1: Per-Anima Module State Management
**What:** Track which modules are enabled for each Anima independently
**When to use:** MODMGMT-04 requires per-Anima enable/disable
**Example:**
```csharp
// AnimaModuleStateService.cs
public class AnimaModuleStateService
{
    private readonly string _animasRoot;
    private readonly Dictionary<string, HashSet<string>> _enabledModules = new();
    
    public bool IsModuleEnabled(string animaId, string moduleName)
    {
        return _enabledModules.TryGetValue(animaId, out var modules) 
            && modules.Contains(moduleName);
    }
    
    public async Task SetModuleEnabled(string animaId, string moduleName, bool enabled)
    {
        if (!_enabledModules.ContainsKey(animaId))
            _enabledModules[animaId] = new HashSet<string>();
            
        if (enabled)
            _enabledModules[animaId].Add(moduleName);
        else
            _enabledModules[animaId].Remove(moduleName);
            
        await SaveStateAsync(animaId);
    }
    
    private async Task SaveStateAsync(string animaId)
    {
        var path = Path.Combine(_animasRoot, animaId, "enabled-modules.json");
        var modules = _enabledModules.GetValueOrDefault(animaId, new());
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(modules));
    }
}
```

### Pattern 2: Blazor InputFile for .oamod Upload
**What:** Use InputFile component to select and upload .oamod files
**When to use:** MODMGMT-02 installation flow
**Example:**
```razor
<InputFile OnChange="HandleFileSelected" accept=".oamod" />

@code {
    private async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (!file.Name.EndsWith(".oamod"))
        {
            // Show error
            return;
        }
        
        // Save to temp location
        var tempPath = Path.Combine(Path.GetTempPath(), file.Name);
        await using var stream = file.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024); // 50MB
        await using var fileStream = File.Create(tempPath);
        await stream.CopyToAsync(fileStream);
        
        // Extract and load
        var extractedPath = OamodExtractor.Extract(tempPath, modulesBasePath);
        var result = ModuleService.LoadModule(extractedPath);
        
        // Clean up temp
        File.Delete(tempPath);
    }
}
```

### Pattern 3: Card Layout with Search Filter
**What:** Card-based module list with real-time search filtering
**When to use:** MODMGMT-01, MODMGMT-06 list and search
**Example:**
```razor
<input type="text" @bind="searchFilter" @bind:event="oninput" placeholder="Search modules..." />

<div class="module-cards">
    @foreach (var module in FilteredModules)
    {
        <div class="module-card" @onclick="() => ShowDetails(module)">
            <span class="module-name">@module.Manifest.Name</span>
            <span class="module-version">v@module.Manifest.Version</span>
            <span class="status-badge @(IsEnabled(module) ? "enabled" : "disabled")">
                @(IsEnabled(module) ? "已启用" : "已禁用")
            </span>
        </div>
    }
</div>

@code {
    private string searchFilter = "";
    
    private IEnumerable<PluginRegistryEntry> FilteredModules =>
        ModuleService.GetAllModules()
            .Where(m => string.IsNullOrWhiteSpace(searchFilter) ||
                       m.Manifest.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase));
}
```

### Pattern 4: Right Sidebar Detail Panel
**What:** Slide-in sidebar from right showing module details
**When to use:** MODMGMT-05 metadata display
**Example:**
```razor
<div class="detail-sidebar @(isVisible ? "visible" : "")">
    <div class="sidebar-header">
        <h3>@selectedModule?.Manifest.Name</h3>
        <button @onclick="Close">×</button>
    </div>
    <div class="sidebar-content">
        <div class="info-section">
            <label>Version</label>
            <span>@selectedModule?.Manifest.Version</span>
        </div>
        <div class="info-section">
            <label>Description</label>
            <span>@selectedModule?.Manifest.Description</span>
        </div>
        <div class="actions">
            <button @onclick="ToggleEnabled">
                @(IsEnabled() ? "禁用" : "启用")
            </button>
            <button @onclick="Uninstall" class="btn-danger">卸载</button>
        </div>
    </div>
</div>

<style>
.detail-sidebar {
    position: fixed;
    right: -400px;
    top: 0;
    width: 400px;
    height: 100vh;
    background: var(--bg-secondary);
    transition: right 0.3s;
    box-shadow: -2px 0 8px rgba(0,0,0,0.1);
}

.detail-sidebar.visible {
    right: 0;
}
</style>
```

### Anti-Patterns to Avoid
- **Global module enable/disable:** Don't use a single enabled flag — each Anima must track independently
- **Synchronous file operations:** Use async File I/O for .oamod extraction to avoid blocking UI
- **Missing file size limits:** InputFile.OpenReadStream() requires maxAllowedSize to prevent memory exhaustion
- **Forgetting .extracted/ cleanup:** When uninstalling, delete both PluginRegistry entry AND .extracted/{moduleName} directory

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| File upload UI | Custom drag-drop with JS | Blazor InputFile | Native component, handles browser compatibility, security |
| .oamod extraction | Custom ZIP parsing | OamodExtractor.Extract() | Already implemented, handles timestamp tracking |
| Module loading | Direct Assembly.LoadFrom | ModuleService.LoadModule() | Handles PluginRegistry, PortRegistry, EventBus injection |
| Confirmation dialogs | Custom modal | ConfirmDialog component | Already localized, consistent styling |
| Card styling | New CSS from scratch | AnimaListPanel.razor.css patterns | Consistent with existing UI, CSS variables |

**Key insight:** The codebase already has 80% of needed infrastructure. The gap is per-Anima state tracking and UI reorganization, not core functionality.

## Common Pitfalls

### Pitfall 1: Module Installation Without Per-Anima State Initialization
**What goes wrong:** Install module globally but forget to initialize per-Anima enabled state, causing inconsistent UI
**Why it happens:** ModuleService.LoadModule() is global, but enable/disable is per-Anima
**How to avoid:** After successful LoadModule(), initialize enabled state to false for all existing Animas
**Warning signs:** New module shows "enabled" for some Animas despite never being explicitly enabled

### Pitfall 2: File Upload Memory Exhaustion
**What goes wrong:** Large .oamod files (>50MB) cause OutOfMemoryException
**Why it happens:** InputFile.OpenReadStream() defaults to 512KB max size
**How to avoid:** Always specify maxAllowedSize parameter: `file.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024)`
**Warning signs:** Upload works for small modules but fails silently for larger ones

### Pitfall 3: Uninstall Leaves .extracted/ Directory
**What goes wrong:** ModuleService.UnloadModule() removes from PluginRegistry but .extracted/{moduleName} remains on disk
**Why it happens:** UnloadModule() only handles in-memory registry, not filesystem
**How to avoid:** After UnloadModule(), explicitly delete .extracted/{moduleName} directory
**Warning signs:** Disk usage grows over time, modules reappear after restart

### Pitfall 4: AnimaContext.ActiveAnimaChanged Not Subscribed
**What goes wrong:** Switch Anima but module list still shows old Anima's enable/disable state
**Why it happens:** Component doesn't subscribe to AnimaContext.ActiveAnimaChanged event
**How to avoid:** Subscribe in OnInitialized(), unsubscribe in Dispose(), call StateHasChanged() in handler
**Warning signs:** Module status badges don't update when switching Animas

### Pitfall 5: Missing i18n for Dynamic Content
**What goes wrong:** Module names, descriptions, error messages display in wrong language
**Why it happens:** Forgetting to use IStringLocalizer for all user-visible text
**How to avoid:** All text must use L["Key"] pattern, including error messages and status text
**Warning signs:** Some UI text doesn't change when switching language in Settings

## Code Examples

Verified patterns from existing codebase:

### Module Card with Status Badge
```razor
@* Source: AnimaListPanel.razor pattern *@
<div class="module-card @(isActive ? "active" : "")"
     @onclick="() => SelectModule(module)"
     @oncontextmenu="(e) => OpenContextMenu(e, module)"
     @oncontextmenu:preventDefault="true">
    <div class="module-info">
        <span class="module-name">@module.Manifest.Name</span>
        <span class="module-version">v@module.Manifest.Version</span>
    </div>
    <span class="status-badge @(IsEnabled(module) ? "enabled" : "disabled")">
        @(IsEnabled(module) ? L["Modules.Enabled"] : L["Modules.Disabled"])
    </span>
</div>
```

### Search Filter Implementation
```razor
@* Source: ModulePalette.razor *@
<input type="text"
       class="search-box"
       placeholder="@L["Modules.SearchPlaceholder"]"
       @bind="searchFilter"
       @bind:event="oninput" />

@code {
    private string searchFilter = string.Empty;
    
    private IEnumerable<PluginRegistryEntry> FilteredModules
    {
        get
        {
            if (string.IsNullOrWhiteSpace(searchFilter))
                return ModuleService.GetAllModules();
                
            return ModuleService.GetAllModules().Where(m =>
                m.Manifest.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase));
        }
    }
}
```

### Confirm Dialog Usage
```razor
@* Source: AnimaListPanel.razor *@
<ConfirmDialog IsVisible="showUninstallConfirm"
               Title="@L["Modules.UninstallTitle"]"
               Message="@($"{L["Modules.UninstallConfirm"].Value} \"{moduleToUninstall}\"?")"
               ConfirmText="@L["Modules.Uninstall"]"
               ConfirmButtonClass="btn-danger"
               OnConfirm="HandleUninstall"
               OnCancel="CancelUninstall" />
```

### AnimaContext Subscription Pattern
```csharp
// Source: AnimaListPanel.razor.cs
protected override void OnInitialized()
{
    AnimaContext.ActiveAnimaChanged += HandleActiveAnimaChanged;
    LangSvc.LanguageChanged += OnLanguageChanged;
}

private void HandleActiveAnimaChanged()
{
    InvokeAsync(StateHasChanged);
}

public ValueTask DisposeAsync()
{
    AnimaContext.ActiveAnimaChanged -= HandleActiveAnimaChanged;
    LangSvc.LanguageChanged -= OnLanguageChanged;
    return ValueTask.CompletedTask;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Global module list only | Per-Anima module instances | Phase 24 (ANIMA-08) | Each Anima needs independent enable/disable state |
| Hardcoded English text | IStringLocalizer | Phase 24 (I18N-01-04) | All new UI must use L["Key"] pattern |
| Single runtime | AnimaRuntimeManager | Phase 23 | Module state must be Anima-scoped |
| Load/unload only | Install/uninstall + enable/disable | Phase 25 (this phase) | Two-level lifecycle: global install, per-Anima enable |

**Deprecated/outdated:**
- Direct PluginRegistry access: Use ModuleService facade instead
- Global EventBus singleton: Each AnimaRuntime has isolated EventBus (Phase 24-01 decision)

## Open Questions

1. **Module metadata: Author field**
   - What we know: PluginManifest has Name, Version, Description, EntryAssembly
   - What's unclear: CONTEXT.md mentions "author" but PluginManifest doesn't have Author property
   - Recommendation: Add optional Author property to PluginManifest, or display "Unknown" if missing

2. **File size display for installed modules**
   - What we know: CONTEXT.md requires "file size" in detail sidebar
   - What's unclear: Should this be .oamod size, extracted directory size, or DLL size?
   - Recommendation: Calculate extracted directory size recursively, cache in enabled-modules.json

3. **Module enable/disable vs. load/unload**
   - What we know: Install is global, enable/disable is per-Anima
   - What's unclear: Does "disable" mean unload from AnimaRuntime.PluginRegistry, or just hide from palette?
   - Recommendation: Disabled = not loaded into AnimaRuntime.PluginRegistry, enabled = loaded and available in editor

## Validation Architecture

> Skipped — workflow.nyquist_validation not found in .planning/config.json

## Sources

### Primary (HIGH confidence)
- Existing codebase: OamodExtractor.cs, ModuleService.cs, PluginRegistry.cs, AnimaRuntimeManager.cs
- Existing UI patterns: AnimaListPanel.razor, ModulePalette.razor, ConfirmDialog.razor, Modules.razor
- Phase 24 decisions: IStringLocalizer pattern, AnimaContext event subscription

### Secondary (MEDIUM confidence)
- .NET documentation: Blazor InputFile component (built-in, well-documented)
- Project CONTEXT.md: User decisions on UI layout and interaction patterns

### Tertiary (LOW confidence)
- None — all findings verified against existing codebase

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All components are .NET built-ins or existing project code
- Architecture: HIGH - Patterns directly observed in AnimaListPanel, ModulePalette, existing Modules.razor
- Pitfalls: HIGH - Identified from existing code patterns (AnimaContext subscription, file size limits, cleanup)

**Research date:** 2026-03-01
**Valid until:** 2026-03-31 (30 days, stable .NET 10.0 platform)
