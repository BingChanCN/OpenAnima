# Phase 18: RMOD Verification Evidence Backfill - Research

**Researched:** 2026-02-27  
**Domain:** 里程碑审计证据链修复（verification artifact backfill）  
**Confidence:** HIGH — 基于仓库现有 phase 工件与审计报告直接分析

## Summary

Phase 18 是纯 gap-closure 阶段，核心目标不是新增运行时代码，而是补齐缺失的验证证据链：`.planning/phases/14-module-refactoring-runtime-integration/14-VERIFICATION.md` 与 `.planning/phases/16-module-runtime-initialization-port-registration/16-VERIFICATION.md`。  

当前 v1.3 失败门禁由 `v1.3-MILESTONE-AUDIT.md` 明确指出：`RMOD-01..04` 已在 `REQUIREMENTS.md` 和 phase summaries 被声明完成，但由于缺少 14/16 的 VERIFICATION 文件，最终被判定为 `orphaned`。这属于“证据缺口”，不是“实现缺口”。

Phase 18 的计划应最小化改动范围，聚焦三件事：
1. 产出两份缺失 verification 报告（结构与已有 15/17 验证报告一致）。  
2. 在报告中明确 RMOD-01..04 的可追溯证据（truths/artifacts/requirements coverage）。  
3. 完成前置一致性校验（frontmatter + 引用 + RMOD 覆盖），为后续重新里程碑审计提供稳定输入。

**Primary recommendation:** 采用单计划（1 plan, 1 wave, 3 tasks）完成双验证报告补齐与 traceability 一致性校验，避免跨 phase 扩散。

## Phase Requirements Coverage

| Req ID | Requirement | Current State | Phase 18 Needed |
|---|---|---|---|
| RMOD-01 | LLM service refactored into LLMModule | 实现与 summary 已存在，验证文档缺失 | 在 14/16 verification 中给出代码+测试证据并标注通过 |
| RMOD-02 | Chat input refactored into ChatInputModule | 同上 | 同上 |
| RMOD-03 | Chat output refactored into ChatOutputModule | 同上 | 同上 |
| RMOD-04 | Heartbeat refactored into HeartbeatModule | 同上 | 同上 |

## Key Findings

### 1) 失败根因是“verification artifact 缺失”
- 审计文件：`.planning/v1.3-MILESTONE-AUDIT.md`
- 明确指出：`14-VERIFICATION.md`、`16-VERIFICATION.md` 不存在，导致 RMOD-01..04 orphaned。

### 2) RMOD 实现证据已充足，可直接回填验证报告
- Phase 14 Plan 01 与 Summary 已覆盖四个模块重构（LLM/ChatInput/ChatOutput/Heartbeat）。
- Phase 16 Plan 01 与 Summary 已覆盖启动期端口注册与模块初始化，对 RMOD 运行态成立提供补强证据。
- 可复用测试域：
  - `tests/OpenAnima.Tests/Modules/ModuleTests.cs`
  - `tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs`
  - `tests/OpenAnima.Tests/Integration/ModulePipelineIntegrationTests.cs`

### 3) Verification 报告应复用现有“可审计结构”
已有 15/17 的 VERIFICATION 报告结构稳定：
- frontmatter: `phase`, `verified`, `status`, `score`, `gaps`, `re_verification`
- 主体包含：`Observable Truths`、`Required Artifacts`、`Requirements Coverage`、`Automated Verification Run`

Phase 18 回填时应保持同等结构，避免未来审计脚本或人工审查出现格式偏差。

### 4) 元数据漂移与本 phase 的边界
`14-03-SUMMARY.md` 中 `WIRE-04/WIRE-05` 是独立 metadata drift，已在 Phase 19 定义处理。  
Phase 18 只需在 verification 报告中使用合法 REQ-ID，并标注该漂移由后续 phase 清理，避免 scope creep。

## Recommended Plan Shape

### Plan 18-01 (Wave 1)
目标：一次性补齐 14/16 verification，并完成 RMOD traceability 预审校验。

任务建议：
1. 生成 `14-VERIFICATION.md`（包含 RMOD-01..04 的可观测真值与证据表）。  
2. 生成 `16-VERIFICATION.md`（补足启动初始化与端口注册链路对 RMOD 的验证证据）。  
3. 运行结构/引用/RMOD覆盖校验，并确认 `REQUIREMENTS.md` 中 RMOD traceability 仍指向 Phase 18 gap closure。

## Validation Architecture

### Automated checks (phase execution内可运行)
- `node /home/user/.codex/get-shit-done/bin/gsd-tools.cjs frontmatter validate .planning/phases/14-module-refactoring-runtime-integration/14-VERIFICATION.md --schema verification`
- `node /home/user/.codex/get-shit-done/bin/gsd-tools.cjs frontmatter validate .planning/phases/16-module-runtime-initialization-port-registration/16-VERIFICATION.md --schema verification`
- `node /home/user/.codex/get-shit-done/bin/gsd-tools.cjs verify references .planning/phases/14-module-refactoring-runtime-integration/14-VERIFICATION.md`
- `node /home/user/.codex/get-shit-done/bin/gsd-tools.cjs verify references .planning/phases/16-module-runtime-initialization-port-registration/16-VERIFICATION.md`
- `rg -n "RMOD-01|RMOD-02|RMOD-03|RMOD-04" .planning/phases/14-module-refactoring-runtime-integration/14-VERIFICATION.md .planning/phases/16-module-runtime-initialization-port-registration/16-VERIFICATION.md`

### Post-phase gate (outside this phase execution)
- 重新执行 `$gsd-audit-milestone`，验证 RMOD orphaned gaps 归零并更新最新审计文件。

## Risks and Mitigations

1. **Risk:** 回填报告内容与现有代码/测试不一致。  
   **Mitigation:** 所有证据必须引用真实文件与可运行测试命令，不写“推断式结论”。

2. **Risk:** 报告格式偏离现有 verification 规范，后续审计无法稳定消费。  
   **Mitigation:** 对齐 Phase 15/17 报告结构，并用 `frontmatter validate` + `verify references` 机械校验。

3. **Risk:** 误把 Phase 19 的 metadata drift 一并处理导致范围蔓延。  
   **Mitigation:** Phase 18 仅处理 RMOD orphan closure；`WIRE-04/WIRE-05` 清理留给 Phase 19。

## Sources

- `.planning/v1.3-MILESTONE-AUDIT.md`
- `.planning/ROADMAP.md`
- `.planning/REQUIREMENTS.md`
- `.planning/phases/14-module-refactoring-runtime-integration/14-01-PLAN.md`
- `.planning/phases/14-module-refactoring-runtime-integration/14-01-SUMMARY.md`
- `.planning/phases/14-module-refactoring-runtime-integration/14-02-SUMMARY.md`
- `.planning/phases/14-module-refactoring-runtime-integration/14-03-SUMMARY.md`
- `.planning/phases/16-module-runtime-initialization-port-registration/16-01-PLAN.md`
- `.planning/phases/16-module-runtime-initialization-port-registration/16-01-SUMMARY.md`
- `.planning/phases/15-fix-configurationloader-key-mismatch/15-VERIFICATION.md`
- `.planning/phases/17-e2e-module-pipeline-integration-editor-polish/17-VERIFICATION.md`
