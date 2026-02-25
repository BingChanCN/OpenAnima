# Pitfalls Research

**Domain:** Visual node/wiring editor + port-based module system in Blazor Server
**Researched:** 2026-02-25
**Confidence:** MEDIUM

## Critical Pitfalls

### Pitfall 1: Circular Dependency Deadlock

**What goes wrong:**
Modules wired in a circular pattern (A → B → C → A) cause execution to hang or infinite loop. The wiring engine attempts to execute modules in dependency order but cannot resolve cycles, leading to runtime deadlock or stack overflow.

**Why it happens:**
Visual editors make it trivially easy to create cycles by dragging connections. Users don't think in terms of directed acyclic graphs (DAGs) — they think "chat output should trigger LLM, LLM output should show in chat" without realizing this creates a cycle. The UI doesn't prevent invalid topologies at wire-time.

**How to avoid:**
- Implement topological sort validation before saving wiring configuration
- Detect cycles using depth-first search during connection creation
- Block invalid connections in the UI with clear error messages
- Add visual feedback (red highlight) when hovering over a port that would create a cycle
- Consider allowing cycles but requiring explicit "cycle breaker" nodes

**Warning signs:**
- Wiring configuration saves successfully but execution hangs on first run
- Stack overflow exceptions during module execution
- Modules execute in unexpected order or not at all
- Debugger shows same module being entered repeatedly

**Phase to address:**
Phase 2 (Wiring Engine) — validation must be built into the connection logic, not added later

---

### Pitfall 2: SignalR Rendering Bottleneck from Frequent Graph Updates

**What goes wrong:**
Every node position change, connection drag, or property edit triggers StateHasChanged, causing full component re-renders. With 10+ modules on canvas, dragging becomes laggy (200ms+ delay). SignalR circuit bandwidth saturates with render diffs, causing UI freezes or circuit disconnects.

**Why it happens:**
Blazor Server sends render diffs over SignalR for every StateHasChanged call. Visual editors generate hundreds of state changes per second during drag operations (mousemove events at 60fps). Default Blazor rendering is synchronous and blocks the UI thread. Developers treat Blazor components like WPF/WinForms without considering the network round-trip cost.

**How to avoid:**
- Throttle StateHasChanged during drag operations (update every 50-100ms, not every mousemove)
- Use JavaScript interop for drag rendering, sync to Blazor state only on drop
- Implement ShouldRender() to prevent unnecessary re-renders of unchanged components
- Use @key directives on node components to prevent full tree diffs
- Consider canvas-based rendering (HTML5 Canvas or SVG) with JS interop instead of Blazor components for node positions
- Batch multiple state changes into single render cycle

**Warning signs:**
- Mouse cursor lags behind during drag operations
- Browser DevTools shows SignalR messages queuing up
- Circuit reconnection messages in browser console
- CPU usage spikes on server during simple drag operations
- Users report "sluggish" or "unresponsive" editor

**Phase to address:**
Phase 3 (Visual Editor) — must be architected for performance from the start, retrofitting is expensive

---

### Pitfall 3: Breaking Existing Features During Modularization

**What goes wrong:**
Refactoring hardcoded LLM/chat/heartbeat into modules breaks existing functionality. Chat stops working, heartbeat doesn't tick, or LLM calls fail silently. Users who upgraded from v1.2 lose working features. Rollback is difficult because database schema or config format changed.

**Why it happens:**
Hardcoded features have implicit dependencies and execution order guarantees that aren't obvious until removed. EventBus message ordering changes when moving from direct calls to async pub/sub. Timing assumptions break (e.g., "heartbeat always runs before LLM check"). Initialization order changes when modules load dynamically instead of at startup. Developers focus on "making modules work" without comprehensive regression testing of existing workflows.

**How to avoid:**
- Create integration tests for existing workflows BEFORE refactoring (chat end-to-end, heartbeat ticking, LLM streaming)
- Implement feature flags to toggle between old hardcoded path and new modular path
- Keep old code paths intact until new modules are fully validated
- Document all implicit dependencies and timing assumptions before refactoring
- Test with actual v1.2 configuration files to ensure migration path works
- Maintain backward compatibility for at least one version

**Warning signs:**
- Tests pass but manual testing reveals broken workflows
- Features work in isolation but fail when combined
- Timing-sensitive operations become unreliable
- Error messages that were clear become generic or missing
- Configuration that worked in v1.2 causes errors in v1.3

**Phase to address:**
Phase 1 (Port System) — establish testing infrastructure and compatibility strategy before touching existing code

---

### Pitfall 4: Type System Too Rigid or Too Loose

**What goes wrong:**
Port type system either prevents valid connections (too rigid) or allows invalid connections that fail at runtime (too loose). Users frustrated by "why can't I connect these?" or surprised by runtime type errors after successful wiring.

**Why it happens:**
Designing type systems requires balancing safety vs. flexibility. Starting with only "Text" and "Trigger" seems simple but real-world data has nuances (plain text vs. JSON vs. Markdown, one-shot trigger vs. continuous stream). Developers either over-engineer with too many types upfront or under-engineer and add types later, breaking existing wiring configs.

**How to avoid:**
- Start with minimal types (Text, Trigger) and explicit conversion nodes for edge cases
- Design type system to be extensible without breaking existing configs
- Use structural typing (duck typing) where possible instead of nominal typing
- Provide clear error messages when type mismatch occurs, suggesting conversion nodes
- Version the type system and support migration of old wiring configs
- Consider "any" type for prototyping with runtime validation

**Warning signs:**
- Users frequently request "why can't I connect X to Y?"
- Many modules need multiple output ports with same data in different types
- Type conversion nodes proliferate in every wiring config
- Runtime errors about type mismatches despite successful wiring
- Frequent type system refactors that break existing configs

**Phase to address:**
Phase 1 (Port System) — type system design is foundational, changing it later breaks everything

---

### Pitfall 5: EventBus Ordering Guarantees Lost

**What goes wrong:**
Existing code assumes EventBus messages arrive in publish order, but modular execution breaks this assumption. Chat messages arrive out of order, LLM responses interleave, or heartbeat events skip. Race conditions appear that never existed in hardcoded version.

**Why it happens:**
Current EventBus uses ConcurrentBag which doesn't guarantee ordering. Hardcoded features had implicit ordering from call stack. Moving to port-based wiring introduces async execution and parallel module processing. Multiple modules publishing to same port create race conditions. Developers assume "wiring order = execution order" but runtime uses topological sort which may differ.

**How to avoid:**
- Document EventBus ordering guarantees (or lack thereof) explicitly
- Add sequence numbers to messages if ordering matters
- Implement execution barriers or synchronization points in wiring engine
- Use queue-based ports for ordered message streams vs. signal-based ports for unordered events
- Test with artificial delays to expose race conditions during development
- Consider single-threaded execution mode for deterministic debugging

**Warning signs:**
- Intermittent failures that don't reproduce consistently
- Messages arrive in different order on different runs
- Chat UI shows responses before questions
- Heartbeat tick count jumps or goes backward
- Modules process stale data from previous execution cycle

**Phase to address:**
Phase 2 (Wiring Engine) — execution semantics must be defined before modules depend on them

---

### Pitfall 6: Wiring Config Deserialization Failures

**What goes wrong:**
Saved wiring configurations fail to load after module updates, type changes, or port renames. Users lose their carefully constructed agent wiring. Error messages are cryptic ("port not found") without indicating which module or version caused the issue.

**Why it happens:**
Wiring configs reference modules by name/ID and ports by string identifiers. Module developers rename ports, change types, or remove features without considering backward compatibility. No versioning strategy for wiring configs. Deserialization code assumes all referenced modules are loaded and ports exist. Partial loading (some modules missing) not handled gracefully.

**How to avoid:**
- Version wiring config format and support migration
- Store module version in wiring config and validate on load
- Implement graceful degradation (load what's possible, mark missing modules)
- Provide clear error messages with module name, expected port, and available ports
- Add config validation tool that checks before loading
- Consider port aliasing to support renames without breaking configs

**Warning signs:**
- Wiring configs that worked yesterday fail to load today
- Errors mention port names that don't exist in current module versions
- Users report "lost all my wiring" after module update
- No way to inspect or repair broken configs
- Config files grow stale and become unloadable

**Phase to address:**
Phase 4 (Config Persistence) — versioning and migration must be designed upfront, not patched later

---

### Pitfall 7: Module Initialization Order Dependencies

**What goes wrong:**
Modules fail to initialize because they depend on other modules being loaded first. LLM module needs EventBus injected, chat module needs LLM module registered, heartbeat module needs all others ready. Initialization order is non-deterministic when loading from wiring config, causing intermittent startup failures.

**Why it happens:**
Current system uses property injection for EventBus after module load. Moving to port-based wiring adds more dependencies (port registry, wiring engine, other modules). No explicit dependency declaration in module manifest. Initialization happens in file system order or wiring config order, not dependency order. Circular initialization dependencies possible (A needs B, B needs A).

**How to avoid:**
- Implement two-phase initialization (construct all modules, then wire all ports)
- Add explicit dependency declaration in module manifest
- Use dependency injection container with proper lifetime management
- Validate initialization order before starting runtime
- Provide clear error messages when initialization fails with dependency chain
- Consider lazy initialization (modules initialize on first use, not at load)

**Warning signs:**
- Modules work when loaded manually in specific order but fail from wiring config
- NullReferenceException during module initialization
- Some modules initialize, others don't, no clear pattern
- Startup time increases as more modules added
- Intermittent "module not ready" errors

**Phase to address:**
Phase 1 (Port System) — initialization strategy must be defined before building modules

---

### Pitfall 8: No Undo/Redo in Visual Editor

**What goes wrong:**
Users accidentally delete connections or move nodes, no way to undo. Must manually recreate complex wiring from memory. Frustration leads to abandoning visual editor in favor of manual config editing. Users afraid to experiment because mistakes are permanent.

**Why it happens:**
Undo/redo seems like "nice to have" feature, gets deprioritized. Implementing undo after editor is built requires refactoring all state mutations. Command pattern or memento pattern not designed in from start. Blazor state management doesn't provide built-in undo. Developers underestimate how often users make mistakes in visual editors.

**How to avoid:**
- Design state management with undo in mind from Phase 3 start
- Use command pattern for all editor operations (add node, delete connection, move node)
- Implement undo stack with reasonable depth (20-50 operations)
- Provide keyboard shortcuts (Ctrl+Z, Ctrl+Y) that users expect
- Show undo/redo availability in UI (grayed out when unavailable)
- Consider auto-save with version history as alternative

**Warning signs:**
- User feedback requests undo feature repeatedly
- Users manually save config before every change "just in case"
- Support requests about "how to restore deleted connection"
- Users avoid visual editor for complex wiring
- High rate of config file corruption from manual editing

**Phase to address:**
Phase 3 (Visual Editor) — must be architected from start, retrofitting is major refactor

---

### Pitfall 9: Port Data Serialization Assumptions

**What goes wrong:**
Modules assume port data is specific .NET types (string, int, custom classes) but wiring engine serializes everything to JSON for persistence/transmission. Type information lost, deserialization fails, or data corruption occurs. Custom types from module assemblies can't be deserialized in core runtime.

**Why it happens:**
Port system needs to persist data for debugging, logging, or cross-process communication. JSON is obvious choice but loses type fidelity. Modules developed in isolation assume direct object passing. No contract for what types are allowed on ports. Developers use complex types without considering serialization.

**How to avoid:**
- Restrict port data to primitive types and simple DTOs
- Document serialization contract in port type system
- Validate data serializability when module registers ports
- Use schema validation (JSON Schema) for complex port types
- Provide serialization testing utilities for module developers
- Consider protobuf or MessagePack for better type preservation

**Warning signs:**
- Runtime errors about "cannot deserialize type X"
- Data looks correct in debugger but wrong after passing through port
- Custom module types cause crashes in core runtime
- Polymorphic types lose derived type information
- Circular references cause serialization to hang

**Phase to address:**
Phase 1 (Port System) — serialization contract must be defined before modules are built

---

### Pitfall 10: Visual Editor State Diverges from Runtime State

**What goes wrong:**
Visual editor shows modules connected and running, but runtime has different wiring. User changes wiring in editor, sees visual update, but runtime still uses old configuration. Debugging becomes impossible because what you see isn't what's executing.

**Why it happens:**
Editor state (Blazor component state) and runtime state (wiring engine) are separate. No synchronization mechanism or it's unreliable. Changes in editor don't trigger runtime reload, or reload fails silently. SignalR updates from runtime don't update editor UI. Users assume "save" applies changes immediately but runtime requires restart.

**How to avoid:**
- Single source of truth for wiring state (runtime owns it, editor is view)
- Editor changes immediately reflected in runtime (hot reload) or clearly marked as "pending"
- Visual indicators for state: "saved", "running", "modified", "error"
- Validation that editor state matches runtime state on load
- Automatic sync or clear "apply changes" button with feedback
- Prevent editing while runtime is executing (or support hot reload properly)

**Warning signs:**
- Users report "I changed the wiring but nothing happened"
- Debugging shows different connections than editor displays
- Restart required after every wiring change
- No feedback when changes applied or failed
- Editor shows success but runtime logs errors

**Phase to address:**
Phase 3 (Visual Editor) — synchronization architecture must be designed upfront

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Skip undo/redo in editor | Faster Phase 3 delivery | Users afraid to experiment, high support burden | Never — users expect this |
| Allow any .NET type on ports | Modules easier to write | Serialization failures, cross-version incompatibility | Only for internal modules in v1.3 MVP |
| Manual JSON config instead of visual editor | No editor complexity | Non-technical users can't use platform | Only for developer preview |
| Single-threaded wiring execution | Simpler engine, deterministic | Can't utilize multi-core, slow for large graphs | Acceptable until >20 modules |
| No wiring config versioning | Simpler persistence | Breaking changes lose user work | Never — users will lose data |
| Hardcode port types (Text, Trigger) | Fast to implement | Can't extend without breaking changes | Acceptable for v1.3, must refactor by v1.4 |
| Skip cycle detection | Faster wiring engine | Runtime hangs, bad UX | Never — causes support nightmares |
| No module dependency declaration | Simpler module API | Initialization order bugs | Only if initialization is two-phase |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Blazor + Canvas drag/drop | Using Blazor events for mousemove (too slow) | JS interop for drag, sync to Blazor on drop only |
| EventBus + Wiring Engine | Assuming message order matches wiring order | Add sequence numbers or use ordered queues |
| AssemblyLoadContext + Ports | Passing module types directly across contexts | Use interface name matching, not type identity |
| SignalR + Visual Editor | Pushing every state change to client | Throttle updates, batch changes, use ShouldRender |
| Module Loading + DI | Injecting dependencies before module loaded | Two-phase init: load all, then inject all |
| JSON Config + Module Versions | No version in config file | Store module version, validate on load |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| StateHasChanged on every mousemove | Laggy drag, high CPU | Throttle to 50-100ms, use JS interop | >5 modules on canvas |
| Full component tree re-render | Entire editor flickers on change | Use @key, ShouldRender, localized state | >10 modules |
| Synchronous wiring execution | UI freezes during execution | Async execution with progress feedback | Execution >100ms |
| No render batching | SignalR bandwidth saturation | Batch multiple changes, throttle updates | >20 state changes/sec |
| Deep object cloning for undo | Memory pressure, GC pauses | Immutable state, structural sharing | >50 undo operations |
| Linear search for port lookup | Slow connection validation | Dictionary/hash-based lookup | >50 ports total |
| Eager module initialization | Slow startup | Lazy initialization on first use | >10 modules |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Executing untrusted module code without sandbox | Malicious modules access file system, network | AssemblyLoadContext isolation, permission system |
| Storing LLM API keys in wiring config | Keys leaked in config files shared/committed | Separate secrets management, reference by ID |
| No validation of port data | Injection attacks via crafted port messages | Schema validation, input sanitization |
| Allowing modules to load arbitrary assemblies | Privilege escalation, sandbox escape | Whitelist allowed assemblies, code signing |
| Exposing internal runtime APIs to modules | Modules bypass port system, break isolation | Minimal public API surface, internal types |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| No visual feedback during drag | Unclear where connection will land | Show preview line, highlight valid targets |
| Cryptic error messages ("port not found") | Users don't know how to fix | "Module 'LLM' port 'Output' not found. Available ports: Response, Error" |
| No indication of execution flow | Can't debug why agent behaves wrong | Highlight active modules, show data flow animation |
| Wiring changes require restart | Frustrating iteration cycle | Hot reload or clear "apply" button with feedback |
| Can't zoom/pan large graphs | Unusable with >20 modules | Canvas zoom, minimap, search/filter |
| No templates or examples | Blank canvas intimidating | Provide "Chat Agent" template, example configs |
| Accidental deletions | Lost work, frustration | Confirmation dialogs, undo/redo |

## "Looks Done But Isn't" Checklist

- [ ] **Visual Editor:** Often missing zoom/pan — verify usable with 20+ modules on canvas
- [ ] **Wiring Engine:** Often missing cycle detection — verify rejects circular dependencies
- [ ] **Port System:** Often missing serialization validation — verify all port types survive JSON round-trip
- [ ] **Module Refactor:** Often missing regression tests — verify existing chat/LLM/heartbeat workflows still work
- [ ] **Config Persistence:** Often missing version migration — verify v1.2 configs load in v1.3
- [ ] **Error Handling:** Often missing user-friendly messages — verify errors explain what's wrong and how to fix
- [ ] **Performance:** Often missing throttling — verify smooth drag with 10+ modules
- [ ] **State Sync:** Often missing runtime-editor sync — verify editor shows actual runtime state
- [ ] **Undo/Redo:** Often missing entirely — verify Ctrl+Z works for all operations
- [ ] **Type Safety:** Often missing runtime validation — verify type mismatches caught at wire-time, not runtime

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Circular dependency deadlock | LOW | Add cycle detection to wiring engine, validate existing configs, provide fix tool |
| SignalR rendering bottleneck | MEDIUM | Refactor drag to JS interop, add throttling, implement ShouldRender |
| Breaking existing features | HIGH | Rollback to v1.2, add integration tests, re-implement with feature flags |
| Type system too rigid/loose | HIGH | Design new type system, implement migration, support both during transition |
| EventBus ordering lost | MEDIUM | Add sequence numbers to messages, document ordering guarantees, fix race conditions |
| Wiring config deserialization | LOW | Add version field, implement migration, provide repair tool |
| Module initialization order | MEDIUM | Implement two-phase init, add dependency declaration, validate order |
| No undo/redo | HIGH | Refactor state management to command pattern, implement undo stack |
| Port data serialization | MEDIUM | Define serialization contract, validate existing modules, provide migration guide |
| Editor/runtime state divergence | MEDIUM | Implement sync mechanism, add state validation, provide clear feedback |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Circular dependency deadlock | Phase 2 (Wiring Engine) | Unit test with circular config, verify rejection with clear error |
| SignalR rendering bottleneck | Phase 3 (Visual Editor) | Performance test: drag 10 modules at 60fps without lag |
| Breaking existing features | Phase 1 (Port System) | Integration tests: v1.2 chat workflow passes in v1.3 |
| Type system too rigid/loose | Phase 1 (Port System) | Test matrix: all valid connections allowed, invalid rejected |
| EventBus ordering lost | Phase 2 (Wiring Engine) | Stress test: 1000 messages arrive in publish order |
| Wiring config deserialization | Phase 4 (Config Persistence) | Load v1.3.0 config in v1.3.1, verify migration |
| Module initialization order | Phase 1 (Port System) | Load modules in random order, verify deterministic init |
| No undo/redo | Phase 3 (Visual Editor) | Manual test: Ctrl+Z after delete, verify restoration |
| Port data serialization | Phase 1 (Port System) | Round-trip test: all port types serialize/deserialize correctly |
| Editor/runtime state divergence | Phase 3 (Visual Editor) | Change wiring, verify runtime reflects change immediately |

## Sources

- [Blazor WASM Drag and Drop Performance Issues - Reddit](https://www.reddit.com/r/Blazor/comments/1i0n9js/blazor_wasm_drag_and_drop_performance_issues/)
- [.Net6 Blazor SignalR Hub Connection causing high CPU](https://github.com/dotnet/aspnetcore/issues/39482)
- [Really poor performance and latency of controls with multiple](https://github.com/dotnet/aspnetcore/issues/19739)
- [Refactoring Module Dependencies - Martin Fowler](https://martinfowler.com/articles/refactoring-dependencies.html)
- [7 Costly Mistakes to Avoid When Architecting a Multi-Module Mobile App](https://medium.com/@sharmapraveen91/7-costly-mistakes-to-avoid-when-architecting-a-multi-module-mobile-app-8ca8a7293963)
- [Topological Sort - Neo4j Graph Data Science](https://neo4j.com/docs/graph-data-science/current/algorithms/dag/topological-sort/)
- [Detect cycle in Directed Graph using Topological Sort](https://www.geeksforgeeks.org/dsa/detect-cycle-in-directed-graph-using-topological-sort/)
- [Blazor Webassembly SVG Drag And Drop](https://medium.com/codex/blazor-webassembly-svg-drag-and-drop-e680769ac682)
- [SVG Performance - Perf issues with thousands of path elements](https://www.reddit.com/r/learnjavascript/comments/3l2odo/svg_performance_perf_issues_with_thousands_of/)
- OpenAnima project context and existing architecture decisions

---
*Pitfalls research for: Visual node/wiring editor + port-based module system in Blazor Server*
*Researched: 2026-02-25*
