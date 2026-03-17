# Requirements: OpenAnima v1.8

**Defined:** 2026-03-16
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

## v1.8 Requirements

Requirements for SDK Runtime Parity milestone. Each maps to roadmap phases.

### Plugin DI Injection

- [ ] **PLUG-01**: PluginLoader reflects external module constructor parameters and injects IModuleConfig/IModuleContext/IEventBus/ICrossAnimaRouter via FullName matching against host DI container
- [ ] **PLUG-02**: PluginLoader creates typed ILogger instances for external modules via ILoggerFactory
- [ ] **PLUG-03**: Optional constructor parameters resolve to null with warning log on failure; required parameters produce clear LoadResult error

### Structured Messages

- [ ] **MSG-01**: ChatMessageInput record type moved from OpenAnima.Core.LLM to OpenAnima.Contracts; Core retains using alias for backward compatibility
- [ ] **MSG-02**: LLMModule has new `messages` input port (PortType.Text) accepting JSON-serialized List<ChatMessageInput>; messages port takes priority over prompt port when both fire
- [ ] **MSG-03**: Contracts provides ChatMessageInput.SerializeList / DeserializeList static helper methods using System.Text.Json

### Module Storage

- [ ] **STOR-01**: IModuleContext exposes GetDataDirectory(string moduleId) returning per-Anima per-Module path (data/animas/{animaId}/module-data/{moduleId}/); directory auto-created on first call

### SDK Validation

- [ ] **ECTX-01**: External ContextModule built via SDK loads into runtime, maintains per-Anima in-session conversation history, serializes as List<ChatMessageInput> JSON to LLMModule messages port
- [ ] **ECTX-02**: ContextModule persists conversation history to DataDirectory/history.json; history restored on application restart

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| New PortType enum values (Image, JSON, Binary) | JSON-on-Text sufficient for v1.8; port type extension deferred |
| ILLMService move to Contracts | Requires ChatMessageInput + LLMResult + streaming types; too broad for v1.8 |
| Per-Anima module instances (ANIMA-08) | Requires DI restructure; global singleton kept |
| Module management UI (MODMGMT-01/02/03/06) | Separate milestone scope |
| STOR-02 Anima delete cleanup | module-data lives inside Anima directory — already cleaned up on delete |
| Dynamic port counts | Requires wiring engine changes; deferred |
| UI extension points for modules | Requires Blazor plugin architecture; deferred |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| PLUG-01 | Phase 38 | Pending |
| PLUG-02 | Phase 38 | Pending |
| PLUG-03 | Phase 38 | Pending |
| MSG-01 | Phase 39 | Pending |
| MSG-02 | Phase 39 | Pending |
| MSG-03 | Phase 39 | Pending |
| STOR-01 | Phase 40 | Pending |
| ECTX-01 | Phase 41 | Pending |
| ECTX-02 | Phase 41 | Pending |

**Coverage:**
- v1.8 requirements: 9 total
- Mapped to phases: 9
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-16*
*Last updated: 2026-03-16 after initial definition*
