---
phase: 65-memory-schema-migration
plan: 02
subsystem: database
tags: [sqlite, dapper, migration, memory-graph, schema]

requires:
  - phase: 65-01
    provides: "MemoryNode/MemoryEdge/MemoryContent/MemoryUriPath records with new field shapes"

provides:
  - "RunDbInitializer creates four-table memory schema (memory_nodes/contents/edges/uri_paths) on fresh install"
  - "MigrateToFourTableModelAsync: atomic transaction migration from old 3-table to new 4-table model with .db.bak-{ts} backup"
  - "Node type inference from URI prefix; display_name from URI last segment"
  - "MemoryMigrationTests: two integration tests verifying data integrity and fresh-install path"

affects: [65-03, memory-graph, sedimentation, memory-recall, memory-tools]

tech-stack:
  added: []
  patterns:
    - "Run MigrateToFourTableModelAsync BEFORE SchemaScript so new indexes (parent_uuid etc.) apply after migration"
    - "Insert snapshot rows first (lower IDs) then current content (highest ID) so ORDER BY id DESC LIMIT 1 returns latest"
    - "Explicit AS column aliases on Dapper SELECT queries to guarantee property mapping without convention reliance"
    - "Optional ILogger<T> injected via factory lambda in DI registration for runtime diagnostics"

key-files:
  created:
    - tests/OpenAnima.Tests/Integration/MemoryMigrationTests.cs
  modified:
    - src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs
    - src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs
    - src/OpenAnima.Core/Components/Shared/MemoryNodeCard.razor

key-decisions:
  - "Migration must execute before SchemaScript: new table indexes reference parent_uuid which fails against old table"
  - "Two-pass content insert: snapshots first (older/lower IDs), current content second (highest ID = latest version)"
  - "Explicit AS aliases in migration SELECT queries to bypass Dapper snake_case convention uncertainty"

patterns-established:
  - "MigrateToFourTableModelAsync: detect via pragma_table_info, backup file, atomic transaction, rollback on failure"
  - "EnsureCreatedAsync ordering: migrate first, then SchemaScript, then column migrations"

requirements-completed: [MEMA-01, MEMA-05, MEMA-06]

duration: 11min
completed: 2026-03-26
---

# Phase 65 Plan 02: RunDbInitializer Four-Table Schema Migration Summary

**SQLite schema updated and atomic data migration implemented: old 3-table memory model (memory_nodes/edges/snapshots) migrates to new 4-table model (memory_nodes/contents/edges/uri_paths) with UUID PKs, file backup, and rollback on failure**

## Performance

- **Duration:** 11 min
- **Started:** 2026-03-25T16:57:06Z
- **Completed:** 2026-03-25T17:08:27Z
- **Tasks:** 4
- **Files modified:** 4

## Accomplishments

- `SchemaScript` updated to four-table memory model: `memory_nodes` with UUID PK, `memory_contents` for versioned content, `memory_edges` with `parent_uuid/child_uuid`, `memory_uri_paths` for URI routing
- `MigrateToFourTableModelAsync` reads all old nodes/edges/snapshots, drops old tables atomically inside BEGIN/COMMIT, recreates new tables, and inserts migrated data — with `.db.bak-{timestamp}` backup and full ROLLBACK on failure
- Node type inferred from URI prefix (`core://` -> System, `sediment://fact/` -> Fact, etc.); display_name extracted from URI last segment
- `MemoryMigrationTests` with two tests: `MigratePreservesAllData` (4 nodes, 6 content rows, 2 edges, 4 URI paths) and `FreshInstallCreatesNewSchemaDirectly` — both pass

## Task Commits

1. **Task 1: Update SchemaScript for new four-table memory model** - `62eb0a0` (feat)
2. **Task 2: Implement MigrateToFourTableModelAsync migration logic** - `6585692` (feat)
3. **Task 3: Update RunDbInitializer DI registration to include ILogger** - `a16e2cf` (feat)
4. **Task 4: Add migration integration test** - `3af2242` (feat)

## Files Created/Modified

- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs` - New 4-table SchemaScript, ILogger field, MigrateToFourTableModelAsync with private DTO records
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs` - RunDbInitializer factory registration with ILogger
- `tests/OpenAnima.Tests/Integration/MemoryMigrationTests.cs` - New migration integrity tests
- `src/OpenAnima.Core/Components/Shared/MemoryNodeCard.razor` - Updated MemorySnapshot -> MemoryContent (Rule 1 auto-fix)

## Decisions Made

- **Migration before SchemaScript**: `EnsureCreatedAsync` calls `MigrateToFourTableModelAsync` FIRST, before `SchemaScript`. The SchemaScript creates `CREATE INDEX ... ON memory_edges(anima_id, parent_uuid)` which would fail against the old table that only has `from_uri/to_uri`. Running migration first ensures the new table is in place before any index creation is attempted.
- **Two-pass content insert**: Snapshots (old versions) are inserted first with lower AUTOINCREMENT IDs; then current node content is inserted last with the highest ID. This ensures `ORDER BY id DESC LIMIT 1` correctly retrieves the most current version.
- **Explicit Dapper aliases**: All migration SELECT queries use `AS PropertyName` column aliases rather than relying on Dapper's snake_case-to-PascalCase stripping. This removes ambiguity about Dapper version behavior.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed MemoryNodeCard.razor using deleted MemorySnapshot type**
- **Found during:** Task 2 (Build verification after implementing migration logic)
- **Issue:** `MemorySnapshot` was deleted in Plan 01 (replaced by `MemoryContent`) but `MemoryNodeCard.razor` still referenced the old type and `GetSnapshotsAsync` method
- **Fix:** Updated `MemoryNodeCard.razor`: `MemorySnapshot` -> `MemoryContent`, `GetSnapshotsAsync` -> `GetContentHistoryAsync`, `RequestRestore(MemorySnapshot)` -> `RequestRestore(MemoryContent)`
- **Files modified:** `src/OpenAnima.Core/Components/Shared/MemoryNodeCard.razor`
- **Verification:** `dotnet build` succeeds with 0 errors
- **Committed in:** `6585692` (Task 2 commit)

**2. [Rule 1 - Bug] Fixed migration executing after SchemaScript (index failure on old table)**
- **Found during:** Task 4 (Running integration tests)
- **Issue:** SchemaScript's `CREATE INDEX ... ON memory_edges(anima_id, parent_uuid)` executed against the old memory_edges table (which only has `from_uri/to_uri`), causing `SQLite Error 1: no such column: parent_uuid`
- **Fix:** Moved `MigrateToFourTableModelAsync` call to BEFORE `SchemaScript` execution in `EnsureCreatedAsync`
- **Files modified:** `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs`
- **Verification:** Both migration tests pass
- **Committed in:** `3af2242` (Task 4 commit)

**3. [Rule 1 - Bug] Fixed content insert order causing wrong "latest" version**
- **Found during:** Task 4 (Running integration tests — MigratePreservesAllData assertion failure)
- **Issue:** Current content was inserted first (lower ID), then snapshots (higher IDs). `ORDER BY id DESC LIMIT 1` returned a snapshot row instead of current content
- **Fix:** Reorganized migration: insert memory_nodes + uri_paths first, then snapshots (step 6), then current content (step 6b) so current content always has the highest ID
- **Files modified:** `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs`
- **Verification:** `MigratePreservesAllData` assertion passes; content matches "I am an AI assistant"
- **Committed in:** `3af2242` (Task 4 commit)

**4. [Rule 1 - Bug] Fixed Dapper property mapping via explicit AS aliases**
- **Found during:** Task 4 (disclosure_trigger assertion failure after insert-order fix)
- **Issue:** `disclosure_trigger` column not mapping to `OldMemoryNode.DisclosureTrigger` without explicit alias — returned null instead of "identity"
- **Fix:** Added explicit `column AS PropertyName` aliases to all three migration SELECT queries
- **Files modified:** `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs`
- **Verification:** Both migration tests pass
- **Committed in:** `3af2242` (Task 4 commit)

---

**Total deviations:** 4 auto-fixed (4 Rule 1 bugs)
**Impact on plan:** All bugs blocking test passage or compilation. No scope creep.

## Issues Encountered

- `EnsureCreatedAsync` execution order required significant redesign: migration must precede SchemaScript to avoid column-reference errors on old tables. This is a structural constraint that should be documented for future schema migrations.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Schema migration is complete and tested. Plan 03 (MemoryGraph rewrite) can proceed.
- `RunDbInitializer` creates clean four-table schema on fresh install; existing installs will migrate automatically on next startup with backup.
- `MemoryMigrationTests` provide regression coverage for both paths.

---
*Phase: 65-memory-schema-migration*
*Completed: 2026-03-26*
