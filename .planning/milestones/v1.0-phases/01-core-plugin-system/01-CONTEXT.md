# Phase 1: Core Plugin System - Context

**Gathered:** 2026-02-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Developers can create and load C# modules with typed interfaces. Modules load in isolation via AssemblyLoadContext, declare capabilities through typed contracts, and register in a module registry. Inter-module event bus (Phase 2) and visual editor (Phase 5) are out of scope.

</domain>

<decisions>
## Implementation Decisions

### Module Contract Design
- Module capabilities and metadata (name, version, description) declared through interface properties — not Attributes
- Contracts shared via a common SDK package that module developers reference
- Hybrid interaction model: contracts support both synchronous method calls and async event patterns
  - Phase 1 implements the contract interfaces; Phase 2 wires up the actual event bus

### Module Packaging & Distribution
- Custom package format: module folder/zip containing DLL + manifest file
- Only module DLL included in package — third-party dependencies resolved by the system
- Zero-config installation: drop module folder into designated directory, system auto-detects

### Module Discovery & Loading
- Hot discovery: system watches module directory, auto-loads new modules when added
- Manual refresh button available as fallback
- Single fixed module directory (e.g., ./modules/)
- Modules have an Initialize hook called automatically on load
- Load failures prompt the user with error details (not silent skip)

### Claude's Discretion
- Input/output interface granularity (single vs multi-port)
- Manifest file format (JSON vs YAML vs other)
- Loading skeleton and error state UI details
- Exact dependency resolution strategy
- AssemblyLoadContext isolation implementation details

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 01-core-plugin-system*
*Context gathered: 2026-02-21*
