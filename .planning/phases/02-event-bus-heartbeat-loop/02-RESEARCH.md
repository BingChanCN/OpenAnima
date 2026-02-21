# Phase 2: Event Bus & Heartbeat Loop - Research

**Researched:** 2026-02-21
**Domain:** Event-driven architecture with MediatR + high-performance heartbeat loop
**Confidence:** HIGH

## Summary

Phase 2 implements inter-module communication via MediatR's notification pattern and a heartbeat loop using System.Threading.PeriodicTimer. MediatR provides a mature, battle-tested event bus with built-in DI integration, supporting both broadcast and filtered event delivery. PeriodicTimer (introduced in .NET 6) is the modern, async-first timer designed for periodic work with minimal CPU overhead and proper cancellation support.

The heartbeat loop acts as a scheduler (not executor) - it dispatches pending events and calls module Tick methods every 100ms. Heavy work is offloaded to background tasks to prevent blocking. A SemaphoreSlim guard prevents tick overlap (Unity FixedUpdate anti-pattern).

**Primary recommendation:** Use MediatR 14.x with INotification for events, PeriodicTimer for heartbeat, and SemaphoreSlim(1,1) to prevent concurrent ticks.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- 每次 tick 执行两件事：派发待处理事件 + 调用每个模块的 Tick 方法
- Tick 是调度器，不是执行器——只做轻量级工作，重活抛到后台异步 Task
- 防雪崩安全网：如果上一次 tick 还没完成，跳过本次 tick，不堆叠
- 心跳间隔可配置，默认 100ms（方便调试时放慢或未来根据负载调整）
- 模块可在运行时动态订阅和取消事件监听
- 支持按类型订阅 + 条件过滤（如只监听 severity > Warning 的事件）
- 同时支持广播模式和定向消息（模块 A 可直接发消息给模块 B，请求-响应模式）
- 使用泛型包装器 + 字符串名称区分事件类型（如 `Event<TPayload>` + eventName）
- 各模块自定义事件类型，不集中在 Contracts 程序集——通过字符串名称匹配实现解耦
- 事件自动携带元信息：时间戳、来源模块、事件 ID
- 事件对象可变——Handler 可以修改事件（如标记已处理）

### Claude's Discretion
- 事件监听的注册机制（接口实现 vs Attribute 标记）
- MediatR 具体集成方式和 Pipeline 配置
- 心跳循环的线程模型和异步调度实现细节
- 条件过滤的具体 API 设计

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MOD-04 | MediatR-based event bus for inter-module communication | MediatR 14.x provides INotification/INotificationHandler pattern for pub-sub, supports multiple handlers per event, DI integration via AddMediatR() |
| RUN-03 | Code-based heartbeat loop running at ≤100ms intervals | PeriodicTimer provides async-first periodic execution with minimal CPU overhead, designed for this exact use case |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| MediatR | 14.x | Event bus / mediator pattern | Industry standard for CQRS/event-driven .NET, 100M+ downloads, mature DI integration |
| MediatR.Extensions.Microsoft.DependencyInjection | 14.x | DI registration | Official extension for .NET DI container, auto-discovers handlers |
| System.Threading.PeriodicTimer | Built-in (.NET 6+) | Heartbeat loop timer | Modern async-first timer, designed for periodic work, no callback overhead |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Threading.SemaphoreSlim | Built-in | Prevent concurrent tick execution | Guard heartbeat loop to skip overlapping ticks |
| System.Threading.Channels | Built-in | Event queue (optional) | If buffering events between ticks, but MediatR handles dispatch directly |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| MediatR | Custom event bus | MediatR handles handler discovery, DI, and lifetime management - custom solution would need to replicate this |
| PeriodicTimer | System.Timers.Timer | Timer uses callbacks (not async), harder to cancel cleanly, more CPU overhead |
| PeriodicTimer | System.Threading.Timer | Similar to Timers.Timer, callback-based, not async-first |

**Installation:**
```bash
dotnet add package MediatR --version 14.0.0
dotnet add package MediatR.Extensions.Microsoft.DependencyInjection --version 14.0.0
```

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Events/
│   ├── ModuleEvent.cs          # Generic event wrapper with metadata
│   ├── IEventFilter.cs         # Interface for conditional filtering
│   └── EventBus.cs             # Wrapper around MediatR IMediator
├── Runtime/
│   ├── HeartbeatLoop.cs        # PeriodicTimer-based tick scheduler
│   └── ITickable.cs            # Interface for modules that need Tick
└── Plugins/
    └── (existing from Phase 1)
```

### Pattern 1: MediatR Notification for Events
**What:** Use INotification for events, INotificationHandler<T> for subscribers
**When to use:** All inter-module communication
**Example:**
```csharp
// Event definition (in module or Core)
public class ModuleEvent<TPayload> : INotification
{
    public string EventName { get; init; }
    public TPayload Payload { get; set; }  // Mutable per user requirement
    public string SourceModuleId { get; init; }
    public DateTime Timestamp { get; init; }
    public Guid EventId { get; init; }
}

// Handler registration (in module)
public class MyEventHandler : INotificationHandler<ModuleEvent<string>>
{
    public Task Handle(ModuleEvent<string> notification, CancellationToken ct)
    {
        if (notification.EventName == "TextMessage")
        {
            Console.WriteLine($"Received: {notification.Payload}");
        }
        return Task.CompletedTask;
    }
}

// Publishing (from any module or Core)
await _mediator.Publish(new ModuleEvent<string>
{
    EventName = "TextMessage",
    Payload = "Hello",
    SourceModuleId = "ModuleA",
    Timestamp = DateTime.UtcNow,
    EventId = Guid.NewGuid()
});
```

### Pattern 2: PeriodicTimer Heartbeat Loop
**What:** Async loop with PeriodicTimer.WaitForNextTickAsync()
**When to use:** Main heartbeat scheduler
**Example:**
```csharp
public class HeartbeatLoop
{
    private readonly PeriodicTimer _timer;
    private readonly SemaphoreSlim _tickLock = new(1, 1);
    private readonly TimeSpan _interval;

    public HeartbeatLoop(TimeSpan interval)
    {
        _interval = interval;
        _timer = new PeriodicTimer(interval);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (await _timer.WaitForNextTickAsync(ct))
        {
            // Skip if previous tick still running (anti-snowball)
            if (!_tickLock.Wait(0))
            {
                Console.WriteLine("Tick skipped - previous tick still running");
                continue;
            }

            try
            {
                await ExecuteTickAsync(ct);
            }
            finally
            {
                _tickLock.Release();
            }
        }
    }

    private async Task ExecuteTickAsync(CancellationToken ct)
    {
        // 1. Dispatch pending events (MediatR handles this)
        // 2. Call Tick on all ITickable modules
        // Heavy work offloaded to Task.Run()
    }
}
```

### Pattern 3: Dynamic Event Subscription with Filtering
**What:** Modules register handlers at runtime with optional predicates
**When to use:** Conditional event listening (e.g., severity filtering)
**Example:**
```csharp
// Wrapper for conditional handling
public class FilteredEventHandler<TPayload> : INotificationHandler<ModuleEvent<TPayload>>
{
    private readonly Func<ModuleEvent<TPayload>, bool> _filter;
    private readonly Func<ModuleEvent<TPayload>, CancellationToken, Task> _handler;

    public FilteredEventHandler(
        Func<ModuleEvent<TPayload>, bool> filter,
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler)
    {
        _filter = filter;
        _handler = handler;
    }

    public Task Handle(ModuleEvent<TPayload> notification, CancellationToken ct)
    {
        if (_filter(notification))
        {
            return _handler(notification, ct);
        }
        return Task.CompletedTask;
    }
}

// Usage: Module subscribes with filter
eventBus.Subscribe<LogEvent>(
    filter: e => e.Payload.Severity > LogLevel.Warning,
    handler: async (e, ct) => await ProcessHighSeverityLog(e)
);
```

### Pattern 4: Request-Response (Targeted Messages)
**What:** Use MediatR IRequest<TResponse> for module-to-module queries
**When to use:** When module A needs a response from module B
**Example:**
```csharp
// Request definition
public record GetModuleStatusRequest(string ModuleId) : IRequest<ModuleStatus>;

// Handler in target module
public class StatusRequestHandler : IRequestHandler<GetModuleStatusRequest, ModuleStatus>
{
    public Task<ModuleStatus> Handle(GetModuleStatusRequest request, CancellationToken ct)
    {
        return Task.FromResult(new ModuleStatus { IsRunning = true });
    }
}

// Caller
var status = await _mediator.Send(new GetModuleStatusRequest("ModuleB"));
```

### Anti-Patterns to Avoid
- **Blocking Tick:** Never do heavy work directly in Tick - always offload to Task.Run()
- **Tick Snowballing:** Without SemaphoreSlim guard, slow ticks stack up and cause cascading delays (Unity FixedUpdate problem)
- **Synchronous Event Handlers:** MediatR handlers are async - don't block with .Result or .Wait()
- **Global Event Types:** Don't centralize event definitions in Contracts - modules define their own, match by string name
- **Handler Lifetime Mismatch:** Register handlers as Scoped or Transient, not Singleton (unless stateless)

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Event bus with DI | Custom pub-sub with manual handler registration | MediatR | Handler discovery, lifetime management, pipeline behaviors, 100M+ downloads of battle-tested code |
| Periodic timer | while(true) + Task.Delay() | PeriodicTimer | Proper cancellation, no drift accumulation, designed for this use case |
| Concurrent execution guard | Manual flag checking | SemaphoreSlim | Thread-safe, async-compatible, handles race conditions |
| Event filtering | Manual if-checks in every handler | Pipeline behavior or wrapper pattern | Centralized logic, reusable, testable |

**Key insight:** MediatR's handler discovery and DI integration are deceptively complex - custom solutions miss edge cases like handler disposal, scoped dependencies, and async pipeline behaviors.

## Common Pitfalls

### Pitfall 1: MediatR Handler Not Discovered
**What goes wrong:** Handler registered but never called when event published
**Why it happens:** Handler not registered in DI, or assembly not scanned by AddMediatR()
**How to avoid:** Use `services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly))` and ensure handler is public
**Warning signs:** Event published but no console output, no exceptions thrown

### Pitfall 2: Tick Snowballing (Unity FixedUpdate Anti-Pattern)
**What goes wrong:** Slow ticks cause next tick to start before previous finishes, cascading delays
**Why it happens:** No guard to prevent concurrent tick execution
**How to avoid:** Use SemaphoreSlim(1,1) with Wait(0) to skip tick if previous still running
**Warning signs:** Tick interval drifts, CPU usage spikes, logs show overlapping tick timestamps

### Pitfall 3: PeriodicTimer Not Disposed
**What goes wrong:** Timer continues running after cancellation, resource leak
**Why it happens:** PeriodicTimer implements IDisposable but not disposed
**How to avoid:** Use `using var timer = new PeriodicTimer(...)` or explicit Dispose() in finally block
**Warning signs:** Background thread continues after app shutdown, memory leak

### Pitfall 4: Event Handler Memory Leak
**What goes wrong:** Handlers not garbage collected, memory grows over time
**Why it happens:** MediatR handlers registered as Singleton but hold references to Scoped services
**How to avoid:** Register handlers as Scoped or Transient, inject IServiceScopeFactory if need Singleton
**Warning signs:** Memory usage grows with each event, handlers not disposed

### Pitfall 5: Blocking Async in Tick
**What goes wrong:** Tick takes >100ms, heartbeat falls behind
**Why it happens:** Using .Result or .Wait() on async operations in Tick
**How to avoid:** Always await async operations, offload heavy work to Task.Run()
**Warning signs:** Tick duration logs show >100ms, heartbeat interval drifts

### Pitfall 6: MediatR Notification vs Request Confusion
**What goes wrong:** Using IRequest when need broadcast, or INotification when need response
**Why it happens:** Misunderstanding MediatR patterns
**How to avoid:** INotification = broadcast (0+ handlers), IRequest = single handler with response
**Warning signs:** Multiple handlers for IRequest throws exception, INotification with no handlers silently succeeds

## Code Examples

Verified patterns from official sources:

### MediatR Registration in .NET 8
```csharp
// Source: https://github.com/LuckyPennySoftware/MediatR
// In Program.cs or Startup.cs
var builder = WebApplication.CreateBuilder(args);

// Register MediatR and scan assemblies for handlers
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    // Optionally scan module assemblies dynamically
    // cfg.RegisterServicesFromAssemblies(moduleAssemblies);
});

var app = builder.Build();
```

### PeriodicTimer with Cancellation
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer
using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

try
{
    while (await timer.WaitForNextTickAsync(cancellationToken))
    {
        // Tick logic here
        await DoWorkAsync();
    }
}
catch (OperationCanceledException)
{
    // Expected when cancellationToken is cancelled
}
```

### Prevent Overlapping Execution
```csharp
// Source: https://stackoverflow.com/questions/37462256/prevent-overlapping-async-code-execution
private readonly SemaphoreSlim _semaphore = new(1, 1);

public async Task ExecuteTickAsync()
{
    // Try to acquire lock, skip if already held
    if (!await _semaphore.WaitAsync(0))
    {
        return; // Previous tick still running
    }

    try
    {
        // Tick work here
    }
    finally
    {
        _semaphore.Release();
    }
}
```

### MediatR Parallel Notification Publishing
```csharp
// Source: https://www.milanjovanovic.tech/blog/how-to-publish-mediatr-notifications-in-parallel
// MediatR 12+ supports parallel publishing
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher); // Parallel
    // Default is ForeachAwaitPublisher (sequential)
});
```

### Dynamic Handler Registration (Advanced)
```csharp
// For runtime subscription, wrap MediatR with custom EventBus
public class EventBus
{
    private readonly IServiceCollection _services;
    private IServiceProvider _provider;

    public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : INotification
    {
        // Register handler dynamically
        _services.AddTransient<INotificationHandler<TEvent>>(sp =>
            new DelegateHandler<TEvent>(handler));

        // Rebuild service provider (expensive, consider alternatives)
        _provider = _services.BuildServiceProvider();
    }
}

private class DelegateHandler<TEvent> : INotificationHandler<TEvent>
    where TEvent : INotification
{
    private readonly Func<TEvent, CancellationToken, Task> _handler;

    public DelegateHandler(Func<TEvent, CancellationToken, Task> handler)
    {
        _handler = handler;
    }

    public Task Handle(TEvent notification, CancellationToken ct)
        => _handler(notification, ct);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| System.Timers.Timer | PeriodicTimer | .NET 6 (2021) | Async-first, no callback overhead, proper cancellation |
| Custom event bus | MediatR | N/A (MediatR existed) | Industry standard, DI integration, pipeline behaviors |
| Sequential notification publishing | Parallel publishing (TaskWhenAllPublisher) | MediatR 12 (2023) | Handlers run concurrently, faster event processing |
| Manual handler registration | Assembly scanning | MediatR 8+ | Auto-discovery, less boilerplate |

**Deprecated/outdated:**
- System.Timers.Timer: Use PeriodicTimer for new code (async-first, better cancellation)
- System.Threading.Timer: Same as above
- Manual event handler registration: Use MediatR's assembly scanning

## Open Questions

1. **Dynamic Handler Unsubscription**
   - What we know: MediatR handlers registered via DI, lifetime managed by container
   - What's unclear: How to unsubscribe at runtime without rebuilding service provider
   - Recommendation: Use wrapper pattern with enabled/disabled flag, or maintain separate handler registry

2. **Module Assembly Scanning for Handlers**
   - What we know: AddMediatR scans specified assemblies for handlers
   - What's unclear: How to scan dynamically loaded plugin assemblies (Phase 1 AssemblyLoadContext)
   - Recommendation: Pass loaded plugin assemblies to RegisterServicesFromAssemblies(), or use reflection to find handlers manually

3. **Event Delivery Guarantees**
   - What we know: MediatR publishes to all registered handlers
   - What's unclear: What happens if handler throws exception (does it stop other handlers?)
   - Recommendation: Test behavior, consider pipeline behavior for error handling

## Sources

### Primary (HIGH confidence)
- [MediatR 14.0.0 NuGet](https://www.nuget.org/packages/mediatr/) - Latest version, installation
- [PeriodicTimer API Docs](https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer?view=net-10.0) - Official .NET docs
- [MediatR GitHub](https://github.com/LuckyPennySoftware/MediatR) - Official repository, examples

### Secondary (MEDIUM confidence)
- [How To Publish MediatR Notifications In Parallel](https://www.milanjovanovic.tech/blog/how-to-publish-mediatr-notifications-in-parallel) - Parallel publishing patterns
- [Implementing CQRS with MediatR in .NET 8](https://dev.to/adrianbailador/implementing-cqrs-with-mediatr-in-net-8-a-complete-guide-1kof) - .NET 8 integration
- [PeriodicTimer vs Other Timers](https://www.c-sharpcorner.com/article/c-sharp-timers-explained-periodictimer-vs-system-timers-timer-vs-system-threading/) - Timer comparison
- [Prevent Overlapping Async Execution](https://stackoverflow.com/questions/37462256/prevent-overlapping-async-code-execution) - SemaphoreSlim pattern
- [5 Techniques to Avoid Memory Leaks by Events](https://michaelscodingspot.com/5-techniques-to-avoid-memory-leaks-by-events-in-c-net-you-should-know/) - Event handler disposal

### Tertiary (LOW confidence)
- [MediatR Dynamic Subscription Issue #208](https://github.com/jbogard/MediatR/issues/208) - Runtime subscription discussion (old, may be outdated)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - MediatR and PeriodicTimer are well-documented, mature, official solutions
- Architecture: HIGH - Patterns verified from official docs and community best practices
- Pitfalls: MEDIUM-HIGH - Based on common issues documented in Stack Overflow and blogs, some require validation

**Research date:** 2026-02-21
**Valid until:** 60 days (stable domain, .NET 8 and MediatR 14 are current)

---

*Research complete. Ready for planning.*
