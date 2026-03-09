---
phase: 27-built-in-modules
plan: 01
subsystem: modules
tags: [csharp, dotnet, eventbus, imoduleexecutor, template-interpolation, expression-evaluator]

# Dependency graph
requires:
  - phase: 26-module-configuration-ui
    provides: EditorConfigSidebar and IAnimaModuleConfigService for per-Anima config

provides:
  - FixedTextModule with {{variable}} template interpolation from config key-value pairs
  - TextJoinModule joining 3 input ports with configurable separator
  - TextSplitModule splitting by delimiter into JSON array output
  - ConditionalBranchModule routing input to true/false port based on expression evaluator
  - HeartbeatModule made optional (excluded from auto-init, still registered for palette)

affects:
  - 27-02 (LLM config extension)
  - Any phase adding more built-in modules

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Config-at-execution: modules read IAnimaModuleConfigService in ExecuteAsync (not InitializeAsync) to respect active Anima singleton
    - Port-registration-vs-auto-init: split Type[] arrays let some modules appear in palette without auto-starting at startup
    - EventBus-execute-trigger: FixedTextModule subscribed to {Name}.execute event for push-based triggering without input ports

key-files:
  created:
    - src/OpenAnima.Core/Modules/FixedTextModule.cs
    - src/OpenAnima.Core/Modules/TextJoinModule.cs
    - src/OpenAnima.Core/Modules/TextSplitModule.cs
    - src/OpenAnima.Core/Modules/ConditionalBranchModule.cs
  modified:
    - src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs
    - src/OpenAnima.Core/Hosting/WiringInitializationService.cs

key-decisions:
  - "PortRegistrationTypes (8 types including HeartbeatModule) vs AutoInitModuleTypes (7 types excluding HeartbeatModule) — split ensures HeartbeatModule appears in ModulePalette but does not start automatically"
  - "TextJoinModule uses fixed 3 input ports (not dynamic) — static port system cannot support dynamic ports without major architectural change"
  - "FixedTextModule subscribes to .execute event (not input port) for version 1 — dynamic input-as-variable is deferred enhancement"
  - "ConditionalBranchModule expression evaluator is ~150 lines with recursive descent for ||, &&, !, parentheses, method calls, length comparisons"

patterns-established:
  - "IAnimaModuleConfigService.GetConfig() called inside ExecuteAsync with _animaContext.ActiveAnimaId to always use the correct per-Anima config"
  - "ShutdownAsync disposes all IDisposable subscriptions from _subscriptions list"
  - "Modules set _state = Error and log warning but route to safe default (false) on expression parse errors rather than crashing"

requirements-completed: [BUILTIN-01, BUILTIN-03, BUILTIN-04, BUILTIN-05, BUILTIN-06, BUILTIN-10]

# Metrics
duration: 5min
completed: 2026-03-02
---

# Phase 27 Plan 01: Built-in Modules (Text + Branch) Summary

**Four new built-in IModuleExecutor modules with EventBus port wiring, config-at-execution, and a recursive expression evaluator for conditional branching**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-02T21:18:40Z
- **Completed:** 2026-03-02T21:23:45Z
- **Tasks:** 2
- **Files modified:** 6 (4 created, 2 modified)

## Accomplishments

- Created FixedTextModule with {{variable}} template interpolation triggered via .execute event subscription
- Created TextJoinModule buffering 3 named input ports and joining with configurable separator
- Created TextSplitModule outputting JSON array string from delimiter-based splitting
- Created ConditionalBranchModule with full recursive expression evaluator (||, &&, !, parentheses, contains/startsWith/endsWith, length comparisons, ==, !=)
- Split WiringInitializationService ModuleTypes into PortRegistrationTypes (8, includes HeartbeatModule) and AutoInitModuleTypes (7, excludes HeartbeatModule) so heartbeat is optional per BUILTIN-10
- Registered all four new modules as singletons in WiringServiceExtensions

## Task Commits

Each task was committed atomically:

1. **Task 1: Create four new built-in module classes** - `4ecd021` (feat)
2. **Task 2: Register new modules in DI and split WiringInitializationService arrays** - `f418dea` (feat)

## Files Created/Modified

- `src/OpenAnima.Core/Modules/FixedTextModule.cs` - Outputs configurable text with {{key}} template interpolation; subscribes to .execute event
- `src/OpenAnima.Core/Modules/TextJoinModule.cs` - Joins input1/input2/input3 with configurable separator; buffers and clears per cycle
- `src/OpenAnima.Core/Modules/TextSplitModule.cs` - Splits by delimiter, serializes parts to JSON array string via System.Text.Json
- `src/OpenAnima.Core/Modules/ConditionalBranchModule.cs` - Expression evaluator with recursive descent; routes to true/false output port
- `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` - Added 4 new singleton registrations
- `src/OpenAnima.Core/Hosting/WiringInitializationService.cs` - Replaced ModuleTypes with PortRegistrationTypes + AutoInitModuleTypes; updated both iteration loops

## Decisions Made

- **Split port arrays:** PortRegistrationTypes includes HeartbeatModule (appears in palette), AutoInitModuleTypes excludes it (not auto-started) — satisfies BUILTIN-10 without removing HeartbeatModule from the system
- **Fixed 3 input ports for TextJoinModule:** Static port system (InputPortAttribute) does not support dynamic port counts; fixed at 3 inputs covers all practical join scenarios
- **FixedTextModule execute trigger only (no input port):** Dynamic input-as-variable requires input port subscriptions that don't exist yet; deferred per plan spec
- **Expression evaluator is pragmatic (~150 lines):** Recursive descent with simple string scanning, not a full parser/lexer — sufficient for the defined operator set

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

None — all four modules compiled cleanly on first attempt.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- All four new module classes are registered and will auto-initialize on startup
- HeartbeatModule appears in ModulePalette but does not auto-start — users can add it manually
- Ready for Phase 27-02 (LLM config extension: apiUrl, apiKey, modelName per-Anima config)
- ConditionalBranchModule's expression evaluator tested via compilation; runtime correctness requires end-to-end testing with wired Anima workflow

---
*Phase: 27-built-in-modules*
*Completed: 2026-03-02*
