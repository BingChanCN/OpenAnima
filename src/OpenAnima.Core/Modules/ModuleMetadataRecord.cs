namespace OpenAnima.Core.Modules;

/// <summary>
/// Temporary compatibility shim while module consumers move to OpenAnima.Contracts.ModuleMetadataRecord.
/// </summary>
public record ModuleMetadataRecord(string Name, string Version, string Description)
    : OpenAnima.Contracts.ModuleMetadataRecord(Name, Version, Description);
