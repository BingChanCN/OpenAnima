---
status: draft
---

# Phase 59: Tool Call Display and UI Wiring - UI Specification

## 1. Registry & Design System
- **Design System:** Native Blazor + Scoped CSS (No external design system detected).
- **Tool:** none (Shadcn not applicable for Blazor Server).
- **Third-Party Registries:** none.
- **Safety Gate:** Not applicable.

*(Source: Codebase detection - Blazor project, no `components.json` or React detected).*

## 2. Layout & Spacing
- **Scale:** 4-point scale (4, 8, 12, 16, 24).
- **Container Paddings:**
  - Tool Card internal padding: `12px` (`0.75rem`) — matches existing `.message-content`.
  - Tool Badge padding: `4px 8px` (`0.25rem 0.5rem`).
- **Gaps:** 
  - Between tool cards: `8px` (`0.5rem`).
  - Between tool cards and final text: `12px` (`0.75rem`).
- **Border Radius:** `8px` for main cards, `4px` for badges/inner blocks.
- **Exceptions:** None.

*(Source: Contextual default derived from `ChatMessage.razor.css`).*

## 3. Typography
- **Font Sizes:**
  - Base Body / Chat Reply: `0.95rem` (~15px)
  - Tool Card Parameters & Result text: `0.85rem` (~13-14px)
  - Tool Name / Titles: `1.1rem` (~18px)
- **Font Weights:**
  - Regular: `400` (Used for parameters, results, body text)
  - Medium: `500` (Used for badge text)
  - Semibold: `600` (Used for tool names and headers)
- **Line Heights:**
  - Body: `1.5`
  - Headings/Badges: `1.2`

*(Source: Codebase defaults from `ChatMessage.razor.css` and `ConfirmDialog.razor.css`).*

## 4. Color
- **Dominant Surface (60%):** `--card-bg` (`rgba(255, 255, 255, 0.05)`) for tool card backgrounds.
- **Secondary Surface (30%):** `--border-color` (`rgba(255, 255, 255, 0.1)`) for tool card borders and parameter/result dividers.
- **Accent & State (10%):**
  - Primary Accent: `#6366f1` (Indigo 500) — Reserved for active/running spinner animations.
  - Success State: `#4ade80` (Green 400) — Reserved for completed tool execution checkmark (`✓`).
  - Danger/Error State: `#dc3545` (Red) — Reserved for cancel action and failed tool execution icon (`✗`).
- **Text:**
  - `--text-primary` (`#e0e0e0`) for main content.
  - `--text-secondary` (`rgba(255, 255, 255, 0.6)`) for tool parameters and JSON keys.

*(Source: Pre-populated from `59-CONTEXT.md` icon color requirements and codebase standard colors).*

## 5. Copywriting
- **Primary CTA:** Cancel action (Red button replacing send button during execution).
- **Textarea Placeholder (Running):** "Agent 正在运行..." / "Agent is running..."
- **Tool Count Badge:** "🛠 已使用 N 个工具" / "🛠 Used N tools"
- **Empty State:** N/A (Tools section remains completely hidden if count is 0).
- **Error State Copy:** Failure shows `✗` next to tool name, with actual failure summary dumped in the result block. Destructive actions prompt skipped for this phase.

*(Source: Explicitly defined in `59-CONTEXT.md` decisions).*

## 6. Interaction & Components Rules
- **Tool Card Default:** Collapsed (Tool name + status icon only).
- **Tool Card Expanded:** Shows Tool parameters (key-value pairs) + Result summary string.
- **Result Truncation:** Result text is truncated to exactly 10 lines maximum using CSS (`line-clamp: 10` or max-height with `overflow-y: auto`).
- **Send Lock:** Textarea becomes `disabled` (opacity 0.5, cursor `not-allowed`). Send button transforms to Cancel (`#dc3545`).
- **Timeout Extension:** Timer resets to exactly `60` seconds on every `ToolCallStarted` and `ToolCallCompleted` event.

*(Source: Explicitly defined in `59-CONTEXT.md` decisions, values hardened to exact numbers by UI spec).*
