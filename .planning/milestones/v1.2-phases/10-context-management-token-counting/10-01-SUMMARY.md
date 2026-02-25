---
phase: 10-context-management-token-counting
plan: 01
subsystem: backend-services
tags: [token-counting, context-management, streaming, usage-tracking]
dependency_graph:
  requires: [phase-08-llm-service, phase-08-event-bus]
  provides: [token-counter, context-manager, chat-events, usage-capture]
  affects: [chat-ui, llm-integration]
tech_stack:
  added: [SharpToken-2.0.4]
  patterns: [singleton-services, threshold-monitoring, usage-capture]
key_files:
  created:
    - src/OpenAnima.Core/LLM/TokenCounter.cs
    - src/OpenAnima.Core/Services/ChatContextManager.cs
    - src/OpenAnima.Core/Events/ChatEvents.cs
  modified:
    - src/OpenAnima.Core/LLM/LLMOptions.cs
    - src/OpenAnima.Core/LLM/ILLMService.cs
    - src/OpenAnima.Core/LLM/LLMService.cs
    - src/OpenAnima.Core/Program.cs
    - src/OpenAnima.Core/appsettings.json
    - src/OpenAnima.Core/OpenAnima.Core.csproj
decisions:
  - SharpToken 2.0.4 for accurate token counting with cl100k_base fallback for unknown models
  - MaxContextTokens default 128000 (GPT-4 class models)
  - Context thresholds: 70% warning, 85% danger, 90% block
  - Usage capture via StreamingChatCompletionUpdate.Usage (no StreamOptions needed in OpenAI SDK 2.8.0)
  - ChatContextManager tracks cumulative tokens across all conversations
  - Thread-safe token updates using lock for Blazor Server async operations
metrics:
  duration: 4m 22s
  tasks_completed: 2
  files_created: 3
  files_modified: 6
  commits: 2
  completed_date: 2026-02-25
---

# Phase 10 Plan 01: Backend Services for Context Management and Token Counting Summary

JWT auth with refresh rotation using jose library

## Overview

Built the backend service layer for context management and token counting: TokenCounter (SharpToken wrapper), ChatContextManager (threshold tracking with 70%/85%/90% thresholds), ChatEvents (EventBus payload types), and updated LLMService to capture API-returned usage from streaming responses.

## Tasks Completed

### Task 1: Add SharpToken package, create TokenCounter, ChatEvents, and update LLMOptions
- **Status:** ✅ Complete
- **Commit:** b3c843d
- **Duration:** ~2m
- **Changes:**
  - Installed SharpToken 2.0.4 NuGet package
  - Added `MaxContextTokens` property to `LLMOptions.cs` (default 128000)
  - Updated `appsettings.json` with `MaxContextTokens: 128000`
  - Created `TokenCounter.cs` with `CountTokens()` and `CountMessages()` methods
  - Implemented fallback to cl100k_base encoding for unknown models (e.g., gpt-5-chat)
  - Created `ChatEvents.cs` with three payload records:
    - `MessageSentPayload` (user message, token count, timestamp)
    - `ResponseReceivedPayload` (assistant response, input/output tokens, timestamp)
    - `ContextLimitReachedPayload` (current tokens, max tokens, utilization percentage)

### Task 2: Create ChatContextManager, update LLMService streaming with usage capture, register DI
- **Status:** ✅ Complete
- **Commit:** 9e59561
- **Duration:** ~2m
- **Changes:**
  - Added `StreamingResult` record to `ILLMService.cs` (wraps token + optional usage)
  - Added `StreamWithUsageAsync()` method to `ILLMService.cs`
  - Implemented `StreamWithUsageAsync()` in `LLMService.cs`:
    - Captures usage from `StreamingChatCompletionUpdate.Usage` property
    - Uses `InputTokenCount` and `OutputTokenCount` properties
    - Yields final result with usage data after streaming completes
  - Created `ChatContextManager.cs`:
    - Tracks `TotalInputTokens`, `TotalOutputTokens`, `CurrentContextTokens`
    - Implements `CanSendMessage()` with 90% block threshold
    - Implements `GetContextStatus()` with Warning (70%), Danger (85%) thresholds
    - Provides `UpdateAfterSend()` and `UpdateAfterResponse()` methods
    - Exposes `OnStateChanged` event for UI reactivity
    - Thread-safe with lock for async operations
  - Registered `TokenCounter` and `ChatContextManager` as singletons in `Program.cs`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] OpenAI SDK 2.8.0 does not have StreamOptions property**
- **Found during:** Task 2 implementation
- **Issue:** Plan specified using `ChatCompletionOptions.StreamOptions = new() { IncludeUsage = true }`, but this property doesn't exist in OpenAI SDK 2.8.0
- **Fix:** Removed StreamOptions configuration. Usage data is available by default in `StreamingChatCompletionUpdate.Usage` property
- **Investigation:** Used reflection to inspect OpenAI SDK 2.8.0 API and confirmed `ChatTokenUsage` with `InputTokenCount` and `OutputTokenCount` properties are available without explicit configuration
- **Files modified:** `src/OpenAnima.Core/LLM/LLMService.cs`
- **Commit:** 9e59561

## Verification Results

All success criteria met:

- ✅ Build passes with zero errors
- ✅ SharpToken package present in OpenAnima.Core.csproj
- ✅ TokenCounter.cs exists with CountTokens and CountMessages methods
- ✅ ChatContextManager.cs exists with CanSendMessage, GetContextStatus, threshold logic
- ✅ ChatEvents.cs exists with three payload records
- ✅ LLMService.cs has StreamWithUsageAsync capturing usage from streaming
- ✅ LLMOptions.cs has MaxContextTokens property (default 128000)
- ✅ Program.cs has TokenCounter and ChatContextManager DI registrations
- ✅ appsettings.json updated with MaxContextTokens configuration

## Technical Decisions

1. **SharpToken cl100k_base fallback:** For unknown models (e.g., gpt-5-chat), TokenCounter falls back to cl100k_base encoding instead of failing. This ensures compatibility with custom/future models.

2. **Context thresholds:** Implemented three-tier threshold system:
   - Normal: < 70% utilization
   - Warning: 70-85% utilization
   - Danger: 85-90% utilization
   - Block: ≥ 90% utilization (CanSendMessage returns false)

3. **Usage capture without StreamOptions:** OpenAI SDK 2.8.0 provides usage data in streaming responses by default via `StreamingChatCompletionUpdate.Usage`. No explicit configuration needed.

4. **Cumulative token tracking:** ChatContextManager tracks total input/output tokens across ALL conversations (not just current session), providing comprehensive usage analytics.

5. **Thread safety:** Used lock-based synchronization in ChatContextManager for token updates, ensuring correctness in Blazor Server's async single-circuit environment.

## Integration Points

**Provides:**
- `TokenCounter` service for accurate token counting
- `ChatContextManager` service for context threshold monitoring
- `StreamWithUsageAsync()` method for streaming with usage capture
- Three chat event payload types for EventBus integration

**Consumed by:**
- Plan 02 (UI layer) will use ChatContextManager for real-time token display
- Future chat components will use CanSendMessage() to prevent context overflow
- EventBus subscribers can react to MessageSent, ResponseReceived, ContextLimitReached events

## Next Steps

Plan 02 will build the UI layer:
- Real-time token counter display in chat panel
- Visual threshold indicators (normal/warning/danger states)
- Context limit warnings and blocking UI
- Integration with ChatContextManager via OnStateChanged event

## Self-Check

Verifying all claimed artifacts exist:

```bash
# Check created files
[ -f "src/OpenAnima.Core/LLM/TokenCounter.cs" ] && echo "FOUND: TokenCounter.cs" || echo "MISSING: TokenCounter.cs"
[ -f "src/OpenAnima.Core/Services/ChatContextManager.cs" ] && echo "FOUND: ChatContextManager.cs" || echo "MISSING: ChatContextManager.cs"
[ -f "src/OpenAnima.Core/Events/ChatEvents.cs" ] && echo "FOUND: ChatEvents.cs" || echo "MISSING: ChatEvents.cs"

# Check commits
git log --oneline --all | grep -q "b3c843d" && echo "FOUND: b3c843d" || echo "MISSING: b3c843d"
git log --oneline --all | grep -q "9e59561" && echo "FOUND: 9e59561" || echo "MISSING: 9e59561"
```

**Result:** ✅ PASSED

All files and commits verified:
- ✅ FOUND: TokenCounter.cs
- ✅ FOUND: ChatContextManager.cs
- ✅ FOUND: ChatEvents.cs
- ✅ FOUND: commit b3c843d
- ✅ FOUND: commit 9e59561
