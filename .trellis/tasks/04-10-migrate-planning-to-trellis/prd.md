# brainstorm: migrate planning to trellis

## Goal

Migrate the project from the legacy `.planning/` workflow to the existing `.trellis/` workflow so the canonical planning and execution flow lives in one system instead of two parallel systems.

## What I already know

* The repository currently has both `.planning/` and `.trellis/`.
* `.planning/` still holds the project's canonical product/project documents such as `PROJECT.md`, `REQUIREMENTS.md`, `ROADMAP.md`, `STATE.md`, and `MILESTONES.md`.
* `README.md` still points contributors to `.planning/` as the main documentation entry point.
* `.trellis/` already provides task management, workspace journals, spec documents, and session workflow scripts.
* The current `.planning/` tree also contains a large amount of historical milestone/phase/quick-plan artifacts that may need preservation even if the active workflow moves to Trellis.

## Assumptions (temporary)

* The intent is not just to add new docs, but to make Trellis the primary workflow.
* Historical `.planning/` artifacts should probably be preserved rather than deleted outright.
* The migration may require both documentation updates and some Trellis structure/extensions so current `.planning/` information has a clear home.

## Open Questions

* Should the migration include only active/canonical planning docs, or also reorganize/archive historical `.planning/` artifacts?

## Requirements (evolving)

* Define the target ownership split between `.trellis/` and `.planning/`.
* Move or remap the active planning entry points so contributors can use Trellis as the canonical workflow.
* Preserve access to historical planning records.
* Update repo-facing documentation to point to the new canonical locations.

## Acceptance Criteria (evolving)

* [ ] There is a clear canonical home in `.trellis/` for active project planning information.
* [ ] `README.md` and other top-level contributor entry points no longer direct users to `.planning/` as the primary workflow.
* [ ] Historical `.planning/` artifacts are either preserved in place with clear status or relocated with clear references.
* [ ] The final structure avoids ambiguous duplication between active `.planning/` and `.trellis/`.

## Definition of Done (team quality bar)

* Tests added/updated where appropriate
* Lint / typecheck / CI green where relevant
* Docs/notes updated if behavior changes
* Rollout/rollback considered if risky

## Out of Scope (explicit)

* Rewriting the historical content of every old planning artifact unless needed for migration clarity
* Changing unrelated runtime/product behavior

## Technical Notes

* Active `.planning/` top-level documents:
  * `.planning/PROJECT.md`
  * `.planning/REQUIREMENTS.md`
  * `.planning/ROADMAP.md`
  * `.planning/STATE.md`
  * `.planning/MILESTONES.md`
* Trellis already contains:
  * `.trellis/workflow.md`
  * `.trellis/spec/*`
  * `.trellis/tasks/*`
  * `.trellis/workspace/*`
* `README.md:88-106` still documents `.planning/` as the repository planning entry point.
