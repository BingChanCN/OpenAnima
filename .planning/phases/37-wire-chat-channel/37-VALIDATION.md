---
phase: 37
slug: wire-chat-channel
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-16
---

# Phase 37 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 |
| **Config file** | none (convention-based) |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~ChatInputModule"` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/` |
| **Estimated runtime** | ~30 seconds (full suite) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~ChatInputModule"`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/`
- **Before `/gsd:verify-work`:** Full suite must be green (334+ tests)
- **Max feedback latency:** 10 seconds (quick), 30 seconds (full)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 37-01-01 | 01 | 1 | CONC-05 | integration | `dotnet test --filter "FullyQualifiedName~ChatInputModule_RoutesThrough_ChatChannel"` | ❌ W0 | ⬜ pending |
| 37-01-01 | 01 | 1 | CONC-06 | integration | `dotnet test --filter "FullyQualifiedName~ChatChannel_ProcessesSerially"` | ❌ W0 | ⬜ pending |
| 37-01-02 | 01 | 1 | Regression | unit | `dotnet test --filter "FullyQualifiedName~ChatInputModule"` | ✅ | ⬜ pending |
| 37-01-02 | 01 | 1 | Regression | integration | `dotnet test tests/OpenAnima.Tests/` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Integration/ChatChannelIntegrationTests.cs` — integration tests for CONC-05 (channel routing), CONC-06 (serial processing)
- [ ] Test: `ChatInputModule_RoutesThrough_ChatChannel` — verifies channel path when host available
- [ ] Test: `ChatInputModule_FallsBackToDirectPublish_WhenNoChannelHost` — verifies backward compat
- [ ] Test: `ChatChannel_ProcessesSerially_FifoOrder` — verifies serial execution guarantee

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
