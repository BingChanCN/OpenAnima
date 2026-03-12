---
phase: 29
slug: routing-modules
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-12
---

# Phase 29 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 |
| **Config file** | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| **Quick run command** | `dotnet test --filter "Category=Routing" --no-build` |
| **Full suite command** | `dotnet test --no-build` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Category=Routing" --no-build`
- **After every plan wave:** Run `dotnet test --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 29-01-01 | 01 | 0 | RMOD-01 | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaInputPort_InitializeAsync_RegistersPort" --no-build` | ❌ W0 | ⬜ pending |
| 29-01-02 | 01 | 0 | RMOD-02 | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaInputPort_RegisterPort_IncludesDescription" --no-build` | ❌ W0 | ⬜ pending |
| 29-01-03 | 01 | 0 | RMOD-03 | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaOutputPort_ConfigDropdown_ListsPorts" --no-build` | ❌ W0 | ⬜ pending |
| 29-01-04 | 01 | 0 | RMOD-04 | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaOutputPort_CompleteRequest_UsesMetadata" --no-build` | ❌ W0 | ⬜ pending |
| 29-01-05 | 01 | 0 | RMOD-05 | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaRoute_ConfigDropdown_ListsAnimas" --no-build` | ❌ W0 | ⬜ pending |
| 29-01-06 | 01 | 0 | RMOD-06 | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaRoute_CascadingDropdown_PopulatesPorts" --no-build` | ❌ W0 | ⬜ pending |
| 29-01-07 | 01 | 0 | RMOD-07 | integration | `dotnet test --filter "FullyQualifiedName~CrossAnimaRoutingE2ETests.AnimaRoute_AwaitResponse_CompletesBeforeDownstream" --no-build` | ❌ W0 | ⬜ pending |
| 29-01-08 | 01 | 0 | RMOD-08 | unit | `dotnet test --filter "FullyQualifiedName~RoutingModulesTests.AnimaRoute_OnTimeout_OutputsErrorJson" --no-build` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Modules/RoutingModulesTests.cs` — stubs for RMOD-01 through RMOD-06, RMOD-08
- [ ] `tests/OpenAnima.Tests/Integration/CrossAnimaRoutingE2ETests.cs` — stubs for RMOD-07
- [ ] Framework already installed (xUnit 2.9.3) — no additional packages needed

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Cascading dropdown UI renders correctly in Blazor sidebar | RMOD-06 | Blazor component rendering requires browser | Open editor, add AnimaRoute, verify dropdown populates |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
