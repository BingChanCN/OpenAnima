namespace OpenAnima.Contracts;

/// <summary>
/// Optional interface that modules may implement to declare their configuration schema.
/// When implemented, the platform can use the schema to auto-render sidebar configuration UI.
/// Modules that do not implement this interface continue to use hand-written Razor components.
/// </summary>
/// <remarks>
/// Auto-rendering of schemas is deferred to v1.8. Implementing this interface in v1.7
/// has no visual effect but documents module configuration structure and is forward-compatible.
/// </remarks>
public interface IModuleConfigSchema
{
    /// <summary>
    /// Returns the list of configuration field descriptors for this module.
    /// Fields are displayed in the order determined by <see cref="ConfigFieldDescriptor.Order"/>.
    /// </summary>
    /// <returns>An ordered, read-only list of configuration field descriptors.</returns>
    IReadOnlyList<ConfigFieldDescriptor> GetSchema();
}
