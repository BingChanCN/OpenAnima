---
phase: 48-artifact-memory-foundation
plan: 02
subsystem: database
tags: [sqlite, dapper, aho-corasick, memory-graph, trie, tdd]

# Dependency graph
requires:
  - phase: 48-artifact-memory-foundation-01
    provides: artifacts table in RunDbInitializer (memory tables appended after artifacts)
  - phase: 45-durable-task-runtime-foundation
    provides: RunDbConnectionFactory, RunDbInitializer schema pattern, per-operation connection style
provides:
  - MemoryNode/MemoryEdge/MemorySnapshot immutable records
  - IMemoryGraph interface with CRUD, prefix query, edges, disclosure, snapshots, glossary
  - MemoryGraph SQLite implementation with snapshot-before-write versioning
  - GlossaryIndex Aho-Corasick trie with Build+FindMatches (case-insensitive)
  - DisclosureMatcher case-insensitive substring trigger matching
  - memory_nodes, memory_edges, memory_snapshots tables + indexes in RunDbInitializer
affects:
  - phase 48-03 onwards (MemoryGraph available for injection/use)
  - phase 49 (structured cognition can now read/write memory graph)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Aho-Corasick trie: Build inserts keywords lowercase; BFS computes failure links; FindMatches walks chars with failure fallback, deduplicates by keyword"
    - "Snapshot-before-write: on upsert, old content snapshotted first, then LIMIT 10 prune, then UPDATE"
    - "Per-Anima glossary cache: ConcurrentDictionary<string, GlossaryIndex> invalidated on WriteNodeAsync/DeleteNodeAsync"
    - "Dapper column alias pattern: SELECT col AS PropName to map snake_case DB columns to PascalCase record properties"

key-files:
  created:
    - src/OpenAnima.Core/Memory/MemoryNode.cs
    - src/OpenAnima.Core/Memory/MemoryEdge.cs
    - src/OpenAnima.Core/Memory/MemorySnapshot.cs
    - src/OpenAnima.Core/Memory/IMemoryGraph.cs
    - src/OpenAnima.Core/Memory/GlossaryIndex.cs
    - src/OpenAnima.Core/Memory/DisclosureMatcher.cs
    - src/OpenAnima.Core/Memory/MemoryGraph.cs
    - tests/OpenAnima.Tests/Unit/MemoryGraphTests.cs
    - tests/OpenAnima.Tests/Unit/DisclosureMatcherTests.cs
  modified:
    - src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs

key-decisions:
  - "GlossaryIndex deduplicates FindMatches by keyword (HashSet<string> seen) — prevents duplicate (keyword, uri) pairs when same keyword appears multiple times in content"
  - "MemoryGraph failure-link propagation in GlossaryIndex.Build copies parent's Matches into child — avoids separate output-link traversal in FindMatches inner loop"
  - "DeleteNodeAsync uses single connection for all 3 DELETEs (edges, snapshots, node) — atomicity for cascade without explicit transaction (SQLite autocommit per statement is acceptable here)"
  - "DisclosureMatcher.Match is a static method — no state needed, simplest API for callers"

patterns-established:
  - "Memory namespace under OpenAnima.Core.Memory — all memory graph types live here"
  - "TDD with in-memory SQLite: keepalive + isRaw factory + RunDbInitializer per test class"

requirements-completed:
  - MEM-01

# Metrics
duration: 8min
completed: 2026-03-21
---

# Phase 48 Plan 02: Memory Graph Persistence Layer Summary

**URI-keyed SQLite memory graph with Aho-Corasick glossary trie, disclosure trigger matching, and snapshot versioning (max 10 per node)**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-21T11:58:08Z
- **Completed:** 2026-03-21T12:06:03Z
- **Tasks:** 2
- **Files modified:** 10 (7 created in Memory/, 2 test files, 1 RunDbInitializer modified)

## Accomplishments

- Memory graph data layer: MemoryNode/MemoryEdge/MemorySnapshot immutable records with full provenance fields (SourceArtifactId, SourceStepId)
- IMemoryGraph interface covering CRUD, URI prefix queries, typed edges, disclosure nodes, snapshot history, and synchronous glossary matching
- MemoryGraph SQLite implementation with snapshot-before-write (old content snapshotted on every update, pruned to last 10 per URI)
- GlossaryIndex implements Aho-Corasick trie with proper failure links and output propagation for linear-time multi-keyword scanning
- DisclosureMatcher static method returns nodes whose DisclosureTrigger is a case-insensitive substring of the context
- 16 unit tests pass across 9 MemoryGraphTests and 7 DisclosureMatcherTests

## Task Commits

Each task was committed atomically:

1. **Task 1: Memory record types, IMemoryGraph, GlossaryIndex, DisclosureMatcher, DB schema** - `d28b76f` (feat)
2. **Task 2: MemoryGraph SQLite implementation + unit tests** - `718c2b9` (feat)

_Note: TDD tasks were RED (compile error on missing MemoryGraph) then GREEN (all 16 tests pass)._

## Files Created/Modified

- `src/OpenAnima.Core/Memory/MemoryNode.cs` - Immutable record: Uri+AnimaId PK, Content, DisclosureTrigger, Keywords, provenance links, timestamps
- `src/OpenAnima.Core/Memory/MemoryEdge.cs` - Immutable record: typed directed edge between URI pairs with Label
- `src/OpenAnima.Core/Memory/MemorySnapshot.cs` - Immutable record: version snapshot with SnapshotAt timestamp
- `src/OpenAnima.Core/Memory/IMemoryGraph.cs` - Interface: WriteNodeAsync, GetNodeAsync, QueryByPrefixAsync, GetAllNodesAsync, DeleteNodeAsync, AddEdgeAsync, GetEdgesAsync, GetDisclosureNodesAsync, GetSnapshotsAsync, RebuildGlossaryAsync, FindGlossaryMatches
- `src/OpenAnima.Core/Memory/GlossaryIndex.cs` - Aho-Corasick trie: TrieNode with Children/Failure/Matches, Build (BFS failure links), FindMatches (linear scan with deduplication)
- `src/OpenAnima.Core/Memory/DisclosureMatcher.cs` - Static Match with OrdinalIgnoreCase Contains check
- `src/OpenAnima.Core/Memory/MemoryGraph.cs` - SQLite-backed implementation: per-operation connections, ConcurrentDictionary glossary cache, snapshot pruning with LIMIT 10 subquery
- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs` - Added memory_nodes, memory_edges, memory_snapshots tables and 3 indexes
- `tests/OpenAnima.Tests/Unit/MemoryGraphTests.cs` - 9 tests covering write/read, snapshot creation, snapshot pruning to 10, prefix query, cascade delete, edges, disclosure nodes, glossary rebuild+match
- `tests/OpenAnima.Tests/Unit/DisclosureMatcherTests.cs` - 7 tests: 4 for DisclosureMatcher (substring, case-insensitive, no match, null trigger), 3 for GlossaryIndex (multi-keyword, empty content, case-insensitive)

## Decisions Made

- GlossaryIndex.FindMatches deduplicates by keyword via `HashSet<string>` — prevents duplicate results when same keyword appears multiple times in content
- Aho-Corasick Build propagates parent's Matches into child nodes during BFS — allows FindMatches inner loop to collect all matches without separate output-link traversal
- DeleteNodeAsync performs 3 sequential DELETEs (edges, snapshots, node) in a single connection — SQLite autocommit makes each atomic; explicit transaction not required for this cascade
- DisclosureMatcher.Match is static — callers pass nodes and context, no instance state needed, simplest API

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - all tasks completed on first attempt.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Memory graph CRUD fully operational with snapshot versioning
- IMemoryGraph ready for DI registration and injection into Anima runtime
- GlossaryIndex and DisclosureMatcher ready for use in prompt injection pipeline (Phase 49)
- RunDbInitializer extended with memory tables — existing databases will auto-migrate on next startup

## Self-Check: PASSED

- Verified all created files exist on disk
- Verified task commits `d28b76f` and `718c2b9` exist in git history

---
*Phase: 48-artifact-memory-foundation*
*Completed: 2026-03-21*
