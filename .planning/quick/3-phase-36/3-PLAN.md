---
phase: quick-3-phase-36
plan: 3
type: execute
wave: 1
depends_on: []
files_modified:
  - .planning/quick/3-phase-36/3-REVIEW.md
autonomous: true
requirements: []

must_haves:
  truths:
    - "Phase 36 requirements coverage is validated against ROADMAP success criteria"
    - "Phase 36 verification evidence matches claimed outcomes"
    - "Phase 36 technical decisions are consistent across plans and summaries"
  artifacts:
    - path: ".planning/quick/3-phase-36/3-REVIEW.md"
      provides: "Cross-review findings for Phase 36"
      min_lines: 50
  key_links:
    - from: ".planning/quick/3-phase-36/3-REVIEW.md"
      to: ".planning/phases/36-built-in-module-decoupling/*"
      via: "verification cross-check"
      pattern: "36-.*\\.md"
---

<objective>
Cross-review Phase 36 (Built-in Module Decoupling) for completeness, consistency, and verification quality.

Purpose: Phase 36 is the final phase of v1.7 milestone. A thorough cross-review ensures the decoupling work is properly documented, verified, and ready for milestone closeout.

Output: Review document identifying any gaps, inconsistencies, or areas needing attention before milestone closeout.
</objective>

<execution_context>
@/home/user/.claude/get-shit-done/workflows/execute-plan.md
@/home/user/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@.planning/ROADMAP.md
@.planning/phases/36-built-in-module-decoupling/36-VERIFICATION.md
@.planning/phases/36-built-in-module-decoupling/36-01-SUMMARY.md
@.planning/phases/36-built-in-module-decoupling/36-05-SUMMARY.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Cross-check requirements coverage and verification evidence</name>
  <files>.planning/quick/3-phase-36/3-REVIEW.md</files>
  <action>
Review Phase 36 against these dimensions:

1. **Requirements Coverage**: Verify all 5 DECPL requirements (DECPL-01 through DECPL-05) from ROADMAP are addressed in plans and verified in 36-VERIFICATION.md

2. **Success Criteria Alignment**: Check that ROADMAP success criteria (12-module inventory, zero Core usings except LLM, DI resolution, zero regressions, template generation) match verification evidence

3. **Verification Evidence Quality**: Validate that 36-VERIFICATION.md provides concrete evidence for each claimed truth:
   - Test file paths exist
   - Test names are specific
   - Pass counts are documented (334/334, 76/76)
   - Source audit approach is described

4. **Technical Consistency**: Cross-check technical decisions across:
   - Plan frontmatter (files_modified, depends_on, wave assignments)
   - Summary deviations sections
   - STATE.md accumulated decisions
   - Verification key links

5. **Completeness Check**:
   - All 5 plans have matching SUMMARY files
   - VERIFICATION.md status is "passed"
   - No gaps array entries in VERIFICATION.md
   - STATE.md reflects phase completion

Create a structured review document with:
- Executive summary (pass/concerns)
- Findings by dimension (requirements, verification, consistency, completeness)
- Specific issues if any (with file references)
- Recommendations for milestone closeout
  </action>
  <verify>
    <automated>test -f .planning/quick/3-phase-36/3-REVIEW.md && wc -l .planning/quick/3-phase-36/3-REVIEW.md | awk '{if ($1 >= 50) exit 0; else exit 1}'</automated>
  </verify>
  <done>Review document exists with structured findings across all review dimensions</done>
</task>

<task type="auto">
  <name>Task 2: Validate test evidence claims</name>
  <files>.planning/quick/3-phase-36/3-REVIEW.md</files>
  <action>
Verify the test files and evidence mentioned in 36-VERIFICATION.md actually exist:

1. Check test files exist:
   - tests/OpenAnima.Tests/Integration/BuiltInModuleDecouplingTests.cs
   - tests/OpenAnima.Tests/Integration/ModuleRuntimeInitializationTests.cs
   - tests/OpenAnima.Cli.Tests/CliFoundationTests.cs

2. Verify key source files mentioned in verification:
   - src/OpenAnima.Core/Modules/LLMModule.cs
   - src/OpenAnima.Cli/Templates/module-cs.tmpl
   - src/OpenAnima.Contracts/ModuleMetadataRecord.cs

3. Check the 12 authoritative module files exist:
   - LLMModule, ChatInputModule, ChatOutputModule, HeartbeatModule
   - FixedTextModule, TextJoinModule, TextSplitModule, ConditionalBranchModule
   - AnimaInputPortModule, AnimaOutputPortModule, AnimaRouteModule, HttpRequestModule

4. Spot-check one module file (e.g., ChatInputModule.cs) to confirm it uses Contracts imports

Append validation results to the review document with:
- Files verified (count)
- Any missing files
- Spot-check findings
- Overall evidence quality assessment
  </action>
  <verify>
    <automated>grep -q "Evidence Validation" .planning/quick/3-phase-36/3-REVIEW.md</automated>
  </verify>
  <done>Review document includes evidence validation section confirming test and source files exist</done>
</task>

</tasks>

<verification>
Review document provides actionable assessment of Phase 36 quality and readiness for milestone closeout.
</verification>

<success_criteria>
- Review document exists with structured findings
- All 5 DECPL requirements are accounted for
- Verification evidence is validated against actual files
- Any issues or recommendations are clearly documented
- Review provides clear go/no-go signal for milestone closeout
</success_criteria>

<output>
After completion, create `.planning/quick/3-phase-36/3-SUMMARY.md`
</output>
