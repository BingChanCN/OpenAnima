using System.Text.RegularExpressions;

namespace OpenAnima.Core.Modules;

/// <summary>
/// A single extracted tool call from an LLM response.
/// </summary>
/// <param name="ToolName">The tool name from the <c>name</c> attribute.</param>
/// <param name="Parameters">All parameters extracted from nested &lt;param&gt; elements.</param>
public record ToolCallExtraction(string ToolName, IReadOnlyDictionary<string, string> Parameters);

/// <summary>
/// The result of running ToolCallParser.Parse on an LLM response.
/// </summary>
/// <param name="ToolCalls">All extracted tool calls, in document order.</param>
/// <param name="PassthroughText">The LLM text with all valid tool_call markers stripped and trimmed.</param>
/// <param name="HasUnclosedMarker">True when an unclosed &lt;tool_call&gt; tag was detected.</param>
public record ToolCallParseResult(
    IReadOnlyList<ToolCallExtraction> ToolCalls,
    string PassthroughText,
    bool HasUnclosedMarker);

/// <summary>
/// Pure static XML parser for &lt;tool_call&gt; markers in LLM output.
/// Mirrors the FormatDetector pattern: compiled regex fields, stateless, thread-safe.
/// </summary>
public static class ToolCallParser
{
    // Matches a well-formed <tool_call name="...">...</tool_call> marker.
    // Singleline so that . matches \n, allowing multiline param values.
    private static readonly Regex ToolCallRegex = new(
        @"<tool_call\s+name\s*=\s*""([^""]*)""\s*>(.*?)</tool_call>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // Matches <param name="...">value</param> inside a tool_call body.
    private static readonly Regex ParamRegex = new(
        @"<param\s+name\s*=\s*""([^""]*)""\s*>(.*?)</param>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // Detects unclosed <tool_call ...> tags (fast-path for malformed LLM output):
    //   (a) a complete open tag with no matching </tool_call> after it, or
    //   (b) an incomplete open tag that never has its closing >
    private static readonly Regex UnclosedRegex = new(
        @"<tool_call(?:\b[^>]*>(?![\s\S]*</tool_call>)|(?![^>]*>))",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// Scans <paramref name="response"/> for tool_call markers, extracts them, and
    /// returns the clean passthrough text alongside all parsed tool calls.
    /// </summary>
    /// <param name="response">The full LLM response string. Null is treated as empty.</param>
    /// <returns>A <see cref="ToolCallParseResult"/> describing what was found.</returns>
    public static ToolCallParseResult Parse(string? response)
    {
        // 1. Fast-path: null or empty input.
        if (string.IsNullOrEmpty(response))
            return new ToolCallParseResult([], response ?? "", false);

        // 2. Fast-path: check for any unclosed <tool_call> tags before attempting extraction.
        if (UnclosedRegex.IsMatch(response))
            return new ToolCallParseResult([], response, true);

        // 3. Extract well-formed tool_call markers, building list in document order.
        var toolCalls = new List<ToolCallExtraction>();

        var passthrough = ToolCallRegex.Replace(response, match =>
        {
            var toolName = match.Groups[1].Value;
            var body = match.Groups[2].Value;

            // Parse all <param name="...">value</param> elements from the body.
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match paramMatch in ParamRegex.Matches(body))
            {
                var paramName = paramMatch.Groups[1].Value;
                var paramValue = paramMatch.Groups[2].Value;
                parameters[paramName] = paramValue;
            }

            toolCalls.Add(new ToolCallExtraction(toolName, parameters));
            return string.Empty; // strip the marker from passthrough text
        });

        // 4. Trim outer whitespace from passthrough.
        passthrough = passthrough.Trim();

        return new ToolCallParseResult(toolCalls, passthrough, false);
    }
}
