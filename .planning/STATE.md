---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: planning
last_updated: "2026-02-27T09:58:00.000Z"
progress:
  total_phases: 8
  completed_phases: 7
  total_plans: 19
  completed_plans: 18
  percent: 95
---

# Project State: OpenAnima v1.3

**Last updated:** 2026-02-27
**Current milestone:** v1.3 True Modularization & Visual Wiring

## Project Reference

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

**Current focus:** Transform hardcoded LLM/chat/heartbeat features into port-based modular architecture with visual drag-and-drop wiring editor.

## Current Position

**Phase:** 17 - E2E Module Pipeline Integration & Editor Polish
**Plan:** 1 of 2 complete
**Status:** In Progress
**Progress:** [█████░░░░░] 50%

**Next action:** Execute Plan 17-02 (editor runtime status + rejection feedback polish).

## Performance Metrics

**Milestone v1.3:**
- Phases: 4 total (11-14)
- Requirements: 17 total
- Coverage: 17/17 mapped (100%)
- Completed: 2 phases (Phase 11, Phase 12)
- In progress: Phase 12.5 (Runtime DI Integration)

**Phase 13 metrics:**
- Plan 13-01: 2 tasks, 10 files, 355 seconds
- Plan 13-02: 2 tasks, 4 files, 1016 seconds

**Phase 12.5 metrics:**
- Plan 12.5-01: 2 tasks, 8 files, 202 seconds, 48 tests passing
- Plan 12.5-02: 1 task, 1 file, 156 seconds, 48 tests passing
- Plan 12.5-03: 1 task, 1 file, 329 seconds, 59 tests passing (11 new integration tests)
- Plan 12.5-02: 2 tasks, 4 files, 195 seconds, 48 tests passing

**Phase 12 metrics:**
- Plan 12-01: 1 task, 2 files, 657 seconds, 12 tests passing
- Plan 12-02: 2 tasks, 3 files, 249 seconds, 9 tests passing
- Plan 12-03: 2 tasks, 3 files, 652 seconds, 7 tests passing

**Phase 11 metrics:**
- Plan 11-01: 2 tasks, 11 files, 188 seconds, 11 tests passing
- Plan 11-02: 2 tasks, 4 files, 548 seconds, 9 tests passing
- Plan 11-03: 2 tasks, 4 files, 167 seconds

**Historical velocity:**
- v1.0: 2 phases, 5 plans, 10 tasks, 1,323 LOC (1 day)
- v1.1: 5 phases, 10 plans, 8 tasks, 2,951 LOC (2 days)
- v1.2: 3 phases, 6 plans, 12 tasks, 2,611 LOC (2 days)

## Accumulated Context

### Key Decisions

**v1.3 architecture decisions:**
- Zero new dependencies: Use .NET 8.0 built-ins (enums, records, System.Text.Json) for port system
- Custom topological sort: ~100 LOC implementation avoids 500KB+ QuikGraph dependency
- HTML5 + SVG editor: Native browser APIs with Blazor, no JavaScript framework needed
- Augmentation over replacement: EventBus remains, wiring engine orchestrates execution order
- Two-phase initialization: Load modules first, then wire connections (prevents circular dependencies)
- Scoped EditorStateService: Per-circuit isolation in Blazor Server for editor state
- Throttled rendering: 50ms throttle during pan operations prevents SignalR bottleneck
- MarkupString for SVG text: Avoids Razor tag conflict with SVG elements
- NodeCard as SVG group: `<g>` element (not HTML div) for proper canvas integration
- Port positioning: Input ports at x=0, output ports at x=nodeWidth for clean connection points
- Bezier curves: Cubic bezier with horizontal control points for smooth connection flow
- Connection hit detection: Invisible 12px wide path underneath visible connection for easier clicking

**Phase 11 implementation decisions:**
- Port types fixed to Text and Trigger enum (not extensible by design - prevents type chaos)
- Attributes use AllowMultiple=true for declarative multi-port modules
- PortMetadata is immutable record with computed Id property
- ValidationResult uses static factory methods (Success/Fail) for clarity
- PortDiscovery uses Attribute.GetCustomAttributes for reflection (works across AssemblyLoadContext)
- PortRegistry uses ConcurrentDictionary for thread-safe module registration
- Fresh EventBus per test method (not reused from fixture) to avoid test isolation issues
- TaskCompletionSource with 5-second timeout for event verification (more reliable than Task.Delay)

**Phase 12 implementation decisions:**
- Kahn's algorithm (BFS-based) for topological sort over DFS for clearer level grouping
- Cycle detection integrated into GetExecutionLevels via incomplete processing check
- HasCycle wraps GetExecutionLevels in try-catch for non-throwing cycle detection
- Dictionary<string, HashSet<string>> for adjacency list enables O(1) edge lookups
- Single JSON file contains both logical topology AND visual layout for Phase 13 readiness
- Strict validation on load: ConfigurationLoader validates module existence and port type compatibility
- Async I/O throughout: JsonSerializer.SerializeAsync/DeserializeAsync with CancellationToken support
- Immutable records with init-only properties for thread-safe configuration handling
- Level-parallel execution: Task.WhenAll within level, sequential between levels for deterministic ordering
- Event-driven data routing: WiringEngine translates PortConnections into EventBus subscriptions with deep copy
- Push-based routing: Subscriptions automatically forward data downstream when source port publishes
- Deep copy via JSON serialization: DataCopyHelper uses round-trip for fan-out isolation
- Isolated failure handling: EventBus catches handler exceptions, preventing cascade failures
- Subscription lifecycle: LoadConfiguration disposes old subscriptions to prevent memory leaks

**Phase 12.5 implementation decisions:**
- Scoped lifetime for all wiring services: Enables per-circuit isolation in Blazor Server
- Factory registration pattern: ConfigurationLoader and WiringEngine use factory for constructor parameters
- Interface-based DI: Constructor parameters changed to interfaces for loose coupling and testability
- WiringInitializationService: Auto-loads last configuration on startup with graceful error handling
- Real DI container testing: Integration tests use ServiceCollection + BuildServiceProvider (not mocks)
- Temp directory per test class: Ensures config file isolation between test runs
- Port registration failure handling: Skip module with warning instead of blocking entire load operation
- IHostedService pattern: Use ASP.NET Core lifecycle for startup orchestration
- Graceful degradation: Missing/corrupt config logs warning and starts empty (no crash on startup)
- Last-config tracking: Simple text file approach for persistence (no database needed)

**Critical patterns from research:**
- Topological sort for execution order (deterministic, prevents race conditions, detects cycles)
- Port-based EventBus routing (WiringEngine translates connections into subscriptions)
- Interface-based port discovery (IPortProvider.GetPorts() works across AssemblyLoadContext)
- Throttled StateHasChanged (50-100ms during drag to prevent SignalR bottleneck)

### Active TODOs

**Phase 12.5 remaining:**
- [x] Extract interfaces and register in DI (Plan 01 - Complete)
- [x] Create WiringInitializationService for startup (Plan 02 - Complete)
- [x] Add integration tests for DI resolution (Plan 03 - Complete)

**Phase 12.5 complete!** All runtime DI integration work finished. Ready for Phase 13.

**Phase 12 remaining:**
- [x] Implement ConnectionGraph for topological sort and cycle detection (Plan 01 - Complete)
- [x] Build WiringConfiguration and ConfigurationLoader (Plan 02 - Complete)
- [x] Add WiringEngine execution orchestration (Plan 03 - Complete)

**Research flags:**
- Phase 13 (Visual Editor) needs performance validation: Specific throttling values and ShouldRender patterns require testing with 10+ modules

### Known Blockers

None currently. Phase 12.5 complete, ready for Phase 13.

### Recent Insights

**From research (2026-02-25):**
- Top 3 pitfalls identified: (1) circular dependency deadlock, (2) SignalR rendering bottleneck, (3) breaking existing features during refactoring
- All three mitigated through upfront design: cycle detection in wiring engine, throttled rendering, integration tests before refactoring
- Phase ordering validated: Port System → Wiring Engine → Visual Editor → Module Refactoring follows natural dependency graph

**From v1.2 completion:**
- SignalR version must match .NET runtime (8.0.x) to avoid circuit crashes
- Batched StateHasChanged (50ms/100 chars) prevents UI lag during streaming
- Send blocking over auto-truncation gives users control of conversation

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 1 | 帮我在github上创建一个仓库并提交代码 | 2026-02-21 | c0652f4 | [1-github](./quick/1-github/) |
| 2 | Install .NET SDK and C# LSP (csharp-ls) | 2026-02-22 | 474f31e | [2-claude-code-c-lsp](./quick/2-claude-code-c-lsp/) |
| Phase 11 P01 | 188 | 2 tasks | 11 files |
| Phase 11 P03 | 167 | 2 tasks | 4 files |
| Phase 11 P02 | 548 | 2 tasks | 4 files |
| Phase 12 P02 | 249 | 2 tasks | 3 files |
| Phase 12 P01 | 657 | 1 task | 2 files |
| Phase 12 P03 | 652 | 2 tasks | 3 files |
| Phase 12.5 P01 | 202 | 2 tasks | 8 files |
| Phase 12.5 P02 | 156 | 1 task | 1 file |
| Phase 12.5 P03 | 329 | 1 task | 1 file |
| Phase 13 P01 | 355 | 2 tasks | 10 files |
| Phase 13 P02 | 1016 | 2 tasks | 4 files |
| Phase 17 P01 | 22min | 2 tasks | 6 files |

## Session Continuity

**What just happened:**
- Completed Phase 17 Plan 01: ChatPanel now routes send/regenerate through ChatInput -> LLM -> ChatOutput pipeline only
- Added runtime topology validator + guided editor prompt when required chain is missing
- Fixed WiringEngine routing to resolve ModuleName from node IDs and preserve typed payload routing by port type
- Added/extended integration tests for configured vs missing chat pipeline and GUID-node routing behavior
- Filtered test run passed: 6/6 (`ChatPanelModulePipelineTests` + `ModulePipelineIntegrationTests`)

**What's next:**
1. Execute Phase 17 Plan 02
2. Add explicit incompatible-connection rejection feedback in editor drag flow
3. Apply RTIM visual contract (running/error/idle border states, warning icon, tooltip details, running pulse)
4. Verify runtime event -> node identity mapping with tests

**Context for next session:**
- Phase 17 Plan 01 complete with summary: `.planning/phases/17-e2e-module-pipeline-integration-editor-polish/17-01-SUMMARY.md`
- ChatPanel no longer uses direct `ILLMService` streaming path
- E2E-01 marked complete in REQUIREMENTS.md
- ROADMAP phase 17 progress updated to `1/2 In Progress`
- Remaining work is Plan 17-02 editor UX/status polish and associated tests

---
*State initialized: 2026-02-25*
*Last updated: 2026-02-27*
*Phase 17 in progress - plan 01 complete*
