# Phase 41: External ContextModule (SDK Validation) - Context

**Gathered:** 2026-03-18
**Status:** Ready for planning

<domain>
## Phase Boundary

通过构建一个真实的外部 ContextModule 来端到端验证 SDK 表面积。ContextModule 管理多轮对话历史，作为 ChatInputModule 和 LLMModule 之间的中间件，验证 PluginLoader DI 注入、端口系统、IModuleConfig、IModuleContext.GetDataDirectory（Phase 40）、.oamod 打包加载等全部 SDK 能力。

</domain>

<decisions>
## Implementation Decisions

### 端口设计与数据流
- 输入端口：`userMessage`（Text）接收用户消息，`llmResponse`（Text）接收 LLM 回复
- 输出端口：`messages`（Text）输出 ChatMessageInput JSON 给 LLMModule，`displayHistory`（Text）输出 ChatMessageInput JSON 用于历史展示
- 触发逻辑：
  - 收到 userMessage → 追加 {role:"user"} 到历史 → 输出完整历史到 messages 端口
  - 收到 llmResponse → 追加 {role:"assistant"} 到历史 → 保存到磁盘 → 输出更新后的历史到 displayHistory 端口
- 数据格式：两个输出端口统一使用 ChatMessageInput.SerializeList() JSON 格式

### 对话历史管理策略
- 历史长度：无限制，全量保存和发送
- System Message：通过 IModuleConfig 配置，始终作为输出历史的第一条消息（role:"system"）
- 持久化时机：每轮对话完成后（收到 llmResponse 时）写入 DataDirectory/history.json
- Anima 隔离：每个 AnimaRuntime 有独立的 ContextModule 实例，InitializeAsync 时从 DataDirectory 加载历史，天然隔离
- history.json 格式：ChatMessageInput JSON 数组（不含 system message，system message 仅在输出时动态拼接）

### 模块项目结构与打包
- 源码位置：`modules/ContextModule/`，独立 .csproj，仅引用 OpenAnima.Contracts
- 打包方式：.oamod 包（ZIP 格式），由 PluginLoader → OamodExtractor 解压后加载
- 构建自动化：MSBuild Target 实现 dotnet build 一步到位打包为 .oamod
- 输出位置：构建后 .oamod 文件直接输出到 OpenAnima.Core 的 modules/ 运行目录

### Claude's Discretion
- ContextModule 内部实现细节（线程安全、错误处理）
- MSBuild Target 的具体实现方式
- 单元测试和集成测试策略
- module.json 的 description 和 version 字段值

</decisions>

<specifics>
## Specific Ideas

- displayHistory 端口可用于未来的历史回放、搜索等 UI 功能
- system message 配置为空字符串时不输出 system 消息

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `PluginLoader.cs`: 已实现 DI 注入（Phase 38），ContractsTypeMap 包含 IModuleConfig/IModuleContext/IEventBus/ICrossAnimaRouter
- `OamodExtractor.cs`: .oamod ZIP 解压，时间戳幂等性检查
- `ChatMessageInput`: Contracts 中的 record，SerializeList/DeserializeList 已就绪
- `PortModule/`: 现有外部模块参考实现，展示端口声明和 DI 注入模式
- `IModuleConfig`: 已有 per-Anima per-module 配置读写能力

### Established Patterns
- 端口声明：`[InputPort("name", PortType.Text)]` / `[OutputPort("name", PortType.Text)]` 类级属性
- 事件命名：`"{ModuleName}.port.{portName}"` 约定
- 端口数据：Text 端口使用 string payload，通过 ModuleEvent<string> 传递
- 模块生命周期：InitializeAsync 订阅事件 → 处理 → ShutdownAsync 清理订阅
- 构造函数 DI：贪心构造函数，FullName 匹配，可选参数传 null + warning

### Integration Points
- `LLMModule.cs`: messages 输入端口接收 ChatMessageInput JSON，ContextModule 的 messages 输出连接到此
- `ChatInputModule.cs`: userMessage 输出端口，连接到 ContextModule 的 userMessage 输入
- `ChatOutputModule.cs`: displayText 输入端口，可连接 ContextModule 的 displayHistory 输出
- `IModuleContext.GetDataDirectory()`: Phase 40 前置依赖，提供 per-Anima 存储路径

### Phase 40 Dependency
- Phase 41 依赖 Phase 40 的 IModuleContext.GetDataDirectory() 实现
- 路径模式：`data/animas/{animaId}/module-data/{moduleId}/`
- ContextModule 使用 GetDataDirectory 获取 history.json 的存储位置

</code_context>

<deferred>
## Deferred Ideas

- **多渠道共享对话历史**：ContextModule 的设计不假设消息来源，未来 Telegram、飞书、游戏内聊天等渠道可以共享同一个对话历史。当前 Phase 41 只验证 SDK 能力，多渠道接入作为后续 phase 规划。

</deferred>

---

*Phase: 41-external-context-module*
*Context gathered: 2026-03-18*
