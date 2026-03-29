# OpenAnima

## Current Milestone: v2.0.4 Intelligent Memory & Persistence

**Goal:** Overhaul the memory system with graph-based architecture, LLM-guided recall, and first-person memory CRUD; fix platform persistence and chat resilience.

**Target features:**
- Memory data model refactor (Node/Memory/Edge/Path four-layer separation)
- LLM-driven graph exploration recall (configurable model, dynamic depth)
- First-person memory CRUD (AI decides what to remember)
- Wiring layout and chat history persistence across restarts
- Background chat execution (survives page navigation)
- Memory operation visibility in chat interface

## Current State

**Latest shipped:** v2.0.3 Editor Experience (2026-03-24)
**Current milestone:** v2.0.4 Intelligent Memory & Persistence
**Milestones complete:** v1.0-v2.0.3 (14 milestones, 64 phases, 149 plans)
**Codebase:** ~52,000 LOC (C#, Razor, CSS, JS) | 662 tests green
**Phase 65 complete:** Memory schema migration — 4-table model (memory_nodes/UUID PK, memory_contents, memory_edges/UUID refs, memory_uri_paths), atomic migration with backup/rollback, MemoryGraph rewrite

## What This Is

A local-first, modular AI agent platform for Windows that lets developers and non-technical users build their own "digital life forms / assistants." Users create multiple independent Anima instances — each with its own heartbeat, module wiring, chat interface, and configuration. Agents are proactive — they think, act, and initiate on their own — while remaining controllable through typed module interfaces and deterministic wiring. The platform provides a C# core runtime with a web-based dashboard, visual drag-and-drop wiring editor, LLM-powered chat with UI-driven provider/model registry, autonomous agent loop (tool calling with think-act-observe cycle), durable task runtime with workspace tools, run inspection with propagation chain visualization, provenance-backed memory graph with automatic recall and living memory sedimentation, graph-native workflow presets, and full Chinese/English internationalization.

## Core Value

Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

## Requirements

### Validated

- ✓ C# modules loaded as in-process assemblies via AssemblyLoadContext with isolation (MOD-01) — v1.0
- ✓ Typed module contracts with declared input/output interfaces (MOD-02) — v1.0
- ✓ Zero-config module installation — download package and load without manual setup (MOD-03) — v1.0
- ✓ MediatR-based event bus for inter-module communication (MOD-04) — v1.0
- ✓ Module registry for discovering and managing loaded modules (MOD-05) — v1.0
- ✓ Code-based heartbeat loop running at ≤100ms intervals (RUN-03) — v1.0
- ✓ Runtime launches as Blazor Server web app with browser auto-launch (INFRA-01, INFRA-03) — v1.1
- ✓ Real-time state push via SignalR without manual refresh (INFRA-02) — v1.1
- ✓ Module status display: loaded modules list, metadata, status indicators (MOD-06, MOD-07) — v1.1
- ✓ Load/unload modules from dashboard with error display (MOD-08, MOD-09, MOD-10) — v1.1
- ✓ Heartbeat monitoring: running state, tick count, per-tick latency, real-time updates (BEAT-01~04) — v1.1
- ✓ Responsive dashboard layout (UI-01) — v1.1
- ✓ UX polish: confirmation dialogs, connection status indicator — v1.1
- ✓ LLM API client via OpenAI-compatible endpoint with streaming and error handling (LLM-01~05) — v1.2
- ✓ Chat UI with streaming responses, Markdown rendering, copy, regenerate (CHAT-01~07) — v1.2
- ✓ Token counting, context capacity tracking, send blocking, EventBus events (CTX-01~04) — v1.2
- ✓ Port type system with visual distinction and connection validation (PORT-01~04) — v1.3
- ✓ Wiring engine with topological execution and cycle detection (WIRE-01~03) — v1.3
- ✓ Visual drag-and-drop editor with pan/zoom, connections, save/load (EDIT-01~06) — v1.3
- ✓ Refactored modules: LLMModule, ChatInputModule, ChatOutputModule, HeartbeatModule (RMOD-01~04) — v1.3
- ✓ Real-time module status display in editor (RTIM-01~02) — v1.3
- ✓ End-to-end conversation via module wiring (E2E-01) — v1.3
- ✓ Developer can create new module project with `oani new <ModuleName>` command (SDK-01) — v1.4
- ✓ Developer can specify output directory with `oani new <ModuleName> -o <path>` option (SDK-02) — v1.4
- ✓ Developer can preview generated files with `oani new <ModuleName> --dry-run` option (SDK-03) — v1.4
- ✓ Generated module project compiles without errors (SDK-04) — v1.4
- ✓ Generated module implements IModule and IModuleMetadata interfaces (SDK-05) — v1.4
- ✓ Developer can install oani CLI as .NET global tool (CLI-01) — v1.4
- ✓ Developer can run `oani --help` to see available commands (CLI-02) — v1.4
- ✓ CLI returns exit code 0 on success, non-zero on failure (CLI-03) — v1.4
- ✓ CLI outputs errors to stderr, normal output to stdout (CLI-04) — v1.4
- ✓ Developer can set verbosity level with `-v` or `--verbosity` option (CLI-05) — v1.4
- ✓ Developer can pack module with `oani pack <path>` command (PACK-01) — v1.4
- ✓ Pack command produces .oamod file containing module.json, DLL, and assets (PACK-02) — v1.4
- ✓ Pack command builds module project before packing (unless --no-build) (PACK-03) — v1.4
- ✓ Developer can specify output directory with `oani pack <path> -o <path>` option (PACK-04) — v1.4
- ✓ Pack command includes SHA256 checksum in package manifest (PACK-05) — v1.4
- ✓ Packed module can be loaded by OpenAnima runtime without modification (PACK-06) — v1.4
- ✓ Developer can validate module with `oani validate <path>` command (VAL-01) — v1.4
- ✓ Validate command checks module.json exists and is valid JSON (VAL-02) — v1.4
- ✓ Validate command checks required manifest fields (id, version, name) (VAL-03) — v1.4
- ✓ Validate command verifies module implements IModule interface (VAL-04) — v1.4
- ✓ Validate command reports all errors, not just first error (VAL-05) — v1.4
- ✓ module.json supports id, version, name, description, author fields (MAN-01) — v1.4
- ✓ module.json supports openanima version compatibility (minVersion, maxVersion) (MAN-02) — v1.4
- ✓ module.json supports port declarations (inputs, outputs) (MAN-03) — v1.4
- ✓ Manifest validation rejects invalid JSON with clear error messages (MAN-04) — v1.4
- ✓ Manifest schema is versioned for future compatibility (MAN-05) — v1.4
- ✓ Developer can specify module type with `--type` option (default: standard) (TEMP-01) — v1.4
- ✓ Developer can specify input ports with `--inputs` option (e.g., --inputs Text,Trigger) (TEMP-02) — v1.4
- ✓ Developer can specify output ports with `--outputs` option (e.g., --outputs Text) (TEMP-03) — v1.4
- ✓ Template generates port attributes based on specified ports (TEMP-04) — v1.4
- ✓ Template generates working ExecuteAsync method with port handling stubs (TEMP-05) — v1.4
- ✓ Developer can read quick-start guide showing create-build-pack workflow (DOC-01) — v1.4
- ✓ Quick-start guide produces working module in under 5 minutes (DOC-02) — v1.4
- ✓ API reference documents all public interfaces (IModule, IModuleExecutor, IEventBus) (DOC-03) — v1.4
- ✓ API reference documents port system (PortType, PortMetadata, InputPortAttribute, OutputPortAttribute) (DOC-04) — v1.4
- ✓ API reference includes code examples for common patterns (DOC-05) — v1.4
- ✓ User can create new Anima with custom name (ANIMA-01) — v1.5
- ✓ User can view list of all Animas in global sidebar (ANIMA-02) — v1.5
- ✓ User can switch between different Animas (ANIMA-03) — v1.5
- ✓ User can delete Anima (ANIMA-04) — v1.5
- ✓ User can rename Anima (ANIMA-05) — v1.5
- ✓ User can clone existing Anima (ANIMA-06) — v1.5
- ✓ Each Anima has independent heartbeat loop (ANIMA-07) — v1.5
- ✓ Each Anima has independent chat interface (ANIMA-09) — v1.5
- ✓ Anima configuration persists across sessions (ANIMA-10) — v1.5
- ✓ User can switch UI language between Chinese and English (I18N-01) — v1.5
- ✓ All UI text displays in selected language (I18N-02) — v1.5
- ✓ Language preference persists across sessions (I18N-03) — v1.5
- ✓ Missing translations fall back to English (I18N-04) — v1.5
- ✓ User can enable/disable module per Anima (MODMGMT-04) — v1.5
- ✓ User can view module information (MODMGMT-05) — v1.5
- ✓ User can click module in editor to show detail panel (MODCFG-01) — v1.5
- ✓ User can edit module-specific configuration in detail panel (MODCFG-02) — v1.5
- ✓ Module configuration persists per Anima (MODCFG-03) — v1.5
- ✓ Configuration changes validate before saving (MODCFG-04) — v1.5
- ✓ Detail panel shows module status and metadata (MODCFG-05) — v1.5
- ✓ Fixed text module outputs configurable text content (BUILTIN-01) — v1.5
- ✓ User can edit fixed text content in detail panel (BUILTIN-02) — v1.5
- ✓ Text concat module concatenates two text inputs (BUILTIN-03) — v1.5
- ✓ Text split module splits text by delimiter (BUILTIN-04) — v1.5
- ✓ Text merge module merges multiple inputs (BUILTIN-05) — v1.5
- ✓ Conditional branch module routes based on condition expression (BUILTIN-06) — v1.5
- ✓ LLM module allows configuration of API URL in detail panel (BUILTIN-07) — v1.5
- ✓ LLM module allows configuration of API key in detail panel (BUILTIN-08) — v1.5
- ✓ LLM module allows configuration of model name in detail panel (BUILTIN-09) — v1.5
- ✓ Heartbeat module is optional (BUILTIN-10) — v1.5
- ✓ CrossAnimaRouter with port registry, correlation IDs, timeout, lifecycle hooks (ROUTE-01~06) — v1.6
- ✓ AnimaInputPort, AnimaOutputPort, AnimaRoute modules with E2E routing (RMOD-01~08) — v1.6
- ✓ LLM prompt auto-injection with service descriptions and routing format (PROMPT-01~04) — v1.6
- ✓ FormatDetector XML marker parsing with self-correction loop (FMTD-01~04) — v1.6
- ✓ HttpRequestModule with IHttpClientFactory resilience, SSRF guard, timeout (HTTP-01~05) — v1.6
- ✓ AnimaRuntimeManager manages all Anima instances (ARCH-01) — v1.5
- ✓ AnimaContext identifies current Anima for scoped services (ARCH-02) — v1.5
- ✓ Each Anima has isolated EventBus instance (ARCH-03) — v1.5
- ✓ Each Anima has isolated WiringEngine instance (ARCH-04) — v1.5
- ✓ Configuration files stored per Anima in separate directories (ARCH-05) — v1.5
- ✓ Service disposal prevents memory leaks (ARCH-06) — v1.5
- ✓ PluginLoader DI-aware constructor resolution for external modules (PLUG-01) — v1.8
- ✓ PluginLoader typed ILogger via ILoggerFactory for external modules (PLUG-02) — v1.8
- ✓ Optional/required parameter handling in PluginLoader DI (PLUG-03) — v1.8
- ✓ ChatMessageInput migrated to Contracts with SerializeList/DeserializeList (MSG-01, MSG-03) — v1.8
- ✓ LLMModule messages input port with priority rule for multi-turn conversations (MSG-02) — v1.8
- ✓ IModuleStorage per-Anima per-Module persistent storage paths (STOR-01) — v1.8
- ✓ External ContextModule with conversation history and persistence (ECTX-01, ECTX-02) — v1.8
- ✓ Event-driven propagation engine with per-module SemaphoreSlim routing (PROP-01, PROP-02) — v1.9
- ✓ Cyclic wiring topologies accepted (PROP-03) — v1.9
- ✓ Modules terminate propagation by not producing output (PROP-04) — v1.9
- ✓ HeartbeatModule standalone PeriodicTimer signal source (BEAT-05) — v1.9
- ✓ HeartbeatModule interval configurable via EditorConfigSidebar schema rendering (BEAT-06) — v1.9
- ✓ ITickable interface removed — pure data-driven execution model — v1.9
- ✓ Durable task runtime with stable run identity, SQLite persistence, resume/cancel lifecycle, and convergence bounds (RUN-01~05, CTRL-01~02) — v2.0
- ✓ Workspace-aware developer tool surface: 12 file/git/shell tools + 3 memory tools with CommandBlacklistGuard safety (WORK-01~05) — v2.0
- ✓ Run inspector with per-step timeline, inputs/outputs, errors, propagation chain visualization, and log correlation (OBS-01~04) — v2.0
- ✓ Artifact store and provenance-backed memory graph with GlossaryIndex, DisclosureMatcher, and /memory UI (ART-01~02, MEM-01~03) — v2.0
- ✓ Structured cognition workflows: JoinBarrier fan-in, PropagationId tracking, workflow presets, codebase analysis E2E (COG-01~04) — v2.0
- ✓ Global LLM Provider registry with AES-GCM encrypted credentials, two-layer Provider > Model hierarchy, CRUD UI on Settings page (PROV-01~10) — v2.0.1
- ✓ LLM module cascading provider/model dropdown selection with three-layer config precedence and manual fallback (LLMN-01~05) — v2.0.1
- ✓ Automatic memory recall: boot injection, disclosure triggers, glossary keywords with ranked/deduped/bounded prompt injection (MEMR-01~05) — v2.0.1
- ✓ Tool-aware memory: memory_recall and memory_link tools with XML descriptor injection (TOOL-01~04) — v2.0.1
- ✓ Living memory sedimentation: auto-extraction from LLM exchanges into provenance-backed memory nodes with snapshot history (LIVM-01~04) — v2.0.1
- ✓ Memory review surfaces: snapshot diff viewer, provenance inspection, relationship edge browsing on /memory (MEMUI-01~03) — v2.0.1
- ✓ Agent loop core: ToolCallParser XML marker extraction, AgentToolDispatcher direct dispatch, bounded iteration loop with configurable limit (max 50), system prompt tool-call syntax injection, cancellation safety (LOOP-01~07) — v2.0.2
- ✓ Tool call display: real-time collapsible tool cards in chat bubbles, "Used N tools" badge, per-event resettable timeout, send locking, cancel button (TCUI-01~04) — v2.0.2
- ✓ Agent loop hardening: StepRecorder bracket steps (AgentLoop/AgentIteration), token budget management (70% of agentContextWindowSize), full-history sedimentation wiring with tool message truncation (HARD-01~03) — v2.0.2
- ✓ Module i18n: localized display names in palette, node cards, and config sidebar with live language switching (EDUX-01) — v2.0.3
- ✓ Module descriptions in config sidebar and palette hover tooltips (EDUX-02, EDUX-05) — v2.0.3
- ✓ Connection deletion via right-click context menu and Delete key with focus guard (EDUX-03) — v2.0.3
- ✓ Port hover tooltips with Chinese descriptions on all built-in module ports (EDUX-04) — v2.0.3

### Active

- [ ] Memory data model refactored to Node/Memory/Edge/Path four-layer separation (inspired by Nocturne Memory)
- [ ] LLM-driven graph exploration recall with configurable model and dynamic depth
- [ ] First-person memory CRUD tools (create/update/delete/organize)
- [ ] Improved sedimentation quality (bilingual keywords, broader triggers)
- [ ] Wiring layout persists across application restarts
- [ ] Chat history persists across application restarts
- [ ] LLM execution continues in background when navigating away from chat
- [ ] Memory operations (create/update) visible in chat interface in real-time

### Deferred

- [ ] Each Anima has independent module instances (ANIMA-08 — global singleton kept for DI compatibility)
- [ ] User can view list of all installed modules (MODMGMT-01)
- [ ] User can install module from .oamod package (MODMGMT-02)
- [ ] User can uninstall module (MODMGMT-03)
- [ ] User can search and filter modules by name (MODMGMT-06)
- [ ] Propagation convergence control (TTL, energy decay, content-based dampening)
- [ ] Dynamic port count (TextJoin fixed 3 ports limitation)
- [ ] LLMProviderRegistryService.InitializeAsync at startup (currently self-heals on /settings visit)
- [ ] LLMModelInfo.IsEnabled for model-level disabled rendering (provider-level disable covers primary case)
- [ ] Edge management tools for LLM agent (memory_link tool exists but no UI-driven edge management)

### Future

- Semantic code intelligence (symbol resolution, refactoring-grade analysis)
- Story/narrative writing workflow presets
- Higher-level agent templates and personas built on graph primitives
- Vector/embedding memory retrieval alongside provenance-backed lexical retrieval
- Explicit autonomy/permission profiles for destructive workspace mutations
- Remote/distributed run execution workers
- Module marketplace discovery, install, and update ecosystem
- Tiered thinking loop (code heartbeat → fast model triage → deep model reasoning)
- Language-agnostic module protocol with typed input/output interfaces
- Additional port types (Stream, Media, etc.) for richer module communication
- Anima background execution (run in background while viewing different Anima)
- Anima execution statistics (uptime, module execution count)
- Module dependency resolution and auto-install
- Additional language support (Japanese, Korean, etc.)

### Out of Scope

- Unity/UE integration — deferred to future milestone, not v1
- Mobile app — Windows desktop first
- Local model hosting (llama.cpp etc.) — v1 uses cloud LLM only, architecture allows future addition
- Module marketplace backend/infrastructure — v1 supports loading local module packages only
- Nested Anima instances — unclear value proposition, high complexity
- Auto-update modules — breaking changes risk, user loses control
- Real-time collaboration — multi-user complexity, single-user focus
- Cloud sync — privacy concerns, local-first principle

## Context

Shipped v2.0.3 with ~52,000 LOC across all source files (C#, Razor, CSS, JS).
Tech stack: .NET 8.0, Blazor Server, SignalR, OpenAI SDK 2.8.0, SharpToken 2.0.4, Markdig 0.41.3, Markdown.ColorCode, System.CommandLine 2.0.0-beta4, Microsoft.Extensions.Http.Resilience 8.7.0, Microsoft.Data.Sqlite 8.0.12, Dapper 2.1.72.
Full test suite: 658/658 green.

v2.0.3 delivered editor experience improvements:
- Module i18n foundation: 15 Module.DisplayName.* resx keys (zh-CN/en-US) with live language switching across palette, node cards, and config sidebar; dual-language search in palette; invariant names preserved for wiring storage
- Connection deletion UX: Fixed DeleteSelected() two-step connection ID parsing, JS interop isActiveElementEditable focus guard, ConnectionContextMenu component with right-click delete and localized label
- Module descriptions: 15 Module.Description.* resx keys wired into EditorConfigSidebar description field and ModulePalette hover tooltips with ResourceNotFound fallback
- Port hover tooltips: 39 Port.Description.* resx keys with browser-native SVG title tooltips on all input/output port circles; HttpRequestModule body port direction disambiguation
- 70 total i18n keys added across 3 resource files with zero namespace collisions

Known tech debt:
- ANIMA-08: Global IEventBus singleton kept for DI — full per-Anima module instances deferred
- MODMGMT-01/02/03/06: Full install/uninstall/search UI deferred
- ILLMService remains in Core (ChatMessageInput moved to Contracts but ILLMService depends on LLMResult + streaming)
- Schema mismatch between CLI and Runtime (extended manifest fields)
- TextJoin fixed 3 input ports — static port system limitation
- LLMProviderRegistryService.InitializeAsync not called at startup (self-heals on /settings visit)
- LLMModelInfo has no IsEnabled field — model-level disabled rendering deferred
- 26+ pre-existing CS0618 deprecation warnings for IAnimaContext/IAnimaModuleConfigService
- HARD-03 cancel iteration step leak: in-flight AgentIteration bracket step not closed on cancellation (low severity)
- Silent fallback when agentEnabled=true but _workspaceToolModule=null — no log warning
- Multi-Anima agent event cross-contamination risk (pre-existing ANIMA-08 limitation)

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| C# core runtime | Strong Windows ecosystem, good performance, Unity/UE compatibility for future | ✓ Good — clean architecture, ~11,000 LOC |
| Dynamic assembly loading for C# modules | Enables "download and run" without separate processes, best performance for C# ecosystem | ✓ Good — AssemblyLoadContext isolation works |
| .slnx format (XML-based solution) | .NET 10 SDK creates .slnx by default; compatible with all dotnet CLI commands | ✓ Good |
| LoadResult record instead of exceptions | Enables caller to decide how to handle failures without try/catch boilerplate | ✓ Good |
| Name-based type discovery | Cross-context type identity solved via interface.FullName comparison | ✓ Good — critical for plugin isolation |
| Duck-typing for ITickable | Reflection-based method lookup solves cross-context type identity for heartbeat | ✓ Good |
| Property injection for EventBus | EventBus injected via setter after module loading, subscription in setter | ✓ Good |
| Lock-free event bus | ConcurrentDictionary + ConcurrentBag with lazy cleanup every 100 publishes | ✓ Good |
| Blazor Server for WebUI | Pure C# full-stack, SignalR built-in for real-time push, seamless .NET runtime integration | ✓ Good — single-project architecture works well |
| Web SDK directly on Core project | No separate web project; OpenAnima.Core is the host | ✓ Good — simpler deployment |
| Pure CSS dark theme (no component library) | Lightweight for monitoring shell, can add MudBlazor later if needed | ✓ Good — fast, no dependencies |
| IHostedService for runtime lifecycle | Clean ASP.NET Core integration for startup/shutdown | ✓ Good |
| Code-behind partial class for SignalR pages | Avoids Razor compiler issues with generic type parameters | ✓ Good — clean separation |
| Throttled UI rendering (every 5th tick) | Prevents jank from 100ms tick frequency | ✓ Good — smooth UX |
| Fire-and-forget Hub push | Avoids blocking the heartbeat tick loop | ✓ Good |
| PluginLoadContext isCollectible: true | Enables assembly unloading for module lifecycle management | ✓ Good |
| Serial operation execution (isOperating flag) | All buttons disable during any operation to prevent race conditions | ✓ Good |
| ConfirmDialog only for destructive ops | Unload/stop get confirmation, load/start don't — reduces friction | ✓ Good |
| OpenAI SDK 2.8.0 for LLM client | Official SDK with built-in retry, streaming, type-safe API | ✓ Good — covers all OpenAI-compatible providers |
| SDK-agnostic ILLMService interface | ChatMessageInput records instead of exposing SDK types | ✓ Good — allows provider swap without consumer changes |
| Inline error tokens in streaming | Yield error messages in stream instead of throwing exceptions | ✓ Good — UI displays errors inline |
| Batched StateHasChanged (50ms/100 chars) | Prevents UI lag during token-by-token streaming | ✓ Good — smooth streaming UX |
| SharpToken for token counting | Accurate tiktoken-compatible counting with cl100k_base fallback | ✓ Good — matches API-returned counts |
| Send blocking over auto-truncation | Block sends at 90% threshold instead of auto-removing messages | ✓ Good — user retains control of conversation |
| SignalR 8.0.x (not 10.x) | Version must match .NET 8 runtime to avoid circuit crashes | ✓ Good — critical compatibility fix |
| Port type categories (Text, Trigger) | Simple two-category system covers current use cases, extensible for future | ✓ Good — v1.3 validates approach |
| Kahn's algorithm for topological sort | Linear O(V+E) complexity, produces level-parallel execution order | ✓ Good — efficient cycle detection |
| SVG-based editor canvas | Native browser rendering, no external dependencies, smooth pan/zoom | ✓ Good — 60fps interactions |
| Module singleton DI registration | Shared state across scopes for consistent module behavior | ✓ Good — EventBus subscriptions persist |
| System.CommandLine for CLI | Industry-standard CLI framework with type-safe parsing and help generation | ✓ Good — v1.4 validates approach |
| Silent-first output | Default verbosity is "quiet" with no output unless errors occur or --verbosity is set | ✓ Good — clean CLI UX |
| Exit code discipline | 0=success, 1=general error, 2=validation error for consistent CLI error reporting | ✓ Good — standard conventions |
| Embedded resources for templates | Templates stored as embedded resources (not file paths) for reliable distribution | ✓ Good — no external dependencies |
| MD5 for checksum algorithm | Sufficient for integrity verification (not cryptographic security) | ✓ Good — fast and adequate |
| In-memory manifest enrichment | Source module.json unchanged, only packed version has checksum/targetFramework | ✓ Good — clean developer experience |
| Name-based type comparison for IModule | Avoids type identity issues across AssemblyLoadContext boundaries | ✓ Good — critical for plugin isolation |
| 8-char hex Anima ID | Guid.NewGuid().ToString("N")[..8] for directory names — readable, collision-free for single-user | ✓ Good — v1.5 |
| AnimaContext as singleton with event | Avoids full layout re-render vs CascadingValue approach | ✓ Good — v1.5 |
| AnimaRuntime container pattern | Per-Anima HeartbeatLoop + WiringEngine + EventBus in single container | ✓ Good — clean isolation |
| LanguageService singleton with Action event | Same pattern as AnimaContext — avoids CascadingValue re-renders | ✓ Good — v1.5 |
| Chinese (zh-CN) as default language | Primary user base is Chinese-speaking | ✓ Good — v1.5 |
| Per-Anima ChatClient per-execution | LLMModule singleton but creates new ChatClient per execution for config isolation | ✓ Good — v1.5 |
| Pragmatic expression evaluator | ~150 LOC recursive descent for ConditionalBranchModule — avoids external dependency | ✓ Good — v1.5 |
| Fixed 3 input ports for TextJoin | Static port system cannot support dynamic port counts without major change | ⚠️ Revisit — limits flexibility |

| Metadata passthrough via Dictionary copy | Prevents aliasing bugs during WiringEngine fan-out deep copy | ✓ Good — v1.6 |
| XML routing markers for LLM format detection | Closest to LLM training-data markup; lenient regex handles 80-95% compliance | ✓ Good — v1.6 |
| SsrfGuard with CIDR bit-level matching | Blocks private/loopback/link-local without third-party IP library | ✓ Good — v1.6 |
| IHttpClientFactory with named client + resilience | Prevents socket exhaustion under heartbeat-driven repeated execution | ✓ Good — v1.6 |
| Self-correction retry loop (MaxRetries=2) | LLM malformed markers get error feedback + format example for re-call | ✓ Good — v1.6 |

| ActivityChannel: Channel.CreateUnbounded<T>() with SingleReader=true | Always TryWrite from tick path — WriteAsync risks deadlock | ✓ Good — v1.7 |
| SemaphoreSlim Wait(0) over WaitAsync() | Synchronous non-blocking TryEnter gives skip-when-busy; WaitAsync() queues callers defeating skip semantics | ✓ Good — v1.7 |
| IModuleConfig.SetConfigAsync per-key (string key, string value) | NOT bulk Dictionary — locked user decision | ✓ Good — v1.7 |
| IModuleContext.ActiveAnimaId is non-nullable string | Platform guarantees initialization before module use | ✓ Good — v1.7 |
| Contracts.Routing sub-namespace | ICrossAnimaRouter + companion types, parallel to existing Contracts.Ports | ✓ Good — v1.7 |
| ModuleMetadataRecord moved to Contracts | Temporary Core.Modules shim inherits from Contracts record for backward compat | ✓ Good — v1.7 |
| SsrfGuard moved to Contracts.Http | Temporary Core.Http shim delegates to Contracts helper | ✓ Good — v1.7 |
| LLMModule keeps OpenAnima.Core.LLM exception | All other module-facing surfaces come from Contracts | ✓ Good — v1.7 |
| ChatInputModule.SetChannelHost is internal | ActivityChannelHost is internal sealed class — InternalsVisibleTo covers test access | ✓ Good — v1.7 |
| Channel-first dispatch uses explicit if/else | Not null-conditional `?.` — fallback behavior is clear and testable | ✓ Good — v1.7 |

| FullName type matching for DI | Cross-AssemblyLoadContext type resolution uses FullName string comparison (consistent with IModule discovery) | ✓ Good — v1.8 |
| Greedy constructor selection | Constructor with most parameters wins (ASP.NET Core DI compatible) | ✓ Good — v1.8 |
| Contracts services optional in DI | IModuleConfig/IModuleContext/IEventBus/ICrossAnimaRouter resolve to null with warning on failure | ✓ Good — v1.8 |
| ILogger via ILoggerFactory (non-generic) | Avoids cross-context generic type issues; ILoggerFactory.CreateLogger(moduleType.FullName) | ✓ Good — v1.8 |
| using alias for ChatMessageInput migration | Core files use `using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput` — avoids namespace pollution | ✓ Good — v1.8 |
| Semaphore priority for messages vs prompt port | messages acquires first, prompt Wait(0) returns false — deterministic priority | ✓ Good — v1.8 |
| IModuleStorage separate from IModuleContext | Dedicated interface (SRP) rather than extending IModuleContext | ✓ Good — v1.8 |
| Bound IModuleStorage per external module | PluginLoader creates bound instance with manifest.Id; built-in modules use explicit GetDataDirectory(moduleId) | ✓ Good — v1.8 |
| manifest.Id ?? manifest.Name for bound moduleId | Manifests without explicit id fall back to Name | ✓ Good — v1.8 |

| Per-module SemaphoreSlim(1,1) in WiringEngine | Serializes concurrent incoming events per module — wave isolation without module awareness | ✓ Good — v1.9 |
| No convergence control for cycles | Modules terminate cycles by not producing output; TTL/energy decay deferred until real-world need | ✓ Good — v1.9 |
| SQLite + Dapper for run persistence | Lightweight embedded DB, no external dependency; Dapper for clean SQL mapping | ✓ Good — v2.0/Phase 45 |
| ConvergenceGuard per-run step budgets | Configurable max steps with restore-on-resume; prevents infinite loops | ✓ Good — v2.0/Phase 45 |
| IHubContext optional injection | SignalR push nullable in RunService/StepRecorder — testable without hub | ✓ Good — v2.0/Phase 45 |
| FixedTextModule trigger input port | Replaces `.execute` subscription with explicit port-driven trigger path | ✓ Good — v1.9 |
| ITickable removed from Contracts | No remaining implementors after propagation engine; duck-typing decision superseded | ✓ Good — v1.9 |
| ModuleSchemaService static type map + IServiceProvider | Avoids reflection scanning; DI-based resolution for built-in + external modules | ✓ Good — v1.9 |
| Schema defaults merged into _currentConfig on load | Auto-save not triggered on load, only on user edits — no spurious persistence | ✓ Good — v1.9 |
| Raw kvp fallback in EditorConfigSidebar | Non-schema modules continue working unchanged — backward compatible | ✓ Good — v1.9 |
| SQLite + Dapper for run persistence | Lightweight embedded DB, no external dependency; Dapper for clean SQL mapping | ✓ Good — v2.0 |
| ConvergenceGuard per-run step budgets | Configurable max steps with restore-on-resume; prevents infinite loops | ✓ Good — v2.0 |
| IHubContext optional injection | SignalR push nullable in RunService/StepRecorder — testable without hub | ✓ Good — v2.0 |
| CommandBlacklistGuard (blacklist model) | All commands allowed except blocked list — simpler than whitelist for dev tools | ✓ Good — v2.0 |
| IWorkspaceTool stateless with per-call workspace root | Tools are stateless, workspace bound per-call not per-instance | ✓ Good — v2.0 |
| 12-char hex artifact IDs | Lower collision probability than 8-char step IDs across runs | ✓ Good — v2.0 |
| Aho-Corasick for GlossaryIndex | Single-pass multi-keyword matching with failure link propagation | ✓ Good — v2.0 |
| DisclosureMatcher static method | No instance state — callers pass nodes and context | ✓ Good — v2.0 |
| JoinBarrierModule double-check pattern | Fast-path count check before Wait(0) guard, re-check after acquiring | ✓ Good — v2.0 |
| LLMModule WaitAsync for workflow branches | Serializes concurrent calls instead of Wait(0) drop — correctness for workflow fan-out | ✓ Good — v2.0 |
| WorkflowPresetService presetsDir constructor arg | Testable preset loading with pragma_table_info migration check | ✓ Good — v2.0 |
| Preset JSON as Content Include in csproj | No manual copy step at runtime — CopyToOutputDirectory Always | ✓ Good — v2.0 |
| AES-GCM + PBKDF2 for API key encryption | Machine-fingerprint derived key; authenticated encryption prevents tampering | ✓ Good — v2.0.1 |
| Write-only API key field via @oninput | Never @bind — prevents stored key from leaking to DOM | ✓ Good — v2.0.1 |
| Three-layer LLM config precedence | Provider-backed > manual > global — deterministic resolution, manual fallback preserved | ✓ Good — v2.0.1 |
| CascadingDropdown ConfigFieldType | Two-tier provider/model rendering in EditorConfigSidebar without new component | ✓ Good — v2.0.1 |
| __manual__ sentinel for manual LLM config | Explicit bypass marker instead of null/empty ambiguity | ✓ Good — v2.0.1 |
| Dictionary dedup by URI for recall | Single RecalledNode per memory URI; glossary keywords joined in reason string | ✓ Good — v2.0.1 |
| Boot recall seeded before Disclosure | byUri dictionary starts with Boot entries so type/priority preserved through merge | ✓ Good — v2.0.1 |
| XML system message for memory injection | <system-memory> block at message[0]; routing before memory before conversation | ✓ Good — v2.0.1 |
| SedimentationService llmCallOverride constructor param | Tests inject fake delegate without mocking OpenAI SDK internals | ✓ Good — v2.0.1 |
| Fire-and-forget sedimentation with CancellationToken.None | Snapshot capture + background Task.Run; isolated from LLM call lifecycle | ✓ Good — v2.0.1 |
| Lazy<IStepRecorder> in BootMemoryInjector | Breaks DI circular dependency surfaced during visual verification | ✓ Good — v2.0.1 |
| Provenance section expanded by default | Most relevant context on node selection; History/Relationships collapsed | ✓ Good — v2.0.1 |
| CountAffectedModules for provider impact | Scans all Anima module configs by provider slug for real impact counts | ✓ Good — v2.0.1 |
| XML text markers for tool calls | `<tool_call>` / `<param>` — consistent with existing `<route>` convention, provider-agnostic | ✓ Good — v2.0.2 |
| Direct tool dispatch (no EventBus) | AgentToolDispatcher calls IWorkspaceTool.ExecuteAsync directly — prevents semaphore deadlock | ✓ Good — v2.0.2 |
| Agent loop as LLMModule internal concern | RunAgentLoopAsync is private; WiringEngine/ChatOutputModule receive only final clean response | ✓ Good — v2.0.2 |
| Hard iteration ceiling (max 50) | Never configurable to 0 or unbounded — default 10, server-side Math.Min clamp | ✓ Good — v2.0.2 |
| Per-event resettable 60s timeout | _agentTimeoutCts replaced (not extended) on each tool call event — 60s from last activity | ✓ Good — v2.0.2 |
| Token budget 70% of agentContextWindowSize | Oldest assistant+tool pairs dropped; truncation notice inserted before removal to stay anchored | ✓ Good — v2.0.2 |
| agentContextWindowSize floor clamped to 1000 | Math.Max prevents zero-budget pathology from misconfigured small values | ✓ Good — v2.0.2 |
| ResourceNotFound fallback for module display names | Missing .resx key returns class name instead of throwing — safe for external plugins | ✓ Good — v2.0.3 |
| SVG `<title>` for port tooltips | Browser-native, zero JS, auto-dismisses on mousedown so drag-to-connect unaffected | ✓ Good — v2.0.3 |
| Two-step split for connection ID parsing | `->` first then `:` on each half — unambiguous and mirrors SelectConnection() construction | ✓ Good — v2.0.3 |
| JS interop focus guard for Delete key | `isActiveElementEditable` checks activeElement tag before allowing keyboard deletion | ✓ Good — v2.0.3 |

## Constraints

- **Platform**: Windows-first — must work on Windows 10/11 without WSL or Docker
- **Architecture**: Local-first — core runtime runs entirely on user's machine, cloud only for LLM API calls
- **Module safety**: Module connections must be code-validated (typed interfaces), not LLM-improvised
- **Performance**: Heartbeat loop must run at ≤100ms intervals without noticeable CPU impact
- **User experience**: Non-technical users must be able to assemble agents without writing code

---
*Last updated: 2026-03-25 after v2.0.4 milestone start*
