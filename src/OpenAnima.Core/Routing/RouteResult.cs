namespace OpenAnima.Core.Routing;

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
/// Use static factory methods Ok, Failed, and NotFound to construct instances.
/// </summary>
public record RouteResult(bool IsSuccess, string? Payload, RouteErrorKind? Error, string? CorrelationId)
{
    /// <summary>Creates a successful routing result with the response payload.</summary>
    public static RouteResult Ok(string payload, string correlationId) =>
        new(true, payload, null, correlationId);

    /// <summary>Creates a failed routing result with an error kind.</summary>
    public static RouteResult Failed(RouteErrorKind error, string correlationId) =>
        new(false, null, error, correlationId);

    /// <summary>Creates a not-found result when the target port is not registered.</summary>
    public static RouteResult NotFound(string key) =>
        new(false, null, RouteErrorKind.NotFound, null);
}
