---
phase: 40
slug: module-storage-path
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-18
---

# Phase 40 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing) |
| **Config file** | `tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/ --filter "Category=ModuleStorage" --no-build -x` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/ --no-build` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/ --filter "Category=ModuleStorage" --no-build -x`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/ --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 40-01-01 | 01 | 1 | STOR-01 | unit | `dotnet test --filter "FullyQualifiedName~ModuleStorageServiceTests" --no-build -x` | ❌ W0 | ⬜ pending |
| 40-01-02 | 01 | 1 | STOR-01 | unit | same | ❌ W0 | ⬜ pending |
| 40-01-03 | 01 | 1 | STOR-01 | unit | same | ❌ W0 | ⬜ pending |
| 40-01-04 | 01 | 1 | STOR-01 | unit | same | ❌ W0 | ⬜ pending |
| 40-01-05 | 01 | 1 | STOR-01 | integration | `dotnet test --filter "FullyQualifiedName~PluginLoaderStorageTests" --no-build -x` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/ModuleStorageServiceTests.cs` — stubs for STOR-01 unit behaviors
- [ ] `tests/OpenAnima.Tests/Integration/PluginLoaderStorageTests.cs` — stubs for STOR-01 PluginLoader injection
- [ ] Add `IModuleStorage` DI resolution test to existing `ContractsApiTests.cs`

*Existing xUnit infrastructure covers framework needs — no new packages required.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Deleting Anima removes module-data dir | STOR-01 #4 | Requires Anima lifecycle + filesystem state | 1. Create Anima, call GetDataDirectory, verify dir exists. 2. Delete Anima. 3. Verify module-data dir removed. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
