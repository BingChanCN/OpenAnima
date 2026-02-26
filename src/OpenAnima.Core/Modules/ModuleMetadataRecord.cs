using OpenAnima.Contracts;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Simple record implementation of IModuleMetadata for concrete modules.
/// </summary>
public record ModuleMetadataRecord(string Name, string Version, string Description) : IModuleMetadata;
