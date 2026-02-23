# Phase 5: SignalR Real-Time Updates - Context

**Gathered:** 2026-02-22
**Status:** Ready for planning

<domain>
## Phase Boundary

通过 SignalR 将运行时状态实时推送到浏览器。用户无需手动刷新即可看到心跳 tick 计数器、per-tick 延迟数据和模块状态变化。不包含控制操作（Phase 6）或 UX 打磨（Phase 7）。

</domain>

<decisions>
## Implementation Decisions

### 实时数据展示
- 新建独立监控页面，集中展示所有实时数据
- 卡片式布局，每个指标一张卡片（tick 计数、延迟、心跳状态等）
- 数字 + 迷你图表（sparkline）展示近期趋势
- 数字更新时使用滚动动画效果，平滑过渡到新值

### 延迟警告行为
- 仅通过颜色变化表示警告，不使用图标或文字提示
- 即时反应：单次 tick 超过阈值就变色，恢复后立即回归正常色
- 三级分级：正常（绿）、注意（50-100ms 黄）、警告（>100ms 红）
- 迷你图表上标注 100ms 基准线，直观对比实际延迟

### 连接状态与断线处理
- 页面上显示状态指示器小圆点（绿/红）表示 SignalR 连接状态
- 断线后自动重连，同时显示"重连中..."状态提示
- 重连成功后自动拉取最新状态，无缝衔接

### Claude's Discretion
- 断线时实时数据区域的视觉处理方式（冻结/置灰等）
- sparkline 图表的具体实现方式和数据点数量
- 卡片间距、排列顺序等布局细节
- SignalR Hub 的技术架构和推送频率

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 05-signalr-real-time-updates*
*Context gathered: 2026-02-22*
