namespace OpenAnima.Core.ViewportPersistence;

/// <summary>
/// Represents the viewport state (pan and zoom position) for an Anima's wiring editor.
/// </summary>
public record ViewportState
{
    /// <summary>The zoom scale of the viewport. Default: 1.0 (100% zoom).</summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>The horizontal pan position of the viewport. Default: 0.</summary>
    public double PanX { get; init; } = 0;

    /// <summary>The vertical pan position of the viewport. Default: 0.</summary>
    public double PanY { get; init; } = 0;
}
