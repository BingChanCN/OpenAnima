namespace OpenAnima.Core.Workflows;

/// <summary>
/// Discovers workflow preset configurations from the presets subdirectory
/// of the wiring-configs directory.
/// </summary>
public class WorkflowPresetService
{
    private readonly string _presetsDir;

    public WorkflowPresetService(string presetsDir)
    {
        _presetsDir = presetsDir;
    }

    /// <summary>
    /// Lists all available workflow presets found in the presets directory.
    /// Returns an empty list if the directory does not exist.
    /// </summary>
    public IReadOnlyList<WorkflowPresetInfo> ListPresets()
    {
        if (!Directory.Exists(_presetsDir)) return [];
        return Directory.GetFiles(_presetsDir, "preset-*.json")
            .Select(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var displayName = BuildDisplayName(name);
                return new WorkflowPresetInfo(name, displayName);
            })
            .OrderBy(p => p.Name)
            .ToList();
    }

    /// <summary>
    /// Returns the full file path for a preset by name (without extension).
    /// Returns null if the file does not exist.
    /// </summary>
    public string? GetPresetPath(string presetName)
    {
        var path = Path.Combine(_presetsDir, $"{presetName}.json");
        return File.Exists(path) ? path : null;
    }

    private static string BuildDisplayName(string name)
    {
        // "preset-codebase-analysis" -> "Codebase Analysis"
        var parts = name.Replace("preset-", "").Split('-');
        return string.Join(" ", parts.Select(p =>
            p.Length > 0 ? char.ToUpper(p[0]) + p[1..] : p));
    }
}

/// <summary>Describes an available workflow preset.</summary>
public record WorkflowPresetInfo(string Name, string DisplayName, string? Description = null);
