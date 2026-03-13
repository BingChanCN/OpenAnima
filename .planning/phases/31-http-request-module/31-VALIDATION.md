---
phase: 31
slug: http-request-module
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-13
---

# Phase 31 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 |
| **Config file** | none (inferred from .csproj) |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/ --filter "Category=HttpRequest" -x` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/ -x` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/ --filter "Category=HttpRequest" -x`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/ -x`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 31-01-01 | 01 | 1 | HTTP-01 | unit | `dotnet test --filter "Category=HttpRequest" -x` | ❌ W0 | ⬜ pending |
| 31-01-02 | 01 | 1 | HTTP-02 | unit | `dotnet test --filter "Category=HttpRequest" -x` | ❌ W0 | ⬜ pending |
| 31-01-03 | 01 | 1 | HTTP-05 | unit | `dotnet test --filter "Category=HttpRequest" -x` | ❌ W0 | ⬜ pending |
| 31-01-04 | 01 | 1 | HTTP-03 | integration | `dotnet test --filter "Category=HttpRequest" -x` | ❌ W0 | ⬜ pending |
| 31-01-05 | 01 | 1 | HTTP-04 | integration | `dotnet test --filter "Category=HttpRequest" -x` | ❌ W0 | ⬜ pending |
| 31-02-01 | 02 | 1 | HTTP-01 | manual | n/a — visual check | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Modules/HttpRequestModuleTests.cs` — stubs for HTTP-01, HTTP-02, HTTP-03, HTTP-04 (unit tests with fake HttpMessageHandler)
- [ ] `tests/OpenAnima.Tests/Unit/SsrfGuardTests.cs` — stubs for HTTP-05 IP range blocking logic in isolation

*Existing xunit infrastructure covers framework needs — no install required.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Sidebar dropdown renders GET/POST/PUT/DELETE/PATCH | HTTP-01 | Blazor UI rendering | Add HttpRequest module, open config sidebar, verify method dropdown appears with all 5 options |
| Headers textarea renders and saves | HTTP-01 | Blazor UI rendering | Configure headers in sidebar, save, reopen, verify persistence |
| Body textarea renders and saves | HTTP-01 | Blazor UI rendering | Configure body in sidebar, save, reopen, verify persistence |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
