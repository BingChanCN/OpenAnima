# Pitfalls Research

**Domain:** Module SDK & CLI Tool Development for Plugin Systems
**Researched:** 2026-02-28
**Confidence:** HIGH

## Critical Pitfalls

### Pitfall 1: The "Works on My Machine" Template Syndrome

**What goes wrong:**
A dotnet new template works for the author but fails for other developers due to hardcoded paths, missing prerequisites, or environment-specific configurations.

**Why it happens:**
Template authors test only in their own environment and don't account for different .NET SDK versions, OS configurations, or missing tools.

**How to avoid:**
- Include a `prerequisites` check that validates .NET SDK version before template instantiation
- Use `sourceName` in template.json for all project/namespace names, not hardcoded values
- Test templates in clean VMs/containers before releasing
- Never hardcode absolute paths - use relative paths and project variables

**Warning signs:**
- Template contains hardcoded "MyProject" or similar placeholder strings
- Template fails on different .NET SDK versions
- Generated project requires manual configuration after `dotnet new`

**Phase to address:**
Phase 1 (CLI & Template) - Build validation into the template itself

---

### Pitfall 2: CLI Exit Code Confusion

**What goes wrong:**
CLI tool returns zero exit code on failure (or non-zero on success), breaking script automation and CI/CD pipelines that expect standard Unix conventions.

**Why it happens:**
Developers unfamiliar with CLI conventions treat exit codes as afterthoughts or use them for debug information instead of success/failure signaling.

**How to avoid:**
- Return zero on success, non-zero on failure - ALWAYS
- Use different non-zero codes for different failure types (1 for user error, 2 for system error, etc.)
- Document exit codes in help text
- Test CLI in shell scripts that check `$?`

**Warning signs:**
- CLI prints "Error:" but returns exit code 0
- CI/CD pipelines pass even when CLI commands fail
- No documentation of exit codes

**Phase to address:**
Phase 1 (CLI & Template) - Establish exit code contract from day one

---

### Pitfall 3: Streaming Errors to Wrong Output

**What goes wrong:**
CLI tool sends primary output to `stderr` or logs errors to `stdout`, breaking pipes and making the tool unusable in scripts.

**Why it happens:**
Developers treat `stderr` as a general "logging" stream instead of reserving it for diagnostic/error messages.

**How to avoid:**
- Send program output to `stdout` - this is what gets piped
- Send errors/progress/logging to `stderr` - this is what humans see
- Provide `--quiet`, `--json`, and `--plain` flags for different use cases
- Never print "info" messages to `stdout` when in piped mode

**Warning signs:**
- `oani pack | cat` shows progress bars or logging
- Scripts receive mixed output/error content
- `--quiet` doesn't suppress all non-essential output

**Phase to address:**
Phase 1 (CLI & Template) - Define output stream contract early

---

### Pitfall 4: Missing Manifest Validation

**What goes wrong:**
.oamod packages with invalid manifests are created and distributed, causing cryptic load failures in the runtime that blame the wrong component.

**Why it happens:**
Manifest validation is often an afterthought - developers assume "it works when I test it" without comprehensive schema validation.

**How to avoid:**
- Define a strict JSON schema for the manifest (like NuGet's nuspec.xsd)
- Validate manifest at `oani pack` time - fail fast with clear errors
- Include required fields: id, version, entryPoint, compatibleRuntime, description
- Add checksums for all included assemblies in the manifest
- Provide `oani validate` command for pre-distribution verification

**Warning signs:**
- Modules load successfully in dev but fail in production
- Error messages say "module not found" when the issue is corrupt manifest
- No checksum validation when loading packages

**Phase to address:**
Phase 2 (Package Format) - Build validation into pack command

---

### Pitfall 5: Version Compatibility Hell

**What goes wrong:**
A module built for runtime v1.3 doesn't work with v1.4, but the error message doesn't explain why. Users blame the module author or the platform.

**Why it happens:**
No explicit version compatibility declaration in the package format. The runtime tries to load incompatible assemblies and fails with confusing errors.

**How to avoid:**
- Require `compatibleRuntime` field in manifest (e.g., ">=1.3.0 <2.0.0")
- Check compatibility BEFORE loading assemblies - fail with clear message
- Use semantic versioning: major = breaking changes, minor = features, patch = fixes
- Document version compatibility requirements in module template
- Provide `oani check-compat` command for testing

**Warning signs:**
- Modules load in one runtime version but crash in another
- Type conversion errors (same type name, different assembly context)
- Error messages mention "AssemblyLoadContext" without explaining version mismatch

**Phase to address:**
Phase 2 (Package Format) - Version contract must be defined early

---

### Pitfall 6: AssemblyLoadContext Type Identity Issues

**What goes wrong:**
Module passes an object to runtime, runtime tries to cast it to an interface, but cast fails with "Object of type 'X' cannot be converted to type 'X'" - same name, different contexts.

**Why it happens:**
When assemblies are loaded in different AssemblyLoadContext instances, types with the same name are NOT the same type. This is the most common plugin architecture pitfall in .NET.

**How to avoid:**
- All shared interfaces MUST be in a shared contract assembly loaded in Default context
- Use duck-typing (reflection) for cross-context type checking (OpenAnima already does this for ITickable)
- Never pass implementation types across context boundaries - use interfaces or serialized data
- Document this clearly for module developers

**Warning signs:**
- "Object of type 'X' cannot be converted to type 'X'" errors
- Module works in isolation but fails when loaded with other modules
- Type casts that should work mysteriously fail

**Phase to address:**
Phase 3 (Documentation) - This MUST be documented prominently for module developers

---

### Pitfall 7: Documentation That Assumes Expert Knowledge

**What goes wrong:**
Documentation jumps straight to advanced concepts without explaining basics. New developers give up and abandon the platform.

**Why it happens:**
Documentation written by experts who forgot what it's like to be a beginner. "Getting Started" becomes "Reference Guide."

**How to avoid:**
- Lead with examples, not concepts (users copy examples first, read docs second)
- Provide a "5-minute quickstart" that produces a working module
- Explain the "why" before the "how"
- Include troubleshooting section for common errors
- Test docs with actual new users

**Warning signs:**
- Quickstart takes more than 10 minutes
- First documentation page mentions advanced concepts
- No working code example in the first 3 paragraphs
- Documentation assumes knowledge of AssemblyLoadContext, dependency injection, etc.

**Phase to address:**
Phase 3 (Documentation) - Documentation should be tested with new developers

---

### Pitfall 8: Overly Verbose or Silent CLI Output

**What goes wrong:**
CLI either floods the terminal with information or provides no feedback, leaving users confused about whether commands succeeded or what they did.

**Why it happens:**
No clear philosophy on output verbosity. Developers add logging for debugging and never remove it, or follow "UNIX tradition of silence" too literally.

**How to avoid:**
- Default output: brief confirmation of what happened
- `--verbose` flag: show detailed progress
- `--quiet` flag: suppress all non-error output
- Always confirm state changes ("Created module 'MyModule' at ./MyModule")
- Show progress for long operations (packing large assemblies)

**Warning signs:**
- CLI command completes with no output (user wonders "did it work?")
- CLI prints more than 5 lines for a simple operation
- No indication of what files were created/modified

**Phase to address:**
Phase 1 (CLI & Template) - Define output philosophy early

---

### Pitfall 9: Template Version Drift

**What goes wrong:**
The dotnet new template generates code that's incompatible with the current runtime because the template wasn't updated when the runtime changed.

**Why it happens:**
Templates are often maintained separately from the main codebase and fall out of sync.

**How to avoid:**
- Store templates in the same repository as the runtime
- Add template update to the PR checklist when changing module interfaces
- Include runtime version in template manifest
- Test template-generated modules against latest runtime in CI
- Version templates alongside runtime releases

**Warning signs:**
- Generated module doesn't compile with latest SDK
- Generated module compiles but crashes at runtime
- Template produces code referencing deprecated APIs

**Phase to address:**
Phase 1 (CLI & Template) - Establish template maintenance process

---

### Pitfall 10: No Graceful Degradation for Missing Features

**What goes wrong:**
CLI or template fails with cryptic error when a feature isn't available in the user's environment, instead of offering alternatives.

**Why it happens:**
Error handling focuses on "what went wrong" internally, not "what can the user do about it."

**How to avoid:**
- Check prerequisites early and provide clear guidance
- Suggest alternatives when possible ("SDK 8.0 not found. Install from https://...")
- Provide helpful error messages, not stack traces
- Include "what to try next" in error output

**Warning signs:**
- Errors show stack traces instead of actionable messages
- Errors don't suggest how to fix the problem
- Users need to search the web to understand errors

**Phase to address:**
All phases - This is an ongoing quality requirement

---

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Skip manifest validation | Faster pack command | Cryptic runtime failures | Never |
| Copy internal types to template | Easier template creation | Breaking changes affect all modules | Never |
| Use string-based type matching | Simpler cross-context code | Runtime errors, no compile safety | Only for truly dynamic scenarios |
| Skip version compatibility check | Load any module | Incompatibility crashes | Never |
| No --verbose flag | Less code to write | Debugging nightmare | Never |
| Skip checksums in manifest | Simpler package format | No tamper detection, load corrupted modules | Never |
| Assume .NET SDK installed | Skip prerequisite check | Confusing errors for new users | MVP only - add checks before release |

## Integration Gotchas

Common mistakes when connecting SDK to existing plugin system.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Module interface | Exposing runtime internals to modules | Use contract interfaces only, keep implementation private |
| Dependency injection | Each module gets new service instances | Register modules as singletons, use shared EventBus |
| Assembly loading | Loading all dependencies in same context | Use PluginLoadContext with isCollectible:true for isolation |
| Configuration | Putting config in template-generated code | Use appsettings.json pattern, inject IConfiguration |
| Logging | Modules log directly to console | Inject ILogger, let runtime control output format |
| Error handling | Modules throw exceptions for all errors | Return Result types for expected failures, throw for unexpected |

## Performance Traps

Patterns that work at small scale but fail as usage grows.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Large assemblies in package | Slow load times, memory usage | Strip debug symbols, use trimming | Package > 10MB |
| No module dependency resolution | Missing dependency errors | Include transitive dependencies in manifest | Modules with 3+ dependencies |
| Sync loading all modules at startup | Slow app startup | Lazy load on-demand, parallel load | 10+ modules installed |
| No module caching | Re-download on every load | Cache .oamod files locally | Network-based module source |

## Security Mistakes

Domain-specific security issues beyond general web security.

| Mistake | Risk | Prevention |
|---------|------|------------|
| Unsigned packages | Tampering, supply chain attacks | Sign .oamod packages, verify signature on load |
| No sandbox for modules | Malicious code execution | Already addressed: modules run in-process but can be limited |
| Secrets in manifest | Credential exposure | Never include secrets in .oamod, use runtime configuration |
| Arbitrary code in templates | Template security | `dotnet new` already warns about untrusted templates |
| Module ID collision | Impersonation | Verify unique ID, consider namespacing convention |

## UX Pitfalls

Common user experience mistakes in CLI/SDK development.

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| No default values for CLI args | User must provide everything | Provide sensible defaults, prompt only when necessary |
| Inconsistent flag names (-v vs --verbose) | Confusion, mistakes | Use standard flags: -h, -v, -q, -n, --help, --verbose, --quiet |
| No --dry-run for destructive operations | Fear of running commands | Add --dry-run to oani pack, oani install |
| Help text without examples | User must read docs to use tool | Lead help text with 1-2 examples |
| Error messages in English only | International users | Support localization (dotnet templates support this) |
| No confirmation for overwrite | Accidental data loss | Confirm before overwriting existing files |

## "Looks Done But Isn't" Checklist

Things that appear complete but are missing critical pieces.

- [ ] **CLI Tool:** Often missing proper exit codes - verify `echo $?` returns 0 on success, non-zero on failure
- [ ] **CLI Tool:** Often missing TTY detection - verify output differs when piped vs interactive
- [ ] **Template:** Often missing version compatibility - verify generated module specifies runtime version
- [ ] **Template:** Often missing localization support - verify template.json has localization structure
- [ ] **Package Format:** Often missing checksums - verify manifest includes SHA256 for assemblies
- [ ] **Package Format:** Often missing signature verification - verify tampered package fails to load
- [ ] **Documentation:** Often missing troubleshooting section - verify common errors are documented
- [ ] **Documentation:** Often missing API reference - verify all public interfaces are documented
- [ ] **Documentation:** Often missing version compatibility guide - verify breaking changes are listed

## Recovery Strategies

When pitfalls occur despite prevention, how to recover.

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Template version drift | MEDIUM | Update template, regenerate affected modules, release new runtime |
| Missing manifest validation | HIGH | Add validation, reject invalid packages, update all existing packages |
| Type identity issues | HIGH | Refactor to use shared interfaces, may require runtime changes |
| Documentation gaps | LOW | Add missing docs incrementally, prioritize by error frequency |
| CLI output philosophy | MEDIUM | Add --verbose/--quiet flags, adjust default output |
| No checksum validation | MEDIUM | Add checksums to manifest, version bump package format |

## Pitfall-to-Phase Mapping

How roadmap phases should address these pitfalls.

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Template "works on my machine" | Phase 1 (CLI & Template) | Test template in clean VM |
| CLI exit code confusion | Phase 1 (CLI & Template) | Test in shell scripts checking $? |
| Wrong output stream | Phase 1 (CLI & Template) | Test `oani pack \| cat` for clean output |
| Missing manifest validation | Phase 2 (Package Format) | Attempt to load invalid manifest |
| Version compatibility hell | Phase 2 (Package Format) | Load module with wrong runtime version |
| AssemblyLoadContext issues | Phase 3 (Documentation) | Verify module docs explain this clearly |
| Documentation assumes experts | Phase 3 (Documentation) | Test docs with new developers |
| Verbose/silent CLI output | Phase 1 (CLI & Template) | User testing with default, --verbose, --quiet |
| Template version drift | Phase 1 (CLI & Template) | CI test template against latest runtime |
| No graceful degradation | All phases | Error message review in PR checklist |

## Sources

- [Command Line Interface Guidelines (clig.dev)](https://clig.dev/) - Comprehensive CLI design best practices and anti-patterns (HIGH confidence)
- [Custom templates for dotnet new - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/custom-templates) - Template authoring reference (HIGH confidence)
- [.nuspec File Reference - Microsoft Learn](https://learn.microsoft.com/en-us/nuget/reference/nuspec) - Package manifest design patterns (HIGH confidence)
- [About AssemblyLoadContext - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext) - Plugin isolation and type identity issues (HIGH confidence)
- [What is NuGet - Microsoft Learn](https://learn.microsoft.com/en-us/nuget/what-is-nuget) - Package management lessons learned (HIGH confidence)
- OpenAnima PROJECT.md - Existing architecture decisions and validated requirements

---
*Pitfalls research for: Module SDK & CLI Tool Development*
*Researched: 2026-02-28*