# OpenAnima

## What This Is

A local-first, modular AI agent platform for Windows that lets developers and non-technical users build their own "digital life forms / assistants." Agents are proactive — they think, act, and initiate on their own — while remaining controllable through typed module interfaces and deterministic wiring. The platform provides a C# core runtime with a web-based monitoring dashboard, real-time control panel, and LLM-powered chat interface, with a visual drag-and-drop editor planned for future milestones.

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

### Active

(None — planning next milestone)

### Future

- Tiered thinking loop (code heartbeat → fast model triage → deep model reasoning)
- Language-agnostic module protocol with typed input/output interfaces
- Dynamic module loading — download from marketplace and run without manual setup
- C# modules loaded as in-process assemblies; other-language modules as packaged executables via IPC
- Visual drag-and-drop editor for non-technical users to wire modules into agents
- Permission system with autonomy levels (manual / assist / auto)
- Agent memory and conversation history persistence (beyond session)
- Example modules: chat interface, scheduled tasks, proactive conversation initiator

### Out of Scope

- Unity/UE integration — deferred to future milestone, not v1
- Mobile app — Windows desktop first
- Local model hosting (llama.cpp etc.) — v1 uses cloud LLM only, architecture allows future addition
- Module marketplace backend/infrastructure — v1 supports loading local module packages only
- Multi-agent orchestration — v1 focuses on single-agent experience
- Module configuration editor — each module has different config schema, use appsettings.json
- Historical data persistence — database complexity, current session only

## Context

Shipped v1.2 with 6,352 LOC C#/Razor/CSS/JS across ~60 source files.
Tech stack: .NET 8.0, Blazor Server, SignalR, OpenAI SDK 2.8.0, SharpToken 2.0.4, Markdig 0.41.3, Markdown.ColorCode.
Core runtime loads modules in isolated contexts, communicates via typed event bus, ticks at 100ms, serves a real-time web dashboard with module management, heartbeat monitoring, and LLM chat with streaming and context management.
xUnit test suite covers memory leak detection and performance validation.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| C# core runtime | Strong Windows ecosystem, good performance, Unity/UE compatibility for future | ✓ Good — clean architecture, 6,352 LOC |
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

## Constraints

- **Platform**: Windows-first — must work on Windows 10/11 without WSL or Docker
- **Architecture**: Local-first — core runtime runs entirely on user's machine, cloud only for LLM API calls
- **Module safety**: Module connections must be code-validated (typed interfaces), not LLM-improvised
- **Performance**: Heartbeat loop must run at ≤100ms intervals without noticeable CPU impact
- **User experience**: Non-technical users must be able to assemble agents without writing code

---
*Last updated: 2026-02-25 after v1.2 milestone*
