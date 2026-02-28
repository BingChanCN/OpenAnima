namespace OpenAnima.Cli.Services;

/// <summary>
/// Validates module names to ensure they are valid C# identifiers.
/// </summary>
public static class ModuleNameValidator
{
    // C# reserved keywords that cannot be used as identifiers
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while",
        // Contextual keywords (can technically be used but discouraged)
        "add", "alias", "ascending", "async", "await", "by", "descending", "dynamic", "equals",
        "from", "get", "global", "group", "into", "join", "let", "nameof", "on", "orderby",
        "partial", "remove", "select", "set", "value", "var", "when", "where", "yield"
    };

    /// <summary>
    /// Validates a module name and returns any validation errors.
    /// </summary>
    /// <param name="moduleName">The module name to validate.</param>
    /// <returns>List of validation errors. Empty if valid.</returns>
    public static List<string> Validate(string moduleName)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(moduleName))
        {
            errors.Add("Module name is required.");
            return errors;
        }

        // Check for reserved keywords
        if (ReservedKeywords.Contains(moduleName))
        {
            errors.Add($"Module name '{moduleName}' is a reserved C# keyword. Choose a different name.");
            return errors;
        }

        // Check first character - must be letter or underscore
        if (!char.IsLetter(moduleName[0]) && moduleName[0] != '_')
        {
            errors.Add($"Module name '{moduleName}' must start with a letter or underscore.");
        }

        // Check remaining characters
        for (int i = 1; i < moduleName.Length; i++)
        {
            if (!char.IsLetterOrDigit(moduleName[i]) && moduleName[i] != '_')
            {
                errors.Add($"Module name '{moduleName}' contains invalid character '{moduleName[i]}'. Only letters, digits, and underscores are allowed.");
                break;
            }
        }

        // Check for common issues
        if (moduleName.StartsWith("_") && moduleName.Length == 1)
        {
            errors.Add("Module name cannot be a single underscore.");
        }

        return errors;
    }

    /// <summary>
    /// Checks if a module name is valid.
    /// </summary>
    /// <param name="moduleName">The module name to check.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValid(string moduleName)
    {
        return Validate(moduleName).Count == 0;
    }

    /// <summary>
    /// Gets a suggestion for fixing an invalid module name.
    /// </summary>
    /// <param name="moduleName">The invalid module name.</param>
    /// <returns>A suggested valid name, or null if cannot suggest.</returns>
    public static string? GetSuggestion(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return "MyModule";
        }

        // If it's a reserved keyword, prefix with "My"
        if (ReservedKeywords.Contains(moduleName))
        {
            return $"My{char.ToUpper(moduleName[0])}{moduleName.Substring(1).ToLower()}";
        }

        // If starts with digit, prefix with "Module"
        if (char.IsDigit(moduleName[0]))
        {
            return $"Module{moduleName}";
        }

        // Replace invalid characters with underscores and ensure valid start
        var chars = moduleName.ToCharArray();
        if (!char.IsLetter(chars[0]) && chars[0] != '_')
        {
            chars[0] = '_';
        }

        for (int i = 1; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}