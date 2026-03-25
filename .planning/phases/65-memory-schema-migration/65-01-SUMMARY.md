---
phase: 65-memory-schema-migration
plan: 01
subsystem: database
tags: [sqlite, memory-graph, schema, records, csharp]

requires: []
provides:
  - MemoryNode record with Uuid, NodeType, DisplayName properties
  - MemoryContent record as versioned content entity (replaces MemorySnapshot)
  - MemoryEdge record with ParentUuid, ChildUuid, Priority, Weight, Bidirectional, DisclosureTrigger
  - MemoryUriPath record for URI-to-node routing
  - RunDbConnectionFactory hardened with Busy Timeout=5000
affects: [65-02, 65-03, memory-persistence, sedimentation]

tech-stack:
  added: []
  patterns:
    - "UUID primary keys on memory records alongside legacy URI keys for backward compatibility"
    - "Versioned content pattern: auto-increment Id, highest Id is current version"
    - "URI routing decoupled from node identity via MemoryUriPath join table"

key-files:
  created:
    - src/OpenAnima.Core/Memory/MemoryContent.cs
    - src/OpenAnima.Core/Memory/MemoryUriPath.cs
  modified:
    - src/OpenAnima.Core/Memory/MemoryNode.cs
    - src/OpenAnima.Core/Memory/MemoryEdge.cs
    - src/OpenAnima.Core/RunPersistence/RunDbConnectionFactory.cs

key-decisions:
  - "All new properties on existing records use defaults so legacy construction sites compile without changes"
  - "FromUri/ToUri kept on MemoryEdge for backward compat; will be populated by SQL JOIN in Plan 03"
  - "Busy Timeout only on production constructor; raw/test constructor unchanged so test connection strings are not altered"

patterns-established:
  - "UUID fields default to string.Empty; migration will populate real UUIDs"
  - "NodeType defaults to Fact; inferred from URI prefix during migration"

requirements-completed: [MEMA-01, MEMA-02, MEMA-03, MEMA-04, MEMA-07, PERS-04]

duration: 4min
completed: 2026-03-25
---

# Phase 65 Plan 01: Memory Schema Migration - Data Model Records Summary

**Four C# record types established for four-table memory schema (nodes/contents/edges/uri_paths) with UUID keys and SQLite Busy Timeout hardening**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-03-25T16:48:44Z
- **Completed:** 2026-03-25T16:52:44Z
- **Tasks:** 5
- **Files modified:** 5 (2 created, 3 updated)

## Accomplishments
- MemoryNode gains Uuid, NodeType ("Fact" default), DisplayName — all existing properties preserved with defaults
- MemoryContent created as versioned content entity replacing MemorySnapshot, referenced by NodeUuid
- MemoryEdge gains ParentUuid, ChildUuid, Priority (int), Weight (double 1.0), Bidirectional, DisclosureTrigger — FromUri/ToUri kept for backward compat
- MemoryUriPath created for URI-to-node routing, decoupling URI identity from node UUID identity
- RunDbConnectionFactory production constructor hardened with Busy Timeout=5000; raw test constructor unchanged

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Uuid, NodeType, DisplayName to MemoryNode** - `a5333a2` (feat)
2. **Task 2: Create MemoryContent record** - `9948b49` (feat)
3. **Task 3: Update MemoryEdge with UUID references and metadata** - `7e2ae64` (feat)
4. **Task 4: Create MemoryUriPath record** - `f326f71` (feat)
5. **Task 5: Add Busy Timeout=5000 to RunDbConnectionFactory** - `0047ad5` (feat)

## Files Created/Modified
- `src/OpenAnima.Core/Memory/MemoryNode.cs` - Added Uuid (first), NodeType, DisplayName; all existing properties kept
- `src/OpenAnima.Core/Memory/MemoryContent.cs` - New versioned content record; NodeUuid FK, full metadata fields
- `src/OpenAnima.Core/Memory/MemoryEdge.cs` - Added ParentUuid, ChildUuid, Priority, Weight, Bidirectional, DisclosureTrigger
- `src/OpenAnima.Core/Memory/MemoryUriPath.cs` - New URI routing record; maps URI to NodeUuid
- `src/OpenAnima.Core/RunPersistence/RunDbConnectionFactory.cs` - Production connection string now includes Busy Timeout=5000

## Decisions Made
- All new record properties have defaults (string.Empty / "Fact" / 1.0 / false) so existing construction sites remain valid without modification
- FromUri/ToUri kept on MemoryEdge for backward compatibility; Plan 03 will populate via SQL JOIN during MemoryGraph rewrite
- Busy Timeout applied only to the `string dbPath` constructor; the `string connectionString, bool isRaw` overload is untouched

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All four record types compile in OpenAnima.Core.Memory namespace
- Tests build with 0 errors (additive changes, default values preserve all callers)
- Plan 02 (SQLite schema migration SQL) can reference these record shapes directly
- Plan 03 (MemoryGraph rewrite) can use Uuid, NodeUuid, ParentUuid, ChildUuid for new JOIN-based queries

---
*Phase: 65-memory-schema-migration*
*Completed: 2026-03-25*

## Self-Check: PASSED

All files confirmed on disk. All 5 task commits verified in git log:
- a5333a2 MemoryNode.cs
- 9948b49 MemoryContent.cs
- 7e2ae64 MemoryEdge.cs
- f326f71 MemoryUriPath.cs
- 0047ad5 RunDbConnectionFactory.cs
