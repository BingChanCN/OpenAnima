---
phase: 60
slug: hardening-and-memory-integration
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-23
---

# Phase 60 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 |
| **Config file** | none — standard `dotnet test` discovery |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModuleAgentLoopHardeningTests" -x` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~AgentLoop" -x`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 60-01-01 | 01 | 0 | HARD-01, HARD-02, HARD-03 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModuleAgentLoopHardeningTests" -x` | ❌ W0 | ⬜ pending |
| 60-01-02 | 01 | 1 | HARD-03 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModuleAgentLoopHardeningTests" -x` | ❌ W0 | ⬜ pending |
| 60-01-03 | 01 | 1 | HARD-02 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModuleAgentLoopHardeningTests" -x` | ❌ W0 | ⬜ pending |
| 60-01-04 | 01 | 1 | HARD-01 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModuleAgentLoopHardeningTests" -x` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/LLMModuleAgentLoopHardeningTests.cs` — stubs for HARD-01, HARD-02, HARD-03
  - Spy `IStepRecorder` capturing RecordStepStart/Complete calls with moduleName and propagationId
  - Spy `ISedimentationService` capturing messages list (reuse `FakeSedimentationService` from `LLMModuleSedimentationTests.cs`)
  - `AgentConfigService` extended with `agentContextWindowSize` key

*Existing infrastructure covers fixtures: xUnit, NullLogger, FakeModuleContext, NullLLMProviderRegistry — all in TestHelpers/.*

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
