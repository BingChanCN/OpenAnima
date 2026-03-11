# Phase 11: Port Type System & Testing Foundation - Context

**Gathered:** 2026-02-25
**Status:** Ready for planning

<domain>
## Phase Boundary

建立端口类型系统（Text、Trigger），包含类型验证和视觉反馈。模块通过 typed interface 声明端口，端口在模块加载时可被发现。同时用集成测试保护现有 v1.2 聊天工作流不回归。

</domain>

<decisions>
## Implementation Decisions

### 端口视觉设计
- 每种端口类型一个固定颜色（如 Text=蓝色、Trigger=橙色），简洁明了
- 所有端口统一圆形，仅通过颜色区分类型
- 输入端口在模块左侧，输出端口在右侧——数据从左到右流动
- 每个端口旁始终显示名称标签（如 "text_in"、"trigger_out"）

### 类型不兼容反馈
- 拖拽连线时实时提示：不兼容端口变灰淡化，兼容端口保持高亮
- 用户强行拖到不兼容端口并松开时，显示弹窗提示
- 弹窗内容包含具体类型名称（如"Text 端口不能连接到 Trigger 端口"），几秒后自动消失

### 端口声明接口
- 模块类上用 Attribute 标注端口，如 [InputPort("text", PortType.Text)]，声明式风格
- 端口元数据最小集：名称 + 类型 + 方向（输入/输出）
- 端口类型为固定枚举（Text、Trigger），后续版本再扩展新类型
- 模块加载时通过反射自动扫描 Attribute，无需手动注册

### Claude's Discretion
- 具体颜色值选择（蓝色/橙色的具体色号）
- 端口圆形的大小和间距
- 弹窗提示的具体动画和消失时间
- Fan-out（一对多连接）的视觉呈现方式
- 集成测试的具体框架和覆盖策略
- 连线的贝塞尔曲线样式

</decisions>

<specifics>
## Specific Ideas

- 端口颜色方案参考节点编辑器常见做法，Text 偏冷色、Trigger 偏暖色
- Attribute 标注风格类似 Unity 的 [SerializeField]，C# 开发者熟悉

</specifics>

<deferred>
## Deferred Ideas

None — 讨论保持在阶段范围内

</deferred>

---

*Phase: 11-port-type-system-testing-foundation*
*Context gathered: 2026-02-25*
