---
phase: 31-http-request-module
plan: 01
subsystem: modules
tags: [http, ssrf, httpclient, resilience, tdd, csharp]

# Dependency graph
requires:
  - phase: 30-prompt-injection-and-format-detection
    provides: LLMModule integration patterns, body+trigger input port pattern (AnimaRouteModule)

provides:
  - SsrfGuard static utility blocking private/loopback/link-local IPs
  - HttpRequestModule (body+trigger inputs, body+statusCode+error outputs)
  - Named HttpClient "HttpRequest" with AddStandardResilienceHandler
  - DI singleton registration + port metadata + auto-init wired in

affects:
  - 31-http-request-module (plan 02 if it exists — UI sidebar config)
  - Any phase that adds wiring graph modules

# Tech tracking
tech-stack:
  added:
    - Microsoft.Extensions.Http.Resilience 8.7.0
  patterns:
    - SSRF protection pattern: static SsrfGuard.IsBlocked before CreateClient
    - Per-request IHttpClientFactory.CreateClient — never cached
    - CancellationTokenSource timeout created AFTER SSRF check
    - Linked CancellationTokenSource for heartbeat + timeout coordination
    - ParseHeaders using IndexOf(':') to preserve colons in header values

key-files:
  created:
    - src/OpenAnima.Core/Http/SsrfGuard.cs
    - src/OpenAnima.Core/Modules/HttpRequestModule.cs
    - tests/OpenAnima.Tests/Unit/SsrfGuardTests.cs
  modified:
    - src/OpenAnima.Core/OpenAnima.Core.csproj
    - src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs
    - src/OpenAnima.Core/Hosting/WiringInitializationService.cs
    - src/OpenAnima.Core/Program.cs

key-decisions:
  - "CIDR range matching via bit-level byte comparison — no third-party library needed for IPv4/IPv6 range checks"
  - "localhost hostname check before IPAddress.TryParse — handles loopback without DNS round-trip"
  - "Mutually exclusive output ports: body/statusCode XOR error per trigger — matches AnimaRouteModule pattern"
  - "CancellationTokenSource timeout created after SSRF check — avoids wasting the 10s window on local checks"
  - "ParseHeaders uses IndexOf(':') not Split(':') — preserves colons in header values (e.g. Authorization: Bearer token)"

patterns-established:
  - "SSRF guard pattern: call SsrfGuard.IsBlocked(url, out reason) before any HTTP operation"
  - "Named HttpClient factory pattern: AddHttpClient(name).AddStandardResilienceHandler() in Program.cs"

requirements-completed: [HTTP-01, HTTP-02, HTTP-04, HTTP-05]

# Metrics
duration: 8min
completed: 2026-03-14
---

# Phase 31 Plan 01: HTTP Request Module — Core Implementation Summary

**HttpRequestModule with SSRF IP blocking (SsrfGuard), IHttpClientFactory resilience pipeline, 10s timeout, and full DI registration — 15 unit tests pass**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-03-13T16:10:03Z
- **Completed:** 2026-03-13T16:18:00Z
- **Tasks:** 2 (Task 1 = TDD RED + GREEN commits; Task 2 = implementation + DI)
- **Files modified:** 7

## Accomplishments

- SsrfGuard static utility blocks all private/loopback/link-local ranges (127/8, 10/8, 172.16/12, 192.168/16, 169.254/16, fc00::/7, fe80::/10) with CIDR bit-level matching
- HttpRequestModule with body+trigger input ports, body+statusCode+error output ports, matching AnimaRouteModule event-driven pattern
- IHttpClientFactory.CreateClient("HttpRequest") per-request with AddStandardResilienceHandler resilience pipeline
- 15 SsrfGuard unit tests covering all blocked ranges, edge cases (172.15.0.1 allowed), public IPs allowed, and invalid URL handling

## Task Commits

Each task was committed atomically:

1. **Task 1: NuGet package + SsrfGuard (RED)** - `e42741a` (test)
2. **Task 1: SsrfGuard implementation (GREEN)** - `10d484e` (feat)
3. **Task 2: HttpRequestModule + DI registration** - `79fe17b` (feat)

_Note: TDD task has two commits (RED test commit, GREEN implementation commit)_

## Files Created/Modified

- `src/OpenAnima.Core/Http/SsrfGuard.cs` — Static SSRF IP blocking utility with CIDR range matching
- `src/OpenAnima.Core/Modules/HttpRequestModule.cs` — HTTP request wiring module (body+trigger pattern)
- `tests/OpenAnima.Tests/Unit/SsrfGuardTests.cs` — 15 unit tests for SSRF IP blocking
- `src/OpenAnima.Core/OpenAnima.Core.csproj` — Added Microsoft.Extensions.Http.Resilience 8.7.0
- `src/OpenAnima.Core/DependencyInjection/WiringServiceExtensions.cs` — HttpRequestModule singleton registration
- `src/OpenAnima.Core/Hosting/WiringInitializationService.cs` — HttpRequestModule in both PortRegistrationTypes and AutoInitModuleTypes
- `src/OpenAnima.Core/Program.cs` — Named HttpClient "HttpRequest" with AddStandardResilienceHandler

## Decisions Made

- **CIDR bit-level matching**: Implemented IsInRange via byte-by-byte bit masking — avoids adding IPNetwork2 or similar library dependency for a 30-line helper.
- **localhost hostname check first**: `uri.Host.Equals("localhost", ...)` checked before `IPAddress.TryParse` because "localhost" is not a parseable IP but maps to 127.0.0.1 — must be caught before the DNS path.
- **Mutually exclusive output ports**: body/statusCode fire together on any HTTP response (including 4xx/5xx — downstream decides if that's an error); error port fires only on network-layer failures (timeout, SSRF, DNS, connection refused).
- **CancellationTokenSource after SSRF check**: Per plan pitfall 4 — avoids burning the 10s timeout budget on local hostname/IP validation.
- **ParseHeaders uses IndexOf(':')**: Split(':') would break `Authorization: Bearer token` or any value containing a colon.

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

None — build and tests passed cleanly on first attempt.

## User Setup Required

None — no external service configuration required. The named HttpClient is registered as part of the standard .NET DI pipeline.

## Next Phase Readiness

- HttpRequestModule is registered, port metadata wired, and auto-initialized on startup
- SsrfGuard is available as a reusable static utility for any future module that makes HTTP calls
- UI sidebar integration (EditorConfigSidebar config fields for url/method/headers/body) would be the natural next plan if one exists

---
*Phase: 31-http-request-module*
*Completed: 2026-03-14*

## Self-Check: PASSED

- FOUND: src/OpenAnima.Core/Http/SsrfGuard.cs
- FOUND: src/OpenAnima.Core/Modules/HttpRequestModule.cs
- FOUND: tests/OpenAnima.Tests/Unit/SsrfGuardTests.cs
- FOUND: .planning/phases/31-http-request-module/31-01-SUMMARY.md
- COMMIT e42741a: test(31-01) add failing SsrfGuard unit tests (RED)
- COMMIT 10d484e: feat(31-01) implement SsrfGuard SSRF protection utility (GREEN)
- COMMIT 79fe17b: feat(31-01) implement HttpRequestModule with DI registration
