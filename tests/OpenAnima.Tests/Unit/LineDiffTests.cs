using OpenAnima.Core.Memory;
using static OpenAnima.Core.Memory.LineDiff;

namespace OpenAnima.Tests.Unit;

public class LineDiffTests
{
    [Fact]
    public void Compute_IdenticalStrings_AllUnchanged()
    {
        var result = LineDiff.Compute("hello\nworld", "hello\nworld");
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(DiffKind.Unchanged, r.Kind));
    }

    [Fact]
    public void Compute_AddedLine_MarkedAsAdded()
    {
        var result = LineDiff.Compute("line1\nline2", "line1\ninserted\nline2");
        Assert.Contains(result, r => r.Kind == DiffKind.Added && r.Line == "inserted");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Compute_RemovedLine_MarkedAsRemoved()
    {
        var result = LineDiff.Compute("line1\nremoved\nline2", "line1\nline2");
        Assert.Contains(result, r => r.Kind == DiffKind.Removed && r.Line == "removed");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Compute_CompletelyDifferent_AllRemovedAndAdded()
    {
        var result = LineDiff.Compute("old", "new");
        Assert.Contains(result, r => r.Kind == DiffKind.Removed && r.Line == "old");
        Assert.Contains(result, r => r.Kind == DiffKind.Added && r.Line == "new");
    }

    [Fact]
    public void Compute_EmptyOld_AllAdded()
    {
        var result = LineDiff.Compute("", "line1\nline2");
        Assert.All(result, r => Assert.Equal(DiffKind.Added, r.Kind));
    }

    [Fact]
    public void Compute_EmptyNew_AllRemoved()
    {
        var result = LineDiff.Compute("line1\nline2", "");
        Assert.All(result, r => Assert.Equal(DiffKind.Removed, r.Kind));
    }

    [Fact]
    public void Compute_NullInputs_NoException()
    {
        var result = LineDiff.Compute(null!, null!);
        Assert.NotNull(result);
    }
}
