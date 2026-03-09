# Phase 26: Module Configuration UI - Context

**Gathered:** 2026-03-01
**Status:** Ready for planning

<domain>
## Phase Boundary

用户可以在编辑器中点击模块节点，右侧侧边栏显示该模块的详情和配置表单，配置修改自动保存并按 Anima 独立持久化。创建模块、连接模块、内置模块实现属于其他阶段。

</domain>

<decisions>
## Implementation Decisions

### 详情面板位置和交互
- 配置面板位于编辑器右侧侧边栏（复用现有 ModuleDetailSidebar 组件模式）
- 面板上部显示模块元数据（名称、版本、描述、端口），下部显示配置表单
- 单选模式：点击模块节点打开面板，点击另一个模块切换内容
- 仅通过右上角 × 关闭按钮关闭面板

### 保存和持久化行为
- 自动保存：用户修改配置项后立即保存，无需手动点击保存按钮
- 保存成功后显示短暂的 Toast 成功提示
- 配置按 Anima 独立持久化，切换 Anima 时显示对应 Anima 的配置
- 验证失败时显示内联错误消息，保留用户输入，允许修正后重试

### Claude's Discretion
- Toast 通知的具体样式和持续时间
- 自动保存的防抖延迟时间（避免每次按键都触发保存）
- 配置表单字段的具体布局和间距

</decisions>

<specifics>
## Specific Ideas

- 无特定参考 — 遵循现有 ModuleDetailSidebar 的视觉风格

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ModuleDetailSidebar.razor`: 已有右侧侧边栏组件，显示模块元数据和启用/禁用操作，可扩展添加配置表单区域
- `ModuleDetailModal.razor`: 模态框版本，可参考其内容布局
- `ConfirmDialog.razor`: 对话框组件，可参考其模式
- `EditorStateService`: 管理编辑器选中状态，已有 `DeleteSelected()` 和 `ClearSelection()` 方法，可扩展选中模块节点的追踪

### Established Patterns
- 侧边栏显示/隐藏：通过 `IsVisible` 参数和 CSS class `.detail-sidebar.visible` 控制
- i18n：使用 `IStringLocalizer<SharedResources>` 和 `L["key"]` 模式，所有 UI 文本需要添加 i18n key
- 服务注入：通过 `@inject` 注入服务，`IAnimaContext` 获取当前 Anima ID
- 状态变更通知：通过事件订阅（`OnStateChanged`）触发 `StateHasChanged()`

### Integration Points
- `Editor.razor`：需要在 `canvas-area` 旁边添加配置侧边栏区域
- `EditorStateService`：需要扩展以追踪当前选中的模块节点（`SelectedNodeId`）
- `IAnimaModuleStateService`：已有按 Anima 存储模块启用状态的模式，配置持久化可参考此模式
- `NodeCard.razor`：编辑器中的模块节点卡片，需要添加点击选中事件

</code_context>

<deferred>
## Deferred Ideas

- 无 — 讨论保持在阶段范围内

</deferred>

---

*Phase: 26-module-configuration-ui*
*Context gathered: 2026-03-01*
