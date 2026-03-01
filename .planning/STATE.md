---
gsd_state_version: 1.0
milestone: v1.5
milestone_name: milestone
status: in_progress
last_updated: "2026-03-01T00:00:00.000Z"
last_activity: "2026-03-01 — Completed Phase 26: Module Configuration UI (all 3 plans)"
progress:
  total_phases: 13
  completed_phases: 13
  total_plans: 29
  completed_plans: 29
  percent: 100
---

# Project State: OpenAnima v1.5 Multi-Anima Architecture

**Last updated:** 2026-03-01
**Current milestone:** v1.5 Multi-Anima Architecture

## Project Reference

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

**Current focus:** Transform from single-runtime dashboard to multi-instance Anima architecture with i18n support and rich module ecosystem.

See: `.planning/PROJECT.md` (updated 2026-02-28)

## Current Position

**Phase:** 26 - Module Configuration UI
**Plan:** All 3 plans complete (phase complete)
**Status:** Phase complete, pending verification
**Last activity:** 2026-03-01 — Completed Phase 26: Module Configuration UI (all 3 plans)

**Progress:** [██████████] 100%

## Performance Metrics

**Velocity:**
- Total plans completed: 39 (across v1.0-v1.4)
- Average duration: ~30 min
- Total execution time: ~19.5 hours

**By Milestone:**

| Milestone | Phases | Plans | Total Time |
|-----------|--------|-------|--------------|
| v1.0 Core Platform | 2 | 5 | ~2.5 hrs |
| v1.1 WebUI Dashboard | 5 | 10 | ~5 hrs |
| v1.2 LLM Integration | 3 | 6 | ~3 hrs |
| v1.3 Visual Wiring | 9 | 18 | ~9 hrs |
| v1.4 Module SDK | 3 | 8 | TBD |
| v1.5 Multi-Anima | 5 | 0 | 0 hrs |

**Recent Trend:**
- Last 5 plans: Consistent ~5 min execution
- Trend: Stable
| Phase 23-multi-anima-foundation P01 | 3 | 2 tasks | 8 files |
| Phase 23-multi-anima-foundation P02 | 3 | 2 tasks | 10 files |
| Phase 24-service-migration-i18n P01 | 90 | 6 tasks | 23 files |
| Phase 24-service-migration-i18n P02 | 3 | 1 tasks | 10 files |
| Phase 24-service-migration-i18n P03 | 12 | 1 tasks | 14 files |
| Phase 25 P03 | 5 | 4 tasks | 7 files |

## Accumulated Context

### v1.5 Scope Decisions

- **Architecture shift:** From single runtime to multi-Anima instances (each with independent heartbeat, modules, chat)
- **UI layout:** Three-column layout (global sidebar + Anima list/Editor + detail panel)
- **i18n scope:** UI text only (Chinese/English), not module content or conversations
- **Module detail panel:** Right-side panel in editor for configuration
- **Data persistence:** Anima configs, module configs, language preference (not conversation history)
- **Built-in modules:** Fixed text, text concat/split/merge, conditional branch, configurable LLM, optional heartbeat
- **Module page:** Real implementation replacing placeholder (list, install/uninstall, enable/disable, info display)
- **Implementation order:** Architecture → Modules → i18n

### Key Decisions (v1.5)

- **Phase 23-01:** Short 8-char hex ID from Guid.NewGuid().ToString("N")[..8] for Anima directory names — readable, effectively collision-free for single-user app
- **Phase 23-01:** AnimaContext is a plain singleton with event (not CascadingValue) to avoid full layout re-render on every active-Anima change
- **Phase 23-01:** Clone copies only anima.json (not runtime state files) to prevent inheriting runtime state in cloned Anima
- **Phase 24-01:** Global IEventBus singleton kept for module constructors (ANIMA-08 partial) — full module instance isolation deferred to next phase
- **Phase 24-01:** IAnimaRuntimeManager implements both IAsyncDisposable and IDisposable to satisfy .NET DI disposal requirements
- **Phase 24-01:** IRuntimeClient methods all include animaId as first parameter so UI can filter SignalR push events by active Anima
- **Phase 24-01:** DeleteAsync auto-switches active Anima via AnimaContext.SetActive() when deleted Anima was active
- **Phase 24-02:** SDK auto-includes .resx as EmbeddedResource — explicit ItemGroup not needed (causes NETSDK1022 duplicate error)
- **Phase 24-02:** LanguageService is a plain singleton with Action event (not CascadingValue) to avoid full layout re-render on every culture change
- **Phase 24-02:** MainLayout reads localStorage on first render to restore language preference on app load
- **Phase 24-02:** Chinese (zh-CN) is default and fallback language per CONTEXT.md locked decision
- **Phase 24-03:** Monitor partial class pattern — IStringLocalizer injected in Monitor.razor.cs to avoid CS0102 duplicate definition with @inject directive
- **Phase 25-03:** ModuleContextMenu follows AnimaContextMenu pattern with backdrop and button-based menu items
- **Phase 25-03:** ModuleDetailSidebar displays 'Unknown' for author since PluginManifest lacks Author property
- **Phase 25-03:** Status badges update automatically on ActiveAnimaChanged event to reflect per-Anima state

### Key Decisions (v1.4)

- **Phase 20:** Silent-first output: Default verbosity is "quiet" with no output unless errors occur or --verbosity is set
- **Phase 20:** Exit code discipline: 0=success, 1=general error, 2=validation error for consistent CLI error reporting
- **Phase 20:** Stream separation: stderr for errors, stdout for normal output
- **Phase 20:** Use System.Text.Json for manifest serialization
- **Phase 20:** Aggregate all validation errors before reporting
- **Phase 20:** Use embedded resources for templates (not file paths)
- **Phase 20:** Simple string.Replace() for template substitution
- **Phase 20:** Rename template files to .tmpl extension for clarity
- **Phase 20:** Module name validation with friendly suggestions (e.g., "Did you mean 'Module123Invalid'?")
- **Phase 20:** Port specification format: "Name" or "Name:Type" with default type Text
- **Phase 21:** Use name-based type comparison for IModule detection to avoid type identity issues across AssemblyLoadContext boundaries
- **Phase 21:** Accumulate all validation errors before reporting for better developer experience
- **Phase 21:** Make assembly validation optional (warning only) if module not built yet
- **Phase 21:** MD5 for checksum algorithm (sufficient for integrity verification, not cryptographic security)
- **Phase 21:** In-memory manifest enrichment (source module.json unchanged, only packed version has checksum/targetFramework)
- **Phase 21:** Search Release then Debug for DLL (supports both --no-build and post-build scenarios)
- **Phase 21:** Extract to .extracted/ subdirectory to avoid conflicts with regular modules
- **Phase 21:** Skip .extracted/ directory during PluginLoader.ScanDirectory to prevent double-loading

### Key Decisions (v1.3)

- Zero new dependencies: Use .NET 8.0 built-ins for port system
- Custom topological sort: ~100 LOC implementation avoids 500KB+ QuikGraph dependency
- HTML5 + SVG editor: Native browser APIs with Blazor, no JavaScript framework
- Two-phase initialization: Load modules first, then wire connections
- Scoped EditorStateService: Per-circuit isolation in Blazor Server
- Port types fixed to Text and Trigger (not extensible by design)
- Level-parallel execution: Task.WhenAll within level, sequential between levels

### Active TODOs

- [x] Begin Phase 23 execution with plan 23-01
- [x] Plan 23-02: AnimaListPanel sidebar UI
- [x] AnimaRuntimeManager.InitializeAsync() needs startup hook (hosted service)

### Known Blockers

None — ready to start

### Technical Debt

*Inherited from v1.4:*
- Schema mismatch between CLI and Runtime (extended manifest fields)
- SUMMARY metadata gaps (documentation only)
- Test isolation issues (pre-existing infrastructure)

---

*State initialized: 2026-02-28*
*Last updated: 2026-02-28*
*Roadmap created, ready for Phase 23 planning*
