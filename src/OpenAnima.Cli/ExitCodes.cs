namespace OpenAnima.Cli;

/// <summary>
/// Exit codes for CLI operations.
/// </summary>
public static class ExitCodes
{
    /// <summary>
    /// Operation completed successfully.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// General error occurred during execution.
    /// </summary>
    public const int GeneralError = 1;

    /// <summary>
    /// Validation error (invalid input, missing required fields).
    /// </summary>
    public const int ValidationError = 2;
}