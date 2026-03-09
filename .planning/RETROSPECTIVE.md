# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

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

### Process Evolution

| Milestone | Commits | Phases | Key Change |
|-----------|---------|--------|------------|
| v1.0 | ~15 | 2 | Initial TDD-first approach |
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
| v1.5 | ~21,155 | AnimaRuntime, i18n, config UI | 5 |

### Top Lessons (Verified Across Milestones)

1. Singleton + event pattern is the right choice for Blazor Server cross-component state (AnimaContext, LanguageService, EditorStateService) — CascadingValue causes re-render storms
2. Name-based type comparison is essential for cross-AssemblyLoadContext scenarios — verified in v1.0 (PluginLoader), v1.4 (CLI validate), v1.5 (module registration)
3. Per-instance isolation requires upfront architectural planning — retrofitting is expensive (v1.3 DI fix, v1.5 runtime container)
4. Wave-based plan execution significantly speeds up milestone delivery when plans are independent
