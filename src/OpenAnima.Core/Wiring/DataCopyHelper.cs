using System.Text.Json;

namespace OpenAnima.Core.Wiring;

/// <summary>
/// Deep copy utility for fan-out data isolation using JSON serialization.
/// </summary>
public static class DataCopyHelper
{
    /// <summary>
    /// Creates a deep copy of an object via JSON round-trip serialization.
    /// Optimized for null and string (immutable) cases.
    /// </summary>
    public static T DeepCopy<T>(T obj)
    {
        // Optimization: null returns default
        if (obj == null)
            return default!;

        // Optimization: strings are immutable, no copy needed
        if (obj is string str)
            return (T)(object)str;

        // JSON round-trip for deep copy
        var json = JsonSerializer.Serialize(obj);
        return JsonSerializer.Deserialize<T>(json)!;
    }
}
