using OpenAnima.Core.Runs;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ConvergenceGuard"/>.
/// ConvergenceGuard is a pure in-memory class — no mocking needed.
/// </summary>
public class ConvergenceGuardTests
{
    // --- Budget: step count ---

    [Fact]
    public void Check_ReturnsContinue_WhenStepCountBelowMax()
    {
        var guard = new ConvergenceGuard(maxSteps: 10, maxWallSeconds: null);

        var result = guard.Check("ModuleA", outputHash: null);

        Assert.Equal(ConvergenceAction.Continue, result.Action);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Check_ReturnsExhausted_WhenStepCountReachesMaxSteps()
    {
        var guard = new ConvergenceGuard(maxSteps: 3, maxWallSeconds: null);

        guard.Check("ModuleA", outputHash: null); // step 1
        guard.Check("ModuleA", outputHash: null); // step 2
        var result = guard.Check("ModuleA", outputHash: null); // step 3 — hits max

        Assert.Equal(ConvergenceAction.Exhausted, result.Action);
        Assert.Contains("3/3", result.Reason);
        Assert.Contains("Budget exhausted", result.Reason);
    }

    // --- Budget: wall-clock time ---

    [Fact]
    public void Check_ReturnsExhausted_WhenWallClockExceedsMaxWallTime()
    {
        // maxWallSeconds=0 means the budget is already exhausted from the very first tick
        var guard = new ConvergenceGuard(maxSteps: null, maxWallSeconds: 0);

        // Small sleep to ensure time has elapsed past 0 seconds
        Thread.Sleep(10);
        var result = guard.Check("ModuleA", outputHash: null);

        Assert.Equal(ConvergenceAction.Exhausted, result.Action);
        Assert.NotNull(result.Reason);
        Assert.Contains("Budget exhausted", result.Reason);
    }

    [Fact]
    public void Check_ReturnsContinue_WithNoStepOrWallBudget()
    {
        var guard = new ConvergenceGuard(maxSteps: null, maxWallSeconds: null);

        // Call many times — no budget, always Continue
        for (int i = 0; i < 1000; i++)
            Assert.Equal(ConvergenceAction.Continue, guard.Check("ModuleA", outputHash: null).Action);
    }

    // --- Non-productive pattern detection ---

    [Fact]
    public void Check_ReturnsNonProductive_AfterThreeIdenticalOutputsFromSameModule()
    {
        var guard = new ConvergenceGuard(maxSteps: null, maxWallSeconds: null);
        const string hash = "abc123";

        guard.Check("LLMModule", outputHash: hash); // 1st
        guard.Check("LLMModule", outputHash: hash); // 2nd
        var result = guard.Check("LLMModule", outputHash: hash); // 3rd — triggers

        Assert.Equal(ConvergenceAction.NonProductive, result.Action);
        Assert.Contains("Non-productive", result.Reason);
        Assert.Contains("LLMModule", result.Reason);
    }

    [Fact]
    public void Check_ReturnsContinue_ForIdenticalOutputsBelowThreshold()
    {
        var guard = new ConvergenceGuard(maxSteps: null, maxWallSeconds: null);
        const string hash = "abc123";

        var result1 = guard.Check("LLMModule", outputHash: hash); // 1st
        var result2 = guard.Check("LLMModule", outputHash: hash); // 2nd

        Assert.Equal(ConvergenceAction.Continue, result1.Action);
        Assert.Equal(ConvergenceAction.Continue, result2.Action);
    }

    [Fact]
    public void Check_ReturnsContinue_WhenOutputHashIsNull()
    {
        // Trigger-only modules pass null — non-productive detection must be skipped
        var guard = new ConvergenceGuard(maxSteps: null, maxWallSeconds: null);

        for (int i = 0; i < 10; i++)
            Assert.Equal(ConvergenceAction.Continue, guard.Check("TriggerModule", outputHash: null).Action);
    }

    // --- RestoreStepCount ---

    [Fact]
    public void RestoreStepCount_SetsInternalCountSoSubsequentChecksCountFromRestoredValue()
    {
        var guard = new ConvergenceGuard(maxSteps: 500, maxWallSeconds: null);

        guard.RestoreStepCount(480);

        Assert.Equal(480, guard.StepCount);

        // Next 19 checks should be Continue
        for (int i = 0; i < 19; i++)
            Assert.Equal(ConvergenceAction.Continue, guard.Check("ModuleA", outputHash: null).Action);

        // The 20th check (step 500) should return Exhausted
        var result = guard.Check("ModuleA", outputHash: null);
        Assert.Equal(ConvergenceAction.Exhausted, result.Action);
        Assert.Contains("500/500", result.Reason);
    }

    [Fact]
    public void RestoreStepCount_Zero_BehavesIdenticalToFreshGuard()
    {
        var fresh = new ConvergenceGuard(maxSteps: 5, maxWallSeconds: null);
        var restored = new ConvergenceGuard(maxSteps: 5, maxWallSeconds: null);
        restored.RestoreStepCount(0);

        // Both guards should exhaust on step 5
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(ConvergenceAction.Continue, fresh.Check("M", null).Action);
            Assert.Equal(ConvergenceAction.Continue, restored.Check("M", null).Action);
        }

        Assert.Equal(ConvergenceAction.Exhausted, fresh.Check("M", null).Action);
        Assert.Equal(ConvergenceAction.Exhausted, restored.Check("M", null).Action);
    }
}
