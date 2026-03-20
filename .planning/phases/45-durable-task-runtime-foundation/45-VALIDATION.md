---
phase: 45
slug: durable-task-runtime-foundation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-20
---

# Phase 45 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 |
| **Config file** | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~Run" --no-build` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~Run" --no-build`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 45-01-01 | 01 | 1 | RUN-01 | unit | `dotnet test --filter "FullyQualifiedName~RunServiceTests"` | ❌ W0 | ⬜ pending |
| 45-01-02 | 01 | 1 | RUN-02 | unit | `dotnet test --filter "FullyQualifiedName~RunRepositoryTests"` | ❌ W0 | ⬜ pending |
| 45-01-03 | 01 | 1 | RUN-05 | unit | `dotnet test --filter "FullyQualifiedName~RunRepositoryTests.Steps"` | ❌ W0 | ⬜ pending |
| 45-02-01 | 02 | 2 | RUN-03 | unit | `dotnet test --filter "FullyQualifiedName~RunServiceTests.Resume"` | ❌ W0 | ⬜ pending |
| 45-02-02 | 02 | 2 | RUN-04 | unit | `dotnet test --filter "FullyQualifiedName~RunServiceTests.Cancel"` | ❌ W0 | ⬜ pending |
| 45-03-01 | 03 | 2 | CTRL-01 | unit | `dotnet test --filter "FullyQualifiedName~ConvergenceGuardTests"` | ❌ W0 | ⬜ pending |
| 45-03-02 | 03 | 2 | CTRL-02 | unit | `dotnet test --filter "FullyQualifiedName~ConvergenceGuardTests.NonProductive"` | ❌ W0 | ⬜ pending |
| 45-04-01 | 04 | 3 | RUN-02 | unit | `dotnet test --filter "FullyQualifiedName~RunRecoveryServiceTests"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/RunServiceTests.cs` — stubs for RUN-01, RUN-03, RUN-04
- [ ] `tests/OpenAnima.Tests/Unit/RunRepositoryTests.cs` — stubs for RUN-02, RUN-05
- [ ] `tests/OpenAnima.Tests/Unit/ConvergenceGuardTests.cs` — stubs for CTRL-01, CTRL-02
- [ ] `tests/OpenAnima.Tests/Unit/RunRecoveryServiceTests.cs` — stubs for RUN-02 (recovery)
- [ ] Package additions to test csproj: `Microsoft.Data.Sqlite 8.0.12`, `Dapper 2.1.72`

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| UI refresh shows persisted runs | RUN-02 | Requires browser interaction | Start run, refresh browser, verify run list shows active/completed runs with step history |
| Run visible after app restart | RUN-02 | Requires process restart | Start run, stop app, restart app, verify runs are listed |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
