# Phase 26: Module Configuration UI - Research

**Researched:** 2026-03-01
**Domain:** Blazor Server UI — per-node config panel, per-Anima persistence, chat isolation
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**详情面板位置和交互**
- 配置面板位于编辑器右侧侧边栏（复用现有 ModuleDetailSidebar 组件模式）
- 面板上部显示模块元数据（名称、版本、描述、端口），下部显示配置表单
- 单选模式：点击模块节点打开面板，点击另一个模块切换内容
- 仅通过右上角 × 关闭按钮关闭面板

**保存和持久化行为**
- 自动保存：用户修改配置项后立即保存，无需手动点击保存按钮
- 保存成功后显示短暂的 Toast 成功提示
- 配置按 Anima 独立持久化，切换 Anima 时显示对应 Anima 的配置
- 验证失败时显示内联错误消息，保留用户输入，允许修正后重试

### Claude's Discretion
- Toast 通知的具体样式和持续时间
- 自动保存的防抖延迟时间（避免每次按键都触发保存）
- 配置表单字段的具体布局和间距

### Deferred Ideas (OUT OF SCOPE)
- 无 — 讨论保持在阶段范围内
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| MODCFG-01 | User can click module in editor to show detail panel on right | NodeCard.HandleCardClick already calls EditorStateService.SelectNode; extend EditorStateService to expose SelectedNodeId; Editor.razor renders config sidebar when node selected |
| MODCFG-02 | User can edit module-specific configuration in detail panel | New IAnimaModuleConfigService stores Dictionary<string,string> per (animaId, moduleId); config form rendered from IConfigurableModule schema or generic key-value fields |
| MODCFG-03 | Module configuration persists per Anima | Mirror AnimaModuleStateService pattern: JSON file at data/animas/{id}/module-configs/{moduleId}.json; SemaphoreSlim + async write |
| MODCFG-04 | Configuration changes validate before saving | Inline validation in config form; validation errors shown below field; save blocked until valid |
| MODCFG-05 | Detail panel shows module status and metadata | Reuse existing ModuleDetailSidebar metadata section; add runtime state from EditorStateService.GetModuleState() |
| ANIMA-09 | Each Anima has independent chat interface | ChatSessionState is already Scoped (per-circuit); need to verify it resets on Anima switch or bind it to AnimaId |
</phase_requirements>

## Summary

Phase 26 adds two independent features: (1) a module configuration panel in the editor that opens when a node is clicked, and (2) per-Anima chat isolation. Both are well-supported by existing infrastructure.

For the config panel, the key insight is that `EditorStateService.SelectedNodeIds` already tracks selection — the phase only needs to expose a single `SelectedNodeId` property (first selected node), wire `NodeCard` click to open the sidebar, and build a new `IAnimaModuleConfigService` that mirrors `AnimaModuleStateService`'s JSON persistence pattern. The existing `ModuleDetailSidebar` component provides the visual shell; the phase extends it with a config form section below the metadata.

For ANIMA-09 (independent chat), `ChatSessionState` is already registered as `Scoped` (per-circuit), which means each Blazor Server circuit gets its own instance. The gap is that switching Anima within the same circuit does not clear the chat history. The fix is to subscribe `ChatPanel` to `IAnimaContext.ActiveAnimaChanged` and clear `ChatSessionState.Messages` on switch.

**Primary recommendation:** Build `IAnimaModuleConfigService` as a direct clone of `IAnimaModuleStateService` (same JSON-file-per-Anima pattern, same SemaphoreSlim locking), extend `EditorStateService` with a `SelectedNodeId` computed property, and add a new `EditorConfigSidebar.razor` component that the editor renders alongside the canvas.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Blazor Server | .NET 10 (project target) | UI components, event handling | Already in use throughout |
| System.Text.Json | .NET built-in | Config JSON serialization | Already used in AnimaModuleStateService |
| IStringLocalizer<SharedResources> | Microsoft.Extensions.Localization | i18n for all UI text | Established project pattern |
| xUnit | 2.9.3 | Unit tests | Already in test project |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| SemaphoreSlim | .NET built-in | Async file write locking | Any new service that writes JSON to disk |
| CancellationTokenSource | .NET built-in | Debounce for auto-save | Same pattern as EditorStateService.TriggerAutoSave |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| JSON file per module | Single JSON file for all modules | Per-module file is simpler to read/write atomically; matches existing enabled-modules.json pattern |
| Inline validation in Razor | FluentValidation | No new dependency needed; config fields are simple strings |

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Services/
│   ├── IAnimaModuleConfigService.cs     # new interface
│   └── AnimaModuleConfigService.cs      # new implementation
├── Components/
│   ├── Pages/
│   │   └── Editor.razor                 # extend: add config sidebar slot
│   └── Shared/
│       └── EditorConfigSidebar.razor    # new component (config form + metadata)
│       └── EditorConfigSidebar.razor.css
├── Resources/
│   ├── SharedResources.zh-CN.resx       # add Editor.Config.* keys
│   └── SharedResources.en-US.resx       # add Editor.Config.* keys
└── DependencyInjection/
    └── AnimaServiceExtensions.cs        # register IAnimaModuleConfigService
```

### Pattern 1: IAnimaModuleConfigService — mirror of IAnimaModuleStateService

**What:** Per-Anima, per-module config stored as `Dictionary<string,string>` in `data/animas/{animaId}/module-configs/{moduleId}.json`.

**When to use:** Any time a module node needs to store key-value configuration that survives app restart.

```csharp
// Source: mirrors AnimaModuleStateService pattern
public interface IAnimaModuleConfigService
{
    Dictionary<string, string> GetConfig(string animaId, string moduleId);
    Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config);
    Task InitializeAsync();
}
```

Implementation uses `SemaphoreSlim(1,1)` for write locking, `System.Text.Json` for serialization, and `Directory.CreateDirectory` to ensure path exists before write — identical to `AnimaModuleStateService`.

### Pattern 2: EditorStateService — expose SelectedNodeId

**What:** Add a computed property that returns the single selected node (first in `SelectedNodeIds`), and a new event `OnNodeSelected` that fires when selection changes to a node (not connection).

```csharp
// Extend EditorStateService
public string? SelectedNodeId =>
    SelectedNodeIds.Count == 1 ? SelectedNodeIds.First() : null;
```

No new event needed — `OnStateChanged` already fires on `SelectNode`. The sidebar component subscribes to `OnStateChanged` and reads `SelectedNodeId`.

### Pattern 3: EditorConfigSidebar — config form component

**What:** New Razor component placed inside `Editor.razor` alongside `EditorCanvas`. Renders when `_state.SelectedNodeId != null`.

```razor
<!-- Editor.razor — extend layout -->
<div class="editor-container" tabindex="0" @onkeydown="HandleKeyDown">
    <div class="canvas-area">
        <EditorCanvas />
    </div>
    <EditorConfigSidebar />
    <div class="palette-area">
        <ModulePalette />
    </div>
</div>
```

The sidebar uses `position: fixed; right: -400px` sliding in (same CSS as `ModuleDetailSidebar`) and becomes `.visible` when `SelectedNodeId != null`.

### Pattern 4: Auto-save with debounce

**What:** Same pattern as `EditorStateService.TriggerAutoSave` — 500ms `CancellationTokenSource` debounce.

```csharp
// In EditorConfigSidebar or a backing service
private CancellationTokenSource? _saveDebounce;

private async void TriggerAutoSave()
{
    _saveDebounce?.Cancel();
    _saveDebounce?.Dispose();
    _saveDebounce = new CancellationTokenSource();
    try
    {
        await Task.Delay(500, _saveDebounce.Token);
        await _configService.SetConfigAsync(animaId, moduleId, _currentConfig);
        ShowToast(); // brief success notification
    }
    catch (OperationCanceledException) { }
}
```

Debounce delay is Claude's discretion — 500ms matches existing editor auto-save.

### Pattern 5: ANIMA-09 — per-Anima chat isolation

**What:** `ChatSessionState` is already `Scoped` (per-circuit). The gap is that switching Anima within the same circuit does not clear messages. Fix: subscribe to `IAnimaContext.ActiveAnimaChanged` in `ChatPanel` and clear messages.

```csharp
// In ChatPanel.razor @code
protected override void OnInitialized()
{
    _animaContext.ActiveAnimaChanged += OnAnimaChanged;
    // ... existing subscriptions
}

private void OnAnimaChanged()
{
    _chatSessionState.Messages.Clear();
    InvokeAsync(StateHasChanged);
}

public ValueTask DisposeAsync()
{
    _animaContext.ActiveAnimaChanged -= OnAnimaChanged;
    // ... existing cleanup
    return ValueTask.CompletedTask;
}
```

### Anti-Patterns to Avoid
- **Storing config in ModuleNode record:** `ModuleNode` is part of `WiringConfiguration` (wiring JSON). Config is separate concern — keep it in `data/animas/{id}/module-configs/`.
- **Using CascadingValue for selected node:** Causes full subtree re-render. Use `EditorStateService.OnStateChanged` event subscription instead (established pattern).
- **Blocking save on validation error:** Per CONTEXT.md, validation failure shows inline error but does NOT block the user from continuing to type. Save is skipped when invalid.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON persistence | Custom binary/XML format | System.Text.Json (already used) | Consistent with enabled-modules.json |
| Debounce | Manual timer management | CancellationTokenSource pattern | Already proven in EditorStateService |
| i18n | Hardcoded strings | IStringLocalizer<SharedResources> | Required by project convention |
| File locking | Custom mutex | SemaphoreSlim(1,1) | Already used in AnimaModuleStateService |

**Key insight:** Every infrastructure problem in this phase is already solved in the codebase. The work is wiring existing patterns together, not building new infrastructure.

## Common Pitfalls

### Pitfall 1: Config sidebar conflicts with ModuleDetailSidebar z-index
**What goes wrong:** Both sidebars use `position: fixed; z-index: 1000`. If both are visible simultaneously, they overlap.
**Why it happens:** `ModuleDetailSidebar` is used on the Modules page; `EditorConfigSidebar` is editor-only. They should never coexist on the same page.
**How to avoid:** `EditorConfigSidebar` is only rendered inside `Editor.razor`. `ModuleDetailSidebar` is only rendered inside `Modules.razor`. No conflict.
**Warning signs:** If you see both sidebars on screen at once, check that `EditorConfigSidebar` is not accidentally added to a shared layout.

### Pitfall 2: SelectedNodeId becomes stale after node deletion
**What goes wrong:** User selects a node, then deletes it via keyboard (Delete key). `SelectedNodeIds` is cleared by `DeleteSelected()`, but the sidebar still shows the old node's config.
**Why it happens:** `EditorStateService.DeleteSelected()` calls `SelectedNodeIds.Clear()` and `NotifyStateChanged()`. The sidebar subscribes to `OnStateChanged` and re-reads `SelectedNodeId` — which will be null after deletion.
**How to avoid:** Sidebar reads `SelectedNodeId` on every `OnStateChanged` event. Since `DeleteSelected` fires `NotifyStateChanged`, the sidebar will automatically hide. No special handling needed.
**Warning signs:** Sidebar shows config for a node that no longer exists in `Configuration.Nodes`.

### Pitfall 3: Config not loading when Anima switches
**What goes wrong:** User switches Anima; sidebar still shows config from previous Anima.
**Why it happens:** `EditorStateService` is `Scoped` (per-circuit). When Anima switches, `Editor.razor` reloads the wiring configuration via `_animaRuntimeManager.GetRuntime()`, but the sidebar's in-memory config cache is not refreshed.
**How to avoid:** `EditorConfigSidebar` subscribes to `IAnimaContext.ActiveAnimaChanged` and reloads config from `IAnimaModuleConfigService` when Anima changes.
**Warning signs:** Config values from Anima A appear when viewing Anima B's module.

### Pitfall 4: Toast notification causes layout shift
**What goes wrong:** Toast appears and pushes content down, disrupting the editor layout.
**Why it happens:** Toast rendered in document flow instead of fixed position.
**How to avoid:** Toast uses `position: fixed; bottom: 1rem; right: 1rem` — same approach as common notification patterns. Does not affect layout.

### Pitfall 5: ANIMA-09 — chat history leaks between Animas
**What goes wrong:** User chats with Anima A, switches to Anima B, sees Anima A's messages.
**Why it happens:** `ChatSessionState` is Scoped (per-circuit), not per-Anima. Switching Anima does not create a new scope.
**How to avoid:** Subscribe `ChatPanel` to `ActiveAnimaChanged` and call `_chatSessionState.Messages.Clear()`. This is the minimal fix — no new service needed.
**Warning signs:** Messages from previous Anima visible after switching.

## Code Examples

### IAnimaModuleConfigService interface
```csharp
// Source: mirrors IAnimaModuleStateService pattern
namespace OpenAnima.Core.Services;

public interface IAnimaModuleConfigService
{
    /// <summary>Returns config for a module in an Anima. Empty dict if none saved.</summary>
    Dictionary<string, string> GetConfig(string animaId, string moduleId);

    /// <summary>Saves config for a module in an Anima. Persists to disk.</summary>
    Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config);

    /// <summary>Loads all saved configs from disk. Call once at startup.</summary>
    Task InitializeAsync();
}
```

### AnimaModuleConfigService — persistence path
```csharp
// data/animas/{animaId}/module-configs/{moduleId}.json
private string GetConfigPath(string animaId, string moduleId)
{
    var dir = Path.Combine(_animasRoot, animaId, "module-configs");
    Directory.CreateDirectory(dir);
    return Path.Combine(dir, $"{moduleId}.json");
}
```

### EditorConfigSidebar — subscription pattern
```csharp
// Source: mirrors AnimaListPanel / ModuleDetailSidebar subscription pattern
protected override void OnInitialized()
{
    _editorState.OnStateChanged += HandleStateChanged;
    _animaContext.ActiveAnimaChanged += HandleAnimaChanged;
    LangSvc.LanguageChanged += OnLanguageChanged;
}

private void HandleStateChanged() => InvokeAsync(StateHasChanged);
private void HandleAnimaChanged() => InvokeAsync(ReloadConfigAsync);
private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

public void Dispose()
{
    _editorState.OnStateChanged -= HandleStateChanged;
    _animaContext.ActiveAnimaChanged -= HandleAnimaChanged;
    LangSvc.LanguageChanged -= OnLanguageChanged;
}
```

### i18n key naming convention
```xml
<!-- New keys follow existing Modules.Detail.* and Editor.* namespace pattern -->
<data name="Editor.Config.Title" xml:space="preserve"><value>模块配置</value></data>
<data name="Editor.Config.NoSelection" xml:space="preserve"><value>点击模块节点查看配置</value></data>
<data name="Editor.Config.SavedToast" xml:space="preserve"><value>已保存</value></data>
<data name="Editor.Config.ValidationError" xml:space="preserve"><value>配置无效</value></data>
<data name="Editor.Config.Status" xml:space="preserve"><value>状态</value></data>
```

### DI registration in AnimaServiceExtensions
```csharp
// Add alongside existing IAnimaModuleStateService registration
services.AddSingleton<IAnimaModuleConfigService>(sp =>
    new AnimaModuleConfigService(animasRoot));
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Global chat state | Scoped ChatSessionState | Phase 24 | Per-circuit isolation; needs Anima-switch clear for full ANIMA-09 |
| Single Anima | Multi-Anima with AnimaContext | Phase 23 | All per-Anima services must key by animaId |
| No module config | New IAnimaModuleConfigService | Phase 26 | Enables MODCFG-02, MODCFG-03 |

## Open Questions

1. **What config fields does each module expose?**
   - What we know: `IModule` interface has no config schema. `LLMModule` reads from `IOptions<LLMOptions>` (appsettings), not from per-Anima config.
   - What's unclear: Phase 26 adds the UI infrastructure; Phase 27 (BUILTIN) will define actual config fields per module. For Phase 26, the config form can render generic key-value fields from whatever is stored, or show a placeholder "no configurable fields" message for modules that don't yet declare config.
   - Recommendation: Phase 26 builds the infrastructure (service + UI shell). The config form renders fields from `IAnimaModuleConfigService.GetConfig()`. If empty, show "no configuration" message. Phase 27 populates actual fields.

2. **Should EditorConfigSidebar replace or extend ModuleDetailSidebar?**
   - What we know: `ModuleDetailSidebar` is used on the Modules page (shows global module info + enable/disable). `EditorConfigSidebar` is editor-specific (shows per-node config + runtime status).
   - What's unclear: Whether to reuse `ModuleDetailSidebar` as a base or create a new component.
   - Recommendation: Create a new `EditorConfigSidebar.razor` component. The two sidebars serve different contexts (global module management vs. per-node editor config). Reusing would couple unrelated concerns.

3. **How does the config sidebar interact with the existing palette-area layout?**
   - What we know: `Editor.razor.css` uses `display: flex; flex-direction: row` with `canvas-area: flex:1` and `palette-area: width:220px`. `ModuleDetailSidebar` uses `position: fixed` (overlays, not in flow).
   - Recommendation: Use `position: fixed` for `EditorConfigSidebar` (same as `ModuleDetailSidebar`) to avoid disrupting the canvas/palette flex layout.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none (implicit) |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~AnimaModuleConfig" -x` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/ -x` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MODCFG-03 | Config persists per Anima to JSON | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~AnimaModuleConfigServiceTests" -x` | ❌ Wave 0 |
| MODCFG-04 | Validation blocks save when invalid | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~AnimaModuleConfigServiceTests" -x` | ❌ Wave 0 |
| ANIMA-09 | Chat messages clear on Anima switch | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~ChatSessionStateTests" -x` | ✅ exists (extend) |
| MODCFG-01 | Click node opens sidebar | manual | n/a — Blazor UI interaction | manual only |
| MODCFG-02 | Edit config in panel | manual | n/a — Blazor UI interaction | manual only |
| MODCFG-05 | Panel shows status + metadata | manual | n/a — Blazor UI interaction | manual only |

### Sampling Rate
- Per task commit: `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~AnimaModuleConfig" -x`
- Per wave merge: `dotnet test tests/OpenAnima.Tests/ -x`
- Phase gate: Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/AnimaModuleConfigServiceTests.cs` — covers MODCFG-03, MODCFG-04
- [ ] Extend `tests/OpenAnima.Tests/Unit/ChatSessionStateTests.cs` — add test for Anima-switch message clear (ANIMA-09)

## Sources

### Primary (HIGH confidence)
- Codebase direct read — `AnimaModuleStateService.cs`, `EditorStateService.cs`, `ModuleDetailSidebar.razor`, `ChatPanel.razor`, `NodeCard.razor`, `Editor.razor`, `AnimaServiceExtensions.cs`, `WiringConfiguration.cs`
- Codebase direct read — `SharedResources.zh-CN.resx`, `SharedResources.en-US.resx` (i18n key patterns)
- Codebase direct read — `ChatSessionState.cs`, `ChatSessionStateTests.cs` (ANIMA-09 gap analysis)

### Secondary (MEDIUM confidence)
- None required — all findings verified directly from codebase

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries already in use in the project
- Architecture: HIGH — all patterns verified from existing code
- Pitfalls: HIGH — derived from direct code analysis, not speculation

**Research date:** 2026-03-01
**Valid until:** 2026-04-01 (stable codebase, no fast-moving dependencies)
