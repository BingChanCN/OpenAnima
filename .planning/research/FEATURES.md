# Feature Landscape

**Domain:** Local-first modular AI agent platform
**Researched:** 2026-02-21

## Table Stakes

Features users expect. Missing = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Visual module wiring | Core value prop - "non-technical users can build agents" | High | Requires node-graph editor, connection validation, real-time preview |
| Module marketplace browsing | Users expect to discover/install modules like app stores | Medium | V1 = local packages only, but UI should support future marketplace |
| Conversation history | AI agents without memory feel broken | Low | SQLite storage, simple CRUD |
| Permission controls | Users fear autonomous agents without safety | Medium | Per-module permissions, autonomy levels (manual/assist/auto) |
| LLM provider selection | Users have different API keys/preferences | Low | Config UI for OpenAI/Claude/local endpoints |
| Module installation | "Download and run" is core requirement | High | Zero-config install, dependency resolution, version management |
| Agent start/stop/pause | Basic lifecycle controls | Low | UI buttons + background service management |
| Activity log | Users need to see what agent is doing | Medium | Real-time event stream, filterable by module/severity |
| Error handling | Modules will fail, users need clear feedback | Medium | Try-catch boundaries, user-friendly error messages, retry logic |
| Module updates | Modules evolve, users expect updates | Medium | Version checking, update notifications, rollback capability |

## Differentiators

Features that set product apart. Not expected, but valued.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Proactive agent behavior | Core differentiator - agents initiate, not just respond | High | Tiered thinking loop, context awareness, interruption handling |
| Typed module contracts | Safety without sacrificing flexibility | Medium | Compile-time validation, prevents runtime connection errors |
| In-process C# modules | Performance advantage over all-IPC architectures | High | AssemblyLoadContext isolation, hot reload, debugging support |
| Tiered thinking loop | Cost optimization - fast triage before expensive reasoning | High | Code heartbeat → fast model → deep model, context passing |
| Visual debugging | See agent's thought process in real-time | Medium | Visualize which modules fired, data flow, decision points |
| Module hot reload | Update modules without restarting agent | High | AssemblyLoadContext unloading, state preservation, connection rewiring |
| Cross-language modules | Python/JS modules alongside C# | Medium | gRPC bridge, packaged executables, zero manual setup |
| Local-first architecture | Privacy, offline capability, no cloud lock-in | Medium | All data local, cloud only for LLM API, works offline with local models (future) |

## Anti-Features

Features to explicitly NOT build.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Visual programming (code generation) | Scope creep, hard to debug, users want modules not code | Provide module SDK, let developers write modules |
| Multi-agent orchestration | V1 complexity explosion, single agent is hard enough | Single agent focus, defer to future milestone |
| Built-in LLM hosting | Infrastructure burden, local models are future feature | Use cloud APIs, architecture allows future local model plugin |
| Module sandboxing (security) | Massive scope, assume trusted modules for V1 | Document "trusted modules only", add security in V2 |
| Mobile app | Windows desktop first, mobile is different UX | Desktop only, responsive UI allows future web version |
| Cloud sync | Local-first means local, cloud sync is feature creep | Export/import for backup, defer sync to future |
| Module marketplace backend | Infrastructure project, not core platform | V1 = local packages, marketplace is separate product |
| Natural language module wiring | LLM-generated connections are unsafe, defeats typed contracts | Visual graph only, type safety is core value |

## Feature Dependencies

```
Module loading → Module wiring (can't wire until modules load)
Module wiring → Agent execution (can't run until wired)
Agent execution → Proactive behavior (execution must work before proactive)
LLM integration → Tiered thinking (thinking loop needs LLM)
Event bus → Module communication (modules communicate via events)
Typed contracts → Connection validation (validation needs type info)
Permission system → Autonomous actions (permissions gate autonomy)
Activity log → Visual debugging (debugging builds on activity log)
```

## MVP Recommendation

**Prioritize (must-have for V1):**
1. Module loading (C# in-process) - foundation
2. Visual module wiring - core UX
3. Event bus - module communication
4. LLM integration (OpenAI-compatible) - intelligence
5. Tiered thinking loop - proactive behavior
6. Basic permission controls - safety
7. Conversation history - memory
8. Activity log - observability
9. Agent lifecycle (start/stop) - basic controls

**Defer (nice-to-have, not V1):**
- Module hot reload - complex, workaround = restart agent
- Visual debugging - activity log is enough for V1
- Cross-language modules (gRPC) - C# modules prove platform first
- Module updates - manual reinstall for V1
- Module marketplace browsing - local packages only
- LLM provider selection UI - config file for V1

**Rationale:**
- MVP proves core value: proactive agent with visual wiring
- Deferred features don't block validation
- C# modules only = simpler, gRPC adds complexity
- Hot reload is nice but restart is acceptable for V1
- Marketplace UI without marketplace backend is premature

## Feature Complexity Analysis

**High complexity (needs dedicated phase):**
- Visual module wiring (Blazor.Diagrams integration, validation, serialization)
- Proactive agent behavior (tiered thinking loop, context management, interruption)
- Module loading (AssemblyLoadContext, dependency resolution, isolation)
- Module hot reload (unloading, state preservation, rewiring)

**Medium complexity (can combine with other work):**
- Permission system (data model + enforcement points)
- Activity log (event collection + UI)
- Module updates (version checking + download)
- Cross-language modules (gRPC service + process management)

**Low complexity (quick wins):**
- Conversation history (SQLite CRUD)
- Agent lifecycle controls (background service start/stop)
- LLM provider config (settings UI)
- Error handling (try-catch + user messages)

## Sources

- PROJECT.md requirements analysis
- Training data on AI agent platforms (AutoGPT, LangChain, Semantic Kernel patterns)
- Plugin architecture patterns from VS Code, Obsidian, Unity ecosystems

---
*Feature research for: OpenAnima*
*Researched: 2026-02-21*
