namespace OpenAnima.Contracts;

/// <summary>
/// Simple record implementation of <see cref="IModuleMetadata"/> for concrete modules.
/// </summary>
public record ModuleMetadataRecord(string Name, string Version, string Description) : IModuleMetadata;
