# OpenAnima

## Current State

**Latest milestone:** v1.6 Cross-Anima Routing (shipped 2026-03-14)
**Next milestone:** Planning (use `/gsd:new-milestone`)

## What This Is

A local-first, modular AI agent platform for Windows that lets developers and non-technical users build their own "digital life forms / assistants." Users create multiple independent Anima instances — each with its own heartbeat, module wiring, chat interface, and configuration. Agents are proactive — they think, act, and initiate on their own — while remaining controllable through typed module interfaces and deterministic wiring. The platform provides a C# core runtime with a web-based dashboard, visual drag-and-drop wiring editor, LLM-powered chat, and full Chinese/English internationalization.

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
- ✓ API reference documents all public interfaces (IModule, IModuleExecutor, ITickable, IEventBus) (DOC-03) — v1.4
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

### Active

- [ ] Each Anima has independent module instances (ANIMA-08 — global singleton kept for DI compatibility)
- [ ] User can view list of all installed modules (MODMGMT-01)
- [ ] User can install module from .oamod package (MODMGMT-02)
- [ ] User can uninstall module (MODMGMT-03)
- [ ] User can search and filter modules by name (MODMGMT-06)

### Future

- Tiered thinking loop (code heartbeat → fast model triage → deep model reasoning)
- Language-agnostic module protocol with typed input/output interfaces
- Dynamic module loading — download from marketplace and run without manual setup
- C# modules loaded as in-process assemblies; other-language modules as packaged executables via IPC
- Permission system with autonomy levels (manual / assist / auto)
- Agent memory and conversation history persistence (beyond session)
- Additional port types (Stream, Media, etc.) for richer module communication
- Anima background execution (run in background while viewing different Anima)
- Anima execution statistics (uptime, module execution count)
- Module dependency resolution and auto-install
- Module marketplace integration
- Loop control module for iterative execution
- Variable storage module for state persistence
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

Shipped v1.6 with ~13,610 LOC across all source files (C#, Razor, CSS, JS).
Tech stack: .NET 8.0, Blazor Server, SignalR, OpenAI SDK 2.8.0, SharpToken 2.0.4, Markdig 0.41.3, Markdown.ColorCode, System.CommandLine 2.0.0-beta4, Microsoft.Extensions.Http.Resilience 8.7.0.

v1.6 delivered cross-Anima routing:
- CrossAnimaRouter singleton with compound-key port registry, Guid correlation IDs, timeout enforcement, periodic cleanup
- AnimaInputPort, AnimaOutputPort, AnimaRoute modules with cascading dropdown config UI
- LLM prompt auto-injection with FormatDetector XML marker parsing and self-correction retry loop
- HttpRequestModule with SsrfGuard IP blocking, IHttpClientFactory resilience pipeline, configurable sidebar UI
- ModuleEvent.Metadata for correlationId passthrough across module boundaries

Known tech debt:
- ANIMA-08: Global IEventBus singleton kept for module constructor DI — full module instance isolation deferred
- MODMGMT-01/02/03/06: Full install/uninstall/search UI deferred — basic card UI with .oamod install works
- Schema mismatch between CLI and Runtime (extended manifest fields)
- Pre-existing test isolation issues (3 failures)
- LLMModule dispatch uses hardcoded event name strings

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

## Constraints

- **Platform**: Windows-first — must work on Windows 10/11 without WSL or Docker
- **Architecture**: Local-first — core runtime runs entirely on user's machine, cloud only for LLM API calls
- **Module safety**: Module connections must be code-validated (typed interfaces), not LLM-improvised
- **Performance**: Heartbeat loop must run at ≤100ms intervals without noticeable CPU impact
- **User experience**: Non-technical users must be able to assemble agents without writing code

---
*Last updated: 2026-03-14 after v1.6 milestone*