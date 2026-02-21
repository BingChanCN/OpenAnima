# Phase 2: Event Bus & Heartbeat Loop - Context

**Gathered:** 2026-02-21
**Status:** Ready for planning

<domain>
## Phase Boundary

模块间通过 MediatR 事件总线通信，心跳循环以可配置间隔（默认 100ms）驱动运行时调度。心跳负责事件派发和模块 Tick 调用，重活异步执行。LLM 集成和思维循环属于后续阶段。

</domain>

<decisions>
## Implementation Decisions

### 心跳循环行为
- 每次 tick 执行两件事：派发待处理事件 + 调用每个模块的 Tick 方法
- Tick 是调度器，不是执行器——只做轻量级工作，重活抛到后台异步 Task
- 防雪崩安全网：如果上一次 tick 还没完成，跳过本次 tick，不堆叠
- 心跳间隔可配置，默认 100ms（方便调试时放慢或未来根据负载调整）

### 模块订阅方式
- 模块可在运行时动态订阅和取消事件监听
- 支持按类型订阅 + 条件过滤（如只监听 severity > Warning 的事件）
- 同时支持广播模式和定向消息（模块 A 可直接发消息给模块 B，请求-响应模式）

### 事件契约设计
- 使用泛型包装器 + 字符串名称区分事件类型（如 `Event<TPayload>` + eventName）
- 各模块自定义事件类型，不集中在 Contracts 程序集——通过字符串名称匹配实现解耦
- 事件自动携带元信息：时间戳、来源模块、事件 ID
- 事件对象可变——Handler 可以修改事件（如标记已处理）

### Claude's Discretion
- 事件监听的注册机制（接口实现 vs Attribute 标记）
- MediatR 具体集成方式和 Pipeline 配置
- 心跳循环的线程模型和异步调度实现细节
- 条件过滤的具体 API 设计

</decisions>

<specifics>
## Specific Ideas

- 用户提到 Unity FixedUpdate 的 tick 雪崩问题作为反面教材——心跳设计必须避免堆叠
- Tick 应该是调度器角色，类似游戏引擎的主循环，重计算不在主循环中执行

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 02-event-bus-heartbeat-loop*
*Context gathered: 2026-02-21*
