# Database Guidelines

> Database patterns and conventions for this project.

---

## Overview

The project uses `Microsoft.Data.Sqlite` plus `Dapper`. There is no EF Core, no DbContext, and no migration toolchain. Schema management is code-driven and idempotent at application startup.

Current durable stores:

- `runs.db` for runs, steps, artifacts, and memory graph tables
- `chat.db` for chat history visibility data

The standard pattern is:

1. Register a singleton connection factory
2. Register a singleton initializer
3. Call initializer startup paths through hosted services or startup services
4. Open a fresh SQLite connection per repository/service method

---

## Query Patterns

- Use raw SQL in multiline string literals.
- Open a new connection inside each public operation with `await using var conn = _factory.CreateConnection();`.
- Call `await conn.OpenAsync(ct)` before executing statements.
- Alias snake_case columns to C# property names when reading rows through Dapper.
- Prefer anonymous objects for parameters rather than manual string interpolation.

Representative examples:

- `src/OpenAnima.Core/RunPersistence/RunRepository.cs`: append-only writes and aliased read models
- `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs`: simple insert/query pattern with JSON payload columns
- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs`: schema creation and additive column migrations

Pattern to follow:

```csharp
await using var conn = _factory.CreateConnection();
await conn.OpenAsync(ct);
var rows = await conn.QueryAsync<RowType>(sql, new { RunId = runId });
```

---

## Migrations

Migrations are initializer-driven, not command-driven.

- Use `CREATE TABLE IF NOT EXISTS` and `CREATE INDEX IF NOT EXISTS` in schema scripts.
- Use explicit additive checks via `pragma_table_info(...)` before `ALTER TABLE`.
- For large schema shape changes, migrate inside an explicit transaction and create a backup first.
- `RunDbInitializer` also enables SQLite WAL mode and `synchronous=NORMAL` before creating schema.

Representative examples:

- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs`: full schema script, additive migrations, and four-table memory migration
- `src/OpenAnima.Core/ChatPersistence/ChatDbInitializer.cs`: additive `sedimentation_json` column migration
- `src/OpenAnima.Core/DependencyInjection/RunServiceExtensions.cs`: startup registration for both DB initializers

There is currently no separate migration history table. Startup idempotence is the migration contract.

---

## Naming Conventions

- SQL tables and columns use `snake_case`: `run_state_events`, `workflow_preset`, `created_at`.
- C# types and properties use `PascalCase`.
- When projecting into Dapper row types, alias SQL columns to the exact C# property names: `run_id AS RunId`.
- Primary keys are usually explicit text IDs for domain entities (`run_id`, `artifact_id`, `uuid`) and `INTEGER PRIMARY KEY AUTOINCREMENT` for append-only event rows.

Representative examples:

- `src/OpenAnima.Core/RunPersistence/RunRepository.cs`: `run_id AS RunId`
- `src/OpenAnima.Core/ChatPersistence/ChatHistoryService.cs`: `tool_calls_json AS ToolCallsJson`
- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs`: schema naming style for tables and indexes

---

## Transactions

Use transactions only when atomicity really matters.

- Multi-step repository writes often rely on a single connection and ordered statements without a transaction if the failure mode is acceptable.
- Destructive or shape-changing migrations must use an explicit transaction.
- Keep transaction scope local and short.

Representative example:

- `src/OpenAnima.Core/RunPersistence/RunDbInitializer.cs`: `BeginTransactionAsync()` around the memory schema migration

---

## Common Mistakes

- Reusing a long-lived `SqliteConnection` instead of creating a fresh one per operation
- Writing SQL that depends on Dapper underscore-mapping instead of using explicit `AS` aliases
- Introducing schema changes outside an initializer, which breaks startup idempotence
- Forgetting to preserve cancellation support on `OpenAsync(ct)`
- Using string interpolation to build SQL values instead of parameters
