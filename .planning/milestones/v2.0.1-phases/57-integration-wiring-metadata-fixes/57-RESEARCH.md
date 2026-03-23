# Phase 57: Integration Wiring & Metadata Fixes - Research

**Researched:** 2026-03-23
**Domain:** C# / .NET 10 — memory recall pipeline, provider impact counting, plan SUMMARY frontmatter
**Confidence:** HIGH

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| MEMR-01 | Developer-agent run startup injects core boot memory into the run timeline automatically | BootMemoryInjector is wired but MemoryRecallService never produces RecallType="Boot" nodes; need `IMemoryRecallService.RecallAsync` to query `core://` prefix and emit them |
| PROV-03 | User can disable a provider without silently breaking existing LLM node selections | Settings.razor `HandleDisable` passes hardcoded `0` to `_confirmMessage`; need real count from `IAnimaModuleConfigService` |
| PROV-04 | User can delete a provider only when its usage impact is surfaced clearly | Settings.razor `HandleDelete` passes hardcoded `0` to `_confirmMessage`; same fix path as PROV-03 |
| PROV-08 | API keys stored securely and excluded from logs | Code exists and passes VERIFICATION; gap is that no plan SUMMARY lists it in `requirements-completed` frontmatter |
| PROV-10 | Developer can query provider/model metadata via `ILLMProviderRegistry` | Code exists and passes VERIFICATION; gap is that no plan SUMMARY lists it in `requirements-completed` frontmatter |
| MEMR-04 | Memory injected into LLM context is ranked, deduplicated, and bounded | Code exists and passes VERIFICATION; gap is that no plan SUMMARY lists it in `requirements-completed` frontmatter |
</phase_requirements>

## Summary

Phase 57 closes three distinct gap categories found in the v2.0.1 milestone audit. All gaps were confirmed functional or near-functional — nothing requires architectural invention.

**Gap 1 — Boot memory dead code (MEMR-01):** `BootMemoryInjector.InjectBootMemoriesAsync` records boot nodes as `StepRecord`s in the run timeline (done), but those nodes never flow into the LLM prompt context. `BuildMemorySystemMessage` in `LLMModule` already has a `<boot-memory>` section that filters on `RecallType == "Boot"`, but `MemoryRecallService.RecallAsync` never produces `RecallType="Boot"` nodes — it only produces Disclosure and Glossary. The fix is to extend `MemoryRecallService.RecallAsync` to also query `_memoryGraph.QueryByPrefixAsync(animaId, "core://")` and seed the `byUri` dictionary with Boot-type entries before Disclosure processing.

**Gap 2 — Impact count hardcoded (PROV-03, PROV-04):** `Settings.razor` lines 184 and 205 call `string.Format(L["..."], 0)` and `string.Format(L["..."], provider.Models.Count, 0)` where the `0` represents affected module count. `ProviderImpactList.razor` already accepts `AffectedModuleCount` as a parameter — the component is ready. `IAnimaModuleConfigService` (backed by `AnimaModuleConfigService`) has `GetConfig(animaId, moduleId)` which returns a `Dictionary<string,string>`; iterating all Animas via `IAnimaRuntimeManager.GetAll()` and checking whether `llmProviderSlug` equals the target slug is the canonical way to count. Settings.razor already injects `LLMProviderRegistryService`; it can also inject `IAnimaRuntimeManager` and `IAnimaModuleConfigService`.

**Gap 3 — SUMMARY metadata (PROV-08, PROV-10, MEMR-04):** These are documentation-only fixes. The implementations are verified. The fix is to add a `requirements-completed:` line to the final SUMMARY of the relevant phase plans: `50-01-SUMMARY.md` (for PROV-08, PROV-10) and `52-02-SUMMARY.md` (for MEMR-04). The frontmatter format is `requirements-completed: [ID-01, ID-02]` on a single YAML array line.

**Primary recommendation:** Implement MEMR-01 boot recall as a single targeted extension to `MemoryRecallService.RecallAsync`; compute actual impact counts in `Settings.razor` using `IAnimaRuntimeManager.GetAll()` + `IAnimaModuleConfigService.GetConfig`; patch the three SUMMARY files with the missing `requirements-completed` entries.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| xunit | 2.9.3 | Unit/integration test framework | Already used across all 596 tests in the project |
| Microsoft.Extensions.Logging.Abstractions | 10.0.3 | NullLogger in tests | Project standard |
| Blazor (Server) | .NET 10 | Settings.razor UI | Project standard — all UI is Blazor |

No new packages required. This phase modifies existing files only.

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| IAnimaRuntimeManager | (in-core) | Enumerate all Anima IDs | Needed in Settings.razor to count affected modules |
| IAnimaModuleConfigService | (in-core) | Read per-Anima module config | Needed to check `llmProviderSlug` per Anima |

## Architecture Patterns

### Recommended Project Structure

No new directories. Changes are targeted to existing files:

```
src/OpenAnima.Core/
├── Memory/
│   └── MemoryRecallService.cs       # Add Boot recall block
└── Components/Pages/
    └── Settings.razor               # Inject IAnimaRuntimeManager + IAnimaModuleConfigService; compute impact

tests/OpenAnima.Tests/Unit/
├── MemoryRecallServiceTests.cs      # Add Boot recall test
└── LLMModuleMemoryTests.cs          # Add boot-memory XML section test

.planning/phases/
├── 50-provider-registry/
│   └── 50-01-SUMMARY.md             # Add requirements-completed: [PROV-08, PROV-10]
├── 52-automatic-memory-recall/
│   └── 52-02-SUMMARY.md             # Add MEMR-04 to requirements-completed
└── 57-integration-wiring-metadata-fixes/
    └── 57-01-SUMMARY.md             # New summary for this phase
```

### Pattern 1: Boot Recall in MemoryRecallService

**What:** Query `core://` prefix nodes and seed them as RecallType="Boot" before Disclosure/Glossary processing.

**When to use:** Every call to `RecallAsync` — boot nodes are unconditional (no trigger matching needed).

**Where to insert:** At the top of `RecallAsync`, before step 1 (disclosure nodes). Seed `byUri` dictionary with Boot entries first so Disclosure/Glossary can merge but never downgrade a Boot node.

```csharp
// Source: MemoryRecallService.cs — insert as new Step 1, shift existing steps to 2-5
var bootNodes = await _memoryGraph.QueryByPrefixAsync(animaId, "core://", ct);
foreach (var node in bootNodes)
{
    byUri[node.Uri] = new RecalledNode
    {
        Node = node,
        Reason = "boot",
        RecallType = "Boot",
        TruncatedContent = Truncate(node.Content)
    };
}
```

**Key insight:** `RecallPriority("Boot")` already returns `0` — the priority slot was implemented but never populated. This patch fills the slot without touching the sorting or budgeting logic.

**Merge behavior:** If a `core://` node also happens to match a disclosure trigger or glossary keyword, the Boot entry wins (seeded first; later Disclosure/Glossary blocks use `TryGetValue` and skip or merge-reason but do NOT downgrade `RecallType`). Review `MemoryRecallService.cs` lines 43-91: Disclosure block does `byUri[node.Uri] = new RecalledNode { RecallType = "Disclosure" }` unconditionally. To protect Boot nodes from being overwritten as Disclosure, the Disclosure seeding block must check: if a Boot entry already exists for this URI, skip or merge reason only. Same check for Glossary merge block.

**Revised merge rule for Disclosure block (lines 43-51):**
```csharp
foreach (var node in matchedDisclosure)
{
    if (byUri.TryGetValue(node.Uri, out var existingBoot) && existingBoot.RecallType == "Boot")
    {
        // Boot node already present — upgrade reason, keep Boot priority
        byUri[node.Uri] = existingBoot with { Reason = $"{existingBoot.Reason} + disclosure" };
    }
    else
    {
        byUri[node.Uri] = new RecalledNode
        {
            Node = node,
            Reason = "disclosure",
            RecallType = "Disclosure",
            TruncatedContent = Truncate(node.Content)
        };
    }
}
```

### Pattern 2: Impact Count Computation in Settings.razor

**What:** Count how many Animas have `llmProviderSlug == targetSlug` in their LLMModule config.

**When to use:** In `HandleDisable` and `HandleDelete` before showing the confirm dialog.

**Service injection:** Settings.razor already has `@inject LLMProviderRegistryService ProviderRegistry`. Add:

```razor
@inject IAnimaRuntimeManager AnimaManager
@inject IAnimaModuleConfigService ModuleConfig
```

Note: `IAnimaModuleConfigService` lives in `OpenAnima.Core.Services`. Check whether Settings.razor needs a `@using` directive.

**Computation method (add to @code block):**

```csharp
private int CountAffectedModules(string providerSlug)
{
    var animas = AnimaManager.GetAll();
    int count = 0;
    foreach (var anima in animas)
    {
        var config = ModuleConfig.GetConfig(anima.Id, "LLMModule");
        if (config.TryGetValue("llmProviderSlug", out var slug) && slug == providerSlug)
            count++;
    }
    return count;
}
```

**Calling it:**
```csharp
// HandleDisable (currently line 184):
_confirmMessage = string.Format(L["Providers.DisableConfirmMessage"], CountAffectedModules(provider.Slug));

// HandleDelete (currently line 205):
_confirmMessage = string.Format(L["Providers.DeleteConfirmMessage"], provider.Models.Count, CountAffectedModules(provider.Slug));
```

`HandleDisable` and `HandleDelete` are synchronous (`void`) — `GetAll()` and `GetConfig()` are both synchronous, so `CountAffectedModules` does not need to be async.

### Pattern 3: SUMMARY Frontmatter Patch

**What:** Add `requirements-completed:` entries to existing SUMMARY files.

**Format:** Single YAML array line (matches all project SUMMARY files):
```yaml
requirements-completed: [PROV-08, PROV-10]
```

**Files to patch:**

| File | Current `requirements-completed` line | Add |
|------|---------------------------------------|-----|
| `50-01-SUMMARY.md` | None (missing entirely) | `requirements-completed: [PROV-01, PROV-05, PROV-06, PROV-07, PROV-08, PROV-10]` |
| `52-02-SUMMARY.md` | `requirements-completed: [MEMR-01, MEMR-02, MEMR-03, MEMR-05]` | Append MEMR-04 |

**Note:** `50-01-SUMMARY.md` has no `requirements-completed` line at all. The audit says PROV-08 and PROV-10 were implemented in Plan 01. Check `50-01-SUMMARY.md` to confirm it covers PROV-08/PROV-10. If Plan 02 covers more, add to Plan 02 as well. The safest approach: add `requirements-completed: [PROV-08, PROV-10]` to `50-01-SUMMARY.md` (the plan that implemented the backend crypto and the interface).

### Anti-Patterns to Avoid

- **Do not add a new `IBootMemoryRecallService` interface:** The existing `IMemoryRecallService.RecallAsync` is the correct extension point. Boot recall is a retrieval strategy, not a separate service.
- **Do not make `HandleDisable`/`HandleDelete` async just for impact counting:** `GetAll()` and `GetConfig()` are synchronous. No async needed.
- **Do not inject `AnimaModuleConfigService` (concrete) in Settings.razor:** Use `IAnimaModuleConfigService` (the interface). The concrete class is used only in test helpers.
- **Do not modify the `RecallAsync` signature:** Add boot recall inline in the method body, not as a new method or parameter.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Counting Animas with a given provider slug | Custom DB query or file scan | `IAnimaRuntimeManager.GetAll()` + `IAnimaModuleConfigService.GetConfig()` | Already loaded in memory; consistent with LLMModule's own config resolution |
| Boot node retrieval | New graph method or IBootMemoryGraph interface | `IMemoryGraph.QueryByPrefixAsync(animaId, "core://")` | Method already exists and is tested; `BootMemoryInjector` uses it today |
| XML section for boot memory | New serializer | `BuildMemorySystemMessage` in LLMModule already handles `RecallType == "Boot"` | The section is dead code waiting to be activated, not missing code |

## Common Pitfalls

### Pitfall 1: Disclosure Block Overwrites Boot Nodes

**What goes wrong:** The current Disclosure seeding block (lines 43-51 of `MemoryRecallService.cs`) unconditionally writes `byUri[node.Uri] = new RecalledNode { RecallType = "Disclosure" }`. If a `core://` node also has a disclosure trigger, it gets downgraded from Boot to Disclosure priority.

**Why it happens:** Boot nodes are seeded first (new logic), then Disclosure block runs unconditionally.

**How to avoid:** Add a guard in the Disclosure seeding block: if `byUri` already contains a Boot entry for this URI, skip the overwrite (or merge reason only, keeping RecallType="Boot"). See Pattern 1 above for the exact code.

**Warning signs:** Test: a `core://` node with a disclosure trigger should appear in `<boot-memory>` XML, not `<recalled-memory>`.

### Pitfall 2: Settings.razor @inject Namespace Mismatch

**What goes wrong:** `IAnimaModuleConfigService` lives in `OpenAnima.Core.Services`. Settings.razor may not have a `@using OpenAnima.Core.Services` directive, causing a build error.

**Why it happens:** Settings.razor currently only uses `OpenAnima.Core.Providers`.

**How to avoid:** Add `@using OpenAnima.Core.Services` and `@using OpenAnima.Core.Anima` directives to Settings.razor. Check `_Imports.razor` for global usings first — if they are already there, no change needed.

**How to check:** `grep -r "OpenAnima.Core.Services\|OpenAnima.Core.Anima" src/OpenAnima.Core/Components/_Imports.razor`

### Pitfall 3: SUMMARY Frontmatter Patch Breaks YAML Parsing

**What goes wrong:** YAML frontmatter is delimited by `---`. If the added line uses inconsistent quoting or indentation, parsers or downstream tooling fail.

**Why it happens:** YAML is whitespace-sensitive; the project uses unquoted bracket arrays.

**How to avoid:** Match the exact format of existing `requirements-completed` lines: `requirements-completed: [ID-01, ID-02]` — unquoted, single space after colon, IDs comma-separated with single spaces.

### Pitfall 4: `50-01-SUMMARY.md` May Lack a requirements-completed Key Entirely

**What goes wrong:** Adding PROV-08/PROV-10 to a SUMMARY that has no existing `requirements-completed` line requires inserting a new YAML key, not editing an existing one.

**How to avoid:** Read the file first, locate the frontmatter block (`--- ... ---`), and insert the key before the closing `---`.

### Pitfall 5: Boot Recall Budget Interaction

**What goes wrong:** Boot nodes consume from the 6000-character total budget (`MaxTotalChars`). A large number of `core://` nodes could crowd out Disclosure/Glossary nodes.

**Why it happens:** Boot nodes are seeded first and sorted at priority 0 — they are consumed first in the budget loop.

**How to avoid:** This is by design (boot memory is highest priority). No code change needed. But document this in the test: verify that when boot nodes fill the budget, recalled nodes are excluded.

## Code Examples

### Adding Boot Recall to MemoryRecallService (full revised method outline)

```csharp
// Source: src/OpenAnima.Core/Memory/MemoryRecallService.cs — RecallAsync
public async Task<RecalledMemoryResult> RecallAsync(
    string animaId, string context, CancellationToken ct = default)
{
    // NEW Step 1: Query boot nodes (core:// prefix) — unconditional
    var bootNodes = await _memoryGraph.QueryByPrefixAsync(animaId, "core://", ct);

    // Step 2 (was 1): Get disclosure nodes
    var disclosureNodes = await _memoryGraph.GetDisclosureNodesAsync(animaId, ct);
    var matchedDisclosure = DisclosureMatcher.Match(disclosureNodes, context);

    // Step 3 (was 2): Glossary rebuild + matches
    await _memoryGraph.RebuildGlossaryAsync(animaId, ct);
    var glossaryMatches = _memoryGraph.FindGlossaryMatches(animaId, context);

    // Build byUri dict — seed Boot entries first
    var byUri = new Dictionary<string, RecalledNode>(StringComparer.Ordinal);
    foreach (var node in bootNodes)
    {
        byUri[node.Uri] = new RecalledNode
        {
            Node = node, Reason = "boot", RecallType = "Boot",
            TruncatedContent = Truncate(node.Content)
        };
    }

    // Seed Disclosure — guard against overwriting Boot entries
    foreach (var node in matchedDisclosure)
    {
        if (byUri.TryGetValue(node.Uri, out var existing) && existing.RecallType == "Boot")
            byUri[node.Uri] = existing with { Reason = $"{existing.Reason} + disclosure" };
        else
            byUri[node.Uri] = new RecalledNode
            {
                Node = node, Reason = "disclosure", RecallType = "Disclosure",
                TruncatedContent = Truncate(node.Content)
            };
    }

    // Glossary logic unchanged (already guards Boot via merge check for "Disclosure")
    // ... existing glossary code unchanged ...
}
```

### Impact Count Helper in Settings.razor

```csharp
// Source: src/OpenAnima.Core/Components/Pages/Settings.razor @code block
private int CountAffectedModules(string providerSlug)
{
    int count = 0;
    foreach (var anima in AnimaManager.GetAll())
    {
        var cfg = ModuleConfig.GetConfig(anima.Id, "LLMModule");
        if (cfg.TryGetValue("llmProviderSlug", out var slug) && slug == providerSlug)
            count++;
    }
    return count;
}
```

### Test for Boot Recall Nodes Appearing in XML

```csharp
// Add to LLMModuleMemoryTests.cs — verifies <boot-memory> section is populated
[Fact]
public async Task ExecuteWithMessages_BootNodes_AppearInBootMemoryXmlSection()
{
    var bootNode = MakeRecalledNode("core://identity/boot", "I am a developer agent", "boot", "Boot");
    var recallService = new FakeMemoryRecallService
    {
        Result = new RecalledMemoryResult { Nodes = [bootNode] }
    };
    var (llmService, module, _) = CreateTestSetup(recallService);
    await module.InitializeAsync();
    await InvokePromptAsync(module, "hello");

    var systemMsg = llmService.LastMessages!
        .First(m => m.Role == "system" && m.Content.Contains("<system-memory>"));
    Assert.Contains("<boot-memory>", systemMsg.Content);
    Assert.Contains("uri=\"core://identity/boot\"", systemMsg.Content);
    Assert.DoesNotContain("<recalled-memory>", systemMsg.Content); // only boot, no recalled section
}
```

### Test for Boot Recall in MemoryRecallServiceTests

```csharp
// Add to MemoryRecallServiceTests.cs
[Fact]
public async Task RecallAsync_BootNodes_ReturnedWithBootRecallType()
{
    var bootNode = MakeNode("core://identity/agent", "I am a developer agent");
    var fake = new FakeMemoryGraph { PrefixNodes = [bootNode] };
    var result = await BuildService(fake).RecallAsync("test-anima", "any context");

    Assert.True(result.HasAny);
    Assert.Equal("Boot", result.Nodes[0].RecallType);
    Assert.Equal("boot", result.Nodes[0].Reason);
    Assert.True(fake.QueryByPrefixCalled);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `core://` nodes reach only run timeline StepRecords | `core://` nodes reach both StepRecords AND LLM prompt `<boot-memory>` section | Phase 57 | MEMR-01 fully satisfied |
| Impact count hardcoded to 0 | Impact count computed from real module config | Phase 57 | PROV-03, PROV-04 UX accurate |
| PROV-08/PROV-10/MEMR-04 absent from SUMMARY metadata | Listed in `requirements-completed` frontmatter | Phase 57 | v2.0.1 audit score 31/31 |

**Deprecated/outdated:**
- Hardcoded `0` in `string.Format(L["Providers.DisableConfirmMessage"], 0)` — replaced by `CountAffectedModules()`

## Open Questions

1. **Does the Glossary merge block need a Boot guard too?**
   - What we know: The Glossary merge block (lines 65-90) calls `byUri.TryGetValue(uri, out var existing)` and produces `existing with { Reason = $"{existing.Reason} + {glossaryReason}" }` — it preserves `RecallType` from the existing entry via `with` expression.
   - What's unclear: `RecordType` is carried through `with`, so a Boot entry whose URI also has a glossary match would retain `RecallType = "Boot"` automatically.
   - Recommendation: No change needed for the Glossary merge block. Verify with a test that a `core://` node with glossary keywords stays `RecallType = "Boot"`.

2. **Should `50-01-SUMMARY.md` get all Phase 50 Plan 01 requirements, or only PROV-08/PROV-10?**
   - What we know: `50-01-SUMMARY.md` has no `requirements-completed` line. Plan 01 implemented the backend service (crypto, CRUD, ILLMProviderRegistry contract). PROV-08 (secure storage) and PROV-10 (registry contract) are clearly Plan 01 deliverables.
   - What's unclear: Whether PROV-01, PROV-05, PROV-06, PROV-07 should also be listed (they were built on Plan 01 backend too, but claimed by `50-02-SUMMARY.md`).
   - Recommendation: Add only `[PROV-08, PROV-10]` to `50-01-SUMMARY.md` to close the documented gap without second-guessing Plan 02's claims.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | none (uses default xunit discovery) |
| Quick run command | `dotnet test tests/OpenAnima.Tests/ --no-build -q --filter "BootRecall\|ProviderImpact\|MemoryRecallService"` |
| Full suite command | `dotnet test tests/OpenAnima.Tests/ --no-build -q` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MEMR-01 | `RecallAsync` returns Boot nodes for `core://` prefix | unit | `dotnet test --no-build -q --filter "RecallAsync_BootNodes"` | Wave 0 |
| MEMR-01 | `<boot-memory>` XML section populated in LLM prompt | unit | `dotnet test --no-build -q --filter "BootNodes_AppearInBootMemoryXml"` | Wave 0 |
| PROV-03 | `CountAffectedModules` returns correct count for disable | unit | `dotnet test --no-build -q --filter "CountAffectedModules"` | Wave 0 |
| PROV-04 | `CountAffectedModules` returns correct count for delete | unit | same as PROV-03 | Wave 0 |
| PROV-08 | SUMMARY frontmatter updated | manual-only | Read `50-01-SUMMARY.md` frontmatter | ✅ |
| PROV-10 | SUMMARY frontmatter updated | manual-only | Read `50-01-SUMMARY.md` frontmatter | ✅ |
| MEMR-04 | SUMMARY frontmatter updated | manual-only | Read `52-02-SUMMARY.md` frontmatter | ✅ |

### Sampling Rate
- **Per task commit:** `dotnet test tests/OpenAnima.Tests/ --no-build -q`
- **Per wave merge:** `dotnet test tests/OpenAnima.Tests/ --no-build -q`
- **Phase gate:** Full suite green (must stay at 596+ passing) before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/OpenAnima.Tests/Unit/MemoryRecallServiceTests.cs` — new test `RecallAsync_BootNodes_ReturnedWithBootRecallType`
- [ ] `tests/OpenAnima.Tests/Unit/LLMModuleMemoryTests.cs` — new test `ExecuteWithMessages_BootNodes_AppearInBootMemoryXmlSection`
- [ ] `tests/OpenAnima.Tests/Unit/ProviderImpactCountTests.cs` (new file) — OR add to `LLMProviderRegistryServiceTests.cs` — tests for `CountAffectedModules` logic (may need to test via Settings.razor indirectly or extract the method)

Note: `CountAffectedModules` is a private method on Settings.razor which is a Blazor component. Direct unit testing of private Blazor component methods is impractical. The preferred approach is to either (a) extract it as a static helper method tested separately, or (b) test via integration at the `LLMProviderRegistryService` level and accept Settings.razor testing as manual-only (consistent with Phase 50/51 patterns). Given Phase 50 precedent of manual UI verification, PROV-03/PROV-04 impact count accuracy is appropriately verified by reading the Settings.razor code rather than a dedicated test.

## Sources

### Primary (HIGH confidence)
- Direct code reading: `src/OpenAnima.Core/Memory/MemoryRecallService.cs` — full implementation reviewed
- Direct code reading: `src/OpenAnima.Core/Memory/BootMemoryInjector.cs` — boot injection confirmed working
- Direct code reading: `src/OpenAnima.Core/Components/Pages/Settings.razor` — hardcoded 0 confirmed at lines 184, 205
- Direct code reading: `src/OpenAnima.Contracts/ILLMProviderRegistry.cs` — interface confirmed
- Direct code reading: `src/OpenAnima.Core/Services/AnimaModuleConfigService.cs` — synchronous GetConfig confirmed
- Direct code reading: `src/OpenAnima.Core/Anima/IAnimaRuntimeManager.cs` — synchronous GetAll() confirmed
- Direct code reading: `.planning/v2.0.1-MILESTONE-AUDIT.md` — gap analysis confirmed

### Secondary (MEDIUM confidence)
- `.planning/phases/52-automatic-memory-recall/52-02-SUMMARY.md` — frontmatter `requirements-completed` format confirmed
- `.planning/phases/50-provider-registry/50-01-SUMMARY.md` — confirmed no `requirements-completed` key exists
- Test files reviewed: `MemoryRecallServiceTests.cs`, `LLMModuleMemoryTests.cs`, `BootMemoryInjectorWiringTests.cs` — fake infrastructure patterns confirmed

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all changes are in-repo
- Architecture: HIGH — all three gaps are narrow, surgical changes to existing code paths
- Pitfalls: HIGH — Boot node overwrite risk confirmed by reading the Disclosure seeding code directly; namespace injection risk confirmed by reading Settings.razor
- SUMMARY patch: HIGH — format confirmed by reading all 20+ existing SUMMARY files

**Research date:** 2026-03-23
**Valid until:** 2026-04-22 (stable codebase, no external dependencies)
