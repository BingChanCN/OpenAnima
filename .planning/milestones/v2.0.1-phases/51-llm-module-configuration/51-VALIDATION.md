---
phase: 51
slug: llm-module-configuration
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-22
---

# Phase 51 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 / net10.0 |
| **Config file** | none — convention-based discovery |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModule" -q` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/ -q` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModule" -q`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/ -q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 51-01-01 | 01 | 0 | LLMN-01..05 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModuleProvider" -q` | ❌ W0 | ⬜ pending |
| 51-02-01 | 02 | 1 | LLMN-01 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModule" -q` | ❌ W0 | ⬜ pending |
| 51-03-01 | 03 | 1 | LLMN-05 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModule" -q` | ❌ W0 | ⬜ pending |
| 51-04-01 | 04 | 2 | LLMN-01..04 | unit | `dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~LLMModule" -q` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/LLMModuleProviderConfigTests.cs` — stubs for LLMN-01 through LLMN-05
- [ ] `FakeLLMProviderRegistry` test double implementing `ILLMProviderRegistry` — inline in test file or shared fixture
- [ ] DI registration verification: confirm `ILLMProviderRegistry` is bound in startup; if not, add alias

*Existing infrastructure covers test framework and config service mocks.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Cascading dropdown renders correctly in sidebar | LLMN-01, LLMN-02 | Blazor UI rendering requires browser | Open editor sidebar for LLMModule, verify provider dropdown populates, select provider, verify model dropdown shows scoped models |
| Disabled provider greyed with "(已禁用)" suffix | LLMN-03 | Visual rendering | Disable a provider in Settings, open LLMModule sidebar, verify greyed option |
| Manual mode toggle hides/shows fields | LLMN-04 | Visual rendering | Select "手动配置", verify apiUrl/apiKey/modelName fields appear; select a provider, verify manual fields hidden |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
