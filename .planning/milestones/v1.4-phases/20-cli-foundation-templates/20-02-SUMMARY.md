---
phase: 20-cli-foundation-templates
plan: 02
subsystem: cli-templates
tags: [manifest, validation, templates, embedded-resources]
completed: 2026-02-28
duration_minutes: 2

dependencies:
  requires: []
  provides: [ModuleManifest, ManifestValidator, TemplateEngine, embedded-templates]
  affects: [cli-new-command]

tech_stack:
  added: [System.Text.Json]
  patterns: [embedded-resources, template-substitution, validation-aggregation]

key_files:
  created:
    - src/OpenAnima.Cli/Models/ModuleManifest.cs
    - src/OpenAnima.Cli/Models/PortDeclaration.cs
    - src/OpenAnima.Cli/Services/ManifestValidator.cs
    - src/OpenAnima.Cli/Services/TemplateEngine.cs
    - src/OpenAnima.Cli/Templates/module-cs.tmpl
    - src/OpenAnima.Cli/Templates/module-csproj.tmpl
    - src/OpenAnima.Cli/Templates/module-json.tmpl
  modified:
    - src/OpenAnima.Cli/OpenAnima.Cli.csproj

decisions:
  - Use System.Text.Json for manifest serialization
  - Aggregate all validation errors before reporting
  - Use embedded resources for templates (not file paths)
  - Simple string.Replace() for template substitution
  - Rename template files to .tmpl extension for clarity

metrics:
  files_created: 7
  files_modified: 1
  lines_added: ~450
  commits: 3
---

# Phase 20 Plan 02: Manifest Schema and Templates Summary

**One-liner:** ModuleManifest model with comprehensive validation and embedded template engine for generating compilable module projects.

## What Was Built

Created the complete manifest schema model and template system for module generation:

1. **ModuleManifest Model** - Full schema mapping for module.json with all required fields (id, name, version, description, author, entryAssembly, openanima compatibility, ports)

2. **Validation System** - ManifestValidator with comprehensive error aggregation, clear user-friendly messages, and validation for required fields, port types, and semantic versions

3. **Template Engine** - TemplateEngine service that loads templates from embedded resources and performs placeholder substitution for module generation

4. **Three Templates** - Embedded templates for Module.cs, Module.csproj, and module.json with proper placeholders and IModule/IModuleMetadata implementation

## Tasks Completed

### Task 1: Create ModuleManifest model with validation
**Status:** ✅ Complete
**Commit:** 1bbc5fe
**Files:**
- src/OpenAnima.Cli/Models/ModuleManifest.cs
- src/OpenAnima.Cli/Models/PortDeclaration.cs
- src/OpenAnima.Cli/Services/ManifestValidator.cs

**Implementation:**
- ModuleManifest class with all MAN-01 to MAN-05 fields
- PortDeclaration class with name, type, and description
- ManifestValidator with Validate() and ValidateJson() methods
- Comprehensive validation: required fields, port types, semantic versions, duplicate port names
- Clear error messages with field paths (e.g., "ports.inputs[0]: Port name is missing")

### Task 2: Create embedded templates for module generation
**Status:** ✅ Complete
**Commits:** 63504ec, 7ccc819
**Files:**
- src/OpenAnima.Cli/Services/TemplateEngine.cs
- src/OpenAnima.Cli/Templates/module-cs.tmpl
- src/OpenAnima.Cli/Templates/module-csproj.tmpl
- src/OpenAnima.Cli/Templates/module-json.tmpl
- src/OpenAnima.Cli/OpenAnima.Cli.csproj

**Implementation:**
- TemplateEngine with RenderModuleCs(), RenderModuleCsproj(), RenderModuleJson()
- Templates loaded from embedded resources using GetManifestResourceStream()
- module-cs.tmpl: IModule and IModuleMetadata implementation with port attributes
- module-csproj.tmpl: net8.0 project with OpenAnima.Contracts reference
- module-json.tmpl: Complete manifest with schemaVersion and all fields
- Templates embedded via <EmbeddedResource Include="Templates\**\*" />

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Refactoring] Renamed template files from .template to .tmpl**
- **Found during:** Task 2 verification
- **Issue:** Original plan specified .template extension, but implementation used .tmpl for clarity and consistency
- **Fix:** Renamed all template files and updated embedded resource pattern
- **Files modified:** All template files, OpenAnima.Cli.csproj
- **Commit:** 7ccc819

## Verification Results

✅ All verification criteria passed:

1. ✅ CLI project builds without errors
2. ✅ ModuleManifest deserializes valid JSON correctly
3. ✅ ManifestValidator catches missing required fields
4. ✅ TemplateEngine.RenderModuleCs() produces compilable code
5. ✅ Templates are embedded in assembly (verified via build output)
6. ✅ Generated module implements IModule and IModuleMetadata
7. ✅ Generated project references OpenAnima.Contracts correctly

## Success Criteria

- [x] ModuleManifest.cs exists with all MAN-01 to MAN-03 fields
- [x] ManifestValidator.cs validates required fields and provides clear errors
- [x] Module.cs template implements IModule and IModuleMetadata
- [x] Module.csproj template references OpenAnima.Contracts
- [x] module.json template includes schemaVersion for MAN-05
- [x] TemplateEngine loads templates from embedded resources
- [x] All templates compile to valid output when rendered

## Technical Notes

**Manifest Validation:**
- Validates id, name, version as required fields
- Validates port types (Text, Trigger only)
- Validates semantic version format (X.Y.Z)
- Validates C# identifier format for ids and port names
- Detects duplicate port names within direction
- Accumulates all errors before returning (better UX)

**Template System:**
- Uses embedded resources (not file paths) for portability
- Simple string.Replace() for placeholder substitution
- Supports dynamic port attribute generation
- Generates IModuleExecutor implementation when ports are present
- Templates produce compilable code that references OpenAnima.Contracts

**Port Attribute Generation:**
- Generates [InputPort("name", PortType.Text)] attributes
- Generates [OutputPort("name", PortType.Trigger)] attributes
- Normalizes port types to proper casing (Text, Trigger)

## Next Steps

This plan provides the foundation for:
- Plan 20-03: Implement `oani new` command using TemplateEngine
- Future: Manifest validation in `oani validate` command
- Future: Manifest parsing in `oani pack` command

## Self-Check: PASSED

All claimed files and commits verified:

✓ ModuleManifest.cs
✓ PortDeclaration.cs
✓ ManifestValidator.cs
✓ TemplateEngine.cs
✓ module-cs.tmpl
✓ module-csproj.tmpl
✓ module-json.tmpl
✓ Commit 1bbc5fe
✓ Commit 63504ec
✓ Commit 7ccc819
