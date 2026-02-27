# Roadmap: OpenAnima

## Milestones

- ✅ **v1.0 Core Platform Foundation** — Phases 1-2 (shipped 2026-02-21)
- ✅ **v1.1 WebUI Runtime Dashboard** — Phases 3-7 (shipped 2026-02-23)
- ✅ **v1.2 LLM Integration** — Phases 8-10 (shipped 2026-02-25)
- ✅ **v1.3 True Modularization & Visual Wiring** — Phases 11-19 + 12.5 (shipped 2026-02-28)
- 🚧 **v1.4 Module SDK & DevEx** — Phases 20-22 (in progress)

## Phases

<details>
<summary>✅ v1.0 Core Platform Foundation (Phases 1-2) — SHIPPED 2026-02-21</summary>

- [x] Phase 1: Core Plugin System (3/3 plans) — completed 2026-02-21
- [x] Phase 2: Event Bus & Heartbeat Loop (2/2 plans) — completed 2026-02-21

See: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md) for full details.

</details>

<details>
<summary>✅ v1.1 WebUI Runtime Dashboard (Phases 3-7) — SHIPPED 2026-02-23</summary>

- [x] Phase 3: Service Abstraction & Hosting (2/2 plans) — completed 2026-02-22
- [x] Phase 4: Blazor UI with Static Display (2/2 plans) — completed 2026-02-22
- [x] Phase 5: SignalR Real-Time Updates (2/2 plans) — completed 2026-02-22
- [x] Phase 6: Control Operations (2/2 plans) — completed 2026-02-22
- [x] Phase 7: Polish & Validation (2/2 plans) — completed 2026-02-23

See: [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md) for full details.

</details>

<details>
<summary>✅ v1.2 LLM Integration (Phases 8-10) — SHIPPED 2026-02-25</summary>

- [x] Phase 8: API Client Setup & Configuration (2/2 plans) — completed 2026-02-24
- [x] Phase 9: Chat UI with Streaming (2/2 plans) — completed 2026-02-25
- [x] Phase 10: Context Management & Token Counting (2/2 plans) — completed 2026-02-25

See: [milestones/v1.2-ROADMAP.md](milestones/v1.2-ROADMAP.md) for full details.

</details>

<details>
<summary>✅ v1.3 True Modularization & Visual Wiring (Phases 11-19) — SHIPPED 2026-02-28</summary>

- [x] Phase 11: Port Type System & Testing Foundation (3/3 plans) — completed 2026-02-25
- [x] Phase 12: Wiring Engine & Execution Orchestration (3/3 plans) — completed 2026-02-25
- [x] Phase 12.5: Runtime DI Integration & Tech Debt Fix (3/3 plans) — completed 2026-02-25
- [x] Phase 13: Visual Drag-and-Drop Editor (3/3 plans) — completed 2026-02-26
- [x] Phase 14: Module Refactoring & Runtime Integration (3/3 plans) — completed 2026-02-26
- [x] Phase 15: Fix ConfigurationLoader Key Mismatch (1/1 plan) — completed 2026-02-26
- [x] Phase 16: Module Runtime Initialization & Port Registration (1/1 plan) — completed 2026-02-27
- [x] Phase 17: E2E Module Pipeline Integration & Editor Polish (2/2 plans) — completed 2026-02-27
- [x] Phase 18: RMOD Verification Evidence Backfill (1/1 plan) — completed 2026-02-27
- [x] Phase 19: Requirement Metadata Drift Cleanup (1/1 plan) — completed 2026-02-28

See: [milestones/v1.3-ROADMAP.md](milestones/v1.3-ROADMAP.md) for full details.

</details>

---

### 🚧 v1.4 Module SDK & DevEx (In Progress)

**Milestone Goal:** Developers can create, validate, and pack custom modules with a simple CLI tool.

#### Phase 20: CLI Foundation & Templates
**Goal**: Developer can create new module projects using a CLI tool
**Depends on**: Phase 19 (v1.3 complete)
**Requirements**: SDK-01, SDK-02, SDK-03, SDK-04, SDK-05, CLI-01, CLI-02, CLI-03, CLI-04, CLI-05, MAN-01, MAN-02, MAN-03, MAN-04, MAN-05, TEMP-01, TEMP-02, TEMP-03, TEMP-04, TEMP-05
**Success Criteria** (what must be TRUE):
  1. Developer can install oani CLI as .NET global tool
  2. Developer can run `oani new MyModule` and get a compilable module project
  3. Developer can customize module template with ports and type options
  4. CLI follows standard conventions (help, exit codes, verbosity, stdout/stderr discipline)
  5. Generated module implements IModule and IModuleMetadata interfaces correctly
**Plans**: TBD

Plans:
- [ ] 20-01: CLI project setup and installable .NET tool
- [ ] 20-02: Module templates with manifest schema
- [ ] 20-03: Template customization options

#### Phase 21: Pack, Validate & Runtime Integration
**Goal**: Developer can validate and pack modules into distributable .oamod files
**Depends on**: Phase 20
**Requirements**: PACK-01, PACK-02, PACK-03, PACK-04, PACK-05, PACK-06, VAL-01, VAL-02, VAL-03, VAL-04, VAL-05
**Success Criteria** (what must be TRUE):
  1. Developer can validate module with `oani validate` and see all errors (not just first)
  2. Developer can pack module into .oamod file containing DLL, manifest, and checksum
  3. Packed module loads in OpenAnima runtime without modification
  4. Pack command builds project before packing (unless --no-build specified)
  5. Validate command checks manifest schema, required fields, and IModule implementation
**Plans**: TBD

Plans:
- [ ] 21-01: Module validation command
- [ ] 21-02: Pack command and .oamod format
- [ ] 21-03: Runtime integration for .oamod loading

#### Phase 22: Documentation
**Goal**: Developer can learn module development in under 5 minutes
**Depends on**: Phase 21
**Requirements**: DOC-01, DOC-02, DOC-03, DOC-04, DOC-05
**Success Criteria** (what must be TRUE):
  1. Quick-start guide produces working module in under 5 minutes
  2. API reference documents all public interfaces (IModule, IModuleExecutor, ITickable, IEventBus)
  3. API reference documents port system (PortType, PortMetadata, InputPortAttribute, OutputPortAttribute)
  4. Documentation includes code examples for common patterns
  5. Create-build-pack workflow documented end-to-end
**Plans**: TBD

Plans:
- [ ] 22-01: Quick-start guide
- [ ] 22-02: API reference documentation

---

## Progress

**Execution Order:**
Phases execute in numeric order: 20 → 21 → 22

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Core Plugin System | v1.0 | 3/3 | Complete | 2026-02-21 |
| 2. Event Bus & Heartbeat | v1.0 | 2/2 | Complete | 2026-02-21 |
| 3. Service Abstraction & Hosting | v1.1 | 2/2 | Complete | 2026-02-22 |
| 4. Blazor UI with Static Display | v1.1 | 2/2 | Complete | 2026-02-22 |
| 5. SignalR Real-Time Updates | v1.1 | 2/2 | Complete | 2026-02-22 |
| 6. Control Operations | v1.1 | 2/2 | Complete | 2026-02-22 |
| 7. Polish & Validation | v1.1 | 2/2 | Complete | 2026-02-23 |
| 8. API Client Setup & Configuration | v1.2 | 2/2 | Complete | 2026-02-24 |
| 9. Chat UI with Streaming | v1.2 | 2/2 | Complete | 2026-02-25 |
| 10. Context Management & Token Counting | v1.2 | 2/2 | Complete | 2026-02-25 |
| 11. Port Type System & Testing | v1.3 | 3/3 | Complete | 2026-02-25 |
| 12. Wiring Engine & Execution | v1.3 | 3/3 | Complete | 2026-02-25 |
| 12.5. Runtime DI Integration & Tech Debt | v1.3 | 3/3 | Complete | 2026-02-25 |
| 13. Visual Drag-and-Drop Editor | v1.3 | 3/3 | Complete | 2026-02-26 |
| 14. Module Refactoring & Runtime Integration | v1.3 | 3/3 | Complete | 2026-02-26 |
| 15. Fix ConfigurationLoader Key Mismatch | v1.3 | 1/1 | Complete | 2026-02-26 |
| 16. Module Runtime Initialization | v1.3 | 1/1 | Complete | 2026-02-27 |
| 17. E2E Module Pipeline Integration | v1.3 | 2/2 | Complete | 2026-02-27 |
| 18. RMOD Verification Evidence Backfill | v1.3 | 1/1 | Complete | 2026-02-27 |
| 19. Requirement Metadata Drift Cleanup | v1.3 | 1/1 | Complete | 2026-02-28 |
| 20. CLI Foundation & Templates | v1.4 | 0/3 | Not started | - |
| 21. Pack, Validate & Integration | v1.4 | 0/3 | Not started | - |
| 22. Documentation | v1.4 | 0/2 | Not started | - |

---
*Roadmap created: 2026-02-28*
*Last updated: 2026-02-28*