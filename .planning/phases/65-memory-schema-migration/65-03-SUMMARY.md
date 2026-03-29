---
phase: 65-memory-schema-migration
plan: "03"
subsystem: memory
tags: [memory, schema-migration, sqlite, interface, tests]
dependency_graph:
  requires: [65-02]
  provides: [memory-graph-four-table-impl, imemory-graph-updated-interface]
  affects: [MemoryNodeCard, SedimentationService, MemoryRecallService, all IMemoryGraph consumers]
tech_stack:
  added: []
  patterns: [UUID-based node identity, content versioning via memory_contents, URI routing via memory_uri_paths]
key_files:
  created: []
  modified:
    - src/OpenAnima.Core/Memory/IMemoryGraph.cs
    - src/OpenAnima.Core/Memory/MemoryGraph.cs
    - src/OpenAnima.Core/Components/Shared/MemoryNodeCard.razor
    - tests/OpenAnima.Tests/Unit/MemoryGraphTests.cs
    - tests/OpenAnima.Tests/Unit/MemoryRecallServiceTests.cs
    - tests/OpenAnima.Tests/Unit/SedimentationServiceTests.cs
  deleted:
    - src/OpenAnima.Core/Memory/MemorySnapshot.cs
decisions:
  - "GetContentHistoryAsync returns IReadOnlyList<MemoryContent> ordered DESC (newest first)"
  - "AddEdgeAsync silently returns if source or target node UUID cannot be resolved from URI"
  - "GetDisclosureNodesAsync uses INNER JOIN on memory_contents (not LEFT JOIN) to filter nodes with non-null disclosure_trigger in latest content"
  - "Edge tests updated to pre-write both nodes before AddEdgeAsync so UUID resolution succeeds"
metrics:
  duration: "~7 minutes"
  completed: "2026-03-26"
  tasks: 4
  files: 7
---

# Phase 65 Plan 03: MemoryGraph Four-Table Schema Rewrite Summary

**One-liner:** MemoryGraph.cs fully rewritten for UUID/four-table schema with content versioning, URI path routing, and all consumers and tests updated.

## What Was Built

The memory graph implementation was rewritten from a 3-table model (memory_nodes / memory_edges / memory_snapshots) to the new 4-table model:
- `memory_nodes` - UUID primary key, stable node identity
- `memory_contents` - versioned content rows (newest = MAX(id))
- `memory_edges` - UUID references for parent/child, with priority/weight/bidirectional fields
- `memory_uri_paths` - URI-to-UUID routing layer for backward compatible URI-based API

## Tasks Completed

| Task | Description | Commit | Files |
|------|-------------|--------|-------|
| 1 | Update IMemoryGraph interface | c72a55a | IMemoryGraph.cs |
| 2 | Rewrite MemoryGraph.cs for four-table schema | 96387ff | MemoryGraph.cs |
| 3 | Delete MemorySnapshot.cs, update MemoryNodeCard.razor | 5994f42 | MemorySnapshot.cs (deleted), MemoryNodeCard.razor |
| 4 | Update unit tests for new schema | 93b3909 | MemoryGraphTests.cs, MemoryRecallServiceTests.cs, SedimentationServiceTests.cs |

## Key Design Patterns Implemented

**URI resolution**: All URI-based methods resolve URI -> UUID via `memory_uri_paths` before operating on nodes. AddEdgeAsync silently returns if either node UUID cannot be resolved.

**Content versioning**: WriteNodeAsync always appends a new `memory_contents` row. Old content is never modified. Latest content is resolved via `MAX(id)` subquery. 10-version pruning applied per node per write.

**UUID generation**: New nodes generate `Guid.NewGuid().ToString("D")` if `node.Uuid` is empty. NodeType is inferred from URI prefix (core:// -> System, sediment://fact/ -> Fact, etc.) if not explicitly set.

**Backward compatibility**: All URI-based interface methods (GetNodeAsync, GetEdgesAsync, etc.) continue working unchanged. Consumers need zero modification.

## Deviations from Plan

**Auto-fixed: Edge tests require both nodes to exist**

During Task 4 test updates, the existing `AddEdgeAsync_CanBeRetrieved` test only wrote nodeA before adding an edge. In the new schema, AddEdgeAsync resolves UUIDs from URIs, so both nodes must exist in `memory_uri_paths`. The test was updated to write both nodes first.

Similarly, `DeleteNodeAsync_RemovesNodeEdgesAndContentHistory` was updated to write nodeB ("core://agent/identity") before adding the edge, and `GetIncomingEdgesAsync_ReturnsEdgesPointingToUri` was updated to write all 4 nodes before adding edges.

**Auto-added: UUID generation and GetNodeByUuidAsync tests**

Added `WriteNodeAsync_NewNode_GeneratesUuid` and `GetNodeByUuidAsync_ExistingNode_ReturnsNode` / `GetNodeByUuidAsync_NonExistent_ReturnsNull` tests to cover the new UUID-based functionality explicitly. These cover MEMA-01 (stable UUID identity).

## Verification Results

- `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj` -- 0 errors, 0 warnings
- `dotnet test --filter "MemoryGraphTests|MemoryRecallServiceTests|SedimentationServiceTests"` -- 39/39 passed
- `dotnet test tests/OpenAnima.Tests` -- 662/662 passed (no regressions)
- MemorySnapshot.cs: confirmed deleted
- IMemoryGraph contains GetContentHistoryAsync and GetNodeByUuidAsync
- IMemoryGraph does NOT contain GetSnapshotsAsync or MemorySnapshot
- MemoryGraph.WriteNodeAsync generates UUID (Guid.NewGuid) for new nodes
- Content updates create new memory_contents rows
- Edge operations resolve URIs to UUIDs via memory_uri_paths

## Self-Check: PASSED

All required files exist, MemorySnapshot.cs confirmed deleted, all 4 task commits verified present.
