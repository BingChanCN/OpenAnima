# Roadmap: OpenAnima

## Milestones

- ✅ **v1.0 Core Platform** — Phases 1-2 (shipped 2026-02-21)
- ✅ **v1.1 WebUI Dashboard** — Phases 3-7 (shipped 2026-02-23)
- ✅ **v1.2 LLM Integration** — Phases 8-10 (shipped 2026-02-25)
- ✅ **v1.3 Visual Wiring** — Phases 11-19 + 12.5 (shipped 2026-02-28)
- ✅ **v1.4 Module SDK** — Phases 20-22 (shipped 2026-02-28)
- ✅ **v1.5 Multi-Anima Architecture** — Phases 23-27 (shipped 2026-03-09)
- ✅ **v1.6 Cross-Anima Routing** — Phases 28-31 (shipped 2026-03-14)
- ✅ **v1.7 Runtime Foundation** — Phases 32-37 (shipped 2026-03-16)
- ◆ **v1.8 SDK Runtime Parity** — Phases 38-41

## Phases

<details>
<summary>✅ v1.0 Core Platform (Phases 1-2) — SHIPPED 2026-02-21</summary>

- [x] Phase 1: Core Runtime (2/2 plans)
- [x] Phase 2: Module System (3/3 plans)

</details>

<details>
<summary>✅ v1.1 WebUI Dashboard (Phases 3-7) — SHIPPED 2026-02-23</summary>

- [x] Phase 3: Blazor Server Host (2/2 plans)
- [x] Phase 4: Dashboard UI (2/2 plans)
- [x] Phase 5: SignalR Real-time (2/2 plans)
- [x] Phase 6: Module Control (2/2 plans)
- [x] Phase 7: UX Polish (2/2 plans)

</details>

<details>
<summary>✅ v1.2 LLM Integration (Phases 8-10) — SHIPPED 2026-02-25</summary>

- [x] Phase 8: LLM API Client (2/2 plans)
- [x] Phase 9: Chat UI (2/2 plans)
- [x] Phase 10: Context Management (2/2 plans)

</details>

<details>
<summary>✅ v1.3 Visual Wiring (Phases 11-19 + 12.5) — SHIPPED 2026-02-28</summary>

- [x] Phase 11: Port Type System (3/3 plans)
- [x] Phase 12: Wiring Engine (3/3 plans)
- [x] Phase 12.5: Runtime DI Fix (3/3 plans)
- [x] Phase 13: Visual Editor (3/3 plans)
- [x] Phase 14: Module Refactoring (3/3 plans)
- [x] Phase 15: Config Key Fix (1/1 plan)
- [x] Phase 16: Module Init (1/1 plan)
- [x] Phase 17: E2E Integration (2/2 plans)
- [x] Phase 18: Verification Backfill (1/1 plan)
- [x] Phase 19: Metadata Cleanup (1/1 plan)

</details>

<details>
<summary>✅ v1.4 Module SDK (Phases 20-22) — SHIPPED 2026-02-28</summary>

- [x] Phase 20: CLI Tool (3/3 plans)
- [x] Phase 21: Pack & Validate (3/3 plans)
- [x] Phase 22: Documentation (2/2 plans)

</details>

<details>
<summary>✅ v1.5 Multi-Anima Architecture (Phases 23-27) — SHIPPED 2026-03-09</summary>

- [x] Phase 23: Multi-Anima Foundation (2/2 plans) — completed 2026-02-28
- [x] Phase 24: Service Migration & i18n (3/3 plans) — completed 2026-02-28
- [x] Phase 25: Module Management (3/3 plans) — completed 2026-02-28
- [x] Phase 26: Module Configuration UI (3/3 plans) — completed 2026-03-01
- [x] Phase 27: Built-in Modules (2/2 plans) — completed 2026-03-02

</details>

<details>
<summary>✅ v1.6 Cross-Anima Routing (Phases 28-31) — SHIPPED 2026-03-14</summary>

- [x] Phase 28: Routing Infrastructure (2/2 plans) — completed 2026-03-11
- [x] Phase 29: Routing Modules (2/2 plans) — completed 2026-03-13
- [x] Phase 30: Prompt Injection and Format Detection (2/2 plans) — completed 2026-03-13
- [x] Phase 31: HTTP Request Module (2/2 plans) — completed 2026-03-14

</details>

<details>
<summary>✅ v1.7 Runtime Foundation (Phases 32-37) — SHIPPED 2026-03-16</summary>

- [x] Phase 32: Test Baseline (1/1 plans) — completed 2026-03-14
- [x] Phase 33: Concurrency Fixes (1/1 plans) — completed 2026-03-14
- [x] Phase 34: Activity Channel Model (2/2 plans) — completed 2026-03-15
- [x] Phase 35: Contracts API Expansion (3/3 plans) — completed 2026-03-15
- [x] Phase 36: Built-in Module Decoupling (5/5 plans) — completed 2026-03-16
- [x] Phase 37: Wire Chat Channel (1/1 plans) — completed 2026-03-16

</details>

### v1.8 SDK Runtime Parity (Phases 38-41)

#### Phase 38: PluginLoader DI Injection
**Goal:** External modules receive Contracts services via constructor injection
**Requirements:** PLUG-01, PLUG-02, PLUG-03
**Plans:** 3/3 plans complete

Plans:
- [ ] 38-01-PLAN.md — PluginLoader DI-aware constructor resolution + test harness extension
- [ ] 38-02-PLAN.md — ModuleService wiring + integration tests
- [ ] 38-03-PLAN.md — Gap closure: fix test build errors + harness property name alignment

**Success Criteria:**
1. External .oamod module with constructor accepting IModuleConfig + IModuleContext + IEventBus loads successfully
2. External module receives typed ILogger instance via ILoggerFactory
3. Module with unresolvable optional parameter loads with null and warning log
4. Module with unresolvable required parameter fails with descriptive LoadResult error
5. Existing 12 built-in modules continue to load without regression

#### Phase 39: Contracts Type Migration & Structured Messages
**Goal:** ChatMessageInput in Contracts; LLMModule accepts structured message list
**Requirements:** MSG-01, MSG-02, MSG-03
**Plans:** 2/2 plans complete

Plans:
- [ ] 39-01-PLAN.md — ChatMessageInput migration to Contracts + SerializeList/DeserializeList helpers
- [ ] 39-02-PLAN.md — LLMModule messages input port with priority rule

**Success Criteria:**
1. External module can reference ChatMessageInput from OpenAnima.Contracts without Core dependency
2. LLMModule messages port receives JSON-serialized List<ChatMessageInput> and sends multi-turn conversation to LLM API
3. LLMModule prompt port continues to work as single-turn (backward compatibility)
4. ChatMessageInput.SerializeList/DeserializeList round-trips correctly
5. Existing wiring configurations load without modification

#### Phase 40: Module Storage Path
**Goal:** Modules can persist data to a stable per-Anima per-Module directory
**Requirements:** STOR-01

**Success Criteria:**
1. IModuleContext.GetDataDirectory("MyModule") returns path under data/animas/{animaId}/module-data/MyModule/
2. Directory is auto-created on first call
3. Path changes when ActiveAnimaId changes (switching Animas)
4. Deleting an Anima removes its module-data directory

#### Phase 41: External ContextModule (SDK Validation)
**Goal:** End-to-end validation of SDK surface via a real external module
**Requirements:** ECTX-01, ECTX-02

**Success Criteria:**
1. ContextModule loads from .oamod package via PluginLoader with DI injection
2. User can have multi-turn conversation — LLM receives full history and responds in context
3. Conversation history persists to DataDirectory/history.json
4. After application restart, previous conversation history is restored
5. History is isolated per Anima — switching Animas shows different history

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Core Runtime | v1.0 | 2/2 | Complete | 2026-02-21 |
| 2. Module System | v1.0 | 3/3 | Complete | 2026-02-21 |
| 3. Blazor Server Host | v1.1 | 2/2 | Complete | 2026-02-23 |
| 4. Dashboard UI | v1.1 | 2/2 | Complete | 2026-02-23 |
| 5. SignalR Real-time | v1.1 | 2/2 | Complete | 2026-02-23 |
| 6. Module Control | v1.1 | 2/2 | Complete | 2026-02-23 |
| 7. UX Polish | v1.1 | 2/2 | Complete | 2026-02-23 |
| 8. LLM API Client | v1.2 | 2/2 | Complete | 2026-02-25 |
| 9. Chat UI | v1.2 | 2/2 | Complete | 2026-02-25 |
| 10. Context Management | v1.2 | 2/2 | Complete | 2026-02-25 |
| 11. Port Type System | v1.3 | 3/3 | Complete | 2026-02-28 |
| 12. Wiring Engine | v1.3 | 3/3 | Complete | 2026-02-28 |
| 12.5. Runtime DI Fix | v1.3 | 3/3 | Complete | 2026-02-28 |
| 13. Visual Editor | v1.3 | 3/3 | Complete | 2026-02-28 |
| 14. Module Refactoring | v1.3 | 3/3 | Complete | 2026-02-28 |
| 15. Config Key Fix | v1.3 | 1/1 | Complete | 2026-02-28 |
| 16. Module Init | v1.3 | 1/1 | Complete | 2026-02-28 |
| 17. E2E Integration | v1.3 | 2/2 | Complete | 2026-02-28 |
| 18. Verification Backfill | v1.3 | 1/1 | Complete | 2026-02-28 |
| 19. Metadata Cleanup | v1.3 | 1/1 | Complete | 2026-02-28 |
| 20. CLI Tool | v1.4 | 3/3 | Complete | 2026-02-28 |
| 21. Pack & Validate | v1.4 | 3/3 | Complete | 2026-02-28 |
| 22. Documentation | v1.4 | 2/2 | Complete | 2026-02-28 |
| 23. Multi-Anima Foundation | v1.5 | 2/2 | Complete | 2026-02-28 |
| 24. Service Migration & i18n | v1.5 | 3/3 | Complete | 2026-02-28 |
| 25. Module Management | v1.5 | 3/3 | Complete | 2026-02-28 |
| 26. Module Configuration UI | v1.5 | 3/3 | Complete | 2026-03-01 |
| 27. Built-in Modules | v1.5 | 2/2 | Complete | 2026-03-02 |
| 28. Routing Infrastructure | v1.6 | 2/2 | Complete | 2026-03-11 |
| 29. Routing Modules | v1.6 | 2/2 | Complete | 2026-03-13 |
| 30. Prompt Injection & Format Detection | v1.6 | 2/2 | Complete | 2026-03-13 |
| 31. HTTP Request Module | v1.6 | 2/2 | Complete | 2026-03-14 |
| 32. Test Baseline | v1.7 | 1/1 | Complete | 2026-03-14 |
| 33. Concurrency Fixes | v1.7 | 1/1 | Complete | 2026-03-14 |
| 34. Activity Channel Model | v1.7 | 2/2 | Complete | 2026-03-15 |
| 35. Contracts API Expansion | v1.7 | 3/3 | Complete | 2026-03-15 |
| 36. Built-in Module Decoupling | v1.7 | 5/5 | Complete | 2026-03-16 |
| 37. Wire Chat Channel | v1.7 | 1/1 | Complete | 2026-03-16 |
| 38. PluginLoader DI Injection | 3/3 | Complete    | 2026-03-17 | — |
| 39. Contracts Type Migration & Structured Messages | 2/2 | Complete    | 2026-03-18 | — |
| 40. Module Storage Path | v1.8 | 0/0 | Pending | — |
| 41. External ContextModule | v1.8 | 0/0 | Pending | — |

**Total shipped: 37 phases, 85 plans across 8 milestones**
**v1.8 in progress: 4 phases, 9 requirements**

---
*Last updated: 2026-03-17 after Phase 39 planning*
