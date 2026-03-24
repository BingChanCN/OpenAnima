---
phase: 64-port-hover-tooltips
plan: 01
subsystem: wiring-editor
tags: [i18n, ux, tooltips, svg, resx]
dependency_graph:
  requires: [Phase 61 Module.DisplayName.* pattern, IStringLocalizer<SharedResources>]
  provides: [Port.Description.* .resx keys, GetPortTooltip helper, SVG port tooltips]
  affects: [NodeCard.razor, SharedResources.*.resx]
tech_stack:
  added: []
  patterns: [SVG title element for browser-native tooltip, ResourceNotFound fallback pattern]
key_files:
  created: []
  modified:
    - src/OpenAnima.Core/Resources/SharedResources.resx
    - src/OpenAnima.Core/Resources/SharedResources.en-US.resx
    - src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx
    - src/OpenAnima.Core/Components/Shared/NodeCard.razor
decisions:
  - key: port-tooltip-mechanism
    summary: "Used SVG <title> child elements for port tooltips — browser-native, zero JS, auto-dismisses on mousedown so drag-to-connect is unaffected"
  - key: http-body-disambiguation
    summary: "HttpRequestModule 'body' port (exists as both input and output) disambiguated via Port.Description.HttpRequestModule.body.{Direction} key pattern"
  - key: key-count-discrepancy
    summary: "Plan acceptance_criteria said 40 keys but the plan's actual key list contains 39 unique keys; all listed keys were added; no key was omitted"
metrics:
  duration: 4min
  completed: 2026-03-24
  tasks: 2
  files_modified: 4
---

# Phase 64 Plan 01: Port Hover Tooltips Summary

Browser-native SVG tooltips on all wiring editor port circles, backed by 39 Port.Description.* i18n keys in all three resource files with Chinese and English values.

## Tasks Completed

| Task | Description | Commit | Files |
|------|-------------|--------|-------|
| 1 | Add Port.Description.* .resx keys to all three resource files | dcbfb39 | SharedResources.resx, SharedResources.en-US.resx, SharedResources.zh-CN.resx |
| 2 | Add GetPortTooltip helper and SVG title elements to NodeCard.razor port circles | a940740 | NodeCard.razor |

## What Was Built

39 `Port.Description.*` .resx keys added to all three resource files covering all 15 built-in modules. Each key maps to an English description (fallback + en-US) and a Chinese description (zh-CN).

`GetPortTooltip(PortMetadata port)` method added to NodeCard.razor after the existing `GetDisplayName` pattern:
- Looks up `Port.Description.{ModuleName}.{PortName}` via `IStringLocalizer<SharedResources>`
- For HttpRequestModule "body" port specifically: looks up `Port.Description.HttpRequestModule.body.{Direction}` (Input or Output) to avoid collision
- Falls back to `"{portName} ({portType})"` if key not found (ResourceNotFound)
- Returns `"{portName}: {description} ({portType})"` when description found

Both input port circles and output port circles now contain `<title>@GetPortTooltip(port)</title>` as a child element, enabling browser-native hover tooltips.

## Deviations from Plan

None - plan executed exactly as written.

The acceptance_criteria stated "40 keys" but the plan's actual key list contained exactly 39 unique keys. All listed keys were added. This is a documentation inconsistency in the plan spec (not a deviation in execution).

## Verification Results

- `dotnet build src/OpenAnima.Core/OpenAnima.Core.csproj --no-restore`: 0 errors, 0 warnings
- `dotnet test tests/OpenAnima.Tests/ --no-restore`: 658 passed, 0 failed
- zh-CN.resx: 39 Port.Description keys present
- en-US.resx: 39 Port.Description keys present
- SharedResources.resx: 39 Port.Description keys present
- NodeCard.razor: 2 occurrences of `<title>@GetPortTooltip(port)</title>`
- NodeCard.razor: GetPortTooltip method with ResourceNotFound fallback confirmed

## Self-Check: PASSED
