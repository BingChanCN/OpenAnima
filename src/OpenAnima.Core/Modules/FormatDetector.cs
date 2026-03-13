using System.Text;
using System.Text.RegularExpressions;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Represents a single extracted routing marker from LLM output.
/// </summary>
/// <param name="ServiceName">The (normalised, lower-case) service name from the marker.</param>
/// <param name="Payload">The text content between the opening and closing route tags.</param>
public record RouteExtraction(string ServiceName, string Payload);

/// <summary>
/// The result of running FormatDetector.Detect on an LLM response.
/// </summary>
/// <param name="PassthroughText">
/// The LLM text with all valid route markers stripped.
/// Unrecognised-service markers are left in place.
/// Multiple consecutive blank lines are collapsed to a single blank line.
/// </param>
/// <param name="Routes">All successfully extracted (serviceName, payload) pairs, in document order.</param>
/// <param name="MalformedMarkerError">
/// Non-null when an unclosed tag or unrecognised service name was detected;
/// null when the response was entirely well-formed.
/// </param>
public record FormatDetectionResult(
    string PassthroughText,
    IReadOnlyList<RouteExtraction> Routes,
    string? MalformedMarkerError);

/// <summary>
/// Pure detection class — scans LLM output for <c>&lt;route service="portName"&gt;payload&lt;/route&gt;</c>
/// XML-style markers, extracts routing payloads, strips markers from passthrough text, and
/// identifies malformed markers.
///
/// No external dependencies beyond .NET BCL. Thread-safe (stateless; all state local to Detect).
/// </summary>
public class FormatDetector
{
    // Matches a well-formed <route service="...">...</route> marker (singleline so . matches \n).
    private static readonly Regex RouteMarkerRegex = new(
        @"<route\s+service\s*=\s*""([^""]*)""\s*>(.*?)</route>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // Matches any <route start that is either:
    //   (a) a complete open tag <route …> with no matching </route> after it, or
    //   (b) an incomplete open tag that never has its closing >
    // Singleline so that the lookahead can scan across newlines.
    private static readonly Regex UnclosedMarkerRegex = new(
        @"<route(?:\b[^>]*>(?![\s\S]*</route>)|(?![^>]*>))",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // Collapses three-or-more consecutive newlines (with optional whitespace between them) to \n\n.
    private static readonly Regex ExcessiveBlankLinesRegex = new(
        @"(\r?\n[ \t]*){3,}",
        RegexOptions.Compiled);

    /// <summary>
    /// Scans <paramref name="response"/> for routing markers.
    /// </summary>
    /// <param name="response">The full LLM response string.</param>
    /// <param name="knownServiceNames">
    /// Set of service names the current Anima has configured (case-insensitive comparison).
    /// </param>
    /// <returns>A <see cref="FormatDetectionResult"/> describing what was found.</returns>
    public FormatDetectionResult Detect(string response, IReadOnlySet<string> knownServiceNames)
    {
        if (string.IsNullOrEmpty(response))
            return new FormatDetectionResult(response, Array.Empty<RouteExtraction>(), null);

        // 1. Fast-path: check for any unclosed <route …> tags before attempting extraction.
        if (UnclosedMarkerRegex.IsMatch(response))
        {
            return new FormatDetectionResult(
                response,
                Array.Empty<RouteExtraction>(),
                "Unclosed <route> tag detected in LLM response.");
        }

        // 2. Extract well-formed markers, validating service names on the fly.
        var routes = new List<RouteExtraction>();
        string? malformedError = null;

        var passthrough = RouteMarkerRegex.Replace(response, match =>
        {
            var rawServiceName = match.Groups[1].Value;
            var payload = match.Groups[2].Value;

            // Look up the service name case-insensitively against the known set.
            // knownServiceNames uses OrdinalIgnoreCase per the factory helper in tests,
            // so Contains is already case-insensitive — but we also normalise the stored name.
            string? resolvedName = null;
            foreach (var known in knownServiceNames)
            {
                if (string.Equals(known, rawServiceName, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedName = known.ToLowerInvariant();
                    break;
                }
            }

            if (resolvedName == null)
            {
                // Unrecognised service — set error, leave the marker text in place.
                malformedError = $"Unrecognised service name '{rawServiceName}' in <route> marker.";
                return match.Value; // preserve original text
            }

            routes.Add(new RouteExtraction(resolvedName, payload));
            return string.Empty; // strip the marker
        });

        // 3. Normalise passthrough: trim outer whitespace, collapse excessive blank lines.
        passthrough = ExcessiveBlankLinesRegex.Replace(passthrough, "\n\n");
        passthrough = passthrough.Trim();

        return new FormatDetectionResult(passthrough, routes, malformedError);
    }
}
