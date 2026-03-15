namespace OpenAnima.Contracts.Routing;

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
    /// <param name="animaId">The Anima instance identifier.</param>
    /// <param name="portName">The unique port name within the Anima.</param>
    /// <param name="description">Human-readable description of this port's purpose.</param>
    /// <returns>A result indicating success or a duplicate-port error.</returns>
    RouteRegistrationResult RegisterPort(string animaId, string portName, string description);

    /// <summary>
    /// Unregister an input port on Anima deletion or module removal.
    /// Idempotent — does not throw if the port is not registered.
    /// </summary>
    /// <param name="animaId">The Anima instance identifier.</param>
    /// <param name="portName">The port name to unregister.</param>
    void UnregisterPort(string animaId, string portName);

    /// <summary>
    /// Returns all registered ports for a given Anima.
    /// Returns an empty list if the Anima has no registered ports.
    /// </summary>
    /// <param name="animaId">The Anima instance identifier.</param>
    /// <returns>A read-only list of port registrations for the Anima.</returns>
    IReadOnlyList<PortRegistration> GetPortsForAnima(string animaId);

    /// <summary>
    /// Route a request to the target animaId::portName. Times out after the configured duration
    /// (default 30 seconds). Returns NotFound immediately if the target port is not registered.
    /// </summary>
    /// <param name="targetAnimaId">The target Anima instance identifier.</param>
    /// <param name="portName">The target port name on the Anima.</param>
    /// <param name="payload">The request payload as a string.</param>
    /// <param name="timeout">Optional timeout override; uses router default if null.</param>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>A route result containing the response payload or an error kind.</returns>
    Task<RouteResult> RouteRequestAsync(
        string targetAnimaId,
        string portName,
        string payload,
        TimeSpan? timeout = null,
        CancellationToken ct = default);

    /// <summary>
    /// Complete a pending request by correlation ID. Returns true if the request was found and completed;
    /// false if the correlation ID is unknown (already completed, timed out, or never existed).
    /// Called by AnimaOutputPort when a response is ready.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the pending request.</param>
    /// <param name="responsePayload">The response payload to deliver to the caller.</param>
    /// <returns>True if the request was found and completed; false otherwise.</returns>
    bool CompleteRequest(string correlationId, string responsePayload);

    /// <summary>
    /// Fail all pending requests targeting the specified Anima with <see cref="RouteErrorKind.Cancelled"/>.
    /// Called by the runtime manager before disposing the Anima runtime.
    /// </summary>
    /// <param name="animaId">The Anima instance identifier whose pending requests should be cancelled.</param>
    void CancelPendingForAnima(string animaId);

    /// <summary>
    /// Remove all registry entries for a given Anima.
    /// Used on Anima deletion to clean up the port registry.
    /// </summary>
    /// <param name="animaId">The Anima instance identifier to fully unregister.</param>
    void UnregisterAllForAnima(string animaId);
}
