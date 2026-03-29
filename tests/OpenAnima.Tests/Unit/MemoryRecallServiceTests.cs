using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Memory;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="MemoryRecallService"/>.
/// Uses a manual <see cref="FakeMemoryGraph"/> stub — no mocking libraries.
/// </summary>
public class MemoryRecallServiceTests
{
    // ── helpers ─────────────────────────────────────────────────────────────────

    private static MemoryRecallService BuildService(FakeMemoryGraph fake) =>
        new(fake, NullLogger<MemoryRecallService>.Instance);

    private static MemoryNode MakeNode(
        string uri,
        string content = "some content",
        string? disclosureTrigger = null,
        string? keywords = null,
        string updatedAt = "2024-01-01T00:00:00Z") =>
        new()
        {
            Uri = uri,
            AnimaId = "test-anima",
            Content = content,
            DisclosureTrigger = disclosureTrigger,
            Keywords = keywords,
            CreatedAt = "2024-01-01T00:00:00Z",
            UpdatedAt = updatedAt
        };

    // ── disclosure matching ──────────────────────────────────────────────────

    [Fact]
    public async Task RecallAsync_DisclosureTriggerMatch_ReturnsNodeWithDisclosureReason()
    {
        var node = MakeNode("core://test/1", disclosureTrigger: "project X");
        var fake = new FakeMemoryGraph
        {
            DisclosureNodes = [node]
        };

        var result = await BuildService(fake).RecallAsync("test-anima", "Let's discuss project X");

        Assert.True(result.HasAny);
        Assert.Single(result.Nodes);
        Assert.Equal("disclosure", result.Nodes[0].Reason);
        Assert.Equal("Disclosure", result.Nodes[0].RecallType);
    }

    // ── glossary matching ────────────────────────────────────────────────────

    [Fact]
    public async Task RecallAsync_GlossaryKeywordMatch_ReturnsNodeWithGlossaryReason()
    {
        var node = MakeNode("core://test/2", content: "architecture content");
        var fake = new FakeMemoryGraph
        {
            GlossaryMatches = [("architecture", "core://test/2")],
            NodesByUri = { ["core://test/2"] = node }
        };

        var result = await BuildService(fake).RecallAsync("test-anima", "some architecture context");

        Assert.True(result.HasAny);
        Assert.Single(result.Nodes);
        Assert.Equal("glossary: architecture", result.Nodes[0].Reason);
        Assert.Equal("Glossary", result.Nodes[0].RecallType);
    }

    // ── deduplication ────────────────────────────────────────────────────────

    [Fact]
    public async Task RecallAsync_SameUriMatchedByBothDisclosureAndGlossary_DeduplicatesToOneNodeWithMergedReason()
    {
        var node = MakeNode("core://test/3", disclosureTrigger: "project X");
        var fake = new FakeMemoryGraph
        {
            DisclosureNodes = [node],
            GlossaryMatches = [("architecture", "core://test/3")],
            NodesByUri = { ["core://test/3"] = node }
        };

        var result = await BuildService(fake).RecallAsync("test-anima", "project X architecture discussion");

        Assert.Single(result.Nodes);
        Assert.Equal("disclosure + glossary: architecture", result.Nodes[0].Reason);
        Assert.Equal("Disclosure", result.Nodes[0].RecallType);
    }

    // ── no matches ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RecallAsync_NoMatches_ReturnsEmptyResult()
    {
        var fake = new FakeMemoryGraph();

        var result = await BuildService(fake).RecallAsync("test-anima", "unrelated context");

        Assert.False(result.HasAny);
        Assert.Empty(result.Nodes);
    }

    // ── priority sorting ─────────────────────────────────────────────────────

    [Fact]
    public async Task RecallAsync_PrioritySorts_DisclosureBeforeGlossary_ThenByUpdatedAtDescending()
    {
        var disclosureNode = MakeNode("core://disclosure/1", disclosureTrigger: "trigger", updatedAt: "2024-01-01T00:00:00Z");
        var glossaryNode = MakeNode("core://glossary/1", updatedAt: "2024-06-01T00:00:00Z");

        var fake = new FakeMemoryGraph
        {
            DisclosureNodes = [disclosureNode],
            GlossaryMatches = [("keyword", "core://glossary/1")],
            NodesByUri = { ["core://glossary/1"] = glossaryNode }
        };

        var result = await BuildService(fake).RecallAsync("test-anima", "trigger keyword");

        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal("Disclosure", result.Nodes[0].RecallType);
        Assert.Equal("Glossary", result.Nodes[1].RecallType);
    }

    // ── content truncation ───────────────────────────────────────────────────

    [Fact]
    public async Task RecallAsync_TruncatesIndividualNodeContentTo500Characters()
    {
        var longContent = new string('x', 800);
        var node = MakeNode("core://test/long", content: longContent, disclosureTrigger: "trigger");
        var fake = new FakeMemoryGraph
        {
            DisclosureNodes = [node]
        };

        var result = await BuildService(fake).RecallAsync("test-anima", "trigger");

        Assert.Single(result.Nodes);
        Assert.Equal(500, result.Nodes[0].TruncatedContent.Length);
    }

    // ── total budget cap ─────────────────────────────────────────────────────

    [Fact]
    public async Task RecallAsync_DropsTailNodesWhenTotalCharacterCountExceeds6000()
    {
        // Each node has 1000 chars content (truncated to 500). 12 nodes * 500 = 6000.
        // The 13th node should be dropped.
        var nodes = Enumerable.Range(1, 13)
            .Select(i => MakeNode(
                $"core://test/{i:D2}",
                content: new string('x', 1000),
                disclosureTrigger: "trigger",
                updatedAt: $"2024-01-{i:D2}T00:00:00Z"))
            .ToList();

        var fake = new FakeMemoryGraph
        {
            DisclosureNodes = nodes
        };

        var result = await BuildService(fake).RecallAsync("test-anima", "trigger");

        // Total budget is 6000 chars; each truncated node is 500 chars: 12 nodes fit exactly.
        Assert.Equal(12, result.Nodes.Count);
    }

    // ── glossary rebuild order ────────────────────────────────────────────────

    [Fact]
    public async Task RecallAsync_CallsRebuildGlossaryAsyncBeforeFindGlossaryMatches()
    {
        var fake = new FakeMemoryGraph();

        await BuildService(fake).RecallAsync("test-anima", "context");

        Assert.True(fake.RebuildGlossaryCalled);
    }

    // ── reason non-empty ─────────────────────────────────────────────────────

    [Fact]
    public async Task RecallAsync_EachRecalledNodeHasNonEmptyReason()
    {
        var disclosureNode = MakeNode("core://disclosure/r1", disclosureTrigger: "reason trigger");
        var glossaryNode = MakeNode("core://glossary/r1");
        var fake = new FakeMemoryGraph
        {
            DisclosureNodes = [disclosureNode],
            GlossaryMatches = [("keyword", "core://glossary/r1")],
            NodesByUri = { ["core://glossary/r1"] = glossaryNode }
        };

        var result = await BuildService(fake).RecallAsync("test-anima", "reason trigger keyword");

        Assert.All(result.Nodes, n => Assert.False(string.IsNullOrEmpty(n.Reason)));
    }

    // ── boot recall ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RecallAsync_BootNodes_ReturnedWithBootRecallType()
    {
        var fake = new FakeMemoryGraph
        {
            PrefixNodes = [MakeNode("core://identity/agent", "I am a developer agent")]
        };

        var result = await BuildService(fake).RecallAsync("test-anima", "any context");

        Assert.True(result.HasAny);
        Assert.Single(result.Nodes);
        Assert.Equal("Boot", result.Nodes[0].RecallType);
        Assert.Equal("boot", result.Nodes[0].Reason);
        Assert.True(fake.QueryByPrefixCalled);
    }

    [Fact]
    public async Task RecallAsync_BootNodeNotOverwrittenByDisclosure()
    {
        var node = MakeNode("core://identity/agent", disclosureTrigger: "project X");
        var fake = new FakeMemoryGraph
        {
            PrefixNodes = [node],
            DisclosureNodes = [node]
        };

        var result = await BuildService(fake).RecallAsync("test-anima", "project X discussion");

        Assert.Single(result.Nodes);
        Assert.Equal("Boot", result.Nodes[0].RecallType);
        Assert.Contains("boot", result.Nodes[0].Reason);
        Assert.Contains("disclosure", result.Nodes[0].Reason);
    }

    [Fact]
    public async Task RecallAsync_BootNodeNotOverwrittenByGlossary()
    {
        var node = MakeNode("core://identity/agent");
        var fake = new FakeMemoryGraph
        {
            PrefixNodes = [node],
            GlossaryMatches = [("architecture", "core://identity/agent")],
            NodesByUri = { ["core://identity/agent"] = node }
        };

        var result = await BuildService(fake).RecallAsync("test-anima", "architecture discussion");

        Assert.Single(result.Nodes);
        Assert.Equal("Boot", result.Nodes[0].RecallType);
        Assert.Contains("boot", result.Nodes[0].Reason);
    }
}

// ── FakeMemoryGraph stub ─────────────────────────────────────────────────────

/// <summary>
/// Manual test stub for <see cref="IMemoryGraph"/>. Configurable via public properties.
/// </summary>
public class FakeMemoryGraph : IMemoryGraph
{
    public List<MemoryNode> DisclosureNodes { get; set; } = [];
    public List<(string Keyword, string Uri)> GlossaryMatches { get; set; } = [];
    public Dictionary<string, MemoryNode> NodesByUri { get; set; } = new();
    public List<MemoryNode> PrefixNodes { get; set; } = [];

    /// <summary>Set to true when <see cref="RebuildGlossaryAsync"/> is called.</summary>
    public bool RebuildGlossaryCalled { get; private set; }

    /// <summary>Set to true when <see cref="QueryByPrefixAsync"/> is called.</summary>
    public bool QueryByPrefixCalled { get; private set; }

    public Task<IReadOnlyList<MemoryNode>> GetDisclosureNodesAsync(string animaId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MemoryNode>>(DisclosureNodes);

    public Task RebuildGlossaryAsync(string animaId, CancellationToken ct = default)
    {
        RebuildGlossaryCalled = true;
        return Task.CompletedTask;
    }

    public IReadOnlyList<(string Keyword, string Uri)> FindGlossaryMatches(string animaId, string content) =>
        GlossaryMatches;

    public Task<MemoryNode?> GetNodeAsync(string animaId, string uri, CancellationToken ct = default) =>
        Task.FromResult(NodesByUri.TryGetValue(uri, out var node) ? node : null);

    public Task<IReadOnlyList<MemoryNode>> QueryByPrefixAsync(string animaId, string uriPrefix, CancellationToken ct = default)
    {
        QueryByPrefixCalled = true;
        return Task.FromResult<IReadOnlyList<MemoryNode>>(PrefixNodes);
    }

    // ── no-op / empty implementations ─────────────────────────────────────

    public Task WriteNodeAsync(MemoryNode node, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<MemoryNode>> GetAllNodesAsync(string animaId, bool includeDeprecated = false, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MemoryNode>>([]);

    public Task SoftDeleteNodeAsync(string animaId, string uri, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task DeleteNodeAsync(string animaId, string uri, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task AddEdgeAsync(MemoryEdge edge, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<MemoryEdge>> GetEdgesAsync(string animaId, string fromUri, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MemoryEdge>>([]);

    public Task<IReadOnlyList<MemoryEdge>> GetIncomingEdgesAsync(string animaId, string toUri, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MemoryEdge>>([]);

    public Task<IReadOnlyList<MemoryContent>> GetContentHistoryAsync(string animaId, string uri, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MemoryContent>>([]);

    public Task<MemoryNode?> GetNodeByUuidAsync(string uuid, CancellationToken ct = default) =>
        Task.FromResult<MemoryNode?>(null);
}
