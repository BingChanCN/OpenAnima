namespace OpenAnima.Core.Runs;

/// <summary>
/// Records step execution events for active durable task runs.
/// Called inline in the <see cref="OpenAnima.Core.Wiring.WiringEngine"/> routing path to capture
/// module execution as durable step records.
/// </summary>
public interface IStepRecorder
{
    /// <summary>
    /// Records the start of a step for the module being invoked.
    /// </summary>
    /// <param name="animaId">The Anima whose active run this step belongs to.</param>
    /// <param name="moduleName">The name of the module that is starting execution.</param>
    /// <param name="inputSummary">Optional summary of the input payload (truncated to 500 chars).</param>
    /// <param name="propagationId">Optional propagation chain identifier for causal graph reconstruction.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A step ID string if an active run is found; null if recording is a no-op (no active run).</returns>
    Task<string?> RecordStepStartAsync(
        string animaId,
        string moduleName,
        string? inputSummary,
        string? propagationId,
        CancellationToken ct = default);

    /// <summary>
    /// Records the successful completion of a step. Runs the convergence check and may trigger
    /// an auto-pause if budgets are exhausted or a non-productive pattern is detected.
    /// </summary>
    /// <param name="stepId">The step ID returned by <see cref="RecordStepStartAsync"/>. If null, this is a no-op.</param>
    /// <param name="moduleName">The name of the module that completed execution.</param>
    /// <param name="outputSummary">Optional summary of the output payload (truncated to 500 chars).</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordStepCompleteAsync(
        string? stepId,
        string moduleName,
        string? outputSummary,
        CancellationToken ct = default);

    /// <summary>
    /// Records step completion with optional artifact content. When artifactContent is non-null,
    /// the content is persisted as a durable artifact and the step's ArtifactRefId is set.
    /// </summary>
    /// <param name="stepId">The step ID returned by <see cref="RecordStepStartAsync"/>. If null, this is a no-op.</param>
    /// <param name="moduleName">The name of the module that completed execution.</param>
    /// <param name="outputSummary">Optional summary of the output payload (truncated to 500 chars).</param>
    /// <param name="artifactContent">Optional full artifact content to persist durably.</param>
    /// <param name="artifactMimeType">MIME type for the artifact. Defaults to "text/plain" if null.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordStepCompleteAsync(
        string? stepId,
        string moduleName,
        string? outputSummary,
        string? artifactContent,
        string? artifactMimeType,
        CancellationToken ct = default);

    /// <summary>
    /// Records a step that failed due to an exception.
    /// </summary>
    /// <param name="stepId">The step ID returned by <see cref="RecordStepStartAsync"/>. If null, this is a no-op.</param>
    /// <param name="moduleName">The name of the module that failed.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordStepFailedAsync(
        string? stepId,
        string moduleName,
        Exception ex,
        CancellationToken ct = default);
}
