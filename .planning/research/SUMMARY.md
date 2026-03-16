# Project Research Summary

**Project:** OpenAnima v1.8 SDK Runtime Parity
**Domain:** .NET 8 Plugin Runtime — DI Injection, Storage Paths, Structured Message Input, External Module Validation
**Researched:** 2026-03-16
**Confidence:** HIGH

## Executive Summary

OpenAnima v1.8 is a focused SDK parity milestone: close the gap between built-in modules (which get full DI, storage, and structured data) and external modules (which currently get none of that). The four features — PluginLoader DI injection (PLUG-01), per-module DataDirectory (STOR-01), LLMModule structured message port (MSG-01), and an external ContextModule as end-to-end validation (ECTX-01) — form a single dependency chain. None of them is optional; together they constitute the milestone.

The recommended approach is surgical and additive. Zero new NuGet packages are required. The work is two categories: type moves (`ChatMessageInput`, `LLMResult` from Core.LLM to Contracts) and behavioral changes (`PluginLoader` accepting `IServiceProvider`, `IModuleContext` gaining `GetDataDirectory`). The existing `PluginLoadContext` null-return pattern for Contracts already handles cross-ALC type identity correctly — the main risk is accidentally breaking it by letting a plugin ship its own copy of `OpenAnima.Contracts.dll`. An explicit one-line exclusion in `PluginLoadContext.Load()` eliminates this risk entirely.

The critical sequencing constraint: `ChatMessageInput` must move to Contracts first (unblocks everything else), then PLUG-01 and STOR-01 can proceed in parallel, then MSG-01 (depends on the type move), and finally ECTX-01 as the integration proof. The ContextModule is both a feature and a test — if it works end-to-end, the entire SDK surface is validated.

## Key Findings

### Recommended Stack

No new dependencies. All primitives are BCL or already transitively referenced via the ASP.NET Core host. `ActivatorUtilities.CreateInstance` (already available via `Microsoft.Extensions.DependencyInjection.Abstractions`) replaces `Activator.CreateInstance` in `PluginLoader`. `System.Text.Json` (BCL) handles message list serialization. `System.IO.Path` + `Directory.CreateDirectory` handle DataDirectory path construction.

The v1.7 zero-dependency principle holds for v1.8. The only "stack" decision with any nuance is the JSON-on-Text approach for the `messages` port — it avoids extending the `PortType` enum (which would cascade into WiringEngine, editor rendering, and port validation) at the cost of an implicit serialization contract between ContextModule and LLMModule.

**Core technologies:**
- `ActivatorUtilities.CreateInstance` — DI injection into ALC-isolated plugins — canonical .NET pattern, already transitively available
- `System.Text.Json` — message list serialization over Text port — BCL, already used in project
- `OpenAnima.Contracts` (extended) — SDK surface for external modules — additive changes only, no breaking changes to existing interfaces
- `PluginLoadContext` (existing, guard added) — ALC isolation with Contracts fallthrough — explicit exclusion guard prevents type identity break

### Expected Features

All four features are P1 — there is no subset that validates the goal.

**Must have (table stakes):**
- PLUG-01: PluginLoader resolves constructor parameters from `IServiceProvider` using FullName-based type matching — without this, external modules cannot access any platform services
- STOR-01: `IModuleContext.GetDataDirectory(string moduleId)` returns a per-Anima per-module path, created on first call — without this, modules invent their own path conventions and collide
- MSG-01: `ChatMessageInput` moved to Contracts + LLMModule `messages` input port (JSON-serialized `List<ChatMessageInput>`) — without this, external modules cannot pass conversation history to LLM
- ECTX-01: External ContextModule that exercises all three above features end-to-end — without this, SDK parity is unverified

**Should have (competitive):**
- Constructor DI with graceful degradation: optional params get `null`, required params produce a clear `LoadResult` error with the missing type name
- `GetDataDirectory` as a method (not property): path computed at call time, safe when `ActiveAnimaId` changes during Anima switching
- `messages` port takes precedence over `prompt` port when both fire: backward compat for existing single-turn wiring configurations

**Defer (v2+):**
- `PortType.MessageList` — first-class port type for structured messages (trigger: second external module needing it)
- ANIMA-08 resolution — per-Anima module instances (trigger: user reports history bleed with animaId-keyed workaround)
- Module marketplace / dynamic install
- `ILLMService` access for external modules (after OpenAI SDK dependency is abstracted)

### Architecture Approach

The v1.8 architecture is a strict layered extension of v1.7. `OpenAnima.Contracts` is the SDK surface — it gains `ChatMessageInput`, `LLMResult`, and `GetDataDirectory`. `OpenAnima.Core` gains the behavioral changes (`PluginLoader`, `AnimaContext`, `LLMModule`). `ContextModule` is a new external project that references only Contracts. The build order is a strict dependency chain: type moves first, then interface additions, then implementations, then the external module.

The key architectural decision is keeping `LLMModule` stateless: it receives a complete message list each call and never accumulates history. History lives in `ContextModule` (external, stateful per-Anima via `DataDirectory`). This avoids the ANIMA-08 cross-Anima contamination problem — a singleton `LLMModule` storing per-Anima history would bleed across Animas.

**Major components:**
1. `PluginLoader` (modified) — accepts `IServiceProvider`, resolves constructor params by FullName, invokes via `ConstructorInfo.Invoke`
2. `AnimaContext` (modified) — implements `GetDataDirectory(moduleId)`, constructs `data/animas/{animaId}/module-data/{moduleId}/`, creates on first call, validates path against animasRoot
3. `LLMModule` (modified) — adds `messages` input port (Text/JSON), deserializes `List<ChatMessageInput>`, `messages` takes precedence over `prompt`
4. `OpenAnima.Contracts` (extended) — gains `ChatMessageInput`, `LLMResult`, `GetDataDirectory` method on `IModuleContext`
5. `ContextModule` (new external) — subscribes to chat events, maintains per-Anima history keyed by `ActiveAnimaId`, serializes to JSON, publishes to `LLMModule.port.messages`

### Critical Pitfalls

1. **Cross-ALC type identity break** — if a plugin ships its own `OpenAnima.Contracts.dll`, `PluginLoadContext.Load()` finds it and loads it into the plugin context, making `IModuleContext` a different CLR type than the host's. Fix: add `if (assemblyName.Name == "OpenAnima.Contracts") return null;` as the first line of `Load()`. One line, must be the first change made.

2. **`ActivatorUtilities.CreateInstance` fails on optional constructor parameters** — treats optional params as required, throws `InvalidOperationException` instead of using the default `null`. Fix: custom instantiation helper that calls `sp.GetService(paramType)` (not `GetRequiredService`) and falls back to the parameter's default value for unresolvable optional params.

3. **`ChatMessageInput` in wrong assembly** — `ContextModule` referencing `OpenAnima.Core` to get `ChatMessageInput` creates a circular dependency and reintroduces the type identity problem. Fix: move `ChatMessageInput` to Contracts before writing any ContextModule code. Add `global using` shims in Core.LLM for backward compat.

4. **DataDirectory path traversal** — a `moduleId` containing `../` in `module.json` can escape the Anima directory sandbox. Fix: `Path.GetFullPath` normalization + `StartsWith(animasRoot)` assertion in `AnimaContext.GetDataDirectory`. Allowlist validation on `moduleId` (alphanumeric, hyphen, dot only).

5. **ContextModule history unbounded growth** — after ~20-50 turns, the full history exceeds the LLM context window. Fix: sliding window eviction with a configurable token budget (default: 80% of model context limit), implemented in ContextModule from the start, not added after overflow is observed.

## Implications for Roadmap

Based on the dependency chain in ARCHITECTURE.md and the pitfall-to-phase mapping in PITFALLS.md, four phases are the natural structure:

### Phase 1: PluginLoader DI Injection (PLUG-01)

**Rationale:** Unblocking prerequisite. Without DI injection, ContextModule cannot receive any platform services and the other three features are untestable from an external assembly. Must come first.
**Delivers:** External modules receive `IModuleConfig`, `IModuleContext`, `IEventBus`, `ICrossAnimaRouter` via constructor injection. `PortModule` canary validates injection without changes to the module itself.
**Addresses:** PLUG-01 from FEATURES.md
**Avoids:** Cross-ALC type identity break (Pitfall 1), optional constructor param failure (Pitfall 3), singleton lifetime + per-Anima state bleed (Pitfall 2)
**Research flag:** Standard .NET pattern — skip `/gsd:research-phase`. Architecture file has exact implementation spec.

### Phase 2: LLMModule Structured Message Input (MSG-01)

**Rationale:** Can proceed in parallel with Phase 3 (STOR-01) once `ChatMessageInput` is moved to Contracts (Step 1 of build order). The type move is the prerequisite for both MSG-01 and ECTX-01.
**Delivers:** `ChatMessageInput` and `LLMResult` in Contracts. LLMModule `messages` port (Text/JSON). Backward compat: existing `prompt` port unchanged.
**Addresses:** MSG-01 from FEATURES.md
**Avoids:** WiringEngine string-only port routing break (Pitfall 4), `ChatMessageInput` in wrong assembly (Pitfall 5)
**Research flag:** Standard pattern — skip `/gsd:research-phase`. JSON-on-Text approach is fully specified.

### Phase 3: DataDirectory Storage (STOR-01)

**Rationale:** Independent of MSG-01, can run in parallel with Phase 2. Unblocks ContextModule's persistence capability.
**Delivers:** `IModuleContext.GetDataDirectory(string moduleId)` in Contracts. `AnimaContext` implementation with path validation and lazy directory creation.
**Addresses:** STOR-01 from FEATURES.md
**Avoids:** DataDirectory path traversal (Pitfall 6), DataDirectory not cleaned up on Anima delete (Pitfall 7)
**Research flag:** Standard pattern — skip `/gsd:research-phase`. Path convention and validation approach fully specified.

### Phase 4: External ContextModule (ECTX-01)

**Rationale:** Integration proof. Requires Phases 1-3 complete. Validates the entire v1.8 SDK surface end-to-end from an external assembly.
**Delivers:** Working multi-turn conversation history via external module. ContextModule loads via PluginLoader, receives DI services, persists history to DataDirectory, publishes JSON message list to LLMModule.
**Addresses:** ECTX-01 from FEATURES.md
**Avoids:** ContextModule history unbounded growth (Pitfall 8), file I/O blocking EventBus callback (Pitfall 9), history bleeding across Animas (Pitfall 2)
**Research flag:** Standard pattern — skip `/gsd:research-phase`. ContextModule design is fully specified in ARCHITECTURE.md.

### Phase Ordering Rationale

- Phase 1 first: strict prerequisite — no external module can be tested without DI injection
- Phases 2 and 3 can be parallelized: they share only the `ChatMessageInput` type move as a prerequisite, which is a 10-minute change that should be the very first commit of the milestone
- Phase 4 last: integration test — only meaningful after the SDK surface is complete
- The `ChatMessageInput` type move (Step 1 of build order) unblocks both Phase 2 and Phase 4 simultaneously and should be committed before Phase 1 work begins

### Research Flags

Phases with standard patterns (skip `/gsd:research-phase`):
- **Phase 1 (PluginLoader DI):** exact implementation specified in ARCHITECTURE.md — FullName-based reflection, `ConstructorInfo.Invoke`, explicit Contracts exclusion in ALC
- **Phase 2 (MSG-01):** type move + port addition — fully specified, no unknowns
- **Phase 3 (STOR-01):** path construction + interface addition — fully specified, no unknowns
- **Phase 4 (ECTX-01):** ContextModule design fully specified — history keyed by animaId, sliding window eviction, in-memory only for v1.8

No phases require `/gsd:research-phase`. All research was conducted against the live codebase with HIGH confidence.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All findings from direct codebase inspection + official .NET docs. Zero new packages — no version risk. |
| Features | HIGH | All four features are well-understood .NET patterns. Exact API surface constrained by existing Contracts decisions. |
| Architecture | HIGH | All conclusions from direct source inspection of PluginLoader, AnimaContext, LLMModule, IModuleContext, WiringEngine. No external sources needed. |
| Pitfalls | HIGH | Pitfalls derived from codebase analysis + official .NET ALC and ActivatorUtilities docs. All prevention strategies are concrete and tested patterns. |

**Overall confidence:** HIGH

### Gaps to Address

- **DataDirectory API shape divergence:** STACK.md recommends `DataDirectory` as a property (per-module `IModuleContext` instance), while ARCHITECTURE.md recommends `GetDataDirectory(string moduleId)` as a method on the shared singleton. Use the method form — it matches the existing `AnimaModuleConfigService` pattern and avoids per-module context instances.

- **`ILLMService` move scope:** STACK.md suggests moving `ILLMService` to Contracts alongside `ChatMessageInput`. ARCHITECTURE.md explicitly defers this (PROJECT.md notes it as known debt). For v1.8, only `ChatMessageInput` and `LLMResult` move. `ILLMService` stays in Core. Phase 2 scope should reflect this.

- **ContextModule persistence:** PITFALLS.md recommends in-memory only for v1.8 (defer disk persistence to avoid EventBus callback blocking). FEATURES.md describes persistence as part of ECTX-01. Default to in-memory for Phase 4; add persistence only if explicitly required by requirements.

## Sources

### Primary (HIGH confidence)
- OpenAnima codebase — `PluginLoader.cs`, `PluginLoadContext.cs`, `AnimaContext.cs`, `LLMModule.cs`, `IModuleContext.cs`, `AnimaServiceExtensions.cs`, `AnimaModuleConfigService.cs`, `WiringEngine`, `PortModule.cs`
- `.planning/PROJECT.md` — v1.8 requirements, known tech debt, key decisions
- [Microsoft Learn — ActivatorUtilities.CreateInstance](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.activatorutilities.createinstance)
- [Microsoft Learn — AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
- [Microsoft Learn — Create .NET app with plugin support](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)
- [Microsoft Learn — Path.GetFullPath for path traversal prevention](https://learn.microsoft.com/en-us/dotnet/standard/io/file-path-formats)
- [OpenAI API docs — context_length_exceeded error](https://platform.openai.com/docs/guides/error-codes)
- [.NET Runtime GitHub — AssemblyLoadContext isolation patterns](https://github.com/dotnet/runtime/blob/main/docs/design/features/assemblyloadcontext.md)

### Secondary (MEDIUM confidence)
- JSON-on-Text serialization contract for `messages` port — inferred from existing `ConditionalBranchModule` pattern; no direct precedent in codebase for cross-module JSON payloads

---
*Research completed: 2026-03-16*
*Ready for roadmap: yes*
