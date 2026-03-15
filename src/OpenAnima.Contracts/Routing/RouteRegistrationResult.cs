namespace OpenAnima.Contracts.Routing;

/// <summary>
/// Result type for port registration operations.
/// Use static factory methods <see cref="Success"/> and <see cref="DuplicateError"/> to construct instances.
/// </summary>
/// <param name="IsSuccess">Whether the registration succeeded.</param>
/// <param name="Error">An error message when <paramref name="IsSuccess"/> is false; null on success.</param>
public record RouteRegistrationResult(bool IsSuccess, string? Error)
{
    /// <summary>Creates a successful registration result.</summary>
    public static RouteRegistrationResult Success() => new(true, null);

    /// <summary>Creates a duplicate-error result when the port is already registered for this Anima.</summary>
    /// <param name="msg">A message describing the duplicate conflict.</param>
    public static RouteRegistrationResult DuplicateError(string msg) => new(false, msg);
}
