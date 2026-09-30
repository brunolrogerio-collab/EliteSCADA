using Scada.Api.Licensing;
using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;

namespace Scada.Api.Runtime;

/// <summary>
/// Final host-side Runtime decorator. HA policy remains owned by RuntimeHighAvailabilityService;
/// this coordinator only consumes the effective-Active decision before industrial effects.
/// </summary>
public sealed class HighAvailabilityRuntimeCoordinator(
    ProductLicensedRuntimeCoordinator inner,
    RuntimeHighAvailabilityService highAvailability,
    IProductLicenseService licensing,
    IScadaEventBus? eventBus = null,
    IConfiguration? configuration = null) :
    IEngineeringRuntimeCoordinator,
    IGatewayRuntimeDiagnosticsProvider
{
    public const string AuthorityDeniedIssueCode = "HA_EFFECTIVE_ACTIVE_REQUIRED";

    public RuntimeDescriptor Describe() => inner.Describe();
    public IReadOnlyCollection<TagDefinition> Tags() => inner.Tags();
    public IReadOnlyCollection<TagValue> CurrentValues() => inner.CurrentValues();
    public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => inner.AlarmDefinitions();
    public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) => inner.Alarms(activeOnly);
    public IReadOnlyCollection<CommandDefinition> Commands() => inner.Commands();
    public IReadOnlyCollection<ClientMemoryRuntimeSource> ClientMemorySources() => inner.ClientMemorySources();
    public bool TryGetTag(Guid tagId, out TagDefinition? tag) => inner.TryGetTag(tagId, out tag);
    public bool TryGetTagByPath(string path, out TagDefinition? tag) => inner.TryGetTagByPath(path, out tag);
    public bool TryGetCurrent(Guid tagId, out TagValue? value) => inner.TryGetCurrent(tagId, out value);
    public bool TryGetCommand(Guid commandId, out CommandDefinition? command) => inner.TryGetCommand(commandId, out command);
    public bool IsServerMemoryTag(Guid tagId) => inner.IsServerMemoryTag(tagId);

    public EngineeringPackage? CaptureApplication() => inner.CaptureApplication();

    public async Task<RuntimeActivationResult> MaterializePassiveAsync(
        string sourceNodeId,
        string projectKey,
        long revision,
        EngineeringPackage package,
        DateTimeOffset? authoritativeActivatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceNodeId);
        ArgumentNullException.ThrowIfNull(package);

        if (!highAvailability.Enabled ||
            highAvailability.LocalNodeId is null ||
            highAvailability.ClusterId is null)
        {
            return PassiveDenied(projectKey, revision, "ha-disabled");
        }

        var topology = highAvailability.Snapshot();
        if (topology.AmbiguousAuthority)
            return PassiveDenied(projectKey, revision, "ambiguous-authority");
        if (topology.PendingTransfer is not null)
            return PassiveDenied(projectKey, revision, "transfer-in-progress");
        if (topology.EffectiveActiveNodeId is null ||
            !topology.EffectiveActiveNodeId.Equals(sourceNodeId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return PassiveDenied(projectKey, revision, "peer-not-effective-active");
        }
        if (sourceNodeId.Equals(highAvailability.LocalNodeId, StringComparison.OrdinalIgnoreCase))
            return PassiveDenied(projectKey, revision, "passive-source-is-local-node");

        var verification = licensing.CurrentVerification;
        var haEntitled =
            verification.State == LicenseState.Valid &&
            verification.SessionEntitlements?.HaRuntime == true;
        if (!haEntitled)
            return PassiveDenied(projectKey, revision, "local-license-not-ha-entitled");

        // Server Scripts are revision-bound process actors. A Standby materialization
        // must cancel any previous generation before swapping the passive projection.
        if (eventBus is not null && configuration is not null)
        {
            await ServerScriptRuntimeManager.GetShared(this, eventBus, configuration)
                .DeactivateForInstallationDetachAsync(cancellationToken);
        }

        var materialized = await inner.MaterializePassiveAsync(
            projectKey,
            revision,
            package,
            authoritativeActivatedAtUtc,
            cancellationToken);
        if (!materialized.Activated)
            return materialized;

        var descriptor = inner.Describe();
        var identityMatches =
            descriptor.Revision == revision &&
            string.Equals(descriptor.ProjectKey, projectKey.Trim(), StringComparison.OrdinalIgnoreCase) &&
            descriptor.ActivatedAtUtc?.ToUniversalTime() ==
                authoritativeActivatedAtUtc?.ToUniversalTime();
        return identityMatches
            ? materialized
            : PassiveDenied(projectKey, revision, "passive-runtime-identity-mismatch");
    }

    public IReadOnlyCollection<GatewayRouteRuntimeDiagnostic> GatewayDiagnostics() =>
        inner.GatewayDiagnostics();

    public ValueTask<bool> AcknowledgeAlarmAsync(
        Guid alarmId,
        string user,
        CancellationToken cancellationToken = default)
    {
        RequireIndustrialAuthority();
        return inner.AcknowledgeAlarmAsync(alarmId, user, cancellationToken);
    }

    public ValueTask<bool> ShelveAlarmAsync(
        Guid alarmId,
        string user,
        CancellationToken cancellationToken = default)
    {
        RequireIndustrialAuthority();
        return inner.ShelveAlarmAsync(alarmId, user, cancellationToken);
    }

    public ValueTask<bool> UnshelveAlarmAsync(
        Guid alarmId,
        string user,
        CancellationToken cancellationToken = default)
    {
        RequireIndustrialAuthority();
        return inner.UnshelveAlarmAsync(alarmId, user, cancellationToken);
    }

    public ValueTask WriteAsync(
        Guid tagId,
        object? value,
        CancellationToken cancellationToken = default)
    {
        RequireIndustrialAuthority();
        return inner.WriteAsync(tagId, value, cancellationToken);
    }

    public ValueTask ResetServerMemoryRetainedValueAsync(
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        RequireIndustrialAuthority();
        return inner.ResetServerMemoryRetainedValueAsync(tagId, cancellationToken);
    }

    public ValueTask ExecuteCommandAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        RequireIndustrialAuthority();
        return inner.ExecuteCommandAsync(commandId, cancellationToken);
    }

    public Task<RuntimeActivationResult> ActivateAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        CancellationToken cancellationToken = default) =>
        ActivateCoreAsync(
            projectKey,
            revision,
            package,
            commitAsync: null,
            cancellationToken);

    public Task<RuntimeActivationResult> ActivateAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commitAsync);
        return ActivateCoreAsync(projectKey, revision, package, commitAsync, cancellationToken);
    }

    private async Task<RuntimeActivationResult> ActivateCoreAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        Func<RuntimeActivationCommitContext, CancellationToken, Task>? commitAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);

        var authority = highAvailability.PrepareLocalActivation(
            projectKey,
            revision,
            licensing.CurrentVerification);
        if (!authority.Allowed)
        {
            return new RuntimeActivationResult(
                projectKey.Trim(),
                revision,
                false,
                Array.Empty<EngineeringDriverIssue>(),
                new[]
                {
                    new RuntimeActivationIssue(
                        AuthorityDeniedIssueCode,
                        $"Runtime activation is fenced by HA authority ({authority.ReasonCode}).",
                        IsError: true)
                });
        }

        return commitAsync is null
            ? await inner.ActivateAsync(projectKey, revision, package, cancellationToken)
            : await inner.ActivateAsync(projectKey, revision, package, commitAsync, cancellationToken);
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private static RuntimeActivationResult PassiveDenied(
        string projectKey,
        long revision,
        string reasonCode) =>
        new(
            projectKey.Trim(),
            revision,
            false,
            Array.Empty<EngineeringDriverIssue>(),
            new[]
            {
                new RuntimeActivationIssue(
                    AuthorityDeniedIssueCode,
                    $"Passive Runtime materialization is fenced by HA authority ({reasonCode}).",
                    IsError: true)
            });

    private void RequireIndustrialAuthority()
    {
        if (!highAvailability.CanOwnIndustrialEffects())
        {
            throw new InvalidOperationException(
                "Industrial Runtime effects are fenced because this node is not the effective HA Active authority.");
        }
    }
}
