# Phase 65: Memory Schema Migration - Context

**Gathered:** 2026-03-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Migrate the existing 3-table memory model (memory_nodes / memory_edges / memory_snapshots) to a 4-table model (memory_nodes / memory_contents / memory_edges / memory_uri_paths) with stable UUID node identity, first-class edges with priority/weight/bidirectional fields, URI path routing layer, and SQLite Busy Timeout hardening. All existing data must be migrated without loss in a single atomic transaction. IMemoryGraph interface updated for the new model.

</domain>

<decisions>
## Implementation Decisions

### Node Type Taxonomy
- Independent semantic type system (decoupled from URI prefix): System / Fact / Preference / Entity / Learning / Artifact
- Migration mapping from URI prefix: `core://` -> System, `sediment://fact/` -> Fact, `sediment://preference/` -> Preference, `sediment://entity/` -> Entity, `sediment://learning/` -> Learning, `run://` -> Artifact
- `node_type` column stores the type string; future agents can specify it explicitly via memory_create tool (optional parameter, falls back to URI-based inference if omitted)
- `display_name` populated during migration by extracting the last segment of the URI (e.g., `sediment://fact/project-uses-blazor` -> `"project-uses-blazor"`)

### Content Versioning Mapping
- Full migration: all existing memory_snapshots rows + current node content migrate to memory_contents table
- Current content becomes the latest version; snapshots map to older versions ordered by snapshot_at timestamp
- Version retention limit remains at 10 per node (consistent with current behavior)
- Old memory_snapshots table is DROPped after successful migration (not retained)

### Migration Failure Handling
- Migration triggers automatically on application startup (inside EnsureCreatedAsync / MigrateSchemaAsync pattern)
- Before migration: automatically backup the .db file as `.db.bak-{timestamp}`
- Atomic transaction: entire migration runs inside BEGIN/COMMIT
- On failure: ROLLBACK + fatal error, application refuses to start. Detailed error logged at Error level. Old data safe due to ROLLBACK + backup file
- On success: Information-level log only, no UI notification. User experience is seamless

### Edge Field Defaults
- `priority`: default 0 (neutral — no special priority)
- `weight`: default 1.0 (standard association strength)
- `bidirectional`: default false (preserve existing directed behavior)
- `disclosure_trigger`: default NULL (edges do not trigger disclosure)
- Edge references migrate from URI-based (from_uri/to_uri) to UUID-based (parent_uuid/child_uuid) using the UUID assigned to each node during migration

### Claude's Discretion
- Exact UUID generation strategy (v4 random vs deterministic from URI hash)
- memory_uri_paths table schema details (columns, indexes)
- IMemoryGraph interface method signature changes (how to maintain backward compatibility)
- Migration SQL ordering and intermediate steps
- Index strategy for new tables
- How RunDbConnectionFactory incorporates Busy Timeout=5000

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Memory Architecture
- `.planning/REQUIREMENTS.md` -- MEMA-01 through MEMA-08 define four-table model requirements, PERS-04 defines SQLite busy timeout
- `.planning/ROADMAP.md` -- Phase 65 success criteria (5 items) and dependency chain

### Current Implementation
- `src/OpenAnima.Core/Memory/IMemoryGraph.cs` -- Current interface contract (11 methods, URI-based identity)
- `src/OpenAnima.Core/Memory/MemoryGraph.cs` -- SQLite-backed implementation with Dapper, WAL mode, glossary cache
- `src/OpenAnima.Core/Memory/MemoryNode.cs` -- Current node record (URI+AnimaId primary key, no UUID)
- `src/OpenAnima.Core/Memory/MemoryEdge.cs` -- Current edge record (from_uri/to_uri, label only)
- `src/OpenAnima.Core/Memory/MemorySnapshot.cs` -- Current snapshot record (to be replaced by memory_contents)
- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs` -- Schema creation + migration pattern (EnsureCreatedAsync + MigrateSchemaAsync)
- `src/OpenAnima.Core/RunPersistence/RunDbConnectionFactory.cs` -- Connection factory (currently no Busy Timeout)

### Consumers (must continue working after migration)
- `src/OpenAnima.Core/Memory/MemoryRecallService.cs` -- Boot/Disclosure/Glossary recall pipeline
- `src/OpenAnima.Core/Memory/SedimentationService.cs` -- LLM-driven knowledge extraction
- `src/OpenAnima.Core/Memory/BootMemoryInjector.cs` -- Boot memory injection
- `src/OpenAnima.Core/Memory/DisclosureMatcher.cs` -- Disclosure trigger matching
- `src/OpenAnima.Core/Tools/MemoryWriteTool.cs` -- Agent memory write tool
- `src/OpenAnima.Core/Tools/MemoryDeleteTool.cs` -- Agent memory delete tool
- `src/OpenAnima.Core/Tools/MemoryQueryTool.cs` -- Agent memory query tool
- `src/OpenAnima.Core/Tools/MemoryRecallTool.cs` -- Agent memory recall tool
- `src/OpenAnima.Core/Tools/MemoryLinkTool.cs` -- Agent memory link tool
- `src/OpenAnima.Core/Modules/MemoryModule.cs` -- Memory module orchestration

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `RunDbInitializer.MigrateSchemaAsync`: Established pattern for additive schema migrations using pragma_table_info column detection
- `RunDbConnectionFactory`: Singleton connection factory -- Busy Timeout can be added to connection string here
- `GlossaryIndex` (Aho-Corasick trie): Needs rebuilding after migration but algorithm unchanged
- Dapper ORM: All queries use Dapper with manual column mapping -- new table queries follow same pattern

### Established Patterns
- WAL journal mode + PRAGMA synchronous=NORMAL already configured in EnsureCreatedAsync
- All memory operations use `await using var conn = _factory.CreateConnection()` pattern
- Column mapping in SELECT uses `column AS Property` aliases for Dapper
- ConcurrentDictionary for glossary cache invalidation on writes

### Integration Points
- `RunDbInitializer.EnsureCreatedAsync()` -- Entry point for schema creation and migration
- `RunDbConnectionFactory.CreateConnection()` -- Where Busy Timeout must be injected
- `IMemoryGraph` interface -- All consumers depend on this; changes here ripple to 10+ files
- `DependencyInjection/RunServiceExtensions.cs` -- DI registration for memory services

</code_context>

<specifics>
## Specific Ideas

No specific requirements -- open to standard approaches within the decisions captured above.

</specifics>

<deferred>
## Deferred Ideas

None -- discussion stayed within phase scope.

</deferred>

---

*Phase: 65-memory-schema-migration*
*Context gathered: 2026-03-26*
