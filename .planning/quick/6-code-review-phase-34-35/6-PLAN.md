---
quick_task: 6
type: review
focus: correctness
scope: phase_34_activity_channels_and_phase_35_contracts_api
autonomous: true
---

# Quick Task 6: Phase 34 / 35 Code Review

**Objective:** Review Phase 34 and Phase 35 implementation for correctness, regression risk, and missing coverage, then capture concrete findings with evidence.

## Tasks

<task type="auto">
  <name>Task 1: Review Phase 34 runtime dispatch changes</name>
  <files>
    src/OpenAnima.Core/Channels/ActivityChannelHost.cs
    src/OpenAnima.Core/Anima/AnimaRuntime.cs
    src/OpenAnima.Core/Runtime/HeartbeatLoop.cs
    src/OpenAnima.Core/Modules/HeartbeatModule.cs
    src/OpenAnima.Core/Modules/ChatInputModule.cs
    src/OpenAnima.Core/Routing/CrossAnimaRouter.cs
    src/OpenAnima.Core/Plugins/PluginRegistry.cs
    tests/OpenAnima.Tests/Unit/ActivityChannelHostTests.cs
    tests/OpenAnima.Tests/Integration/ActivityChannelIntegrationTests.cs
    tests/OpenAnima.Tests/Integration/ActivityChannelSoakTests.cs
  </files>
  <action>
Review the Phase 34 activity-channel model against its stated goal:

- Verify heartbeat ticks still drive the real heartbeat-trigger behavior, not just synthetic execute events
- Verify the stateless dispatch fork is reachable in the real runtime
- Verify user chat ingress actually flows through the chat channel
- Check shutdown/enqueue semantics around channel completion
- Check whether the scoped tests exercise the real runtime seams or only synthetic shortcuts
  </action>
  <verify>
Manual code inspection plus targeted test execution. Record only concrete findings with file:line references and impact.
  </verify>
  <done>
Phase 34 findings captured with severity, reasoning, and evidence.
  </done>
</task>

<task type="auto">
  <name>Task 2: Review Phase 35 Contracts, DI bridge, and config semantics</name>
  <files>
    src/OpenAnima.Contracts/IModuleConfig.cs
    src/OpenAnima.Contracts/IModuleContext.cs
    src/OpenAnima.Contracts/Routing/ICrossAnimaRouter.cs
    src/OpenAnima.Core/DependencyInjection/AnimaServiceExtensions.cs
    src/OpenAnima.Core/Anima/AnimaRuntimeManager.cs
    src/OpenAnima.Core/Services/AnimaModuleConfigService.cs
    PortModule/PortModule.cs
    tests/OpenAnima.Tests/Integration/CanaryModuleTests.cs
    tests/OpenAnima.Tests/Unit/ContractsApiTests.cs
  </files>
  <action>
Review the Phase 35 contracts expansion for behavior, not just API shape:

- Verify `AddAnimaServices()` can resolve the manager/router pair without hanging
- Verify per-key config writes are safe under concurrent callers
- Verify the canary tests prove real injection of all promised capability services
- Check whether compatibility shims and DI wiring preserve the intended runtime behavior
  </action>
  <verify>
Manual code inspection, targeted tests, and a throwaway DI resolution probe outside the repo. Document only reproducible findings.
  </verify>
  <done>
Phase 35 findings captured with severity, reasoning, and evidence.
  </done>
</task>

<task type="auto">
  <name>Task 3: Write review artifacts</name>
  <files>
    .planning/quick/6-code-review-phase-34-35/6-REVIEW.md
    .planning/quick/6-code-review-phase-34-35/6-SUMMARY.md
  </files>
  <action>
Write the review file with findings first, then summarize scope, evidence, and immediate recommendations.
  </action>
  <verify>
Artifacts clearly identify blockers vs warnings and reference the exact files/lines that need attention.
  </verify>
  <done>
Quick-task review artifacts written and ready for state tracking.
  </done>
</task>
