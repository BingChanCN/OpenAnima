# Phase 19: Requirement Metadata Drift Cleanup - Research

**Researched:** 2026-02-28
**Domain:** Requirement traceability and metadata consistency
**Confidence:** HIGH

## Summary

This phase addresses metadata drift between summary frontmatter and the canonical REQUIREMENTS.md file. The v1.3 milestone audit identified `14-03-SUMMARY.md` containing invalid requirement IDs (`WIRE-04`, `WIRE-05`) that do not exist in the requirements specification. This creates orphaned references that break audit traceability and mislead future readers about what requirements were satisfied.

**Primary recommendation:** Replace `WIRE-04, WIRE-05` in `14-03-SUMMARY.md` with `E2E-01` (the correct requirement per the plan), or with an empty array if E2E-01 is already claimed by Phase 17.

## User Constraints

No user decisions provided. This phase is a gap-closure task with defined scope.

## Problem Analysis

### The Invalid IDs

`14-03-SUMMARY.md` frontmatter contains:
```yaml
requirements-completed: [WIRE-04, WIRE-05]
```

However, REQUIREMENTS.md only defines WIRE-01, WIRE-02, WIRE-03 under the "Wiring Engine" section. There is no WIRE-04 or WIRE-05 defined anywhere in the project.

### Correct Mapping for Phase 14 Plan 03

The plan file `14-03-PLAN.md` specifies:
```yaml
requirements: [E2E-01]
```

The plan's objective was:
> "Wire modules into DI, connect ChatPanel UI to ChatInputModule/ChatOutputModule, and verify end-to-end ChatInput→LLM→ChatOutput conversation flow matches v1.2 behavior."

This work directly supports **E2E-01**: "User can wire ChatInput -> LLM -> ChatOutput in editor and have a working conversation identical to v1.2"

### Summary Requirement Mapping Audit

| Summary File | `requirements-completed` | Valid? | Notes |
|--------------|--------------------------|--------|-------|
| `14-01-SUMMARY.md` | `[RMOD-01, RMOD-02, RMOD-03, RMOD-04]` | VALID | Matches plan requirements |
| `14-02-SUMMARY.md` | `[RTIM-01, RTIM-02]` | VALID | Matches plan requirements |
| `14-03-SUMMARY.md` | `[WIRE-04, WIRE-05]` | **INVALID** | IDs do not exist in REQUIREMENTS.md |
| `15-01-SUMMARY.md` | `[]` | VALID | Empty is intentional (unblock-only phase) |
| `16-01-SUMMARY.md` | `[PORT-04, RMOD-01, RMOD-02, RMOD-03, RMOD-04, EDIT-01]` | VALID | Matches plan requirements |
| `17-01-SUMMARY.md` | `[E2E-01]` | VALID | Matches plan requirements |
| `17-02-SUMMARY.md` | `[RTIM-01, RTIM-02]` | VALID | Matches plan requirements |
| `18-01-SUMMARY.md` | `[RMOD-01, RMOD-02, RMOD-03, RMOD-04]` | VALID | Verification backfill |

### Additional Metadata Gaps from Audit

The v1.3-MILESTONE-AUDIT.md identifies:

1. **Checkbox drift in REQUIREMENTS.md**:
   - PORT-04 and EDIT-01 are marked `[ ]` (unchecked) despite verification evidence showing completion
   - RMOD-01..04 remain `[ ]` (pending Phase 18 backfill verification)

2. **Missing frontmatter in v1.3 summaries**:
   - Phase 11, 12, 12.5, 13 summaries lack `requirements-completed` frontmatter
   - This causes 11 requirements to show as "partial" in the 3-source audit matrix

## Recommended Fix Strategy

### Primary Fix (Phase 19 Scope)

**File:** `.planning/phases/14-module-refactoring-runtime-integration/14-03-SUMMARY.md`

Change line 35 from:
```yaml
requirements-completed: [WIRE-04, WIRE-05]
```

To:
```yaml
requirements-completed: []
```

**Rationale:** E2E-01 is already correctly claimed by `17-01-SUMMARY.md`. Phase 14 Plan 03 contributed foundational work (DI registration, initial E2E tests) but the full requirement was satisfied in Phase 17 with the complete ChatPanel module pipeline integration. Using an empty array is honest about the contribution without creating duplicate claims or invalid IDs.

### Out of Scope for Phase 19

Per ROADMAP.md, Phase 19 scope is limited to:
> Eliminate invalid requirement IDs in summary metadata and restore consistent requirement traceability

The following are explicitly documented in the audit but **NOT in Phase 19 scope**:
1. Backfilling missing frontmatter in Phase 11/12/12.5/13 summaries
2. Fixing checkbox drift in REQUIREMENTS.md (PORT-04, EDIT-01)
3. Changing any other summary files

These items can be addressed in future phases if needed.

## Validation Approach

After the fix, re-run the milestone audit to confirm:
1. No invalid requirement IDs remain in any summary frontmatter
2. All summary `requirements-completed` entries reference IDs that exist in REQUIREMENTS.md
3. The integration gap for `14-03-SUMMARY.md → REQUIREMENTS.md` is closed

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| N/A | This phase has no functional requirements — it is metadata cleanup only | N/A |

## Success Criteria (from ROADMAP.md)

1. `14-03-SUMMARY.md` no longer references undefined requirement IDs
2. Summary frontmatter requirement IDs are valid against `.planning/REQUIREMENTS.md`
3. Audit reports no metadata drift for requirement mappings

## Sources

### Primary (HIGH confidence)
- `.planning/REQUIREMENTS.md` - Canonical requirement definitions
- `.planning/v1.3-MILESTONE-AUDIT.md` - Audit findings identifying the drift
- `.planning/phases/14-module-refactoring-runtime-integration/14-03-SUMMARY.md` - File with invalid IDs
- `.planning/phases/14-module-refactoring-runtime-integration/14-03-PLAN.md` - Correct requirement mapping

### Secondary (MEDIUM confidence)
- `.planning/ROADMAP.md` - Phase 19 scope definition
- `.planning/phases/17-e2e-module-pipeline-integration-editor-polish/17-01-SUMMARY.md` - Confirms E2E-01 ownership

## Metadata

**Confidence breakdown:**
- Problem identification: HIGH - Direct observation of invalid IDs in summary frontmatter
- Correct mapping: HIGH - Cross-referenced with plan file and REQUIREMENTS.md
- Scope understanding: HIGH - ROADMAP explicitly defines Phase 19 goals

**Research date:** 2026-02-28
**Valid until:** Until requirements or summaries are modified