---
phase: 48
slug: artifact-memory-foundation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-21
---

# Phase 48 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 |
| **Config file** | none — discovered by convention |
| **Quick run command** | `dotnet test --filter "ArtifactStore\|MemoryGraph\|MemoryModule\|DisclosureMatcher" --no-build` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/ --no-build` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "ArtifactStore\|MemoryGraph\|MemoryModule\|DisclosureMatcher" --no-build`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/ --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 48-01-01 | 01 | 1 | ART-01 | unit | `dotnet test --filter ArtifactStoreTests --no-build` | ❌ W0 | ⬜ pending |
| 48-01-02 | 01 | 1 | ART-01 | unit | `dotnet test --filter StepRecorderArtifactTests --no-build` | ❌ W0 | ⬜ pending |
| 48-01-03 | 01 | 1 | ART-02 | unit | `dotnet test --filter ArtifactStoreTests --no-build` | ❌ W0 | ⬜ pending |
| 48-02-01 | 02 | 2 | MEM-01 | unit | `dotnet test --filter MemoryGraphTests --no-build` | ❌ W0 | ⬜ pending |
| 48-02-02 | 02 | 2 | MEM-01 | unit | `dotnet test --filter MemoryGraphTests --no-build` | ❌ W0 | ⬜ pending |
| 48-02-03 | 02 | 2 | MEM-02 | unit | `dotnet test --filter MemoryModuleTests --no-build` | ❌ W0 | ⬜ pending |
| 48-02-04 | 02 | 2 | MEM-03 | unit | `dotnet test --filter MemoryGraphTests --no-build` | ❌ W0 | ⬜ pending |
| 48-02-05 | 02 | 2 | MEM-03 | unit | `dotnet test --filter DisclosureMatcherTests --no-build` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/ArtifactStoreTests.cs` — stubs for ART-01, ART-02
- [ ] `tests/OpenAnima.Tests/Unit/MemoryGraphTests.cs` — stubs for MEM-01, MEM-03
- [ ] `tests/OpenAnima.Tests/Unit/DisclosureMatcherTests.cs` — stubs for MEM-03 disclosure scan
- [ ] `tests/OpenAnima.Tests/Unit/MemoryModuleTests.cs` — stubs for MEM-02

All test files follow the `RunRepositoryTests` in-memory SQLite pattern: unique DB name per class, keepalive connection, `isRaw: true` factory, `EnsureCreatedAsync()` in constructor.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Artifact inline preview renders correctly by MIME type | ART-02 | Blazor rendering requires browser | Open run inspector, expand step with artifact, verify markdown/JSON/text renders with correct formatting |
| Memory provenance links navigate to source step | MEM-02 | Navigation requires browser interaction | Click provenance link on injected memory step, verify it scrolls to source step |
| Memory page URI tree displays correct hierarchy | MEM-03 | Visual layout verification | Navigate to /memory, verify tree shows core://, run://, project:// prefixes with correct nesting |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
