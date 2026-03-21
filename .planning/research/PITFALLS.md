# Pitfalls Research

**Domain:** OpenAnima v2.0 Structured Cognition Foundation — long-running graph-native developer agents
**Researched:** 2026-03-20
**Confidence:** HIGH

## Critical Pitfalls

### Pitfall 1: Cyclic graphs never converge

**What goes wrong:**
Once v1.9 allows cycles, a seemingly smart graph becomes a runaway loop: repeated LLM calls, repeated tool scans, and endless fan-out with no clear stop.

**Why it happens:**
Cycles are now legal, but convergence control was explicitly deferred. Without budgets, cooldowns, or stop semantics, "deep thinking" becomes unbounded churn.

**How to avoid:**
- Add run-level step/token/time budgets
- Add explicit completion, abort, and idle conditions
- Track repeated node/edge patterns and dampen or halt them
- Make termination state visible in the UI and stored in the run record

**Warning signs:**
- Same module sequence repeats many times in one run
- Queue depth or step count rises while artifact output does not
- Token usage grows without new findings

**Phase to address:**
Phase 45 (runtime foundation) and Phase 49 (structured cognition workflows)

---

### Pitfall 2: Long-running tasks disappear on refresh or restart

**What goes wrong:**
A user starts a codebase analysis run, refreshes the UI or restarts the app, and the system loses track of what was happening.

**Why it happens:**
Current UI/session state is not a durable run model. `ChatSessionState` and similar in-memory state are not enough for persistent autonomous work.

**How to avoid:**
- Persist runs, steps, and artifacts in SQLite from the start
- Make resume/cancel/recover explicit lifecycle operations
- Use append-only step logging so partial progress survives failures

**Warning signs:**
- Work exists only in memory or logs
- There is no stable run ID visible to the user
- A crash forces the user to start over from scratch

**Phase to address:**
Phase 45 (durable task runtime foundation)

---

### Pitfall 3: Tools act on the wrong workspace

**What goes wrong:**
Search, git, or command steps operate in the wrong repo, wrong directory, or wrong branch, producing misleading analysis or dangerous side effects.

**Why it happens:**
Agents often assume a single ambient working directory. Long-running workflows need an explicit workspace binding, not implicit process state.

**How to avoid:**
- Bind every run to a workspace root
- Include workspace root in every tool request/result
- Default to read-oriented operations first
- Require explicit intent before mutating repo state

**Warning signs:**
- Artifacts reference files outside the intended repo
- Git status/diff results don’t match user expectations
- Tool steps omit working-directory metadata

**Phase to address:**
Phase 46 (workspace tool surface)

---

### Pitfall 4: Memory becomes opaque and untrustworthy

**What goes wrong:**
The system injects "memory" into downstream nodes, but the user cannot tell where it came from or whether it is still valid.

**Why it happens:**
Memory gets treated as hidden prompt state instead of a retrieval layer over artifacts, notes, and summaries with provenance.

**How to avoid:**
- Store memory records with artifact path, step ID, timestamp, and source summary
- Prefer lexical/provenance retrieval before vector-first recall
- Show why a memory item was injected into the run

**Warning signs:**
- Retrieved context has no source link
- Users cannot inspect or delete stale memory items
- Memory injections contradict the workspace state

**Phase to address:**
Phase 48 (memory and artifact retrieval foundation)

---

### Pitfall 5: Observability stops at "running/error"

**What goes wrong:**
A run fails or stalls, but the UI only shows that a node was running or errored. There is no timeline, no input/output context, and no reason trace.

**Why it happens:**
The current runtime status surface is intentionally minimal. Parallel multi-node cognition requires richer telemetry than border colors or logs.

**How to avoid:**
- Record per-step start/end/failure events
- Track queue depth, latency, retries, and routed downstream edges
- Surface inputs, outputs, and artifact links in a run inspector
- Correlate logs and traces by run/step IDs

**Warning signs:**
- Debugging requires reading raw logs only
- Users cannot answer "why did this node run?"
- Failures cannot be tied back to a specific upstream decision

**Phase to address:**
Phase 47 (observability and run inspection)

---

### Pitfall 6: Artifact and event volume explodes

**What goes wrong:**
Runs create too many files, too many step rows, or too much UI detail, slowing the app and burying useful results.

**Why it happens:**
Long-running graph systems naturally produce lots of intermediate state. If everything is stored at full fidelity forever, the product degrades under its own trace data.

**How to avoid:**
- Separate raw events from user-facing artifacts
- Truncate or summarize large step payloads
- Add retention/pruning policies
- Promote only important outputs to durable artifacts

**Warning signs:**
- SQLite file size grows rapidly on a single run
- Run inspector becomes slow with large timelines
- Disk usage spikes faster than user value

**Phase to address:**
Phase 47 (timeline design) and Phase 48 (artifact lifecycle)

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Keep run state in memory only | Fastest initial implementation | No recovery, no resume, weak debugging | Never for v2.0 |
| Store every event payload in full forever | Easy implementation, maximum raw detail | Disk bloat, UI slowdown, hard-to-use history | Only during short-lived dev prototyping |
| Let tool modules inherit ambient CWD | Less plumbing | Wrong-repo execution and nondeterministic behavior | Never |
| Use opaque prompt memory blobs | Faster demo of "memory" | No provenance, hard to debug, stale recall | Only for throwaway experiments, not milestone scope |
| Solve deep thinking with one larger prompt | Low implementation cost | Fails milestone intent and hides reasoning structure | Never |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| `git` | Running read commands without capturing branch/worktree context | Store branch, workspace root, and command output together per step |
| `rg` | Treating CLI availability as guaranteed | Capability-detect at runtime and degrade clearly when unavailable |
| SQLite | Treating it like a remote DB and writing huge blobs casually | Keep metadata in SQLite, large content in files/artifacts |
| OpenTelemetry | Adding package references but no useful span boundaries | Instrument run/step/tool/artifact boundaries explicitly |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Re-scanning full repo every loop | Slow runs, repeated identical findings | Cache scope, track touched paths, reuse prior artifacts | Medium-to-large repos |
| Persisting every raw payload | Database/file growth and sluggish inspection UI | Summarize, truncate, and promote selected artifacts | Long multi-hour runs |
| Re-rendering every event live in Blazor | UI jank, excessive SignalR traffic | Project and batch timeline updates | High-frequency multi-node activity |
| Unlimited parallel tool execution | CPU spikes, command contention, noisy results | Add per-run and per-tool concurrency limits | Multiple active nodes + heavy CLI use |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Feeding repo text directly into shell commands | Command injection or unintended execution | Structured command arguments, no raw shell interpolation |
| Allowing unrestricted path access from user/repo content | Path traversal and sensitive file exposure | Enforce workspace root boundaries and validate paths |
| Treating codebase prompt injection as trustworthy instructions | Agent hijack, poisoned outputs | Separate data from control and mark repo content as untrusted |
| Logging secrets from env/files/tool output | Credential leakage into artifacts or UI | Redact sensitive values before persistence/display |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Agent acts without visible run plan or objective | Feels random and unsafe | Show objective, current phase, and next step in inspector |
| Graph is busy but progress is unclear | Users lose trust quickly | Display artifacts produced, steps completed, and stop reason |
| Memory retrieval is invisible | Users cannot verify context injection | Show retrieved memory items and their sources |
| Failures are only in logs | Non-expert users cannot recover | Surface actionable failure cards in the run UI |

## "Looks Done But Isn't" Checklist

- [ ] **Durable runs:** Often missing crash recovery — verify restart behavior on an in-progress run
- [ ] **Workspace tools:** Often missing explicit workspace root — verify every tool result records where it ran
- [ ] **Memory retrieval:** Often missing provenance — verify every injected memory item links to a source artifact
- [ ] **Observability:** Often missing downstream-route visibility — verify users can answer why a node ran
- [ ] **Convergence control:** Often missing real stop conditions — verify cyclic runs terminate for the right reason
- [ ] **Artifacts:** Often missing retention policy — verify large runs do not create uncontrolled storage growth

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Cyclic non-convergence | MEDIUM | Abort run, inspect repeated step pattern, tighten budgets/cooldowns, replay with limits |
| Lost in-memory run | HIGH | Reconstruct from artifacts/logs if possible, then implement durable run journal before further rollout |
| Wrong-workspace execution | MEDIUM | Invalidate affected artifacts, rebind workspace, rerun tool steps with explicit root |
| Opaque memory injection | MEDIUM | Disable retrieval for affected runs, rebuild memory index from source-linked artifacts |
| Event/artifact explosion | LOW/MEDIUM | Prune old transient data, summarize large payloads, add retention thresholds |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Cyclic graphs never converge | Phase 45 / Phase 49 | Run cyclic workflow and verify bounded completion or explicit halt reason |
| Long-running tasks disappear | Phase 45 | Restart app during a run and verify resume/cancel state persists |
| Tools act on wrong workspace | Phase 46 | Verify every tool step records and respects workspace root |
| Memory becomes opaque | Phase 48 | Verify every retrieval item links to artifact + step provenance |
| Observability stops at running/error | Phase 47 | Verify run inspector shows timeline, route edges, inputs/outputs, and failure reason |
| Artifact/event volume explodes | Phase 47 / Phase 48 | Verify long run remains performant and storage stays bounded |

## Sources

- Direct codebase inspection — current runtime primitives and known gaps
- `.planning/PROJECT.md` and `.planning/STATE.md` — deferred convergence control and current architecture state
- User milestone discussion — long-running tasks, structure-driven cognition, developer orientation
- Official docs referenced in stack research for SQLite and OpenTelemetry behavior

---
*Pitfalls research for: OpenAnima v2.0 Structured Cognition Foundation*
*Researched: 2026-03-20*
