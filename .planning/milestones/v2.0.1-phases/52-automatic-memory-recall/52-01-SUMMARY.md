---
phase: 52-automatic-memory-recall
plan: 01
subsystem: memory
tags: [memory-recall, tdd, disclosure, glossary, deduplication, ranking]
dependency_graph:
  requires: []
  provides: [IMemoryRecallService, RecalledMemoryResult, RecalledNode, MemoryRecallService]
  affects: [LLMModule, RunService]
tech_stack:
  added: []
  patterns: [Manual fake stubs (no Moq), TDD Red-Green, Dictionary dedup by URI, Aho-Corasick glossary via IMemoryGraph]
key_files:
  created:
    - src/OpenAnima.Core/Memory/IMemoryRecallService.cs
    - src/OpenAnima.Core/Memory/RecalledMemoryResult.cs
    - src/OpenAnima.Core/Memory/MemoryRecallService.cs
    - tests/OpenAnima.Tests/Unit/MemoryRecallServiceTests.cs
  modified: []
decisions:
  - Glossary dedup: multiple keyword matches for same URI are joined into a single reason string ("glossary: k1, k2") rather than producing duplicate nodes
  - RecallType remains "Disclosure" when a node is matched by both disclosure and glossary — priority is not downgraded
  - Budget test uses 13 nodes * 1000-char content (truncated to 500) to verify exactly 12 fit within the 6000-char cap
metrics:
  duration: 2m 30s
  completed_date: "2026-03-22"
  tasks_completed: 2
  files_created: 4
  files_modified: 0
  tests_added: 9
  tests_total: 546
---

# Phase 52 Plan 01: Memory Recall Service Summary

**One-liner:** IMemoryRecallService with disclosure + glossary orchestration, URI deduplication, Boot > Disclosure > Glossary priority sort, 500-char node truncation, and 6000-char total budget.

## What Was Built

A standalone `MemoryRecallService` that accepts `(animaId, context)` and returns a ranked, deduplicated, budget-bounded list of `RecalledNode` objects. It keeps recall logic out of `LLMModule` by orchestrating existing primitives (`DisclosureMatcher`, `IMemoryGraph.FindGlossaryMatches`, `IMemoryGraph.RebuildGlossaryAsync`).

### Files Created

| File | Purpose |
|------|---------|
| `IMemoryRecallService.cs` | Service contract: `RecallAsync(animaId, context, ct)` |
| `RecalledMemoryResult.cs` | Result record with `Nodes`, `HasAny`; `RecalledNode` with `Reason`, `RecallType`, `TruncatedContent` |
| `MemoryRecallService.cs` | Full implementation: disclosure match, glossary match, dedup, priority sort, truncate, budget cap |
| `MemoryRecallServiceTests.cs` | 9 unit tests + `FakeMemoryGraph` stub with `RebuildGlossaryCalled` flag |

## TDD Execution

**RED:** Created interface, result records, and 9 failing tests. Build produced exactly 1 error: `MemoryRecallService` type not found. Committed as `test(52-01)`.

**GREEN:** Implemented `MemoryRecallService`. All 9 target tests pass. Full suite 546/546 green. Committed as `feat(52-01)`.

## Decisions Made

1. **Multiple glossary keywords per URI:** When `FindGlossaryMatches` returns multiple `(keyword, uri)` pairs for the same URI, keywords are joined into a single reason string (`"glossary: k1, k2"`) — one `RecalledNode` per URI.
2. **RecallType on merged nodes:** A node matched by both disclosure and glossary keeps `RecallType = "Disclosure"` to preserve its higher priority in ranking.
3. **Budget boundary:** The accumulator breaks on `> MaxTotalChars`, not `>=`, so exactly 12 nodes of 500 chars each fit within the 6000-char budget.

## Test Coverage

| Test | Behavior Verified |
|------|------------------|
| `DisclosureTriggerMatch_ReturnsNodeWithDisclosureReason` | Disclosure recall + reason |
| `GlossaryKeywordMatch_ReturnsNodeWithGlossaryReason` | Glossary recall + reason |
| `SameUriMatchedByBothDisclosureAndGlossary_DeduplicatesToOneNodeWithMergedReason` | URI dedup + merged reason |
| `NoMatches_ReturnsEmptyResult` | `HasAny == false` guard |
| `PrioritySorts_DisclosureBeforeGlossary_ThenByUpdatedAtDescending` | Sort order |
| `TruncatesIndividualNodeContentTo500Characters` | 500-char node cap |
| `DropsTailNodesWhenTotalCharacterCountExceeds6000` | 6000-char budget cap |
| `CallsRebuildGlossaryAsyncBeforeFindGlossaryMatches` | Glossary rebuild order |
| `EachRecalledNodeHasNonEmptyReason` | Reason non-empty invariant |

## Deviations from Plan

None — plan executed exactly as written.

## Self-Check: PASSED

| Item | Status |
|------|--------|
| `IMemoryRecallService.cs` exists | FOUND |
| `RecalledMemoryResult.cs` exists | FOUND |
| `MemoryRecallService.cs` exists | FOUND |
| `MemoryRecallServiceTests.cs` exists | FOUND |
| Commit `f9409c7` (RED) | FOUND |
| Commit `5c5d1bd` (GREEN) | FOUND |
| All 9 MemoryRecallServiceTests pass | PASSED |
| Full suite 546/546 green | PASSED |
