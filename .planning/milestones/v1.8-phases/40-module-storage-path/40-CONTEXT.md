# Phase 40: Module Storage Path - Context

**Gathered:** 2026-03-18
**Status:** Ready for planning

<domain>
## Phase Boundary

Modules can persist data to a stable per-Anima per-Module directory via a new IModuleStorage interface. Additionally provides a global (non-Anima-scoped) data directory for modules that need cross-Anima shared storage. Existing IModuleContext interface is NOT modified — storage is a separate concern.

</domain>

<decisions>
## Implementation Decisions

### API 签名设计
- 新建 IModuleStorage 接口（不修改 IModuleContext）
- GetDataDirectory() 无参重载：实现层通过构造时绑定的模块信息自动推断 moduleId
- GetDataDirectory(string moduleId) 有参重载：显式传入 moduleId
- GetGlobalDataDirectory(string moduleId) 全局路径方法：返回不含 animaId 的共享目录
- 返回类型为 string（路径字符串），不是 DirectoryInfo
- 调用时自动创建目录（Directory.CreateDirectory）— STOR-01 要求 auto-created on first call
- 不提供额外存储辅助方法（ReadFile/WriteFile 等）— 模块自己用 File API 操作

### 路径结构与约定
- per-Anima 路径：data/animas/{animaId}/module-data/{moduleId}/（与现有 module-configs 平级）
- 全局路径：data/module-data/{moduleId}/（data/ 根下，不属于任何 Anima）
- 删除 Anima 时 per-Anima module-data 随 Anima 目录自然清理（STOR-01 success criteria #4）
- moduleId 做路径安全检查：拒绝包含 .. / \ 等路径穿越字符的 moduleId，抛出 ArgumentException

### Anima 切换语义
- GetDataDirectory 每次调用动态读取当前 ActiveAnimaId，返回对应路径
- 切换 Anima 后下次调用自然返回新 Anima 的路径
- 不新增 DataDirectoryChanged 事件 — 现有 ActiveAnimaChanged 事件已足够模块感知切换
- 模块不应缓存路径，每次需要时调用 GetDataDirectory

### 接口变更策略
- 新建 IModuleStorage 接口，放在 OpenAnima.Contracts 根命名空间
- IModuleContext 不变 — 避免 breaking change
- 内置模块也注入 IModuleStorage（保持一致性，即使当前无使用场景）
- IModuleStorage 在 PluginLoader DI 中视为可选：解析失败传 null + warning log（与 Phase 38 Contracts 服务可选策略一致）
- PluginLoader 的 FullName 匹配列表需新增 IModuleStorage

### Claude's Discretion
- ModuleStorage 实现类内部细节（路径拼接、缓存策略）
- 无参 GetDataDirectory() 如何获取当前模块 ID（构造函数绑定 or 其他机制）
- 单元测试策略和 mock 方式
- 路径安全检查的具体正则/字符集

</decisions>

<specifics>
## Specific Ideas

- 全局存储场景：记忆模块插入多个 Anima 需要共享同一份记忆文件，per-Anima 路径不够用
- 路径风格与现有 AnimaModuleConfigService 的 data/animas/{id}/module-configs/ 保持一致

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `AnimaModuleConfigService.cs`: 已建立 data/animas/{id}/module-configs/ 路径约定，GetConfigPath 方法可参考
- `AnimaContext.cs`: 实现 IModuleContext，持有 ActiveAnimaId，GetDataDirectory 需要读取此值
- `IAnimaContext.cs`: 继承 IModuleContext 并加 SetActive 方法，标记为 Obsolete

### Established Patterns
- FullName 类型匹配：PluginLoader 用 FullName 字符串比较做跨 AssemblyLoadContext 类型发现
- Contracts 服务可选注入：Phase 38 决定 Contracts 服务解析失败传 null + warning
- 路径约定：data/animas/{animaId}/ 下按功能分子目录（module-configs、wiring 等）

### Integration Points
- `PluginLoader.cs`: DI 解析列表需新增 IModuleStorage 的 FullName 匹配
- `AnimaServiceExtensions.cs`: DI 注册 IModuleStorage 实现
- `AnimaContext.cs` / `IAnimaContext.cs`: ModuleStorage 实现需要访问 ActiveAnimaId
- 内置模块构造函数：可选注入 IModuleStorage 参数

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 40-module-storage-path*
*Context gathered: 2026-03-18*
