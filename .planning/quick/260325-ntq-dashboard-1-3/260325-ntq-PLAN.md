---
quick_id: 260325-ntq
description: "修复Dashboard中输入框会侵入左右边界，输入框宽度约为窗口1/3而不是铺满"
tasks: 2
---

# Quick Plan: Dashboard Chat Input Width Constraint

## Task 1: Tighten ChatInput width on desktop

**files:** `src/OpenAnima.Core/Components/Shared/ChatInput.razor.css`
**action:**
- Replace the loose percentage width with a clamped width centered in the panel
- Keep the input responsive by capping width at the parent container on narrow screens
- Ensure the textarea can shrink inside the flex container without pushing past the card edges

**verify:** CSS compiles and the chat input renders near one third of the viewport on desktop
**done:** Input card is centered and no longer spans into the left and right edges

## Task 2: Reinforce wrapper alignment for the Dashboard chat footer

**files:** `src/OpenAnima.Core/Components/Shared/ChatPanel.razor.css`
**action:**
- Center the input wrapper so the child component is not stretched by the flex column layout
- Add a small-screen breakpoint so the input can fall back to full width when the viewport is narrow

**verify:** The footer remains centered on desktop and usable on small screens
**done:** Parent layout preserves the intended width constraint
