# Phase 10: Context Management & Token Counting - Context

**Gathered:** 2026-02-25
**Status:** Ready for planning

<domain>
## Phase Boundary

让对话保持在上下文窗口限制内，提供准确的 token 追踪，并在接近限制时限制发送。用户可以看到 token 用量和剩余上下文容量。聊天事件通过 EventBus 发布供其他模块消费。

持久化存储、对话历史管理、消息摘要压缩属于其他阶段。

</domain>

<decisions>
## Implementation Decisions

### Token 用量展示
- Token 用量和上下文容量是两个独立的信息，分开展示
- 位置：聊天输入框附近
- 区分输入 token（用户+系统）和输出 token（助手回复）
- 更新时机：每条消息完成后更新，不在流式过程中实时变化
- 累计记录所有对话的总 token 消耗（不仅是当前对话）
- 上下文容量接近限制时使用颜色预警（绿→黄→红）

### 截断策略
- 使用百分比阈值触发（如 80%）
- 接近限制且用户尝试发送消息时：弹窗提示并限制发送
- 不自动截断旧消息，而是阻止用户继续发送
- 截断后的用户操作（如新建对话）暂不深入设计，因为直接与 LLM 对话不是项目核心目标

### Token 计数方式
- 优先使用 API 返回的 usage 字段（最精确）
- 模型的上下文窗口大小在配置文件中指定（如 LLMOptions 中添加 MaxContextTokens）
- 区分输入/输出 token 分别计数

### EventBus 聊天事件
- 发布三个核心事件：消息发送、响应接收、截断发生
- 复用现有 EventBus 架构，与其他模块事件一致
- 截断事件包含被移除的消息数量和释放的 token 数

### Claude's Discretion
- 系统消息（system message）在截断时的保护策略
- 事件携带的具体数据结构
- 颜色预警的具体阈值设定
- token 用量和上下文容量的具体 UI 布局细节
- 百分比阈值的默认值选择

</decisions>

<specifics>
## Specific Ideas

- Token 用量的核心目的是方便计算 token 消耗成本，不是上下文管理的辅助信息
- 上下文容量需要单独展示，与 token 消耗是不同维度的信息
- 直接与 LLM 对话不是项目核心目标，所以截断后的恢复流程可以简单处理

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 10-context-management-token-counting*
*Context gathered: 2026-02-25*
