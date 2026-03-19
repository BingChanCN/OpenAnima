---
phase: 43
slug: heartbeat-refactor
status: complete
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-20
---

# Phase 43 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x (.NET) |
| **Config file** | tests/OpenAnima.Tests/OpenAnima.Tests.csproj |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests --filter "HeartbeatModule" --no-restore` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests --no-restore` |
| **Estimated runtime** | ~31 seconds (full), ~1 second (filtered) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "HeartbeatModule"`
- **After every plan wave:** Run `dotnet test` (full suite)
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 31 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 43-01-01 | 01 | 1 | BEAT-05 | unit | `dotnet test --filter "TickAsync_PublishesToTickPort"` | ✅ HeartbeatModuleTests.cs | ✅ green |
| 43-01-01 | 01 | 1 | BEAT-05 | unit | `dotnet test --filter "InitializeAsync_StartsTimerLoop"` | ✅ HeartbeatModuleTests.cs | ✅ green |
| 43-01-02 | 01 | 1 | BEAT-06 | unit | `dotnet test --filter "GetSchema_ReturnsIntervalMsField"` | ✅ HeartbeatModuleTests.cs | ✅ green |
| 43-01-02 | 01 | 1 | BEAT-06 | unit | `dotnet test --filter "ReadIntervalFromConfig_UsesConfigValue"` | ✅ HeartbeatModuleTests.cs | ✅ green |
| 43-01-02 | 01 | 1 | BEAT-06 | unit | `dotnet test --filter "ReadIntervalFromConfig_MinimumClampedTo50ms"` | ✅ HeartbeatModuleTests.cs | ✅ green |
| 43-02-01 | 02 | 2 | BEAT-05 | integration | `dotnet test --filter "HeartbeatModule_TickAsync_PublishesTriggerEvent"` | ✅ ModuleTests.cs | ✅ green |
| 43-02-02 | 02 | 2 | BEAT-05, BEAT-06 | regression | `dotnet test tests/OpenAnima.Tests --no-restore` | ✅ full suite | ✅ green (394) |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. xUnit framework was already in place with 389 baseline tests before Phase 43.

---

## Manual-Only Verifications

All phase behaviors have automated verification.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 31s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-03-20