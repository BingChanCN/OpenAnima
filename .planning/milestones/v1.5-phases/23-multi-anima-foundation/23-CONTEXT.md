# Phase 23: Multi-Anima Foundation - Context

**Gathered:** 2026-02-28
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can create, list, switch, and delete independent Anima instances with isolated runtime state. Each Anima has its own configuration directory. This phase delivers the core Anima management infrastructure — per-Anima service isolation (EventBus, HeartbeatLoop, WiringEngine) belongs to Phase 24.

</domain>

<decisions>
## Implementation Decisions

### Anima 列表与侧边栏
- Anima 列表放在侧边栏中导航链接上方，Logo 区域下方
- 竖向卡片列表展示，每个卡片显示名称和状态指示器（运行中/停止）
- 当前活动 Anima 有高亮背景，类似 Discord 服务器列表
- 侧边栏收缩时，Anima 列表显示名称首字符的圆形头像，保持可切换
- 侧边栏展开时显示完整卡片
- 列表底部有 "+" 按钮作为创建新 Anima 的入口

### Anima 创建与管理交互
- 创建新 Anima：点击 + 按钮后弹出模态对话框，输入名称后确认创建（可扩展现有 ConfirmDialog 组件）
- 管理操作（重命名、克隆、删除）：右键上下文菜单触发
- 删除确认：使用确认对话框，显示 Anima 名称和警告信息（复用 ConfirmDialog）
- 克隆行为：复制所有配置（模块布局、连接、设置），但运行时状态从零开始。新 Anima 自动命名为 "原名 (Copy)"

### 默认 Anima 与迁移
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

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- ConfirmDialog.razor: 可扩展用于创建和删除确认对话框
- MainLayout.razor: 侧边栏布局需要修改以容纳 Anima 列表
- MainLayout.razor.css: 现有侧边栏样式可扩展

### Established Patterns
- Blazor Server 交互式渲染模式
- SignalR Hub 用于实时推送（RuntimeHub）
- JSON 序列化配置存储（ConfigurationLoader）
- DI 注册集中在 Program.cs（全部单例）

### Integration Points
- Program.cs: 需要注册 AnimaRuntimeManager 和 AnimaContext 服务
- MainLayout.razor: 侧边栏需要添加 Anima 列表区域
- ConfigurationLoader: 需要支持 per-Anima 配置目录
- WiringConfiguration: 可能需要关联 Anima ID

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 23-multi-anima-foundation*
*Context gathered: 2026-02-28*
