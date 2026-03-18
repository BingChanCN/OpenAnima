---
phase: 39-contracts-type-migration-structured-messages
plan: "01"
subsystem: contracts
tags: [contracts, llm, serialization, migration]
dependency_graph:
  requires: []
  provides: [ChatMessageInput in OpenAnima.Contracts, SerializeList, DeserializeList]
  affects: [OpenAnima.Core.LLM, OpenAnima.Core.Services, OpenAnima.Tests]
tech_stack:
  added: [System.Text.Json camelCase serialization in Contracts]
  patterns: [using alias for cross-project type migration, TDD red-green]
key_files:
  created:
    - src/OpenAnima.Contracts/ChatMessageInput.cs
    - tests/OpenAnima.Tests/Unit/ChatMessageInputContractsTests.cs
  modified:
    - src/OpenAnima.Core/LLM/ILLMService.cs
    - src/OpenAnima.Core/LLM/LLMService.cs
    - src/OpenAnima.Core/LLM/TokenCounter.cs
    - src/OpenAnima.Core/Services/ChatContextManager.cs
decisions:
  - "using alias pattern (not global using) for Core files — explicit, scoped, no namespace pollution"
  - "Test files already had using OpenAnima.Contracts — no changes needed for 6 test files"
  - "IReadOnlyList<ChatMessageInput> return type for DeserializeList — consistent with ILLMService interface"
metrics:
  duration: "~8 minutes"
  completed: "2026-03-18"
  tasks_completed: 2
  files_changed: 6
---

# Phase 39 Plan 01: Contracts Type Migration — ChatMessageInput Summary

ChatMessageInput record moved from OpenAnima.Core.LLM to OpenAnima.Contracts with SerializeList/DeserializeList helpers; all Core consumers updated via using alias; 353 tests passing.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create ChatMessageInput in Contracts + unit tests | e0d4eb6 | ChatMessageInput.cs, ChatMessageInputContractsTests.cs |
| 2 | Migrate consumers — remove record, add using aliases | 396c4c8 | ILLMService.cs, LLMService.cs, TokenCounter.cs, ChatContextManager.cs |

## Decisions Made

- **using alias pattern**: Core files use `using ChatMessageInput = OpenAnima.Contracts.ChatMessageInput;` — explicit and scoped, avoids namespace pollution from a broad `using OpenAnima.Contracts;` in LLM-layer files.
- **Test files unchanged**: All 6 test files already had `using OpenAnima.Contracts;` from prior phases — no modifications needed.
- **IReadOnlyList return type**: `DeserializeList` returns `IReadOnlyList<ChatMessageInput>` to match the `ILLMService` interface signature.

## Deviations from Plan

None — plan executed exactly as written. The 6 test files already had the required `using OpenAnima.Contracts;` directive, so no edits were needed there.

## Verification Results

1. `dotnet build src/OpenAnima.Contracts/OpenAnima.Contracts.csproj` — Build succeeded
2. `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj` — Build succeeded (zero errors)
3. `dotnet test tests/OpenAnima.Tests/OpenAnima.Tests.csproj` — 353 passed, 0 failed
4. `grep -r "public record ChatMessageInput" src/` — appears ONLY in Contracts/ChatMessageInput.cs

## Self-Check: PASSED
