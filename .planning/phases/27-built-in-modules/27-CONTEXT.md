# Phase 27: Built-in Modules - Context

**Gathered:** 2026-03-02
**Status:** Ready for planning

<domain>
## Phase Boundary

构建内置模块生态系统，包括文本处理模块（固定文本、文本拼接/合并、文本拆分）、流程控制模块（条件分支）、以及可配置的 LLM 模块。用户可以在编辑器中添加这些模块，通过配置面板编辑参数，并在 Anima 运行时使用它们。

</domain>

<decisions>
## Implementation Decisions

### 固定文本模块 (FixedTextModule)
- **模板系统**：支持 `{{variable}}` 语法的模板插值
- **变量来源**：
  - 静态变量：在配置面板中定义 key-value 对（如 `name=Alice`）
  - 动态变量：通过输入端口接收（端口名即变量名，如输入端口 `input1` 对应 `{{input1}}`）
- **触发机制**：事件驱动——每次输入端口收到新数据时触发输出；无输入时可被 WiringEngine 直接执行输出静态内容
- **配置 UI**：复用 EditorConfigSidebar 的 key-value 表单 + 添加 textarea 编辑模板内容
- **端口**：动态输入端口（可选）+ 单个 Text 输出端口

### 文本处理模块

#### TextJoin 模块（合并 Concat 和 Merge）
- **功能**：将多个文本输入拼接为一个输出
- **输入端口**：动态数量的 Text 输入端口（用户可在配置中添加/删除）
- **分隔符**：可配置分隔符字符串（默认为空），在配置面板中设置
- **输出端口**：单个 Text 输出端口
- **说明**：BUILTIN-03 (concat) 和 BUILTIN-05 (merge) 功能重叠，合并为一个模块

#### TextSplit 模块
- **功能**：按分隔符拆分文本
- **分隔符类型**：字符串分隔符（如逗号、换行符等），在配置面板中设置
- **输入端口**：单个 Text 输入端口
- **输出方式**：单个 Text 输出端口，输出 JSON 数组字符串（如 `["part1", "part2", "part3"]`）
- **说明**：由于端口系统是静态定义的，采用 JSON 数组输出避免动态端口问题

### 条件分支模块 (ConditionalBranchModule)
- **条件表达式语法**：支持表达式语法，用户在配置面板中填写表达式字符串
  - 引用输入数据：使用 `input` 关键字（如 `input.contains("hello")`、`input.length > 10`、`input == "yes"`）
  - 支持的操作：字符串方法（contains、startsWith、endsWith）、比较运算符（==、!=、>、<、>=、<=）、逻辑运算符（&&、||、!）、属性访问（length）
- **输入端口**：单个 Text 输入端口
- **输出端口**：两个 Text 输出端口（`true` 和 `false`）
- **输出数据**：透传输入数据到匹配的分支，不匹配的分支不触发
- **执行逻辑**：表达式求值为 true 时触发 `true` 端口，否则触发 `false` 端口

### LLM 模块配置扩展
- **配置字段**（BUILTIN-07/08/09）：
  - `apiUrl`：LLM API 端点 URL（文本输入）
  - `apiKey`：API 密钥（文本输入，界面使用 password 类型遮罩显示）
  - `modelName`：模型名称（文本输入，如 `gpt-4`、`claude-3-opus`）
- **存储方式**：明文存储在 JSON 配置文件中（本地应用可接受）
- **安全措施**：
  - 配置面板中 API Key 输入框使用 `type="password"` 遮罩显示
  - 日志输出时脱敏处理（不输出完整 key）
- **运行时行为**：配置的 API URL/Key/Model 覆盖全局 LLMOptions，实现每个 Anima 独立配置 LLM
- **现有代码**：LLMModule 已存在，需要扩展配置读取逻辑和 EditorConfigSidebar 支持

### Heartbeat 模块可选性 (BUILTIN-10)
- **需求**：Heartbeat 模块是可选的，不是 Anima 运行的必需模块
- **实现**：HeartbeatModule 已存在，确保它不在默认配置中自动添加，用户可从模块面板手动添加

### Claude 的自由裁量权
- 模块的具体实现细节（如表达式解析器的实现、错误处理策略）
- 配置面板的具体布局和样式细节
- 日志输出格式和级别
- 单元测试和集成测试的设计

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- **IModuleExecutor 接口**：所有可执行模块的基础接口，提供 ExecuteAsync、GetState、GetLastError 方法
- **[InputPort] / [OutputPort] 属性**：用于声明模块的输入输出端口，PortRegistry 自动扫描
- **AnimaModuleConfigService**：提供 per-Anima、per-module 的 key-value 配置持久化（JSON 文件）
- **EditorConfigSidebar.razor**：已有配置表单 UI，支持 key-value 输入、自动保存、验证、toast 提示
- **EventBus 订阅模式**：模块通过 EventBus 订阅输入端口事件（如 `{ModuleName}.port.{PortName}`），发布输出端口事件
- **WiringEngine**：负责模块执行调度、数据路由、错误隔离

### Established Patterns
- **模块生命周期**：InitializeAsync（订阅输入端口）→ ExecuteAsync（处理逻辑）→ ShutdownAsync（清理订阅）
- **端口命名约定**：事件名格式为 `{ModuleName}.port.{PortName}`
- **配置加载**：模块在 ExecuteAsync 中通过 IAnimaModuleConfigService.GetConfig(animaId, moduleName) 读取配置
- **状态管理**：模块维护 ModuleExecutionState（Idle/Running/Completed/Error）和 Exception? LastError
- **端口类型**：目前只有 PortType.Text 和 PortType.Trigger

### Integration Points
- **模块注册**：新模块需要在 DI 容器中注册（AnimaServiceExtensions 或 WiringServiceExtensions）
- **PortRegistry**：自动扫描模块类上的 [InputPort]/[OutputPort] 属性，无需手动注册端口
- **EditorConfigSidebar 扩展**：需要支持 textarea 类型字段（用于模板内容）和 password 类型字段（用于 API Key）
- **LLMModule 配置集成**：需要在 LLMModule.ExecuteAsync 中读取配置并覆盖 ILLMService 的调用参数

</code_context>

<specifics>
## Specific Ideas

- **固定文本模板示例**：
  ```
  Hello {{name}}, your order {{orderId}} is ready!
  ```
  配置：`name=Alice`, `orderId=12345`
  输出：`Hello Alice, your order 12345 is ready!`

- **条件分支表达式示例**：
  - `input.contains("error")` — 检查输入是否包含 "error"
  - `input.length > 100` — 检查输入长度是否超过 100
  - `input == "yes" || input == "y"` — 检查输入是否为 "yes" 或 "y"

- **TextSplit 输出示例**：
  - 输入：`"apple,banana,cherry"`，分隔符：`,`
  - 输出：`["apple", "banana", "cherry"]`（JSON 数组字符串）

- **LLM 配置覆盖**：每个 Anima 可以配置不同的 LLM 提供商（如 Anima A 用 OpenAI，Anima B 用 Anthropic）

</specifics>

<deferred>
## Deferred Ideas

无 — 讨论保持在阶段范围内

</deferred>

---

*Phase: 27-built-in-modules*
*Context gathered: 2026-03-02*
