# Plan 05-01 Summary: SignalR Hub Infrastructure + Latency Tracking

**Status:** Complete
**Committed:** b75a560

## What Was Built

Created the SignalR real-time push infrastructure for the OpenAnima runtime:

1. **RuntimeHub** (`Hub<IRuntimeClient>`) mapped at `/hubs/runtime` — strongly-typed server-push hub
2. **IRuntimeClient** interface with three push methods: `ReceiveHeartbeatTick`, `ReceiveHeartbeatStateChanged`, `ReceiveModuleCountChanged`
3. **HeartbeatLoop** updated with `Stopwatch`-based per-tick latency tracking and `IHubContext` push on every tick
4. **ModuleService** pushes module count changes on load via `IHubContext`
5. **IHeartbeatService** exposes `LastTickLatencyMs` property

## Key Files

- `src/OpenAnima.Core/Hubs/IRuntimeClient.cs` — client interface
- `src/OpenAnima.Core/Hubs/RuntimeHub.cs` — SignalR Hub
- `src/OpenAnima.Core/Runtime/HeartbeatLoop.cs` — latency tracking + push
- `src/OpenAnima.Core/Services/ModuleService.cs` — module count push
- `src/OpenAnima.Core/Program.cs` — AddSignalR() + MapHub registration

## Decisions

- Used fire-and-forget (`_ =`) for Hub push calls to avoid blocking the tick loop
- Made `IHubContext` parameter optional (nullable) in HeartbeatLoop constructor for backward compatibility
- Replaced `DateTime.UtcNow` timing with `Stopwatch` for precise latency measurement
