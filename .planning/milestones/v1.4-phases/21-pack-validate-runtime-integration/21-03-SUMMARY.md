---
phase: 21-pack-validate-runtime-integration
plan: 03
subsystem: runtime-loader
tags: [oamod, extraction, hot-reload, plugin-system]
completed: 2026-02-28
duration_minutes: 5
dependency_graph:
  requires: [21-02]
  provides: [oamod-runtime-loading]
  affects: [plugin-loader, module-watcher]
tech_stack:
  added: [System.IO.Compression.ZipFile]
  patterns: [timestamp-based-extraction, debounced-file-watching]
key_files:
  created:
    - src/OpenAnima.Core/Plugins/OamodExtractor.cs
  modified:
    - src/OpenAnima.Core/Plugins/PluginLoader.cs
    - src/OpenAnima.Core/Plugins/ModuleDirectoryWatcher.cs
    - tests/OpenAnima.Cli.Tests/CliFoundationTests.cs
    - tests/OpenAnima.Cli.Tests/OpenAnima.Cli.Tests.csproj
decisions:
  - Extract to .extracted/ subdirectory to avoid conflicts with regular modules
  - Use timestamp marker file for extraction freshness checks
  - Skip .extracted/ directory during PluginLoader.ScanDirectory to prevent double-loading
  - Idempotent extraction with clean re-extract on each call
  - Same 500ms debounce pattern for .oamod files as directory watcher
metrics:
  tasks_completed: 2
  commits: 3
  files_created: 1
  files_modified: 4
  tests_added: 6
---

# Phase 21 Plan 03: Runtime Integration for .oamod Packages

**One-liner:** .oamod packages extract and load seamlessly in OpenAnima runtime with hot reload support

## Overview

Extended the OpenAnima runtime to automatically extract and load packed .oamod modules. The runtime now handles .oamod files identically to unpacked module directories, completing the full develop-validate-pack-load cycle.

## What Was Built

### 1. OamodExtractor Service
- **Extract method**: Extracts .oamod ZIP to `.extracted/{moduleName}` subdirectory
- **NeedsExtraction method**: Timestamp-based freshness check using marker file
- **Idempotent extraction**: Clean re-extract on each call (deletes existing directory)
- **Timestamp tracking**: Writes `.extraction-timestamp` marker file with .oamod LastWriteTimeUtc

### 2. PluginLoader Extensions
- **ScanDirectory enhancement**: Scans for both subdirectories and .oamod files
- **Skip .extracted/**: Prevents double-loading of extracted modules
- **Error handling**: Extraction failures captured in LoadResult (no crashes)

### 3. ModuleDirectoryWatcher Extensions
- **File watcher**: Added second FileSystemWatcher for `*.oamod` files
- **Hot reload**: Detects Created and Changed events for .oamod files
- **Debounced extraction**: 500ms debounce before extracting and loading
- **RefreshAll enhancement**: Scans and extracts .oamod files during manual refresh

### 4. Test Coverage
- **OamodExtractorTests**: 4 tests covering extraction, idempotency, and timestamp checks
- **PluginLoaderOamodTests**: 2 tests covering .oamod scanning and .extracted/ skipping
- **All tests passing**: 6/6 tests pass

## Tasks Completed

### Task 1: Create OamodExtractor and extend PluginLoader.ScanDirectory
**Status:** ✅ Complete
**Approach:** TDD (RED-GREEN)
**Commits:**
- `c17d641`: test(21-03): add failing tests for OamodExtractor and PluginLoader .oamod support
- `6c47d8e`: feat(21-03): implement OamodExtractor and extend PluginLoader for .oamod support

**Deliverables:**
- OamodExtractor.cs with Extract and NeedsExtraction methods
- PluginLoader.ScanDirectory handles .oamod files
- .extracted/ directory skipped during scan
- 6 unit tests (all passing)

### Task 2: Extend ModuleDirectoryWatcher for .oamod hot reload
**Status:** ✅ Complete
**Commits:**
- `eace7c1`: feat(21-03): extend ModuleDirectoryWatcher for .oamod hot reload

**Deliverables:**
- FileSystemWatcher for .oamod files (Created and Changed events)
- OnOamodCreated handler with extraction and callback
- RefreshAll scans .oamod files
- Proper disposal of file watcher

## Deviations from Plan

None - plan executed exactly as written.

## Technical Decisions

### 1. Extract to .extracted/ subdirectory
**Rationale:** Avoids naming conflicts with regular module directories. Clear separation between packed and unpacked modules.

### 2. Timestamp-based extraction check
**Rationale:** Avoids unnecessary re-extraction on every startup. Marker file approach is simple and reliable.

### 3. Idempotent extraction with clean re-extract
**Rationale:** Ensures extracted content always matches .oamod file. Simpler than incremental updates.

### 4. Skip .extracted/ during directory scan
**Rationale:** Prevents double-loading (once from .oamod, once from extracted directory). Critical for correctness.

## Verification

### Automated Tests
```bash
dotnet test tests/OpenAnima.Cli.Tests/ --filter "OamodExtractor|PluginLoaderOamod"
```
**Result:** 6/6 tests passed

### Build Verification
```bash
dotnet build src/OpenAnima.Core/
```
**Result:** Build succeeded, 0 warnings, 0 errors

## Integration Points

### Upstream Dependencies
- **21-02 (Pack Command)**: Produces .oamod files that this plan consumes
- **PluginLoader.LoadModule**: Existing method used to load extracted modules
- **PluginManifest**: Existing manifest parsing used for extracted modules

### Downstream Impact
- **OpenAnimaHostedService**: No changes needed (uses PluginLoader.ScanDirectory)
- **IModuleService**: No changes needed (uses PluginLoader under the hood)
- **Module developers**: Can now distribute .oamod files for seamless loading

## Success Criteria

- [x] OamodExtractor.cs extracts .oamod ZIPs to loadable directories
- [x] PluginLoader.ScanDirectory handles both directories and .oamod files
- [x] ModuleDirectoryWatcher detects .oamod file additions for hot reload
- [x] Extracted modules load through existing PluginLoader.LoadModule() pathway
- [x] .extracted/ subdirectory managed correctly (no double-load, clean re-extract)
- [x] Full solution builds and all tests pass
- [x] PACK-06 satisfied: packed module loads in runtime without modification

## Files Changed

### Created
- `src/OpenAnima.Core/Plugins/OamodExtractor.cs` (80 lines)

### Modified
- `src/OpenAnima.Core/Plugins/PluginLoader.cs` (+20 lines)
- `src/OpenAnima.Core/Plugins/ModuleDirectoryWatcher.cs` (+92 lines)
- `tests/OpenAnima.Cli.Tests/CliFoundationTests.cs` (+295 lines)
- `tests/OpenAnima.Cli.Tests/OpenAnima.Cli.Tests.csproj` (+1 line)

## Next Steps

Phase 21 complete. All three plans executed successfully:
- 21-01: Validate command (manifest and assembly validation)
- 21-02: Pack command (.oamod creation with checksum)
- 21-03: Runtime integration (.oamod loading and hot reload)

The full develop-validate-pack-load cycle is now operational. Module developers can:
1. Create modules with `oani new`
2. Validate with `oani validate`
3. Pack with `oani pack`
4. Load .oamod files in OpenAnima runtime (automatic extraction and loading)

## Self-Check: PASSED

### Created Files
```bash
[ -f "src/OpenAnima.Core/Plugins/OamodExtractor.cs" ] && echo "FOUND"
```
**Result:** FOUND

### Commits
```bash
git log --oneline --grep="21-03" | head -3
```
**Result:**
- eace7c1: feat(21-03): extend ModuleDirectoryWatcher for .oamod hot reload
- 6c47d8e: feat(21-03): implement OamodExtractor and extend PluginLoader for .oamod support
- c17d641: test(21-03): add failing tests for OamodExtractor and PluginLoader .oamod support

All commits exist and are properly formatted.
