---
phase: 32
slug: test-baseline
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-15
---

# Phase 32 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x |
| **Config file** | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --no-build` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --no-build --filter "FullyQualifiedName~UnloadModule OR FullyQualifiedName~HeartbeatLoop OR FullyQualifiedName~DataRouting_FanOut"`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 32-01-01 | 01 | 1 | CONC-10 | unit | `dotnet test --filter "FullyQualifiedName~UnloadModule_ReleasesMemory"` | ✅ | ⬜ pending |
| 32-01-02 | 01 | 1 | CONC-10 | integration | `dotnet test --filter "FullyQualifiedName~HeartbeatLoop_MaintainsPerformance"` | ✅ | ⬜ pending |
| 32-01-03 | 01 | 1 | CONC-10 | integration | `dotnet test --filter "FullyQualifiedName~DataRouting_FanOut"` | ✅ | ⬜ pending |
| 32-01-04 | 01 | 1 | CONC-10 | regression | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. The phase consists entirely of fixing existing tests, not adding new ones.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Formerly-flaky tests annotated with [Trait] or skipped with tracked reason | CONC-10 | Requires inspection of test attributes | Grep for `[Trait("Category"` and `[Fact(Skip =` in test files |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
