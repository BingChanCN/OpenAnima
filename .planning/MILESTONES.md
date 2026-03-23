# Milestones

## v2.0.1 Provider Registry & Living Memory (Shipped: 2026-03-23)

**Phases:** 50-57 | **Plans:** 16 | **Tasks:** ~30 | **LOC:** ~2,000 C# added (+26,600 insertions)
**Git range:** feat(50-01)..docs(phase-57) | **Timeline:** 2026-03-22 → 2026-03-23 (2 days)

**Delivered:** UI-driven LLM provider registry with encrypted credentials, automatic memory recall pipeline with bounded prompt injection, tool-aware memory operations, living memory sedimentation with provenance-backed snapshot history, and memory review surfaces — enabling agents to accumulate and reuse knowledge safely.

**Key accomplishments:**

- Global LLM Provider Registry with AES-GCM encrypted API key storage, full CRUD UI on Settings page, connection testing, and safe disable/delete with impact surfacing
- LLM module cascading provider/model dropdown selection with three-layer config precedence (provider-backed > manual > global) and manual fallback
- Automatic memory recall pipeline: boot injection at run start, disclosure trigger matching, glossary keyword matching (Aho-Corasick) — ranked, deduplicated, bounded XML prompt injection
- Tool-aware memory operations: memory_recall and memory_link IWorkspaceTools with XML descriptor injection into LLM system messages
- Living memory sedimentation: fire-and-forget LLM extraction of stable knowledge into provenance-backed memory nodes with snapshot versioning
- Memory review surfaces on /memory: snapshot history diff viewer (LCS line-level), provenance StepRecord expansion, relationship edge browsing with clickable navigation
- Full test suite: 603/603 green, zero regressions across all 8 phases

**Tech debt (accepted):** 6 items — LLMProviderRegistryService.InitializeAsync not called at startup (self-heals on /settings visit), LLMModelInfo lacks IsEnabled field (provider-level disable covers primary case), 4 sets of human UI verification pending. See v2.0.1-MILESTONE-AUDIT.md.

**Archive:** [milestones/v2.0.1-ROADMAP.md](milestones/v2.0.1-ROADMAP.md) | [milestones/v2.0.1-REQUIREMENTS.md](milestones/v2.0.1-REQUIREMENTS.md) | [milestones/v2.0.1-MILESTONE-AUDIT.md](milestones/v2.0.1-MILESTONE-AUDIT.md)

---

## v2.0 Structured Cognition Foundation (Shipped: 2026-03-21)

**Phases:** 45-49 | **Plans:** 18 | **Tasks:** ~40 | **LOC:** ~11,234 C# added (+11,234 insertions)
**Git range:** feat(45-01)..feat(49-03) | **Timeline:** 2026-03-20 → 2026-03-21 (2 days)

**Delivered:** Structured cognition developer-agent foundation — durable task runtime with SQLite persistence and convergence control, 15 workspace and memory tools for repo-grounded execution, run inspector with propagation chain visualization, provenance-backed artifact and memory graph, and graph-native workflow presets for end-to-end codebase analysis.

**Key accomplishments:**

- Durable task runtime with SQLite persistence, run lifecycle engine (create/start/pause/resume/cancel/fail), convergence guard with configurable step budgets, and /runs UI page with real-time SignalR updates
- 12 workspace tools (file_read, file_write, directory_list, file_search, grep_search, git_status, git_diff, git_log, git_show, git_commit, git_checkout, shell_exec) + 3 memory tools with CommandBlacklistGuard safety
- Run inspector at /runs/{RunId} with mixed chronological timeline, accordion step detail, PropagationColorAssigner chain visualization, TimelineFilterBar, and click-to-highlight causality tracing
- Artifact store with ArtifactFileWriter path safety and provenance-backed memory graph with GlossaryIndex (Aho-Corasick), DisclosureMatcher, snapshot versioning, and /memory UI page
- Graph-native structured cognition: JoinBarrierModule fan-in, PropagationId carry-through, WorkflowPresetService with codebase analysis preset, WorkflowProgressBar, and WorkflowPresetSelector UI
- Full test suite: 495/495 green, zero regressions across all 5 phases

**Tech debt (accepted):** 11 items — BootMemoryInjector not called from run-start path, GetToolDescriptors() not consumed by LLM, WorkflowProgressBar imprecise fraction, SUMMARY frontmatter documentation gaps, pre-existing CS0618 warnings. See v2.0-MILESTONE-AUDIT.md.

**Archive:** [milestones/v2.0-ROADMAP.md](milestones/v2.0-ROADMAP.md) | [milestones/v2.0-REQUIREMENTS.md](milestones/v2.0-REQUIREMENTS.md) | [milestones/v2.0-MILESTONE-AUDIT.md](milestones/v2.0-MILESTONE-AUDIT.md)

---

## v1.9 Event-Driven Propagation Engine (Shipped: 2026-03-20)

**Phases:** 42-44 | **Plans:** 6 | **Tasks:** 12 | **LOC:** ~2,457 C# added (+3,270 insertions)
**Git range:** feat(42-02)..docs(v1.9) | **Timeline:** 2026-03-19 → 2026-03-20 (2 days)

**Delivered:** Event-driven propagation engine — modules execute on data arrival with output fan-out, cyclic topologies supported, HeartbeatModule refactored to standalone timer with config-schema-driven sidebar rendering.

**Key accomplishments:**

- Replaced DAG topological sort with event-driven per-module SemaphoreSlim routing — modules execute immediately on data arrival, output fans out to all connected downstream ports
- Cyclic wiring topologies accepted — ConnectionGraph no longer rejects cycles, enabling feedback loops in module networks
- HeartbeatModule refactored to standalone PeriodicTimer with config-driven interval (50ms floor) — no longer drives WiringEngine execution loop
- ITickable interface removed from Contracts — pure data-driven execution model, all modules execute via port events
- ModuleSchemaService + EditorConfigSidebar schema-aware rendering — IModuleConfigSchema modules show default fields without prior persistence
- Full test suite: 394/394 green, zero regressions across all 3 phases

**Known gaps (accepted):** BEAT-05 missing formal VERIFICATION.md (procedural gap — 5 unit tests, UAT 4/4 passed, VALIDATION.md Nyquist-compliant)

**Archive:** [milestones/v1.9-ROADMAP.md](milestones/v1.9-ROADMAP.md) | [milestones/v1.9-REQUIREMENTS.md](milestones/v1.9-REQUIREMENTS.md) | [milestones/v1.9-MILESTONE-AUDIT.md](milestones/v1.9-MILESTONE-AUDIT.md)

---

## v1.8 SDK Runtime Parity (Shipped: 2026-03-18)

**Phases:** 38-41 | **Plans:** 8 | **Tasks:** 15 | **LOC:** ~1,280 C# added (+6,776 insertions)
**Git range:** feat(38-01)..docs(phase-41) | **Timeline:** 2026-03-17 → 2026-03-18 (2 days)

**Delivered:** External module SDK parity — PluginLoader DI injection, per-Anima module storage, structured message input, and a real external ContextModule that validates the full SDK surface end-to-end with multi-turn conversation history.

**Key accomplishments:**

- PluginLoader DI-aware constructor resolution via FullName matching across AssemblyLoadContext boundaries — external modules receive IModuleConfig, IModuleContext, IEventBus, ICrossAnimaRouter, ILogger, IModuleStorage
- ChatMessageInput migrated from Core.LLM to Contracts with SerializeList/DeserializeList helpers; Core retains using alias for backward compatibility
- LLMModule messages input port with semaphore-based priority rule — multi-turn conversation support via structured message list
- IModuleStorage interface with per-Anima per-Module persistent storage paths, auto-created directories, and bound instance injection for external modules
- External ContextModule — real .oamod module maintaining conversation history, persisting to DataDirectory/history.json, restoring on restart, with per-Anima isolation
- Full test suite: 389/389 green, zero regressions across all 4 phases

**Tech debt (accepted):** Nyquist validation partial across all 4 phases (VALIDATION.md exists but nyquist_compliant: false), SUMMARY frontmatter missing requirements_completed for MSG-01/02/03 and STOR-01

**Archive:** [milestones/v1.8-ROADMAP.md](milestones/v1.8-ROADMAP.md) | [milestones/v1.8-REQUIREMENTS.md](milestones/v1.8-REQUIREMENTS.md) | [milestones/v1.8-MILESTONE-AUDIT.md](milestones/v1.8-MILESTONE-AUDIT.md)

---

## v1.7 Runtime Foundation (Shipped: 2026-03-16)

**Phases:** 32-37 | **Plans:** 13 | **Tasks:** 26 | **LOC:** ~10,100 C# added (+14,079 insertions)
**Git range:** feat(32-01)..docs(phase-37) | **Timeline:** 2026-03-14 → 2026-03-16 (3 days)

**Delivered:** Hardened runtime foundation — race-free module execution, per-Anima Activity Channel serialization model, Contracts-first module API surface, and full built-in module decoupling — completing the architectural prerequisites for external module parity.

**Key accomplishments:**

- Race-free module execution via ConcurrentDictionary, local variable capture, and SemaphoreSlim(1,1) skip-when-busy guards across WiringEngine and 5 modules
- ActivityChannelHost with 3 unbounded named channels (heartbeat/chat/routing) — serial within each channel, parallel between channels, with [StatelessModule] attribute for concurrent dispatch classification
- 9 new contract types in OpenAnima.Contracts (IModuleConfig, IModuleContext, IModuleConfigSchema, ICrossAnimaRouter + routing companions) — external modules achieve feature parity via Contracts-only dependency
- 12 active built-in modules migrated to Contracts-first APIs; LLMModule keeps only the documented Core.LLM exception
- ChatInputModule wired through ActivityChannelHost chat channel for production serial execution guarantee (CONC-05/CONC-06 gap closure)
- Full test suite: 337/337 green, zero regressions across all 6 phases

**Tech debt (accepted):** ANIMA-08 global singleton kept for DI, ILLMService remains in Core (requires ChatMessageInput move), Nyquist validation partial across phases, IModuleConfigSchema has no production consumer yet

**Archive:** [milestones/v1.7-ROADMAP.md](milestones/v1.7-ROADMAP.md) | [milestones/v1.7-REQUIREMENTS.md](milestones/v1.7-REQUIREMENTS.md) | [milestones/v1.7-MILESTONE-AUDIT.md](milestones/v1.7-MILESTONE-AUDIT.md)

---

## v1.6 Cross-Anima Routing (Shipped: 2026-03-14)

**Phases:** 28-31 | **Plans:** 8 | **Tasks:** 16 | **LOC:** ~4,910 C# added (+4,910 insertions)
**Git range:** feat(28-01)..docs(phase-31) | **Timeline:** 2026-03-11 → 2026-03-14 (3 days)

**Delivered:** Cross-Anima request-response routing with LLM-driven service discovery, automatic prompt injection, XML format detection with self-correction, and an HTTP request tool module with SSRF protection — enabling multi-agent collaboration through deterministic wiring.

**Key accomplishments:**

- CrossAnimaRouter singleton with compound-key port registry, Guid correlation IDs, configurable timeout, periodic cleanup, and Anima deletion lifecycle hooks
- Three routing modules (AnimaInputPort, AnimaOutputPort, AnimaRoute) with end-to-end request-response across separate Anima EventBuses, cascading dropdown config UI
- LLMModule auto-injects service descriptions and routing format instructions; FormatDetector parses XML markers with self-correction retry loop (up to 2 retries)
- HttpRequestModule with IHttpClientFactory resilience pipeline, SsrfGuard IP blocking (all private/loopback/link-local ranges), 10s timeout, and configurable method/headers/body sidebar UI
- ModuleEvent.Metadata transport layer for correlationId passthrough across module boundaries
- 53+ routing tests, 25 format detection tests, 23 HTTP tests — zero regressions to full suite

**Tech debt (accepted):** REQUIREMENTS.md tracking table stale for FMTD-01/02/04, PROMPT-02 requirement text stale (user pivoted to no cap), hardcoded event name strings in LLMModule dispatch, 3 pre-existing test failures

**Archive:** [milestones/v1.6-ROADMAP.md](milestones/v1.6-ROADMAP.md) | [milestones/v1.6-REQUIREMENTS.md](milestones/v1.6-REQUIREMENTS.md) | [milestones/v1.6-MILESTONE-AUDIT.md](milestones/v1.6-MILESTONE-AUDIT.md)

---

## v1.5 Multi-Anima Architecture (Shipped: 2026-03-09)

**Phases:** 23-27 | **Plans:** 13 | **LOC:** ~6,600 C#/Razor/CSS added (+11,753 insertions)
**Git range:** feat(23-01)..feat(27-02) | **Timeline:** 2026-02-28 → 2026-03-02 (3 days)

**Delivered:** Multi-instance Anima architecture with independent runtimes, full Chinese/English i18n, module management UI, per-module configuration, and rich built-in modules — transforming from a single-runtime dashboard to a multi-agent platform.

**Key accomplishments:**

- Multi-Anima architecture: Create, list, switch, delete, rename, clone independent Anima instances with isolated state
- Per-Anima runtime isolation: Each Anima runs independent HeartbeatLoop, WiringEngine, and EventBus
- Full i18n: Chinese/English UI with LanguageService, .resx resources, persistent preferences, all components localized
- Module management: Card-layout UI with .oamod installation, per-Anima enable/disable, context menu, detail sidebar
- Module configuration: EditorConfigSidebar with metadata display, typed config form (text/textarea/password), auto-save, validation
- Built-in modules: FixedText (template interpolation), TextJoin, TextSplit, ConditionalBranch (expression evaluator), configurable LLM with per-Anima API overrides

**Known gaps (accepted):** ANIMA-08 (independent module instances — global singleton kept for DI), MODMGMT-01/02/03/06 (full install/uninstall/search deferred — card UI with .oamod install implemented)

**Archive:** [milestones/v1.5-ROADMAP.md](milestones/v1.5-ROADMAP.md) | [milestones/v1.5-REQUIREMENTS.md](milestones/v1.5-REQUIREMENTS.md) | [milestones/v1.5-MILESTONE-AUDIT.md](milestones/v1.5-MILESTONE-AUDIT.md)

---

## v1.4 Module SDK & DevEx (Shipped: 2026-02-28)

**Phases:** 20-22 | **Plans:** 8 | **Tasks:** 16 | **LOC:** ~1,747 C# CLI + ~1,408 lines docs
**Git range:** feat(20-02)..feat(21-03) | **Timeline:** 2026-02-28 (1 day)

**Delivered:** Complete module SDK with CLI tool, project templates, packaging system, and comprehensive documentation — developers can create, validate, and pack custom modules in under 5 minutes.

**Key accomplishments:**

- Installable .NET global tool (oani) with System.CommandLine, exit codes, and verbosity control
- Module validation with manifest JSON checking and IModule implementation verification via isolated assembly reflection
- Pack command creates .oamod ZIP archives with MD5 checksums and target framework metadata
- 5-minute quick-start tutorial showing complete create-build-pack workflow with HelloModule example
- Complete API reference documentation for all public interfaces (IModule, IModuleExecutor, ITickable, IEventBus, port system)

**Tech debt (accepted):** Schema mismatch between CLI and Runtime (extended manifest fields generated but not consumed), SUMMARY metadata gaps (documentation only), test isolation issues (pre-existing infrastructure issue)

**Archive:** [milestones/v1.4-ROADMAP.md](milestones/v1.4-ROADMAP.md) | [milestones/v1.4-REQUIREMENTS.md](milestones/v1.4-REQUIREMENTS.md) | [milestones/v1.4-MILESTONE-AUDIT.md](milestones/v1.4-MILESTONE-AUDIT.md)

---

## v1.0 Core Platform Foundation (Shipped: 2026-02-21)

**Phases:** 1-2 | **Plans:** 5 | **Tasks:** 10 | **LOC:** 1,323 C#
**Git range:** cd4670a..f690d50 | **Timeline:** 2026-02-21

**Delivered:** Modular plugin runtime with isolated assembly loading, typed contracts, event-driven communication, and a 100ms heartbeat loop — the foundation for proactive agent behavior.

**Key accomplishments:**

- Typed module contracts (IModule, IModuleMetadata, generic IModuleInput/IModuleOutput)
- Isolated plugin loading via AssemblyLoadContext with hot directory discovery
- Thread-safe module registry with cross-context name-based type handling
- Lock-free event bus with dynamic subscription and parallel handler dispatch
- 100ms heartbeat loop with PeriodicTimer and anti-snowball protection
- End-to-end pipeline: scan → load → register → inject EventBus → heartbeat → pub/sub

**Tech debt (6 items):** See milestones/v1.0-MILESTONE-AUDIT.md

**Archive:** [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md) | [milestones/v1.0-REQUIREMENTS.md](milestones/v1.0-REQUIREMENTS.md)

---

## v1.1 WebUI Runtime Dashboard (Shipped: 2026-02-23)

**Phases:** 3-7 | **Plans:** 10 | **Tasks:** 8 | **LOC:** 3,741 C#/Razor/CSS (+2,951 from v1.0)
**Git range:** 990c943..5db8eb4 | **Timeline:** 2026-02-22 → 2026-02-23

**Delivered:** Real-time web-based monitoring and control dashboard for the OpenAnima runtime — Blazor Server with SignalR push, module management, heartbeat monitoring, and a complete desktop app experience.

**Key accomplishments:**

- Converted runtime to Blazor Server web host with service facades and browser auto-launch
- Dark-themed responsive dashboard with collapsible sidebar, module list, and heartbeat status
- SignalR real-time push with per-tick latency tracking and sparkline visualization
- Control operations: load/unload modules, start/stop heartbeat from browser UI
- UX polish: confirmation dialogs for destructive ops, connection status indicator
- xUnit test suite with memory leak detection and performance validation

**Tech debt (7 items):** See milestones/v1.1-MILESTONE-AUDIT.md

**Archive:** [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md) | [milestones/v1.1-REQUIREMENTS.md](milestones/v1.1-REQUIREMENTS.md)

---

## v1.2 LLM Integration (Shipped: 2026-02-25)

**Phases:** 8-10 | **Plans:** 6 | **Tasks:** 12 | **LOC:** 6,352 C#/Razor/CSS/JS (+2,611 from v1.1)
**Git range:** f0855ec..d98df09 | **Timeline:** 2026-02-24 → 2026-02-25

**Delivered:** LLM conversation capability — OpenAI-compatible API client with streaming, real-time chat UI with Markdown rendering, and context window management with token tracking and send blocking.

**Key accomplishments:**

- OpenAI-compatible LLM API client with streaming, comprehensive error handling, and SDK built-in retry
- Real-time chat UI with token-by-token streaming, role-based styling, and auto-scroll
- Markdown rendering with syntax highlighting, copy-to-clipboard, and regenerate last response
- Token counting with SharpToken and cumulative usage tracking (input/output/total)
- Context capacity monitoring with color-coded thresholds (70%/85%/90%) and send blocking
- EventBus chat events for module integration (message sent, response received, context limit)

**Archive:** [milestones/v1.2-ROADMAP.md](milestones/v1.2-ROADMAP.md) | [milestones/v1.2-REQUIREMENTS.md](milestones/v1.2-REQUIREMENTS.md)

---

## v1.3 True Modularization & Visual Wiring (Shipped: 2026-02-28)

**Phases:** 11-19 + 12.5 | **Plans:** 21 | **Tasks:** 65+ | **LOC:** ~4,800 C#/Razor/JS added
**Git range:** 1aa43fb..03ae354 | **Timeline:** 2026-02-25 → 2026-02-28 (4 days)

**Delivered:** Visual drag-and-drop wiring editor with port-based module connections — transform hardcoded LLM/chat/heartbeat into modular architecture with topological execution, cycle detection, and real-time status monitoring.

**Key accomplishments:**

- Port type system (Text, Trigger) with color-coded visual distinction and connection validation
- Wiring engine with Kahn's algorithm for topological execution and cycle detection
- Visual HTML5/SVG editor with pan/zoom, bezier connections, and auto-save
- Module refactoring: LLMModule, ChatInputModule, ChatOutputModule, HeartbeatModule
- Runtime integration with SignalR status push and startup initialization
- E2E verified: User can wire ChatInput→LLM→ChatOutput and have working conversation

**Tech debt:** None — all requirements verified with evidence

**Archive:** [milestones/v1.3-ROADMAP.md](milestones/v1.3-ROADMAP.md) | [milestones/v1.3-REQUIREMENTS.md](milestones/v1.3-REQUIREMENTS.md) | [milestones/v1.3-MILESTONE-AUDIT.md](milestones/v1.3-MILESTONE-AUDIT.md)

---
