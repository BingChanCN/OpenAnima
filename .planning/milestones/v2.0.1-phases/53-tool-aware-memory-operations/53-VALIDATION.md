---
phase: 53
slug: tool-aware-memory-operations
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-22
---

# Phase 53 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 |
| **Config file** | none — uses xunit defaults |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryToolPhase53" -x` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryToolPhase53" -x`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 53-01-01 | 01 | 1 | TOOL-01 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~ToolDescriptorInjection" -x` | ❌ W0 | ⬜ pending |
| 53-01-02 | 01 | 1 | TOOL-01 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~ToolDescriptorInjection" -x` | ❌ W0 | ⬜ pending |
| 53-02-01 | 02 | 1 | TOOL-02 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryLinkTool" -x` | ❌ W0 | ⬜ pending |
| 53-02-02 | 02 | 1 | TOOL-02 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryLinkTool" -x` | ❌ W0 | ⬜ pending |
| 53-03-01 | 03 | 1 | TOOL-03 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryRecallTool" -x` | ❌ W0 | ⬜ pending |
| 53-03-02 | 03 | 1 | TOOL-03 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~MemoryRecallTool" -x` | ❌ W0 | ⬜ pending |
| 53-04-01 | 04 | 1 | TOOL-04 | unit | Covered by TOOL-02/TOOL-03 tests | existing | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/MemoryToolPhase53Tests.cs` — stubs for TOOL-01, TOOL-02, TOOL-03 (follows MemoryModuleTests.cs pattern with shared-cache SQLite)

*Existing infrastructure covers framework requirements — xunit already present.*

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
