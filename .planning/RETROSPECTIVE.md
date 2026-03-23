# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

## Milestone: v1.9 — Event-Driven Propagation Engine

**Shipped:** 2026-03-20
**Phases:** 3 | **Plans:** 6 | **Commits:** ~15

### What Was Built
- WiringEngine replaced DAG topological sort with event-driven per-module SemaphoreSlim routing — modules execute on data arrival, output fans out downstream
- ConnectionGraph accepts cyclic graphs (DFS HasCycle is informational only) — feedback loops enabled without engine rejection
- HeartbeatModule refactored to standalone PeriodicTimer with IModuleConfigSchema — config-driven interval with 50ms floor clamp
- ITickable interface removed from Contracts — pure data-driven execution model, no remaining implementors
- ModuleSchemaService resolves IModuleConfigSchema by module name via static type map + DI
- EditorConfigSidebar schema-aware rendering with type-appropriate inputs, DisplayName labels, and schema defaults merged on load

### What Worked
- Tight 3-phase scope (engine → heartbeat → sidebar) delivered cleanly in 2 days with zero regressions
- Phase 44 as gap closure — added after initial 42-43 roadmap to close BEAT-06 frontend gap before shipping
- Per-module SemaphoreSlim(1,1) keyed by targetModuleRuntimeName — clean wave isolation without module awareness
- ITickable removal was surgical — single interface deletion cascaded cleanly through HeartbeatLoop/HeartbeatModule/Contracts
- Schema defaults merge pattern (inject on load, auto-save only on edit) avoids spurious persistence

### What Was Inefficient
- BEAT-05 missing formal VERIFICATION.md — procedural gap persists despite 5 unit tests and UAT 4/4 passed (audit caught it twice)
- v1.9 ROADMAP initially created with only 2 phases; Phase 44 gap closure added retroactively after Phase 43 verification revealed BEAT-06 sidebar gap
- Phase 43 plan checkboxes in ROADMAP.md not marked as [x] after completion — stale tracking in roadmap source

### Patterns Established
- Event-driven propagation: modules execute via EventBus routing subscriptions, not topological sort — all future modules follow this pattern
- Schema-first config: modules implementing IModuleConfigSchema get automatic sidebar rendering with defaults — no manual EditorConfigSidebar cases needed
- ModuleSchemaService: static type map for built-in modules + IServiceProvider.GetService fallback for external modules — extensible without reflection

### Key Lessons
1. Gap closure phases (Phase 44) should be anticipated at roadmap creation — schema rendering was a known IModuleConfigSchema gap from v1.7
2. Three-phase milestones with tight dependency chains (42→43→44) ship faster than broad multi-phase milestones — fewer coordination points
3. VERIFICATION.md should be generated as part of plan execution, not as a separate audit step — procedural gaps persist across re-audits
4. Static type maps for DI-based resolution are pragmatic — avoids reflection scanning while remaining extensible via the fallback path

### Cost Observations
- Model mix: ~70% sonnet (executor), ~20% haiku (research), ~10% opus (planning/audit)
- Sessions: ~2 sessions across 2 days
- Notable: Smallest milestone by phase count (3) but cleanest execution — zero deviations in Phase 44, single auto-fix in Phase 43

---

## Milestone: v1.8 — SDK Runtime Parity

**Shipped:** 2026-03-18
**Phases:** 4 | **Plans:** 8 | **Commits:** 45

### What Was Built
- PluginLoader DI-aware constructor resolution with ContractsTypeMap FullName matching, greedy constructor selection, ILogger via ILoggerFactory, optional/required parameter handling
- ChatMessageInput migrated from Core.LLM to Contracts with SerializeList/DeserializeList static helpers using System.Text.Json camelCase options
- LLMModule messages input port with semaphore-based priority rule — messages port acquires first, prompt Wait(0) returns false
- IModuleStorage interface in Contracts with ModuleStorageService implementation — per-Anima per-Module paths, auto-created directories, path validation
- PluginLoader bound IModuleStorage injection — special case before generic ContractsTypeMap lookup, manifest.Id ?? manifest.Name as moduleId
- External ContextModule — real .oamod module with conversation history accumulation, history.json persistence, restart restore, per-Anima isolation

### What Worked
- Phase dependency chain (38→39→40→41) built incrementally — each phase added one capability consumed by the next
- Phase 41 as SDK validation capstone — exercised all prior phases' APIs in a real module, catching integration issues early
- Bound IModuleStorage pattern — clean separation between built-in (explicit moduleId) and external (auto-bound) usage
- Re-verification after gap closure (Phase 38) — initial verification caught test build errors, Plan 03 fixed them, re-verification confirmed
- Integration checker validated all 12 cross-phase wiring points and 4 E2E flows with zero gaps

### What Was Inefficient
- SUMMARY frontmatter missing requirements_completed for 4 of 9 requirements (MSG-01/02/03, STOR-01) ��� documentation gap caught by audit
- Nyquist VALIDATION.md created for all 4 phases but never signed off (nyquist_compliant: false) — validation strategy drafted but not executed
- Phase 38 required 3 plans (gap closure) due to test build errors from CrossAnimaRouter constructor ambiguity and harness property name mismatch

### Patterns Established
- ContractsTypeMap for cross-context DI: `Dictionary<string, Type>` mapping FullName → host type for PluginLoader parameter resolution
- Bound service injection: PluginLoader creates per-module bound instances for services that need module identity (IModuleStorage)
- using alias migration: `using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput` for backward-compatible type moves
- Semaphore priority: First subscriber acquires SemaphoreSlim(1,1), second subscriber's Wait(0) returns false — deterministic port priority

### Key Lessons
1. SUMMARY frontmatter requirements_completed should be populated by the executor, not left for audit to discover — 4/9 missing is too many
2. Nyquist validation strategies should be signed off during phase execution, not left as draft — partial compliance across all phases
3. Gap closure plans are a healthy pattern — catching test build errors in verification and fixing them in a dedicated plan is better than ignoring them
4. SDK validation via a real external module is the most effective way to verify API surface — ContextModule caught the IModuleStorage binding gap

### Cost Observations
- Model mix: ~70% sonnet (executor/verifier), ~25% haiku (research), ~5% opus (planning)
- Sessions: ~3 sessions across 2 days
- Notable: 4 phases, 8 plans, 45 commits in 2 days — consistent velocity with v1.6/v1.7

---

## Milestone: v1.7 — Runtime Foundation

**Shipped:** 2026-03-16
**Phases:** 6 | **Plans:** 13 | **Commits:** ~40

### What Was Built
- Race-free module execution via ConcurrentDictionary + SemaphoreSlim skip-when-busy guards
- ActivityChannelHost with 3 named channels (heartbeat/chat/routing) — serial within, parallel between
- 9 new contract types in OpenAnima.Contracts
- 12 active built-in modules decoupled to Contracts-first APIs
- ChatInputModule wired through chat channel for serial execution guarantee

### What Worked
- [StatelessModule] attribute for concurrent dispatch classification — clean opt-in
- SemaphoreSlim Wait(0) over WaitAsync() — synchronous non-blocking skip semantics
- Code review quick tasks (3, 4, 5, 6) caught real issues before milestone completion

### What Was Inefficient
- Phase 36 required 5 plans (most in any phase) — built-in module decoupling was broader than estimated
- IModuleConfigSchema added to Contracts but has no production consumer yet

### Key Lessons
1. Code review as quick tasks between phases catches issues early — worth the investment
2. Channel-based serialization (ActivityChannelHost) is cleaner than lock-based approaches for module execution ordering

---

## Milestone: v1.6 — Cross-Anima Routing

**Shipped:** 2026-03-14
**Phases:** 4 | **Plans:** 8 | **Commits:** 25

### What Was Built
- CrossAnimaRouter singleton with ConcurrentDictionary port registry, TCS-based request correlation, 32-char Guid IDs, PeriodicTimer cleanup, and Anima deletion lifecycle hooks
- AnimaInputPort, AnimaOutputPort, AnimaRoute modules with end-to-end cross-Anima request-response via EventBus push delivery
- ModuleEvent.Metadata nullable dictionary for correlationId passthrough across module boundaries
- FormatDetector XML routing marker parser with case-insensitive matching, unclosed tag detection, multiline payloads
- LLMModule extended with system message injection, FormatDetector integration, self-correction retry loop (MaxRetries=2), error output port
- HttpRequestModule with SsrfGuard IP blocking, IHttpClientFactory resilience pipeline, 10s timeout, configurable method/headers/body sidebar UI
- EditorConfigSidebar extended with cascading Anima/port dropdowns, HTTP method select, textarea fields

### What Worked
- TDD red-green consistently produced clean implementations — FormatDetector (4 min), SsrfGuard (8 min), all tests passing on first GREEN
- Phase dependency chain (28→29→30) cleanly separated concerns: infrastructure → modules → intelligence layer
- Phase 31 (HTTP) was independent of 28-30, allowing parallel planning
- Deferred singleton lambda pattern for DI circular dependency (ICrossAnimaRouter ↔ IAnimaRuntimeManager) — resolved at first use
- Integration checker caught potential DI gap (LLMModule optional parameter) during milestone audit

### What Was Inefficient
- REQUIREMENTS.md tracking table not updated after Phase 30 Plan 01 — FMTD-01/02/04 marked "Pending" despite being implemented
- PROMPT-02 requirement text never updated after user pivoted to no token cap — stale requirement text persisted through verification
- Hardcoded event name strings ("AnimaRouteModule.port.request") in LLMModule dispatch — fragile coupling

### Patterns Established
- Routing event chain: CrossAnimaRouter.RouteRequestAsync → EventBus routing.incoming.{port} → AnimaInputPortModule → LLM chain → AnimaOutputPortModule → CompleteRequest
- Metadata passthrough: copy dictionary at each hop, never share reference
- SSRF guard pattern: static SsrfGuard.IsBlocked(url, out reason) before any HTTP operation
- FakeHttpMessageHandler + TCS/WhenAny pattern for testing mutually-exclusive EventBus output ports
- Self-correction loop: append assistant+user correction turns to message list, re-call LLM up to MaxRetries

### Key Lessons
1. Update REQUIREMENTS.md tracking table immediately after each plan completes — stale checkboxes create noise in milestone audit
2. TDD with short timeouts (100-150ms) is effective for testing async timeout behavior without 30s waits
3. Optional DI parameters with defaults should be verified at integration time — test suites that bypass DI won't catch resolution gaps
4. XML routing markers are a pragmatic format for LLM-driven routing — close enough to training data for 80-95% compliance

### Cost Observations
- Model mix: ~75% sonnet (executor/verifier), ~20% haiku (research), ~5% opus (planning)
- Sessions: ~4 sessions across 3 days
- Notable: Entire milestone (4 phases, 8 plans, 16 tasks) completed in ~100 min execution time — fastest per-phase velocity yet

---

## Milestone: v1.5 — Multi-Anima Architecture

**Shipped:** 2026-03-09
**Phases:** 5 | **Plans:** 13 | **Commits:** 55

### What Was Built
- Multi-Anima architecture with AnimaRuntimeManager, AnimaContext, per-Anima runtime containers
- Full Chinese/English i18n with LanguageService, .resx resources, all components localized
- Module management UI with card layout, .oamod installation, per-Anima enable/disable
- EditorConfigSidebar with metadata display, typed config form, auto-save, validation
- 5 built-in modules: FixedText (template interpolation), TextJoin, TextSplit, ConditionalBranch (expression evaluator), LLM (per-Anima config override)
- Per-Anima chat isolation with message clearing on Anima switch

### What Worked
- Wave-based parallelization for plan execution — independent plans executed concurrently
- Singleton + event pattern (AnimaContext, LanguageService) — consistent, avoids CascadingValue re-render issues
- Per-Anima runtime container pattern — clean isolation of HeartbeatLoop, WiringEngine, EventBus per instance
- 3-day timeline for 5 phases, 13 plans — rapid execution with consistent quality
- Phase 24 (service migration) handled the hardest refactoring (DI, SignalR filtering) cleanly

### What Was Inefficient
- Milestone audit was stale — run at roadmap creation time (only Phase 23 complete), not re-run before completion
- ANIMA-08 (independent module instances) deferred as tech debt — global singleton kept for DI compatibility
- MODMGMT-01/02/03/06 partially implemented — card UI works but full install/uninstall/search flows incomplete
- 23-02-SUMMARY.md missing requirements-completed frontmatter — metadata gap caught by audit but never fixed

### Patterns Established
- AnimaRuntime container: Encapsulates per-instance services (HeartbeatLoop, WiringEngine, EventBus) in a single disposable container
- Key-name-based field type rendering: EditorConfigSidebar infers textarea/password/text from config key names
- Per-execution ChatClient creation: Singleton module creates new client per execution for config isolation
- Expression evaluator pattern: Pragmatic recursive descent (~150 LOC) for well-defined operator set

### Key Lessons
1. Run milestone audits after all phases complete, not at roadmap creation — stale audit provided no value
2. Global singleton DI for module constructors is hard to break — plan module instance isolation as its own phase
3. Chinese-first i18n works well when .resx is the primary mechanism — SDK auto-includes as EmbeddedResource
4. Fixed port counts (TextJoin's 3 inputs) are a real limitation of the static port system — dynamic ports need architectural investment

### Cost Observations
- Model mix: ~80% sonnet (executor), ~15% haiku (research), ~5% opus (planning)
- Sessions: ~8 sessions across 3 days
- Notable: Phase 24-01 (per-Anima runtime isolation) was the most complex plan at 90 minutes — DI refactoring + SignalR filtering across 23 files

---

## Milestone: v2.0 — Structured Cognition Foundation

**Shipped:** 2026-03-21
**Phases:** 5 | **Plans:** 18 | **Commits:** 48

### What Was Built
- Durable task runtime: RunDescriptor/StepRecord/RunStateEvent domain types, SQLite persistence via IRunRepository with Dapper, RunService lifecycle engine, ConvergenceGuard step budgets, StepRecorder with hash-based dedup, RunRecoveryService, /runs UI page with 5 shared components and SignalR real-time
- Workspace tool surface: 12 tools (file_read, file_write, directory_list, file_search, grep_search, git_status, git_diff, git_log, git_show, git_commit, git_checkout, shell_exec) + IWorkspaceTool interface + CommandBlacklistGuard + WorkspaceToolModule orchestrator
- Run inspector: RunDetail page with mixed chronological timeline, StepTimelineRow accordion, PropagationColorAssigner chain visualization, TimelineFilterBar with module/status/chain dropdowns, click-to-highlight causality
- Artifact & memory: ArtifactStore SQLite+filesystem, ArtifactFileWriter with path safety, MemoryGraph with GlossaryIndex (Aho-Corasick), DisclosureMatcher, snapshot versioning, 3 memory tools, BootMemoryInjector, /memory UI page
- Structured cognition: JoinBarrierModule fan-in with double-check semaphore, PropagationId carry-through in StepRecorder, LLMModule WaitAsync serialization, WorkflowPresetService with DB migration, codebase analysis preset JSON, WorkflowProgressBar + WorkflowPresetSelector UI

### What Worked
- 5-phase linear dependency chain (45→46→47→48→49) executed cleanly in 2 days — largest milestone by plan count (18) with fastest per-plan velocity
- Each phase boundary was a clean capability addition consumed by the next — RunRepository → WorkspaceTools → RunDetail → ArtifactStore → WorkflowPresets
- SQLite + Dapper per-operation connection pattern established in Phase 45, reused consistently in ArtifactStore and MemoryGraph — no connection lifecycle bugs
- PropagationId carry-through via ConcurrentDictionary (same pattern as _stepAnimaIds) — pattern reuse reduces implementation risk
- 495/495 tests green at milestone end — zero regressions despite 125 files changed and 11,234 insertions

### What Was Inefficient
- BootMemoryInjector registered in DI but never called from run-start path — integration gap caught by audit, accepted as tech debt
- GetToolDescriptors() declared on IWorkspaceTool but never consumed by LLMModule — tool schema injection not wired
- SUMMARY frontmatter missing requirements_completed for 5 of 25 requirements (WORK-02/03/04, OBS-04, COG-01) — documentation gap persists from v1.8 pattern
- WorkflowProgressBar uses raw step count as numerator vs node count denominator — imprecise fraction, accepted for v2.0
- Nyquist VALIDATION.md exists for 4/5 phases but none are compliant (wave_0 incomplete); Phase 46 missing entirely

### Patterns Established
- Per-operation SQLite connections with WAL mode for concurrent access — RunRepository, ArtifactStore, MemoryGraph all follow this
- IWorkspaceTool stateless interface with per-call workspaceRoot — tools don't hold workspace state
- Double-check semaphore guard: fast-path count check before Wait(0), re-check after acquiring — JoinBarrierModule fan-in
- WaitAsync for workflow branch serialization vs Wait(0) skip-when-busy — LLMModule needs correctness over throughput in workflow context
- Preset JSON as Content Include in csproj with CopyToOutputDirectory Always — no manual copy step
- 12-char hex IDs for artifacts vs 8-char for steps — lower collision probability for cross-run references

### Key Lessons
1. Linear dependency chains (A→B→C→D→E) execute faster than broad parallel phases — each phase builds directly on the previous, minimizing context switching
2. SQLite WAL + per-operation connections is the right pattern for embedded persistence — zero connection management bugs across 3 independent stores
3. SUMMARY frontmatter requirements_completed gap is a systemic issue (v1.8, v1.9, v2.0) — needs automation or executor-level enforcement
4. Integration gaps (BootMemoryInjector, GetToolDescriptors) emerge when phases are designed independently — cross-phase wiring review at roadmap creation would catch these
5. Nyquist validation strategy continues to be partially adopted — consider making it a mandatory plan step rather than optional phase artifact

### Cost Observations
- Model mix: ~65% sonnet (executor), ~25% haiku (research), ~10% opus (planning/audit)
- Sessions: ~4 sessions across 2 days
- Notable: 18 plans in 2 days is fastest delivery rate — 9 plans/day vs previous best of ~6 plans/day (v1.7)

---

## Milestone: v2.0.1 — Provider Registry & Living Memory

**Shipped:** 2026-03-23
**Phases:** 8 | **Plans:** 16 | **Commits:** 115

### What Was Built
- Global LLM Provider Registry: ILLMProviderRegistry contract, LLMProviderRegistryService with AES-GCM encrypted credentials, full CRUD Settings UI (ProviderCard/ProviderDialog/ProviderModelList/ProviderImpactList), connection testing with 30s timeout
- LLM module configuration: IModuleConfigSchema with CascadingDropdown field type, three-layer config precedence, auto-clear on deleted provider/model, EditorConfigSidebar LLM-specific cascading dropdown rendering
- Automatic memory recall: IMemoryRecallService with boot injection (core:// prefix), DisclosureMatcher trigger matching, GlossaryIndex keyword matching, ranked/deduped/bounded RecalledMemoryResult, XML <system-memory> injection in LLMModule
- Memory tools: MemoryRecallTool + MemoryLinkTool as IWorkspaceTools with XML tool descriptor injection via BuildToolDescriptorBlock
- Living memory sedimentation: ISedimentationService with fire-and-forget LLM extraction, JSON keyword normalization, provenance-backed MemoryNode writing with snapshot versioning
- Sedimentation LLM config: Settings page Living Memory section with cascading provider/model dropdowns for sedimentation pipeline activation
- Memory review surfaces: Three collapsible MemoryNodeCard sections (Provenance, Snapshot History, Relationships) with LineDiff, restore confirmation, edge navigation
- Integration wiring: Boot recall in RecallAsync, CountAffectedModules for real impact counts, SUMMARY metadata gap closure

### What Worked
- Tight phasing: 6 initial phases (50-55) designed at roadmap creation, 2 gap closure phases (56-57) added after audit — clean gap identification and resolution
- Audit-driven gap closure: First audit found 3/31 unsatisfied requirements + 3 integration gaps; Phases 56-57 closed all gaps before milestone completion
- Consistent DI patterns: ILLMProviderRegistry, IMemoryRecallService, ISedimentationService all followed established singleton + optional constructor param patterns
- Fire-and-forget sedimentation: Task.Run with CancellationToken.None and snapshot-captured values — clean isolation from LLM call lifecycle
- 603/603 tests green with zero regressions across 8 phases — 34 new tests added during milestone

### What Was Inefficient
- SUMMARY frontmatter one_liner field null for all 16 summaries — summary-extract tool returned null for all, required manual accomplishment extraction
- LLMProviderRegistryService.InitializeAsync ordering dependency — providers empty until user visits /settings (self-heals but bad UX on first launch)
- Nyquist compliance partial: 5/8 phases have draft VALIDATION.md (all non-compliant), 3 phases missing entirely — systemic gap continues
- Phase 56 and 57 planned without discuss-phase — directly created after audit gap identification, but could have benefited from targeted questioning
- Two-day milestone execution compressed 8 phases into very dense sessions — higher context pressure than spreading over 3+ days

### Patterns Established
- AES-GCM + PBKDF2 machine-fingerprint key derivation for local secret encryption — reusable for any future credential storage
- CascadingDropdown ConfigFieldType: Two-tier select rendering in EditorConfigSidebar driven by IModuleConfigSchema field metadata
- __manual__ sentinel: Explicit bypass marker for manual LLM config path — avoids null/empty ambiguity
- Boot recall seeded before Disclosure/Glossary: byUri dictionary starts with Boot entries, merge preserves type/priority
- Fire-and-forget sedimentation: snapshot + Task.Run + CancellationToken.None — pattern for background work after LLM calls

### Key Lessons
1. Audit-driven gap closure (audit → identify gaps → add phases → re-audit) is the most effective way to ensure milestone completeness — v2.0.1 went from 28/31 to 31/31 requirements
2. LLMProviderRegistryService.InitializeAsync ordering should have been wired into IHostedService from the start — deferred startup initialization creates subtle UX gaps
3. SUMMARY one_liner frontmatter needs to be populated during execution — automated extraction fails when the field is missing
4. Provider registry CRUD pattern (card list → dialog → impact check → confirm) is reusable for any entity management UI
5. Three-layer config precedence (registry-backed > manual > global) is a clean pattern for gradual migration from legacy config

### Cost Observations
- Model mix: ~60% sonnet (executor), ~30% haiku (research), ~10% opus (planning/audit)
- Sessions: ~4 sessions across 2 days
- Notable: 8 phases / 16 plans in 2 days — 8 plans/day matches v2.0's record pace; gap closure phases (56-57) took <30min combined

---

## Cross-Milestone Trends

| Milestone | Commits | Phases | Notable Patterns |
|-----------|---------|--------|------------------|
| v2.0.1 | 115 | 8 | Provider registry, memory recall pipeline, sedimentation, review surfaces |
| v2.0 | 48 | 5 | Durable runs, workspace tools, run inspector, memory graph, workflow presets |
| v1.9 | ~15 | 3 | Event-driven propagation, cycle support, schema-first config |
| v1.8 | 45 | 4 | PluginLoader DI, ChatMessageInput migration, IModuleStorage, ContextModule |
| v1.7 | ~40 | 6 | ActivityChannelHost, Contracts expansion, module decoupling |
| v1.6 | 25 | 4 | Cross-Anima routing, format detection, HTTP module |
| v1.5 | 55 | 5 | Multi-instance architecture, i18n, module config |
| v1.4 | ~20 | 3 | CLI tool, .oamod packaging |
| v1.3 | ~40 | 10 | SVG editor, port system, verification backfill |
| v1.2 | ~20 | 3 | OpenAI SDK integration pattern |
| v1.1 | ~25 | 5 | Blazor Server architecture established |

### Cumulative Quality

| Milestone | LOC (total) | New Patterns | Tech Debt Items |
|-----------|-------------|--------------|-----------------|
| v1.0 | ~1,300 | AssemblyLoadContext, EventBus | 6 |
| v1.1 | ~3,700 | SignalR push, Blazor Server | 7 |
| v1.2 | ~6,400 | Streaming, token counting | 0 |
| v1.3 | ~11,000 | SVG editor, topological sort | 0 |
| v1.4 | ~14,500 | CLI framework, .oamod format | 3 |
| v1.5 | ~20,000 | Multi-Anima runtime, i18n, module config | 5 |
| v1.6 | ~13,610 | Routing event chain, SSRF guard, self-correction loop | 5 |
| v1.7 | ~23,734 | ActivityChannelHost, SemaphoreSlim skip, [StatelessModule] | 5 |
| v1.8 | ~25,015 | ContractsTypeMap DI, bound service injection, semaphore priority | 7 |
| v1.9 | ~27,500 | Event-driven propagation, ITickable removal, schema-first sidebar | 6 |
| v2.0 | ~41,773 | SQLite WAL stores, IWorkspaceTool, JoinBarrier, Aho-Corasick glossary, workflow presets | 11 |
| v2.0.1 | ~44,700 | AES-GCM encryption, CascadingDropdown, memory recall pipeline, sedimentation, review surfaces | 6 |

### Top Lessons (Verified Across Milestones)

1. Singleton + event pattern is the right choice for Blazor Server cross-component state (AnimaContext, LanguageService, EditorStateService) — CascadingValue causes re-render storms
2. Name-based type comparison is essential for cross-AssemblyLoadContext scenarios — verified in v1.0 (PluginLoader), v1.4 (CLI validate), v1.5 (module registration)
3. Per-instance isolation requires upfront architectural planning — retrofitting is expensive (v1.3 DI fix, v1.5 runtime container)
4. Wave-based plan execution significantly speeds up milestone delivery when plans are independent
5. TDD with short timeouts and TCS-based completion produces deterministic async tests — verified in v1.6 routing and HTTP modules
6. Update tracking tables immediately after plan completion — stale docs create audit noise (v1.6 lesson)
7. SDK validation via a real external module is the most effective API surface verification — catches binding gaps that unit tests miss (v1.8 lesson)
8. Gap closure plans are healthy — catching issues in verification and fixing in a dedicated plan is better than ignoring (v1.8 lesson)
9. Linear phase dependency chains deliver faster than broad parallel phases — each builds directly on previous with minimal context switching (v2.0 lesson)
10. SQLite WAL + per-operation connections is the reliable embedded persistence pattern — zero connection bugs across 3 independent stores (v2.0 lesson)
11. Audit-driven gap closure (audit → gaps → add phases → re-audit) is the most effective milestone completeness strategy — v2.0.1 went from 28/31 to 31/31 requirements (v2.0.1 lesson)
12. Fire-and-forget background work after LLM calls should use snapshot-captured values + CancellationToken.None — isolates background work from caller lifecycle (v2.0.1 lesson)
