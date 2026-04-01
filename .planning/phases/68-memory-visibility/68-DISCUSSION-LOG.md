# Phase 68: Memory Visibility - Discussion Log

> **Audit trail only.** Do not use this file as input to planning, research, or execution agents.
> Decisions are captured in `68-CONTEXT.md`; this log preserves the alternatives considered and the user's selections.

**Date:** 2026-04-01
**Language:** Chinese
**Mode:** Interactive discuss-phase, one area at a time

## Boundary Presented

- Phase 68 only covers memory-operation visibility inside chat.
- In scope: explicit `memory_create` / `memory_update` / `memory_delete` cards, one sedimentation summary chip, visual distinction for memory cards.
- Out of scope: recall changes, new memory abilities, `/memory` page work, and navigation-away background execution from Phase 69.

## Process Note

- The user explicitly rejected a batched multi-decision prompt: "一个一个来讨论，不要一次性丢给我".
- Discussion was then conducted one gray area at a time.

## Discussion Areas

### 1. Explicit memory card structure

**Question 1:** What structural direction should explicit memory cards take?

Options presented:
- **A.** Reuse the existing tool-card skeleton
- **B.** Build a dedicated memory-card model
- **C.** Use an even more minimal card with most detail hidden

**User answer:** `A`

**Captured decision:**
- Explicit memory cards will reuse the existing chat tool-card skeleton rather than introducing a second bespoke model.

**Follow-up Question 1A:** What should the folded state show?

Options presented:
- **A.** Operation + URI
- **B.** Operation + URI + one-line content summary
- **C.** Natural-language-only summary

**User answer:** `B`

**Captured decision:**
- Folded state shows operation + URI + one-line content summary.

### 2. Sedimentation summary chip density

**Question:** How much information should the background sedimentation summary chip show?

Discussion path:
- The assistant recommended the quietest version because the requirement says "single collapsed summary chip" and explicit memory tools already have separate cards.
- Recommended option: count-only summary, no create/update split, no URI detail.

**User question back:** `你推荐哪种`

**Recommendation given:** Count-only summary chip.

**User answer:** `同意`

**Captured decision:**
- Sedimentation visibility is a single collapsed summary chip showing only the total count of sedimented memories.

### 3. Visual differentiation strength

**Question:** How strong should the visual distinction between memory cards and generic tool cards be?

Recommendation given:
- Medium-strength differentiation: dedicated iconography and memory-specific accent/border/background treatment, but not a loud full-block visual that competes with the assistant text.

**User answer:** `同意`

**Captured decision:**
- Memory cards will use medium-strength differentiation: noticeable at a glance, but not visually dominant.

### 4. Persistence and replay

**Question:** Should memory visibility records remain in chat history after reload/restart?

Recommendation given:
- Persist both explicit memory cards and the sedimentation summary chip so replay preserves the user-visible explanation of what happened during that assistant response.

**User answer:** `保留`

**Captured decision:**
- Explicit memory cards and the sedimentation summary chip both persist with chat history and remain attached to the original assistant message on replay.

## Final Locked Decisions

- Reuse the existing tool-card skeleton for explicit memory cards.
- Folded memory cards show operation + URI + one-line content summary.
- Sedimentation uses a single collapsed count-only summary chip.
- Memory cards use medium-strength visual differentiation under `ToolCategory.Memory`.
- Both explicit memory cards and sedimentation summary chips persist with chat history.

## Deferred / Not Chosen

- Dedicated memory-only card model.
- Sedimentation breakdown by create/update counts.
- Sedimentation URI list or per-node cards.
- Overly strong visual treatment that would overpower assistant text.
- Transient-only visibility that disappears after reload.

---

*Phase: 68-memory-visibility*
*Discussion logged: 2026-04-01*
