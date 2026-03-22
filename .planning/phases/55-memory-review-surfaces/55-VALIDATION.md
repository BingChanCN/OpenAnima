---
phase: 55
slug: memory-review-surfaces
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-22
---

# Phase 55 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 |
| **Config file** | No xunit.runner.json — uses .csproj defaults |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~MemoryGraphTests\|FullyQualifiedName~RunRepositoryTests\|FullyQualifiedName~SnapshotDiffTests" --no-build` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests --no-build` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~MemoryGraphTests|FullyQualifiedName~RunRepositoryTests|FullyQualifiedName~SnapshotDiffTests" --no-build`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 55-01-01 | 01 | 1 | MEMUI-03 | unit | `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~MemoryGraphTests" --no-build` | ✅ (extend) | ⬜ pending |
| 55-01-02 | 01 | 1 | MEMUI-02 | unit | `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~RunRepositoryTests" --no-build` | ✅ (extend) | ⬜ pending |
| 55-01-03 | 01 | 1 | MEMUI-01 | unit | `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~SnapshotDiffTests" --no-build` | ❌ W0 | ⬜ pending |
| 55-02-01 | 02 | 2 | MEMUI-02 | manual | n/a | manual-only | ⬜ pending |
| 55-02-02 | 02 | 2 | MEMUI-01 | manual | n/a | manual-only | ⬜ pending |
| 55-02-03 | 02 | 2 | MEMUI-03 | manual | n/a | manual-only | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/SnapshotDiffTests.cs` — stubs for MEMUI-01 line-level diff helper

*Existing infrastructure covers all other phase requirements. MemoryGraphTests and RunRepositoryTests use established in-memory SQLite fixture pattern.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Snapshot history expand/collapse | MEMUI-01 | Blazor UI interaction — no test framework | Open /memory, select node with snapshots, expand Snapshot History section, verify timeline entries appear |
| Snapshot diff highlighting | MEMUI-01 | Visual CSS rendering | Expand a snapshot entry, verify green/red background diff highlighting renders correctly |
| Snapshot restore flow | MEMUI-01 | Blazor confirm overlay interaction | Click "Restore to this version", confirm dialog, verify content updates and snapshot is created |
| Provenance expand/collapse with step details | MEMUI-02 | Blazor UI interaction + async fetch | Open /memory, select node with SourceStepId, verify provenance section shows step details |
| Manually created provenance label | MEMUI-02 | Visual UI rendering | Select node without SourceStepId, verify "Manually created" label appears |
| Edge list display with tooltips | MEMUI-03 | Blazor hover + tooltip rendering | Expand Relationships section, hover over counterpart URI, verify tooltip shows content summary |
| Edge counterpart URI navigation | MEMUI-03 | Cross-component navigation | Click counterpart URI in edge list, verify left tree selection updates and detail card shows the target node |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
