---
phase: 24-service-migration-i18n
verified: 2026-02-28T00:00:00Z
status: gaps_found
score: 10/12 must-haves verified
re_verification: false
gaps:
  - truth: "Each Anima has isolated module instances with separate state"
    status: failed
    reason: "Module singletons (ChatOutputModule, LLMModule, etc.) share a global IEventBus registered in Program.cs. Per-Anima module instance isolation was explicitly deferred. ROADMAP success criterion 2 is not met. REQUIREMENTS.md marks ANIMA-08 as [ ] (incomplete)."
    artifacts:
      - path: "src/OpenAnima.Core/Program.cs"
        issue: "Lines 28-29 register global EventBus singleton: AddSingleton<EventBus>() and AddSingleton<IEventBus>(...). Module singletons receive this shared bus at construction time."
    missing:
      - "Per-Anima module instance isolation (full ANIMA-08) — deferred to Phase 25 per plan known_limitations"
  - truth: "Missing translations fall back to English gracefully (ROADMAP criterion 5 / I18N-04 as written in REQUIREMENTS.md)"
    status: failed
    reason: "REQUIREMENTS.md I18N-04 states 'Missing translations fall back to English'. ROADMAP success criterion 5 states 'Missing translations fall back to English gracefully'. Implementation uses zh-CN as default and fallback (per CONTEXT.md locked decision). The plan noted the orchestrator would align REQUIREMENTS.md and ROADMAP, but neither document was updated — they still say 'fall back to English'."
    artifacts:
      - path: "src/OpenAnima.Core/Services/LanguageService.cs"
        issue: "Default culture is zh-CN. .NET IStringLocalizer falls back to the neutral resource (zh-CN.resx) when a key is missing in en-US, not to English."
      - path: ".planning/REQUIREMENTS.md"
        issue: "I18N-04 text still reads 'Missing translations fall back to English' — not updated to reflect Chinese fallback decision."
      - path: ".planning/ROADMAP.md"
        issue: "Phase 24 success criterion 5 still reads 'Missing translations fall back to English gracefully' — not updated."
    missing:
      - "Either update REQUIREMENTS.md I18N-04 and ROADMAP.md criterion 5 to say 'fall back to Chinese' to match implementation, OR change the fallback language to English to match the documented requirement"
human_verification:
  - test: "Navigate to /settings, switch language to English, reload the page"
    expected: "Language preference is restored to English on reload (localStorage persistence)"
    why_human: "localStorage read/write requires a running browser; cannot verify programmatically"
  - test: "Create two Animas, start heartbeat on Anima A, switch to Anima B"
    expected: "Anima B shows its own heartbeat state (stopped), Anima A continues ticking independently"
    why_human: "Real-time SignalR filtering by animaId requires a running app"
  - test: "Delete the currently active Anima when a second Anima exists"
    expected: "UI automatically switches to the next Anima without error"
    why_human: "Auto-switch behavior requires UI interaction to verify"
---

# Phase 24: Service Migration & i18n Verification Report

**Phase Goal:** Each Anima has isolated EventBus/HeartbeatLoop/WiringEngine, and users can switch UI language
**Verified:** 2026-02-28
**Status:** gaps_found
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Each Anima has its own EventBus instance that does not share subscriptions with other Animas | VERIFIED | `AnimaRuntime.cs:33` — `new EventBus(loggerFactory.CreateLogger<EventBus>())` per instance; two runtimes have `NotSame` EventBus (AnimaRuntimeTests line 34) |
| 2 | Each Anima has its own HeartbeatLoop that can be started/stopped independently | VERIFIED | `AnimaRuntime.cs:36-42` — `new HeartbeatLoop(EventBus, PluginRegistry, animaId:...)` per instance; `AnimaRuntimeTests.AnimaRuntime_DisposeAsync_StopsHeartbeatLoop` passes |
| 3 | Each Anima has its own WiringEngine with isolated configuration | VERIFIED | `AnimaRuntime.cs:44-49` — `new WiringEngine(EventBus, new PortRegistry(), animaId:...)` per instance |
| 4 | SignalR push messages include animaId so UI can filter by active Anima | VERIFIED | `IRuntimeClient.cs` — all 5 methods have `string animaId` as first parameter; `HeartbeatLoop.cs:165` passes `_animaId`; `WiringEngine.cs:182,194,204` passes `_animaId` |
| 5 | Deleting a running Anima stops its HeartbeatLoop and disposes its runtime | VERIFIED | `AnimaRuntimeManager.cs:86-90` — `runtime.DisposeAsync()` called before `_runtimes.Remove(id)`; `AnimaRuntimeTests.DeleteAsync_DisposesRuntime` passes |
| 6 | If deleted Anima was active, auto-switch to next Anima via AnimaContext.SetActive() | VERIFIED | `AnimaRuntimeManager.cs:105-110` — checks `_animaContext.ActiveAnimaId == id`, calls `_animaContext.SetActive(next.Id)`; `AnimaRuntimeTests.DeleteAsync_WhenDeletedAnimaWasActive_AutoSwitchesToNextAnima` passes |
| 7 | New Anima starts with runtime stopped (user must manually start) | VERIFIED | `AnimaRuntime.cs` — `HeartbeatLoop` created but not started; `IsRunning => HeartbeatLoop.IsRunning` returns false until `StartAsync()` called |
| 8 | Per-Anima PluginRegistry provides isolated event routing; module singleton instances are shared (known limitation) | VERIFIED (partial) | `AnimaRuntime.cs:34` — `new PluginRegistry()` per Anima; `Program.cs:28-29` — global `IEventBus` singleton kept for module constructors; documented in plan known_limitations |
| 9 | Each Anima has isolated module instances with separate state | FAILED | `Program.cs:28-29` — `AddSingleton<EventBus>()` and `AddSingleton<IEventBus>()` remain; module singletons share global bus. ANIMA-08 marked `[ ]` in REQUIREMENTS.md |
| 10 | User can navigate to Settings page via gear icon in top navigation | VERIFIED | `MainLayout.razor:69-75` — `<NavLink href="/settings">` with gear icon `&#x2699;` and `@L["Nav.Settings"]` |
| 11 | User can switch between Chinese and English on Settings page | VERIFIED | `Settings.razor:14-20` — `<select>` with zh-CN and en-US options; `OnLanguageChanged` calls `LanguageService.SetLanguage(culture)` |
| 12 | Language switch takes effect immediately without page reload | VERIFIED | `LanguageService.cs:24` — fires `LanguageChanged` event; `MainLayout.razor:98,114-116` — subscribes and calls `InvokeAsync(StateHasChanged)`; all 11 components subscribe to `LanguageChanged` |
| 13 | Language preference persists across browser sessions via localStorage | VERIFIED | `Settings.razor:33,52` — reads `localStorage.getItem("openanima-language")` on first render, writes on change; `MainLayout.razor:105-110` — also reads on first render |
| 14 | MainLayout nav labels display in selected language via IStringLocalizer | VERIFIED | `MainLayout.razor:38,45,52,59,66,73` — all nav labels use `@L["Nav.*"]` keys |
| 15 | Chinese is the default language | VERIFIED | `LanguageService.cs:11` — `_current = new CultureInfo("zh-CN")`; `LanguageServiceTests.Default_Culture_Is_ZhCN` passes |
| 16 | Missing translations fall back to Chinese | VERIFIED (implementation) / FAILED (requirement text) | Implementation: zh-CN.resx is the default resource. REQUIREMENTS.md I18N-04 and ROADMAP criterion 5 still say "fall back to English" — documents not updated as planned |

**Score:** 10/12 truths verified (2 gaps)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/OpenAnima.Core/Anima/AnimaRuntime.cs` | Per-Anima runtime container owning EventBus + HeartbeatLoop + WiringEngine | VERIFIED | 58 lines, substantive implementation, used by AnimaRuntimeManager.GetOrCreateRuntime |
| `tests/OpenAnima.Tests/Unit/AnimaRuntimeTests.cs` | Unit tests for AnimaRuntime lifecycle | VERIFIED | 6 tests, all pass (11 total in filter run) |
| `src/OpenAnima.Core/Services/LanguageService.cs` | Singleton service holding current CultureInfo with LanguageChanged event | VERIFIED | 26 lines, full implementation, registered in Program.cs |
| `src/OpenAnima.Core/Resources/SharedResources.zh-CN.resx` | Chinese translation resource file (default) | VERIFIED | 70+ keys covering Nav, Anima, Settings, Chat, Common, Dashboard, Heartbeat, Monitor, Modules, Editor |
| `src/OpenAnima.Core/Resources/SharedResources.en-US.resx` | English translation resource file | VERIFIED | Matching keys with English values |
| `src/OpenAnima.Core/Components/Pages/Settings.razor` | Settings page with language switcher dropdown | VERIFIED | Route `/settings`, dropdown with zh-CN/en-US, localStorage read/write, LanguageChanged subscription |
| `tests/OpenAnima.Tests/Unit/LanguageServiceTests.cs` | Unit tests for LanguageService | VERIFIED | 5 tests, all pass |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `AnimaRuntimeManager.cs` | `AnimaRuntime.cs` | `Dictionary<string, AnimaRuntime> _runtimes` | WIRED | `_runtimes[animaId] = runtime` at line 209; `GetOrCreateRuntime` and `DeleteAsync` both use it |
| `AnimaRuntime.cs` | `EventBus.cs` | `new EventBus(...)` in constructor | WIRED | Line 33: `EventBus = new EventBus(loggerFactory.CreateLogger<EventBus>())` |
| `HeartbeatLoop.cs` | `IRuntimeClient.cs` | `ReceiveHeartbeatTick` with animaId | WIRED | Line 165: `_hubContext.Clients.All.ReceiveHeartbeatTick(_animaId, _tickCount, latencyMs)` |
| `AnimaRuntimeManager.cs` | `AnimaContext.cs` | `DeleteAsync` calls `SetActive` | WIRED | Lines 105-110: checks active ID, calls `_animaContext.SetActive(next.Id)` |
| `Settings.razor` | `LanguageService.cs` | `SetLanguage()` on dropdown change | WIRED | Lines 37, 51: `LanguageService.SetLanguage(saved)` and `LanguageService.SetLanguage(culture)` |
| `MainLayout.razor` | `LanguageService.cs` | Subscribe to `LanguageChanged` for re-render | WIRED | Lines 98, 114-116, 131: subscribe, handler, unsubscribe |
| `LanguageService.cs` | localStorage | `IJSRuntime` for persistence | WIRED | `Settings.razor:33,52` — `localStorage.getItem/setItem("openanima-language", ...)` |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| ANIMA-07 | 24-01 | Each Anima has independent heartbeat loop | SATISFIED | AnimaRuntime creates per-Anima HeartbeatLoop; AnimaRuntimeTests pass |
| ANIMA-08 | 24-01 (partial) | Each Anima has independent module instances | BLOCKED | Module singletons share global IEventBus (Program.cs:28-29); REQUIREMENTS.md marks as `[ ]`; deferred to Phase 25 |
| I18N-01 | 24-02 | User can switch UI language between Chinese and English | SATISFIED | Settings.razor dropdown + LanguageService.SetLanguage() |
| I18N-02 | 24-02, 24-03 | All UI text displays in selected language | SATISFIED | All 11 components inject IStringLocalizer; 10 via @inject, 1 via partial class [Inject] (Monitor.razor.cs) |
| I18N-03 | 24-02 | Language preference persists across sessions | SATISFIED | localStorage read/write in Settings.razor and MainLayout.razor |
| I18N-04 | 24-02 | Missing translations fall back to [language] | PARTIAL | Implementation falls back to zh-CN (correct per CONTEXT.md decision). REQUIREMENTS.md and ROADMAP still say "fall back to English" — documents not updated as planned |
| ARCH-03 | 24-01 | Each Anima has isolated EventBus instance | SATISFIED | AnimaRuntime.cs:33 — `new EventBus(...)` per Anima |
| ARCH-04 | 24-01 | Each Anima has isolated WiringEngine instance | SATISFIED | AnimaRuntime.cs:44-49 — `new WiringEngine(...)` per Anima |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/OpenAnima.Core/Program.cs` | 28-29 | Global `IEventBus` singleton kept alongside per-Anima EventBus | Info | Intentional ANIMA-08 partial — documented in plan known_limitations. Module singletons cannot receive per-Anima bus at construction time. |

No stub implementations, empty handlers, or TODO/FIXME blockers found in any key files.

### Human Verification Required

#### 1. Language Persistence Across Reload

**Test:** Navigate to `/settings`, switch language to English, close and reopen the browser tab
**Expected:** Language is restored to English on reload without manual re-selection
**Why human:** localStorage read requires a running browser with JavaScript execution

#### 2. Per-Anima Heartbeat Isolation

**Test:** Create two Animas, start heartbeat on Anima A, switch to Anima B in the sidebar
**Expected:** Anima B's Heartbeat page shows "Stopped" state; Anima A continues ticking in background; switching back to Anima A shows its tick count still incrementing
**Why human:** Real-time SignalR animaId filtering requires a running app with WebSocket connection

#### 3. Auto-Switch on Active Anima Delete

**Test:** Create two Animas, make Anima A active, delete Anima A via context menu
**Expected:** UI automatically switches to Anima B; no error state; Anima B's runtime is accessible
**Why human:** UI state transition requires browser interaction

### Gaps Summary

Two gaps block full goal achievement:

**Gap 1 — ANIMA-08 (module instance isolation):** The ROADMAP success criterion "Each Anima has isolated module instances with separate state" is not met. Module singletons (`ChatOutputModule`, `LLMModule`, etc.) share a global `IEventBus` registered in `Program.cs`. This was a deliberate deferral documented in the plan's `known_limitations` section. The gap is structural — full isolation requires changes to `PluginLoader` and module lifecycle management, scoped to Phase 25.

**Gap 2 — I18N-04 document alignment:** The implementation correctly uses Chinese as the fallback language per the CONTEXT.md locked user decision. However, `REQUIREMENTS.md` I18N-04 and `ROADMAP.md` Phase 24 success criterion 5 still say "fall back to English". The plan stated the orchestrator would align these documents, but they were not updated. This is a documentation gap, not an implementation gap — the code is correct per the user's decision.

---

_Verified: 2026-02-28_
_Verifier: Claude (gsd-verifier)_
