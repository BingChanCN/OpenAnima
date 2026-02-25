namespace OpenAnima.Core.Ports;

/// <summary>
/// Result of a port connection validation.
/// </summary>
public record ValidationResult(bool IsValid, string? ErrorMessage)
{
    public static ValidationResult Success() => new(true, null);
    public static ValidationResult Fail(string message) => new(false, message);
}
