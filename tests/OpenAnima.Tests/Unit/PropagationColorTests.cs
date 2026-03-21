using OpenAnima.Core.Runs;

namespace OpenAnima.Tests.Unit;

public class PropagationColorTests
{
    [Fact]
    public void GetColor_ReturnsHexString_StartingWithHash()
    {
        var color = PropagationColorAssigner.GetColor("abc123");
        Assert.StartsWith("#", color);
        Assert.Equal(7, color.Length);
    }

    [Fact]
    public void GetColor_IsDeterministic_SameInputSameOutput()
    {
        var first = PropagationColorAssigner.GetColor("abc123");
        var second = PropagationColorAssigner.GetColor("abc123");
        Assert.Equal(first, second);
    }

    [Fact]
    public void GetColor_EmptyString_ReturnsTransparent()
    {
        Assert.Equal("transparent", PropagationColorAssigner.GetColor(""));
    }

    [Fact]
    public void GetColor_Null_ReturnsTransparent()
    {
        Assert.Equal("transparent", PropagationColorAssigner.GetColor(null));
    }

    [Fact]
    public void GetColor_CyclesThroughExactly8Colors()
    {
        var expectedColors = new[]
        {
            "#6c8cff", "#4ade80", "#fbbf24", "#f87171",
            "#a78bfa", "#34d399", "#fb923c", "#60a5fa"
        };

        // Generate enough distinct IDs to cover all 8 slots
        var seen = new HashSet<string>();
        for (int i = 0; i < 1000; i++)
        {
            seen.Add(PropagationColorAssigner.GetColor($"id-{i}"));
        }

        // Every returned color must be in the expected palette
        foreach (var color in seen)
        {
            Assert.Contains(color, expectedColors);
        }

        // All 8 colors must appear
        foreach (var expected in expectedColors)
        {
            Assert.Contains(expected, seen);
        }
    }

    [Fact]
    public void GetColor_IndexIsAbsHashMod8()
    {
        var expectedColors = new[]
        {
            "#6c8cff", "#4ade80", "#fbbf24", "#f87171",
            "#a78bfa", "#34d399", "#fb923c", "#60a5fa"
        };

        var id = "test-propagation";
        var expectedIndex = Math.Abs(id.GetHashCode()) % 8;
        var expected = expectedColors[expectedIndex];
        Assert.Equal(expected, PropagationColorAssigner.GetColor(id));
    }
}
