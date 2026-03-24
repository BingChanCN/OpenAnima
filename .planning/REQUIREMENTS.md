# Requirements: OpenAnima

**Defined:** 2026-03-24
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

## v2.0.3 Requirements

Requirements for Editor Experience milestone. Each maps to roadmap phases.

### i18n Localization

- [ ] **EDUX-01**: Module names display in Chinese in palette, node card title, and config sidebar when language is zh-CN

### Editor Interaction

- [ ] **EDUX-03**: User can delete connections via click-select + Delete key and right-click context menu

### Module Information

- [ ] **EDUX-02**: Editor config sidebar shows module description below module header
- [ ] **EDUX-04**: Port circles show Chinese tooltip on hover explaining their purpose
- [ ] **EDUX-05**: Module palette items show description tooltip on hover

## Future Requirements

Deferred to future release. Tracked but not in current roadmap.

### Editor Enhancement

- **EDPLT-01**: Module palette search matches localized display names
- **EDPLT-02**: Undo/redo for connection deletion

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Rich styled port tooltips with type badge and color | Complexity — SVG `<title>` is sufficient for v2.0.3 |
| Port description editing by end users | End users are not module authors |
| IModuleMetadata contract changes for i18n | External SDK contract stability — use .resx instead |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| EDUX-01 | — | Pending |
| EDUX-02 | — | Pending |
| EDUX-03 | — | Pending |
| EDUX-04 | — | Pending |
| EDUX-05 | — | Pending |

**Coverage:**
- v2.0.3 requirements: 5 total
- Mapped to phases: 0
- Unmapped: 5

---
*Requirements defined: 2026-03-24*
*Last updated: 2026-03-24 after initial definition*
