# Phase 36: Built-in Module Decoupling - Research

**Researched:** 2026-03-15
**Domain:** .NET module contract migration, built-in module inventory normalization, shared helper relocation
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Migration Object List**
- Phase 36 is still scoped and evaluated against **14 built-in modules**, not auto-reduced to the currently visible set of 12.
- Research/planning must explicitly audit the historical and current built-in module list, identify the missing 2 modules, and either:
  - migrate all 14, or
  - prove the current real inventory is 12 and correct the project documentation in the same phase.
- If the audit concludes the real active built-in inventory is 12, Phase 36 should **update the docs** to replace the stale "14 built-in modules" wording.
- "Module code" includes not just `*Module.cs`, but also module-owned metadata and helper types that affect the module's outward shape.
- Historical built-in candidates should be inventoried and dispositioned clearly; do not leave the 14-vs-12 discrepancy implicit.

**Contracts Purity Rule**
- The target is **Contracts-first with a very small explicit exception list**, not absolute purity at any cost.
- `LLMModule` may remain an explicit, documented exception in Phase 36; do not block the phase on moving all LLM-facing APIs to Contracts.
- `HttpRequestModule` must be decoupled in Phase 36. If it still needs Core-only helpers, add the **minimum Contracts abstraction** required by the module's real usage.
- When expanding Contracts, only add types/interfaces that built-in modules already concretely need. Do not pre-design future-facing APIs.
- Any exception that remains after the migration should be deliberate and documented, not an accidental leftover.

**Metadata and Module Outward Shape**
- `ModuleMetadataRecord` should move to `OpenAnima.Contracts` in Phase 36.
- Built-in modules should keep using a common metadata pattern after the move; avoid splitting into many ad hoc per-module metadata implementations unless forced.
- Simple modules should move toward an external-module-like shape where practical.
- More complex modules may retain some internal platform-specific structure, but should still consume module-facing contracts from `OpenAnima.Contracts`.
- If a module currently depends on Core helper types, prefer moving the genuinely module-facing pieces into Contracts instead of rewriting modules around awkward platform-only workarounds.
- Across the 14-module migration, consistency of migration style is preferred over one-off patterns.

**`oani new` Template Direction**
- `oani new` is part of Phase 36 scope and must end up **Contracts-only**.
- The template should go beyond a bare compile-only stub and show the modern module shape for config/context/routing usage where appropriate.
- Even with richer capability, the generated code should still feel **minimal and clean**, not bulky or over-explained.
- If the main built-in migration proves more complex than expected, planner may split template work into its own small plan or closing plan within Phase 36 rather than forcing it into the first migration batch.
- Example depth is secondary to getting the Contracts-only structure right; richer template behavior can be scaled to fit the phase risk budget.

### Claude's Discretion
- Exact sequencing of the 14-module audit versus the first migration batch
- Whether the phase is best planned as one inventory plan plus multiple migration plans, or another equivalent breakdown
- The smallest acceptable Contracts abstraction for `HttpRequestModule`
- Which modules count as "simple" versus "complex" during migration ordering
- How much richer behavior `oani new` should demonstrate once the Contracts-only structure is in place

### Deferred Ideas (OUT OF SCOPE)
None - discussion stayed within phase scope.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| DECPL-01 | All 14 built-in modules depend only on OpenAnima.Contracts — zero `using OpenAnima.Core.*` in module files | Audit must first prove whether the real active set is 12; then migrate every live module file and update stale docs/goal wording in the same phase if 14 is wrong |
| DECPL-02 | DI resolution succeeds for all 14 module types after decoupling | Extend startup/DI tests to resolve every registered built-in module type from the real container, not just a subset |
| DECPL-03 | All existing tests compile and pass after module migration | Use focused migration tests during each plan and a final full-suite run; `OpenAnima.Tests` is currently green, but the CLI suite has a known failing baseline that must be repaired inside Phase 36 before this requirement can close |
| DECPL-04 | `oani new` project template generates Contracts-only module code | Update CLI templates and template-engine/new-command tests so generated code and project references stay Contracts-only |
| DECPL-05 | ModuleMetadataRecord moved to Contracts so decoupled modules can reference it | Promote `ModuleMetadataRecord` from Core.Modules to Contracts and update module/test/template consumers |
</phase_requirements>

---

## Summary

Phase 36 is not a pure mechanical `using` swap. The codebase currently has **12 live built-in module types**, while several project docs say **14 built-in modules**. The strongest evidence is in the real registration points: `WiringServiceExtensions` and `WiringInitializationService` each register exactly the same 12 module types. The `src/OpenAnima.Core/Modules/` directory contains 14 `.cs` files only because two of them are helpers (`FormatDetector.cs` and `ModuleMetadataRecord.cs`), which likely contaminated prior counting. A second source of confusion is historical planning: v1.5 requirements also listed future built-ins `BUILTIN-11` and `BUILTIN-12` that were never implemented.

The good news is that most modules are already close to Contracts-only after Phase 35. Nine modules still import Core only for `IAnimaContext` and `IAnimaModuleConfigService`, and the routing trio still imports `Core.Routing` even though Contracts routing types already exist. Those are straightforward migrations once the plan accounts for bulk-config initialization moving to per-key `IModuleConfig.SetConfigAsync(...)`. `ModuleMetadataRecord` is the shared outward-shape blocker for nearly every built-in module and should move first.

The non-mechanical seams are smaller than they look. `ChatInputModule` still references `ActivityChannelHost`, but the setter is dead code: there is no `SetChannelHost(...)` call site anywhere in the repo. The smallest correct move is to delete that unused branch and keep the existing direct `EventBus.PublishAsync(...)` path. `HttpRequestModule` still calls `SsrfGuard.IsBlocked(...)`; the lowest-churn solution is to move `SsrfGuard` itself into Contracts (or an equivalent minimal Contracts wrapper) rather than inventing a broad new platform API. `LLMModule` can switch config/context/router imports to Contracts now, but should remain the explicit documented exception for `ILLMService` and LLM DTOs unless the phase intentionally expands scope.

**Primary recommendation:** Plan Phase 36 as an inventory-and-shared-contracts foundation plan followed by two or three migration cohorts and a final verification/template plan. Normalize the active inventory to 12 if the audit confirms it, move `ModuleMetadataRecord` first, remove dead `ChatInputModule` channel plumbing, move `SsrfGuard` with minimal surface area, and document `LLMModule` as the single intentional Core-dependent exception if full LLM promotion stays out of scope.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET / C# | net8.0 | Runtime and module target | Project standard across Contracts, Core, CLI, and tests |
| xUnit | 2.9.3 | Regression and startup smoke tests | Existing project test framework; runtime tests are currently green, while CLI tests need a planned baseline repair before final full-suite proof |
| Microsoft.Extensions.DependencyInjection | project standard | Real DI startup validation | Existing container used by runtime and integration tests |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| OpenAI SDK | existing project dependency | `LLMModule` exception area only | Keep untouched unless phase explicitly expands beyond the allowed exception |
| dotnet CLI | runtime | Full-suite and targeted verification | Existing project verification path |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Normalize inventory to 12 and fix docs | Pretend 14 still exists | Hides a proven discrepancy and makes requirement verification dishonest |
| Move `SsrfGuard` to Contracts | Add a new DI-heavy URL validator service | More churn and more API design than the current module usage requires |
| Delete dead `ChatInputModule` channel hook | Add a new chat-dispatch Contracts interface | Adds surface area for runtime plumbing that currently has no call site |

---

## Architecture Patterns

### Current Active Built-in Inventory

The real live set comes from the two registration points below and is currently 12 modules:

1. `LLMModule`
2. `ChatInputModule`
3. `ChatOutputModule`
4. `HeartbeatModule`
5. `FixedTextModule`
6. `TextJoinModule`
7. `TextSplitModule`
8. `ConditionalBranchModule`
9. `AnimaInputPortModule`
10. `AnimaOutputPortModule`
11. `AnimaRouteModule`
12. `HttpRequestModule`

Evidence:
- `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs`
- `src/OpenAnima.Core/Hosting/WiringInitializationService.cs`

The two extra `.cs` files in `src/OpenAnima.Core/Modules/` are helper types, not module classes:
- `FormatDetector.cs`
- `ModuleMetadataRecord.cs`

Historical candidates that explain stale docs:
- Future v1.5 requirements `BUILTIN-11` and `BUILTIN-12` (`Loop control`, `Variable storage`) were planned but never shipped.
- Older demo modules (`TextInput`, `LLMProcessor`, `TextOutput`, `TriggerButton`) were removed long ago and are explicitly asserted absent by tests.

### Recommended Project Structure After Phase 36

```text
src/OpenAnima.Contracts/
├── IModule.cs
├── IModuleMetadata.cs
├── ModuleMetadataRecord.cs          # MOVED from Core.Modules
├── IModuleConfig.cs
├── IModuleContext.cs
├── Ports/
├── Routing/
└── Http/
    └── SsrfGuard.cs                 # MOVED if chosen as the minimal HTTP seam

src/OpenAnima.Core/
├── Modules/
│   ├── 12 built-in module classes   # All import Contracts-first namespaces
│   ├── FormatDetector.cs            # Stays local helper for LLMModule
│   └── (no ModuleMetadataRecord.cs)
├── Http/
│   └── (empty or shim wrapper if keeping back-compat alias)
└── ...
```

### Pattern 1: Inventory Normalization Before Migration

**What:** Start the phase by codifying the real active inventory and fixing stale roadmap/requirements/project wording if the audit confirms 12 active modules.

**Why:** The plan checker will read the roadmap literally. If the plan silently migrates 12 files while the roadmap still demands 14 modules with zero exceptions, verification will fail for the wrong reason.

**How to prove it:**
- Registration count in `WiringServiceExtensions`
- Registration count in `WiringInitializationService`
- `find src/OpenAnima.Core/Modules -name '*.cs'` to show helper-file contamination
- Existing tests proving demo modules are gone

### Pattern 2: Move `ModuleMetadataRecord` First

**What:** Promote `ModuleMetadataRecord` from `OpenAnima.Core.Modules` into `OpenAnima.Contracts`.

**Why:** Nearly every built-in module references it, and the user explicitly wants to preserve the common metadata pattern rather than replace it with one-off classes. This is the cleanest way to make simple modules look like real external examples.

**Recommended shape:**
```csharp
namespace OpenAnima.Contracts;

/// <summary>
/// Simple record implementation of IModuleMetadata for concrete modules.
/// </summary>
public record ModuleMetadataRecord(string Name, string Version, string Description) : IModuleMetadata;
```

**Follow-through required:**
- Update built-in modules
- Update tests that new up `ModuleMetadataRecord`
- Update CLI template/examples if they should demonstrate the shared pattern

### Pattern 3: Mechanical Config/Context/Router Swap

**What:** For the majority cohort, replace:
- `IAnimaModuleConfigService` -> `IModuleConfig`
- `IAnimaContext` -> `IModuleContext`
- `OpenAnima.Core.Routing` -> `OpenAnima.Contracts.Routing`

**Why:** Phase 35 already put the needed Contracts APIs in place. The remaining work is mostly constructor/import cleanup and converting bulk default config seeding to per-key writes.

**Recommended default-config pattern:**
```csharp
if (existing.Count == 0)
{
    await _config.SetConfigAsync(animaId, Metadata.Name, "key1", value1);
    await _config.SetConfigAsync(animaId, Metadata.Name, "key2", value2);
}
```

Do not keep modules on `IAnimaModuleConfigService` just for the bulk overload. Loop over explicit keys instead.

### Pattern 4: Remove Dead `ChatInputModule` Channel Plumbing

**What:** Delete `ActivityChannelHost` storage, `SetChannelHost(...)`, and the dead branch in `SendMessageAsync(...)`.

**Why:** There is no call site for `ChatInputModule.SetChannelHost(...)`. Keeping a Core-only import for dead runtime plumbing is unnecessary complexity, and adding a new Contracts abstraction would be strictly worse than removing unused code.

**Result:** `ChatInputModule` becomes a clean Contracts-only source module that simply publishes to the event bus.

### Pattern 5: Keep the HTTP Seam Minimal

**What:** Promote `SsrfGuard` itself into Contracts (preferred lowest-churn option) or add a one-method equivalent wrapper if planner sees a strong reason.

**Why:** `HttpRequestModule` only needs one module-facing safety operation: `IsBlocked(string url, out string reason)`. This is small, BCL-only, and already self-contained. A broader platform API is unnecessary.

**Recommended move:**
- `src/OpenAnima.Core/Http/SsrfGuard.cs` -> `src/OpenAnima.Contracts/Http/SsrfGuard.cs`
- Update `HttpRequestModule` and `SsrfGuardTests`
- Only keep a Core shim if another caller still needs the old namespace

### Pattern 6: LLM as the Explicit Exception

**What:** Convert `LLMModule` to Contracts for config/context/router imports, but leave `ILLMService`, `ChatMessageInput`, and `LLMResult` in Core for this phase.

**Why:** The user explicitly allowed a narrow exception. A full LLM surface move would ripple into `LLMService`, `ChatContextManager`, Blazor chat components, and multiple test fakes. That is a different scope than built-in module decoupling.

**Required companion work:**
- Update phase docs/verification language so the exception is deliberate
- Do not let the roadmap continue to promise zero Core imports in `LLMModule` if that is no longer true for Phase 36

### Pattern 7: Verify Through Real Startup, Not Only Grep

**What:** Pair source-level purity checks with runtime DI/startup tests.

**Why:** DECPL-02 is not satisfied by compile success alone. Built-in modules are all resolved from DI on app startup, so verification must resolve the real module set from the real container.

**Best existing anchors:**
- `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs`
- `tests/OpenAnima.Tests/Integration/WiringDIIntegrationTests.cs`
- `tests/OpenAnima.Cli.Tests/CliFoundationTests.cs`

---

## Don't Hand-Roll

- Do not replace `ModuleMetadataRecord` with twelve custom metadata classes just to avoid moving one small record.
- Do not duplicate SSRF logic inside `HttpRequestModule`; move the helper or wrap it once.
- Do not invent a broad activity-channel contract for `ChatInputModule` when the current hook is unused.
- Do not keep bulk `SetConfigAsync(Dictionary<...>)` in modules via Core shim types just for convenience.
- Do not silently preserve the stale "14 built-in modules" wording if the audit proves the active set is 12.

---

## Common Pitfalls

### Pitfall 1: Counting Files Instead of Module Types

`src/OpenAnima.Core/Modules/` has 14 `.cs` files, but only 12 actual module classes. `FormatDetector` and `ModuleMetadataRecord` are helpers. If the plan fails to separate helper files from module types, DECPL-01 and the roadmap wording will stay inconsistent.

### Pitfall 2: Missing the Historical Reason for "14"

Two different historical artifacts can mislead the phase:
- v1.5 future requirements `BUILTIN-11` and `BUILTIN-12`
- old helper/demo-file counting

The plan should record the disposition explicitly so the project does not reintroduce the same ambiguity later.

### Pitfall 3: Modules Sticking to Core for Bulk Config Writes

Once a module switches to `IModuleConfig`, any default seeding logic that relied on `SetConfigAsync(Dictionary<string,string>)` must become explicit per-key writes. This affects the routing trio and `HttpRequestModule`.

### Pitfall 4: Preserving Dead `ChatInputModule` Plumbing

`ChatInputModule` still imports `ActivityChannelHost`, but no runtime code calls `SetChannelHost(...)`. Carrying that dependency into the phase plan makes the migration look harder than it is.

### Pitfall 5: LLM Purity Expanding Into a Different Phase

Trying to move `ILLMService` and its DTOs into Contracts in the same phase would drag in non-module consumers: `LLMService`, `ChatContextManager`, `ChatPanel`, and multiple test fakes. That is larger than the user-approved exception budget.

### Pitfall 6: Verification That Only Checks Grep

`rg "using OpenAnima.Core"` on module files is necessary, but DECPL-02 also requires the application to start without DI failures. Extend real startup tests rather than relying on compile output alone.

### Pitfall 7: Forgetting CLI Template Parity

The template is already Contracts-only at the project-reference level, but its generated C# still uses an inline metadata class and does not demonstrate the modern config/context/routing shape. Phase 36 needs an explicit template decision, not a vague assumption that it is "already done."

---

## Code Examples

### Real 12-Module Registration Snapshot

From the current live registration points:

```csharp
services.AddSingleton<LLMModule>();
services.AddSingleton<ChatInputModule>();
services.AddSingleton<ChatOutputModule>();
services.AddSingleton<HeartbeatModule>();
services.AddSingleton<FixedTextModule>();
services.AddSingleton<TextJoinModule>();
services.AddSingleton<TextSplitModule>();
services.AddSingleton<ConditionalBranchModule>();
services.AddSingleton<AnimaInputPortModule>();
services.AddSingleton<AnimaOutputPortModule>();
services.AddSingleton<AnimaRouteModule>();
services.AddSingleton<HttpRequestModule>();
```

### Recommended Per-Key Default Seeding

```csharp
var existing = _config.GetConfig(animaId, Metadata.Name);
if (existing.Count == 0)
{
    await _config.SetConfigAsync(animaId, Metadata.Name, "serviceName", "");
    await _config.SetConfigAsync(animaId, Metadata.Name, "serviceDescription", "");
    await _config.SetConfigAsync(animaId, Metadata.Name, "inputFormatHint", "");
}
```

### Contracts Metadata Record

```csharp
using OpenAnima.Contracts;

public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
    "TextSplitModule",
    "1.0.0",
    "Splits text by delimiter into JSON array");
```

### Inventory / Purity Verification Commands

```bash
rg -n 'AddSingleton<.*Module>' src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs
rg -n 'typeof\\(.*Module\\)' src/OpenAnima.Core/Hosting/WiringInitializationService.cs
rg -n '^using OpenAnima\\.Core\\.' src/OpenAnima.Core/Modules/*Module.cs
dotnet test /home/user/OpenAnima/tests/OpenAnima.Tests/ -q
dotnet test /home/user/OpenAnima/tests/OpenAnima.Cli.Tests/ -q
```

---

## State of the Art

- Phase 35 already established the Contracts interfaces the modules need: `IModuleConfig`, `IModuleContext`, `ICrossAnimaRouter`, and routing companion types.
- `ModuleRuntimeInitializationTests` already exercises real startup behavior and is the natural place to codify the active built-in inventory and DI resolution expectations.
- `CanaryModuleTests` already proves a Contracts-only external module can receive config/context/router services from DI.
- `OpenAnima.Tests` is currently green at `326/326`.
- `OpenAnima.Cli.Tests` is not fully green today: the live baseline is `67 passed / 7 failed`, and all failures sit in `CliFoundationTests.cs`-hosted CLI coverage (`Program_InvalidVerbosity_ReturnsError`, `PackCommand_HelpOutput_ContainsPack`, `ValidateCommand_InvalidJson_ReturnsValidationError`, `ValidateCommand_NonExistentPath_ReturnsValidationError`, `NewCommand_GeneratedFilesContainExpectedContent`, `Pack_EmbeddedManifestContainsChecksum`, `Pack_CreatedOamodIsValidZip_ContainsModuleJsonAndDll`).
- The repeated `ObjectDisposedException: Cannot write to a closed TextWriter` stack traces point to shared `Console.SetOut` / `Console.SetError` capture in the CLI tests as the main stability issue, so the template/CLI plan should repair that baseline directly instead of deferring it to the final verification wave.

---

## Open Questions

1. Should the doc correction normalize the phase to **12 active modules + 1 explicit LLM exception**, or keep the roadmap headline broader while clarifying the exception in success criteria? The user decision allows the exception; the plan must reflect that explicitly.
2. Is moving `SsrfGuard` into Contracts acceptable even though a more generic URL-validation abstraction is listed as future work? Based on current usage, this is the smallest honest move.
3. How much richer should `oani new` become in this phase? The template can remain minimal with the shared metadata record, or it can also demonstrate config/context/routing injection patterns in a lightweight example.

---

## Validation Architecture

### Test Framework

- Use existing xUnit projects: `tests/OpenAnima.Tests` and `tests/OpenAnima.Cli.Tests`
- Favor focused regression tests per migration cohort plus one final full-suite run
- Add one explicit inventory/startup smoke test so the 12-vs-14 decision is encoded in code, not only docs

### Phase Requirements -> Test Map

| Requirement | Verification Strategy |
|-------------|-----------------------|
| DECPL-01 | Module-file import grep + targeted unit/integration tests covering migrated modules + inventory assertion |
| DECPL-02 | Extend startup/DI tests to resolve all registered built-in module types with the real service container |
| DECPL-03 | Run full test suites for OpenAnima.Tests and OpenAnima.Cli.Tests after all migrations |
| DECPL-04 | TemplateEngine + NewCommand tests assert generated C# / csproj stay Contracts-only and match updated metadata pattern |
| DECPL-05 | Unit tests or reflection checks assert `ModuleMetadataRecord` lives in Contracts and module/test consumers compile against it |

### Sampling Rate

- After each migration cohort: run only the directly affected test slice
- Before phase completion: run full `dotnet test` on both test projects
- Because the CLI suite is not fully green today, the phase plan must explicitly repair the current 7-test CLI baseline failure set before DECPL-03 can be satisfied by a full-suite run.

### Wave 0 Gaps

- There is no current codified test that asserts the authoritative built-in inventory count
- There is no current codified test that every registered built-in module resolves from DI after startup
- CLI template tests assert Contracts project reference, but not yet the modern metadata/config/context/routing shape
- The full CLI suite is not clean before Phase 36 starts, so DECPL-03 needs an explicit baseline-repair plan rather than an assumed green suite

---

## Sources

### Primary (HIGH confidence)

- `.planning/phases/36-built-in-module-decoupling/36-CONTEXT.md`
- `.planning/ROADMAP.md`
- `.planning/REQUIREMENTS.md`
- `.planning/STATE.md`
- `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs`
- `src/OpenAnima.Core/Hosting/WiringInitializationService.cs`
- `src/OpenAnima.Core/Modules/*.cs`
- `src/OpenAnima.Cli/Templates/module-cs.tmpl`
- `src/OpenAnima.Cli/Templates/module-csproj.tmpl`
- `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs`
- `tests/OpenAnima.Tests/Integration/CanaryModuleTests.cs`
- `tests/OpenAnima.Tests/Unit/SsrfGuardTests.cs`
- `tests/OpenAnima.Cli.Tests/CliFoundationTests.cs`

### Secondary (MEDIUM confidence)

- `.planning/milestones/v1.5-REQUIREMENTS.md`
- `.planning/milestones/v1.5-phases/27-built-in-modules/27-RESEARCH.md`
- `.planning/research/ARCHITECTURE.md`
- Local test runs on 2026-03-15:
  - `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj -q` -> Passed: 326, Failed: 0
  - `dotnet test tests/OpenAnima.Cli.Tests/OpenAnima.Cli.Tests.csproj -q` -> Passed: 67, Failed: 7

---

## Metadata

- No web research was required; all findings came from the local repository and planning artifacts.
- Confidence is HIGH because the inventory, imports, and test baselines are directly observable in source.
