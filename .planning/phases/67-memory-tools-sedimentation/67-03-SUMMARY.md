---
phase: 67-memory-tools-sedimentation
plan: "03"
subsystem: memory
tags: [sedimentation, disclosure-matcher, bilingual, recall, tdd]
dependency_graph:
  requires:
    - 67-01 (MemoryNode.Deprecated, schema migration)
    - 67-02 (SedimentationService base)
  provides:
    - Bilingual keyword extraction from Chinese+English conversations
    - Multi-scenario OR-split disclosure trigger matching
    - 20-message input cap for token cost control
  affects:
    - MemoryRecallService (uses DisclosureMatcher)
    - SedimentationService (extraction prompt and message cap)
tech_stack:
  added: []
  patterns:
    - TDD (RED/GREEN) for DisclosureMatcher changes
    - " OR " separator convention for multi-scenario triggers
key_files:
  created: []
  modified:
    - src/OpenAnima.Core/Memory/SedimentationService.cs
    - src/OpenAnima.Core/Memory/DisclosureMatcher.cs
    - tests/OpenAnima.Tests/Unit/DisclosureMatcherTests.cs
    - tests/OpenAnima.Tests/Unit/SedimentationServiceTests.cs
decisions:
  - "Bilingual keywords required when conversation contains Chinese — BOTH Chinese and English forms in keywords field"
  - "Disclosure triggers use ' OR ' separator for multi-scenario matching — sedimentation LLM now generates 3+ scenarios per node"
  - "20-message cap applied pre-extraction (last 20) — older messages rarely contain new stable knowledge worth re-extracting"
  - "StringSplitOptions.TrimEntries on OR split — handles whitespace variations in LLM output"
metrics:
  duration_seconds: 687
  completed_date: "2026-03-29"
  tasks_completed: 2
  files_modified: 4
requirements:
  - MEMS-01
  - MEMS-02
  - MEMS-03
---

# Phase 67 Plan 03: Memory Tools Sedimentation — Bilingual & OR-Split Summary

**One-liner:** Bilingual keyword extraction (Chinese+English) with OR-split multi-scenario disclosure triggers and 20-message input cap for cost-controlled, recall-friendly sedimentation.

## What Was Built

### Task 1: SedimentationService bilingual prompt + 20-message cap

`ExtractionSystemPrompt` replaced with a new prompt that:
1. Explicitly requires bilingual keywords when conversation contains Chinese (both Chinese `关键词` and English versions)
2. Requires multi-scenario disclosure triggers using " OR " separator with 3+ scenarios per node
3. Provides concrete examples in both requirements

20-message cap added before `BuildExtractionMessages`:
```csharp
var cappedMessages = messages.Count > 20
    ? (IReadOnlyList<ChatMessageInput>)messages.Skip(messages.Count - 20).ToList()
    : messages;
```
`BuildExtractionMessages` now uses `cappedMessages` instead of raw `messages`.

Two new tests in `SedimentationServiceTests.cs`:
- `SedimentAsync_MoreThan20Messages_CapsToLast20`: verifies messages 11-30 pass when 30 total; messages 1-10 excluded
- `SedimentAsync_ExactlyTwentyMessages_PassesAllThrough`: verifies no truncation at exactly 20 messages

### Task 2: DisclosureMatcher OR-split matching (TDD)

RED: 4 failing tests added for multi-scenario trigger behavior including Chinese sub-trigger.

GREEN: `DisclosureMatcher.Match` updated:
```csharp
var triggers = node.DisclosureTrigger.Split(" OR ",
    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

if (triggers.Any(t => context.Contains(t, StringComparison.OrdinalIgnoreCase)))
    results.Add(node);
```

Old single-match `context.Contains(node.DisclosureTrigger, ...)` pattern fully replaced.

Backward compatible: single-phrase triggers (no " OR ") return single-element array from `Split`, still match correctly.

## Commits

| Hash | Type | Description |
|------|------|-------------|
| b965876 | feat | Update SedimentationService with bilingual prompt and 20-message cap |
| c22fc29 | test | Add failing tests for DisclosureMatcher OR-split trigger matching (TDD RED) |
| a37b03f | feat | Implement DisclosureMatcher OR-split multi-scenario trigger matching (TDD GREEN) |

## Verification Results

- `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj` — succeeded (0 errors)
- `dotnet test --filter "Sedimentation|DisclosureMatcher"` — Passed 33/33

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed compile errors preventing test execution**
- **Found during:** Task 2 initial test run
- **Issue:** `MemoryGraph.cs` was missing `SoftDeleteNodeAsync` implementation and `GetAllNodesAsync` had wrong signature (no `bool includeDeprecated` parameter). These were added by the 67-01 parallel agent to the interface + tests but the implementation was incomplete at the time this agent ran.
- **Fix:** The 67-01 parallel agent completed the implementation between our initial read and subsequent build attempts. Build succeeded once that work landed.
- **Files modified:** `src/OpenAnima.Core/Memory/MemoryGraph.cs` (by 67-01 agent, not this plan)
- **Commit:** via 67-01 agent

**2. [Rule 2 - Scope adjustment] 20-message cap test placed in SedimentationServiceTests instead of LLMModuleSedimentationTests**
- **Found during:** Task 1
- **Issue:** The plan specified adding the cap test to `LLMModuleSedimentationTests.cs`, but that file tests `LLMModule`'s wiring to `ISedimentationService` (the interface). The cap is internal `SedimentationService` behavior and the `llmCallOverride` injection for message capture is only available in `SedimentationServiceTests`.
- **Fix:** Added tests to `SedimentationServiceTests.cs` where the `MakeService(llmOverride)` pattern exists for capturing LLM call parameters.
- **Files modified:** `tests/OpenAnima.Tests/Unit/SedimentationServiceTests.cs`

## Known Stubs

None — all functionality is wired end-to-end through existing `MemoryRecallService` which calls `DisclosureMatcher.Match`, and `LLMModule` which calls `ISedimentationService.SedimentAsync`.

## Self-Check: PASSED

- FOUND: src/OpenAnima.Core/Memory/SedimentationService.cs
- FOUND: src/OpenAnima.Core/Memory/DisclosureMatcher.cs
- FOUND: tests/OpenAnima.Tests/Unit/DisclosureMatcherTests.cs
- FOUND: tests/OpenAnima.Tests/Unit/SedimentationServiceTests.cs
- FOUND: .planning/phases/67-memory-tools-sedimentation/67-03-SUMMARY.md
- Commits b965876, c22fc29, a37b03f all verified in git log
