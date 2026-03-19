---
phase: 41
slug: external-context-module
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-18
---

# Phase 41 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 on net10.0 |
| **Config file** | none (default xunit discovery) |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/ --no-build -l "console;verbosity=minimal"` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/ -l "console;verbosity=minimal"` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/ --no-build -l "console;verbosity=minimal"`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/ -l "console;verbosity=minimal"`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 41-01-01 | 01 | 0 | ECTX-01 | integration | `dotnet test --filter "Category=ContextModule"` | ❌ W0 | ⬜ pending |
| 41-01-02 | 01 | 1 | ECTX-01 | integration | `dotnet test --filter "Category=ContextModule"` | ❌ W0 | ⬜ pending |
| 41-01-03 | 01 | 1 | ECTX-01 | integration | `dotnet test --filter "Category=ContextModule"` | ❌ W0 | ⬜ pending |
| 41-01-04 | 01 | 2 | ECTX-02 | integration | `dotnet test --filter "Category=ContextModule"` | ❌ W0 | ⬜ pending |
| 41-01-05 | 01 | 2 | ECTX-02 | integration | `dotnet test --filter "Category=ContextModule"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Integration/ContextModuleTests.cs` — stubs for ECTX-01, ECTX-02
- [ ] PluginLoader bound IModuleStorage injection fix — required code change before module can use storage

*Existing test infrastructure (374+ tests) covers framework setup.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Multi-turn conversation in UI | ECTX-01 | Requires running app + LLM API key | 1. Start app 2. Load ContextModule 3. Send messages 4. Verify history in chat |
| History survives restart | ECTX-02 | Requires app restart cycle | 1. Send messages 2. Stop app 3. Restart 4. Verify history restored |
| Anima isolation in UI | ECTX-01 | Requires switching Animas in running app | 1. Chat in Anima A 2. Switch to Anima B 3. Verify empty history 4. Switch back to A 5. Verify history intact |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
