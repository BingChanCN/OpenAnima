# Phase 29: Routing Modules - Research

**Researched:** 2026-03-12
**Domain:** C# module implementation, EventBus metadata passthrough, Blazor UI configuration
**Confidence:** HIGH

## Summary

Phase 29 implements three routing modules (AnimaInputPort, AnimaOutputPort, AnimaRoute) that enable end-to-end cross-Anima request-response without LLM involvement. The architecture uses implicit metadata passthrough via a new `Metadata` dictionary on `ModuleEvent<T>` to carry correlation IDs transparently through the wiring graph. CrossAnimaRouter actively pushes requests to target Anima EventBuses, and AnimaRoute MUST await responses synchronously to prevent fire-and-forget execution bugs.

**Primary recommendation:** Extend ModuleEvent<T> with nullable Metadata dictionary, modify DataCopyHelper to preserve metadata during fan-out, and implement three modules following established FixedTextModule patterns. AnimaRoute's ExecuteAsync MUST await RouteRequestAsync — no fire-and-forget.


<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Correlation ID 传递方式**
- 采用隐式元数据通道：ModuleEvent<T> 新增 `Dictionary<string, string>? Metadata` 属性，默认 null
- AnimaInputPort 输出事件时在 Metadata 中附带 `correlationId`
- WiringEngine 的 DataCopyHelper 转发事件时全量复制 Metadata 到下游事件
- 中间模块（LLM、FixedText 等）无需感知 correlationId，只处理 Payload
- AnimaOutputPort 从接收事件的 Metadata["correlationId"] 取出 ID，调用 `router.CompleteRequest()`

**请求投递方式**
- CrossAnimaRouter 主动推送：RouteRequestAsync 内部通过 AnimaRuntimeManager 获取目标 Anima 的 EventBus 实例，直接发布事件触发 AnimaInputPort
- CrossAnimaRouter 需要新增对 IAnimaRuntimeManager 的依赖

**AnimaInputPort 模块**
- 单输出端口设计：只有一个 "request" (Text) 输出端口，输出收到的请求 payload
- correlationId 通过 Metadata 隐式传递，不暴露为端口
- 侧边栏配置项：服务名称（必填）、服务描述（必填）、输入格式提示（可选，如 "JSON" 或 "纯文本"）
- InitializeAsync 时向 CrossAnimaRouter 注册端口（名称 + 描述）
- ShutdownAsync 时注销端口

**AnimaOutputPort 模块**
- 单输入端口 "response" (Text)，接收响应数据
- 侧边栏配置：下拉菜单列出当前 Anima 已注册的 InputPort 名称，用户选择匹配的服务
- 从接收事件的 Metadata 中提取 correlationId，调用 router.CompleteRequest()

**AnimaRoute 模块**
- 2 输入 + 2 输出端口设计：
  - 输入：request (Text) + trigger (Trigger)
  - 输出：response (Text) + error (Text)
- 收到 trigger 信号时，将 request 端口的内容发送到目标 Anima
- 侧边栏配置：级联下拉菜单 — 第一个选目标 Anima，第二个自动加载该 Anima 的已注册 InputPort 列表
- 侧边栏只显示配置表单，不显示运行时状态

**错误输出行为**
- error 端口输出 JSON 结构化内容，如 `{"error":"Timeout","target":"animaB::summarize","timeout":30}`
- response 和 error 互斥输出：成功时只触发 response，失败时只触发 error
- 错误类型对应 CrossAnimaRouter 的 RouteErrorKind：Timeout、NotFound、Cancelled、Failed

### Claude's Discretion

- ModuleEvent Metadata 字段的具体实现细节（属性命名、null 处理）
- DataCopyHelper 中 Metadata 复制的具体实现
- 三个模块的 DI 注册方式��初始化顺序
- 级联下拉菜单的 UI 刷新策略（实时 vs 手动刷新）
- AnimaRoute 内部 request 数据的暂存机制（收到 request 后等待 trigger）

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| RMOD-01 | User can add AnimaInputPort module to declare a named service on an Anima | Module implementation pattern from FixedTextModule; port declaration via attributes |
| RMOD-02 | AnimaInputPort registers with CrossAnimaRouter on initialization with service name and description | CrossAnimaRouter.RegisterPort() API already exists; call in InitializeAsync |
| RMOD-03 | User can add AnimaOutputPort module paired by name with AnimaInputPort for response return | Dropdown config pattern; CrossAnimaRouter.GetPortsForAnima() for port list |
| RMOD-04 | AnimaOutputPort completes cross-Anima request via correlation ID through CrossAnimaRouter | CrossAnimaRouter.CompleteRequest() API exists; extract correlationId from Metadata |
| RMOD-05 | User can add AnimaRoute module and select target Anima via dropdown | Blazor dropdown pattern; AnimaRuntimeManager.GetAll() for Anima list |
| RMOD-06 | User can select target remote input port via second dropdown (populated from selected Anima's registered ports) | Cascading dropdown pattern; CrossAnimaRouter.GetPortsForAnima() |
| RMOD-07 | AnimaRoute sends request and awaits response synchronously within wiring tick | RouteRequestAsync returns Task<RouteResult>; MUST await to prevent fire-and-forget |
| RMOD-08 | AnimaRoute exposes error/timeout output port for routing failure handling in wiring | RouteResult.IsSuccess + RouteResult.Error enum; publish to error port on failure |

</phase_requirements>

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| xUnit | 2.9.3 | Unit testing framework | Already in use; native async support, Theory/Fact attributes |
| Microsoft.Extensions.DependencyInjection | 10.0.3 | DI container | ASP.NET Core standard; singleton/scoped registration |
| System.Text.Json | Built-in | JSON serialization | Used in DataCopyHelper; metadata serialization for fan-out |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.Extensions.Logging.Abstractions | 10.0.3 | Logging interfaces | All modules take ILogger<T> for diagnostics |
| NullLogger | Built-in | Test logging | Unit tests use NullLogger<T>.Instance to suppress output |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Metadata dictionary | Dedicated Trigger wire | Trigger wire requires explicit wiring; metadata is transparent but less discoverable |
| JSON error output | Separate error ports per type | Multiple ports increase wiring complexity; JSON is flexible |
| Active push delivery | Pull-based polling | Polling adds latency; active push is immediate but requires AnimaRuntimeManager dependency |

**Installation:**

No new packages required — all dependencies already in project.

## Architecture Patterns

### Recommended Project Structure

```
src/OpenAnima.Core/Modules/
├── AnimaInputPortModule.cs      # Declares service, registers with router
├── AnimaOutputPortModule.cs     # Completes request via correlationId
└── AnimaRouteModule.cs          # Sends request, awaits response

src/OpenAnima.Contracts/
└── ModuleEvent.cs               # Add Metadata property

src/OpenAnima.Core/Wiring/
└── DataCopyHelper.cs            # Extend to copy Metadata

src/OpenAnima.Core/Routing/
└── CrossAnimaRouter.cs          # Add IAnimaRuntimeManager dependency

tests/OpenAnima.Tests/Modules/
└── RoutingModulesTests.cs       # Unit tests for 3 modules

tests/OpenAnima.Tests/Integration/
└── CrossAnimaRoutingE2ETests.cs # End-to-end routing test
```

### Pattern 1: Module with EventBus Subscription

**What:** Module subscribes to input port events in InitializeAsync, publishes to output ports in handler
**When to use:** All modules with input ports (AnimaOutputPort, AnimaRoute)
**Example:**

```csharp
// Source: FixedTextModule.cs (existing pattern)
public class AnimaOutputPortModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly ICrossAnimaRouter _router;
    private readonly List<IDisposable> _subscriptions = new();

    public Task InitializeAsync(CancellationToken ct = default)
    {
        var sub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.response",
            async (evt, ct) => await HandleResponseAsync(evt, ct));
        _subscriptions.Add(sub);
        return Task.CompletedTask;
    }

    private async Task HandleResponseAsync(ModuleEvent<string> evt, CancellationToken ct)
    {
        // Extract correlationId from Metadata
        if (evt.Metadata?.TryGetValue("correlationId", out var correlationId) == true)
        {
            _router.CompleteRequest(correlationId, evt.Payload);
        }
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }
}
```

### Pattern 2: Metadata Passthrough in DataCopyHelper

**What:** Deep copy Metadata dictionary during fan-out to preserve correlation context
**When to use:** WiringEngine data routing (ForwardPayloadAsync)
**Example:**

```csharp
// Source: DataCopyHelper.cs (to be extended)
public static T DeepCopy<T>(T obj)
{
    if (obj == null) return default!;
    if (obj is string str) return (T)(object)str;

    // JSON round-trip preserves Metadata dictionary
    var json = JsonSerializer.Serialize(obj);
    return JsonSerializer.Deserialize<T>(json)!;
}
```

### Pattern 3: Awaiting Async Routing in Module Execution

**What:** AnimaRoute MUST await RouteRequestAsync to prevent fire-and-forget
**When to use:** AnimaRoute trigger handler
**Example:**

```csharp
// CRITICAL: MUST await — fire-and-forget causes downstream to execute with empty data
private async Task HandleTriggerAsync(ModuleEvent<DateTime> evt, CancellationToken ct)
{
    var result = await _router.RouteRequestAsync(
        _targetAnimaId,
        _targetPortName,
        _requestPayload,
        timeout: TimeSpan.FromSeconds(30),
        ct);

    if (result.IsSuccess)
    {
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.response",
            Payload = result.Payload!
        }, ct);
    }
    else
    {
        var errorJson = JsonSerializer.Serialize(new
        {
            error = result.Error.ToString(),
            target = $"{_targetAnimaId}::{_targetPortName}",
            timeout = 30
        });
        await _eventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = $"{Metadata.Name}.port.error",
            Payload = errorJson
        }, ct);
    }
}
```

### Pattern 4: Cascading Dropdown in Blazor

**What:** Second dropdown populates based on first dropdown selection
**When to use:** AnimaRoute config sidebar (Anima selection → Port selection)
**Example:**

```csharp
// Source: EditorConfigSidebar.razor pattern (to be extended)
<select @onchange="HandleAnimaChanged">
    @foreach (var anima in _allAnimas)
    {
        <option value="@anima.Id">@anima.Name</option>
    }
</select>

<select @onchange="HandlePortChanged">
    @foreach (var port in _availablePorts)
    {
        <option value="@port.PortName">@port.PortName - @port.Description</option>
    }
</select>

@code {
    private List<PortRegistration> _availablePorts = new();

    private void HandleAnimaChanged(ChangeEventArgs e)
    {
        var selectedAnimaId = e.Value?.ToString();
        if (selectedAnimaId != null)
        {
            _availablePorts = _router.GetPortsForAnima(selectedAnimaId).ToList();
            StateHasChanged();
        }
    }
}
```

### Anti-Patterns to Avoid

- **Fire-and-forget routing:** AnimaRoute MUST await RouteRequestAsync. Fire-and-forget causes downstream modules to execute in the same tick with empty data, breaking topological execution.
- **Metadata mutation:** Metadata should be set once at event creation. Mutating Metadata in handlers breaks isolation.
- **Synchronous CompleteRequest in async handler:** CompleteRequest is synchronous but safe to call from async context (no blocking I/O).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Correlation ID propagation | Manual ID passing through module configs | Metadata dictionary on ModuleEvent | Transparent to intermediate modules; no config pollution |
| Dropdown state management | Custom JavaScript interop | Blazor @onchange + StateHasChanged | Native Blazor pattern; no JS complexity |
| Event deep copy | Manual field-by-field copy | System.Text.Json round-trip | Handles nested objects, preserves Metadata |
| Async timeout handling | Manual CancellationTokenSource + Task.Delay | CrossAnimaRouter built-in timeout | Already implemented with cleanup loop |

**Key insight:** CrossAnimaRouter already handles timeout, cleanup, and correlation — modules only need to call RegisterPort/RouteRequestAsync/CompleteRequest. Don't reimplement request-response correlation.

## Common Pitfalls

### Pitfall 1: Fire-and-Forget Routing

**What goes wrong:** AnimaRoute calls RouteRequestAsync without await, causing downstream modules to execute immediately with empty response data in the same wiring tick.

**Why it happens:** Developer forgets that WiringEngine executes modules level-by-level synchronously. Fire-and-forget breaks topological order.

**How to avoid:** ALWAYS await RouteRequestAsync in AnimaRoute's trigger handler. Add integration test verifying response arrives before downstream execution.

**Warning signs:** Downstream modules receive empty/null data; routing appears to "not work" despite no errors.

### Pitfall 2: Metadata Not Copied in Fan-Out

**What goes wrong:** WiringEngine's DataCopyHelper creates deep copy of Payload but loses Metadata, breaking correlation ID passthrough.

**Why it happens:** DataCopyHelper uses JSON round-trip, which only serializes public properties. If Metadata is not included in serialization, it's lost.

**How to avoid:** Ensure ModuleEvent<T> Metadata property is public and serializable. Verify DataCopyHelper test covers Metadata preservation.

**Warning signs:** AnimaOutputPort logs "correlationId not found in Metadata"; requests timeout instead of completing.

### Pitfall 3: Null Metadata Access Without Check

**What goes wrong:** AnimaOutputPort accesses evt.Metadata["correlationId"] without null check, causing NullReferenceException when Metadata is null.

**Why it happens:** Metadata is nullable (default null for non-routing events). Direct dictionary access throws on null.

**How to avoid:** Use null-conditional operator: `evt.Metadata?.TryGetValue("correlationId", out var id)`.

**Warning signs:** NullReferenceException in AnimaOutputPort handler; module enters Error state.

### Pitfall 4: CrossAnimaRouter Circular Dependency

**What goes wrong:** CrossAnimaRouter takes IAnimaRuntimeManager, which takes ICrossAnimaRouter — circular dependency breaks DI registration.

**Why it happens:** Both services are singletons and reference each other.

**How to avoid:** Register CrossAnimaRouter BEFORE AnimaRuntimeManager in AnimaServiceExtensions (already done in Phase 28). CrossAnimaRouter constructor takes IAnimaRuntimeManager via DI, not constructor injection.

**Warning signs:** DI container throws "circular dependency detected" on startup.

### Pitfall 5: Dropdown Not Refreshing After Anima Selection

**What goes wrong:** User selects Anima in first dropdown, but second dropdown (port list) doesn't update.

**Why it happens:** Blazor doesn't auto-refresh UI after state change. Must call StateHasChanged().

**How to avoid:** Call StateHasChanged() after updating _availablePorts in HandleAnimaChanged.

**Warning signs:** Second dropdown shows stale data or remains empty.

## Code Examples

Verified patterns from existing codebase:

### Module Port Declaration

```csharp
// Source: FixedTextModule.cs
[OutputPort("output", PortType.Text)]
public class FixedTextModule : IModuleExecutor
{
    // ...
}

// For AnimaInputPort:
[OutputPort("request", PortType.Text)]
public class AnimaInputPortModule : IModuleExecutor { }

// For AnimaOutputPort:
[InputPort("response", PortType.Text)]
public class AnimaOutputPortModule : IModuleExecutor { }

// For AnimaRoute:
[InputPort("request", PortType.Text)]
[InputPort("trigger", PortType.Trigger)]
[OutputPort("response", PortType.Text)]
[OutputPort("error", PortType.Text)]
public class AnimaRouteModule : IModuleExecutor { }
```

### Module Config Access

```csharp
// Source: FixedTextModule.cs
var animaId = _animaContext.ActiveAnimaId;
var config = _configService.GetConfig(animaId, Metadata.Name);
var serviceName = config.TryGetValue("serviceName", out var name) ? name : string.Empty;
```

### EventBus Publish with Metadata

```csharp
// New pattern for AnimaInputPort
await _eventBus.PublishAsync(new ModuleEvent<string>
{
    EventName = $"{Metadata.Name}.port.request",
    SourceModuleId = Metadata.Name,
    Payload = requestPayload,
    Metadata = new Dictionary<string, string>
    {
        ["correlationId"] = correlationId
    }
}, ct);
```

### CrossAnimaRouter Integration

```csharp
// Source: CrossAnimaRouter.cs + AnimaRuntimeManager.cs
// In AnimaInputPort.InitializeAsync:
var result = _router.RegisterPort(animaId, serviceName, description);
if (!result.IsSuccess)
{
    _logger.LogError("Port registration failed: {Error}", result.Error);
}

// In AnimaInputPort.ShutdownAsync:
_router.UnregisterPort(animaId, serviceName);

// In AnimaRoute trigger handler:
var result = await _router.RouteRequestAsync(
    targetAnimaId,
    portName,
    payload,
    timeout: TimeSpan.FromSeconds(30),
    ct);
```

### xUnit Async Test Pattern

```csharp
// Source: ModuleTests.cs
[Fact]
public async Task AnimaRoute_AwaitResponse_DeliversToDownstream()
{
    // Arrange
    var eventBus = new EventBus(NullLogger<EventBus>.Instance);
    var router = new CrossAnimaRouter(NullLogger<CrossAnimaRouter>.Instance);
    var module = new AnimaRouteModule(eventBus, router, /* ... */);
    await module.InitializeAsync();

    var responseTcs = new TaskCompletionSource<string>();
    eventBus.Subscribe<string>(
        "AnimaRouteModule.port.response",
        (evt, ct) => { responseTcs.TrySetResult(evt.Payload); return Task.CompletedTask; });

    // Act
    await eventBus.PublishAsync(new ModuleEvent<DateTime>
    {
        EventName = "AnimaRouteModule.port.trigger",
        Payload = DateTime.UtcNow
    });

    // Assert
    var response = await WaitWithTimeout(responseTcs.Task, TimeSpan.FromSeconds(5));
    Assert.NotNull(response);
}

private static async Task<T> WaitWithTimeout<T>(Task<T> task, TimeSpan timeout)
{
    using var cts = new CancellationTokenSource(timeout);
    var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
    if (completedTask == task)
    {
        cts.Cancel();
        return await task;
    }
    throw new TimeoutException($"Task did not complete within {timeout.TotalSeconds}s");
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Dedicated Trigger wire for correlation | Metadata dictionary | Phase 29 (2026-03-12) | Transparent passthrough; no explicit wiring |
| Pull-based port discovery | Active push delivery | Phase 29 (2026-03-12) | Immediate delivery; requires AnimaRuntimeManager dependency |
| Text prefix for routing markers | Deferred to Phase 30 | Phase 29 scope | Phase 29 is pure module wiring, no LLM markers |

**Deprecated/outdated:**

- Manual correlation ID wiring through module configs: Replaced by Metadata dictionary (transparent to intermediate modules)

## Open Questions

1. **Metadata serialization in DataCopyHelper**
   - What we know: DataCopyHelper uses System.Text.Json round-trip for deep copy
   - What's unclear: Whether Metadata dictionary serializes correctly by default, or needs JsonPropertyName attribute
   - Recommendation: Add unit test for DataCopyHelper.DeepCopy with Metadata; verify dictionary survives round-trip

2. **AnimaRoute request buffering strategy**
   - What we know: AnimaRoute has separate request and trigger input ports
   - What's unclear: How to buffer request data when it arrives before trigger (or vice versa)
   - Recommendation: Use private field `_lastRequestPayload` to store most recent request; trigger handler uses buffered value

3. **Dropdown refresh timing**
   - What we know: Blazor requires StateHasChanged() for UI refresh
   - What's unclear: Whether to refresh port list on every Anima selection, or cache and refresh on sidebar open
   - Recommendation: Refresh on Anima selection change (simpler, always up-to-date)

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| Quick run command | `dotnet test --filter "Category=Routing" --no-build` |
| Full suite command | `dotnet test --no-build` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| RMOD-01 | AnimaInputPort declares service and registers with router | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaInputPort_InitializeAsync_RegistersPort" --no-build` | ❌ Wave 0 |
| RMOD-02 | AnimaInputPort registration includes name and description | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaInputPort_RegisterPort_IncludesDescription" --no-build` | ❌ Wave 0 |
| RMOD-03 | AnimaOutputPort config dropdown lists registered ports | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaOutputPort_ConfigDropdown_ListsPorts" --no-build` | ❌ Wave 0 |
| RMOD-04 | AnimaOutputPort extracts correlationId from Metadata and completes request | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaOutputPort_CompleteRequest_UsesMetadata" --no-build` | ❌ Wave 0 |
| RMOD-05 | AnimaRoute config dropdown lists all Animas | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaRoute_ConfigDropdown_ListsAnimas" --no-build` | ❌ Wave 0 |
| RMOD-06 | AnimaRoute second dropdown populates from selected Anima's ports | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaRoute_CascadingDropdown_PopulatesPorts" --no-build` | ❌ Wave 0 |
| RMOD-07 | AnimaRoute awaits response synchronously within wiring tick | integration | `dotnet test --filter "FullyQualifiedName~CrossAnimaRoutingE2ETests.AnimaRoute_AwaitResponse_CompletesBeforeDownstream" --no-build` | ❌ Wave 0 |
| RMOD-08 | AnimaRoute error port outputs JSON on routing failure | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaRoute_OnTimeout_OutputsErrorJson" --no-build` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "Category=Routing" --no-build` (routing-specific tests only, < 30s)
- **Per wave merge:** `dotnet test --no-build` (full suite)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `tests/OpenAnima.Tests/Modules/RoutingModulesTests.cs` — covers RMOD-01 through RMOD-06, RMOD-08 (unit tests for 3 modules)
- [ ] `tests/OpenAnima.Tests/Integration/CrossAnimaRoutingE2ETests.cs` — covers RMOD-07 (end-to-end routing test)
- [ ] Framework already installed (xUnit 2.9.3) — no additional packages needed

## Sources

### Primary (HIGH confidence)

- CrossAnimaRouter.cs (src/OpenAnima.Core/Routing/) — RegisterPort, RouteRequestAsync, CompleteRequest APIs
- FixedTextModule.cs (src/OpenAnima.Core/Modules/) — Module implementation pattern
- ModuleTests.cs (tests/OpenAnima.Tests/Modules/) — xUnit async test pattern
- CrossAnimaRouterTests.cs (tests/OpenAnima.Tests/Unit/) — Router unit test pattern
- EditorConfigSidebar.razor (src/OpenAnima.Core/Components/Shared/) — Blazor config sidebar pattern
- WiringEngine.cs (src/OpenAnima.Core/Wiring/) — DataCopyHelper usage in ForwardPayloadAsync
- AnimaRuntimeManager.cs (src/OpenAnima.Core/Anima/) — GetOrCreateRuntime for EventBus access

### Secondary (MEDIUM confidence)

- [ASP.NET Core Blazor cascading values and parameters](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/cascading-values-and-parameters?view=aspnetcore-10.0) — Blazor dropdown pattern (official docs)

### Tertiary (LOW confidence)

None — all findings verified against existing codebase

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All dependencies already in project; xUnit 2.9.3 confirmed in .csproj
- Architecture: HIGH - Patterns verified in FixedTextModule, CrossAnimaRouter, WiringEngine
- Pitfalls: HIGH - Fire-and-forget risk documented in roadmap; metadata passthrough verified in DataCopyHelper

**Research date:** 2026-03-12
**Valid until:** 2026-04-12 (30 days — stable C# patterns, no fast-moving dependencies)
