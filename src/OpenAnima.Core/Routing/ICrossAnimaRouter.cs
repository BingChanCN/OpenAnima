namespace OpenAnima.Core.Routing;

/// <summary>
/// Public API surface for cross-Anima routing.
/// Manages a port registry for service discovery and a pending request map for request-response correlation.
/// </summary>
public interface ICrossAnimaRouter : IDisposable
{
    /// <summary>
    /// Register an input port on an Anima. Returns a duplicate error if the port name is already
    /// registered for the given Anima. Different Animas may register ports with the same name.
    /// </summary>
    RouteRegistrationResult RegisterPort(string animaId, string portName, string description);

    /// <summary>
    /// Unregister an input port on Anima deletion or module removal.
    /// Idempotent — does not throw if the port is not registered.
    /// </summary>
    void UnregisterPort(string animaId, string portName);

    /// <summary>
    /// Returns all registered ports for a given Anima.
    /// Returns an empty list if the Anima has no registered ports.
    /// </summary>
    IReadOnlyList<PortRegistration> GetPortsForAnima(string animaId);

    /// <summary>
    /// Route a request to the target animaId::portName. Times out after the configured duration
    /// (default 30 seconds). Returns NotFound immediately if the target port is not registered.
    /// </summary>
    Task<RouteResult> RouteRequestAsync(
        string targetAnimaId,
        string portName,
        string payload,
        TimeSpan? timeout = null,
        CancellationToken ct = default);

    /// <summary>
    /// Complete a pending request by correlation ID. Returns true if the request was found and completed;
    /// false if the correlation ID is unknown (already completed, timed out, or never existed).
    /// Called by AnimaOutputPort in Phase 29.
    /// </summary>
    bool CompleteRequest(string correlationId, string responsePayload);

    /// <summary>
    /// Fail all pending requests targeting the specified Anima with RouteErrorKind.Cancelled.
    /// Called by AnimaRuntimeManager.DeleteAsync before disposing the runtime.
    /// </summary>
    void CancelPendingForAnima(string animaId);

    /// <summary>
    /// Remove all registry entries for a given Anima.
    /// Used on Anima deletion to clean up the port registry.
    /// </summary>
    void UnregisterAllForAnima(string animaId);
}
