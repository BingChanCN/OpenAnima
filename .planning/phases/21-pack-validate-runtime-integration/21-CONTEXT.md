# Phase 21: Pack, Validate & Runtime Integration - Context

**Gathered:** 2026-02-28
**Status:** Ready for planning

<domain>
## Phase Boundary

Developer can validate modules and pack them into distributable `.oamod` files. Packed modules load in OpenAnima runtime without modification. This phase covers the pack/validate CLI commands and runtime loading mechanism.

</domain>

<decisions>
## Implementation Decisions

### .oamod Package Format
- **Package contents**: Module DLL + manifest.json (minimal, essential files)
- **Internal structure**: ZIP format (renamed to .oamod) - simple and universal
- **Checksum algorithm**: MD5 for integrity verification
- **Version metadata**: Module version, target platform version, target framework

### Runtime Loading Mechanism
- **Load location**: Dedicated `modules/` directory - automatic discovery and loading
- **Assembly isolation**: Each module in separate AssemblyLoadContext to avoid dependency conflicts
- **Dependency handling**: Share OpenAnima core dependencies, reduce package size
- **Load timing**: Hot reload support - modules can be added/removed at runtime
- **Unload strategy**: Safe unload - wait for all references to be released before unloading

### Claude's Discretion
- Exact ZIP internal layout and file naming conventions
- Checksum file format and location within the package
- Module discovery mechanism (file system watcher vs polling)
- Error handling for corrupted or incompatible packages
- Validation command output format and error reporting style

</decisions>

<specifics>
## Specific Ideas

- Hot reload enables rapid development workflow - developer can update module and see changes without restart
- AssemblyLoadContext isolation ensures modules don't interfere with each other's dependencies
- Minimal package format keeps things simple - just what's needed to run

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope.

</deferred>

---

*Phase: 21-pack-validate-runtime-integration*
*Context gathered: 2026-02-28*