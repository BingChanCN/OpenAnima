using OpenAnima.Core.Runs;

namespace OpenAnima.Tests.Unit;

public class RunDetailTimelineTests
{
    private record TimelineEntry(string OccurredAt, bool IsStateEvent, StepRecord? Step, RunStateEvent? StateEvent);

    private static List<TimelineEntry> MergeTimeline(
        IReadOnlyList<StepRecord> steps,
        IReadOnlyList<RunStateEvent> stateEvents)
    {
        var stepEntries = steps.Select(s => new TimelineEntry(s.OccurredAt, false, s, null));
        var eventEntries = stateEvents.Select(e => new TimelineEntry(e.OccurredAt, true, null, e));

        return stepEntries
            .Concat(eventEntries)
            .OrderBy(e => e.OccurredAt)
            .ThenByDescending(e => e.IsStateEvent)
            .ToList();
    }

    [Fact]
    public void MergeTimeline_TwoStepsAndTwoStateEvents_ReturnsFourEntriesSortedByOccurredAt()
    {
        var steps = new List<StepRecord>
        {
            new() { StepId = "s1", OccurredAt = "2026-03-21T10:00:01Z" },
            new() { StepId = "s2", OccurredAt = "2026-03-21T10:00:03Z" }
        };
        var events = new List<RunStateEvent>
        {
            new() { Id = 1, OccurredAt = "2026-03-21T10:00:00Z", State = "Created" },
            new() { Id = 2, OccurredAt = "2026-03-21T10:00:02Z", State = "Running" }
        };

        var result = MergeTimeline(steps, events);

        Assert.Equal(4, result.Count);
        Assert.Equal("2026-03-21T10:00:00Z", result[0].OccurredAt);
        Assert.Equal("2026-03-21T10:00:01Z", result[1].OccurredAt);
        Assert.Equal("2026-03-21T10:00:02Z", result[2].OccurredAt);
        Assert.Equal("2026-03-21T10:00:03Z", result[3].OccurredAt);
    }

    [Fact]
    public void MergeTimeline_EmptyStepsAndOneStateEvent_ReturnsOneEntry()
    {
        var steps = new List<StepRecord>();
        var events = new List<RunStateEvent>
        {
            new() { Id = 1, OccurredAt = "2026-03-21T10:00:00Z", State = "Created" }
        };

        var result = MergeTimeline(steps, events);

        Assert.Single(result);
        Assert.True(result[0].IsStateEvent);
        Assert.NotNull(result[0].StateEvent);
        Assert.Null(result[0].Step);
    }

    [Fact]
    public void MergeTimeline_OverlappingTimestamps_StateEventBeforeStepOnTie()
    {
        var ts = "2026-03-21T10:00:00Z";
        var steps = new List<StepRecord>
        {
            new() { StepId = "s1", OccurredAt = ts }
        };
        var events = new List<RunStateEvent>
        {
            new() { Id = 1, OccurredAt = ts, State = "Running" }
        };

        var result = MergeTimeline(steps, events);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].IsStateEvent);   // state event first on tie
        Assert.False(result[1].IsStateEvent);  // step second
    }

    [Fact]
    public void MergeTimeline_EachEntryHasCorrectNullability()
    {
        var steps = new List<StepRecord>
        {
            new() { StepId = "s1", OccurredAt = "2026-03-21T10:00:01Z" }
        };
        var events = new List<RunStateEvent>
        {
            new() { Id = 1, OccurredAt = "2026-03-21T10:00:00Z", State = "Created" }
        };

        var result = MergeTimeline(steps, events);

        var stateEntry = result.First(e => e.IsStateEvent);
        Assert.NotNull(stateEntry.StateEvent);
        Assert.Null(stateEntry.Step);

        var stepEntry = result.First(e => !e.IsStateEvent);
        Assert.NotNull(stepEntry.Step);
        Assert.Null(stepEntry.StateEvent);
    }
}
