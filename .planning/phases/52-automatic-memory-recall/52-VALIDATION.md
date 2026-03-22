---
phase: 52
slug: automatic-memory-recall
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-22
---

# Phase 52 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (.NET 10.0) |
| **Config file** | `tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~MemoryRecall OR FullyQualifiedName~BootMemory"` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~MemoryRecall OR FullyQualifiedName~BootMemory"`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 52-01-01 | 01 | 0 | MEMR-02, MEMR-03, MEMR-04, MEMR-05 | unit | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~MemoryRecallServiceTests"` | ❌ W0 | ⬜ pending |
| 52-01-02 | 01 | 0 | MEMR-01 | unit | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~BootMemoryInjectorWiringTests"` | ❌ W0 | ⬜ pending |
| 52-01-03 | 01 | 0 | MEMR-02, MEMR-03, MEMR-05 | unit | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~LLMModuleMemoryTests"` | ❌ W0 | ⬜ pending |
| 52-02-01 | 02 | 1 | MEMR-02, MEMR-03, MEMR-04, MEMR-05 | unit | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~MemoryRecallService"` | ❌ W0 | ⬜ pending |
| 52-03-01 | 03 | 2 | MEMR-01 | unit | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~BootMemory"` | ❌ W0 | ⬜ pending |
| 52-04-01 | 04 | 2 | MEMR-02, MEMR-03, MEMR-05 | unit | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~LLMModuleMemory"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/MemoryRecallServiceTests.cs` — stubs for MEMR-02, MEMR-03, MEMR-04, MEMR-05
- [ ] `tests/OpenAnima.Tests/Unit/BootMemoryInjectorWiringTests.cs` — stubs for MEMR-01
- [ ] `tests/OpenAnima.Tests/Unit/LLMModuleMemoryTests.cs` — stubs for MEMR-02, MEMR-03, MEMR-05

*Existing tests `DisclosureMatcherTests.cs` and `MemoryGraphTests.cs` cover primitives — no changes needed.*

---

## Manual-Only Verifications

All phase behaviors have automated verification.

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
