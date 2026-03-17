---
phase: 39
slug: contracts-type-migration-structured-messages
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-17
---

# Phase 39 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 |
| **Config file** | none (standard xunit discovery) |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj --filter "FullyQualifiedName~ChatMessageInputContractsTests\|FullyQualifiedName~LLMModuleMessagesPortTests\|FullyQualifiedName~PromptInjectionIntegrationTests" --no-build` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run quick run command (phase-specific tests)
- **After every plan wave:** Run full suite command
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 39-01-01 | 01 | 1 | MSG-01 | unit (reflection) | `dotnet test --filter "FullyQualifiedName~ChatMessageInputContractsTests"` | ❌ W0 | ⬜ pending |
| 39-01-02 | 01 | 1 | MSG-01 | unit (compile) | `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj` | ❌ W0 | ⬜ pending |
| 39-02-01 | 02 | 1 | MSG-03 | unit | `dotnet test --filter "FullyQualifiedName~ChatMessageInputContractsTests"` | ❌ W0 | ⬜ pending |
| 39-02-02 | 02 | 1 | MSG-03 | unit | `dotnet test --filter "FullyQualifiedName~ChatMessageInputContractsTests"` | ❌ W0 | ⬜ pending |
| 39-03-01 | 03 | 2 | MSG-02 | integration | `dotnet test --filter "FullyQualifiedName~LLMModuleMessagesPortTests"` | ❌ W0 | ⬜ pending |
| 39-03-02 | 03 | 2 | MSG-02 | integration | `dotnet test --filter "FullyQualifiedName~LLMModuleMessagesPortTests"` | ❌ W0 | ⬜ pending |
| 39-03-03 | 03 | 2 | MSG-02 | integration | `dotnet test --filter "FullyQualifiedName~PromptInjectionIntegrationTests"` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/ChatMessageInputContractsTests.cs` — stubs for MSG-01 (namespace shape) + MSG-03 (serialization round-trips)
- [ ] `tests/OpenAnima.Tests/Integration/LLMModuleMessagesPortTests.cs` — stubs for MSG-02 (messages port behavior, priority rule)

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
