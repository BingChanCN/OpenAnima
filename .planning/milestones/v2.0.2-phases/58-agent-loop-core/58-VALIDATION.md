---
phase: 58
slug: agent-loop-core
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-23
---

# Phase 58 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 |
| **Config file** | none — convention-based discovery |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "Category=AgentLoop" -x` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "Category=AgentLoop" -x`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 58-01-01 | 01 | 1 | LOOP-01 | unit | `dotnet test --filter "ToolCallParser"` | ❌ W0 | ⬜ pending |
| 58-01-02 | 01 | 1 | LOOP-01 | unit | `dotnet test --filter "ToolCallParser"` | ❌ W0 | ⬜ pending |
| 58-01-03 | 01 | 1 | LOOP-02 | unit | `dotnet test --filter "AgentToolDispatcher"` | ❌ W0 | ⬜ pending |
| 58-01-04 | 01 | 1 | LOOP-02, LOOP-05 | unit | `dotnet test --filter "AgentToolDispatcher"` | ❌ W0 | ⬜ pending |
| 58-02-01 | 02 | 2 | LOOP-03 | integration | `dotnet test --filter "AgentLoop"` | ❌ W0 | ⬜ pending |
| 58-02-02 | 02 | 2 | LOOP-03 | integration | `dotnet test --filter "AgentLoop"` | ❌ W0 | ⬜ pending |
| 58-02-03 | 02 | 2 | LOOP-04 | unit | `dotnet test --filter "AgentLoop"` | ❌ W0 | ⬜ pending |
| 58-02-04 | 02 | 2 | LOOP-04 | unit | `dotnet test --filter "AgentLoop"` | ❌ W0 | ⬜ pending |
| 58-02-05 | 02 | 2 | LOOP-06 | unit | `dotnet test --filter "AgentLoopSystemMessage"` | ❌ W0 | ⬜ pending |
| 58-02-06 | 02 | 2 | LOOP-06 | unit | `dotnet test --filter "AgentLoopSystemMessage"` | ❌ W0 | ⬜ pending |
| 58-02-07 | 02 | 2 | LOOP-07 | unit | `dotnet test --filter "AgentLoop"` | ❌ W0 | ⬜ pending |
| 58-02-08 | 02 | 2 | LOOP-07 | unit | `dotnet test --filter "AgentLoopToolRole"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/ToolCallParserTests.cs` — stubs for LOOP-01
- [ ] `tests/OpenAnima.Tests/Unit/AgentToolDispatcherTests.cs` — stubs for LOOP-02, LOOP-05
- [ ] `tests/OpenAnima.Tests/Unit/LLMModuleAgentLoopTests.cs` — stubs for LOOP-03, LOOP-04, LOOP-06, LOOP-07

*Existing infrastructure (xunit, NullAnimaModuleConfigService, FakeModuleContext, SpyLlmService) covers all phase requirements.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Cancel during execution releases semaphore cleanly | LOOP-07 | Requires UI interaction (Cancel button press) | 1. Start agent loop with multi-tool task 2. Press Cancel mid-execution 3. Send another message — should not deadlock |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
