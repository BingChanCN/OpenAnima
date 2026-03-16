---
quick_task: 6
type: review
scope: phase_34_activity_channel_model_and_phase_35_contracts_api_expansion
date: 2026-03-16
reviewed_files: 19
---

# Phase 34 / Phase 35 Code Review

## Findings by Severity

### Blockers (4)

**B1: Phase 35 DI bridge deadlocks the manager/router pair**
- **Files:** `src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs:36`, `src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs:41`, `src/OpenAnima.Core/Program.cs:67`, `src/OpenAnima.Core/Hosting/AnimaInitializationService.cs:21`, `tests/OpenAnima.Tests/Integration/CanaryModuleTests.cs:101`, `tests/OpenAnima.Tests/Integration/CanaryModuleTests.cs:134`
- **Issue:** `ICrossAnimaRouter` is registered by resolving `IAnimaRuntimeManager`, and `IAnimaRuntimeManager` is registered by resolving `ICrossAnimaRouter`. The default DI container does not break that cycle.
- **Impact:** Real DI resolution hangs. A throwaway net8.0 console that only called `services.AddLogging(); services.AddAnimaServices(...);` timed out after 2 seconds resolving either `IAnimaRuntimeManager` or `ICrossAnimaRouter`.
- **Why it shipped:** The canary coverage never resolves a non-null router. The tests either pass `null` for `ICrossAnimaRouter` or omit the router registration entirely.

**B2: Phase 35 routing “shims” are compile-time aliases, not real compatibility shims**
- **Files:** `src/OpenAnima.Core/Routing/ICrossAnimaRouter.cs:1`, `src/OpenAnima.Core/Routing/PortRegistration.cs:1`, `src/OpenAnima.Core/Routing/RouteResult.cs:1`, `src/OpenAnima.Core/Routing/RouteRegistrationResult.cs:1`, `tests/OpenAnima.Tests/Unit/ContractsApiTests.cs:350`
- **Issue:** The old `OpenAnima.Core.Routing.*` files are now only `global using` aliases. That helps current source files compile, but it does not emit `OpenAnima.Core.Routing` types or type-forwarded metadata for already-built consumers.
- **Impact:** The binary-compatibility claim for existing plugins/consumers is not actually satisfied. Anything compiled against the old Core routing type identities will not be rescued by these aliases.
- **Why it shipped:** The tests only compile against the new source tree. They do not validate an already-built consumer that still references the old Core routing symbols.

**B3: Phase 34 tick routing stopped driving `HeartbeatModule.port.tick`**
- **Files:** `src/OpenAnima.Core/Runtime/HeartbeatLoop.cs:141`, `src/OpenAnima.Core/Anima/AnimaRuntime.cs:84`, `src/OpenAnima.Core/Modules/HeartbeatModule.cs:34`, `src/OpenAnima.Core/Modules/HeartbeatModule.cs:40`, `tests/OpenAnima.Tests/Integration/ActivityChannelIntegrationTests.cs:277`
- **Issue:** Once `HeartbeatLoop` has an `ActivityChannelHost`, a tick enqueues `TickWorkItem` and the channel callback later publishes `HeartbeatModule.execute`. But `HeartbeatModule` emits its trigger output only from `TickAsync`; `ExecuteAsync` is intentionally a no-op.
- **Impact:** Pipelines waiting on `HeartbeatModule.port.tick` stop firing in the real runtime, even though ticks are still counted.
- **Why it shipped:** The phase test asserts that `HeartbeatModule.execute` is published, not that the real heartbeat output port fires.

**B4: The stateless-dispatch fork is effectively dead code**
- **Files:** `src/OpenAnima.Core/Anima/AnimaRuntime.cs:39`, `src/OpenAnima.Core/Anima/AnimaRuntime.cs:73`, `src/OpenAnima.Core/Anima/AnimaRuntime.cs:76`, `src/OpenAnima.Core/Plugins/PluginRegistry.cs:36`, `src/OpenAnima.Core/Services/ModuleService.cs:59`, `tests/OpenAnima.Tests/Integration/ActivityChannelIntegrationTests.cs:200`, `tests/OpenAnima.Tests/Integration/ActivityChannelSoakTests.cs:130`
- **Issue:** `AnimaRuntime` creates a fresh per-runtime `PluginRegistry` and uses it to classify modules as stateless, but nothing populates that registry for built-in modules. The registrations happen on the app-level `PluginRegistry` owned by `ModuleService`, not the per-runtime one.
- **Impact:** `PluginRegistry.GetModule(node.ModuleName)` returns `null`, so `statelessIds` stays empty and the Phase 34 concurrent stateless bypass never actually engages.
- **Why it shipped:** The phase tests simulate concurrency by publishing `*.execute` events directly instead of exercising the real `AnimaRuntime` classification path.

### Warnings (2)

**W1: The real chat UI path still bypasses the chat channel**
- **Files:** `src/OpenAnima.Core/Components/Shared/ChatPanel.razor:223`, `src/OpenAnima.Core/Modules/ChatInputModule.cs:34`, `src/OpenAnima.Core/Anima/AnimaRuntime.cs:102`, `tests/OpenAnima.Tests/Integration/ActivityChannelIntegrationTests.cs:120`
- **Issue:** `ChatPanel` calls `ChatInputModule.SendMessageAsync`, and `SendMessageAsync` still publishes straight to the event bus. There is no channel-host setter or `EnqueueChat` path on the module anymore.
- **Impact:** User chat ingress remains outside the serialization model Phase 34 was supposed to enforce for heartbeat/chat/routing ingress.
- **Why it shipped:** The integration coverage enqueues `ChatWorkItem` directly on the host instead of using the real UI/module path.

**W2: Per-key `SetConfigAsync` can lose concurrent writes**
- **Files:** `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs:37`, `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs:44`
- **Issue:** The per-key overload calls `GetConfig()` before acquiring `_lock`, mutates that detached copy, then forwards it to the bulk setter. Two concurrent callers can read the same snapshot and each overwrite the other's key.
- **Impact:** Parallel config saves can drop fields nondeterministically. This is exactly the kind of usage the Phase 35 per-key API invites.

## Positive Notes

- The Contracts surface itself is clean and minimal. `IModuleConfig`, `IModuleContext`, and `ICrossAnimaRouter` expose the intended module-facing capabilities without leaking extra Core internals.
- The compatibility-shim approach is directionally sound: keeping Core aliases thin reduces migration churn.
- The focused regression suite is fast and easy to run. The problem is coverage depth at the changed seams, not test maintainability.

## Evidence

- Scoped regression run:
  `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~ContractsApiTests|FullyQualifiedName~CanaryModuleTests|FullyQualifiedName~ActivityChannelIntegrationTests|FullyQualifiedName~ActivityChannelSoakTests|FullyQualifiedName~ActivityChannelHostTests"`
  Result: **85/85 passed**
- Throwaway DI probe:
  A temporary net8.0 console referencing `OpenAnima.Core` and calling `AddAnimaServices()` timed out resolving both `IAnimaRuntimeManager` and `ICrossAnimaRouter`.

## Immediate Recommendations

1. Fix the DI cycle in `AddAnimaServices()` before relying on Phase 35’s advertised router injection path.
2. Replace the routing aliases with real compatibility types or actual type forwarding metadata if binary compatibility is required.
3. Restore real heartbeat behavior by ensuring the channel path still reaches `HeartbeatModule.TickAsync` or directly publishes `HeartbeatModule.port.tick`.
4. Make Phase 34’s stateless classification use the actual module inventory the runtime executes, not an empty per-runtime registry.
5. Reintroduce a real `EnqueueChat` path for `ChatInputModule` and add a test that goes through `ChatPanel` or `ChatInputModule.SendMessageAsync`.
6. Move the per-key config merge inside the lock or provide an atomic update primitive.
