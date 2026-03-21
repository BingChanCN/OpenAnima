---
phase: 49
slug: structured-cognition-workflows
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-21
---

# Phase 49 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (OpenAnima.Tests, net10.0) |
| **Config file** | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests --filter "Category=Unit" --no-build` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests --no-build` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests --no-build`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 49-01-01 | 01 | 1 | COG-01 | unit | `dotnet test --filter "FullyQualifiedName~JoinBarrierModuleTests"` | ❌ W0 | ⬜ pending |
| 49-01-02 | 01 | 1 | COG-01 | unit | `dotnet test --filter "FullyQualifiedName~JoinBarrierModuleTests"` | ❌ W0 | ⬜ pending |
| 49-01-03 | 01 | 1 | COG-01 | unit | `dotnet test --filter "FullyQualifiedName~JoinBarrierModuleTests"` | ❌ W0 | ⬜ pending |
| 49-02-01 | 02 | 1 | COG-04 | unit | `dotnet test --filter "FullyQualifiedName~WiringEngineScopeTests"` | ✅ extend | ⬜ pending |
| 49-02-02 | 02 | 1 | COG-04 | unit | `dotnet test --filter "FullyQualifiedName~StepRecorderPropagationTests"` | ❌ W0 | ⬜ pending |
| 49-03-01 | 03 | 2 | COG-03 | unit | `dotnet test --filter "FullyQualifiedName~WorkflowPresetServiceTests"` | ❌ W0 | ⬜ pending |
| 49-03-02 | 03 | 2 | COG-02 | unit | `dotnet test --filter "FullyQualifiedName~RunServiceTests"` | ✅ extend | ⬜ pending |
| 49-03-03 | 03 | 2 | COG-04 | unit | `dotnet test --filter "FullyQualifiedName~RunRepositoryTests"` | ✅ extend | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/JoinBarrierModuleTests.cs` — stubs for COG-01 (emit-on-all-arrived, ignore-unconnected, buffer-clear)
- [ ] `tests/OpenAnima.Tests/Unit/StepRecorderPropagationTests.cs` — stubs for COG-04 (PropagationId carry-through)
- [ ] `tests/OpenAnima.Tests/Unit/WorkflowPresetServiceTests.cs` — stubs for COG-03 (preset discovery)

Existing files to extend:
- [ ] `tests/OpenAnima.Tests/Unit/WiringEngineScopeTests.cs` — add PropagationId non-null assertion
- [ ] `tests/OpenAnima.Tests/Unit/RunServiceTests.cs` — add workflowPreset parameter test
- [ ] `tests/OpenAnima.Tests/Unit/RunRepositoryTests.cs` — add workflow_preset column persistence test

*Existing infrastructure covers test framework setup — no new framework install needed.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| RunCard shows progress bar (X/Y steps) | COG-04 | Visual UI component | Load preset, start run, verify progress bar updates in real-time |
| NodeCard shows running/completed state during workflow | COG-04 | Visual UI component | Start codebase analysis, observe node state changes in editor |
| Codebase analysis produces grounded report | COG-03 | LLM output quality | Run preset against a real workspace, review report for accuracy |
| Preset loads correctly into editor and all nodes appear | COG-02 | Visual graph rendering | Select preset from library, verify graph renders with correct topology |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
