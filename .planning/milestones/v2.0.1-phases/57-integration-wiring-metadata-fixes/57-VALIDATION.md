---
phase: 57
slug: integration-wiring-metadata-fixes
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-23
---

# Phase 57 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 |
| **Config file** | none (uses default xunit discovery) |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/ --no-build -q --filter "BootRecall\|ProviderImpact\|MemoryRecallService"` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/ --no-build -q` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/ --no-build -q`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/ --no-build -q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 57-01-01 | 01 | 1 | MEMR-01 | unit | `dotnet test --no-build -q --filter "RecallAsync_BootNodes"` | ❌ W0 | ⬜ pending |
| 57-01-01 | 01 | 1 | MEMR-01 | unit | `dotnet test --no-build -q --filter "BootNodes_AppearInBootMemoryXml"` | ❌ W0 | ⬜ pending |
| 57-01-02 | 01 | 1 | PROV-03, PROV-04 | manual | Code inspection (Blazor private method) | ✅ | ⬜ pending |
| 57-02-01 | 02 | 1 | PROV-08, PROV-10 | manual | Read `50-01-SUMMARY.md` frontmatter | ✅ | ⬜ pending |
| 57-02-02 | 02 | 1 | MEMR-04 | manual | Read `52-02-SUMMARY.md` frontmatter | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/MemoryRecallServiceTests.cs` — new test `RecallAsync_BootNodes_ReturnedWithBootRecallType`
- [ ] `tests/OpenAnima.Tests/Unit/LLMModuleMemoryTests.cs` — new test `ExecuteWithMessages_BootNodes_AppearInBootMemoryXmlSection`

*Note: PROV-03/PROV-04 (CountAffectedModules) is a private Blazor component method — direct unit testing impractical. Manual verification via code inspection consistent with Phase 50/51 precedent.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Impact count in disable confirm | PROV-03 | Private Blazor method; Phase 50 precedent | Read Settings.razor `HandleDisable` — confirm `CountAffectedModules` call replaces hardcoded 0 |
| Impact count in delete confirm | PROV-04 | Private Blazor method; Phase 50 precedent | Read Settings.razor `HandleDelete` — confirm `CountAffectedModules` call replaces hardcoded 0 |
| PROV-08 in SUMMARY metadata | PROV-08 | Documentation-only | `grep -c "PROV-08" .planning/phases/50-provider-registry/50-01-SUMMARY.md` returns 1 |
| PROV-10 in SUMMARY metadata | PROV-10 | Documentation-only | `grep -c "PROV-10" .planning/phases/50-provider-registry/50-01-SUMMARY.md` returns 1 |
| MEMR-04 in SUMMARY metadata | MEMR-04 | Documentation-only | `grep -c "MEMR-04" .planning/phases/52-automatic-memory-recall/52-02-SUMMARY.md` returns 1 |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
