---
phase: 66
plan: 01
subsystem: Chat & Viewport Persistence Infrastructure
tags: [persistence, SQLite, viewport, debounce, DI]
dependency_graph:
  requires: [Phase 65 complete, SQLite infrastructure proven]
  provides: [ChatDbConnectionFactory, ChatDbInitializer, ViewportStateService]
  affects: [Phase 67 Memory Tools, Phase 69 Chat Resilience]
tech_stack:
  added: [SQLite chat_messages table, JSON viewport persistence, 1000ms debounce]
  patterns: [Dapper ORM, CancellationTokenSource debounce, Factory/Initializer pattern]
key_files:
  created:
    - src/OpenAnima.Core/ChatPersistence/ChatDbConnectionFactory.cs
    - src/OpenAnima.Core/ChatPersistence/ChatDbInitializer.cs
    - src/OpenAnima.Core/ViewportPersistence/ViewportState.cs
    - src/OpenAnima.Core/ViewportPersistence/ViewportStateService.cs
  modified:
    - src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs
    - src/OpenAnima.Core/Hosting/RunRecoveryService.cs
decisions:
  - Chat database stored in separate chat.db file (not runs.db) for schema independence
  - Viewport stored as per-Anima {animaId}.viewport.json files in config directory
  - ViewportStateService uses 1000ms debounce (vs 500ms for config auto-save) for viewport-specific frequency
  - Database initialization happens in RunRecoveryService.StartAsync() for startup consistency
metrics:
  duration: "11 minutes"
  completed_date: "2026-03-29T07:01:22Z"
  task_count: 6
  file_count: 6
---

# Phase 66 Plan 01: Platform Persistence Infrastructure Summary

**One-liner:** Established SQLite chat persistence with `chat.db` + per-Anima JSON viewport state using debounce pattern, with DI registration and startup initialization hooks.

---

## Objective

Build the persistence infrastructure for Phase 66: SQLite chat database factory + schema with `chat_messages` table, viewport JSON service with debounce, and DI registration. This foundation supports later phases for chat history restore (Wave 2) and token-budget context truncation (Wave 3).

---

## Tasks Completed

All 6 tasks executed successfully and committed atomically.

| Task | Name | Complexity | Status | Commit Hash |
|------|------|-----------|--------|-------------|
| 1 | Create ChatDbConnectionFactory | Medium | DONE | c217ac9 |
| 2 | Create ChatDbInitializer with chat_messages Schema | Medium | DONE | c217ac9 |
| 3 | Create ViewportStateService with Debounce | Medium-High | DONE | c217ac9 |
| 4 | Create ViewportState Record | Low | DONE | c217ac9 |
| 5 | Register Services in DI | Medium | DONE | 3f17c6c |
| 6 | Initialize Chat Database on Startup | Low | DONE | 7303b4f |

---

## Deviations from Plan

### Registration Location Adjustment
**Found during:** Task 5 (DI registration)
**Rationale:** Plan specified adding registrations to AnimaServiceExtensions, but RunServiceExtensions is the logical location since:
- Chat database is part of the "run persistence" layer (alongside RunDbConnectionFactory, RunDbInitializer)
- ViewportStateService uses config directory already determined in RunServiceExtensions context
- Keeps database initialization logic centralized in one service extension method

**Impact:** No functional change; cleaner code organization. Both services now registered with RunServiceExtensions instead of AnimaServiceExtensions.

---

## Implementation Details

### 1. ChatDbConnectionFactory
- File: `/home/user/OpenAnima/src/OpenAnima.Core/ChatPersistence/ChatDbConnectionFactory.cs`
- Singleton factory providing `SqliteConnection` instances with `Busy Timeout=5000`
- Copied exact pattern from `RunDbConnectionFactory` per Phase 65 proven approach
- Supports production path-based constructor (used) and raw connection string constructor (for testing)

### 2. ChatDbInitializer
- File: `/home/user/OpenAnima/src/OpenAnima.Core/ChatPersistence/ChatDbInitializer.cs`
- Creates idempotent schema with `CREATE TABLE IF NOT EXISTS` pattern
- Table: `chat_messages` with columns: id (PK), anima_id, role, content, tool_calls_json, input_tokens, output_tokens, created_at
- Index: `idx_chat_messages_anima_id` on (anima_id) for per-Anima queries
- Logs success on initialization

### 3. ViewportState
- File: `/home/user/OpenAnima/src/OpenAnima.Core/ViewportPersistence/ViewportState.cs`
- Simple init-only record: Scale (double, default 1.0), PanX (double, default 0), PanY (double, default 0)
- Serializable with System.Text.Json for JSON persistence

### 4. ViewportStateService
- File: `/home/user/OpenAnima/src/OpenAnima.Core/ViewportPersistence/ViewportStateService.cs`
- Constructor: Takes `configDirectory` and `ILogger<ViewportStateService>`
- `LoadAsync(animaId, ct)`: Returns ViewportState from `{animaId}.viewport.json`, or default if missing/error
- `TriggerSaveViewport(animaId, scale, panX, panY)`: Async void, 1000ms debounce via CancellationTokenSource swap
- Logs warnings on deserialization failure, errors on save failure; cancellation ignored gracefully

### 5. DI Registration
- File: `/home/user/OpenAnima/src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs`
- ChatDbConnectionFactory: Singleton with chat.db path (data/chat.db)
- ChatDbInitializer: Singleton, factory + logger injected
- ViewportStateService: Singleton, config directory created if missing, logger injected
- Registered after RunDbConnectionFactory/Initializer for logical grouping

### 6. Startup Integration
- File: `/home/user/OpenAnima/src/OpenAnima.Core/Hosting/RunRecoveryService.cs`
- ChatDbInitializer dependency added to constructor
- `EnsureCreatedAsync(ct)` called in `StartAsync()` immediately after RunDbInitializer initialization
- Ensures chat.db schema exists before first use

---

## Verification Results

### Compilation
- `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj -c Release`: **PASSED** (0 errors, 0 warnings)
- All four new classes compile without errors or warnings
- DI registrations verify (no missing dependencies)

### Acceptance Criteria

All 6 acceptance criteria sets met:

**Task 1 (ChatDbConnectionFactory)**
- [x] File exists at `src/OpenAnima.Core/ChatPersistence/ChatDbConnectionFactory.cs`
- [x] Contains `public class ChatDbConnectionFactory`
- [x] Constructor signature: `public ChatDbConnectionFactory(string dbPath)`
- [x] Connection string includes `Busy Timeout=5000`
- [x] Method `CreateConnection()` returns `SqliteConnection`
- [x] Compiles without errors

**Task 2 (ChatDbInitializer)**
- [x] File exists at `src/OpenAnima.Core/ChatPersistence/ChatDbInitializer.cs`
- [x] Contains `public class ChatDbInitializer`
- [x] Constructor: `public ChatDbInitializer(ChatDbConnectionFactory factory, ILogger<ChatDbInitializer> logger)`
- [x] Method: `public async Task EnsureCreatedAsync(CancellationToken ct = default)`
- [x] SQL contains all columns: anima_id, role, content, tool_calls_json, input_tokens, output_tokens, created_at
- [x] Index on anima_id present
- [x] Compiles without errors

**Task 3 (ViewportStateService)**
- [x] File exists at `src/OpenAnima.Core/ViewportPersistence/ViewportStateService.cs`
- [x] Constructor: `ViewportStateService(string configDirectory, ILogger<ViewportStateService> logger)`
- [x] `LoadAsync(string animaId, CancellationToken ct = default)` exists
- [x] File path uses: `Path.Combine(_configDirectory, $"{animaId}.viewport.json")`
- [x] `TriggerSaveViewport(string animaId, double scale, double panX, double panY)` is async void
- [x] Debounce uses CancellationTokenSource
- [x] Delay is exactly 1000ms
- [x] Compiles without errors

**Task 4 (ViewportState)**
- [x] File exists at `src/OpenAnima.Core/ViewportPersistence/ViewportState.cs`
- [x] Contains `public record ViewportState`
- [x] Three properties with init: Scale (double, 1.0), PanX (double, 0), PanY (double, 0)
- [x] Compiles without errors

**Task 5 (DI Registration)**
- [x] ChatDbConnectionFactory registered (in RunServiceExtensions)
- [x] Registration contains: `new ChatDbConnectionFactory(chatDbPath)`
- [x] ChatDbInitializer registration with factory and logger injection
- [x] ViewportStateService registration with configDirectory and logger
- [x] Compiles without errors

**Task 6 (Startup)**
- [x] ChatDbInitializer.EnsureCreatedAsync() called during startup (RunRecoveryService.StartAsync)
- [x] Called after RunDbInitializer.EnsureCreatedAsync()

---

## Post-Phase Status

### Wave 1 Infrastructure Complete
- Database connectivity ✓
- Schema creation ✓
- Viewport serialization ✓
- Debounce pattern established ✓
- DI registration ✓
- Startup initialization ✓

### Dependencies Ready for Wave 2
- Phase 67 (Memory Tools) can depend on chat.db for sedimentation input
- Phase 69 (Chat Resilience) can depend on chat history restore mechanism
- Services are singleton and available to all Animas

### Future Hooks
- ChatHistoryService will use ChatDbConnectionFactory for message persistence (Wave 2)
- EditorStateService.UpdatePan/UpdateScale will call ViewportStateService.TriggerSaveViewport (Wave 2)
- ChatPanel.OnAnimaChanged will restore viewport via ViewportStateService.LoadAsync (Wave 2)

---

## Commits

| Hash | Message |
|------|---------|
| c217ac9 | feat(66-01): create chat persistence and viewport services |
| 3f17c6c | feat(66-01): register chat and viewport persistence services in DI |
| 7303b4f | feat(66-01): initialize chat database on app startup |

---

## No Deferred Issues

All tasks completed as planned. No blocking issues encountered. No auto-fix deviations applied (no bugs, missing critical functionality, or blocking issues discovered).
