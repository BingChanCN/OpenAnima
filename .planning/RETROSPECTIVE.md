# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

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

## Cross-Milestone Trends

| v1.8 | 45 | 4 | PluginLoader DI, ChatMessageInput migration, IModuleStorage, ContextModule |
| v1.7 | ~40 | 6 | ActivityChannelHost, Contracts expansion, module decoupling |
| v1.6 | 25 | 4 | Cross-Anima routing, format detection, HTTP module |
| v1.1 | ~25 | 5 | Blazor Server architecture established |
| v1.2 | ~20 | 3 | OpenAI SDK integration pattern |
| v1.3 | ~40 | 10 | SVG editor, port system, verification backfill |
| v1.4 | ~20 | 3 | CLI tool, .oamod packaging |
| v1.5 | 55 | 5 | Multi-instance architecture, i18n, module config |

### Cumulative Quality

| Milestone | LOC (total) | New Patterns | Tech Debt Items |
|-----------|-------------|--------------|-----------------|
| v1.0 | ~1,300 | AssemblyLoadContext, EventBus | 6 |
| v1.1 | ~3,700 | SignalR push, Blazor Server | 7 |
| v1.2 | ~6,400 | Streaming, token counting | 0 |
| v1.3 | ~11,000 | SVG editor, topological sort | 0 |
| v1.4 | ~14,500 | CLI framework, .oamod format | 3 |
| v1.6 | ~13,610 | Routing event chain, SSRF guard, self-correction loop | 5 |
| v1.7 | ~23,734 | ActivityChannelHost, SemaphoreSlim skip, [StatelessModule] | 5 |
| v1.8 | ~25,015 | ContractsTypeMap DI, bound service injection, semaphore priority | 7 |

### Top Lessons (Verified Across Milestones)

1. Singleton + event pattern is the right choice for Blazor Server cross-component state (AnimaContext, LanguageService, EditorStateService) — CascadingValue causes re-render storms
2. Name-based type comparison is essential for cross-AssemblyLoadContext scenarios — verified in v1.0 (PluginLoader), v1.4 (CLI validate), v1.5 (module registration)
3. Per-instance isolation requires upfront architectural planning — retrofitting is expensive (v1.3 DI fix, v1.5 runtime container)
4. Wave-based plan execution significantly speeds up milestone delivery when plans are independent
5. TDD with short timeouts and TCS-based completion produces deterministic async tests — verified in v1.6 routing and HTTP modules
6. Update tracking tables immediately after plan completion — stale docs create audit noise (v1.6 lesson)
7. SDK validation via a real external module is the most effective API surface verification — catches binding gaps that unit tests miss (v1.8 lesson)
8. Gap closure plans are healthy — catching issues in verification and fixing in a dedicated plan is better than ignoring (v1.8 lesson)
