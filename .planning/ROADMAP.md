# Roadmap: OpenAnima v1.5

**Milestone:** v1.5 Multi-Anima Architecture
**Created:** 2026-02-28
**Depth:** standard
**Coverage:** 47/47 requirements mapped

## Phases

- [ ] **Phase 23: Multi-Anima Foundation** - Core architecture for independent Anima instances
- [ ] **Phase 24: Service Migration & i18n** - Refactor singleton services and add internationalization
- [ ] **Phase 25: Module Management** - Install/uninstall/enable/disable module capabilities
- [ ] **Phase 26: Module Configuration UI** - Detail panel for per-module configuration
- [ ] **Phase 27: Built-in Modules** - Rich module ecosystem with text processing and flow control

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 23. Multi-Anima Foundation | 0/2 | Planned | - |
| 24. Service Migration & i18n | 0/? | Not started | - |
| 25. Module Management | 0/? | Not started | - |
| 26. Module Configuration UI | 0/? | Not started | - |
| 27. Built-in Modules | 0/? | Not started | - |

## Phase Details

### Phase 23: Multi-Anima Foundation
**Goal**: Users can create, list, switch, and delete independent Anima instances with isolated runtime state
**Depends on**: Nothing (first phase of v1.5)
**Requirements**: ANIMA-01, ANIMA-02, ANIMA-03, ANIMA-04, ANIMA-05, ANIMA-06, ANIMA-10, ARCH-01, ARCH-02, ARCH-05, ARCH-06
**Success Criteria** (what must be TRUE):
  1. User can create new Anima with custom name from UI
  2. User can view list of all Animas in global sidebar
  3. User can switch between Animas and see different runtime state
  4. User can delete Anima and its configuration is removed
  5. User can rename or clone existing Anima
  6. Anima configuration persists across application restarts
**Plans**: 2 plans
- [ ] 23-01-PLAN.md — Core Anima services (AnimaRuntimeManager, AnimaContext, DI) + TDD tests
- [ ] 23-02-PLAN.md — Sidebar UI (AnimaListPanel, create/rename/clone/delete) + startup initialization

### Phase 24: Service Migration & i18n
**Goal**: Each Anima has isolated EventBus/HeartbeatLoop/WiringEngine, and users can switch UI language
**Depends on**: Phase 23 (requires AnimaRuntimeManager and AnimaContext)
**Requirements**: ANIMA-07, ANIMA-08, I18N-01, I18N-02, I18N-03, I18N-04, ARCH-03, ARCH-04
**Success Criteria** (what must be TRUE):
  1. Each Anima runs independent heartbeat loop without interference
  2. Each Anima has isolated module instances with separate state
  3. User can switch UI language between Chinese and English
  4. Language preference persists across sessions
  5. Missing translations fall back to English gracefully
**Plans**: TBD

### Phase 25: Module Management
**Goal**: Users can install, uninstall, enable, and disable modules with metadata display
**Depends on**: Phase 24 (requires per-Anima module instances)
**Requirements**: MODMGMT-01, MODMGMT-02, MODMGMT-03, MODMGMT-04, MODMGMT-05, MODMGMT-06
**Success Criteria** (what must be TRUE):
  1. User can view list of all installed modules with status
  2. User can install module from .oamod package
  3. User can uninstall module and it's removed from disk
  4. User can enable/disable module per Anima independently
  5. User can view module metadata (name, version, author, description)
  6. User can search and filter modules by name
**Plans**: TBD

### Phase 26: Module Configuration UI
**Goal**: Users can configure module-specific settings through detail panel in editor
**Depends on**: Phase 25 (requires module selection mechanism)
**Requirements**: MODCFG-01, MODCFG-02, MODCFG-03, MODCFG-04, MODCFG-05, ANIMA-09
**Success Criteria** (what must be TRUE):
  1. User can click module in editor to show detail panel on right
  2. User can edit module-specific configuration in detail panel
  3. Module configuration persists per Anima across sessions
  4. Configuration changes validate before saving with clear error messages
  5. Detail panel shows module status and metadata
  6. Each Anima has independent chat interface with isolated conversation
**Plans**: TBD

### Phase 27: Built-in Modules
**Goal**: Rich module ecosystem with text processing, flow control, and configurable LLM
**Depends on**: Phase 26 (requires configuration UI)
**Requirements**: BUILTIN-01, BUILTIN-02, BUILTIN-03, BUILTIN-04, BUILTIN-05, BUILTIN-06, BUILTIN-07, BUILTIN-08, BUILTIN-09, BUILTIN-10
**Success Criteria** (what must be TRUE):
  1. Fixed text module outputs configurable text content editable in detail panel
  2. Text concat module concatenates two text inputs into one output
  3. Text split module splits text by delimiter into multiple outputs
  4. Text merge module merges multiple inputs into one output
  5. Conditional branch module routes to different outputs based on condition expression
  6. LLM module allows configuration of API URL, API key, and model name in detail panel
  7. Heartbeat module is optional and can be added/removed per Anima
**Plans**: TBD

---
*Last updated: 2026-02-28*
