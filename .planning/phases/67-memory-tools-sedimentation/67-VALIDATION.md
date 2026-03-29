---
phase: 67
slug: memory-tools-sedimentation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-29
---

# Phase 67 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | dotnet test (xUnit) |
| **Config file** | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests --filter "Category=Memory"` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests --filter "Category=Memory"`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| TBD | 01 | 1 | MEMT-01 | unit | `dotnet test --filter "MemoryCreateTool"` | ❌ W0 | ⬜ pending |
| TBD | 01 | 1 | MEMT-02 | unit | `dotnet test --filter "MemoryUpdateTool"` | ❌ W0 | ⬜ pending |
| TBD | 01 | 1 | MEMT-03 | unit | `dotnet test --filter "MemoryDeleteTool"` | ❌ W0 | ⬜ pending |
| TBD | 01 | 1 | MEMT-04 | unit | `dotnet test --filter "MemoryListTool"` | ❌ W0 | ⬜ pending |
| TBD | 02 | 1 | MEMT-05 | unit | `dotnet test --filter "MemoryOperationPayload"` | ❌ W0 | ⬜ pending |
| TBD | 03 | 2 | MEMS-01 | unit | `dotnet test --filter "Sedimentation"` | ❌ W0 | ⬜ pending |
| TBD | 03 | 2 | MEMS-02 | unit | `dotnet test --filter "Sedimentation"` | ❌ W0 | ⬜ pending |
| TBD | 03 | 2 | MEMS-03 | unit | `dotnet test --filter "Sedimentation"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Tools/MemoryCreateToolTests.cs` — stubs for MEMT-01
- [ ] `tests/OpenAnima.Tests/Tools/MemoryUpdateToolTests.cs` — stubs for MEMT-02
- [ ] `tests/OpenAnima.Tests/Tools/MemoryDeleteToolTests.cs` — stubs for MEMT-03
- [ ] `tests/OpenAnima.Tests/Tools/MemoryListToolTests.cs` — stubs for MEMT-04
- [ ] `tests/OpenAnima.Tests/Events/MemoryOperationPayloadTests.cs` — stubs for MEMT-05
- [ ] `tests/OpenAnima.Tests/Memory/SedimentationServiceTests.cs` — stubs for MEMS-01, MEMS-02, MEMS-03

*Existing infrastructure covers test framework. Only test files need creation.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Node appears on /memory page after create | MEMT-01 | Requires browser rendering | Create node via tool, navigate to /memory, verify card appears |
| Updated content shows on /memory page | MEMT-02 | Requires browser rendering | Update node, refresh /memory, verify content changed |
| Deprecated node hidden from recall | MEMT-03 | Recall context depends on LLM | Soft-delete node, trigger recall, verify node excluded |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
