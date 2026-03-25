---
quick_id: 260325-ntq
description: "修复Dashboard中输入框会侵入左右边界，输入框宽度约为窗口1/3而不是铺满"
completed: "2026-03-25"
tasks_completed: 2
commits: ["7fc953f", "cc44ede"]
files_modified:
  - src/OpenAnima.Core/Components/Shared/ChatInput.razor.css
  - src/OpenAnima.Core/Components/Shared/ChatPanel.razor.css
verification:
  - dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj
---

# Quick Task 260325-ntq: Dashboard Input Width Summary

**One-liner:** Constrained the Dashboard chat composer to an approximately one-third viewport width on desktop and kept it responsive on smaller screens.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Clamp the chat input card width and prevent the textarea from overflowing its flex container | 7fc953f |
| 2 | Center the chat input wrapper so the parent chat panel does not stretch the composer across the full width | cc44ede |

## Changes Made

### Task 1: ChatInput.razor.css

- Replaced the loose `36%` width with `width: clamp(20rem, 33vw, 34rem)` plus `max-width: 100%`
- Added `min-width: 0` on the textarea so the flex child can shrink cleanly within the card
- Added a mobile breakpoint that restores full-width behavior below `768px`

### Task 2: ChatPanel.razor.css

- Added `align-items: center` to `.chat-input-wrapper` so the child input card keeps its intended centered width

## Deviations from Plan

None.

## Verification

- `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj` succeeded with 0 warnings and 0 errors
