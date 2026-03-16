---
phase: 36-built-in-module-decoupling
verified: 2026-03-16T05:32:22Z
status: passed
score: 10/10 must-haves verified
re_verification: false
gaps: []
human_verification: []
---

# Phase 36: Built-in Module Decoupling Verification Report

**Phase Goal:** Decouple the 12 active built-in modules from Core-facing module APIs, keep only the documented `OpenAnima.Core.LLM` exception in `LLMModule`, and prove the migrated runtime with honest full-suite verification
**Verified:** 2026-03-16
**Status:** PASSED
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | The authoritative active built-in inventory is codified as exactly 12 module source files | VERIFIED | `tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs` hard-codes the 12 module files and verifies they exist |
| 2  | `FormatDetector.cs` and `ModuleMetadataRecord.cs` are present in the modules directory but excluded from the active inventory | VERIFIED | `HelperFiles_ArePresent_ButNotCountedAsActiveBuiltInModules` asserts both helper files exist and are absent from the hard-coded active list |
| 3  | Zero `using OpenAnima.Core.*` directives remain in the 11 non-`LLMModule` built-in module files | VERIFIED | `NonLlmBuiltInModules_HaveNoCoreUsings_AndLlmModuleHasOnlyTheDocumentedException` scans every active module file directly |
| 4  | `LLMModule` retains only the documented `using OpenAnima.Core.LLM;` exception | VERIFIED | The same source-audit test asserts the only Core using in `LLMModule.cs` is `OpenAnima.Core.LLM` |
| 5  | The real wiring DI container can resolve all 12 registered built-in modules without `InvalidOperationException` | VERIFIED | `BuiltInModules_AllResolveFromTheRealDIContainer` iterates the authoritative module type set in `ModuleRuntimeInitializationTests.cs` |
| 6  | Startup port registration covers the same authoritative 12-module set | VERIFIED | `WiringInitializationService_RegistersAllModulePorts` asserts the registered module-name set equals the 12 expected built-ins |
| 7  | Startup initialization still wires module subscriptions correctly after the migration | VERIFIED | `WiringInitializationService_InitializesModules_EventBusSubscriptionsActive` still proves `ChatOutputModule` receives published events after hosted-service startup |
| 8  | `oani new` generates Contracts-only module code using `ModuleMetadataRecord` directly | VERIFIED | `src/OpenAnima.Cli/Templates/module-cs.tmpl` constructs `ModuleMetadataRecord`, and `tests/OpenAnima.Cli.Tests/CliFoundationTests.cs` asserts no `OpenAnima.Core` reference appears in generated output |
| 9  | The full `OpenAnima.Tests` suite passes after all decoupling changes | VERIFIED | `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj -v minimal` passed `334/334` |
| 10 | The full `OpenAnima.Cli.Tests` suite passes with the repaired CLI baseline | VERIFIED | `dotnet test tests/OpenAnima.Cli.Tests/OpenAnima.Cli.Tests.csproj -v minimal` passed `76/76` |

**Score:** 10/10 truths verified

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs` | Hard-coded inventory and source-level decoupling audit | VERIFIED | Contains 12 authoritative module files, helper-file exclusions, and Core-using scan |
| `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs` | Startup/DI coverage for all built-in modules | VERIFIED | Registers required runtime services, verifies all built-ins resolve, and asserts startup port inventory |
| `src/OpenAnima.Core/Modules/LLMModule.cs` | Only documented Core-facing exception remains | VERIFIED | Uses Contracts metadata/config/context/routing surfaces and keeps only `using OpenAnima.Core.LLM;` |
| `src/OpenAnima.Cli/Templates/module-cs.tmpl` | Contracts-only generated module shape | VERIFIED | Generates direct `ModuleMetadataRecord` construction and no inline metadata helper class |
| `tests/OpenAnima.Cli.Tests/CliFoundationTests.cs` | CLI template/new-command assertions and stable console capture | VERIFIED | Confirms Contracts-only generated output and keeps the CLI project green |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs` | `src/OpenAnima.Core/Modules/*Module.cs` | direct source scan | WIRED | Regex-based audit checks each authoritative module file for forbidden Core imports |
| `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs` | `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` | authoritative module-type set | WIRED | The startup test fixture resolves the same 12 built-ins that `AddWiringServices` registers |
| `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs` | `src/OpenAnima.Core/Hosting/WiringInitializationService.cs` | hosted-service startup assertions | WIRED | Port registration and module initialization tests execute the real hosted service |
| `src/OpenAnima.Cli/Templates/module-cs.tmpl` | `src/OpenAnima.Contracts/ModuleMetadataRecord.cs` | generated metadata construction | WIRED | Generated modules instantiate `ModuleMetadataRecord` directly from Contracts |

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| DECPL-01 | 36-02/03/04/05 | The 12 active built-in modules consume Contracts-first APIs, with only the documented `LLMModule` exception | SATISFIED | Source audit test proves 11 non-LLM modules have zero Core usings and `LLMModule` keeps only `OpenAnima.Core.LLM` |
| DECPL-02 | 36-05 | DI resolution succeeds for all 12 active module types after decoupling | SATISFIED | `BuiltInModules_AllResolveFromTheRealDIContainer` resolves every built-in from the startup container |
| DECPL-03 | 36-04/05 | All existing tests compile and pass after module migration | SATISFIED | Full regression passed sequentially: `334/334` in `OpenAnima.Tests`, `76/76` in `OpenAnima.Cli.Tests` |
| DECPL-04 | 36-04 | `oani new` template generates Contracts-only module code | SATISFIED | Template uses `ModuleMetadataRecord`; CLI tests assert no `OpenAnima.Core` reference in generated `.cs` or `.csproj` output |
| DECPL-05 | 36-01 | `ModuleMetadataRecord` moved to Contracts | SATISFIED | `src/OpenAnima.Contracts/ModuleMetadataRecord.cs` exists, built-ins/templates construct the Contracts record explicitly, and prior Phase 36 tests remained green |

All 5 decoupling requirements (DECPL-01 through DECPL-05) are accounted for. No gaps were found.

---

### Anti-Patterns Found

None. Verification found no remaining blockers or accepted regressions. The only retained Core-facing surface is the explicitly documented `OpenAnima.Core.LLM` exception in `LLMModule`.

---

### Human Verification Required

None. The phase goal is fully verifiable by source inspection plus automated regression coverage:
- Source-level decoupling is checked directly against the authoritative module files
- Startup/DI behavior is proven by integration tests against the real hosted-service path
- Template behavior is proven by CLI tests
- Regression safety is proven by full sequential suite runs

---

### Summary

Phase 36 goal is fully achieved. The repository now has an automated proof that the active built-in inventory is exactly 12 modules, helper files are excluded from that count, all non-LLM built-ins are free of Core-facing module imports, and `LLMModule` keeps only the narrow `OpenAnima.Core.LLM` exception that was explicitly allowed for v1.7.

The runtime proof is also in place: every built-in module registered by `AddWiringServices` resolves from the real startup container, startup port registration matches the authoritative inventory, and the full regression suite passes with no carried-forward CLI failures. Across both test projects, `410/410` tests passed.

---

_Verified: 2026-03-16_
_Verifier: Codex_
