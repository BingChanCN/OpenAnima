using Microsoft.Extensions.Logging;
using OpenAnima.Core.Runs;

namespace OpenAnima.Core.Memory;

/// <summary>
/// Injects boot memory nodes from the core:// namespace as inspectable StepRecords at run start.
/// Each core:// node is recorded as a "BootMemory" step with provenance linking to the memory graph.
/// This makes boot context visible in the run timeline for observability and debugging.
/// </summary>
public class BootMemoryInjector
{
    private readonly IMemoryGraph _memoryGraph;
    private readonly IStepRecorder _stepRecorder;
    private readonly ILogger<BootMemoryInjector> _logger;

    public BootMemoryInjector(
        IMemoryGraph memoryGraph,
        IStepRecorder stepRecorder,
        ILogger<BootMemoryInjector> logger)
    {
        _memoryGraph = memoryGraph ?? throw new ArgumentNullException(nameof(memoryGraph));
        _stepRecorder = stepRecorder ?? throw new ArgumentNullException(nameof(stepRecorder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Queries all core:// memory nodes for the given Anima and records each as a BootMemory step.
    /// If no boot nodes exist, this method is a no-op.
    /// </summary>
    /// <param name="animaId">The Anima to inject boot memories for.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task InjectBootMemoriesAsync(string animaId, CancellationToken ct = default)
    {
        var bootNodes = await _memoryGraph.QueryByPrefixAsync(animaId, "core://", ct);
        if (bootNodes.Count == 0)
        {
            _logger.LogDebug("No boot memory nodes for Anima {AnimaId}", animaId);
            return;
        }

        _logger.LogInformation("Injecting {Count} boot memories for Anima {AnimaId}", bootNodes.Count, animaId);
        foreach (var node in bootNodes)
        {
            var stepId = await _stepRecorder.RecordStepStartAsync(
                animaId, "BootMemory", $"Boot: {node.Uri}", null, ct);
            var summary = node.Content.Length > 500 ? node.Content[..500] : node.Content;
            await _stepRecorder.RecordStepCompleteAsync(stepId, "BootMemory", summary, ct);
        }
    }
}
