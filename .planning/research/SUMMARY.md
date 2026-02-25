# Project Research Summary

**Project:** OpenAnima v1.3 True Modularization & Visual Wiring
**Domain:** Port-based module system with visual node editor in Blazor Server
**Researched:** 2026-02-25
**Confidence:** MEDIUM-HIGH

## Executive Summary

OpenAnima v1.3 transforms the existing hardcoded LLM/chat/heartbeat features into a true port-based modular architecture with visual drag-and-drop wiring. Research shows this is achievable with zero new dependencies — .NET 8.0's built-in capabilities (enums, records, System.Text.Json, HTML5 drag-and-drop, SVG rendering) provide everything needed. The recommended approach uses a custom topological sort (~100 LOC) for execution ordering, HTML5 + SVG for the visual editor, and extends the existing EventBus for data routing between connected ports.

The critical success factor is avoiding the "refactoring trap" — breaking existing v1.2 functionality while modularizing. Research identifies 10 major pitfalls, with the top three being: (1) circular dependency deadlock from invalid wiring, (2) SignalR rendering bottleneck during drag operations, and (3) breaking existing features during modularization. All three can be mitigated through upfront design: cycle detection in the wiring engine, throttled StateHasChanged during drag, and comprehensive integration tests before refactoring.

The architecture follows a clean separation: Port Type System (foundation) → Wiring Engine (execution orchestration) → Visual Editor (UI) → Module Refactoring (demonstration). This sequence ensures each layer is validated before the next depends on it, reducing integration risk.

## Key Findings

### Recommended Stack

No new NuGet packages required. The existing .NET 8.0 stack already provides all necessary capabilities for v1.3's port system, wiring engine, and visual editor.

**Core technologies:**
- **C# enums and records** (built-in): Port type system (Text, Trigger) with compile-time validation and clean JSON serialization
- **Custom topological sort** (~100 LOC): DAG execution ordering with cycle detection, avoiding 500KB+ QuikGraph dependency
- **HTML5 Drag & Drop API + SVG** (native browser): Visual editor with zero JavaScript dependencies, Blazor-native rendering
- **System.Text.Json** (built-in): Wiring configuration persistence, already used throughout project
- **Existing EventBus** (custom): Data routing between connected ports, no changes needed to EventBus itself

**Critical version requirements:**
- .NET 8.0 LTS (no upgrade needed, supported until Nov 2026)
- SignalR 8.0.x must match runtime version for circuit stability

**Alternatives rejected:**
- QuikGraph (graph algorithms library): 500KB+ for 100 lines of topological sort code
- Syncfusion/DevExpress (visual editor): Commercial license ($995+/year), conflicts with pure CSS philosophy
- Blazor.Diagram (open-source): Adds learning curve, may not support custom port validation
- Canvas rendering: Heavy JSInterop complexity, SVG is simpler for <100 connections

### Expected Features

**Must have (table stakes):**
- Port type system with Text and Trigger types, same-type connection validation
- Visual drag-and-drop module placement with pan/zoom canvas
- Click-drag wiring from port to port with visual preview and invalid connection rejection
- Bezier curve rendering for professional appearance
- Save/load wiring configuration with auto-save
- Execute wiring topology at runtime with topological sort
- Module lifecycle integration (editor reflects runtime state)
- Delete nodes and connections with keyboard shortcuts

**Should have (differentiators):**
- Undo/redo for graph edits (command pattern)
- Port tooltips explaining purpose
- Node search/palette for finding modules
- Live execution visualization (highlight active connections)
- Comments/annotations for documenting graph sections
- Export graph as image for sharing

**Defer (v2+):**
- Subgraphs/groups (complex nested execution context)
- Breakpoints on nodes (requires deep runtime integration)
- Collaborative editing (conflict resolution complexity)
- Connection reroute points (manual curve control)

**Anti-features (explicitly avoid):**
- Automatic layout (users want control)
- Inline code editing in nodes (scope creep)
- Visual scripting for module logic (modules are C# code)
- AI-suggested connections (unreliable, deterministic wiring is core value)

### Architecture Approach

v1.3 integrates three new subsystems with minimal disruption to existing components: Port Type System extends IModule contracts with IPortProvider interface, Wiring Engine replaces direct EventBus usage with topology-driven execution, and Visual Editor adds a new Blazor page using HTML5 + SVG. The key integration principle is augmentation rather than replacement — EventBus remains for internal messaging, wiring engine orchestrates module execution order based on port connections.

**Major components:**
1. **PortRegistry** — Discovers and catalogs ports from loaded modules via IPortProvider interface
2. **WiringEngine** — Executes modules in topological order, uses EventBus for data passing between connected ports
3. **WiringService** — Service facade for load/save/validate wiring configuration operations
4. **WiringEditor.razor** — Visual drag-and-drop canvas, reuses existing SignalR infrastructure
5. **Refactored modules** — HeartbeatModule, LLMModule, ChatInputModule, ChatOutputModule implement IModule + IPortProvider

**Critical patterns:**
- Two-phase initialization: Load all modules first, then wire connections (avoids circular dependencies)
- Topological sort for execution order: Deterministic, prevents race conditions, detects cycles
- Port-based EventBus routing: WiringEngine translates port connections into EventBus subscriptions
- Interface-based port discovery: Explicit IPortProvider.GetPorts() works across AssemblyLoadContext boundaries

### Critical Pitfalls

1. **Circular Dependency Deadlock** — Visual editors make cycles trivially easy to create (A → B → C → A). Users don't think in DAGs. Mitigation: Implement topological sort validation before saving, detect cycles using DFS during connection creation, block invalid connections in UI with clear error messages. Address in Phase 2 (Wiring Engine) — validation must be built into connection logic from the start.

2. **SignalR Rendering Bottleneck** — Every node position change triggers StateHasChanged, causing full re-renders. With 10+ modules, dragging becomes laggy (200ms+ delay). SignalR bandwidth saturates with render diffs. Mitigation: Throttle StateHasChanged to 50-100ms during drag, use JS interop for drag rendering (sync to Blazor only on drop), implement ShouldRender() to prevent unnecessary re-renders, use @key directives on node components. Address in Phase 3 (Visual Editor) — must be architected for performance from the start.

3. **Breaking Existing Features During Modularization** — Refactoring hardcoded LLM/chat/heartbeat into modules breaks existing functionality. Hardcoded features have implicit dependencies and execution order guarantees that aren't obvious until removed. Mitigation: Create integration tests for existing workflows BEFORE refactoring, implement feature flags to toggle between old and new paths, keep old code intact until new modules are validated, test with actual v1.2 configs. Address in Phase 1 (Port System) — establish testing infrastructure before touching existing code.

4. **Type System Too Rigid or Too Loose** — Port type system either prevents valid connections (too rigid) or allows invalid connections that fail at runtime (too loose). Mitigation: Start with minimal types (Text, Trigger), design for extensibility without breaking configs, use structural typing where possible, provide clear error messages suggesting conversion nodes. Address in Phase 1 (Port System) — foundational design, changing later breaks everything.

5. **EventBus Ordering Guarantees Lost** — Current EventBus uses ConcurrentBag which doesn't guarantee ordering. Moving to port-based wiring introduces async execution and parallel module processing. Mitigation: Document EventBus ordering guarantees explicitly, add sequence numbers to messages if ordering matters, implement execution barriers in wiring engine, test with artificial delays to expose race conditions. Address in Phase 2 (Wiring Engine) — execution semantics must be defined before modules depend on them.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Port Type System & Testing Foundation
**Rationale:** Foundation for all wiring functionality. Must establish testing infrastructure before refactoring existing code to avoid breaking v1.2 features (Pitfall #3).

**Delivers:**
- PortType and PortDirection enums (Text, Trigger, Input, Output)
- PortMetadata record and IPortProvider interface in OpenAnima.Contracts
- PortRegistry for cataloging ports from loaded modules
- PortTypeValidator for connection validation (type matching, direction, no cycles)
- Integration tests for existing v1.2 workflows (chat, LLM, heartbeat)

**Addresses features:**
- Port type system with same-type connection validation (table stakes)
- Type safety prevents invalid connections (table stakes)

**Avoids pitfalls:**
- Pitfall #3: Breaking existing features (integration tests first)
- Pitfall #4: Type system too rigid/loose (minimal types, extensible design)
- Pitfall #7: Module initialization order dependencies (two-phase init design)
- Pitfall #9: Port data serialization assumptions (define contract upfront)

**Research flag:** Standard patterns, skip research-phase. Port systems and type validation are well-documented.

### Phase 2: Wiring Engine & Execution Orchestration
**Rationale:** Core execution logic depends on port system being stable. Must implement cycle detection and ordering guarantees before visual editor allows users to create arbitrary connections.

**Delivers:**
- Wire record and WiringConfig model
- TopologicalSorter with cycle detection (Kahn's algorithm, ~100 LOC)
- WiringEngine for topology-driven module execution
- WiringService facade (load/save/validate config)
- EventBus integration for data routing between connected ports
- Modified HeartbeatService delegating to WiringEngine

**Uses stack:**
- Custom topological sort (avoids QuikGraph dependency)
- System.Text.Json for config serialization
- Existing EventBus for data passing

**Implements architecture:**
- Topological sort for execution order pattern
- Port-based EventBus routing pattern

**Avoids pitfalls:**
- Pitfall #1: Circular dependency deadlock (cycle detection in topological sort)
- Pitfall #5: EventBus ordering guarantees lost (document semantics, add sequence numbers)
- Pitfall #6: Wiring config deserialization failures (versioning, validation)

**Research flag:** Standard patterns, skip research-phase. Topological sort is textbook algorithm.

### Phase 3: Visual Drag-and-Drop Editor
**Rationale:** UI layer depends on wiring engine being functional. Performance optimizations (throttling, ShouldRender) must be designed in from start, not retrofitted.

**Delivers:**
- WiringEditor.razor with HTML5 drag-and-drop canvas
- SVG rendering for bezier curve connections
- Pan/zoom canvas navigation
- Visual connection preview with validation feedback
- Node selection and deletion
- Minimal JavaScript interop (~50 LOC) for mouse tracking
- RuntimeHub extension for wiring operations

**Uses stack:**
- HTML5 Drag & Drop API (native browser)
- SVG for connection rendering
- Blazor JSInterop for mouse coordinates

**Implements architecture:**
- WiringEditor.razor component
- Single source of truth (WiringConfig)

**Addresses features:**
- Visual drag-and-drop module placement (table stakes)
- Click-drag wiring with preview (table stakes)
- Bezier curve rendering (table stakes)
- Pan/zoom canvas (table stakes)
- Delete nodes and connections (table stakes)

**Avoids pitfalls:**
- Pitfall #2: SignalR rendering bottleneck (throttle StateHasChanged, JS interop for drag)
- Pitfall #8: No undo/redo (command pattern from start, defer implementation to post-MVP)
- Pitfall #10: Editor/runtime state divergence (single source of truth, clear sync mechanism)

**Research flag:** Needs research-phase for performance optimization. Specific throttling values and ShouldRender patterns need validation with 10+ modules.

### Phase 4: Module Refactoring & Config Persistence
**Rationale:** Demonstrates port-based architecture by refactoring existing features. Validates that wiring system actually works for real use cases. Config persistence ensures user work is saved.

**Delivers:**
- HeartbeatModule (refactored from HeartbeatService)
- LLMModule (refactored from LLMService)
- ChatInputModule (new, user input capture)
- ChatOutputModule (new, response display)
- Wiring config persistence (save/load/auto-save)
- Migration path from v1.2 to v1.3
- Example wiring configurations

**Addresses features:**
- Save/load wiring configuration (table stakes)
- Auto-save on change (table stakes)
- Module lifecycle integration (table stakes)

**Avoids pitfalls:**
- Pitfall #3: Breaking existing features (integration tests from Phase 1 validate)
- Pitfall #6: Wiring config deserialization failures (versioning, graceful degradation)

**Research flag:** Standard patterns, skip research-phase. Module refactoring follows established patterns from v1.0-v1.2.

### Phase Ordering Rationale

- **Phase 1 before Phase 2:** Port type system is foundation for wiring engine. Testing infrastructure must exist before refactoring to avoid breaking v1.2 features.
- **Phase 2 before Phase 3:** Visual editor needs functional wiring engine to validate connections and show execution state. Cycle detection must exist before users can create arbitrary connections.
- **Phase 3 before Phase 4:** Module refactoring demonstrates the system works, but visual editor is the primary deliverable. Users can manually edit JSON configs if modules aren't refactored yet.
- **Phase 4 validates entire system:** Refactoring existing features proves the port-based architecture works for real use cases, not just toy examples.

This ordering follows the dependency graph from ARCHITECTURE.md: PortType → PortMetadata → IPortProvider → PortRegistry → WiringEngine → WiringEditor → Module Refactoring.

### Research Flags

**Phases needing deeper research during planning:**
- **Phase 3 (Visual Editor):** Performance optimization needs validation. Specific throttling values (50ms? 100ms?), ShouldRender patterns, and JS interop boundaries need testing with 10+ modules on canvas. Research should focus on Blazor Server rendering performance with SignalR.

**Phases with standard patterns (skip research-phase):**
- **Phase 1 (Port System):** Type systems and validation are well-documented. C# enums and records are proven patterns.
- **Phase 2 (Wiring Engine):** Topological sort is textbook algorithm (Kahn's or DFS-based). Cycle detection is standard graph theory.
- **Phase 4 (Module Refactoring):** Follows established module patterns from v1.0-v1.2. No new concepts.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | .NET 8.0 built-ins confirmed via codebase analysis. HTML5 + SVG approach validated in Blazor community articles. No new dependencies = no compatibility issues. |
| Features | MEDIUM-HIGH | Table stakes features match industry standards (Unreal Blueprint, Node-RED, n8n). MVP prioritization is clear. Differentiators are well-scoped. |
| Architecture | MEDIUM | Core patterns (topological sort, two-phase init, port-based routing) are well-established. Specific integration with existing EventBus and AssemblyLoadContext needs validation during implementation. |
| Pitfalls | MEDIUM | Top 10 pitfalls identified from Blazor Server performance issues, module refactoring risks, and node editor UX patterns. Mitigation strategies are concrete. Recovery costs estimated. |

**Overall confidence:** MEDIUM-HIGH

Research provides clear direction for implementation. Stack choices are validated, feature scope is well-defined, architecture patterns are proven. Main uncertainty is performance at scale (10+ modules) and specific integration details with existing EventBus/module loading.

### Gaps to Address

- **SignalR rendering performance:** Specific throttling values (50ms vs 100ms) need empirical testing with 10+ modules. Research suggests throttling is necessary but exact values depend on module complexity and connection count. Plan to implement telemetry in Phase 3 to measure actual render times.

- **EventBus ordering semantics:** Current EventBus uses ConcurrentBag which doesn't guarantee ordering. Research identifies this as Pitfall #5 but doesn't specify whether ordering is actually required for v1.3 use cases. Plan to document ordering guarantees (or lack thereof) in Phase 2 and add sequence numbers only if testing reveals race conditions.

- **Module initialization dependencies:** Two-phase initialization pattern is recommended but existing v1.2 modules may have implicit dependencies not captured in code. Plan to audit existing module loading in Phase 1 and document all dependencies before refactoring.

- **Type system extensibility:** Starting with two types (Text, Trigger) is validated, but research doesn't specify how to add new types in v1.4+ without breaking v1.3 wiring configs. Plan to include version field in WiringConfig and design type system with forward compatibility in mind.

## Sources

### Primary (HIGH confidence)
- OpenAnima codebase analysis — Existing EventBus, module loading, AssemblyLoadContext patterns
- .NET 8.0 documentation — Built-in libraries (System.Text.Json, System.Linq, System.Collections.Concurrent)
- W3C standards — HTML5 Drag & Drop API, SVG path rendering
- Microsoft Blazor documentation — JSInterop patterns, SignalR integration

### Secondary (MEDIUM confidence)
- [Blazor Basics: Building Drag-and-Drop Functionality](https://www.telerik.com/blogs/blazor-basics-building-drag-drop-functionality-blazor-applications) — HTML5 drag-and-drop patterns
- [Investigating Drag and Drop with Blazor](https://chrissainty.com/investigating-drag-and-drop-with-blazor/) — Native API usage
- [Topological Sort - Neo4j Graph Data Science](https://neo4j.com/docs/graph-data-science/current/algorithms/dag/topological-sort/) — Algorithm reference
- [Blazor WASM Drag and Drop Performance Issues - Reddit](https://www.reddit.com/r/Blazor/comments/1i0n9js/blazor_wasm_drag_and_drop_performance_issues/) — Performance pitfalls
- [.Net6 Blazor SignalR Hub Connection causing high CPU](https://github.com/dotnet/aspnetcore/issues/39482) — SignalR performance issues
- [Really poor performance and latency of controls with multiple](https://github.com/dotnet/aspnetcore/issues/19739) — Blazor rendering bottlenecks

### Tertiary (LOW confidence)
- Training data on Unreal Blueprint, Unity Visual Scripting, Node-RED, n8n, Blender nodes — Industry patterns for node-based editors
- [2026: The Year of the Node-Based Editor](https://medium.com/@fadimantium/2026-the-year-of-the-node-based-editor-941f0f15d467) — WebSearch only, fetch blocked
- [Designing your own node-based visual programming language](https://dev.to/cosmomyzrailgorynych/designing-your-own-node-based-visual-programming-language-2mpg) — WebSearch only, fetch blocked

---
*Research completed: 2026-02-25*
*Ready for roadmap: yes*
