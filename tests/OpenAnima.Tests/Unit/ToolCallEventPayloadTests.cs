using OpenAnima.Core.Events;

namespace OpenAnima.Tests.Unit;

public class ToolCallEventPayloadTests
{
    [Fact]
    public void ToolCallStartedPayload_HoldsValues()
    {
        var parameters = new Dictionary<string, string> { ["path"] = "/test.txt" };
        var payload = new ToolCallStartedPayload("read_file", parameters);
        Assert.Equal("read_file", payload.ToolName);
        Assert.Equal("/test.txt", payload.Parameters["path"]);
    }

    [Fact]
    public void ToolCallCompletedPayload_HoldsValues()
    {
        var payload = new ToolCallCompletedPayload("read_file", "file contents here", true);
        Assert.Equal("read_file", payload.ToolName);
        Assert.Equal("file contents here", payload.ResultSummary);
        Assert.True(payload.Success);
    }

    [Fact]
    public void ToolCallCompletedPayload_FailureCase()
    {
        var payload = new ToolCallCompletedPayload("shell_exec", "Error: command not found", false);
        Assert.False(payload.Success);
    }
}
