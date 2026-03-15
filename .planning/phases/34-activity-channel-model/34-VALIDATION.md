---
phase: 34
slug: activity-channel-model
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-15
---

# Phase 34 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 |
| **Config file** | none (implicit discovery) |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/ --filter "Category!=Soak" -x` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/` |
| **Estimated runtime** | ~25 seconds (including 10s soak test) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/ --filter "Category!=Soak" -x`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/`
- **Before `/gsd:verify-work`:** Full suite must be green (including soak)
- **Max feedback latency:** 15 seconds (excluding soak)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 34-01-01 | 01 | 0 | CONC-05 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "ActivityChannel"` | ❌ W0 | ⬜ pending |
| 34-01-02 | 01 | 0 | CONC-08 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "StatelessModule"` | ❌ W0 | ⬜ pending |
| 34-01-03 | 01 | 0 | CONC-09 | unit + soak | `dotnet test tests/OpenAnima.Tests/ --filter "Heartbeat"` | ❌ W0 | ⬜ pending |
| 34-01-04 | 01 | 0 | CONC-06 | integration | `dotnet test tests/OpenAnima.Tests/ --filter "ActivityChannel"` | ❌ W0 | ⬜ pending |
| 34-01-05 | 01 | 0 | CONC-07 | integration | `dotnet test tests/OpenAnima.Tests/ --filter "Stateless"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/ActivityChannelHostTests.cs` — stubs for CONC-05, CONC-08, CONC-09 (unit-level)
- [ ] `tests/OpenAnima.Tests/Integration/ActivityChannelIntegrationTests.cs` — stubs for CONC-06, CONC-07 (parallel channels, stateless path)
- [ ] `tests/OpenAnima.Tests/Integration/ActivityChannelSoakTests.cs` — stubs for CONC-05 soak (10s heartbeat + chat, no deadlock or missed ticks)

*Soak test tagged `[Trait("Category", "Soak")]` and excluded from quick run filter.*

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
