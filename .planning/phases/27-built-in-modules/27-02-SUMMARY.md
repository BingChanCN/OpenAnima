---
phase: 27-built-in-modules
plan: "02"
subsystem: modules-ui
tags: [llm, config-ui, per-anima, editor-sidebar]
dependency_graph:
  requires: ["27-01"]
  provides: ["BUILTIN-02", "BUILTIN-07", "BUILTIN-08", "BUILTIN-09"]
  affects: ["EditorConfigSidebar", "LLMModule"]
tech_stack:
  added: []
  patterns:
    - "Key-name-based field-type rendering in Blazor (textarea/password/text)"
    - "Per-Anima ChatClient creation per-execution for LLM config override"
    - "API key masking in structured log output (first 4 chars + ***)"
key_files:
  created: []
  modified:
    - src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor
    - src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor.css
    - src/OpenAnima.Core/Modules/LLMModule.cs
decisions:
  - "Per-Anima ChatClient is created per-execution (not cached) — modules are singletons but multiple Animas can have different configs"
  - "All three fields (apiUrl, apiKey, modelName) must be non-empty to activate per-Anima config; partial config falls back entirely to global ILLMService"
  - "template key validation exemption — empty template is a valid state for FixedTextModule"
  - "CompleteWithCustomClientAsync duplicates message mapping from LLMService — keeps module self-contained, avoids tight coupling to LLMService internals"
metrics:
  duration: "3 min"
  completed_date: "2026-03-02"
  tasks: 2
  files_modified: 3
---

# Phase 27 Plan 02: EditorConfigSidebar Field Types + LLMModule Per-Anima Config Summary

**One-liner:** Field-type-aware config sidebar (textarea/password/text by key name) and per-Anima ChatClient creation in LLMModule with three-field all-or-nothing fallback to global ILLMService.

## What Was Built

### Task 1: EditorConfigSidebar field-type-aware rendering

Updated `EditorConfigSidebar.razor` to render different input controls based on config key name:

- `"template"` key → `<textarea rows="6">` (monospace font, vertical resize)
- `"apiKey"` key → `<input type="password">` (masked input)
- All other keys → `<input type="text">` (unchanged behavior)

Updated `HandleConfigChanged` to exempt the `"template"` key from empty-value validation (empty template is a valid state for FixedTextModule — users may want to clear template content).

Added `.config-field textarea` CSS rules to `EditorConfigSidebar.razor.css`:
- Matches existing input styling (surface-dark background, border-color, border-radius, padding, font-size)
- Adds `font-family: monospace` for template content readability
- Adds `resize: vertical` and `width: 100%; box-sizing: border-box` for proper layout
- Adds `:focus` state matching existing input focus styling

### Task 2: LLMModule per-Anima configuration override

Extended `LLMModule` constructor to accept `IAnimaModuleConfigService` and `IAnimaContext` (both registered as singletons in `AnimaServiceExtensions`).

`ExecuteAsync` now checks per-Anima config at execution time:
1. Reads `animaId` from `IAnimaContext.ActiveAnimaId`
2. Calls `IAnimaModuleConfigService.GetConfig(animaId, "LLMModule")`
3. If all three fields (`apiUrl`, `apiKey`, `modelName`) are non-empty → create local `ChatClient` and use `CompleteWithCustomClientAsync`
4. If any field is missing or empty → fall back to global `ILLMService.CompleteAsync`
5. If partial config detected (some but not all fields) → logs debug warning before falling back

`CompleteWithCustomClientAsync` private helper:
- Creates `OpenAIClientOptions` with custom endpoint URI
- Instantiates `ChatClient` with model name and `ApiKeyCredential`
- Maps `ChatMessageInput` records to OpenAI SDK `ChatMessage` subtypes
- Returns `LLMResult` on success or failure
- Masks API key in error logs: `apiKey[..4] + "***"`

## Commits

| Hash | Description |
|------|-------------|
| `9cc592f` | feat(27-02): extend EditorConfigSidebar with textarea and password field types |
| `b51ca6a` | feat(27-02): extend LLMModule with per-Anima LLM config override |

## Decisions Made

1. **Per-execution ChatClient creation (not cached):** LLMModule is a singleton but different Animas may have different LLM configs. Creating ChatClient per-execution is lightweight and avoids stale-config bugs when users update settings between executions.

2. **All-or-nothing per-Anima config fallback:** Partial config (e.g., apiUrl set but apiKey missing) falls back entirely to global ILLMService rather than mixing per-Anima URL with global API key. This prevents accidental credential leakage across config sources.

3. **template key validation exemption:** `HandleConfigChanged` skips empty-value validation for the `"template"` key. An empty template is a valid use case (FixedTextModule produces no output, which may be intentional). Other required fields like `apiUrl`, `apiKey`, `modelName` still require non-empty values.

4. **Self-contained message mapping:** `CompleteWithCustomClientAsync` reimplements the `ChatMessageInput` → `ChatMessage` mapping from `LLMService` rather than extracting a shared utility. This keeps LLMModule independent of LLMService internals and avoids coupling two independently-evolvable components.

## Deviations from Plan

None — plan executed exactly as written.

## Verification Results

All 8 verification criteria passed:
1. `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj` — Build succeeded, 0 warnings, 0 errors
2. EditorConfigSidebar renders `<textarea>` for "template" config key
3. EditorConfigSidebar renders `<input type="password">` for "apiKey" config key
4. EditorConfigSidebar allows empty "template" values without validation error
5. LLMModule constructor accepts IAnimaModuleConfigService and IAnimaContext
6. LLMModule.ExecuteAsync reads per-Anima config and creates ChatClient when all three fields present
7. LLMModule falls back to global ILLMService when any field is missing
8. API key is masked in log output (first 4 chars + "***")

## Self-Check

### Files exist:
- `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor` - FOUND
- `src/OpenAnima.Core/Components/Shared/EditorConfigSidebar.razor.css` - FOUND
- `src/OpenAnima.Core/Modules/LLMModule.cs` - FOUND

### Commits exist:
- `9cc592f` - FOUND
- `b51ca6a` - FOUND

## Self-Check: PASSED
