# Requirements: v1.9 Event-Driven Propagation Engine

## Propagation Engine

- [x] **PROP-01**: Module executes immediately when any input port receives data, without waiting for heartbeat-driven topological sort
- [x] **PROP-02**: Module output automatically fans out to all connected downstream ports, propagating like a wave through the network
- [x] **PROP-03**: Wiring topology allows cycles — connections that form loops are accepted and executed
- [x] **PROP-04**: Module can choose not to produce output on any execution, naturally terminating propagation at that point

## Heartbeat Refactor

- [x] **BEAT-05**: HeartbeatModule exists as a standalone timer signal source module, no longer drives the WiringEngine execution loop
- [x] **BEAT-06**: User can configure HeartbeatModule trigger interval via the module configuration sidebar

## Future Requirements (deferred)

- ANIMA-08: Per-Anima independent module instances (global singleton kept)
- MODMGMT-01/02/03/06: Module install/uninstall/search UI
- Propagation convergence control (TTL, energy decay, content-based dampening)
- Dynamic port count (TextJoin fixed 3 ports limitation)

## Out of Scope

- Convergence/dampening mechanisms — wait until real-world usage reveals need
- New module types (file, database, code execution) — engine-only milestone
- Dynamic topology changes at runtime — static wiring with cyclic support first
- Multi-language module support — C# only

## Traceability

| REQ-ID | Phase | Status |
|--------|-------|--------|
| PROP-01 | Phase 42 | Complete |
| PROP-02 | Phase 42 | Complete |
| PROP-03 | Phase 42 | Complete |
| PROP-04 | Phase 42 | Complete |
| BEAT-05 | Phase 43 | Complete |
| BEAT-06 | Phase 43 | Complete |

---
*Created: 2026-03-19 for milestone v1.9*
*Traceability updated: 2026-03-19 after roadmap creation*
