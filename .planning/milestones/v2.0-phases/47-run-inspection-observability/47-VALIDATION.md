---
phase: 47
slug: run-inspection-observability
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-21
---

# Phase 47 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x + Microsoft.AspNetCore.Mvc.Testing |
| **Config file** | `tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~RunDetail OR FullyQualifiedName~PropagationColor OR FullyQualifiedName~WiringEngineScope" --no-build -x` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/ --no-build` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~RunDetail OR FullyQualifiedName~PropagationColor OR FullyQualifiedName~WiringEngineScope" --no-build -x`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/ --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 47-01-01 | 01 | 0 | OBS-01 | unit | `dotnet test --filter "FullyQualifiedName~RunDetailTimelineTests" -x` | ❌ W0 | ⬜ pending |
| 47-01-02 | 01 | 0 | OBS-03 | unit | `dotnet test --filter "FullyQualifiedName~PropagationColorTests" -x` | ❌ W0 | ⬜ pending |
| 47-01-03 | 01 | 0 | OBS-04 | unit | `dotnet test --filter "FullyQualifiedName~WiringEngineScopeTests" -x` | ❌ W0 | ⬜ pending |
| 47-02-01 | 02 | 1 | OBS-01 | integration | `dotnet test --filter "FullyQualifiedName~RunDetailTimelineTests" -x` | ❌ W0 | ⬜ pending |
| 47-02-02 | 02 | 1 | OBS-02 | integration | `dotnet test --filter "FullyQualifiedName~RunRepositoryTests" -x` | ✅ existing | ⬜ pending |
| 47-03-01 | 03 | 1 | OBS-03 | unit | `dotnet test --filter "FullyQualifiedName~PropagationColorTests" -x` | ❌ W0 | ⬜ pending |
| 47-04-01 | 04 | 2 | OBS-04 | unit | `dotnet test --filter "FullyQualifiedName~WiringEngineScopeTests" -x` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/RunDetailTimelineTests.cs` — stubs for OBS-01 (MergeTimeline sort, mixed entry types)
- [ ] `tests/OpenAnima.Tests/Unit/PropagationColorTests.cs` — stubs for OBS-03 (color cycling, empty propagationId returns transparent)
- [ ] `tests/OpenAnima.Tests/Unit/WiringEngineScopeTests.cs` — stubs for OBS-04 (BeginScope called with StepId)

*Existing infrastructure covers OBS-02 via RunRepositoryTests.cs.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Timeline renders chronologically with mixed step/state entries | OBS-01 | Blazor rendering requires browser | Open `/runs/{runId}`, verify entries sorted by time |
| Accordion expand shows step detail fields | OBS-02 | Visual layout verification | Click step row, verify InputSummary, OutputSummary, ErrorInfo, DurationMs visible |
| PropagationId color grouping visible | OBS-03 | CSS color rendering | Verify steps with same PropagationId share left-border color |
| Click-highlight dims non-chain steps | OBS-03 | Interactive behavior | Click a step, verify chain highlighted, others dimmed |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
