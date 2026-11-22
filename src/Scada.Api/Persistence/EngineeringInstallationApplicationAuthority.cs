using Microsoft.Extensions.Configuration;
using Scada.Engineering.Persistence;

namespace Scada.Api.Persistence;

/// <summary>
/// Resolves the installation-level Application identity used by persisted Runtime
/// activation and projection. A durable FND-07 binding supersedes static bootstrap
/// configuration once the installation has left Legacy state.
/// </summary>
public sealed record EngineeringInstallationApplicationAuthority(
    string? ProjectKey,
    EngineeringInstallationBindingState? BindingState,
    bool UsesLegacyConfiguration)
{
    public bool Matches(string? projectKey) =>
        !string.IsNullOrWhiteSpace(ProjectKey) &&
        !string.IsNullOrWhiteSpace(projectKey) &&
        ProjectKey.Equals(projectKey.Trim(), StringComparison.OrdinalIgnoreCase);
}

public static class EngineeringInstallationApplicationAuthorityResolver
{
    public static async Task<EngineeringInstallationApplicationAuthority> ResolveAsync(
        IEngineeringInstallationBindingStore? bindingStore,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredProjectKey = Normalize(configuration["EngineeringRuntime:ProjectKey"]);
        if (bindingStore is null)
        {
            return new EngineeringInstallationApplicationAuthority(
                configuredProjectKey,
                BindingState: null,
                UsesLegacyConfiguration: true);
        }

        var binding = await bindingStore.GetAsync(cancellationToken);
        return binding.State switch
        {
            EngineeringInstallationBindingState.Legacy =>
                new EngineeringInstallationApplicationAuthority(
                    configuredProjectKey,
                    binding.State,
                    UsesLegacyConfiguration: true),

            EngineeringInstallationBindingState.Attached =>
                new EngineeringInstallationApplicationAuthority(
                    Normalize(binding.ProjectKey),
                    binding.State,
                    UsesLegacyConfiguration: false),

            EngineeringInstallationBindingState.Neutral or
            EngineeringInstallationBindingState.AttachInProgress or
            EngineeringInstallationBindingState.DetachInProgress =>
                new EngineeringInstallationApplicationAuthority(
                    ProjectKey: null,
                    binding.State,
                    UsesLegacyConfiguration: false),

            _ => throw new InvalidOperationException(
                $"Unsupported installation Application binding state '{binding.State}'.")
        };
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
