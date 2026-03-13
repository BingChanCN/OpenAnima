---
phase: 30-prompt-injection-and-format-detection
verified: 2026-03-13T14:30:00Z
status: passed
score: 15/15 must-haves verified
re_verification: false
human_verification:
  - test: "End-to-end LLM prompt injection with real LLM client"
    expected: "System message containing service list and <route> format instructions is visible in actual LLM API request; passthrough text appears in chat UI, route payload triggers downstream Anima"
    why_human: "Cannot verify real LLM API call contents or downstream Anima response path without a live environment"
---

# Phase 30: Prompt Injection and Format Detection — Verification Report

**Phase Goal:** Build prompt injection system with format detection for LLM-driven cross-Anima routing
**Verified:** 2026-03-13T14:30:00Z
**Status:** PASSED
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | FormatDetector.Detect finds well-formed `<route service="x">payload</route>` markers in LLM output | VERIFIED | `Detect_SingleWellFormedMarker_ExtractsRouteAndStripsMarker` test + regex implementation in `FormatDetector.cs:41` |
| 2  | FormatDetector strips matched markers from passthrough text and returns cleaned text | VERIFIED | `RouteMarkerRegex.Replace` returns `string.Empty` for matched markers; `passthrough.Trim()` applied |
| 3  | FormatDetector returns MalformedMarkerError for unclosed route tags | VERIFIED | `UnclosedMarkerRegex` fast-path at line 71; `Detect_UnclosedRouteTag_ReturnsMalformedErrorWithUnclosed` test passes |
| 4  | FormatDetector returns MalformedMarkerError for unrecognized service names | VERIFIED | Foreach loop against `knownServiceNames` at line 91; sets `malformedError` when `resolvedName == null` |
| 5  | Case-insensitive service name matching works | VERIFIED | `StringComparer.OrdinalIgnoreCase` in `Services()` helper + `StringComparison.OrdinalIgnoreCase` in Detect; 2 tests cover this |
| 6  | Multiple route markers in a single response are all extracted in order | VERIFIED | `Detect_MultipleMarkers_ExtractsAllInOrder` passes; `RegexOptions.None` preserves document order |
| 7  | A response with no route markers returns empty routes list and null error | VERIFIED | `Detect_PlainText_ReturnsUnchangedTextEmptyRoutesNoError` and `Detect_EmptyString_ReturnsEmptyTextEmptyRoutesNoError` pass |
| 8  | LLMModule injects system message with service list and routing format when AnimaRoute is configured | VERIFIED | `BuildSystemMessage()` at line 246; `LLMModule_WithAnimaRouteConfig_InjectsSystemMessageWithServiceList` test asserts system+user message pair and format content |
| 9  | LLMModule sends no system message when no AnimaRoute configured | VERIFIED | `BuildKnownServiceNames()` returns empty set when router null or no config; `LLMModule_WithNoAnimaRouteConfig_DoesNotInjectSystemMessage` test asserts single user message |
| 10 | Route payloads dispatched to AnimaRouteModule.port.request then .port.trigger (request first) | VERIFIED | `DispatchRoutesAsync()` at line 285 — request published before trigger; `LLMModule_WithValidRouteMarker_DispatchesToAnimaRouteModuleAndPublishesPassthrough` asserts `requestReceivedFirst == true` |
| 11 | Passthrough text (outside markers) published to response port with markers stripped | VERIFIED | `PublishResponseAsync(detection.PassthroughText)` after `_formatDetector.Detect()`; test asserts `DoesNotContain("<route")` |
| 12 | Malformed markers trigger self-correction cycle — error feedback sent back to LLM, up to 2 retries | VERIFIED | While loop in `ExecuteAsync` appends assistant+user correction turns; `LLMModule_WithMalformedThenValidMarker_Retries_AndDispatchesCorrectly` asserts `CallCount == 2` |
| 13 | After 2 failed retries, error published to LLMModule.port.error | VERIFIED | `attempt >= MaxRetries` branch publishes to `LLMModule.port.error`; `LLMModule_WithPersistentMalformedMarker_PublishesToErrorPortAfterMaxRetries` asserts `CallCount == 3` and error received |
| 14 | LLMModule constructor accepts optional ICrossAnimaRouter for backward compatibility | VERIFIED | `ICrossAnimaRouter? router = null` in constructor at line 49; `LLMModule_WithNoRouter_BehavesAsOriginal` test passes |
| 15 | No token budget cap applied — all services injected regardless of count | VERIFIED | `BuildSystemMessage` injects all ports via `string.Join`; no truncation or count limit anywhere in implementation |

**Score:** 15/15 truths verified

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/OpenAnima.Core/Modules/FormatDetector.cs` | Pure detection class: regex-based XML marker parsing | VERIFIED | 119 lines; exports `FormatDetector`, `FormatDetectionResult`, `RouteExtraction`; file-scoped namespace `OpenAnima.Core.Modules` |
| `tests/OpenAnima.Tests/Unit/FormatDetectorTests.cs` | Comprehensive unit tests (min 80 lines) | VERIFIED | 218 lines; 16 test methods covering all specified behaviors |
| `src/OpenAnima.Core/Modules/LLMModule.cs` | Extended with prompt injection, FormatDetector, self-correction, error port | VERIFIED | 372 lines; `[OutputPort("error", PortType.Text)]` present at line 26; all five extended methods implemented |
| `tests/OpenAnima.Tests/Integration/PromptInjectionIntegrationTests.cs` | Integration tests (min 80 lines) | VERIFIED | 516 lines; 7 integration tests tagged `[Trait("Category", "Integration")]` |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `tests/OpenAnima.Tests/Unit/FormatDetectorTests.cs` | `src/OpenAnima.Core/Modules/FormatDetector.cs` | `new FormatDetector()` | WIRED | `private readonly FormatDetector _detector = new();` at line 12 |
| `src/OpenAnima.Core/Modules/LLMModule.cs` | `src/OpenAnima.Core/Modules/FormatDetector.cs` | `_formatDetector.Detect()` in `ExecuteAsync` | WIRED | `_formatDetector.Detect(currentContent, knownServiceNames)` at line 134 |
| `src/OpenAnima.Core/Modules/LLMModule.cs` | `src/OpenAnima.Core/Routing/ICrossAnimaRouter.cs` | `GetPortsForAnima` for system message building | WIRED | `_router.GetPortsForAnima(targetAnimaId)` at line 96 |
| `src/OpenAnima.Core/Modules/LLMModule.cs` | `AnimaRouteModule.port.request` + `AnimaRouteModule.port.trigger` | EventBus publish in `DispatchRoutesAsync` | WIRED | Explicit event names at lines 295 and 303; request published before trigger |

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| FMTD-01 | 30-01 | FormatDetector scans LLM output for routing markers after response completes | SATISFIED | `FormatDetector.Detect()` is a post-response scan called inside `ExecuteAsync` after `CallLlmAsync()` returns |
| FMTD-02 | 30-01 | FormatDetector splits passthrough text from routing payload | SATISFIED | `FormatDetectionResult.PassthroughText` and `FormatDetectionResult.Routes` are distinct; markers stripped from passthrough |
| FMTD-04 | 30-01 | Format detection handles near-miss and malformed markers gracefully (no crash) | SATISFIED | `UnclosedMarkerRegex` catches partial tags; unrecognized service leaves marker in text and returns error; self-correction loop prevents crash on malformed input |
| PROMPT-01 | 30-02 | LLMModule system prompt auto-includes descriptions of available cross-Anima services | SATISFIED | `BuildSystemMessage(ports)` uses `_router.GetPortsForAnima(targetAnimaId)` to include port names and descriptions |
| PROMPT-02 | 30-02 | Prompt injection respects token budget cap (200-300 tokens) | NOTE: REQUIREMENTS PIVOT — The requirement text specifies a token cap; user decision locked this as "no cap, inject all services". Implementation has no cap. `BuildSystemMessage` injects all ports. REQUIREMENTS.md tracking table marks this Complete; the requirement text itself is now stale but not a gap — the user explicitly made this decision (documented in 30-RESEARCH.md). |
| PROMPT-03 | 30-02 | Prompt injection includes format instructions for LLM to trigger routing | SATISFIED | `BuildSystemMessage` includes "Routing marker format:" section with `<route service="...">` example |
| PROMPT-04 | 30-02 | Prompt injection skips when no routes are configured for current Anima | SATISFIED | `BuildKnownServiceNames()` returns empty set when config absent; `useFormatDetection = false` short-circuits to original behavior |
| FMTD-03 | 30-02 | FormatDetector dispatches extracted routing calls to CrossAnimaRouter | SATISFIED | `DispatchRoutesAsync()` publishes to `AnimaRouteModule.port.request` then `AnimaRouteModule.port.trigger` for each route extraction |

**Note on tracking table inconsistency:** `REQUIREMENTS.md` tracking table shows FMTD-01, FMTD-02, and FMTD-04 as "Pending" (lines 111-114). These are implemented and tested in Plan 01 — the tracking table was not updated after Plan 01 completed. This is a documentation gap only; the implementations exist and all tests pass.

---

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
|------|---------|----------|--------|
| None found | — | — | — |

No TODO/FIXME/placeholder comments, empty returns, or stub implementations found in any phase artifact.

---

### Test Results

**Targeted suite (FormatDetector, PromptInjection, LLMModule):**
```
Passed! — Failed: 0, Passed: 25, Skipped: 0, Total: 25, Duration: 427 ms
```

**Full suite:**
```
Failed! — Failed: 3, Passed: 215, Skipped: 0, Total: 218, Duration: 5 s
```

The 3 failures are pre-existing and unrelated to Phase 30:
- `MemoryLeakTests.UnloadModule_ReleasesMemory_After100Cycles` — plugin loading test failure in temp directory
- `PerformanceTests.HeartbeatLoop_MaintainsPerformance_With20Modules` — same plugin loading issue
- `WiringEngineIntegrationTests.DataRouting_FanOut_EachReceiverGetsData` — fan-out routing test (pre-existing)

These 3 failures were documented as pre-existing in both Plan 01 and Plan 02 summaries and are not caused by Phase 30 changes.

---

### Human Verification Required

#### 1. End-to-End LLM Prompt Injection

**Test:** Configure an Anima with AnimaRouteModule pointing to a second Anima, then send a message that should trigger routing. Observe the actual HTTP request sent to the LLM API.
**Expected:** The LLM API call payload includes a "system" message listing the target service and containing `<route service="...">` format instructions. The LLM response containing a route marker causes the second Anima to receive the payload; the passthrough text appears in the chat panel.
**Why human:** Cannot verify actual LLM API wire traffic, UI chat display, or downstream Anima activation without a live environment and configured Anima pair.

#### 2. Self-Correction Loop with Real LLM

**Test:** Use a scenario where the LLM consistently produces a malformed marker (e.g., by prompting it to output an incomplete tag). Verify the self-correction feedback is visible in LLM API call #2 (the correction message should contain the error reason and format example).
**Expected:** LLM receives the correction turn on retry; eventually produces a valid marker or the error port fires after 3 attempts.
**Why human:** Real LLM behavior under correction prompts cannot be tested without live API calls.

---

### Commits

All 4 plan commits verified in git log:
- `33762e6` — test(30-01): add failing FormatDetector unit tests (RED)
- `7939397` — feat(30-01): implement FormatDetector XML routing marker parser (GREEN)
- `c516954` — test(30-02): add failing PromptInjection integration tests (RED)
- `cb9a519` — feat(30-02): extend LLMModule with prompt injection and FormatDetector integration (GREEN)

---

### Summary

Phase 30 goal is achieved. The prompt injection and format detection pipeline is fully implemented and tested:

- `FormatDetector.cs` is a complete, pure, stateless class with two compiled Regex fields and a `Detect()` method covering all 8 specified behaviors (plain text, single marker, case-insensitive match, multiple markers, multiline payload, unrecognized service, unclosed tag, partial tag). 16 unit tests all pass.

- `LLMModule.cs` is extended with 5 new private methods (`BuildKnownServiceNames`, `BuildSystemMessage`, `BuildCorrectionMessage`, `DispatchRoutesAsync`, `CallLlmAsync`) and an `[OutputPort("error", PortType.Text)]` attribute. The self-correction loop (MaxRetries=2) is correctly implemented. Backward compatibility is preserved via the optional `ICrossAnimaRouter? router = null` constructor parameter. 7 integration tests all pass.

One documentation inconsistency noted: REQUIREMENTS.md tracking table marks FMTD-01, FMTD-02, and FMTD-04 as "Pending" despite being implemented in Plan 01. The tracking table should be updated to "Complete" for these three IDs.

The PROMPT-02 requirement text ("respects token budget cap 200-300 tokens") is stale — the user made an explicit decision to have no cap, documented in RESEARCH.md and both plans. The implementation correctly applies no cap.

---

_Verified: 2026-03-13T14:30:00Z_
_Verifier: Claude (gsd-verifier)_
