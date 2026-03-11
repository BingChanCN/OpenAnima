# Phase 12: Wiring Engine & Execution Orchestration - Context

**Gathered:** 2026-02-25
**Status:** Ready for planning

<domain>
## Phase Boundary

Execute modules in topological order based on port connections with cycle detection. Includes wiring configuration persistence (save/load JSON) and data routing between connected ports. Visual editor is Phase 13; module refactoring is Phase 14.

</domain>

<decisions>
## Implementation Decisions

### Execution Model
- Level-parallel execution: modules at the same topological level run concurrently, levels execute sequentially
- Isolated failure: errored module's downstream nodes are skipped, unaffected branches continue
- Event-driven triggering: graph executes on-demand when a trigger event occurs (e.g., user message, timer fire), not on a tick loop
- No execution status events for now — status broadcasting deferred to Phase 13/14 integration

### Wiring Configuration Format
- Single JSON file per configuration
- Contains both logical topology (module IDs, port connections) and visual layout (node positions, sizes) for Phase 13 readiness
- Multi-configuration support: users can create, switch between, and manage multiple wiring configurations
- Strict validation on load: referenced modules must exist and port types must match, otherwise reject the configuration

### Data Routing Semantics
- Push-based: module pushes output data to all connected downstream input ports after execution
- Fan-out uses deep copy: each downstream input port receives an independent copy of the data
- Trust connection-time type validation (Phase 11 port type system), no runtime type re-checking
- Multi-input trigger policy is module-defined: each module developer decides when their module fires (wait-all, any-input, custom logic)

### Claude's Discretion
- JSON schema design details (field names, nesting structure)
- Topological sort algorithm choice
- Cycle detection algorithm choice
- Deep copy implementation strategy
- Configuration file naming and storage location conventions
- Error message wording for cycle detection and validation failures

</decisions>

<specifics>
## Specific Ideas

- Multi-config support feels like "presets" — user can have different wiring setups and switch between them
- Module trigger policy should be part of the module's port declaration interface (from Phase 11)
- Strict validation now, auto-download missing modules is a future idea

</specifics>

<deferred>
## Deferred Ideas

- Auto-download missing modules when loading configuration — future capability
- Execution status event broadcasting (running/completed/failed per module) — Phase 13/14

</deferred>

---

*Phase: 12-wiring-engine-execution-orchestration*
*Context gathered: 2026-02-25*
