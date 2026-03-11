namespace OpenAnima.Core.Routing;

/// <summary>
/// Result type for port registration operations.
/// Use static factory methods Success and DuplicateError to construct instances.
/// </summary>
public record RouteRegistrationResult(bool IsSuccess, string? Error)
{
    /// <summary>Creates a successful registration result.</summary>
    public static RouteRegistrationResult Success() => new(true, null);

    /// <summary>Creates a duplicate-error result when the port is already registered for this Anima.</summary>
    public static RouteRegistrationResult DuplicateError(string msg) => new(false, msg);
}
