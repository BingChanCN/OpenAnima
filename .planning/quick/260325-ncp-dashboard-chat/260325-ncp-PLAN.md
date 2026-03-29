---
quick_id: 260325-ncp
description: "Dashboard Chat input box - center and make rectangular"
tasks: 2
---

# Quick Plan: Dashboard Chat Input Redesign

## Task 1: Modify ChatInput.razor.css — center and reshape input container

**files:** `src/OpenAnima.Core/Components/Shared/ChatInput.razor.css`
**action:**
- Change `.chat-input-container` from full-width fixed bar to centered with `max-width: 680px` and `margin: 0 auto`
- Remove `position: fixed; bottom: 0; left: 0; right: 0;` — parent layout handles positioning
- Change textarea `rows` default and add `min-height` to make it taller (rectangular, not single line strip)
- Add border-radius to container for rectangular card appearance

**verify:** CSS compiles, input box visually centered and rectangular
**done:** Input container is centered and rectangular

## Task 2: Adjust ChatInput.razor and ChatPanel.razor.css for layout integration

**files:**
- `src/OpenAnima.Core/Components/Shared/ChatInput.razor`
- `src/OpenAnima.Core/Components/Shared/ChatPanel.razor.css`

**action:**
- Change textarea `rows="1"` to `rows="3"` in ChatInput.razor for rectangular shape
- Update `.chat-messages` padding-bottom in ChatPanel.razor.css to accommodate new input height
- Ensure `.chat-panel` properly contains the non-fixed input

**verify:** Chat messages don't overlap with input, input is properly contained within chat panel
**done:** Layout integrates cleanly with reshaped input
