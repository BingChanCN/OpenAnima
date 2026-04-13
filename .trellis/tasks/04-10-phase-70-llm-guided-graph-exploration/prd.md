# Phase 70: LLM-Guided Graph Exploration

## Goal

Extend memory recall with an optional LLM-guided graph exploration pass that can traverse relevant branches more intelligently than the current disclosure and glossary matching alone.

## Requirements

- Add graph exploration as an optional fourth recall pass after Boot, Disclosure, and Glossary.
- Let the exploration flow start from root or top-level nodes and choose relevant branches via a secondary LLM.
- Explore selected branches in parallel with a configurable concurrency cap.
- Keep exploration depth dynamic but hard-limited.
- Add a dedicated model selection surface for exploration in Settings.
- Keep the feature opt-in per Anima and safe against cyclic graphs, cost explosion, and hallucinated URI selections.

## Acceptance Criteria

- [ ] Recall pipeline supports a disabled-by-default graph exploration pass after the existing three passes.
- [ ] Exploration chooses only from validated candidate URIs and rejects hallucinated branches.
- [ ] Cyclic graphs cannot loop forever because traversal tracks visited nodes across depth levels.
- [ ] Per-level candidate caps and concurrency caps are enforced.
- [ ] Users can configure a dedicated exploration model separately from the main chat model.
- [ ] The final implementation preserves current recall behavior when exploration is disabled.

## Technical Notes

- Source milestone: `.trellis/planning/current-milestone.md`
- Migrated from legacy planning on `2026-04-10`
- Primary legacy references:
  - `.planning/ROADMAP.md` Phase 70 section
  - `.planning/REQUIREMENTS.md` `MEMR-01` to `MEMR-09`
- Likely implementation areas:
  - `src/OpenAnima.Core/Memory/MemoryRecallService.cs`
  - `src/OpenAnima.Core/Modules/LLMModule.cs`
  - `src/OpenAnima.Core/Components/Pages/Settings.razor`
  - `src/OpenAnima.Core/Providers/LLMProviderRegistryService.cs`
- Phase ordering note: do not start implementation until Phase 69 execution ownership and replay behavior are stable.
