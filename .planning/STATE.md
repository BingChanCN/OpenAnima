---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: unknown
stopped_at: "Completed 56-01-PLAN.md (Phase 56 complete - checkpoint:human-verify pending)"
last_updated: "2026-03-22T16:49:00.656Z"
progress:
  total_phases: 8
  completed_phases: 7
  total_plans: 16
  completed_plans: 15
---

# Project State: OpenAnima

**Last updated:** 2026-03-22
**Current milestone:** v2.0.1 Provider Registry & Living Memory

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-22)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Phase 56 — Sedimentation LLM Configuration

## Current Position

Phase: 56 (Sedimentation LLM Configuration) — EXECUTING
Plan: 1 of 1

## Performance Metrics

**Velocity:**

- Total plans completed: 117
- Average duration: TBD for v2.0.1
- Total execution time: TBD for v2.0.1

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 50. Provider Registry | 0/TBD | - | - |
| 51. LLM Module Configuration | 0/TBD | - | - |
| 52. Automatic Memory Recall | 0/TBD | - | - |
| 53. Tool-Aware Memory Operations | 0/TBD | - | - |
| 54. Living Memory Sedimentation | 0/TBD | - | - |
| 55. Memory Review Surfaces | 0/TBD | - | - |

**Recent Trend:**

- Last 5 plans: 49-03, 49-02, 49-01, 48-05, 48-04
- Trend: Stable

| Phase 50 P01 | 6m | 2 tasks | 9 files |
| Phase 50 P02 | 8m | 2 tasks | 14 files |
| Phase 50 P03 | 3m | 2 tasks | 2 files |
| Phase 51 P01 | 23 | 2 tasks | 13 files |
| Phase 51 P02 | continuation | 2 tasks | 4 files |
| Phase 52 P01 | 2m 30s | 2 tasks | 4 files |
| Phase 52 P02 | 10m | 2 tasks | 8 files |
| Phase 53 P01 | 3m | 2 tasks | 4 files |
| Phase 53 P02 | 6m | 1 tasks | 3 files |
| Phase 54 P01 | 5 | 1 tasks | 3 files |
| Phase 54 P02 | 10m | 2 tasks | 3 files |
| Phase 55 P01 | 4m | 2 tasks | 11 files |
| Phase 55 P02 | ~30m | 2 tasks | 3 files |
| Phase 57 P02 | 2min | 2 tasks | 2 files |
| Phase 56 P01 | 4m | 1 tasks | 5 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- Phase 50: Provider registry owns global provider/model lifecycle, secure key handling, and impact-aware disable/delete behavior.
- Phase 52: Automatic recall must stay bounded, ranked, deduplicated, and visibly explainable in the run timeline and prompt context.
- Phase 54: Living memory stores stable learnings with provenance and snapshots, not raw transcript dumps.
- [Phase 50]: ApiKeyProtector: Assert.ThrowsAny used in tests because AuthenticationTagMismatchException is a CryptographicException subclass in .NET 8
- [Phase 50]: ConnectionTestResult record defined in LLMProviderRegistryService.cs for co-location with the service
- [Phase 50]: ProviderDialogResult declared in separate .cs file: Blazor razor cannot declare types outside @code block
- [Phase 50]: Settings admin page injects concrete LLMProviderRegistryService (not ILLMProviderRegistry) to access full LLMProviderRecord with model lists
- [Phase 50]: API key field enforces write-only contract via @oninput exclusively, never @bind
- [Phase 50]: Test button only visible in edit mode (EditTarget != null) — no slug exists to test in create mode
- [Phase 50]: CTS cancelled in Dispose() before disposal to prevent ObjectDisposedException in background Task.Run auto-clear
- [Phase 51]: LLMModule constructor upgraded from IModuleConfig to IAnimaModuleConfigService to enable SetConfigAsync(dict) for auto-clear logic
- [Phase 51]: ClearModelSelectionAsync retains llmProviderSlug to preserve user's provider choice — only the stale model binding is cleared
- [Phase 51]: NullRegistryServiceFactory uses Lazy<T> singleton with shared temp dir for integration test isolation of LLMModule constructor
- [Phase 51]: Model-level disabled rendering excluded: LLMModelInfo has no IsEnabled field, no dead i18n key Editor.LLM.ModelDisabledWarning added
- [Phase 51]: LLM sidebar cascade reset: HandleProviderChanged clears llmModelId to prevent stale cross-provider model binding
- [Phase 52]: MemoryRecallService deduplicates glossary keyword matches per URI, joining multiple keywords in one reason string rather than producing duplicate RecalledNode entries
- [Phase 52]: RecallType stays Disclosure when a node matches both disclosure trigger and glossary — priority is not downgraded by the glossary match
- [Phase 52]: BuiltInModuleDecouplingTests allowlist updated with Core.Memory and Core.Runs as Phase 52 exceptions for LLMModule memory wiring
- [Phase 52]: Memory system message insertion order: memory at [0], then routing at [0] pushes memory to [1] — routing first, memory second, then conversation
- [Phase 53]: MemoryRecallTool calls RebuildGlossaryAsync before FindGlossaryMatches to ensure trie is populated per-call
- [Phase 53]: MemoryRecallTool deduplicates via HashSet<string> on URIs so nodes matching both glossary and disclosure paths appear once
- [Phase 53]: MemoryEdge has no SourceStepId -- provenance tracked at WorkspaceToolModule dispatch level via StepRecord, not on the edge
- [Phase 53]: Tool block appended to existing system message[0] -- memory/routing content comes first, tools last; BuildToolDescriptorBlock returns null for empty lists to prevent empty XML tags
- [Phase 54]: SedimentationService accepts Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<string>>? llmCallOverride constructor parameter — production builds ChatClient from config, tests inject fake delegate without mocking OpenAI SDK
- [Phase 54]: Keyword normalization: if value starts with '[', treat as JSON array and keep as-is; otherwise split on comma and serialize to JSON array — matches MemoryWriteTool pattern
- [Phase 54]: TriggerSedimentation fires on both non-routing and routing paths using fire-and-forget Task.Run with CancellationToken.None and snapshot-captured message values
- [Phase 54]: ISedimentationService injected as optional constructor parameter (= null) in LLMModule for backward compatibility with existing callers
- [Phase 55]: Provenance section expanded by default in MemoryNodeCard — most relevant context on node selection; Snapshot History and Relationships collapsed to reduce initial visual noise
- [Phase 55]: Restore confirmation rendered as inline overlay in MemoryGraph.razor — avoids a new component for a single-use case
- [Phase 55]: Lazy<IStepRecorder> used in BootMemoryInjector to break pre-existing DI circular dependency surfaced during visual verification
- [Phase 57]: SUMMARY metadata patched retroactively — implementation was correct; only the audit-tracking record was missing
- [Phase 56]: Settings.razor injects IAnimaContext and IAnimaModuleConfigService via existing obsoleted aliases to match existing codebase patterns
- [Phase 56]: Sedimentation config section only shows enabled providers to prevent stale selection of disabled providers

### Pending Todos

None.

### Blockers/Concerns

- Provider deletion UX must surface downstream impact clearly before destructive changes.
- Recall ranking and sedimentation thresholds may need tuning to avoid noisy memory.

## Session Continuity

Last session: 2026-03-22T16:49:00.654Z
Stopped at: Completed 56-01-PLAN.md (Phase 56 complete - checkpoint:human-verify pending)
Resume file: None
