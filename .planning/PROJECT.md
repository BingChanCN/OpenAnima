# OpenAnima

## What This Is

A local-first, modular AI agent platform for Windows that lets developers and non-technical users build their own "digital life forms / assistants." Agents are proactive — they think, act, and initiate on their own — while remaining controllable through typed module interfaces and deterministic wiring. The platform provides a C# core runtime with a Web-based visual editor for drag-and-drop agent assembly.

## Core Value

Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Tiered thinking loop (code heartbeat → fast model triage → deep model reasoning)
- [ ] Language-agnostic module protocol with typed input/output interfaces
- [ ] Dynamic module loading — download from marketplace and run without manual setup
- [ ] C# modules loaded as in-process assemblies; other-language modules as packaged executables via IPC
- [ ] Visual drag-and-drop editor for non-technical users to wire modules into agents
- [ ] Permission system with autonomy levels (manual / assist / auto)
- [ ] LLM integration via OpenAI-compatible API (cloud-first, local models later)
- [ ] Event bus for inter-module communication
- [ ] Agent memory and conversation history persistence
- [ ] Example modules: chat interface, scheduled tasks, proactive conversation initiator

### Out of Scope

- Unity/UE integration — deferred to future milestone, not v1
- Mobile app — Windows desktop first
- Local model hosting (llama.cpp etc.) — v1 uses cloud LLM only, architecture allows future addition
- Module marketplace backend/infrastructure — v1 supports loading local module packages only
- Multi-agent orchestration — v1 focuses on single-agent experience

## Context

- Target platform: Windows (local-first, no cloud dependency for core runtime)
- Core language: C# (.NET) for runtime, module host, and backend services
- Frontend: Web-based UI (specific framework TBD — research phase will evaluate Blazor, Electron, Tauri)
- Data storage: TBD — research phase will evaluate SQLite, LiteDB, file-based options
- LLM access: OpenAI-compatible API format covers most providers (OpenAI, Claude via proxy, local model servers)
- The proactive behavior is the key differentiator — most agent frameworks are reactive (wait for input)
- Module protocol must be language-agnostic at the wire level but optimized for C# in-process modules
- "Download and run" module experience is critical — users should never need to install dependencies or start separate processes manually

## Constraints

- **Platform**: Windows-first — must work on Windows 10/11 without WSL or Docker
- **Architecture**: Local-first — core runtime runs entirely on user's machine, cloud only for LLM API calls
- **Module safety**: Module connections must be code-validated (typed interfaces), not LLM-improvised
- **Performance**: Heartbeat loop must run at ≤100ms intervals without noticeable CPU impact
- **User experience**: Non-technical users must be able to assemble agents without writing code

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| C# core runtime | Strong Windows ecosystem, good performance, Unity/UE compatibility for future | — Pending |
| Tiered thinking loop (3 layers) | Balances intelligence and speed — code heartbeat is free, only deep reasoning costs tokens | — Pending |
| OpenAI-compatible API first | Covers most LLM providers with single interface, simplest path to working product | — Pending |
| Typed interfaces + visual graph | Safety from type system, accessibility from visual editor — serves both user groups | — Pending |
| Dynamic assembly loading for C# modules | Enables "download and run" without separate processes, best performance for C# ecosystem | — Pending |
| IPC for non-C# modules | Language agnostic while keeping C# modules fast, packaged executables for zero-setup | — Pending |
| Web-based UI (framework TBD) | Best ecosystem for visual editors (node graphs, drag-drop), cross-platform potential | — Pending |
| V1 = core platform + proactive chat demo | Proves the core value (proactive agent) without overscoping into Unity/marketplace | — Pending |

---
*Last updated: 2026-02-21 after initialization*
