---
gsd_state_version: 1.0
milestone: v1.4
milestone_name: Module SDK & DevEx
status: planning
last_updated: "2026-02-28T00:00:00Z"
progress:
  total_phases: 0
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 0
---

# Project State: OpenAnima v1.4 Module SDK & DevEx

**Last updated:** 2026-02-28
**Current milestone:** Planning v1.4 Module SDK & DevEx

## Project Reference

**Core value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

**Current focus:** Define v1.4 requirements and roadmap.

See: `.planning/PROJECT.md` (updated 2026-02-28)

## Current Position

**Status:** Defining requirements
**Progress:** [----------] 0%

**Next action:** Define v1.4 requirements for Module SDK & DevEx.

## Accumulated Context

### v1.4 Scope Decisions

- **SDK 形态:** dotnet new 项目模板
- **CLI 工具:** 极简 CLI（oani new、oani pack）
- **包格式:** 自定义 .oamod 格式（含清单和校验）
- **文档范围:** API 参考 + 快速入门 + 示例模块 + 开发指南
- **内置模块:** 不新增（v1.4 聚焦 SDK/文档）
- **分发方式:** 本地包加载（为模块市场预留基础）

### Key Decisions (v1.3)

- Zero new dependencies: Use .NET 8.0 built-ins for port system
- Custom topological sort: ~100 LOC implementation avoids 500KB+ QuikGraph dependency
- HTML5 + SVG editor: Native browser APIs with Blazor, no JavaScript framework
- Two-phase initialization: Load modules first, then wire connections
- Scoped EditorStateService: Per-circuit isolation in Blazor Server
- Port types fixed to Text and Trigger (not extensible by design)
- Level-parallel execution: Task.WhenAll within level, sequential between levels

### Active TODOs

(None — awaiting v1.4 planning)

### Known Blockers

None

---

*State initialized: 2026-02-28*
*Last updated: 2026-02-28*
*v1.3 complete, v1.4 planning pending*