namespace OpenAnima.Core.Routing;

/// <summary>
/// Record representing an in-flight cross-Anima request awaiting a response.
/// Holds the TaskCompletionSource for async correlation and a CancellationTokenSource for timeout enforcement.
/// </summary>
public record PendingRequest(
    string CorrelationId,
    TaskCompletionSource<RouteResult> Tcs,
    CancellationTokenSource Cts,
    DateTimeOffset ExpiresAt,
    string TargetAnimaId);
