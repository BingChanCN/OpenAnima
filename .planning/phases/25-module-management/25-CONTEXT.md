# Phase 25: Module Management - Context

**Gathered:** 2026-03-01
**Status:** Ready for planning

<domain>
## Phase Boundary

用户可以安装、卸载、启用和禁用模块，并查看模块元数据。每个 Anima 可以独立启用或禁用模块。模块配置界面属于 Phase 26，不在本阶段范围内。

</domain>

<decisions>
## Implementation Decisions

### 安装流程
- 使用文件选择器让用户选择 .oamod 文件进行安装
- 安装过程中显示简单加载提示（"正在安装..."）和加载动画
- 安装失败时显示错误弹窗，包含失败原因和建议操作
- 安装成功后显示成功提示消息
- 新安装的模块默认对所有 Anima 禁用，需要用户手动启用

### 启用/禁用机制
- 通过右键菜单启用或禁用模块
- 使用彩色徽章显示状态（绿色=已启用，灰色=已禁用）
- 模块列表显示当前活跃 Anima 的模块状态
- 切换 Anima 时，列表自动更新显示新 Anima 的模块状态
- 每个 Anima 独立管理模块启用状态

### 模块列表界面
- 使用卡片式布局，类似 AnimaListPanel 的风格
- 每个模块卡片显示：模块名称、版本号、状态徽章
- 提供搜索框，支持按模块名称搜索（类似 ModulePalette）
- 空状态显示友好提示："暂无已安装的模块，点击安装按钮开始"，并显示安装按钮

### 模块元数据展示
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

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- **ModuleService** (src/OpenAnima.Core/Services/ModuleService.cs): 已有 LoadModule、UnloadModule、ScanAndLoadAll、GetAvailableModules 方法，可直接使用
- **OamodExtractor** (src/OpenAnima.Core/Plugins/OamodExtractor.cs): 处理 .oamod 包解压
- **PluginRegistry** (src/OpenAnima.Core/Plugins/PluginRegistry.cs): 线程安全的模块注册表，提供 Register/Unregister 方法
- **AnimaListPanel** (src/OpenAnima.Core/Components/Shared/AnimaListPanel.razor): 卡片式布局参考，包含右键菜单、确认对话框
- **ModulePalette** (src/OpenAnima.Core/Components/Shared/ModulePalette.razor): 搜索框实现参考
- **ConfirmDialog** (src/OpenAnima.Core/Components/Shared/ConfirmDialog.razor): 确认对话框组件
- **IStringLocalizer**: 国际化支持，所有文本需要本地化

### Established Patterns
- 卡片式布局：AnimaListPanel 使用卡片展示 Anima，可复用相同风格
- 右键菜单：AnimaContextMenu 提供右键菜单交互模式
- 搜索过滤：ModulePalette 使用 @bind 实现实时搜索
- 状态管理：使用 StateHasChanged() 触发 UI 更新
- 国际化：所有用户可见文本使用 IStringLocalizer

### Integration Points
- 模块管理 UI 需要注入 IModuleService 访问模块操作
- 需要注入 IAnimaContext 获取当前活跃的 Anima
- 需要注入 IAnimaRuntimeManager 管理每个 Anima 的模块启用状态
- 通过 SignalR (RuntimeHub) 通知模块状态变化

</code_context>

<specifics>
## Specific Ideas

- 模块列表的卡片风格应该与 AnimaListPanel 保持一致，使用相同的阴影、圆角、hover 效果
- 右键菜单的交互方式参考 AnimaContextMenu，提供"启用"、"禁用"、"卸载"、"查看详情"等选项
- 搜索框的实现参考 ModulePalette，使用 @bind:event="oninput" 实现实时过滤
- 确认对话框使用 ConfirmDialog 组件，卸载时显示"确定要卸载模块 \"{模块名}\" 吗？此操作将删除模块文件。"

</specifics>

<deferred>
## Deferred Ideas

无 — 讨论保持在阶段范围内

</deferred>

---

*Phase: 25-module-management*
*Context gathered: 2026-03-01*
