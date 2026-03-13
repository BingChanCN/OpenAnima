using OpenAnima.Core.Modules;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for FormatDetector — XML routing marker parser.
/// Covers all behaviors defined in 30-01-PLAN.md.
/// </summary>
[Trait("Category", "FormatDetection")]
public class FormatDetectorTests
{
    private readonly FormatDetector _detector = new();

    private static IReadOnlySet<string> Services(params string[] names)
        => new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

    // ── No-marker pass-through ────────────────────────────────────────────────

    [Fact]
    public void Detect_PlainText_ReturnsUnchangedTextEmptyRoutesNoError()
    {
        var result = _detector.Detect("plain text with no markers", Services("svc"));

        Assert.Equal("plain text with no markers", result.PassthroughText);
        Assert.Empty(result.Routes);
        Assert.Null(result.MalformedMarkerError);
    }

    [Fact]
    public void Detect_EmptyString_ReturnsEmptyTextEmptyRoutesNoError()
    {
        var result = _detector.Detect("", Services("svc"));

        Assert.Equal("", result.PassthroughText);
        Assert.Empty(result.Routes);
        Assert.Null(result.MalformedMarkerError);
    }

    // ── Well-formed single marker ─────────────────────────────────────────────

    [Fact]
    public void Detect_SingleWellFormedMarker_ExtractsRouteAndStripsMarker()
    {
        var result = _detector.Detect(
            "Hello <route service=\"summarize\">payload</route> world",
            Services("summarize"));

        Assert.Single(result.Routes);
        Assert.Equal("summarize", result.Routes[0].ServiceName);
        Assert.Equal("payload", result.Routes[0].Payload);
        Assert.Null(result.MalformedMarkerError);
        // Marker stripped; surrounding text preserved (trimmed)
        Assert.DoesNotContain("<route", result.PassthroughText);
        Assert.Contains("Hello", result.PassthroughText);
        Assert.Contains("world", result.PassthroughText);
    }

    // ── Case-insensitive service name matching ────────────────────────────────

    [Fact]
    public void Detect_UpperCaseServiceName_MatchesCaseInsensitively()
    {
        var result = _detector.Detect(
            "<route service=\"Summarize\">p</route>",
            Services("summarize"));

        Assert.Single(result.Routes);
        Assert.Equal("summarize", result.Routes[0].ServiceName);
        Assert.Null(result.MalformedMarkerError);
    }

    [Fact]
    public void Detect_MixedCaseServiceName_MatchesCaseInsensitively()
    {
        var result = _detector.Detect(
            "<route service=\"SuMmArIzE\">p</route>",
            Services("summarize"));

        Assert.Single(result.Routes);
        Assert.Equal("summarize", result.Routes[0].ServiceName);
        Assert.Null(result.MalformedMarkerError);
    }

    // ── Multiple markers ──────────────────────────────────────────────────────

    [Fact]
    public void Detect_MultipleMarkers_ExtractsAllInOrder()
    {
        var result = _detector.Detect(
            "text <route service=\"svc\">p1</route> mid <route service=\"svc2\">p2</route> end",
            Services("svc", "svc2"));

        Assert.Equal(2, result.Routes.Count);
        Assert.Equal("svc", result.Routes[0].ServiceName);
        Assert.Equal("p1", result.Routes[0].Payload);
        Assert.Equal("svc2", result.Routes[1].ServiceName);
        Assert.Equal("p2", result.Routes[1].Payload);
        Assert.Null(result.MalformedMarkerError);
    }

    // ── Multiline payload ─────────────────────────────────────────────────────

    [Fact]
    public void Detect_MultilinePayload_ExtractsFullPayload()
    {
        var result = _detector.Detect(
            "text <route service=\"svc\">multi\nline\npayload</route>",
            Services("svc"));

        Assert.Single(result.Routes);
        Assert.Equal("multi\nline\npayload", result.Routes[0].Payload);
        Assert.Null(result.MalformedMarkerError);
    }

    // ── Unrecognized service name ─────────────────────────────────────────────

    [Fact]
    public void Detect_UnrecognizedServiceName_ReturnsMalformedError()
    {
        var result = _detector.Detect(
            "text <route service=\"unknown\">p</route>",
            Services("svc"));

        Assert.NotNull(result.MalformedMarkerError);
        Assert.Contains("unknown", result.MalformedMarkerError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Detect_UnrecognizedServiceName_LeavesMarkerInPassthroughText()
    {
        var result = _detector.Detect(
            "text <route service=\"unknown\">p</route>",
            Services("svc"));

        Assert.Contains("unknown", result.PassthroughText);
        Assert.Contains("<route", result.PassthroughText);
    }

    // ── Unclosed / malformed tags ─────────────────────────────────────────────

    [Fact]
    public void Detect_UnclosedRouteTag_ReturnsMalformedErrorWithUnclosed()
    {
        var result = _detector.Detect(
            "text <route service=\"svc\">unclosed tag",
            Services("svc"));

        Assert.NotNull(result.MalformedMarkerError);
        Assert.Contains("Unclosed", result.MalformedMarkerError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Detect_PartialRouteTagWithoutAttribute_ReturnsMalformedError()
    {
        var result = _detector.Detect(
            "text with <route but no closing",
            Services("svc"));

        Assert.NotNull(result.MalformedMarkerError);
        Assert.Contains("Unclosed", result.MalformedMarkerError, StringComparison.OrdinalIgnoreCase);
    }

    // ── Lenient whitespace in attributes ─────────────────────────────────────

    [Fact]
    public void Detect_ExtraWhitespaceInAttribute_StillParses()
    {
        var result = _detector.Detect(
            "<route  service = \"svc\" >payload</route>",
            Services("svc"));

        Assert.Single(result.Routes);
        Assert.Equal("svc", result.Routes[0].ServiceName);
        Assert.Equal("payload", result.Routes[0].Payload);
        Assert.Null(result.MalformedMarkerError);
    }

    // ── Passthrough text cleanup ──────────────────────────────────────────────

    [Fact]
    public void Detect_TextBeforeAndAfterMarker_PassthroughContainsBothParts()
    {
        var result = _detector.Detect(
            "Before text\n<route service=\"svc\">payload</route>\nAfter text",
            Services("svc"));

        Assert.Contains("Before text", result.PassthroughText);
        Assert.Contains("After text", result.PassthroughText);
        Assert.DoesNotContain("<route", result.PassthroughText);
    }

    [Fact]
    public void Detect_OnlyMarker_PassthroughTextIsEmptyOrWhitespace()
    {
        var result = _detector.Detect(
            "<route service=\"svc\">payload</route>",
            Services("svc"));

        Assert.Equal(string.Empty, result.PassthroughText.Trim());
    }

    // ── Record equality and structure ─────────────────────────────────────────

    [Fact]
    public void RouteExtraction_RecordEquality_Works()
    {
        var r1 = new RouteExtraction("svc", "payload");
        var r2 = new RouteExtraction("svc", "payload");
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void FormatDetectionResult_Routes_AreReadOnlyList()
    {
        var result = _detector.Detect("no markers", Services("svc"));
        Assert.IsAssignableFrom<IReadOnlyList<RouteExtraction>>(result.Routes);
    }
}
