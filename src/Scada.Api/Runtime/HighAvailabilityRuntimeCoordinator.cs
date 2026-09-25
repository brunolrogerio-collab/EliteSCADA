using Scada.Api.Licensing;
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
    IProductLicenseService licensing) :
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

    private void RequireIndustrialAuthority()
    {
        if (!highAvailability.CanOwnIndustrialEffects())
        {
            throw new InvalidOperationException(
                "Industrial Runtime effects are fenced because this node is not the effective HA Active authority.");
        }
    }
}
