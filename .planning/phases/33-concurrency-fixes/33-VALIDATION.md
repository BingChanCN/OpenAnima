---
phase: 33
slug: concurrency-fixes
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-15
---

# Phase 33 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x |
| **Config file** | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --no-build` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --no-build`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 33-01-01 | 01 | 1 | CONC-02 | unit | `dotnet test --filter "FullyQualifiedName~WiringEngine"` | Yes | pending |
| 33-01-02 | 01 | 1 | CONC-03 | integration | `dotnet test --filter "FullyQualifiedName~LLMModule"` | Yes | pending |
| 33-01-03 | 01 | 1 | CONC-04 | unit | `dotnet test --filter "FullyQualifiedName~Concurrency"` | No — W0 | pending |
| 33-01-04 | 01 | 1 | CONC-01 | regression | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` | Yes | pending |

*Status: pending · green · red · flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/ConcurrencyGuardTests.cs` — stubs for CONC-04 (skip-when-busy behavior verification)

*Existing infrastructure covers CONC-01, CONC-02, CONC-03 via regression suite.*

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
