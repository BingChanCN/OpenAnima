using OpenAnima.Core.Providers;

namespace OpenAnima.Core.Components.Shared;

/// <summary>
/// Result DTO returned by ProviderDialog when user saves a provider (create or edit).
/// </summary>
public record ProviderDialogResult(
    string Slug,
    string DisplayName,
    string BaseUrl,
    string? ApiKey,
    List<ProviderModelRecord> Models,
    bool IsNew
);
