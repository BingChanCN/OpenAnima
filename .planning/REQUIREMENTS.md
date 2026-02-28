# Requirements: OpenAnima v1.4 Module SDK & DevEx

**Defined:** 2026-02-28
**Core Value:** Agents that proactively think and act on their own, while module connections remain deterministic and safe — intelligence without loss of control.

## v1 Requirements

### SDK Foundation

- [ ] **SDK-01**: Developer can create new module project with `oani new <ModuleName>` command
- [ ] **SDK-02**: Developer can specify output directory with `oani new <ModuleName> -o <path>` option
- [ ] **SDK-03**: Developer can preview generated files with `oani new <ModuleName> --dry-run` option
- [ ] **SDK-04**: Generated module project compiles without errors
- [ ] **SDK-05**: Generated module implements IModule and IModuleMetadata interfaces

### CLI Tool

- [ ] **CLI-01**: Developer can install oani CLI as .NET global tool
- [ ] **CLI-02**: Developer can run `oani --help` to see available commands
- [ ] **CLI-03**: CLI returns exit code 0 on success, non-zero on failure
- [ ] **CLI-04**: CLI outputs errors to stderr, normal output to stdout
- [ ] **CLI-05**: Developer can set verbosity level with `-v` or `--verbosity` option

### Module Pack

- [x] **PACK-01**: Developer can pack module with `oani pack <path>` command
- [x] **PACK-02**: Pack command produces .oamod file containing module.json, DLL, and assets
- [x] **PACK-03**: Pack command builds module project before packing (unless --no-build)
- [x] **PACK-04**: Developer can specify output directory with `oani pack <path> -o <path>` option
- [x] **PACK-05**: Pack command includes SHA256 checksum in package manifest
- [x] **PACK-06**: Packed module can be loaded by OpenAnima runtime without modification

### Module Validate

- [x] **VAL-01**: Developer can validate module with `oani validate <path>` command
- [x] **VAL-02**: Validate command checks module.json exists and is valid JSON
- [x] **VAL-03**: Validate command checks required manifest fields (id, version, name)
- [x] **VAL-04**: Validate command verifies module implements IModule interface
- [x] **VAL-05**: Validate command reports all errors, not just first error

### Manifest Schema

- [ ] **MAN-01**: module.json supports id, version, name, description, author fields
- [ ] **MAN-02**: module.json supports openanima version compatibility (minVersion, maxVersion)
- [ ] **MAN-03**: module.json supports port declarations (inputs, outputs)
- [ ] **MAN-04**: Manifest validation rejects invalid JSON with clear error messages
- [ ] **MAN-05**: Manifest schema is versioned for future compatibility

### Template Customization

- [ ] **TEMP-01**: Developer can specify module type with `--type` option (default: standard)
- [ ] **TEMP-02**: Developer can specify input ports with `--inputs` option (e.g., --inputs Text,Trigger)
- [ ] **TEMP-03**: Developer can specify output ports with `--outputs` option (e.g., --outputs Text)
- [ ] **TEMP-04**: Template generates port attributes based on specified ports
- [ ] **TEMP-05**: Template generates working ExecuteAsync method with port handling stubs

### Documentation

- [ ] **DOC-01**: Developer can read quick-start guide showing create-build-pack workflow
- [ ] **DOC-02**: Quick-start guide produces working module in under 5 minutes
- [ ] **DOC-03**: API reference documents all public interfaces (IModule, IModuleExecutor, ITickable, IEventBus)
- [ ] **DOC-04**: API reference documents port system (PortType, PortMetadata, InputPortAttribute, OutputPortAttribute)
- [ ] **DOC-05**: API reference includes code examples for common patterns

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Example Modules

- **EX-01**: Example module demonstrating text processing (input → transform → output)
- **EX-02**: Example module demonstrating trigger-based execution (tick → action)
- **EX-03**: Example module demonstrating event subscription (subscribe → process → publish)

### Advanced Features

- **ADV-01**: Developer can run `oani run <path>` for local module testing
- **ADV-02**: CLI supports shell completion generation for bash/zsh/pwsh
- **ADV-03**: Module marketplace integration (search, install, publish)

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Module marketplace | Requires backend infrastructure; v1.4 focuses on local packages |
| Automatic versioning | Semantic versioning requires human judgment |
| Code generation wizards | Creates unmaintainable boilerplate |
| Complex project templates | Overwhelming for beginners; minimal template is better |
| Runtime dependency bundling | OpenAnima.Contracts.dll must not be bundled to avoid type identity issues |
| Digital signatures | Requires PKI infrastructure; overkill for local-first platform |
| Example modules | Deferred to v2; quick-start guide covers basic patterns |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| SDK-01 | Phase 20 | Pending |
| SDK-02 | Phase 20 | Pending |
| SDK-03 | Phase 20 | Pending |
| SDK-04 | Phase 20 | Pending |
| SDK-05 | Phase 20 | Pending |
| CLI-01 | Phase 20 | Pending |
| CLI-02 | Phase 20 | Pending |
| CLI-03 | Phase 20 | Pending |
| CLI-04 | Phase 20 | Pending |
| CLI-05 | Phase 20 | Pending |
| PACK-01 | Phase 21 | Complete |
| PACK-02 | Phase 21 | Complete |
| PACK-03 | Phase 21 | Complete |
| PACK-04 | Phase 21 | Complete |
| PACK-05 | Phase 21 | Complete |
| PACK-06 | Phase 21 | Complete |
| VAL-01 | Phase 21 | Complete |
| VAL-02 | Phase 21 | Complete |
| VAL-03 | Phase 21 | Complete |
| VAL-04 | Phase 21 | Complete |
| VAL-05 | Phase 21 | Complete |
| MAN-01 | Phase 20 | Pending |
| MAN-02 | Phase 20 | Pending |
| MAN-03 | Phase 20 | Pending |
| MAN-04 | Phase 20 | Pending |
| MAN-05 | Phase 20 | Pending |
| TEMP-01 | Phase 20 | Pending |
| TEMP-02 | Phase 20 | Pending |
| TEMP-03 | Phase 20 | Pending |
| TEMP-04 | Phase 20 | Pending |
| TEMP-05 | Phase 20 | Pending |
| DOC-01 | Phase 22 | Pending |
| DOC-02 | Phase 22 | Pending |
| DOC-03 | Phase 22 | Pending |
| DOC-04 | Phase 22 | Pending |
| DOC-05 | Phase 22 | Pending |

**Coverage:**
- v1 requirements: 31 total
- Mapped to phases: 31
- Unmapped: 0 ✓

---
*Requirements defined: 2026-02-28*
*Last updated: 2026-02-28 after initial definition*