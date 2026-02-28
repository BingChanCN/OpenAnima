---
phase: 21-pack-validate-runtime-integration
plan: 01
subsystem: cli-validation
tags: [cli, validation, manifest, assembly-reflection]
dependency_graph:
  requires: [phase-20-cli-foundation]
  provides: [validate-command, manifest-validation, assembly-validation]
  affects: [cli-commands, module-development-workflow]
tech_stack:
  added: [System.Runtime.Loader.AssemblyLoadContext]
  patterns: [isolated-assembly-loading, name-based-type-checking, error-accumulation]
key_files:
  created:
    - src/OpenAnima.Cli/Commands/ValidateCommand.cs
    - tests/OpenAnima.Cli.Tests/CliFoundationTests.cs (ValidateCommandTests)
  modified:
    - src/OpenAnima.Cli/Program.cs
decisions:
  - Use name-based type comparison for IModule detection to avoid type identity issues across AssemblyLoadContext boundaries
  - Make assembly validation optional (warning only) if module not built yet
  - Accumulate all errors before reporting to provide complete validation feedback
  - Use isolated AssemblyLoadContext with isCollectible=true for safe assembly inspection
metrics:
  duration_minutes: 3
  tasks_completed: 2
  tests_added: 6
  files_created: 2
  files_modified: 1
  commits: 2
  completed_date: "2026-02-28"
---

# Phase 21 Plan 01: Validate Command Implementation Summary

**One-liner:** Module validation command with manifest JSON checking and IModule implementation verification via isolated assembly reflection.

## What Was Built

Implemented the `oani validate <path>` command that validates module projects before packing. The command checks:

1. **Manifest existence** - Verifies module.json exists in the target directory
2. **JSON validity** - Parses and validates JSON structure using ManifestValidator
3. **Required fields** - Ensures id, version, and name fields are present and valid
4. **IModule implementation** - When DLL exists, loads assembly in isolated context and verifies IModule implementation using name-based type checking
5. **Comprehensive error reporting** - Accumulates and reports ALL validation errors at once

The validate command follows the same pattern as NewCommand, using System.CommandLine with proper exit code propagation through InvocationContext.

## Tasks Completed

### Task 1: Create ValidateCommand with manifest and assembly validation (TDD)
- **Commit:** 7b85a99
- **Files:**
  - Created: src/OpenAnima.Cli/Commands/ValidateCommand.cs
  - Created: tests/OpenAnima.Cli.Tests/CliFoundationTests.cs (ValidateCommandTests class)
  - Modified: src/OpenAnima.Cli/Program.cs
- **Tests:** 6 tests added covering all validation scenarios
- **Status:** ✓ Complete

### Task 2: Verify full validate command integration
- **Commit:** 7a26867
- **Files:**
  - Modified: tests/OpenAnima.Cli.Tests/CliFoundationTests.cs (test helper fix)
- **Verification:** All 36 CLI tests pass, no regressions
- **Status:** ✓ Complete

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed test helper StringWriter disposal issue**
- **Found during:** Task 2 integration testing
- **Issue:** Test helper was disposing StringWriter in `using` statement before command finished writing, causing ObjectDisposedException
- **Fix:** Changed to capture output before disposing writers in finally block
- **Files modified:** tests/OpenAnima.Cli.Tests/CliFoundationTests.cs
- **Commit:** 7a26867

## Technical Decisions

### 1. Name-based type comparison for IModule detection
**Context:** Cross-context type identity issues when using `typeof(IModule).IsAssignableFrom(type)`

**Decision:** Use name-based comparison: `i.FullName == "OpenAnima.Contracts.IModule"`

**Rationale:** Follows the pattern established in PluginLoader.cs. Avoids type identity issues when loading assemblies in isolated AssemblyLoadContext.

### 2. Optional assembly validation
**Context:** Module may not be built yet when running validate

**Decision:** If bin/ directory or DLL doesn't exist, skip assembly validation (no error)

**Rationale:** Developers should be able to validate manifest before building. Assembly validation is a bonus check when available.

### 3. Error accumulation pattern
**Context:** VAL-05 requirement to report ALL errors, not just first

**Decision:** Use `List<string> errors` to accumulate all validation errors before outputting

**Rationale:** Better developer experience - see all issues at once rather than fix-one-run-again cycle.

## Requirements Satisfied

- **VAL-01:** ✓ Developer can run `oani validate <path>` and see validation results
- **VAL-02:** ✓ Validate checks module.json exists and is valid JSON
- **VAL-03:** ✓ Validate checks required manifest fields (id, version, name)
- **VAL-04:** ✓ Validate verifies IModule implementation via assembly reflection
- **VAL-05:** ✓ Validate reports ALL errors, not just the first one

## Test Coverage

**ValidateCommand tests (6 new):**
- ValidateCommand_NoPathArgument_ReturnsError
- ValidateCommand_ValidModule_ReturnsSuccess
- ValidateCommand_MissingModuleJson_ReturnsValidationError
- ValidateCommand_InvalidJson_ReturnsValidationError
- ValidateCommand_MultipleErrors_ReportsAll
- ValidateCommand_NonExistentPath_ReturnsValidationError

**All tests passing:** 36/36 (including existing CLI foundation tests)

## Verification Results

✓ CLI project builds without errors
✓ All 36 tests pass (no regressions)
✓ `oani --help` lists validate command
✓ `oani validate --help` shows command help
✓ `oani validate /nonexistent` returns exit code 2 with error message

## Key Files

**Created:**
- `src/OpenAnima.Cli/Commands/ValidateCommand.cs` (145 lines) - Main validation command implementation
- `tests/OpenAnima.Cli.Tests/CliFoundationTests.cs` - ValidateCommandTests class (200+ lines)

**Modified:**
- `src/OpenAnima.Cli/Program.cs` - Registered ValidateCommand, updated help text

## Next Steps

Ready for Phase 21 Plan 02: Pack Command Implementation

The validate command provides the foundation for the pack command, which will use the same validation logic before creating .oamod packages.

## Self-Check

Verifying created files and commits exist:

```bash
# Check files
[ -f "src/OpenAnima.Cli/Commands/ValidateCommand.cs" ] && echo "✓ ValidateCommand.cs exists"
[ -f "tests/OpenAnima.Cli.Tests/CliFoundationTests.cs" ] && echo "✓ CliFoundationTests.cs exists"

# Check commits
git log --oneline --all | grep -q "7b85a99" && echo "✓ Commit 7b85a99 exists"
git log --oneline --all | grep -q "7a26867" && echo "✓ Commit 7a26867 exists"
```

## Self-Check: PASSED

✓ ValidateCommand.cs exists
✓ CliFoundationTests.cs exists
✓ Commit 7b85a99 exists
✓ Commit 7a26867 exists
