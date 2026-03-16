---
gsd_state_version: 1.0
milestone: v1.7
milestone_name: Runtime Foundation
status: in_progress
last_updated: "2026-03-16T06:34:23Z"
last_activity: 2026-03-16 — Completed quick task 5: Phase 36 code review fixes (W1, W2, S1, S2, S3)
progress:
  total_phases: 5
  completed_phases: 5
  total_plans: 12
  completed_plans: 12
  percent: 100
---

# Project State: OpenAnima

**Last updated:** 2026-03-16
**Current milestone:** v1.7 Runtime Foundation

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-15)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Phase 36 COMPLETE and verified — the built-in module decoupling work is done, v1.7 phase work is complete, and the next workflow step is milestone closeout or planning the next milestone

## Current Position

Phase: 36 of 36 (Built-in Module Decoupling) — COMPLETE
Plan: 5 of 5 completed
Status: Phase 36 complete and verified — the authoritative 12-module inventory is codified in tests, the one `OpenAnima.Core.LLM` exception is enforced automatically, all built-ins resolve from startup DI, and the full regression suite is green
Last activity: 2026-03-16 — Phase 36 complete and verified (334/334 OpenAnima.Tests + 76/76 OpenAnima.Cli.Tests)

Progress: [██████████] 100% (v1.7)

## Performance Metrics

**Velocity:**
- Total plans completed: 83 (across v1.0–v1.7 Phase 36 complete)

**By Milestone:**

| Milestone | Phases | Plans | Shipped |
|-----------|--------|-------|---------|
| v1.0 Core Platform | 2 | 5 | 2026-02-21 |
| v1.1 WebUI Dashboard | 5 | 10 | 2026-02-23 |
| v1.2 LLM Integration | 3 | 6 | 2026-02-25 |
| v1.3 Visual Wiring | 10 | 21 | 2026-02-28 |
| v1.4 Module SDK | 3 | 8 | 2026-02-28 |
| v1.5 Multi-Anima | 5 | 13 | 2026-03-09 |
| v1.6 Cross-Anima Routing | 4 | 8 | 2026-03-14 |
| v1.7 Phase 32 (Test Baseline) | 1 | 1 | 2026-03-15 |
| v1.7 Phase 33 (Concurrency Fixes) | 1 | 1 | 2026-03-15 |
| v1.7 Phase 34 (Activity Channel Model) | 1 | 2 | 2026-03-15 |

**Phase 32 Metrics:**
- Plan 01: 15 min, 2 tasks, 2 files modified

**Phase 33 Metrics:**
- Plan 01: 20 min, 2 tasks, 7 files modified

**Phase 34 Metrics:**
- Plan 01: 13 min, 2 tasks, 4 files created
- Plan 02: 30 min, 2 tasks, 14 files modified

**Phase 35 Metrics:**
- Plan 01: 4 min, 2 tasks, 9 files created
- Plan 02: 12 min, 2 tasks, 19 files modified
- Plan 03: 11 min, 2 tasks, 5 files created/modified

**Phase 36 Metrics:**
- Plan 01: 35 min, 2 tasks, 10 files created/modified
- Plan 02: 36 min, 2 tasks, 4 files modified (3 cohort files audited with no source delta)
- Plan 03: 23 min, 2 tasks, 4 files modified
- Plan 04: 67 min, 2 tasks, 3 files modified
- Plan 05: resumed session, 2 tasks, 2 files created/modified

## Accumulated Context

### Decisions (v1.7)

- ActivityChannel: use Channel.CreateUnbounded<T>() with SingleReader=true; always TryWrite from tick path — WriteAsync risks deadlock
- Interface moves to Contracts: type-forward aliases in old Core namespaces must ship in same commit — binary compat for .oamod packages
- Module migration order: simplest first (ChatInput/Output/Heartbeat → text utils → routing → LLM/HTTP); DI smoke test after each
- ANIMA-08 singleton ruled out as root cause of 3 test failures — failures were test infrastructure bugs (missing Compile Include, type identity split, EventBus type-bucket mismatch); no change needed to ANIMA-08 scope
- PluginLoadContext type identity: delete OpenAnima.Contracts.dll from plugin output dir after build so AssemblyDependencyResolver falls back to Default context's shared assembly copy
- SemaphoreSlim Wait(0) over WaitAsync(): synchronous non-blocking TryEnter gives skip-when-busy; WaitAsync() queues callers defeating skip semantics (Phase 33)
- ExecuteInternalAsync pattern: IModuleExecutor.ExecuteAsync() stays as no-op; real logic in private ExecuteInternalAsync with typed captured parameter (Phase 33)
- HttpRequestModule guard wraps HandleTriggerAsync at subscription boundary, not inside the handler (Phase 33)
- Reader.Count on UnboundedChannel throws NotSupportedException when net8.0 assembly consumed by net10.0 test runtime — use Interlocked counter for queue depth tracking (Phase 34)
- ActivityChannelHost._statelessCache is static ConcurrentDictionary shared across all instances — module types immutable at runtime, cache never stales (Phase 34)
- ActivityChannelHost property is internal (not public) on AnimaRuntime — internal type constraint; InternalsVisibleTo covers test access (Phase 34 P02)
- WiringEngine.ExecuteAsync skipModuleIds is optional ISet<string>? = null — backward compatible, stateless dispatch fork passes set to avoid double-dispatch (Phase 34 P02)
- CrossAnimaRouter routing channel test uses direct EnqueueRoute — router needs runtimeManager which is chicken-and-egg in unit tests; direct channel enqueue is correct unit of testability (Phase 34 P02)
- IModuleConfig.SetConfigAsync per-key (string key, string value) NOT bulk Dictionary — locked user decision (Phase 35 P01)
- IModuleContext.ActiveAnimaId is non-nullable string — platform guarantees initialization before module use (Phase 35 P01)
- Contracts.Routing sub-namespace established for ICrossAnimaRouter + companion types, parallel to existing Contracts.Ports (Phase 35 P01)
- RoutingTypesTests.cs keeps Core.Routing alongside Contracts.Routing — PendingRequest is Core-internal, not exported to Contracts (Phase 35 P02)
- global using alias shims for Core.Routing type files make the assembly backward-compatible without touching any call sites (Phase 35 P02)
- Direct ProjectReference to PortModule from test project for canary validation — simpler than PluginLoadContext subprocess; key proof (constructor accepts Contracts services) is fully demonstrated (Phase 35 P03)
- ICrossAnimaRouter is null in canary tests — router requires AnimaRuntimeManager chicken-and-egg; IModuleConfig and IModuleContext verified with real Core implementations (Phase 35 P03)
- AnimaModuleConfigService in DI requires await using ServiceProvider (implements IAsyncDisposable, not IDisposable) (Phase 35 P03)
- ModuleMetadataRecord now lives in OpenAnima.Contracts; the temporary Core.Modules shim inherits from the Contracts record so existing call sites keep compiling during the migration (Phase 36 P01)
- SsrfGuard now lives in OpenAnima.Contracts.Http; the temporary Core.Http shim delegates to the Contracts helper until HttpRequestModule switches imports directly (Phase 36 P01)
- The low-risk non-LLM built-in cohort already had ChatInputModule, ChatOutputModule, and HeartbeatModule aligned closely enough that Phase 36 Plan 02 only needed source deltas in the text/branch modules after audit (Phase 36 P02)
- Inside OpenAnima.Core.Modules files, construct `OpenAnima.Contracts.ModuleMetadataRecord` explicitly to avoid accidentally binding back to the temporary Core shim by unqualified name (Phase 36 P02/P03)
- Existing test stubs that implement obsolete Core config/context interfaces remain assignable to `IModuleConfig` and `IModuleContext`, so the routing/HTTP regression suite stayed source-compatible during the module migration (Phase 36 P03)
- `LLMModule` keeps `OpenAnima.Core.LLM` as the only remaining Core import; all other module-facing surfaces in the file should come from Contracts (Phase 36 P04)
- In-process CLI command tests must serialize `Console.SetOut`/`Console.SetError` usage and disable assembly-level parallelization to avoid false failures from shared console capture state (Phase 36 P04)
- Source-audit tests that inspect repo files from the test output directory must walk up five parent segments from `AppContext.BaseDirectory` to reach the repository root in this solution layout (Phase 36 P05)
- Test fixtures that register real `AnimaModuleConfigService`/router/runtime services must dispose the ServiceProvider asynchronously and keep temp-directory cleanup best-effort to avoid teardown-only false failures (Phase 36 P05)

### Known Blockers

- [Phase 32]: RESOLVED — 3 pre-existing failures fixed; ANIMA-08 was NOT the root cause
- [Phase 33]: RESOLVED — CONC-01 through CONC-04 all fixed; 244/244 tests green
- [Phase 34]: RESOLVED — ActivityChannelHost wired, all channels active, 266/266 tests green
- [Phase 35]: ILLMService move also requires ChatMessageInput move — v1.7 vs v1.8 scope decision needed during Phase 35 planning
- [Phase 36]: `dotnet test ... -q` on `OpenAnima.Tests` can false-fail under the .NET 10 SDK with `Building target "CoreCompile" completely`; rerun with normal verbosity for reliable verification evidence
- [Phase 36]: Running two `dotnet test` processes against either test project in parallel can race on shared `obj` outputs (`SharedResources.*.resources`, `AssemblyReference.cache`, static web assets caches) — verify test projects sequentially

### Technical Debt (carried forward)

- MODMGMT-01/02/03/06: Full install/uninstall/search UI deferred to v1.8
- Schema mismatch between CLI and Runtime (extended manifest fields)
- TextJoin fixed 3 input ports — static port system limitation
- ANIMA-08: Global IEventBus singleton kept for DI — full per-Anima module instances deferred to v2+
- ModuleTestHarness subprocess compilation: fragile; consider Roslyn CSharpCompilation API refactor in future (deferred, not needed for CONC-10)

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|--------------|
| 3 | 交叉评审一下phase 36 | 2026-03-16 | e7464d2 | [3-phase-36](./quick/3-phase-36/) |
| 4 | Phase 36 code quality review | 2026-03-16 | f5feaf8 | [4-phase-36-code-review](./quick/4-phase-36-code-review/) |
| 5 | Phase 36 code review fixes (W1, W2, S1, S2, S3) | 2026-03-16 | 9bc2d97 | [5-phase-36-code-review-2-warnings-3-sugges](./quick/5-phase-36-code-review-2-warnings-3-sugges/) |

---

*State updated: 2026-03-16*
*Stopped at: Completed quick task 5: Phase 36 code review fixes (W1, W2, S1, S2, S3) — fixed 2 warnings and implemented 3 suggestions, 410/410 tests green*
