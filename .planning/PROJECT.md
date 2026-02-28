# OpenAnima

## Current Status

**Latest milestone:** v1.4 Module SDK & DevEx (shipped 2026-02-28)

**What shipped:**
- Installable .NET global tool (oani) with create, validate, and pack commands
- Module project templates with customizable ports and types
- .oamod package format with MD5 checksums and manifest validation
- Complete documentation: 5-minute quick-start + API reference + common patterns

## Current Milestone: v1.5 Multi-Anima Architecture

**Goal:** Transform from single-runtime dashboard to multi-instance Anima architecture with i18n support and rich module ecosystem.

**Target features:**
- Multi-Anima architecture: Each Anima is an independent agent instance with its own heartbeat, modules, and chat interface
- Internationalization: Chinese/English UI with language preference persistence
- Module ecosystem: Rebuilt module management page with install/uninstall/enable/disable capabilities
- Rich built-in modules: Fixed text, text processing (concat/split/merge), conditional branching, configurable LLM, optional heartbeat
- Module configuration UI: Right-side detail panel in editor for per-module configuration

## What This Is

A local-first, modular AI agent platform for Windows that lets developers and non-technical users build their own "digital life forms / assistants." Agents are proactive — they think, act, and initiate on their own — while remaining controllable through typed module interfaces and deterministic wiring. The platform provides a C# core runtime with a web-based monitoring dashboard, real-time control panel, LLM-powered chat interface, and visual drag-and-drop wiring editor.

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

### Active

- [ ] User can switch UI language between Chinese and English
- [ ] User's language preference persists across sessions
- [ ] User can create new Anima instances
- [ ] User can view list of all Animas in global sidebar
- [ ] Each Anima has independent heartbeat loop
- [ ] Each Anima has independent module instances
- [ ] Each Anima has independent chat interface
- [ ] User can switch between Animas
- [ ] Anima configuration (name, module connections) persists across sessions
- [ ] User can view module list (built-in + third-party)
- [ ] User can install/uninstall modules
- [ ] User can enable/disable modules
- [ ] User can view module information (author, version, description)
- [ ] User can click module in editor to show detail panel on right
- [ ] User can edit module configuration in detail panel
- [ ] Module configuration persists across sessions
- [ ] Fixed text module: User can edit text content in detail panel
- [ ] Text concat module: Concatenates two text inputs
- [ ] Text split module: Splits text by delimiter
- [ ] Text merge module: Merges multiple inputs into one output
- [ ] Conditional branch module: Routes to different outputs based on condition
- [ ] LLM module: User can configure API URL and key in detail panel
- [ ] Heartbeat module: Optional module (no longer core requirement)

### Future

- Tiered thinking loop (code heartbeat → fast model triage → deep model reasoning)
- Language-agnostic module protocol with typed input/output interfaces
- Dynamic module loading — download from marketplace and run without manual setup
- C# modules loaded as in-process assemblies; other-language modules as packaged executables via IPC
- Permission system with autonomy levels (manual / assist / auto)
- Agent memory and conversation history persistence (beyond session)
- Additional port types (Stream, Media, etc.) for richer module communication

### Out of Scope

- Unity/UE integration — deferred to future milestone, not v1
- Mobile app — Windows desktop first
- Local model hosting (llama.cpp etc.) — v1 uses cloud LLM only, architecture allows future addition
- Module marketplace backend/infrastructure — v1 supports loading local module packages only
- Multi-agent orchestration — v1 focuses on single-agent experience
- Module configuration editor — each module has different config schema, use appsettings.json
- Historical data persistence — database complexity, current session only

## Context

Shipped v1.4 with ~1,747 LOC C# CLI + ~1,408 lines documentation (total ~14,500 LOC across all source files).
Tech stack: .NET 8.0, Blazor Server, SignalR, OpenAI SDK 2.8.0, SharpToken 2.0.4, Markdig 0.41.3, Markdown.ColorCode, System.CommandLine 2.0.0-beta4.

v1.4 delivered the module SDK:
- CLI tool: Installable .NET global tool (oani) with System.CommandLine, exit codes, verbosity control
- Module validation: Manifest JSON checking and IModule implementation verification via isolated assembly reflection
- Pack command: Creates .oamod ZIP archives with MD5 checksums and target framework metadata
- Documentation: 5-minute quick-start tutorial + complete API reference for all public interfaces
- E2E verified: Developer can create, validate, pack, and load custom modules in under 5 minutes

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

## Constraints

- **Platform**: Windows-first — must work on Windows 10/11 without WSL or Docker
- **Architecture**: Local-first — core runtime runs entirely on user's machine, cloud only for LLM API calls
- **Module safety**: Module connections must be code-validated (typed interfaces), not LLM-improvised
- **Performance**: Heartbeat loop must run at ≤100ms intervals without noticeable CPU impact
- **User experience**: Non-technical users must be able to assemble agents without writing code

---
*Last updated: 2026-02-28 after v1.5 milestone started*