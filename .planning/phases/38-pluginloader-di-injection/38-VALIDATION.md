---
phase: 38
slug: pluginloader-di-injection
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-17
---

# Phase 38 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 |
| **Config file** | none — convention-based test discovery |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~PluginLoader" --no-build` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests --no-build` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests --filter "FullyQualifiedName~PluginLoader" --no-build`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 38-01-01 | 01 | 1 | PLUG-01 | integration | `dotnet test --filter "FullyQualifiedName~PluginLoaderDITests.ExternalModule_WithContractsServices_LoadsSuccessfully" --no-build` | ❌ W0 | ⬜ pending |
| 38-01-02 | 01 | 1 | PLUG-02 | integration | `dotnet test --filter "FullyQualifiedName~PluginLoaderDITests.ExternalModule_ReceivesILogger_ViaFactory" --no-build` | ❌ W0 | ⬜ pending |
| 38-01-03 | 01 | 1 | PLUG-03 | integration | `dotnet test --filter "FullyQualifiedName~PluginLoaderDITests.Module_OptionalParameter_LoadsWithNull" --no-build` | ❌ W0 | ⬜ pending |
| 38-01-04 | 01 | 1 | PLUG-03 | integration | `dotnet test --filter "FullyQualifiedName~PluginLoaderDITests.Module_RequiredParameter_FailsWithError" --no-build` | ❌ W0 | ⬜ pending |
| 38-01-05 | 01 | 1 | PLUG-01 | integration | `dotnet test --filter "FullyQualifiedName~ModuleTests" --no-build` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Integration/PluginLoaderDITests.cs` — stubs for PLUG-01, PLUG-02, PLUG-03
- [ ] Test helper: `CreateTestModuleWithConstructor(params Type[] parameterTypes)` — extends ModuleTestHarness for DI testing
- [ ] Mock IServiceProvider setup with Contracts services registered

*Existing infrastructure covers built-in module regression testing.*

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
