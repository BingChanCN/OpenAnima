# Phase 36: Built-in Module Decoupling - Context

**Gathered:** 2026-03-15
**Status:** Ready for research/planning

<domain>
## Phase Boundary

Decouple the built-in module layer so module code depends on `OpenAnima.Contracts` instead of `OpenAnima.Core` internals, while keeping runtime startup/DI working and bringing `oani new` into the same Contracts-only model. This phase clarifies the true built-in module inventory, migrates shared module-facing metadata/helpers where needed, and updates module scaffolding. It does not add new runtime capabilities or expand unrelated platform APIs.

</domain>

<decisions>
## Implementation Decisions

### Migration Object List
- Phase 36 is still scoped and evaluated against **14 built-in modules**, not auto-reduced to the currently visible set of 12.
- Research/planning must explicitly audit the historical and current built-in module list, identify the missing 2 modules, and either:
  - migrate all 14, or
  - prove the current real inventory is 12 and correct the project documentation in the same phase.
- If the audit concludes the real active built-in inventory is 12, Phase 36 should **update the docs** to replace the stale "14 built-in modules" wording.
- "Module code" includes not just `*Module.cs`, but also module-owned metadata and helper types that affect the module's outward shape.
- Historical built-in candidates should be inventoried and dispositioned clearly; do not leave the 14-vs-12 discrepancy implicit.

### Contracts Purity Rule
- The target is **Contracts-first with a very small explicit exception list**, not absolute purity at any cost.
- `LLMModule` may remain an explicit, documented exception in Phase 36; do not block the phase on moving all LLM-facing APIs to Contracts.
- `HttpRequestModule` must be decoupled in Phase 36. If it still needs Core-only helpers, add the **minimum Contracts abstraction** required by the module's real usage.
- When expanding Contracts, only add types/interfaces that built-in modules already concretely need. Do not pre-design future-facing APIs.
- Any exception that remains after the migration should be deliberate and documented, not an accidental leftover.

### Metadata and Module Outward Shape
- `ModuleMetadataRecord` should move to `OpenAnima.Contracts` in Phase 36.
- Built-in modules should keep using a common metadata pattern after the move; avoid splitting into many ad hoc per-module metadata implementations unless forced.
- Simple modules should move toward an external-module-like shape where practical.
- More complex modules may retain some internal platform-specific structure, but should still consume module-facing contracts from `OpenAnima.Contracts`.
- If a module currently depends on Core helper types, prefer moving the genuinely module-facing pieces into Contracts instead of rewriting modules around awkward platform-only workarounds.
- Across the 14-module migration, consistency of migration style is preferred over one-off patterns.

### `oani new` Template Direction
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

</decisions>

<specifics>
## Specific Ideas

- The current runtime registration and startup initialization paths only show 12 active built-in module types:
  - `LLMModule`
  - `ChatInputModule`
  - `ChatOutputModule`
  - `HeartbeatModule`
  - `FixedTextModule`
  - `TextJoinModule`
  - `TextSplitModule`
  - `ConditionalBranchModule`
  - `AnimaInputPortModule`
  - `AnimaOutputPortModule`
  - `AnimaRouteModule`
  - `HttpRequestModule`
- The "14 built-in modules" wording in roadmap/requirements/project should be treated as an unresolved inventory problem to solve, not silently ignored.
- `LLMModule` is the one acceptable likely exception if a full Contracts move would require broad LLM API promotion in the same phase.
- `HttpRequestModule` is not an allowed exception; the phase should carry it across the Contracts boundary cleanly.
- The desired end state is that built-in modules look credible as exemplars for external module authors, even if a few complex modules keep limited internal wiring details.

</specifics>

<code_context>
## Existing Code Insights

### Current Evidence of the 12-Module Active Set
- `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` registers 12 built-in modules as singletons.
- `src/OpenAnima.Core/Hosting/WiringInitializationService.cs` uses the same 12-module set for port registration, with 11 of them auto-initialized at startup (`HeartbeatModule` excluded from auto-init).

### Current Core Dependencies in Module Files
- Many modules still import `OpenAnima.Core.Anima` and `OpenAnima.Core.Services` for `IAnimaContext` / `IAnimaModuleConfigService`.
- Routing modules still import routing types through Core namespaces even though Phase 35 created Contracts routing types and shims.
- `LLMModule` still imports `OpenAnima.Core.LLM`.
- `HttpRequestModule` still imports `OpenAnima.Core.Http`.
- `ChatInputModule` still imports `OpenAnima.Core.Channels`.

### Phase 36 Constraints Implied by Existing Requirements
- `DECPL-05` explicitly requires moving `ModuleMetadataRecord` to Contracts.
- `DECPL-04` makes `oani new` template output part of the same phase, not an optional side task.
- `REQUIREMENTS.md` currently says moving `ILLMService` to Contracts is out of scope, which is why `LLMModule` is allowed to remain an explicit exception if needed.

</code_context>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope.

</deferred>

---

*Phase: 36-built-in-module-decoupling*
*Context gathered: 2026-03-15*
