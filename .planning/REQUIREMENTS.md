# Requirements: OpenAnima v1.7

**Defined:** 2026-03-14
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

## v1.7 Requirements

Requirements for v1.7 Runtime Foundation. Each maps to roadmap phases.

### Concurrency

- [x] **CONC-01**: Module execution is race-free — no concurrent writes to shared mutable fields (`_pendingPrompt`, `_failedModules`, `_state`, etc.)
- [x] **CONC-02**: WiringEngine._failedModules uses thread-safe collection (ConcurrentDictionary) instead of HashSet
- [x] **CONC-03**: LLMModule._pendingPrompt race condition is eliminated via local capture or Channel<T>
- [x] **CONC-04**: Each module has SemaphoreSlim(1,1) execution guard with skip-when-busy semantics
- [x] **CONC-05**: ActivityChannel component serializes all state-mutating work per Anima (HeartbeatTick, UserMessage, IncomingRoute)
- [x] **CONC-06**: Stateful Anima has named activity channels (heartbeat, chat) — parallel between channels, serial within each
- [x] **CONC-07**: Stateless/mechanical Anima supports concurrent request-level execution without channel serialization
- [x] **CONC-08**: Modules can declare concurrency mode via [StatelessModule] attribute — runtime enforces correct execution strategy
- [x] **CONC-09**: HeartbeatLoop enqueues via TryWrite (never WriteAsync) to prevent deadlock in tick path
- [x] **CONC-10**: Pre-existing 3 test failures are resolved before concurrency work begins (clean baseline)

### Module API

- [x] **API-01**: IModuleConfig interface (config read/write) exists in OpenAnima.Contracts
- [x] **API-02**: IAnimaContext (or IModuleContext with immutable AnimaId) exists in OpenAnima.Contracts
- [x] **API-03**: ICrossAnimaRouter interface exists in OpenAnima.Contracts with type-forward shim in Core
- [x] **API-04**: IModuleConfigSchema interface in Contracts — modules declare config fields, platform auto-renders sidebar
- [x] **API-05**: Binary compatibility maintained — type-forward aliases in old Core namespaces for moved interfaces
- [ ] **API-06**: Canary .oamod round-trip test validates external plugin compatibility after interface moves
- [ ] **API-07**: External modules achieve feature parity with built-in modules via Contracts-only dependency

### Module Decoupling

- [ ] **DECPL-01**: All 14 built-in modules depend only on OpenAnima.Contracts — zero `using OpenAnima.Core.*` in module files
- [ ] **DECPL-02**: DI resolution succeeds for all 14 module types after decoupling (startup smoke test)
- [ ] **DECPL-03**: All existing tests compile and pass after module migration
- [ ] **DECPL-04**: `oani new` project template generates Contracts-only module code
- [ ] **DECPL-05**: ModuleMetadataRecord moved to Contracts so decoupled modules can reference it

## Future Requirements

### v1.8

- **MODMGMT-01**: User can view list of all installed modules
- **MODMGMT-02**: User can install module from .oamod package via UI
- **MODMGMT-03**: User can uninstall module via UI
- **MODMGMT-06**: User can search and filter modules by name
- **LIFECYCLE-01**: IModuleLifecycle context object — convenience DI wrapper

### v2+

- **ANIMA-08**: Per-Anima module instances — full DI restructure replacing global IEventBus singleton
- **URLVAL-01**: IUrlValidator/ISsrfGuard abstracted in Contracts for external HTTP modules
- **AUTOUI-01**: Auto-rendered config sidebar replacing per-module Razor components

## Out of Scope

| Feature | Reason |
|---------|--------|
| Per-Anima module instances (ANIMA-08 full fix) | Requires full DI restructure; v1.7 focuses on interface decoupling as prerequisite |
| ILLMService moved to Contracts | Requires moving ChatMessageInput + OpenAI SDK types; LLMModule can remain partially Core-dependent |
| Module marketplace / remote install | v1 supports local .oamod only |
| Auto-rendered config sidebar | Depends on IModuleConfigSchema adoption across all modules; v1.8+ |
| Module management UI (install/uninstall/search) | Deferred to v1.8 per user decision |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CONC-10 | Phase 32 | Complete |
| CONC-01 | Phase 33 | Complete |
| CONC-02 | Phase 33 | Complete |
| CONC-03 | Phase 33 | Complete |
| CONC-04 | Phase 33 | Complete |
| CONC-05 | Phase 34 | Complete |
| CONC-06 | Phase 34 | Complete |
| CONC-07 | Phase 34 | Complete |
| CONC-08 | Phase 34 | Complete |
| CONC-09 | Phase 34 | Complete |
| API-01 | Phase 35 | Complete |
| API-02 | Phase 35 | Complete |
| API-03 | Phase 35 | Complete |
| API-04 | Phase 35 | Complete |
| API-05 | Phase 35 | Complete |
| API-06 | Phase 35 | Pending |
| API-07 | Phase 35 | Pending |
| DECPL-01 | Phase 36 | Pending |
| DECPL-02 | Phase 36 | Pending |
| DECPL-03 | Phase 36 | Pending |
| DECPL-04 | Phase 36 | Pending |
| DECPL-05 | Phase 36 | Pending |

**Coverage:**
- v1.7 requirements: 22 total
- Mapped to phases: 22
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-14*
*Last updated: 2026-03-14 after initial definition*
