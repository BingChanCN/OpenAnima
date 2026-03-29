---
phase: 48-artifact-memory-foundation
plan: 01
subsystem: database
tags: [sqlite, dapper, artifacts, filesystem, persistence, unit-tests]

# Dependency graph
requires:
  - phase: 45-durable-task-runtime-foundation
    provides: RunDbConnectionFactory, RunDbInitializer, IRunRepository, RunRepository patterns
  - phase: 47-run-inspection-observability
    provides: StepRecord.ArtifactRefId field (stub reference now fulfilled)
provides:
  - ArtifactRecord immutable record with 7 init-only properties
  - IArtifactStore interface with 5 methods
  - ArtifactStore SQLite+filesystem implementation
  - ArtifactFileWriter with path safety, MIME extension mapping
  - artifacts table DDL in RunDbInitializer SchemaScript
  - 7 unit tests covering all write/read/delete paths
affects: [48-02-memory-graph, 48-03-step-recorder-integration, 48-04-artifact-ui, 49-structured-cognition]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - ArtifactStore follows RunRepository per-operation connection pattern (WAL-compatible)
    - ArtifactFileWriter path traversal guard via Path.GetFullPath comparison
    - 12-char hex artifact IDs from Guid.NewGuid().ToString("N")[..12]
    - ArtifactStoreTests uses keepalive + shared-cache in-memory SQLite + temp dir cleanup pattern

key-files:
  created:
    - src/OpenAnima.Core/Artifacts/ArtifactRecord.cs
    - src/OpenAnima.Core/Artifacts/IArtifactStore.cs
    - src/OpenAnima.Core/Artifacts/ArtifactFileWriter.cs
    - src/OpenAnima.Core/Artifacts/ArtifactStore.cs
    - tests/OpenAnima.Tests/Unit/ArtifactStoreTests.cs
  modified:
    - src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs

key-decisions:
  - "ArtifactFileWriter uses Path.GetFullPath comparison for path traversal prevention — security-critical for filesystem write operations"
  - "ArtifactStore uses per-operation connections (same WAL pattern as RunRepository) — no shared connection state"
  - "12-char hex IDs for artifacts (vs 8-char step IDs) — slightly longer for lower collision probability across runs"
  - "FileSizeBytes computed from Encoding.UTF8.GetByteCount at write time — no second file read needed"

patterns-established:
  - "ArtifactStore pattern: write to filesystem first, then insert metadata row — filesystem is source of truth for content"
  - "Column alias pattern: SELECT artifact_id AS ArtifactId for direct Dapper mapping to record properties"
  - "ArtifactFileWriter.MimeToExtension is static — callable without instance for pre-write path computation"

requirements-completed: [ART-01]

# Metrics
duration: 5min
completed: 2026-03-21
---

# Phase 48 Plan 01: Artifact Memory Foundation — Data Layer Summary

**SQLite-backed artifact persistence with filesystem content storage: ArtifactRecord, IArtifactStore, ArtifactStore, ArtifactFileWriter, schema DDL, and 7 passing unit tests**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-03-21T11:57:19Z
- **Completed:** 2026-03-21T12:02:32Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments

- Created the complete artifact data layer: types, interface, SQLite+filesystem implementation, filesystem helper
- Extended RunDbInitializer SchemaScript with the artifacts table and two indexes (run_id, step_id)
- Path traversal prevention in ArtifactFileWriter via Path.GetFullPath boundary check
- 7 unit tests using in-memory SQLite + temp filesystem — all passing, mirroring the RunRepositoryTests keepalive pattern

## Task Commits

Each task was committed atomically:

1. **Task 1: ArtifactRecord, IArtifactStore, ArtifactFileWriter, DB schema extension** - `622a219` (feat)
2. **Task 2: ArtifactStore implementation + unit tests** - `6c20547` (feat)

## Files Created/Modified

- `src/OpenAnima.Core/Artifacts/ArtifactRecord.cs` - Immutable record with 7 init-only properties (ArtifactId, RunId, StepId, MimeType, FilePath, FileSizeBytes, CreatedAt)
- `src/OpenAnima.Core/Artifacts/IArtifactStore.cs` - Interface declaring WriteArtifactAsync, GetArtifactsByRunIdAsync, GetArtifactByIdAsync, ReadContentAsync, DeleteArtifactsByRunAsync
- `src/OpenAnima.Core/Artifacts/ArtifactFileWriter.cs` - Filesystem helper with path safety, WriteAsync, ReadAsync, DeleteDirectoryAsync, MimeToExtension
- `src/OpenAnima.Core/Artifacts/ArtifactStore.cs` - SQLite+filesystem implementation via Dapper and ArtifactFileWriter
- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs` - SchemaScript extended with artifacts table + idx_artifacts_run_id + idx_artifacts_step_id
- `tests/OpenAnima.Tests/Unit/ArtifactStoreTests.cs` - 7 unit tests: write/read/delete/null/MIME/ID-length cases

## Decisions Made

- **Path safety first**: ArtifactFileWriter validates that resolved absolute paths remain under `_artifactsRoot` using `Path.GetFullPath` comparison — prevents path traversal attacks on artifact writes.
- **Per-operation connections**: ArtifactStore opens a new connection per method call, consistent with RunRepository's WAL-safe pattern.
- **12-char IDs**: Artifacts use `Guid.NewGuid().ToString("N")[..12]` (vs 8-char step IDs) for slightly lower collision probability given higher artifact volume per run.
- **UTF-8 byte count at write time**: FileSizeBytes uses `Encoding.UTF8.GetByteCount(content)` — avoids a second filesystem read just for size.

## Deviations from Plan

None — plan executed exactly as written.

Note: RunDbInitializer.cs was also modified by an automatic process that added memory_nodes, memory_edges, and memory_snapshots tables. These are part of Plan 48-02 and were added concurrently. The modifications are consistent with the overall phase 48 direction and do not conflict with this plan's artifacts table.

## Issues Encountered

None.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- Artifact persistence layer is complete and tested — Plan 48-03 can wire ArtifactStore into StepRecorder
- IArtifactStore is DI-ready: constructor injection via RunDbConnectionFactory + ArtifactFileWriter + ILogger
- ArtifactRecord.ArtifactId can be stored in StepRecord.ArtifactRefId (already has the field)
- RunDbInitializer.EnsureCreatedAsync will create the artifacts table on next startup (idempotent)

---
*Phase: 48-artifact-memory-foundation*
*Completed: 2026-03-21*
