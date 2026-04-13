# Phase 69: Background Chat Execution

## Goal

Allow chat generation and tool execution to continue when the user navigates away from the chat page, then restore the full result when the user comes back.

## Requirements

- Keep LLM streaming execution alive when the chat component detaches during page navigation.
- Restore the full assistant response when the user returns to chat.
- Resume live streaming in the UI if the user returns while generation is still in progress.
- Preserve cancel support for in-flight background generation after navigation.
- Preserve workspace and memory tool execution continuity while the UI is detached.

## Acceptance Criteria

- [ ] User sends a message, navigates away, returns later, and sees the finished assistant response in chat.
- [ ] User returns while generation is still running and sees live streaming resume instead of a stuck placeholder.
- [ ] User can cancel an in-progress generation after returning to chat.
- [ ] Workspace and memory tool calls continue to completion even if the chat page is not mounted.
- [ ] The final design does not rely on `ChatSessionState` alone as the only owner of in-flight chat execution state.

## Technical Notes

- Source milestone: `.trellis/planning/current-milestone.md`
- Migrated from legacy planning on `2026-04-10`
- Primary legacy references:
  - `.planning/ROADMAP.md` Phase 69 section
  - `.planning/REQUIREMENTS.md` `CHAT-01` to `CHAT-04`
- Likely implementation areas:
  - `src/OpenAnima.Core/Components/Shared/ChatPanel.razor`
  - `src/OpenAnima.Core/Services/ChatSessionState.cs`
  - `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs`
  - `src/OpenAnima.Core/Modules/LLMModule.cs`
- Cross-layer checklist required because this phase spans UI lifecycle, scoped state, persistence, and runtime execution ownership.
