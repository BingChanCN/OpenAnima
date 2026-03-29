using OpenAnima.Core.Modules;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for ToolCallParser — the pure static XML parser for &lt;tool_call&gt; markers.
/// </summary>
[Trait("Category", "AgentLoop")]
public class ToolCallParserTests
{
    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyResult()
    {
        var result = ToolCallParser.Parse("");

        Assert.Empty(result.ToolCalls);
        Assert.Equal("", result.PassthroughText);
        Assert.False(result.HasUnclosedMarker);
    }

    [Fact]
    public void Parse_NullString_ReturnsEmptyResult()
    {
        var result = ToolCallParser.Parse(null!);

        Assert.Empty(result.ToolCalls);
        Assert.Equal("", result.PassthroughText);
        Assert.False(result.HasUnclosedMarker);
    }

    [Fact]
    public void Parse_PlainTextNoMarkers_ReturnsPassthroughOnly()
    {
        var result = ToolCallParser.Parse("plain text with no markers");

        Assert.Empty(result.ToolCalls);
        Assert.Equal("plain text with no markers", result.PassthroughText);
        Assert.False(result.HasUnclosedMarker);
    }

    // ── Single tool call ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_SingleToolCall_ExtractsNameAndParam()
    {
        var input = "<tool_call name=\"file_read\"><param name=\"path\">/foo/bar.txt</param></tool_call>";

        var result = ToolCallParser.Parse(input);

        Assert.Single(result.ToolCalls);
        Assert.Equal("file_read", result.ToolCalls[0].ToolName);
        Assert.Equal("/foo/bar.txt", result.ToolCalls[0].Parameters["path"]);
        Assert.Equal("", result.PassthroughText);
        Assert.False(result.HasUnclosedMarker);
    }

    [Fact]
    public void Parse_ToolCallWithSurroundingText_StripsMarkerAndPreservesText()
    {
        var input = "Here is the answer\n<tool_call name=\"file_read\"><param name=\"path\">/foo</param></tool_call>\nDone";

        var result = ToolCallParser.Parse(input);

        Assert.Single(result.ToolCalls);
        Assert.Equal("file_read", result.ToolCalls[0].ToolName);
        Assert.Equal("/foo", result.ToolCalls[0].Parameters["path"]);
        // Passthrough text should have marker stripped
        Assert.Contains("Here is the answer", result.PassthroughText);
        Assert.Contains("Done", result.PassthroughText);
        Assert.DoesNotContain("tool_call", result.PassthroughText);
        Assert.False(result.HasUnclosedMarker);
    }

    // ── Multiple tool calls ───────────────────────────────────────────────────

    [Fact]
    public void Parse_TwoToolCalls_ReturnsBothInDocumentOrder()
    {
        var input = "<tool_call name=\"file_read\"><param name=\"path\">/a.txt</param></tool_call>" +
                    "<tool_call name=\"file_write\"><param name=\"path\">/b.txt</param></tool_call>";

        var result = ToolCallParser.Parse(input);

        Assert.Equal(2, result.ToolCalls.Count);
        Assert.Equal("file_read", result.ToolCalls[0].ToolName);
        Assert.Equal("file_write", result.ToolCalls[1].ToolName);
        Assert.False(result.HasUnclosedMarker);
    }

    // ── Multiple params ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_ToolCallWithMultipleParams_ExtractsAllParams()
    {
        var input = "<tool_call name=\"shell_exec\">" +
                    "<param name=\"command\">echo hello</param>" +
                    "<param name=\"timeout\">30</param>" +
                    "</tool_call>";

        var result = ToolCallParser.Parse(input);

        Assert.Single(result.ToolCalls);
        Assert.Equal("echo hello", result.ToolCalls[0].Parameters["command"]);
        Assert.Equal("30", result.ToolCalls[0].Parameters["timeout"]);
    }

    // ── Multiline param values ────────────────────────────────────────────────

    [Fact]
    public void Parse_MultilineParamValue_ExtractsCorrectly()
    {
        var input = "<tool_call name=\"file_read\"><param name=\"content\">line1\nline2\nline3</param></tool_call>";

        var result = ToolCallParser.Parse(input);

        Assert.Single(result.ToolCalls);
        Assert.Equal("line1\nline2\nline3", result.ToolCalls[0].Parameters["content"]);
    }

    // ── Unclosed tags ─────────────────────────────────────────────────────────

    [Fact]
    public void Parse_UnclosedToolCallTag_ReturnsHasUnclosedMarkerTrue()
    {
        var input = "<tool_call name=\"file_read\">";

        var result = ToolCallParser.Parse(input);

        Assert.True(result.HasUnclosedMarker);
        Assert.Empty(result.ToolCalls);
    }

    [Fact]
    public void Parse_UnclosedToolCallTagWithParams_ReturnsHasUnclosedMarkerTrue()
    {
        var input = "Some text <tool_call name=\"file_read\"><param name=\"path\">/foo</param> and no closing tag";

        var result = ToolCallParser.Parse(input);

        Assert.True(result.HasUnclosedMarker);
        Assert.Empty(result.ToolCalls);
    }

    // ── Case insensitivity ────────────────────────────────────────────────────

    [Fact]
    public void Parse_UpperCaseTagNames_ExtractsCorrectly()
    {
        var input = "<TOOL_CALL name=\"file_read\"><PARAM name=\"path\">/foo</PARAM></TOOL_CALL>";

        var result = ToolCallParser.Parse(input);

        Assert.Single(result.ToolCalls);
        Assert.Equal("file_read", result.ToolCalls[0].ToolName);
        Assert.Equal("/foo", result.ToolCalls[0].Parameters["path"]);
    }
}
