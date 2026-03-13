---
phase: 30
slug: prompt-injection-and-format-detection
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-13
---

# Phase 30 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 |
| **Config file** | none (implicit discovery) |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/ --filter "FormatDetector\|PromptInjection" --no-build` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/ --no-build` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/ --filter "FormatDetector|PromptInjection" --no-build`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/ --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 30-01-01 | 01 | 1 | FMTD-01 | unit | `dotnet test --filter "FormatDetector" --no-build` | ❌ W0 | ⬜ pending |
| 30-01-02 | 01 | 1 | FMTD-02 | unit | `dotnet test --filter "FormatDetector" --no-build` | ❌ W0 | ⬜ pending |
| 30-01-03 | 01 | 1 | FMTD-04 | unit | `dotnet test --filter "FormatDetector" --no-build` | ❌ W0 | ⬜ pending |
| 30-02-01 | 02 | 1 | PROMPT-01 | unit | `dotnet test --filter "PromptInjection" --no-build` | ❌ W0 | ⬜ pending |
| 30-02-02 | 02 | 1 | PROMPT-03 | unit | `dotnet test --filter "PromptInjection" --no-build` | ❌ W0 | ⬜ pending |
| 30-02-03 | 02 | 1 | PROMPT-04 | unit | `dotnet test --filter "PromptInjection" --no-build` | ❌ W0 | ⬜ pending |
| 30-03-01 | 03 | 2 | FMTD-03 | integration | `dotnet test --filter "Category=Routing" --no-build` | ❌ W0 | ⬜ pending |
| 30-03-02 | 03 | 2 | PROMPT-02 | unit | `dotnet test --filter "PromptInjection" --no-build` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Modules/FormatDetectorTests.cs` — stubs for FMTD-01, FMTD-02, FMTD-04
- [ ] `tests/OpenAnima.Tests/Integration/PromptInjectionIntegrationTests.cs` — stubs for PROMPT-01, PROMPT-03, PROMPT-04, FMTD-03

*Existing infrastructure covers test framework — no new package installs needed.*

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
