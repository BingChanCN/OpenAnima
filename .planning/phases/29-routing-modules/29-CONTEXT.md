# Phase 29: Routing Modules - Context

**Gathered:** 2026-03-12
**Status:** Ready for planning

<domain>
## Phase Boundary

AnimaInputPort、AnimaOutputPort、AnimaRoute 三个内置模块，用户可在可视化编辑器中拖拽连线，实现端到端跨 Anima 请求-响应路由。不涉及 LLM — 纯模块接线演示。

</domain>

<decisions>
## Implementation Decisions

### Correlation ID 传递方式
- 采用隐式元数据通道：ModuleEvent<T> 新增 `Dictionary<string, string>? Metadata` 属性，默认 null
- AnimaInputPort 输出事件时在 Metadata 中附带 `correlationId`
- WiringEngine 的 DataCopyHelper 转发事件时全量复制 Metadata 到下游事件
- 中间模块（LLM、FixedText 等）无需感知 correlationId，只处理 Payload
- AnimaOutputPort 从接收事件的 Metadata["correlationId"] 取出 ID，调用 `router.CompleteRequest()`

### 请求投递方式
- CrossAnimaRouter 主动推送：RouteRequestAsync 内部通过 AnimaRuntimeManager 获取目标 Anima 的 EventBus 实例，直接发布事件触发 AnimaInputPort
- CrossAnimaRouter 需要新增对 IAnimaRuntimeManager 的依赖

### AnimaInputPort 模块
- 单输出端口设计：只有一个 "request" (Text) 输出端口，输出收到的请求 payload
- correlationId 通过 Metadata 隐式传递，不暴露为端口
- 侧边栏配置项：服务名称（必填）、服务描述（必填）、输入格式提示（可选，如 "JSON" 或 "纯文本"）
- InitializeAsync 时向 CrossAnimaRouter 注册端口（名称 + 描述）
- ShutdownAsync 时注销端口

### AnimaOutputPort 模块
- 单输入端口 "response" (Text)，接收响应数据
- 侧边栏配置：下拉菜单列出当前 Anima 已注册的 InputPort 名称，用户选择匹配的服务
- 从接收事件的 Metadata 中提取 correlationId，调用 router.CompleteRequest()

### AnimaRoute 模块
- 2 输入 + 2 输出端口设计：
  - 输入：request (Text) + trigger (Trigger)
  - 输出：response (Text) + error (Text)
- 收到 trigger 信号时，将 request 端口的内容发送到目标 Anima
- 侧边栏配置：级联下拉菜单 — 第一个选目标 Anima，第二个自动加载该 Anima 的已注册 InputPort 列表
- 侧边栏只显示配置表单，不显示运行时状态

### 错误输出行为
- error 端口输出 JSON 结构化内容，如 `{"error":"Timeout","target":"animaB::summarize","timeout":30}`
- response 和 error 互斥输出：成功时只触发 response，失败时只触发 error
- 错误类型对应 CrossAnimaRouter 的 RouteErrorKind：Timeout、NotFound、Cancelled、Failed

### Claude's Discretion
- ModuleEvent Metadata 字段的具体实现细节（属性命名、null 处理）
- DataCopyHelper 中 Metadata 复制的具体实现
- 三个模块的 DI 注册方式和初始化顺序
- 级联下拉菜单的 UI 刷新策略（实时 vs 手动刷新）
- AnimaRoute 内部 request 数据的暂存机制（收到 request 后等待 trigger）

</decisions>

<specifics>
## Specific Ideas

- Phase 28 STATE.md 中标注的待决事项"Correlation ID passthrough"已锁定为隐式元数据通道方案
- Phase 28 STATE.md 中标注的"Routing marker format"属于 Phase 30 范畴，本阶段不涉及
- AnimaRoute 的 ExecuteAsync 必须 await 响应（roadmap Key Risk），不能 fire-and-forget

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `CrossAnimaRouter` (src/OpenAnima.Core/Routing/CrossAnimaRouter.cs): 已实现 RegisterPort、RouteRequestAsync、CompleteRequest 全套 API，Phase 29 模块直接调用
- `ICrossAnimaRouter` 接口: 已定义 CompleteRequest(correlationId, payload) 签名，AnimaOutputPort 直接使用
- `FixedTextModule` (src/OpenAnima.Core/Modules/FixedTextModule.cs): 参考模块实现模式 — IModuleExecutor、EventBus 订阅、config 读取、状态跟踪
- `IAnimaModuleConfigService`: 已有 GetConfig(animaId, moduleName) 方法，用于读取侧边栏配置
- `AnimaRuntimeManager`: 已有 GetOrCreateRuntime() 方法获取目标 Anima 的 EventBus 实例
- `EditorConfigSidebar`: 已支持 text/textarea/password 类型配置表单，需扩展支持 dropdown 类型

### Established Patterns
- 模块通过 `[InputPort]`/`[OutputPort]` 属性声明端口
- 模块实现 `IModuleExecutor` 接口，通过 EventBus 订阅/发布通信
- 事件命名约定：`{ModuleName}.port.{PortName}`
- 配置通过 `IAnimaModuleConfigService` 以 key-value 形式持久化
- 结果类型使用 record + 静态工厂方法（RouteResult.Ok / RouteResult.Failed）

### Integration Points
- `WiringServiceExtensions.AddWiringServices()`: 注册三个新模块为 singleton
- `WiringInitializationService`: 注册新模块的端口元数据
- `DataCopyHelper`: 需要修改以支持 Metadata 全量复制
- `ModuleEvent<T>` (OpenAnima.Contracts): 需要新增 Metadata 字段
- `CrossAnimaRouter`: 需要新增 IAnimaRuntimeManager 依赖以实现推送投递
- `EditorConfigSidebar`: 需要支持 dropdown 配置类型（级联下拉）

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 29-routing-modules*
*Context gathered: 2026-03-12*
