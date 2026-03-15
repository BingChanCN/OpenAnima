namespace OpenAnima.Contracts;

/// <summary>
/// Describes the data type and UI rendering hint for a module configuration field.
/// Used by <see cref="ConfigFieldDescriptor"/> to indicate how a field should be
/// displayed and validated in the sidebar configuration UI.
/// </summary>
public enum ConfigFieldType
{
    /// <summary>A single-line plain text input.</summary>
    String,

    /// <summary>An integer numeric input.</summary>
    Int,

    /// <summary>A boolean toggle or checkbox input.</summary>
    Bool,

    /// <summary>A fixed-value selection from a predefined set (see <see cref="ConfigFieldDescriptor.EnumValues"/>).</summary>
    Enum,

    /// <summary>A masked text input for sensitive values such as API keys or passwords.</summary>
    Secret,

    /// <summary>A multi-line text area input for long-form content.</summary>
    MultilineText,

    /// <summary>A searchable or scrollable dropdown list (see <see cref="ConfigFieldDescriptor.EnumValues"/>).</summary>
    Dropdown,

    /// <summary>A floating-point or decimal numeric input.</summary>
    Number
}
