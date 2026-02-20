# Project Research Summary

**Project:** OpenAnima
**Domain:** Local-first modular AI agent platform (Windows, C# core, Web UI)
**Researched:** 2026-02-21
**Confidence:** MEDIUM

## Executive Summary

OpenAnima is a local-first AI agent platform that enables non-technical users to build proactive agents through visual module wiring. The recommended approach centers on .NET 9 with Blazor Hybrid for the UI, AssemblyLoadContext for C# module isolation, and a tiered thinking loop (code heartbeat → fast LLM triage → deep reasoning) to balance intelligence with performance. This architecture prioritizes Windows desktop first, with gRPC bridges for cross-language modules deferred until C# modules prove the platform.

The critical architectural decision is the tiered thinking loop combined with typed module contracts. This differentiates OpenAnima from reactive chatbots by enabling proactive behavior while maintaining cost efficiency and type safety. The main technical risks are AssemblyLoadContext memory leaks during hot reload, Blazor.Diagrams maturity for the visual editor, and LLM API rate limits breaking the proactive behavior model.

Mitigation strategies include early prototyping of Blazor.Diagrams (with Electron + React Flow as fallback), rigorous testing of AssemblyLoadContext unloading (100+ load/unload cycles in CI), and client-side rate limiting with token bucket patterns. The research indicates a 7-phase roadmap structure, with plugin system and thinking loop as foundational phases before adding visual editing and cross-language support.

## Key Findings

### Recommended Stack

The stack centers on .NET 9 as the core runtime, leveraging its enhanced AssemblyLoadContext for plugin isolation and native AOT support for performance. Blazor Hybrid provides web-based UI within a desktop app using WebView2, avoiding Electron's overhead while maintaining modern web rendering. SQLite handles local persistence with zero configuration, and MediatR provides a lightweight in-process event bus for the heartbeat loop's performance requirements.

**Core technologies:**
- .NET 9 (9.0.x): Core runtime and module host — Latest LTS with improved AssemblyLoadContext, superior Windows integration
- Blazor Hybrid: Web-based UI in desktop app — Native .NET solution, shares code with backend, no Chromium overhead
- SQLite (3.45+): Local data persistence — Industry standard for local-first apps, zero-config, ACID compliant
- gRPC (2.60+): IPC for non-C# modules — High performance, strongly typed contracts, cross-language support (defer to Phase 5+)
- MediatR (12.x): In-process event bus — Lightweight CQRS/mediator pattern, minimal overhead for heartbeat loop
- Blazor.Diagrams (3.x): Node-graph visual editor — Best Blazor option but needs early prototype validation
- Betalgo.OpenAI (8.x): OpenAI-compatible API client — Most actively maintained .NET SDK, supports multiple providers
- Polly (8.x): Resilience and fault handling — Essential for LLM API retry, circuit breaker, timeout patterns

### Expected Features

The feature landscape divides into table stakes (users expect these), differentiators (competitive advantages), and anti-features (explicitly avoid). The MVP focuses on proving the core value proposition: proactive agents with visual wiring.

**Must have (table stakes):**
- Visual module wiring — Core value prop, users expect drag-drop node graphs
- Module installation — Zero-config "download and run" experience
- Conversation history — AI agents without memory feel broken
- Permission controls — Users fear autonomous agents without safety
- Activity log — Users need to see what agent is doing
- Agent lifecycle controls — Basic start/stop/pause functionality
- Error handling — Modules will fail, users need clear feedback

**Should have (competitive):**
- Proactive agent behavior — Core differentiator, agents initiate not just respond
- Typed module contracts — Safety without sacrificing flexibility
- In-process C# modules — Performance advantage over all-IPC architectures
- Tiered thinking loop — Cost optimization via fast triage before expensive reasoning
- Module hot reload — Update modules without restarting agent (defer to v2)
- Cross-language modules — Python/JS modules alongside C# (Phase 5+)

**Defer (v2+):**
- Module hot reload — Complex, workaround is restart agent
- Visual debugging — Activity log sufficient for V1
- Module marketplace UI — Local packages only for V1
- LLM provider selection UI — Config file acceptable for V1

### Architecture Approach

The architecture follows a layered pattern with clear separation between runtime, module hosting, and UI. The thinking loop orchestrator manages tiered reasoning (heartbeat → triage → deep reasoning), while the event bus provides loose coupling between modules. C# modules load via AssemblyLoadContext for in-process performance, while non-C# modules communicate via gRPC as separate processes.

**Major components:**
1. Thinking Loop Orchestrator — Manages tiered agent reasoning cycle with 100ms heartbeat, condition-based escalation to fast LLM triage, then deep reasoning
2. Event Bus (MediatR) — Routes typed messages between modules, enables loose coupling, supports pub/sub patterns
3. Module Host (C#) — Loads assemblies via AssemblyLoadContext with isolation, enables hot reload, manages dependencies
4. Module Host (IPC) — Manages external process modules via gRPC, handles Python/JS/other languages, process lifecycle management
5. LLM Abstraction — Unified interface to providers (OpenAI, Claude, local), handles streaming, retry logic, rate limiting
6. Permission Enforcer — Controls module autonomy levels (manual/assist/auto), policy engine for action requests
7. Visual Editor (Blazor Hybrid) — Drag-drop node graph for module wiring, connection validation, graph serialization
8. Persistence Layer — SQLite for conversation history, agent state, module registry, graph storage

### Critical Pitfalls

The research identified 12 pitfalls across critical, moderate, and minor severity. The top risks require architectural decisions and testing strategies.

1. **AssemblyLoadContext Memory Leaks** — Modules don't unload due to static event handlers or runtime references. Prevention: weak event pattern, explicit IDisposable cleanup, test 100+ load/unload cycles in CI.

2. **Blazor.Diagrams Maturity Unknown** — Visual editor is core UX but library may lack features (custom validation, complex nodes, performance). Prevention: prototype in Phase 1, have Electron + React Flow fallback, budget 2 weeks for evaluation.

3. **gRPC Overhead for High-Frequency Events** — Heartbeat fires every 100ms, serialization overhead could break performance target. Prevention: C# modules use in-process calls, gRPC only for non-C# modules, batch events, profile early.

4. **LLM API Rate Limits Kill Proactive Behavior** — Agent hits rate limits (3500 RPM for GPT-4), thinking loop stalls. Prevention: token bucket rate limiter client-side, queue requests, Polly retry with exponential backoff, fallback to cached responses.

5. **SQLite Write Contention** — Multiple modules write simultaneously, SQLite locks. Prevention: enable WAL mode, single writer pattern, batch writes, in-memory cache with periodic flush.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Core Plugin System
**Rationale:** Foundation for everything. Must prove AssemblyLoadContext isolation, module loading, and dependency resolution work before building on top.
**Delivers:** Module interface contracts, AssemblyLoadContext loader, module registry, DI container setup
**Addresses:** Module loading (C# in-process) from FEATURES.md
**Avoids:** AssemblyLoadContext memory leaks (Pitfall 1) via weak event pattern and unload testing

### Phase 2: Thinking Loop & Event Bus
**Rationale:** Proves performance requirements achievable. Heartbeat loop is core to proactive behavior, must validate ≤100ms target before adding complexity.
**Delivers:** Background service with PeriodicTimer, MediatR event bus, tiered thinking loop skeleton (no LLM yet)
**Uses:** System.Threading.Channels for lock-free messaging, PeriodicTimer for precise intervals
**Implements:** Thinking Loop Orchestrator component from ARCHITECTURE.md
**Avoids:** Timer drift and GC pauses (Pitfall from phase warnings) via profiling with PerfView

### Phase 3: LLM Integration
**Rationale:** Adds intelligence to thinking loop. Relatively straightforward integration, unblocks module development that needs AI capabilities.
**Delivers:** Betalgo.OpenAI client, Polly resilience policies, streaming responses, prompt management
**Addresses:** LLM provider selection from FEATURES.md (config file for V1)
**Avoids:** Rate limits (Pitfall 4) via token bucket limiter and exponential backoff

### Phase 4: Visual Editor (Blazor Hybrid)
**Rationale:** HIGH RISK phase. Blazor.Diagrams maturity unknown, but visual wiring is core value prop. Needs working runtime to validate against.
**Delivers:** Blazor Hybrid app setup, Blazor.Diagrams integration, node graph UI, connection validation, graph serialization
**Addresses:** Visual module wiring from FEATURES.md (table stakes feature)
**Avoids:** Blazor.Diagrams maturity issues (Pitfall 2) via early prototype and Electron fallback plan

### Phase 5: gRPC Module Bridge
**Rationale:** Extends to cross-language modules. Deferred until C# modules prove platform works. Adds complexity but enables Python/JS ecosystem.
**Delivers:** gRPC service definition (.proto), process manager for external modules, example Python module
**Addresses:** Cross-language modules from FEATURES.md (differentiator)
**Avoids:** gRPC overhead (Pitfall 3) via batching and profiling, only use for non-C# modules

### Phase 6: Data Persistence
**Rationale:** Can be incremental, doesn't block other work. Start simple with SQLite, evolve schema as needed.
**Delivers:** SQLite setup with WAL mode, conversation history storage, agent state persistence, graph storage
**Addresses:** Conversation history from FEATURES.md (table stakes)
**Avoids:** SQLite write contention (Pitfall 5) via WAL mode and single writer pattern

### Phase 7: Example Modules & Polish
**Rationale:** Validates platform end-to-end. Surfaces integration issues. Proves the "download and run" experience works.
**Delivers:** Chat interface module, scheduled tasks module, proactive initiator module, permission controls UI, activity log UI
**Addresses:** Agent lifecycle controls, activity log, permission controls from FEATURES.md
**Avoids:** Module execution timeouts (Pitfall 11) via CancellationToken with 30s default

### Phase Ordering Rationale

- Plugin system first because everything depends on modules loading correctly
- Heartbeat loop second to prove performance requirements before adding complexity
- LLM third because modules need AI capabilities and it's relatively low risk
- Visual editor fourth because it's HIGH RISK (Blazor.Diagrams) but needs working runtime to validate
- gRPC fifth because it extends to other languages but isn't critical path for C# modules
- Persistence sixth because it can be incremental and doesn't block development
- Examples last for integration validation and surfacing real-world issues

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 4 (Visual Editor):** Blazor.Diagrams maturity unknown, complex integration, needs spike to validate custom nodes, connection validation hooks, performance with 50+ nodes
- **Phase 5 (gRPC Bridge):** Non-C# module packaging strategies (PyInstaller, pkg, single-file executables), process lifecycle management patterns

Phases with standard patterns (skip research-phase):
- **Phase 1 (Plugin System):** AssemblyLoadContext is well-documented .NET pattern
- **Phase 2 (Thinking Loop):** Background services and timers are standard .NET
- **Phase 3 (LLM Integration):** HTTP client patterns are straightforward
- **Phase 6 (Persistence):** SQLite integration is well-established
- **Phase 7 (Examples):** Module implementation follows established contracts

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | MEDIUM | .NET 9/Blazor/gRPC are HIGH confidence (official Microsoft, mature). Blazor.Diagrams is MEDIUM (smaller community, ~2k stars, needs validation). Betalgo.OpenAI is MEDIUM (most active SDK but verify current status). |
| Features | HIGH | Requirements are clear from PROJECT.md, no ambiguity. Feature dependencies are well-understood. MVP scope is realistic. |
| Architecture | HIGH | AssemblyLoadContext + gRPC pattern is well-established for plugin systems. Tiered thinking loop is proven pattern from agent frameworks. Event-driven architecture is standard. |
| Pitfalls | MEDIUM | AssemblyLoadContext unloading issues are documented. Blazor.Diagrams is main unknown. Other risks (rate limits, SQLite contention) have known mitigations. |

**Overall confidence:** MEDIUM

### Gaps to Address

**Immediate validation needed (before Phase 1):**
- Verify .NET 9 current version and any breaking changes since training data cutoff
- Check Blazor.Diagrams GitHub activity, recent issues, feature completeness
- Validate Betalgo.OpenAI supports Claude and streaming quality
- Confirm WebView2 distribution strategy for Windows 10 users

**Phase-specific research needed later:**
- Phase 4: If Blazor.Diagrams fails prototype, research alternatives (custom canvas with HTML5 Canvas API, or Electron migration path with React Flow)
- Phase 5: Research non-C# module packaging (PyInstaller for Python, pkg for Node.js, single-file executables)
- Phase 6: SQLite performance tuning for high-frequency writes (WAL mode configuration, batch write strategies, index optimization)

**Not researched (out of scope):**
- Specific module designs (chat interface, scheduler implementations) — covered at high level in FEATURES.md
- Deployment/distribution strategy (installer, auto-update) — not requested
- Testing strategy (unit, integration, E2E) — not requested
- Security model for untrusted modules — mentioned in PROJECT.md as V2 feature

## Sources

### Primary (HIGH confidence)
- .NET 9 official documentation — AssemblyLoadContext patterns, Blazor Hybrid architecture, performance improvements
- Microsoft.Data.Sqlite documentation — WAL mode, concurrency patterns
- gRPC official documentation — Performance characteristics, .NET integration
- MediatR GitHub repository — CQRS/mediator patterns, performance benchmarks

### Secondary (MEDIUM confidence)
- Blazor.Diagrams GitHub repository (~2k stars) — Feature set, community activity (unable to verify current 2026 status)
- Betalgo.OpenAI GitHub repository — Most active .NET OpenAI SDK (unable to verify current status)
- Training data on AI agent platforms — AutoGPT, LangChain, Semantic Kernel architectural patterns
- Training data on plugin architectures — VS Code, Obsidian, Unity plugin systems

### Tertiary (LOW confidence)
- Version numbers for packages — Training data through August 2025, versions may have updated
- Blazor.Diagrams production readiness — Needs validation via prototype
- Current state of .NET ecosystem in 2026 — Unable to access web search during research

---
*Research completed: 2026-02-21*
*Ready for roadmap: yes*
