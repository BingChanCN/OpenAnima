# Parallel Agent Thinking Guide

> **Purpose**: Decide when to split work across multiple agents and when to keep execution in one thread.

---

## The Problem

Parallel agents can reduce wall-clock time, but only when the work is truly independent.

The failure mode is predictable:

- two agents edit the same files or the same logical boundary
- one agent blocks on another agent's unresolved output
- integration is deferred until conflicts become expensive

Use parallel agents to speed up execution, not to create merge debt.

---

## Use Parallel Agents When

- the task can be split into 2+ bounded subtasks with no immediate dependency between them
- each subtask has a clear owner and a disjoint write scope
- one or more subtasks are read-only research, inspection, or verification
- the interface between subtasks is already fixed enough that agents do not need to negotiate it live
- the main thread can keep moving on integration or the critical path while delegated work runs

Typical good fits:

- one agent inspects existing docs while another inspects implementation files
- one agent writes tests for a settled contract while another updates the implementation in different files
- one agent handles a frontend slice while another handles a backend slice after the API contract is fixed

---

## Keep Work in One Agent When

- the next step is blocked on a single unresolved result
- multiple subtasks need to edit the same file or the same feature boundary
- the contract is still changing and downstream work would likely be redone
- the task is small enough that delegation overhead outweighs the benefit
- the work is exploratory debugging where the hypothesis is not stable yet

---

## Ownership Rules

- Assign one owner per write set.
- Do not let multiple agents edit the same file unless one pass is explicitly sequential.
- Keep final integration in the main thread.
- For repo-changing parallel execution, prefer Trellis worktree-backed agents so each agent runs in an isolated checkout.

Trellis references:

- `python3 ./.trellis/scripts/task.py set-branch <task-dir> <branch-name>`
- `python3 ./.trellis/scripts/multi_agent/start.py <task-dir>`
- `.trellis/worktree.yaml` controls per-worktree copied files and setup hooks

The current Trellis multi-agent flow creates a dedicated worktree, copies the task directory into that worktree, writes `.trellis/.current-task`, and registers the running agent. Treat that isolation as a tool for independent work, not a substitute for task decomposition discipline.

---

## Pre-Spawn Checklist

Before spawning more agents, verify:

- [ ] each subtask can make progress without waiting on another unfinished subtask
- [ ] each subtask has a disjoint file/module ownership boundary
- [ ] shared contracts or schemas are already fixed
- [ ] there is a clear integration owner
- [ ] there is a verification plan after parallel work completes

---

## Wrong vs Correct

### Wrong

- spawn two agents to edit the same service and its tests at the same time without a fixed contract
- delegate the only blocking investigation and then wait idle for the result
- split work by "whatever looks easy" instead of by ownership boundary

### Correct

- keep contract design local, then split implementation by disjoint ownership
- run read-only exploration or review in parallel with local implementation
- use separate worktrees for independent repo-changing tasks and integrate centrally

---

## Related

- [Code Reuse Thinking Guide](./code-reuse-thinking-guide.md)
- [Cross-Layer Thinking Guide](./cross-layer-thinking-guide.md)
