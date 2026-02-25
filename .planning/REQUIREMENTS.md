# Requirements: OpenAnima

**Defined:** 2026-02-25
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe

## v1.3 Requirements

Requirements for v1.3 True Modularization & Visual Wiring. Each maps to roadmap phases.

### Port System (端口类型系统)

- [x] **PORT-01**: User can see port type categories (Text, Trigger) on module ports with visual color distinction
- [x] **PORT-02**: User cannot connect ports of different types — editor rejects with visual feedback
- [x] **PORT-03**: User can connect one output port to multiple input ports (fan-out)
- [x] **PORT-04**: Modules declare input/output ports via typed interface, discoverable at load time

### Wiring Engine (连线引擎)

- [x] **WIRE-01**: Runtime executes modules in topological order based on wiring connections
- [x] **WIRE-02**: Runtime detects and rejects circular dependencies at wire-time with clear error message
- [x] **WIRE-03**: Wiring engine routes data between connected ports during execution

### Visual Editor (可视化编辑器)

- [ ] **EDIT-01**: User can drag modules from palette onto canvas to place them
- [ ] **EDIT-02**: User can pan canvas by dragging background and zoom with mouse wheel
- [x] **EDIT-03**: User can drag from output port to input port to create connection with bezier curve preview
- [ ] **EDIT-04**: User can click to select nodes/connections and press Delete to remove them
- [x] **EDIT-05**: User can save wiring configuration to JSON and load it back with full graph restoration
- [x] **EDIT-06**: Editor auto-saves wiring configuration after changes

### Refactored Modules (官方模块拆分)

- [ ] **RMOD-01**: LLM service refactored into LLMModule with typed input/output ports
- [ ] **RMOD-02**: Chat input refactored into ChatInputModule with output port
- [ ] **RMOD-03**: Chat output refactored into ChatOutputModule with input port
- [ ] **RMOD-04**: Heartbeat refactored into HeartbeatModule with trigger port

### Runtime Integration (运行时集成)

- [ ] **RTIM-01**: Editor displays real-time module status (running, error, stopped) synced from runtime
- [ ] **RTIM-02**: Module errors during execution shown as visual indicators on corresponding nodes

### End-to-End (端到端验证)

- [ ] **E2E-01**: User can wire ChatInput → LLM → ChatOutput in editor and have a working conversation identical to v1.2

## Future Requirements

Deferred to future release. Tracked but not in current roadmap.

### Advanced Editor UX

- **EDIT-07**: User can undo/redo graph edits (Ctrl+Z/Y)
- **EDIT-08**: User can search and filter modules in palette
- **EDIT-09**: User can multi-select nodes (Ctrl+click or drag-box) for bulk operations
- **EDIT-10**: User can see live execution visualization (highlight active connections)

### Port System Enhancements

- **PORT-05**: User can hover over port to see tooltip explaining purpose
- **PORT-06**: Input ports enforce single connection (new connection replaces old)
- **PORT-07**: Unconnected input ports use default values

### Workflow Features

- **EDIT-11**: User can add comments/annotations to graph sections
- **EDIT-12**: User can collapse module groups into subgraphs
- **EDIT-13**: User can export graph as image for sharing

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Automatic layout | Users want control over positioning; auto-layout rarely matches intent |
| Inline code editing in nodes | Scope creep; modules should be proper C# classes |
| Visual scripting for module logic | Out of scope; this is module *wiring*, not module *creation* |
| AI-suggested connections | Unreliable; deterministic wiring is core value |
| Collaborative editing | Complex conflict resolution; single-user first |
| 3D node graph | Gimmick; 2D is proven and sufficient |
| Marketplace integration in editor | v1.3 is about wiring, not distribution |
| Breakpoints on nodes | Requires deep runtime integration, defer to v2+ |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| PORT-01 | Phase 11 | Complete |
| PORT-02 | Phase 11 | Complete |
| PORT-03 | Phase 11 | Complete |
| PORT-04 | Phase 11, Phase 12.5 (runtime fix) | Complete (test), Pending (runtime) |
| WIRE-01 | Phase 12, Phase 12.5 (DI fix) | Complete (test), Pending (runtime) |
| WIRE-02 | Phase 12, Phase 12.5 (DI fix) | Complete (test), Pending (runtime) |
| WIRE-03 | Phase 12, Phase 12.5 (DI fix) | Complete (test), Pending (runtime) |
| EDIT-01 | Phase 13 | Pending |
| EDIT-02 | Phase 13 | Pending |
| EDIT-03 | Phase 13 (depends on Phase 12.5) | Complete |
| EDIT-04 | Phase 13 | Pending |
| EDIT-05 | Phase 13 (depends on Phase 12.5) | Complete |
| EDIT-06 | Phase 13 (depends on Phase 12.5) | Complete |
| RMOD-01 | Phase 14 | Pending |
| RMOD-02 | Phase 14 | Pending |
| RMOD-03 | Phase 14 | Pending |
| RMOD-04 | Phase 14 | Pending |
| RTIM-01 | Phase 14 | Pending |
| RTIM-02 | Phase 14 | Pending |
| E2E-01 | Phase 14 (depends on Phase 12.5) | Pending |

**Coverage:**
- v1.3 requirements: 20 total (17 functional + 3 runtime integration fixes)
- Mapped to phases: 20
- Unmapped: 0 ✓

---
*Requirements defined: 2026-02-25*
*Last updated: 2026-02-26 after gap closure phase creation*
