namespace OpenAnima.Contracts;

/// <summary>
/// Describes a single configuration field declared by a module implementing <see cref="IModuleConfigSchema"/>.
/// Each descriptor provides all metadata needed to render, validate, and persist a configuration value.
/// </summary>
/// <param name="Key">
/// The configuration key used to store and retrieve this field's value.
/// Must match the key passed to <see cref="IModuleConfig.GetConfig"/> and <see cref="IModuleConfig.SetConfigAsync"/>.
/// </param>
/// <param name="Type">The data type and UI rendering hint for this field.</param>
/// <param name="DisplayName">The human-readable label shown in the sidebar UI.</param>
/// <param name="DefaultValue">Optional default value used when no configuration has been persisted.</param>
/// <param name="Description">Optional tooltip or helper text shown alongside the field.</param>
/// <param name="EnumValues">
/// Required values list for <see cref="ConfigFieldType.Enum"/> and <see cref="ConfigFieldType.Dropdown"/> types.
/// Ignored for other field types.
/// </param>
/// <param name="Group">Optional logical group name for visually separating fields within the sidebar.</param>
/// <param name="Order">Display order within the group. Lower values appear first. Defaults to 0.</param>
/// <param name="Required">Whether the field must have a non-empty value before the module can be used.</param>
/// <param name="ValidationPattern">Optional regex pattern used to validate the field value client-side.</param>
public record ConfigFieldDescriptor(
    string Key,
    ConfigFieldType Type,
    string DisplayName,
    string? DefaultValue,
    string? Description,
    string[]? EnumValues,
    string? Group,
    int Order,
    bool Required,
    string? ValidationPattern
);
