using Scada.Api.Licensing;
using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;

namespace Scada.Api.Runtime;

public sealed record ScadaRuntimeDescriptor(
    string Mode,
    string? ProjectKey,
    long? Revision,
    DateTimeOffset? ActivatedAtUtc,
    IReadOnlyCollection<DriverStatus> Drivers,
    IReadOnlyCollection<CommunicationDriverDiagnosticSnapshot> CommunicationDrivers,
    int TagCount,
    int ActiveAlarmCount,
    ServerScriptRuntimeSnapshot? ServerScripts = null);

public sealed class ScadaRuntimeFacade(
    IEngineeringRuntimeCoordinator engineeringRuntime,
    GatewayEngineeringRuntimeCoordinator? operationalEvents = null,
    IScadaEventBus? eventBus = null,
    IConfiguration? configuration = null,
    IInstallationRuntimeFence? installationFence = null,
    RuntimeHighAvailabilityService? highAvailability = null)
{
    private IOperationalEventRuntime? EventRuntime =>
        operationalEvents ?? engineeringRuntime as IOperationalEventRuntime;

    public bool IsInstallationNeutral => installationFence?.ProcessEffectsFenced == true;
    public bool IsEngineeringActive
    {
        get
        {
            var active = engineeringRuntime.Describe();
            return !IsInstallationNeutral &&
                   active.Revision.HasValue &&
                   !string.IsNullOrWhiteSpace(active.ProjectKey);
        }
    }

    public ScadaRuntimeDescriptor Describe()
    {
        if (IsInstallationNeutral)
        {
            return NeutralDescriptor();
        }

        var engineering = engineeringRuntime.Describe();
        if (!engineering.Revision.HasValue || string.IsNullOrWhiteSpace(engineering.ProjectKey))
            return NeutralDescriptor();

        var serverScripts = eventBus is not null && configuration is not null
            ? ServerScriptRuntimeManager.GetShared(
                engineeringRuntime,
                eventBus,
                configuration).Snapshot()
            : null;

        return new ScadaRuntimeDescriptor(
            "engineering",
            engineering.ProjectKey,
            engineering.Revision,
            engineering.ActivatedAtUtc,
            engineering.Drivers,
            engineering.CommunicationDrivers,
            engineering.TagCount,
            engineering.ActiveAlarmCount,
            serverScripts);
    }

    public IReadOnlyCollection<TagDefinition> Tags() =>
        IsInstallationNeutral || !IsEngineeringActive
            ? Array.Empty<TagDefinition>()
            : engineeringRuntime.Tags();

    public IReadOnlyCollection<TagValue> CurrentValues() =>
        IsInstallationNeutral || !IsEngineeringActive
            ? Array.Empty<TagValue>()
            : engineeringRuntime.CurrentValues();

    public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() =>
        IsInstallationNeutral || !IsEngineeringActive
            ? Array.Empty<AlarmDefinition>()
            : engineeringRuntime.AlarmDefinitions();

    public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) =>
        IsInstallationNeutral || !IsEngineeringActive
            ? Array.Empty<AlarmInstance>()
            : engineeringRuntime.Alarms(activeOnly);

    public IReadOnlyCollection<CommandDefinition> Commands() =>
        IsInstallationNeutral || !IsEngineeringActive
            ? Array.Empty<CommandDefinition>()
            : engineeringRuntime.Commands();

    public IReadOnlyCollection<OperationalEventDefinition> OperationalEventDefinitions() =>
        !IsInstallationNeutral && IsEngineeringActive && EventRuntime is { } events
            ? events.OperationalEventDefinitions()
            : Array.Empty<OperationalEventDefinition>();

    public IReadOnlyCollection<ClientMemoryRuntimeSource> ClientMemorySources() =>
        !IsInstallationNeutral && IsEngineeringActive
            ? engineeringRuntime.ClientMemorySources()
            : Array.Empty<ClientMemoryRuntimeSource>();

    public IReadOnlyCollection<DriverStatus> Drivers() =>
        IsInstallationNeutral || !IsEngineeringActive
            ? Array.Empty<DriverStatus>()
            : engineeringRuntime.Describe().Drivers;

    public bool TryGetTag(Guid tagId, out TagDefinition? tag)
    {
        if (IsInstallationNeutral || !IsEngineeringActive)
        {
            tag = null;
            return false;
        }
        if (IsEngineeringActive)
            return engineeringRuntime.TryGetTag(tagId, out tag);

        tag = null;
        return false;
    }

    public bool TryGetTagByPath(string path, out TagDefinition? tag)
    {
        if (IsInstallationNeutral || !IsEngineeringActive)
        {
            tag = null;
            return false;
        }
        if (IsEngineeringActive)
            return engineeringRuntime.TryGetTagByPath(path, out tag);

        tag = null;
        return false;
    }

    public bool TryGetCurrent(Guid tagId, out TagValue? value)
    {
        if (IsInstallationNeutral || !IsEngineeringActive)
        {
            value = null;
            return false;
        }
        if (IsEngineeringActive)
            return engineeringRuntime.TryGetCurrent(tagId, out value);

        value = null;
        return false;
    }

    public bool TryGetCommand(Guid commandId, out CommandDefinition? command)
    {
        if (IsInstallationNeutral || !IsEngineeringActive)
        {
            command = null;
            return false;
        }
        if (IsEngineeringActive)
            return engineeringRuntime.TryGetCommand(commandId, out command);

        command = null;
        return false;
    }

    public bool TryGetOperationalEvent(Guid definitionId, out OperationalEventDefinition? definition)
    {
        if (IsInstallationNeutral)
        {
            definition = null;
            return false;
        }
        if (IsEngineeringActive && EventRuntime is { } events)
            return events.TryGetOperationalEvent(definitionId, out definition);

        definition = null;
        return false;
    }

    public bool IsServerMemoryTag(Guid tagId) =>
        !IsInstallationNeutral && IsEngineeringActive && engineeringRuntime.IsServerMemoryTag(tagId);

    public ValueTask<bool> AcknowledgeAlarmAsync(
        Guid alarmId,
        string user,
        CancellationToken cancellationToken = default)
    {
        ThrowIfProcessEffectsFenced();
        RequireActiveEngineeringRuntime();
        RequireIndustrialAuthority();
        return engineeringRuntime.AcknowledgeAlarmAsync(alarmId, user, cancellationToken);
    }

    public ValueTask<bool> ShelveAlarmAsync(
        Guid alarmId,
        string user,
        CancellationToken cancellationToken = default)
    {
        ThrowIfProcessEffectsFenced();
        RequireActiveEngineeringRuntime();
        RequireIndustrialAuthority();
        return engineeringRuntime.ShelveAlarmAsync(alarmId, user, cancellationToken);
    }

    public ValueTask<bool> UnshelveAlarmAsync(
        Guid alarmId,
        string user,
        CancellationToken cancellationToken = default)
    {
        ThrowIfProcessEffectsFenced();
        RequireActiveEngineeringRuntime();
        RequireIndustrialAuthority();
        return engineeringRuntime.UnshelveAlarmAsync(alarmId, user, cancellationToken);
    }

    public ValueTask WriteAsync(
        Guid tagId,
        object? value,
        CancellationToken cancellationToken = default)
    {
        ThrowIfProcessEffectsFenced();
        RequireActiveEngineeringRuntime();
        RequireIndustrialAuthority();
        return engineeringRuntime.WriteAsync(tagId, value, cancellationToken);
    }

    public ValueTask ResetServerMemoryRetainedValueAsync(
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfProcessEffectsFenced();
        RequireIndustrialAuthority();
        RequireActiveEngineeringRuntime();
        return engineeringRuntime.ResetServerMemoryRetainedValueAsync(tagId, cancellationToken);
    }

    public async ValueTask ExecuteCommandAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfProcessEffectsFenced();
        RequireIndustrialAuthority();
        RequireActiveEngineeringRuntime();
        await engineeringRuntime.ExecuteCommandAsync(commandId, cancellationToken);
    }

    public ValueTask<OperationalEventOccurred> EmitOperationalEventAsync(
        Guid definitionId,
        OperationalEventEmissionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfProcessEffectsFenced();
        RequireIndustrialAuthority();
        RequireActiveEngineeringRuntime();
        if (EventRuntime is not { } events)
            throw new InvalidOperationException("The active Engineering runtime does not expose Operational Event support.");
        return events.EmitOperationalEventAsync(definitionId, context, cancellationToken);
    }

    private void ThrowIfProcessEffectsFenced()
    {
        if (IsInstallationNeutral)
            throw new InvalidOperationException(
                "Runtime process effects are fenced while the installation is in neutral bootstrap.");
    }

    private void RequireIndustrialAuthority()
    {
        if (highAvailability?.Enabled == true && !highAvailability.CanOwnIndustrialEffects())
        {
            throw new InvalidOperationException(
                "Industrial Runtime effects are fenced because this node is not the effective HA Active authority.");
        }
    }

    private void RequireActiveEngineeringRuntime()
    {
        if (!IsEngineeringActive)
            throw new InvalidOperationException("No Active Engineering Runtime is selected.");
    }

    private static ScadaRuntimeDescriptor NeutralDescriptor() => new(
        "neutral",
        null,
        null,
        null,
        Array.Empty<DriverStatus>(),
        Array.Empty<CommunicationDriverDiagnosticSnapshot>(),
        0,
        0,
        null);
}
