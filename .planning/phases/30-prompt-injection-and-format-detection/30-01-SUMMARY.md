---
phase: 30-prompt-injection-and-format-detection
plan: 01
subsystem: modules
tags: [regex, format-detection, routing, xml-parsing, tdd]

# Dependency graph
requires:
  - phase: 29-routing-modules
    provides: AnimaRouteModule, AnimaInputPortModule, AnimaOutputPortModule — FormatDetector output feeds into these
provides:
  - FormatDetector class with Detect() method — pure XML routing marker parser
  - RouteExtraction record (ServiceName, Payload)
  - FormatDetectionResult record (PassthroughText, Routes, MalformedMarkerError)
  - Comprehensive unit test suite (16 tests) for all detection behaviors
affects:
  - 30-02 — LLMModule integration consumes FormatDetector.Detect()

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Compiled static Regex fields for performance-critical parsing"
    - "Regex.Replace as extraction mechanism (match callback builds routes list)"
    - "Two-regex approach: UnclosedMarkerRegex fast-path + RouteMarkerRegex extraction"
    - "Pure stateless class with no constructor dependencies (no ILogger)"

key-files:
  created:
    - src/OpenAnima.Core/Modules/FormatDetector.cs
    - tests/OpenAnima.Tests/Unit/FormatDetectorTests.cs
  modified: []

key-decisions:
  - "UnclosedMarkerRegex catches both complete-open-tag-without-close and partial tags (no closing >) using alternation: <route(?:\\b[^>]*>(?![\\s\\S]*</route>)|(?![^>]*>))"
  - "Unrecognised service names: MalformedMarkerError set but marker left in passthrough text (not stripped) — preserves LLM output for user to see"
  - "Service name normalisation: stored as lower-case of the known set entry (not of the raw LLM text) for canonical form"
  - "Passthrough text: trimmed + excessive blank lines (3+) collapsed to double-newline via ExcessiveBlankLinesRegex"

patterns-established:
  - "TDD RED-GREEN for regex-heavy logic: write behavior-driven tests first, then iterate regex until all pass"

requirements-completed: [FMTD-01, FMTD-02, FMTD-04]

# Metrics
duration: 4min
completed: 2026-03-13
---

# Phase 30 Plan 01: FormatDetector Summary

**Regex-based XML routing marker parser with case-insensitive service matching, unclosed-tag detection, and passthrough-text cleanup — built TDD with 16 unit tests**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-13T13:18:05Z
- **Completed:** 2026-03-13T13:22:21Z
- **Tasks:** 2 (RED + GREEN, no refactor needed)
- **Files modified:** 2

## Accomplishments

- FormatDetector.Detect() correctly parses `<route service="portName">payload</route>` XML markers from LLM output
- Case-insensitive service name matching (LLM may capitalise "Summarize" while config stores "summarize")
- Multiline payloads fully extracted (Singleline Regex mode)
- Two error cases distinguished: unclosed tags vs. unrecognised service names — both set MalformedMarkerError; unrecognised leaves marker in passthrough text
- Multiple markers per response all extracted in document order
- Empty/no-marker responses pass through cleanly with null error and empty routes list
- Lenient attribute whitespace handled by `service\s*=\s*"..."` pattern

## Task Commits

Each task was committed atomically:

1. **RED — Failing FormatDetector unit tests** - `33762e6` (test)
2. **GREEN — FormatDetector implementation** - `7939397` (feat)

_TDD: test commit precedes implementation commit._

## Files Created/Modified

- `src/OpenAnima.Core/Modules/FormatDetector.cs` — FormatDetector class with RouteExtraction and FormatDetectionResult records; two compiled static Regex fields
- `tests/OpenAnima.Tests/Unit/FormatDetectorTests.cs` — 16 unit tests covering all behavior cases from the plan

## Decisions Made

- **UnclosedMarkerRegex pattern choice:** The plan's suggested pattern `<route\b[^>]*>(?![\s\S]*</route>)` only catches complete open tags missing their close; a second alternation branch `(?![^>]*>)` was added to also catch partial `<route` strings that never reach their `>`. This was found during GREEN (test `Detect_PartialRouteTagWithoutAttribute_ReturnsMalformedError` failed) and fixed inline.

- **Service name normalisation:** Storing `known.ToLowerInvariant()` (not `rawServiceName.ToLowerInvariant()`) ensures canonical form matches whatever the config registered, avoiding surprising output if the config uses mixed case.

- **No ILogger:** FormatDetector is pure logic with no side effects. Logging is deferred to LLMModule (Plan 02) which will call Detect() and can log the result.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] UnclosedMarkerRegex didn't catch partial `<route` with no closing `>`**
- **Found during:** GREEN phase (test execution)
- **Issue:** `Detect_PartialRouteTagWithoutAttribute_ReturnsMalformedError` failed — input `"text with <route but no closing"` has no `>` so the tag never completes; the plan's suggested pattern requires `>` to be present before the lookahead fires
- **Fix:** Changed regex to alternation: catches both complete-open-tag-no-close AND incomplete open tag (no `>`)
- **Files modified:** `src/OpenAnima.Core/Modules/FormatDetector.cs`
- **Verification:** All 16 tests pass; 208 other tests unaffected (3 pre-existing failures unchanged)
- **Committed in:** `7939397` (GREEN implementation commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 — Bug)
**Impact on plan:** Necessary for correctness. The regex edge case was not covered by the plan's suggested pattern. No scope creep.

## Issues Encountered

None beyond the single regex fix documented above.

## User Setup Required

None — no external service configuration required.

## Self-Check

- [x] `src/OpenAnima.Core/Modules/FormatDetector.cs` — exists
- [x] `tests/OpenAnima.Tests/Unit/FormatDetectorTests.cs` — exists
- [x] Commit `33762e6` — exists (test: add failing FormatDetector unit tests)
- [x] Commit `7939397` — exists (feat: implement FormatDetector XML routing marker parser)
- [x] All 16 FormatDetector tests pass
- [x] No new test regressions (3 pre-existing failures unchanged)

## Self-Check: PASSED

## Next Phase Readiness

- FormatDetector is ready to be consumed by LLMModule in Plan 02
- API: `var result = detector.Detect(llmResponse, knownServiceNames)` — matches Plan 02 interface spec exactly
- RouteExtraction and FormatDetectionResult records are exported from `OpenAnima.Core.Modules` namespace

---
*Phase: 30-prompt-injection-and-format-detection*
*Completed: 2026-03-13*
