# Phase 9: Chat UI with Streaming - Context

**Gathered:** 2026-02-25
**Status:** Ready for planning

<domain>
## Phase Boundary

Dashboard 内的实时聊天界面。用户可以发送消息、查看对话历史、实时看到 LLM 流式响应，并对消息进行复制和重新生成操作。多会话管理、消息编辑、导出等高级功能属于后续 Phase。

</domain>

<decisions>
## Implementation Decisions

### 聊天布局与消息样式
- 宽消息条布局（类似 ChatGPT），消息占据大部分宽度
- 用户消息右对齐，AI 消息左对齐
- 用背景色微差区分角色（用户/AI 不同底色）
- 仅 AI 消息侧显示图标，用户消息不显示头像
- 不显示时间戳，保持界面简洁

### 输入框设计
- 固定在底部
- 支持自动扩展为多行（类似 ChatGPT）
- Shift+Enter 换行，Enter 发送
- 右侧发送按钮

### Claude's Discretion
- 流式响应的视觉效果（打字指示器、光标动画等）
- 自动滚动的具体实现方式和阈值
- Markdown 渲染风格和代码块高亮主题选择
- 复制按钮的位置和交互方式
- 重新生成按钮的位置和确认流程
- 空对话状态的引导界面设计
- 错误/断连时的提示方式
- 加载状态和骨架屏设计
- 长消息的折叠/展开处理

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

*Phase: 09-chat-ui-with-streaming*
*Context gathered: 2026-02-25*
