namespace OpenAnima.Contracts.Routing;

/// <summary>
/// Categorizes routing failures for callers to take appropriate action.
/// </summary>
public enum RouteErrorKind
{
    /// <summary>Request exceeded the configured timeout duration.</summary>
    Timeout,

    /// <summary>Target animaId::portName does not exist in the registry.</summary>
    NotFound,

    /// <summary>Target Anima was deleted while the request was pending.</summary>
    Cancelled,

    /// <summary>Target processing failed (generic catch-all).</summary>
    Failed
}

/// <summary>
/// Result type for cross-Anima routing operations.
/// Use static factory methods <see cref="Ok"/>, <see cref="Failed"/>, and <see cref="NotFound"/>
/// to construct instances.
/// </summary>
/// <param name="IsSuccess">Whether the routing operation succeeded.</param>
/// <param name="Payload">The response payload when <paramref name="IsSuccess"/> is true; null otherwise.</param>
/// <param name="Error">The error kind when <paramref name="IsSuccess"/> is false; null otherwise.</param>
/// <param name="CorrelationId">The correlation ID for this routing operation; null for not-found results.</param>
public record RouteResult(bool IsSuccess, string? Payload, RouteErrorKind? Error, string? CorrelationId)
{
    /// <summary>Creates a successful routing result with the response payload.</summary>
    /// <param name="payload">The response payload returned by the target port.</param>
    /// <param name="correlationId">The correlation ID for this routing operation.</param>
    public static RouteResult Ok(string payload, string correlationId) =>
        new(true, payload, null, correlationId);

    /// <summary>Creates a failed routing result with an error kind.</summary>
    /// <param name="error">The categorized error reason for the failure.</param>
    /// <param name="correlationId">The correlation ID for this routing operation.</param>
    public static RouteResult Failed(RouteErrorKind error, string correlationId) =>
        new(false, null, error, correlationId);

    /// <summary>Creates a not-found result when the target port is not registered.</summary>
    /// <param name="key">The key (animaId::portName) that was not found.</param>
    public static RouteResult NotFound(string key) =>
        new(false, null, RouteErrorKind.NotFound, null);
}
