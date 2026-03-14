# Technology Stack

**Project:** OpenAnima v1.7 Runtime Foundation
**Researched:** 2026-03-14
**Confidence:** HIGH

## Executive Summary

For v1.7's three target areas — Activity Channel concurrency model, Contracts API thickening, and built-in module decoupling — **no new NuGet packages are required**. Every needed primitive (`Channel<T>`, `SemaphoreSlim`, `IServiceProvider`, `CancellationToken`) ships with the .NET 8 BCL. The work is entirely architectural: introducing the right concurrency primitives at the right layer, moving interfaces from `Core` to `Contracts`, and severing the `using OpenAnima.Core.*` dependency chain in the 14 built-in modules.

**Zero-new-dependency principle holds for v1.7.** No NuGet additions. No new framework. No actor model library.

---

## Baseline: Validated v1.6 Stack

Already shipped and unchanged:

| Package | Version | Status |
|---------|---------|--------|
| .NET 8.0 | runtime | unchanged |
| Blazor Server + SignalR | 8.0.x | unchanged |
| OpenAI SDK | 2.8.0 | unchanged |
| SharpToken | 2.0.4 | unchanged |
| Markdig + Markdown.ColorCode | 0.41.3 / 3.0.1 | unchanged |
| System.CommandLine | 2.0.0-beta4 | unchanged |
| Microsoft.Extensions.Http.Resilience | 8.7.0 | unchanged |

Existing architecture that v1.7 builds on:
- Per-Anima `AnimaRuntime` container (isolated `EventBus` + `WiringEngine` + `HeartbeatLoop`)
- Lock-free `EventBus` (`ConcurrentDictionary` + `ConcurrentBag`)
- `HeartbeatLoop` with `PeriodicTimer` + `SemaphoreSlim(1,1)` skip-tick guard
- `WiringEngine` with Kahn's algorithm topological sort + level-parallel `Task.WhenAll`
- `ICrossAnimaRouter` singleton with `ConcurrentDictionary<Guid, TaskCompletionSource<string>>`

---

## New Stack Elements for v1.7

### Concurrency Primitives (BCL — No NuGet)

| Primitive | Namespace | Purpose | Why This One |
|-----------|-----------|---------|--------------|
| `Channel<T>` | `System.Threading.Channels` | Activity Channel per-channel serial queue — one `Channel<ChannelItem>` per named activity channel on a stateful Anima | Built into .NET 8 BCL (no package needed). `Channel.CreateUnbounded<T>()` with a single consumer task gives guaranteed FIFO serial execution within a channel while multiple channels run concurrently across `Task.WhenAll`. This is the idiomatic .NET pattern for serialized async processing without blocking threads. |
| `SemaphoreSlim` | `System.Threading` | Per-request execution gate for stateless Animas — allow N concurrent executions, block excess | Already used in `HeartbeatLoop` for the skip-tick guard. `SemaphoreSlim(maxConcurrency, maxConcurrency)` + `WaitAsync()` in `finally` is the BCL idiom for async throttling. No thread blocking — waiters return to thread pool. |
| `CancellationTokenSource` | `System.Threading` | Lifecycle token for each channel consumer task | Already used throughout the codebase. Each `Channel<T>` consumer loop needs a `CancellationToken` tied to the `AnimaRuntime` disposal path. `CreateLinkedTokenSource` from the existing HeartbeatLoop token is the correct composition. |
| `TaskCreationOptions.RunContinuationsAsynchronously` | `System.Threading.Tasks` | Required flag on `TaskCompletionSource` when bridging channel completions back to callers | Already used in `CrossAnimaRouter` for the same reason: prevents deadlocks when TCS completions run in-line on the thread that called `TrySetResult`. |
| `Interlocked` | `System.Threading` | Lock-free counters for channel queue depth metrics (monitoring) | Already used in `HeartbeatLoop` for `_tickCount`. Use `Interlocked.Increment`/`Read` for per-channel metrics without locking. |

### Contracts API Additions (No NuGet — Interface Design)

Interfaces to move from `OpenAnima.Core` to `OpenAnima.Contracts`. These are additions to the `Contracts` project only — no new packages:

| Interface | Move From | Move To | What Modules Need It For |
|-----------|-----------|---------|--------------------------|
| `IModuleConfigProvider` | new (extracted from `IAnimaModuleConfigService`) | `OpenAnima.Contracts` | Modules reading their own config dict by `(animaId, moduleId)` — `LLMModule`, `AnimaRouteModule`, `ConditionalBranchModule`, `FixedTextModule` all do this today via `IAnimaModuleConfigService` which lives in `Core.Services` |
| `IAnimaContext` | `OpenAnima.Core.Anima` | `OpenAnima.Contracts` | Modules needing `ActiveAnimaId` (current scope) — `LLMModule`, `AnimaRouteModule`, `AnimaInputPortModule`, `AnimaOutputPortModule` all inject this today |
| `ICrossAnimaRouter` (read-only subset) | `OpenAnima.Core.Routing` | `OpenAnima.Contracts` | `AnimaRouteModule` and `AnimaInputPortModule` need `RouteRequestAsync` and `GetPortsForAnima` — currently they reference `OpenAnima.Core.Routing.ICrossAnimaRouter` which is a Core type |

**Design rule:** The `Contracts` versions are thin interfaces containing only the surface external modules actually call. Implementation details (persistence path, registry internals) stay in `Core`.

---

## Architecture of the Activity Channel Model

This is an in-process concurrency architecture, not a new technology. The stack primitives map to these roles:

### Stateful Anima: Activity Channel Model

```
HeartbeatLoop tick
  └─ for each Activity Channel (name → Channel<ChannelItem>)
       └─ Channel<ChannelItem>.Writer.TryWrite(item)   ← non-blocking, fire-and-forget into queue

Per-channel consumer Task (started at AnimaRuntime.StartAsync)
  └─ await foreach (var item in channel.Reader.ReadAllAsync(ct))
       └─ await WiringEngine.ExecuteAsync(item, ct)    ← serial within channel, bounded by reader
```

**Multiple channels run concurrently** (one Task per channel). **Within each channel, execution is serial** (single consumer reader). This gives "parallel channels, serial within channel" semantics with zero locks.

### Stateless Anima: Request-Level Isolation

```
Incoming request (chat, heartbeat, external trigger)
  └─ await semaphore.WaitAsync(ct)                     ← gate: max N concurrent executions
       └─ var snapshot = CreateExecutionSnapshot()     ← copy of current state
       └─ await WiringEngine.ExecuteAsync(snapshot, ct)
  finally: semaphore.Release()
```

**Each request gets an independent execution snapshot.** `SemaphoreSlim(N, N)` caps concurrency without blocking threads.

### Key Design Constraint: Channel<T> Not Used for EventBus

The existing `EventBus` is lock-free broadcast (`ConcurrentDictionary` + `ConcurrentBag`). It is **not** being replaced by `Channel<T>`. The Channel model sits **above** the EventBus at the request-scheduling layer, not inside event dispatch. This avoids rewiring the entire pub-sub system.

---

## Contracts API Design Principles

These principles govern what goes into `IModuleConfigProvider`, `IAnimaContext` in Contracts:

**Principle 1: Contracts expose read-only or append-only operations only.**
- `IModuleConfigProvider.GetConfig(animaId, moduleId)` — read only, returns `IReadOnlyDictionary<string, string>`
- `IAnimaContext.ActiveAnimaId` — read only getter + event
- `ICrossAnimaRouter.RouteRequestAsync(...)` + `GetPortsForAnima(...)` — calling, not mutating registry

**Principle 2: No Core types in Contracts method signatures.**
- `IModuleConfigProvider` must not reference `AnimaModuleConfigService`, `PluginRegistry`, or any `Core` type
- Parameter types: primitives, BCL types (`string`, `IReadOnlyDictionary`, `CancellationToken`), or other Contracts types

**Principle 3: Thin — don't dump everything there.**
- `IModuleService`, `IHeartbeatService`, `IAnimaModuleStateService` stay in `Core` — these are host-side management APIs, not module-facing APIs
- Only what the 14 built-in modules actually call goes to Contracts

**Principle 4: Additive — no breaking changes to existing Contracts.**
- `IModule`, `IModuleExecutor`, `IEventBus`, `ITickable`, `IModuleMetadata`, `IModuleInput/Output` are already in Contracts and must not change
- New interfaces are additions only

---

## Module Decoupling: What Changes Per Module

Analysis of which `Core` namespaces each built-in module currently imports:

| Module | Core Deps to Remove | Contracts Replacement |
|--------|--------------------|-----------------------|
| `LLMModule` | `OpenAnima.Core.Anima.IAnimaContext`, `OpenAnima.Core.Services.IAnimaModuleConfigService`, `OpenAnima.Core.Routing.ICrossAnimaRouter`, `OpenAnima.Core.LLM.*` | `IAnimaContext` (Contracts), `IModuleConfigProvider` (Contracts), `ICrossAnimaRouter` subset (Contracts); `ILLMService` moved to Contracts or kept in Core via constructor injection |
| `AnimaRouteModule` | `OpenAnima.Core.Anima.IAnimaContext`, `OpenAnima.Core.Services.IAnimaModuleConfigService`, `OpenAnima.Core.Routing.ICrossAnimaRouter` | All three move to Contracts |
| `AnimaInputPortModule` | `OpenAnima.Core.Anima.IAnimaContext`, `OpenAnima.Core.Routing.ICrossAnimaRouter` | Both move to Contracts |
| `AnimaOutputPortModule` | `OpenAnima.Core.Anima.IAnimaContext`, `OpenAnima.Core.Routing.ICrossAnimaRouter` | Both move to Contracts |
| `ChatInputModule` | None (only `IEventBus` from Contracts) | Already clean |
| `ChatOutputModule` | Minimal Core refs | Check for `IAnimaContext` usage |
| `FixedTextModule` | `IAnimaModuleConfigService` | `IModuleConfigProvider` (Contracts) |
| `TextJoinModule` | None | Already clean |
| `TextSplitModule` | None | Already clean |
| `ConditionalBranchModule` | `IAnimaModuleConfigService` | `IModuleConfigProvider` (Contracts) |
| `HeartbeatModule` | None | Already clean |
| `HttpRequestModule` | `IAnimaModuleConfigService` | `IModuleConfigProvider` (Contracts) |
| `FormatDetector` | None (pure logic) | Already clean |

**Decoupling completion test:** After v1.7, `dotnet build` on a project that references only `OpenAnima.Contracts` (not `OpenAnima.Core`) must succeed for all 14 built-in modules if they were extracted. This is the DECPL-01 validation criterion.

---

## Installation

```bash
# No new packages for v1.7 — everything is BCL

# Verify System.Threading.Channels is available (it is, in .NET 8 BCL)
# No dotnet add package needed

# Verify build after interface moves compile correctly
cd /path/to/OpenAnima
dotnet build src/OpenAnima.Contracts/OpenAnima.Contracts.csproj
dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj
```

---

## Alternatives Considered

### Concurrency Model

| Recommended | Alternative | Why Not |
|-------------|-------------|---------|
| `Channel<T>` for Activity Channels | `BlockingCollection<T>` | `BlockingCollection` blocks the calling thread on `Take()`; unusable in async code. `Channel<T>` is the modern async replacement. |
| `Channel<T>` for Activity Channels | `ActionBlock<T>` (TPL Dataflow) | `ActionBlock` requires `System.Threading.Tasks.Dataflow` NuGet package (not BCL in all targets). More complex API. `Channel<T>` covers the serial-queue use case with simpler code. |
| `Channel<T>` for Activity Channels | `ConcurrentQueue<T>` + polling | Polling wastes CPU. `Channel<T>` uses `ValueTask`-based notification (zero allocation on hot path). |
| `SemaphoreSlim` for stateless gate | `lock {}` keyword | `lock` blocks the thread; cannot be used with `await`. `SemaphoreSlim.WaitAsync()` yields the thread to the pool while waiting. |
| `SemaphoreSlim` for stateless gate | Orleans / Akka.NET actor model | Full actor frameworks (Orleans, Akka.NET) are correct for distributed or high-scale scenarios but are 100-500 KB of dependencies and significant conceptual overhead for a single-process local-first agent runtime. `SemaphoreSlim` + `Channel<T>` deliver the same per-entity serial execution semantics with zero external dependencies. |
| In-process `Channel<T>` | Per-request `IServiceScope` from DI | Scoped DI is for request isolation at the dependency level, not execution concurrency. `Channel<T>` controls when work runs; DI scope controls what services are resolved. These are orthogonal and can coexist. |

### Contracts API Design

| Recommended | Alternative | Why Not |
|-------------|-------------|---------|
| Thin `IModuleConfigProvider` (read-only dict) | Pass full `IAnimaModuleConfigService` to Contracts | `IAnimaModuleConfigService` has `SetConfigAsync`, `InitializeAsync`, and references `Core.Services` internals — exposing it to Contracts creates a circular dep risk and exposes write operations modules should not call |
| Move `IAnimaContext` to Contracts | Keep in Core, reference Core from Contracts | Core cannot reference Contracts AND Contracts reference Core — circular. Current flow must be Contracts ← Core, so types modules need must be in Contracts |
| Subset `ICrossAnimaRouter` interface in Contracts | Move the whole `CrossAnimaRouter` implementation | Implementation has `ConcurrentDictionary`, `AnimaRuntimeManager` refs, and cleanup timers — all Core concerns. Only the `Task RouteRequestAsync(...)` and `IReadOnlyList<PortRegistration> GetPortsForAnima(...)` signatures move to Contracts |

### Module Decoupling

| Recommended | Alternative | Why Not |
|-------------|-------------|---------|
| Move built-in modules to `OpenAnima.Contracts`-only deps | Keep modules in Core with Core deps | Modules in Core with Core deps = external developers can never achieve feature parity with built-ins (API-02 requirement). The whole point of decoupling is that a third-party module compiled only against Contracts can do everything a built-in does |
| Keep `ILLMService` interface in Core for now | Move `ILLMService` to Contracts immediately | `ILLMService` depends on `ChatMessageInput` which is currently a Core type. Moving it to Contracts requires moving `ChatMessageInput` too. This is correct but is a larger refactor; flag for v1.7 scope assessment |

---

## What NOT to Add

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Orleans / Akka.NET / Service Fabric Reliable Actors | Full distributed actor frameworks — massive dependency for a local-first single-process runtime. Per-entity serial execution is achievable with `Channel<T>` + single consumer task at 1/100th the complexity | `Channel<T>` + `SemaphoreSlim` |
| `System.Threading.Tasks.Dataflow.ActionBlock<T>` | Requires separate NuGet package, more complex API surface, same semantics as `Channel<T>` + consumer task | `Channel.CreateUnbounded<T>()` + `ReadAllAsync` consumer |
| `BlockingCollection<T>` | Thread-blocking API incompatible with the async-first codebase | `Channel<T>` |
| `lock {}` in async methods | Blocks thread pool threads; causes deadlocks when awaiting inside locked sections | `SemaphoreSlim.WaitAsync()` |
| MEF (Managed Extensibility Framework) | Heavy plugin framework; the project already has its own `AssemblyLoadContext` isolation with `PluginLoadContext`. Adding MEF would conflict with the existing duck-typing + interface-name resolution approach | Existing `PluginLoadContext` + Contracts interfaces |
| Moving ALL of `IAnimaModuleConfigService` to Contracts | Interface has write operations (`SetConfigAsync`) that modules should not call; moving it full creates over-broad module permissions | Thin `IModuleConfigProvider` (read-only subset) in Contracts |
| Reactive Extensions (Rx.NET) / `IObservable<T>` | Introduces push-based observable streams as a third concurrency model alongside EventBus and Channel. Increases mental model complexity with no benefit for the current use cases | `Channel<T>` for queuing, `IEventBus` for events |

---

## Integration Points with Existing Architecture

| Existing Component | v1.7 Integration |
|--------------------|-----------------|
| `AnimaRuntime` | Gains `ActivityChannelManager` — owns a `Dictionary<string, Channel<ChannelItem>>` + one `Task` consumer per channel, started/stopped alongside `HeartbeatLoop` |
| `HeartbeatLoop` | Instead of directly calling `WiringEngine.ExecuteAsync`, heartbeat tick writes to the appropriate `Channel<T>` for each stateful Anima. The channel consumer calls `WiringEngine.ExecuteAsync`. Stateless Animas keep direct execution with `SemaphoreSlim` gate |
| `WiringEngine` | No API change needed — `ExecuteAsync(CancellationToken)` signature stays. Concurrency protection is at the scheduling layer (Channel consumer), not inside WiringEngine itself |
| `EventBus` | Not changed. Channel model is above EventBus, not inside it. EventBus continues to handle pub-sub within a single execution |
| `IAnimaModuleConfigService` | Implements new `IModuleConfigProvider` (Contracts) interface — one interface addition to existing class, no behavior change |
| `AnimaContext` | Implements new `IAnimaContext` in Contracts namespace — `IAnimaContext` is extracted from `Core.Anima` to `Contracts`; `AnimaContext` class still lives in Core and still implements it |
| `CrossAnimaRouter` | Implements new `ICrossAnimaRouter` subset in Contracts — two methods extracted to Contracts interface; full implementation stays in Core |
| Built-in modules (14) | `using OpenAnima.Core.Anima`, `using OpenAnima.Core.Services`, `using OpenAnima.Core.Routing` replaced with `using OpenAnima.Contracts` equivalents. Constructor signatures change from Core types to Contracts types |
| DI registration (`Program.cs`) | No change at registration site — concrete types registered remain the same. Only the injected interfaces in module constructors change from Core to Contracts |

---

## Version Compatibility

| Component | Version | Notes |
|-----------|---------|-------|
| `System.Threading.Channels` | Built-in .NET 8 BCL | No NuGet needed. `Channel.CreateUnbounded<T>()`, `ChannelWriter<T>.TryWrite`, `ChannelReader<T>.ReadAllAsync` all available. |
| `System.Threading.SemaphoreSlim` | Built-in .NET 8 BCL | `WaitAsync(CancellationToken)` overload available since .NET 4.5; `SemaphoreSlim(initialCount, maxCount)` constructor available. |
| `System.Threading.Tasks.TaskCompletionSource<T>` | Built-in .NET 8 BCL | `TaskCreationOptions.RunContinuationsAsynchronously` flag available since .NET 4.6. |
| `System.Threading.Interlocked` | Built-in .NET 8 BCL | `Interlocked.Read(ref long)` for lock-free 64-bit reads on 32-bit platforms. |
| `OpenAnima.Contracts` project | v1.7 additions | Interface additions are additive — no breaking changes to `IModule`, `IModuleExecutor`, `IEventBus`, `ITickable` |

---

## Confidence Assessment

| Area | Confidence | Rationale |
|------|------------|-----------|
| `Channel<T>` for Activity Channels | HIGH | BCL type since .NET Core 3.0; documented in official .NET docs; `ReadAllAsync` + single consumer is the canonical serial-queue pattern confirmed in multiple official sources |
| `SemaphoreSlim` for stateless gate | HIGH | Already used in `HeartbeatLoop`; `WaitAsync()` + `finally Release()` is BCL idiom with no API uncertainty |
| Contracts API design (thin interfaces) | HIGH | Plugin/module contracts design pattern well-established in .NET ecosystem; the specific interface splits are based on direct code inspection of the 14 modules |
| Module decoupling path (which imports to remove) | HIGH | Based on direct code inspection of `LLMModule.cs`, `AnimaRouteModule.cs`, etc. The `using` statements are read directly — no inference needed |
| No new NuGet packages needed | HIGH | All named primitives (`Channel<T>`, `SemaphoreSlim`, `CancellationToken`, `Interlocked`) are BCL; verified BCL status of `System.Threading.Channels` in .NET 8 via NuGet search results |
| `ILLMService` move to Contracts (deferred) | MEDIUM | Correct direction but depends on also moving `ChatMessageInput`; scope risk for v1.7; flagged as deferred |

---

## Sources

- [Microsoft Learn — Channels in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) — `Channel<T>` API, BCL inclusion, bounded vs unbounded, `ReadAllAsync` consumer pattern. HIGH confidence.
- [.NET Blog — Introduction to System.Threading.Channels](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/) — Producer-consumer patterns, back-pressure, completion semantics. HIGH confidence.
- [NuGet — System.Threading.Channels 8.0.0](https://www.nuget.org/packages/System.Threading.Channels/8.0.0) — Confirms BCL inclusion for .NET 8 targets (no explicit package reference needed). HIGH confidence.
- [Microsoft Learn — SemaphoreSlim](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim) — `WaitAsync(CancellationToken)` API, async throttling pattern. HIGH confidence.
- [Microsoft Learn — Create .NET app with plugin support](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support) — AssemblyLoadContext plugin contracts pattern, interface-only Contracts assembly. HIGH confidence.
- [Microsoft Learn — About AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext) — Plugin isolation, type identity across contexts. HIGH confidence.
- [blog.semirhamid.com — .NET Concurrency: lock, SemaphoreSlim & Channels](https://blog.semirhamid.com/net-concurrency-lock-semaphore-slim-and-channels) — Comparison of `SemaphoreSlim` vs `Channel<T>` for serial queue vs throttling. MEDIUM confidence (blog, consistent with official docs).
- [ConcurrentDictionary pitfalls — dotnet/runtime issue #33221](https://github.com/dotnet/runtime/issues/33221) — `GetOrAdd` delegate execution outside lock; factory may run multiple times. HIGH confidence (official GitHub issue).
- Direct codebase inspection: `HeartbeatLoop.cs`, `WiringEngine.cs`, `EventBus.cs`, `LLMModule.cs`, `AnimaRouteModule.cs`, `AnimaRuntime.cs`, `IAnimaContext.cs`, `IAnimaModuleConfigService.cs` — Determines exact import removal targets and existing pattern compatibility. HIGH confidence (first-party source).

---

*Stack research for: v1.7 Runtime Foundation (concurrency model, Contracts API thickening, module decoupling)*
*Researched: 2026-03-14*
*Confidence: HIGH*
