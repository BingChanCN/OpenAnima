---
phase: 28
slug: routing-infrastructure
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-11
---

# Phase 28 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 |
| **Config file** | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "Category=Routing" --no-build` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --no-build` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "Category=Routing" --no-build`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green (minus 3 pre-existing failures)
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 28-01-01 | 01 | 1 | ROUTE-01 | unit | `dotnet test ... --filter "CrossAnimaRouterTests"` | ❌ W0 | ⬜ pending |
| 28-01-02 | 01 | 1 | ROUTE-02 | unit | `dotnet test ... --filter "CrossAnimaRouterTests"` | ❌ W0 | ⬜ pending |
| 28-01-03 | 01 | 1 | ROUTE-03 | unit | `dotnet test ... --filter "RouteRequestAsync_ValidTarget_TimesOut"` | ❌ W0 | ⬜ pending |
| 28-01-04 | 01 | 1 | ROUTE-04 | unit | `dotnet test ... --filter "PeriodicCleanup_RemovesExpiredEntries"` | ❌ W0 | ⬜ pending |
| 28-01-05 | 01 | 1 | ROUTE-05 | unit | `dotnet test ... --filter "CancelPendingForAnima_FailsInflightRequests"` | ❌ W0 | ⬜ pending |
| 28-02-01 | 02 | 1 | ROUTE-06 | integration | `dotnet test ... --filter "DeleteAsync_CancelsPendingRequests"` | ❌ W0 | ⬜ pending |
| 28-02-02 | 02 | 1 | ANIMA-08 | integration | `dotnet test ... --filter "AnimaEventBus_Isolation"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/CrossAnimaRouterTests.cs` — stubs for ROUTE-01, ROUTE-02, ROUTE-03, ROUTE-04, ROUTE-05
- [ ] `tests/OpenAnima.Tests/Integration/CrossAnimaRouterIntegrationTests.cs` — stubs for ROUTE-06 and ANIMA-08 isolation
- [ ] `src/OpenAnima.Core/Routing/` directory and all new routing source files

*No new test framework needed — xunit is already installed and configured.*

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
