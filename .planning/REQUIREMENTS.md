# Requirements: OpenAnima v1.5

**Defined:** 2026-02-28
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

## v1.5 Requirements

### Anima Management (ANIMA)

- [x] **ANIMA-01**: User can create new Anima with custom name
- [x] **ANIMA-02**: User can view list of all Animas in global sidebar
- [x] **ANIMA-03**: User can switch between different Animas
- [x] **ANIMA-04**: User can delete Anima
- [x] **ANIMA-05**: User can rename Anima
- [x] **ANIMA-06**: User can clone existing Anima (duplicate configuration)
- [x] **ANIMA-07**: Each Anima has independent heartbeat loop
- [ ] **ANIMA-08**: Each Anima has independent module instances
- [ ] **ANIMA-09**: Each Anima has independent chat interface
- [x] **ANIMA-10**: Anima configuration persists across sessions

### Internationalization (I18N)

- [x] **I18N-01**: User can switch UI language between Chinese and English
- [x] **I18N-02**: All UI text displays in selected language
- [x] **I18N-03**: Language preference persists across sessions
- [x] **I18N-04**: Missing translations fall back to English

### Module Management (MODMGMT)

- [ ] **MODMGMT-01**: User can view list of all installed modules
- [ ] **MODMGMT-02**: User can install module from .oamod package
- [ ] **MODMGMT-03**: User can uninstall module
- [ ] **MODMGMT-04**: User can enable/disable module per Anima
- [ ] **MODMGMT-05**: User can view module information (name, version, author, description)
- [ ] **MODMGMT-06**: User can search and filter modules by name

### Module Configuration (MODCFG)

- [ ] **MODCFG-01**: User can click module in editor to show detail panel on right
- [ ] **MODCFG-02**: User can edit module-specific configuration in detail panel
- [ ] **MODCFG-03**: Module configuration persists per Anima
- [ ] **MODCFG-04**: Configuration changes validate before saving
- [ ] **MODCFG-05**: Detail panel shows module status and metadata

### Built-in Modules (BUILTIN)

- [ ] **BUILTIN-01**: Fixed text module outputs configurable text content
- [ ] **BUILTIN-02**: User can edit fixed text content in detail panel
- [ ] **BUILTIN-03**: Text concat module concatenates two text inputs
- [ ] **BUILTIN-04**: Text split module splits text by delimiter
- [ ] **BUILTIN-05**: Text merge module merges multiple inputs into one output
- [ ] **BUILTIN-06**: Conditional branch module routes based on condition expression
- [ ] **BUILTIN-07**: LLM module allows configuration of API URL in detail panel
- [ ] **BUILTIN-08**: LLM module allows configuration of API key in detail panel
- [ ] **BUILTIN-09**: LLM module allows configuration of model name in detail panel
- [ ] **BUILTIN-10**: Heartbeat module is optional (not required for Anima to run)

### Architecture (ARCH)

- [x] **ARCH-01**: AnimaRuntimeManager manages all Anima instances
- [x] **ARCH-02**: AnimaContext identifies current Anima for scoped services
- [x] **ARCH-03**: Each Anima has isolated EventBus instance
- [x] **ARCH-04**: Each Anima has isolated WiringEngine instance
- [x] **ARCH-05**: Configuration files stored per Anima in separate directories
- [x] **ARCH-06**: Service disposal prevents memory leaks (IAsyncDisposable)

## v2 Requirements

### Future Enhancements

- **ANIMA-11**: Anima can run in background while viewing different Anima
- **ANIMA-12**: Anima execution statistics (uptime, module execution count)
- **MODMGMT-07**: Module dependency resolution and auto-install
- **MODMGMT-08**: Module marketplace integration
- **BUILTIN-11**: Loop control module for iterative execution
- **BUILTIN-12**: Variable storage module for state persistence
- **I18N-05**: Additional language support (Japanese, Korean, etc.)

## Out of Scope

| Feature | Reason |
|---------|--------|
| Nested Anima instances | Unclear value proposition, high complexity |
| Cross-Anima communication | Violates isolation principle, wait for user demand |
| Auto-update modules | Breaking changes risk, user loses control |
| Module marketplace backend | Infrastructure burden, validate local-first approach first |
| Real-time collaboration | Multi-user complexity, single-user focus for v1.5 |
| Cloud sync | Privacy concerns, local-first principle |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| ANIMA-01 | Phase 23 | Complete |
| ANIMA-02 | Phase 23 | Complete |
| ANIMA-03 | Phase 23 | Complete |
| ANIMA-04 | Phase 23 | Complete |
| ANIMA-05 | Phase 23 | Complete |
| ANIMA-06 | Phase 23 | Complete |
| ANIMA-10 | Phase 23 | Complete |
| ARCH-01 | Phase 23 | Complete |
| ARCH-02 | Phase 23 | Complete |
| ARCH-05 | Phase 23 | Complete |
| ARCH-06 | Phase 23 | Complete |
| ANIMA-07 | Phase 24 | Complete |
| ANIMA-08 | Phase 24 | Pending |
| I18N-01 | Phase 24 | Complete |
| I18N-02 | Phase 24 | Complete |
| I18N-03 | Phase 24 | Complete |
| I18N-04 | Phase 24 | Complete |
| ARCH-03 | Phase 24 | Complete |
| ARCH-04 | Phase 24 | Complete |
| MODMGMT-01 | Phase 25 | Pending |
| MODMGMT-02 | Phase 25 | Pending |
| MODMGMT-03 | Phase 25 | Pending |
| MODMGMT-04 | Phase 25 | Pending |
| MODMGMT-05 | Phase 25 | Pending |
| MODMGMT-06 | Phase 25 | Pending |
| MODCFG-01 | Phase 26 | Pending |
| MODCFG-02 | Phase 26 | Pending |
| MODCFG-03 | Phase 26 | Pending |
| MODCFG-04 | Phase 26 | Pending |
| MODCFG-05 | Phase 26 | Pending |
| ANIMA-09 | Phase 26 | Pending |
| BUILTIN-01 | Phase 27 | Pending |
| BUILTIN-02 | Phase 27 | Pending |
| BUILTIN-03 | Phase 27 | Pending |
| BUILTIN-04 | Phase 27 | Pending |
| BUILTIN-05 | Phase 27 | Pending |
| BUILTIN-06 | Phase 27 | Pending |
| BUILTIN-07 | Phase 27 | Pending |
| BUILTIN-08 | Phase 27 | Pending |
| BUILTIN-09 | Phase 27 | Pending |
| BUILTIN-10 | Phase 27 | Pending |

**Coverage:**
- v1.5 requirements: 47 total
- Mapped to phases: 47
- Unmapped: 0

---
*Requirements defined: 2026-02-28*
*Last updated: 2026-02-28 after roadmap creation*
