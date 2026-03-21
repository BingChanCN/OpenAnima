using OpenAnima.Core.Memory;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="DisclosureMatcher"/> and <see cref="GlossaryIndex"/>.
/// These tests verify matching behavior in isolation without requiring a database connection.
/// </summary>
public class DisclosureMatcherTests
{
    // --- DisclosureMatcher ---

    [Fact]
    public void Match_SubstringTrigger_ReturnsMatchingNode()
    {
        var nodes = new[]
        {
            new MemoryNode { Uri = "core://test/1", AnimaId = "a", Content = "c", DisclosureTrigger = "project X" }
        };

        var results = DisclosureMatcher.Match(nodes, "Let's discuss project X in detail");

        Assert.Single(results);
        Assert.Equal("core://test/1", results[0].Uri);
    }

    [Fact]
    public void Match_CaseInsensitive_ReturnsMatch()
    {
        var nodes = new[]
        {
            new MemoryNode { Uri = "core://test/2", AnimaId = "a", Content = "c", DisclosureTrigger = "Project X" }
        };

        var results = DisclosureMatcher.Match(nodes, "project x plans are ready");

        Assert.Single(results);
    }

    [Fact]
    public void Match_NoTriggerMatch_ReturnsEmpty()
    {
        var nodes = new[]
        {
            new MemoryNode { Uri = "core://test/3", AnimaId = "a", Content = "c", DisclosureTrigger = "project Y" }
        };

        var results = DisclosureMatcher.Match(nodes, "project X is underway");

        Assert.Empty(results);
    }

    [Fact]
    public void Match_NullTrigger_ExcludedFromResults()
    {
        var nodes = new[]
        {
            new MemoryNode { Uri = "core://test/4", AnimaId = "a", Content = "c", DisclosureTrigger = null }
        };

        var results = DisclosureMatcher.Match(nodes, "any context string");

        Assert.Empty(results);
    }

    // --- GlossaryIndex ---

    [Fact]
    public void GlossaryIndex_Build_FindsMultipleKeywords()
    {
        var index = new GlossaryIndex();
        index.Build(new[] { ("arch", "uri1"), ("pattern", "uri2") });

        var matches = index.FindMatches("The arch uses this pattern");

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, m => m.Keyword == "arch" && m.Uri == "uri1");
        Assert.Contains(matches, m => m.Keyword == "pattern" && m.Uri == "uri2");
    }

    [Fact]
    public void GlossaryIndex_EmptyContent_ReturnsEmpty()
    {
        var index = new GlossaryIndex();
        index.Build(new[] { ("hello", "uri1") });

        var matches = index.FindMatches(string.Empty);

        Assert.Empty(matches);
    }

    [Fact]
    public void GlossaryIndex_CaseInsensitive_Matches()
    {
        var index = new GlossaryIndex();
        index.Build(new[] { ("Hello", "uri1") });

        var matches = index.FindMatches("hello world");

        Assert.Single(matches);
        Assert.Equal("Hello", matches[0].Keyword);
    }
}
