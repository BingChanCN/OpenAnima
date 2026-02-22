# OpenAnima

## What This Is

A local-first, modular AI agent platform for Windows that lets developers and non-technical users build their own "digital life forms / assistants." Agents are proactive — they think, act, and initiate on their own — while remaining controllable through typed module interfaces and deterministic wiring. The platform provides a C# core runtime with a Web-based visual editor for drag-and-drop agent assembly.

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
- ✓ Blazor Server WebUI with real-time runtime monitoring dashboard — v1.1 Phase 3-5
- ✓ Module status display: loaded modules list, metadata, running state — v1.1 Phase 4
- ✓ Heartbeat monitoring: running state, tick count, latency, real-time updates — v1.1 Phase 4-5
- ✓ Control operations: load/unload modules, start/stop heartbeat from UI (MOD-08, MOD-09, MOD-10, BEAT-02) — v1.1 Phase 6

### Active

- [ ] Runtime as background service with browser auto-launch (complete desktop app experience)

### Future

- Tiered thinking loop (code heartbeat → fast model triage → deep model reasoning)
- Language-agnostic module protocol with typed input/output interfaces
- Dynamic module loading — download from marketplace and run without manual setup
- C# modules loaded as in-process assemblies; other-language modules as packaged executables via IPC
- Visual drag-and-drop editor for non-technical users to wire modules into agents
- Permission system with autonomy levels (manual / assist / auto)
- LLM integration via OpenAI-compatible API (cloud-first, local models later)
- Agent memory and conversation history persistence
- Example modules: chat interface, scheduled tasks, proactive conversation initiator

### Out of Scope

- Unity/UE integration — deferred to future milestone, not v1
- Mobile app — Windows desktop first
- Local model hosting (llama.cpp etc.) — v1 uses cloud LLM only, architecture allows future addition
- Module marketplace backend/infrastructure — v1 supports loading local module packages only
- Multi-agent orchestration — v1 focuses on single-agent experience

## Current Milestone: v1.1 WebUI Runtime Dashboard

**Goal:** Provide a real-time web-based monitoring and control panel for the OpenAnima runtime, delivering a complete desktop application experience.

**Target features:**
- Module status display with metadata and running state
- Heartbeat monitoring with real-time tick/latency data
- Control operations (load/unload modules, start/stop heartbeat)
- Runtime as background service with browser auto-launch

## Context

Shipped v1.0 with 1,323 LOC C# across 17 source files.
Tech stack: .NET 8.0, AssemblyLoadContext isolation, ConcurrentDictionary, PeriodicTimer, FileSystemWatcher.
Core runtime loads modules in isolated contexts, communicates via typed event bus, and ticks at 100ms.
v1.1 adds Blazor Server WebUI for runtime monitoring and control — the first user-facing interface.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| C# core runtime | Strong Windows ecosystem, good performance, Unity/UE compatibility for future | ✓ Good — 1,323 LOC, clean architecture |
| Tiered thinking loop (3 layers) | Balances intelligence and speed — code heartbeat is free, only deep reasoning costs tokens | — Pending (Phase 4) |
| OpenAI-compatible API first | Covers most LLM providers with single interface, simplest path to working product | — Pending (Phase 3) |
| Typed interfaces + visual graph | Safety from type system, accessibility from visual editor — serves both user groups | — Pending (Phase 5) |
| Dynamic assembly loading for C# modules | Enables "download and run" without separate processes, best performance for C# ecosystem | ✓ Good — AssemblyLoadContext isolation works |
| IPC for non-C# modules | Language agnostic while keeping C# modules fast, packaged executables for zero-setup | — Pending |
| Web-based UI (framework TBD) | Best ecosystem for visual editors (node graphs, drag-drop), cross-platform potential | ✓ Good — Blazor Server chosen for v1.1 dashboard |
| V1 = core platform + proactive chat demo | Proves the core value (proactive agent) without overscoping into Unity/marketplace | — Pending |
| .slnx format (XML-based solution) | .NET 10 SDK creates .slnx by default; compatible with all dotnet CLI commands | ✓ Good |
| LoadResult record instead of exceptions | Enables caller to decide how to handle failures without try/catch boilerplate | ✓ Good |
| Name-based type discovery | Cross-context type identity solved via interface.FullName comparison | ✓ Good — critical for plugin isolation |
| Duck-typing for ITickable | Reflection-based method lookup solves cross-context type identity for heartbeat | ✓ Good |
| Property injection for EventBus | EventBus injected via setter after module loading, subscription in setter | ✓ Good |
| Blazor Server for WebUI | Pure C# full-stack, SignalR built-in for real-time push, seamless .NET runtime integration | — Pending |
| Lock-free event bus | ConcurrentDictionary + ConcurrentBag with lazy cleanup every 100 publishes | ✓ Good |

## Constraints

- **Platform**: Windows-first — must work on Windows 10/11 without WSL or Docker
- **Architecture**: Local-first — core runtime runs entirely on user's machine, cloud only for LLM API calls
- **Module safety**: Module connections must be code-validated (typed interfaces), not LLM-improvised
- **Performance**: Heartbeat loop must run at ≤100ms intervals without noticeable CPU impact
- **User experience**: Non-technical users must be able to assemble agents without writing code

---
*Last updated: 2026-02-22 after Phase 6 (v1.1 milestone complete)*
