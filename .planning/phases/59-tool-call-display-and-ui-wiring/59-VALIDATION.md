---
phase: 59
slug: tool-call-display-and-ui-wiring
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-23
---

# Phase 59 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 |
| **Config file** | none — discovered via .csproj PackageReference |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/ --filter "Category=ToolCallUI" -x` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/ --filter "Category=ToolCallUI" -x`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 59-01-01 | 01 | 1 | TCUI-01 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "ToolCallInfo" -x` | ❌ W0 | ⬜ pending |
| 59-01-02 | 01 | 1 | TCUI-01 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "ToolCallPayload" -x` | ❌ W0 | ⬜ pending |
| 59-01-03 | 01 | 1 | TCUI-01 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "ChatSessionMessage" -x` | ❌ W0 | ⬜ pending |
| 59-02-01 | 02 | 1 | TCUI-02 | manual | visual review in browser | N/A | ⬜ pending |
| 59-02-02 | 02 | 1 | TCUI-03 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "AgentTimeout" -x` | ❌ W0 | ⬜ pending |
| 59-02-03 | 02 | 1 | TCUI-04 | manual | browser interaction test | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/ToolCallInfoTests.cs` — stubs for TCUI-01: ToolCallInfo record properties, status enum, payload deserialization, ChatSessionMessage.ToolCalls default state
- [ ] Add `ToolCallInfo_DefaultStatus_IsRunning` and `ToolCallInfo_IsExpanded_DefaultsFalse` to new file
- [ ] Add `ChatSessionMessage_ToolCalls_StartsEmpty` to existing `tests/OpenAnima.Tests/Unit/ChatSessionStateTests.cs`

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Badge shows "Used N tools" when ToolCalls.Count > 0 and not streaming | TCUI-02 | Blazor component rendering requires browser | Run agent conversation, verify badge appears after completion |
| Send button disabled during generation | TCUI-04 | Blazor component UI state requires browser | Start agent loop, verify send button is disabled, verify re-enable after completion |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
