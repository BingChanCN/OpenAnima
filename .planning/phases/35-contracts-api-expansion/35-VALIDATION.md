---
phase: 35
slug: contracts-api-expansion
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-15
---

# Phase 35 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 |
| **Config file** | none — conventional discovery |
| **Quick run command** | `dotnet test tests/OpenAnima.Tests/ -q --filter "Category=Routing"` |
| **Full suite command** | `dotnet test tests/OpenAnima.Tests/ -q` |
| **Estimated runtime** | ~28 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/OpenAnima.Tests/ -q`
- **After every plan wave:** Run `dotnet test tests/OpenAnima.Tests/ -q` + `dotnet build src/OpenAnima.Contracts/OpenAnima.Contracts.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green + Contracts isolation build green
- **Max feedback latency:** 28 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 35-01-01 | 01 | 1 | API-01 | unit | `dotnet test tests/OpenAnima.Tests/ -q --filter "FullyQualifiedName~IModuleConfig"` | ❌ W0 | ⬜ pending |
| 35-01-02 | 01 | 1 | API-02 | unit | `dotnet test tests/OpenAnima.Tests/ -q --filter "FullyQualifiedName~IModuleContext"` | ❌ W0 | ⬜ pending |
| 35-01-03 | 01 | 1 | API-03 | unit/integration | `dotnet test tests/OpenAnima.Tests/ -q --filter "Category=Routing"` | ✅ | ⬜ pending |
| 35-01-04 | 01 | 1 | API-04 | unit | `dotnet build src/OpenAnima.Contracts/OpenAnima.Contracts.csproj` | ❌ W0 | ⬜ pending |
| 35-02-01 | 02 | 2 | API-05 | compile | `dotnet build tests/OpenAnima.Tests/` | ✅ | ⬜ pending |
| 35-02-02 | 02 | 2 | API-06 | integration | `dotnet test tests/OpenAnima.Tests/ -q --filter "FullyQualifiedName~Canary"` | ❌ W0 | ⬜ pending |
| 35-02-03 | 02 | 2 | API-07 | integration | Part of canary test | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/OpenAnima.Tests/Unit/ContractsApiTests.cs` — stubs for API-01, API-02, API-04: verifies new types are in correct namespaces and Contracts isolation build passes
- [ ] `tests/OpenAnima.Tests/Integration/CanaryModuleTests.cs` — stubs for API-06, API-07: PortModule round-trip as canary
- [ ] Update `tests/OpenAnima.Tests/TestHelpers/NullAnimaModuleConfigService.cs` — add per-key `SetConfigAsync` implementation
- [ ] Update private stub configs in: `CrossAnimaRoutingE2ETests.cs`, `PromptInjectionIntegrationTests.cs`, `RoutingModulesTests.cs`, `HttpRequestModuleTests.cs` — add per-key `SetConfigAsync` method to each `StubConfig` class

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| .oamod binary compat loading | API-05 | Requires pre-compiled .oamod artifact | Build PortModule, pack as .oamod, load in runtime, verify resolution |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 28s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
