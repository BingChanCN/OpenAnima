# Requirements: OpenAnima v1.5

**Defined:** 2026-02-28
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

## v1.5 Requirements

### Anima Management (ANIMA)

- [ ] **ANIMA-01**: User can create new Anima with custom name
- [ ] **ANIMA-02**: User can view list of all Animas in global sidebar
- [ ] **ANIMA-03**: User can switch between different Animas
- [ ] **ANIMA-04**: User can delete Anima
- [ ] **ANIMA-05**: User can rename Anima
- [ ] **ANIMA-06**: User can clone existing Anima (duplicate configuration)
- [ ] **ANIMA-07**: Each Anima has independent heartbeat loop
- [ ] **ANIMA-08**: Each Anima has independent module instances
- [ ] **ANIMA-09**: Each Anima has independent chat interface
- [ ] **ANIMA-10**: Anima configuration persists across sessions

### Internationalization (I18N)

- [ ] **I18N-01**: User can switch UI language between Chinese and English
- [ ] **I18N-02**: All UI text displays in selected language
- [ ] **I18N-03**: Language preference persists across sessions
- [ ] **I18N-04**: Missing translations fall back to English

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

- [ ] **ARCH-01**: AnimaRuntimeManager manages all Anima instances
- [ ] **ARCH-02**: AnimaContext identifies current Anima for scoped services
- [ ] **ARCH-03**: Each Anima has isolated EventBus instance
- [ ] **ARCH-04**: Each Anima has isolated WiringEngine instance
- [ ] **ARCH-05**: Configuration files stored per Anima in separate directories
- [ ] **ARCH-06**: Service disposal prevents memory leaks (IAsyncDisposable)

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
| (To be filled by roadmapper) | | |

**Coverage:**
- v1.5 requirements: TBD total
- Mapped to phases: TBD
- Unmapped: TBD

---
*Requirements defined: 2026-02-28*
*Last updated: 2026-02-28 after initial definition*
