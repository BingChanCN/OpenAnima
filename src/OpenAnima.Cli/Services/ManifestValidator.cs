using OpenAnima.Cli.Models;

namespace OpenAnima.Cli.Services;

/// <summary>
/// Validates module manifest files and provides clear error messages.
/// </summary>
public static class ManifestValidator
{
    /// <summary>
    /// Validates a module manifest and returns all validation errors.
    /// </summary>
    /// <param name="manifest">The manifest to validate.</param>
    /// <returns>List of validation error messages. Empty if valid.</returns>
    public static List<string> Validate(ModuleManifest manifest)
    {
        var errors = new List<string>();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            errors.Add("Required field 'id' is missing or empty.");
        }
        else
        {
            // Validate id format (valid C# identifier)
            if (!IsValidIdentifier(manifest.Id))
            {
                errors.Add($"Field 'id' value '{manifest.Id}' is not a valid identifier. Use alphanumeric characters and underscores, starting with a letter or underscore.");
            }
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            errors.Add("Required field 'name' is missing or empty.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            errors.Add("Required field 'version' is missing or empty.");
        }
        else if (!IsValidVersion(manifest.Version))
        {
            errors.Add($"Field 'version' value '{manifest.Version}' is not a valid semantic version. Expected format: X.Y.Z (e.g., 1.0.0).");
        }

        // Validate port declarations
        ValidatePorts(manifest.Ports.Inputs, "inputs", errors);
        ValidatePorts(manifest.Ports.Outputs, "outputs", errors);

        // Validate OpenAnima compatibility if specified
        if (manifest.OpenAnima != null)
        {
            if (!string.IsNullOrWhiteSpace(manifest.OpenAnima.MinVersion) && !IsValidVersion(manifest.OpenAnima.MinVersion))
            {
                errors.Add($"Field 'openanima.minVersion' value '{manifest.OpenAnima.MinVersion}' is not a valid semantic version.");
            }

            if (!string.IsNullOrWhiteSpace(manifest.OpenAnima.MaxVersion) && !IsValidVersion(manifest.OpenAnima.MaxVersion))
            {
                errors.Add($"Field 'openanima.maxVersion' value '{manifest.OpenAnima.MaxVersion}' is not a valid semantic version.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates a JSON string as a module manifest.
    /// </summary>
    /// <param name="json">JSON content to validate.</param>
    /// <returns>Tuple of (manifest if valid, list of errors).</returns>
    public static (ModuleManifest? Manifest, List<string> Errors) ValidateJson(string json)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add("JSON content is empty or null.");
            return (null, errors);
        }

        try
        {
            var manifest = System.Text.Json.JsonSerializer.Deserialize<ModuleManifest>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
            });

            if (manifest == null)
            {
                errors.Add("Failed to parse JSON: result is null.");
                return (null, errors);
            }

            // Validate the manifest fields
            var fieldErrors = Validate(manifest);
            errors.AddRange(fieldErrors);

            return (manifest, errors);
        }
        catch (System.Text.Json.JsonException ex)
        {
            errors.Add($"JSON parsing error: {ex.Message}");
            return (null, errors);
        }
    }

    /// <summary>
    /// Validates port declarations.
    /// </summary>
    private static void ValidatePorts(List<PortDeclaration> ports, string direction, List<string> errors)
    {
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < ports.Count; i++)
        {
            var port = ports[i];
            var prefix = $"ports.{direction}[{i}]";

            if (string.IsNullOrWhiteSpace(port.Name))
            {
                errors.Add($"{prefix}: Port name is missing or empty.");
            }
            else
            {
                if (!IsValidIdentifier(port.Name))
                {
                    errors.Add($"{prefix}: Port name '{port.Name}' is not a valid identifier.");
                }

                if (seenNames.Contains(port.Name))
                {
                    errors.Add($"{prefix}: Duplicate port name '{port.Name}' in {direction}.");
                }
                else
                {
                    seenNames.Add(port.Name);
                }
            }

            if (!port.IsValidType())
            {
                errors.Add($"{prefix}: Invalid port type '{port.Type}'. Valid types: Text, Trigger.");
            }
        }
    }

    /// <summary>
    /// Checks if a string is a valid C# identifier.
    /// </summary>
    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        // Must start with letter or underscore
        if (!char.IsLetter(value[0]) && value[0] != '_')
            return false;

        // Rest must be letters, digits, or underscores
        for (int i = 1; i < value.Length; i++)
        {
            if (!char.IsLetterOrDigit(value[i]) && value[i] != '_')
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a string is a valid semantic version (X.Y.Z format).
    /// </summary>
    private static bool IsValidVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split('.');
        if (parts.Length < 2 || parts.Length > 3)
            return false;

        foreach (var part in parts)
        {
            if (!int.TryParse(part, out _))
                return false;
        }

        return true;
    }
}