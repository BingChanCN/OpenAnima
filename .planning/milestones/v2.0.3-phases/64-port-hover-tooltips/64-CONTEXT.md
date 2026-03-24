# Phase 64: Port Hover Tooltips - Context

**Gathered:** 2026-03-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can hover over port circles on node cards in the wiring editor to see a Chinese description tooltip explaining what each port does. All ~38 ports across 15 built-in modules get Chinese description text via .resx keys. Tooltip format includes port name, description, and type. Ports without descriptions fall back to port name and type only. Tooltips must not interfere with drag-to-connect interaction on port circles.

</domain>

<decisions>
## Implementation Decisions

### Tooltip rendering approach
- Use SVG `<title>` child element inside each port `<circle>` in NodeCard.razor
- Browser-native tooltip — no custom tooltip component, no JS interop needed
- REQUIREMENTS.md Out of Scope explicitly confirms: "Rich styled port tooltips with type badge and color — SVG `<title>` is sufficient for v2.0.3"
- SVG `<title>` auto-dismisses on mousedown — no interference with drag-to-connect (pointer events pass through to circle)

### Port description .resx key pattern
- Pattern: `Port.Description.{ModuleName}.{PortName}` (e.g., `Port.Description.LLMModule.prompt`)
- Consistent with established conventions: `Module.DisplayName.{ClassName}` (Phase 61), `Module.Description.{ClassName}` (Phase 63)
- Add keys to all three .resx files: SharedResources.resx (fallback), SharedResources.en-US.resx, SharedResources.zh-CN.resx
- Chinese descriptions are the primary requirement (EDUX-04); English descriptions also added for completeness

### Tooltip text format
- Format per Success Criteria #3: "portName: description (Type)"
- Example: "prompt: System prompt text (Text)" / "prompt: LLM system prompt (Text)"
- Chinese example: "prompt: LLM system prompt (Text)"
- Port type suffix uses the PortType enum name (Text, Trigger) — not localized
- Fallback (SC#4): ports without .resx description show "portName (Type)" only

### Port enumeration (all ~38 ports across 15 modules)
- LLMModule: messages (in), prompt (in), response (out), error (out)
- ChatInputModule: userMessage (out)
- ChatOutputModule: displayText (in)
- HeartbeatModule: tick (out)
- FixedTextModule: trigger (in), output (out)
- TextJoinModule: input1 (in), input2 (in), input3 (in), output (out)
- TextSplitModule: input (in), output (out)
- ConditionalBranchModule: input (in), true (out), false (out)
- AnimaInputPortModule: request (out)
- AnimaOutputPortModule: response (in)
- AnimaRouteModule: request (in), trigger (in), response (out), error (out)
- HttpRequestModule: body (in), trigger (in), body (out), statusCode (out), error (out)
- JoinBarrierModule: input_1 (in), input_2 (in), input_3 (in), input_4 (in), output (out)
- MemoryModule: query (in), write (in), result (out)
- WorkspaceToolModule: invoke (in), result (out)

### Tooltip resolution in NodeCard.razor
- Add helper method `GetPortTooltip(PortMetadata port)` in NodeCard.razor @code block
- Uses `L[$"Port.Description.{Node.ModuleName}.{port.Name}"]` with ResourceNotFound fallback
- If description found: return `"{port.Name}: {description} ({port.Type})"`
- If no description: return `"{port.Name} ({port.Type})"`
- IStringLocalizer already injected in NodeCard.razor — no new injection needed
- LanguageService.LanguageChanged already subscribed — tooltips update on language switch

### Claude's Discretion
- Exact Chinese description text for each of the ~38 ports (should be concise, functional, describing what data flows through the port)
- Whether to wrap `<title>` in a helper method or inline it directly in the Razor loop
- Whether HttpRequestModule output port "body" needs disambiguation from input port "body" in the .resx key (same port name, different direction — key pattern may need direction suffix like `Port.Description.HttpRequestModule.body.Input`)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Port System (Contracts layer)
- `src/OpenAnima.Contracts/Ports/PortMetadata.cs` — Port record: Name, Type, Direction, ModuleName
- `src/OpenAnima.Contracts/Ports/InputPortAttribute.cs` — Input port declaration attribute (Name, Type only — no Description)
- `src/OpenAnima.Contracts/Ports/OutputPortAttribute.cs` — Output port declaration attribute (Name, Type only — no Description)
- `src/OpenAnima.Contracts/Ports/PortType.cs` — Port type enum (Text, Trigger)

### Target Surface (must modify)
- `src/OpenAnima.Core/Components/Shared/NodeCard.razor` — Lines 60-85: port circle rendering loops; add `<title>` child to each `<circle>` element; GetDisplayName helper at line 200 shows resolution pattern

### i18n Infrastructure (from Phase 61)
- `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` — Chinese translations; add Port.Description.* keys
- `src/OpenAnima.Core/Resources/SharedResources.en-US.resx` — English translations; add Port.Description.* keys
- `src/OpenAnima.Core/Resources/SharedResources.resx` — Fallback resource file; add Port.Description.* keys
- `src/OpenAnima.Core/Resources/SharedResources.cs` — Marker class for IStringLocalizer<SharedResources>

### Port Registry
- `src/OpenAnima.Core/Ports/IPortRegistry.cs` — GetPorts(moduleName) returns List<PortMetadata>
- `src/OpenAnima.Core/Ports/PortRegistry.cs` — Implementation that scans module attributes

### Built-in Module Port Declarations (all files with [InputPort]/[OutputPort] attributes)
- `src/OpenAnima.Core/Modules/LLMModule.cs` — Lines 31-34: messages, prompt, response, error
- `src/OpenAnima.Core/Modules/ChatInputModule.cs` — Line 13: userMessage
- `src/OpenAnima.Core/Modules/ChatOutputModule.cs` — Line 12: displayText
- `src/OpenAnima.Core/Modules/HeartbeatModule.cs` — Line 13: tick
- `src/OpenAnima.Core/Modules/FixedTextModule.cs` — Lines 13-14: trigger, output
- `src/OpenAnima.Core/Modules/TextJoinModule.cs` — Lines 13-16: input1-3, output
- `src/OpenAnima.Core/Modules/TextSplitModule.cs` — Lines 13-14: input, output
- `src/OpenAnima.Core/Modules/ConditionalBranchModule.cs` — Lines 22-24: input, true, false
- `src/OpenAnima.Core/Modules/AnimaInputPortModule.cs` — Line 13: request
- `src/OpenAnima.Core/Modules/AnimaOutputPortModule.cs` — Line 12: response
- `src/OpenAnima.Core/Modules/AnimaRouteModule.cs` — Lines 16-19: request, trigger, response, error
- `src/OpenAnima.Core/Modules/HttpRequestModule.cs` — Lines 18-22: body(in), trigger, body(out), statusCode, error
- `src/OpenAnima.Core/Modules/JoinBarrierModule.cs` — Lines 18-22: input_1-4, output
- `src/OpenAnima.Core/Modules/MemoryModule.cs` — Lines 14-16: query, write, result
- `src/OpenAnima.Core/Modules/WorkspaceToolModule.cs` — Lines 14-15: invoke, result

### Phase Dependencies
- `.planning/phases/61-module-i18n-foundation/61-CONTEXT.md` — Established .resx key conventions, ResourceNotFound fallback pattern
- `.planning/phases/63-module-descriptions/63-CONTEXT.md` — Module.Description.* pattern, GetDescription helper approach

### Requirements
- `.planning/REQUIREMENTS.md` — EDUX-04 definition, Out of Scope: "Rich styled port tooltips with type badge and color — SVG `<title>` is sufficient for v2.0.3"

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `GetDisplayName(string moduleName)` in NodeCard.razor (line 200-204): Exact pattern to replicate for `GetPortTooltip()` — uses `L[$"Module.DisplayName.{moduleName}"]` with ResourceNotFound fallback
- `IStringLocalizer<SharedResources>` injection: Already present in NodeCard.razor (line 10)
- `LanguageService.LanguageChanged` subscription: Already wired in NodeCard.razor (line 107) — tooltips auto-update on language switch
- `PortColors.GetHex(port.Type)`: Used in port circle rendering — port.Type available in loop context for tooltip format

### Established Patterns
- SVG `<title>` already used in NodeCard.razor line 16 for the overall card tooltip (GetStatusTooltip)
- Port circle rendering uses `@foreach` loops with `(port, index)` tuples — `<title>` goes inside the `<circle>` element
- ResourceNotFound fallback: `localized.ResourceNotFound ? fallbackValue : localized.Value` pattern

### Integration Points
- NodeCard.razor lines 65-68: Input port `<circle>` elements — add `<title>` child
- NodeCard.razor lines 78-82: Output port `<circle>` elements — add `<title>` child
- Note: Input port circles have `@onmouseup` handler; Output port circles have `@onmousedown` handler — SVG `<title>` does not interfere with these event handlers
- HttpRequestModule has duplicate port name "body" for both input and output — .resx key may need direction qualifier

</code_context>

<specifics>
## Specific Ideas

- Success Criteria #3 explicitly specifies tooltip format: "prompt: System prompt text (Text)" — follow this exactly
- STATE.md blocker note resolved: "SVG `<title>` vs custom SVG overlay" — REQUIREMENTS.md Out of Scope confirms SVG `<title>` is the approach
- Port descriptions should be concise and functional (1-5 words explaining data flow purpose, not module documentation)
- HttpRequestModule has "body" as both input and output port name — may need `Port.Description.HttpRequestModule.body.Input` / `Port.Description.HttpRequestModule.body.Output` or handle by direction in the lookup

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 64-port-hover-tooltips*
*Context gathered: 2026-03-24*
