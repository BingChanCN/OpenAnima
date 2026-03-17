# Phase 38: PluginLoader DI Injection - Context

**Gathered:** 2026-03-17
**Status:** Ready for planning

<domain>
## Phase Boundary

External modules receive Contracts services (IModuleConfig, IModuleContext, IEventBus, ICrossAnimaRouter, ILogger) via constructor injection through PluginLoader. Currently PluginLoader uses `Activator.CreateInstance()` with parameterless constructor only — external modules get zero DI services. This phase replaces that with reflection-based constructor parameter resolution using FullName matching against the host DI container.

</domain>

<decisions>
## Implementation Decisions

### DI 解析策略
- PluginLoader.LoadModule() 接收 IServiceProvider 作为方法参数（不是构造函数注入）
- 跨 AssemblyLoadContext 类型匹配使用 FullName 字符串比较（与现有 IModule 发现逻辑一致）
- 反射构造函数参数，按 FullName 在 IServiceProvider 中查找匹配的已注册服务类型
- 多个构造函数时选参数最多的（贪心构造函数，类似 ASP.NET Core 约定）

### EventBus 注入迁移
- 只对外部模块使用构造函数注入，12 个内置模块保持现有属性注入不动
- 如果外部模块构造函数已注入 IEventBus，跳过属性注入
- 如果外部模块构造函数没有要求 IEventBus，回退到现有属性注入机制

### 可选 vs 必需参数行为
- Contracts 服务（IModuleConfig、IModuleContext、IEventBus、ICrossAnimaRouter）全部视为可选：解析失败传 null + warning log
- 非 Contracts 的未知参数：按 C# 构造函数参数是否有默认值判断 — 有默认值 = 可选（null + warning），没有默认值 = 必需（LoadResult 错误）

### ILogger 创建方式
- 使用非泛型 ILogger（不是 ILogger<T>）���避免跨 AssemblyLoadContext 泛型类型解析问题
- 通过 ILoggerFactory.CreateLogger(moduleType.FullName) 创建，日志类别名为模块完整类名
- ILogger 视为可选参数：ILoggerFactory 不可用时传 null + warning

### Claude's Discretion
- PluginLoader 内部实现细节（参数解析缓存、错误消息措辞）
- 单元测试策略和 mock 方式
- ScanDirectory 方法签名是否也需要 IServiceProvider 参数

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `PluginLoader.cs`: 当前 LoadModule() 在第 92 行用 Activator.CreateInstance()，需要替换为反射构造函数 + DI 解析
- `PluginLoadContext.cs`: 已有的 AssemblyLoadContext 隔离机制，不需要修改
- `LoadResult` record: 已有 Error 字段，可直接用于报告必需参数解析失败

### Established Patterns
- FullName 类型比较：PluginLoader 第 68 行已用 `i.FullName == "OpenAnima.Contracts.IModule"` 做 IModule 发现
- EventBus 属性注入：ModuleService 中现有的 setter 注入逻辑，外部模块未通过构造函数注入时需回退到此
- Result 对象模式：LoadResult record 捕获成功/失败，不抛异常

### Integration Points
- `ModuleService.cs`: 调用 PluginLoader.LoadModule() 的地方，需要传入 IServiceProvider
- `Program.cs`: DI 注册，可能需要调整 PluginLoader 相关注册
- `AnimaInitializationService.cs` / `OpenAnimaHostedService.cs`: 启动时加载模块的入口

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 38-pluginloader-di-injection*
*Context gathered: 2026-03-17*
