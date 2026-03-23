---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: unknown
stopped_at: Completed 60-01-PLAN.md
last_updated: "2026-03-23T14:17:19.940Z"
progress:
  total_phases: 3
  completed_phases: 3
  total_plans: 5
  completed_plans: 5
---

# Project State: OpenAnima

**Last updated:** 2026-03-23
**Current milestone:** v2.0.2 Chat Agent Loop

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-23)

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.
**Current focus:** Phase 60 — hardening-and-memory-integration

## Current Position

Phase: 60 (hardening-and-memory-integration) — EXECUTING
Plan: 1 of 1

## Accumulated Context

### Decisions

- Agent loop is an internal LLMModule concern — WiringEngine and ChatOutputModule receive only the final clean response
- Tool dispatch via AgentToolDispatcher.DispatchAsync directly (not through EventBus) — prevents semaphore deadlock
- XML text markers for tool calls (`<tool_call>` / `<param>`) — consistent with existing `<route>` convention, provider-agnostic
- _executionGuard held for full loop duration — intermediate tool call iterations suppressed from port.response
- Hard iteration ceiling (never configurable to 0 or unbounded) — default 10, max 50

Decisions are logged in PROJECT.md Key Decisions table.

- [Phase 58]: agentEnabled and agentMaxIterations added to 'agent' group in GetSchema() — separate group from provider/manual
- [Phase 58]: RunAgentLoopAsync is a private instance method on LLMModule — loop is an implementation detail, not a separate class
- [Phase 59]: OpenAnima.Core.Events added to LLMModule allowed imports — documented as Phase 59 exception in BuiltInModuleDecouplingTests
- [Phase 59]: Tool cards render BEFORE reply text in assistant bubble — visible during streaming tool execution
- [Phase 59]: IAnimaModuleConfigService injected into ChatPanel for agent mode detection — reads agentEnabled from LLMModule config
- [Phase 59]: _agentTimeoutCts replaced (not extended) on each tool call event — 60s from last activity, not from start
- [Phase 60]: agentContextWindowSize floor clamped to 1000 to prevent zero-budget pathology; truncation notice inserted before pair removal so it stays anchored in preserved zone

### Pending Todos

None.

### Blockers/Concerns

- Validate safe max iterations empirically during Phase 58 integration testing (read_file and shell_exec produce largest outputs)

## Session Continuity

Last session: 2026-03-23T14:12:00.422Z
Stopped at: Completed 60-01-PLAN.md
Resume file: None

### Quick Tasks Completed

| Task | Date | Description |
|---|---|---|
| `260323-of9` | 2026-03-23 | UI review and fix editor layout overlap |
| `260323-ox4` | 2026-03-23 | Redesign dialogs to rectangular and fix sidebar overlap |
