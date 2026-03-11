# Phase 14: Module Refactoring & Runtime Integration - Context

**Gathered:** 2026-02-27
**Status:** Ready for planning

<domain>
## Phase Boundary

Refactor hardcoded LLM, chat, and heartbeat features into port-based modules. Define a Module SDK interface first, then implement concrete modules. Achieve v1.2 feature parity through wiring (ChatInput→LLM→ChatOutput). Editor displays real-time module status (read-only monitoring).

</domain>

<decisions>
## Implementation Decisions

### Module SDK Design
- SDK interface defined first, concrete modules (LLM/Chat/Heartbeat) implemented after
- Modules can have multiple input and output ports — developers design their own port layout and trigger logic
- Input sources are diverse: fixed text values, outputs from other modules, etc.
- Module SDK must account for different input source types and trigger mechanisms

### Module Isolation
- Modules are fully isolated — interaction only through ports and events
- No DI-shared services between modules; no direct access to other modules or global services
- All data exchange between modules must go through port connections

### Runtime Status Sync
- Event-driven real-time push from runtime to editor (not polling)
- Node border color indicates module state: green=running, red=error, gray=stopped
- Click on error node to pop up detailed error information (exception message, stack trace)
- Editor is read-only monitoring — no start/stop/restart controls from editor

### Claude's Discretion
- Specific Module SDK interface design (IModule, port registration API, lifecycle hooks)
- Trigger mechanism implementation details (which ports trigger execution)
- Event bus / SignalR implementation for status push
- Error detail panel UI layout
- Heartbeat module interval configuration approach

</decisions>

<specifics>
## Specific Ideas

- User emphasized that module SDK planning should come before module implementation — treat SDK as the foundation
- Input diversity is a key concern: some inputs are static config, others are dynamic data from upstream modules
- The trigger logic should be flexible enough for developers to define per-module behavior

</specifics>

<deferred>
## Deferred Ideas

- Module control from editor (start/stop/restart) — future phase
- Module marketplace or plugin discovery — out of scope

</deferred>

---

*Phase: 14-module-refactoring-runtime-integration*
*Context gathered: 2026-02-27*
