---
phase: 12-wiring-engine-execution-orchestration
plan: 01
subsystem: wiring-engine
tags: [graph-algorithms, topological-sort, cycle-detection, tdd]
dependency_graph:
  requires: []
  provides: [ConnectionGraph, Kahn-algorithm, level-parallel-execution]
  affects: [WiringEngine]
tech_stack:
  added: []
  patterns: [Kahn-algorithm, BFS-traversal, in-degree-tracking]
key_files:
  created:
    - src/OpenAnima.Core/Wiring/ConnectionGraph.cs
    - tests/OpenAnima.Tests/Unit/ConnectionGraphTests.cs
  modified: []
decisions:
  - "Used Kahn's algorithm (BFS-based) for topological sort over DFS approach for clearer level grouping"
  - "Cycle detection integrated into GetExecutionLevels via incomplete processing check"
  - "HasCycle wraps GetExecutionLevels in try-catch for non-throwing cycle detection"
  - "Dictionary<string, HashSet<string>> for adjacency list enables O(1) edge lookups"
metrics:
  duration_seconds: 657
  tasks_completed: 1
  tests_added: 12
  tests_passing: 12
  files_created: 2
  lines_added: 248
  commits: 2
completed_date: "2026-02-26"
---

# Phase 12 Plan 01: ConnectionGraph with Topological Sort Summary

**One-liner:** Kahn's algorithm implementation for level-parallel topological sort with integrated cycle detection using BFS and in-degree tracking.

## What Was Built

Implemented ConnectionGraph, the core graph algorithm class that determines module execution order and prevents circular dependencies. This is the foundation for WIRE-01 (execution orchestration) and WIRE-02 (cycle detection).

**Core algorithm:** Kahn's algorithm with level-based BFS traversal
- In-degree tracking for zero-dependency node identification
- Level grouping: nodes at same topological level collected for parallel execution
- Cycle detection: if processedNodes != totalNodes after traversal, cycle exists

**Public API:**
- `AddNode(string)` - Register node without connections (idempotent)
- `AddConnection(string, string)` - Add directed edge (auto-registers nodes)
- `RemoveNode(string)` - Remove node and all its connections
- `GetExecutionLevels()` - Returns List<List<string>> of execution levels (throws on cycle)
- `HasCycle()` - Non-throwing boolean cycle check
- `GetNodeCount()` - Total nodes in graph

## Test Coverage

**12 test cases covering:**
- Empty graph handling
- Single node isolation
- Linear chains (A→B→C)
- Diamond patterns (A→B,C→D with B,C in same level)
- Fan-out patterns (A→B,C,D)
- Circular dependency detection (A→B→C→A)
- Self-loop rejection (A→A)
- Multiple disconnected subgraphs
- Node removal with connection cleanup
- Cycle detection (throwing and non-throwing)

**All tests passing:** 12/12 ✓

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed test expectations for disconnected graphs**
- **Found during:** GREEN phase test execution
- **Issue:** Test expected 3 levels for A→B, C→D, E graph but correct answer is 2 levels (roots in level 0, children in level 1)
- **Fix:** Corrected MultipleDisconnectedSubgraphs test to expect 2 levels with proper assertions for level 1 contents
- **Files modified:** tests/OpenAnima.Tests/Unit/ConnectionGraphTests.cs
- **Commit:** 90b34ac (included in GREEN phase commit)

**2. [Rule 1 - Bug] Fixed test expectations for RemoveNode**
- **Found during:** GREEN phase test execution
- **Issue:** Test expected 2 levels after removing middle node from A→B→C, but correct answer is 1 level (both A and C become isolated roots)
- **Fix:** Corrected RemoveNode test to expect 1 level with 2 nodes in level 0
- **Files modified:** tests/OpenAnima.Tests/Unit/ConnectionGraphTests.cs
- **Commit:** 90b34ac (included in GREEN phase commit)

## Technical Decisions

**Why Kahn's algorithm over DFS?**
- BFS naturally produces level groupings for parallel execution
- In-degree tracking makes zero-dependency identification explicit
- Cycle detection is a natural byproduct (incomplete processing)
- Clearer mental model for execution orchestration

**Why Dictionary<string, HashSet<string>> for adjacency list?**
- O(1) edge existence checks
- O(1) edge addition (HashSet.Add returns false if exists)
- Automatic duplicate edge prevention
- Efficient neighbor iteration

**Why wrap GetExecutionLevels for HasCycle?**
- Avoids code duplication
- Cycle detection logic stays in one place
- Performance acceptable (cycle check is infrequent)
- Simpler maintenance

## Integration Points

**Consumed by (future plans):**
- Plan 12-02: WiringEngine will use GetExecutionLevels() for module execution ordering
- Plan 12-03: Connection validation will use HasCycle() for user feedback

**Dependencies:**
- None - pure graph algorithm with no external dependencies
- Uses only .NET 8.0 built-ins (Dictionary, HashSet, List, Queue)

## Verification

```bash
dotnet test tests/OpenAnima.Tests/ --filter "FullyQualifiedName~ConnectionGraph" -v normal
# Result: Test Run Successful. Total tests: 12, Passed: 12

dotnet build src/OpenAnima.Core/ --no-restore
# Result: Build succeeded. 0 Warning(s) 0 Error(s)
```

## Commits

| Commit | Type | Description |
|--------|------|-------------|
| 0bbddeb | test | Add failing tests for ConnectionGraph (RED phase) |
| 90b34ac | feat | Implement ConnectionGraph with Kahn's algorithm (GREEN phase) |

## Self-Check

Verifying created files exist:

```bash
[ -f "src/OpenAnima.Core/Wiring/ConnectionGraph.cs" ]
# FOUND: src/OpenAnima.Core/Wiring/ConnectionGraph.cs

[ -f "tests/OpenAnima.Tests/Unit/ConnectionGraphTests.cs" ]
# FOUND: tests/OpenAnima.Tests/Unit/ConnectionGraphTests.cs
```

Verifying commits exist:

```bash
git log --oneline --all | grep "0bbddeb"
# FOUND: 0bbddeb

git log --oneline --all | grep "90b34ac"
# FOUND: 90b34ac
```

## Self-Check: PASSED

All files created and all commits exist in git history.
