using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Modbus;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Runtime;

public sealed record RuntimeActivationIssue(
    string Code,
    string Message,
    string? EntityKey = null,
    bool IsError = true);

public sealed record RuntimeActivationResult(
    string ProjectKey,
    long Revision,
    bool Activated,
    IReadOnlyCollection<EngineeringDriverIssue> CompilationIssues,
    IReadOnlyCollection<RuntimeActivationIssue> RuntimeIssues,
    DateTimeOffset? ActivatedAtUtc = null);

public sealed record RuntimeActivationCommitContext(
    string ProjectKey,
    long Revision,
    DateTimeOffset ActivatedAtUtc);

public sealed record RuntimeDescriptor(
    string? ProjectKey,
    long? Revision,
    DateTimeOffset? ActivatedAtUtc,
    IReadOnlyCollection<DriverStatus> Drivers,
    int TagCount,
    int ActiveAlarmCount);

public interface IEngineeringRuntimeCoordinator : IAsyncDisposable
{
    RuntimeDescriptor Describe();
    IReadOnlyCollection<TagDefinition> Tags();
    IReadOnlyCollection<TagValue> CurrentValues();
    IReadOnlyCollection<AlarmDefinition> AlarmDefinitions();
    IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false);
    IReadOnlyCollection<CommandDefinition> Commands();
    bool TryGetTag(Guid tagId, out TagDefinition? tag);
    bool TryGetTagByPath(string path, out TagDefinition? tag);
    bool TryGetCurrent(Guid tagId, out TagValue? value);
    bool TryGetCommand(Guid commandId, out CommandDefinition? command);
    ValueTask<bool> AcknowledgeAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default);
    ValueTask<bool> ShelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default);
    ValueTask<bool> UnshelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default);
    ValueTask ExecuteCommandAsync(Guid commandId, CancellationToken cancellationToken = default);
    Task<RuntimeActivationResult> ActivateAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        CancellationToken cancellationToken = default);
    Task<RuntimeActivationResult> ActivateAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync,
        CancellationToken cancellationToken = default);
}

public sealed class EngineeringRuntimeCoordinator : IEngineeringRuntimeCoordinator
{
    private readonly IScadaEventBus _externalEventBus;
    private readonly IEngineeringDriverCompiler _compiler;
    private readonly TimeSpan _activationTimeout;
    private readonly SemaphoreSlim _activationGate = new(1, 1);
    private RuntimeState _active;

    public EngineeringRuntimeCoordinator(
        IScadaEventBus externalEventBus,
        IEngineeringDriverCompiler compiler,
        TimeSpan? activationTimeout = null)
    {
        _externalEventBus = externalEventBus ?? throw new ArgumentNullException(nameof(externalEventBus));
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _activationTimeout = activationTimeout ?? TimeSpan.FromSeconds(10);
        if (_activationTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(activationTimeout));

        _active = RuntimeState.Empty(_externalEventBus);
    }

    public RuntimeDescriptor Describe()
    {
        var state = Volatile.Read(ref _active);
        return new RuntimeDescriptor(
            state.ProjectKey,
            state.Revision,
            state.ActivatedAtUtc,
            state.Drivers.Select(x => x.Status).ToArray(),
            state.Registry.Snapshot().Count,
            state.Alarms.Snapshot(activeOnly: true).Count);
    }

    public IReadOnlyCollection<TagDefinition> Tags() => Volatile.Read(ref _active).Registry.Snapshot();

    public IReadOnlyCollection<TagValue> CurrentValues() => Volatile.Read(ref _active).Cache.Snapshot();

    public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => Volatile.Read(ref _active).Alarms.Definitions();

    public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) =>
        Volatile.Read(ref _active).Alarms.Snapshot(activeOnly);

    public IReadOnlyCollection<CommandDefinition> Commands() => Volatile.Read(ref _active).Commands.Snapshot();

    public bool TryGetTag(Guid tagId, out TagDefinition? tag) =>
        Volatile.Read(ref _active).Registry.TryGet(tagId, out tag);

    public bool TryGetTagByPath(string path, out TagDefinition? tag) =>
        Volatile.Read(ref _active).Registry.TryGetByPath(path, out tag);

    public bool TryGetCurrent(Guid tagId, out TagValue? value) =>
        Volatile.Read(ref _active).Cache.TryGet(tagId, out value);

    public bool TryGetCommand(Guid commandId, out CommandDefinition? command) =>
        Volatile.Read(ref _active).Commands.TryGet(commandId, out command);

    public ValueTask<bool> AcknowledgeAlarmAsync(
        Guid alarmId,
        string user,
        CancellationToken cancellationToken = default) =>
        Volatile.Read(ref _active).Alarms.AcknowledgeAsync(alarmId, user, cancellationToken);

    public ValueTask<bool> ShelveAlarmAsync(
        Guid alarmId,
        string user,
        CancellationToken cancellationToken = default) =>
        Volatile.Read(ref _active).Alarms.ShelveAsync(alarmId, user, cancellationToken);

    public ValueTask<bool> UnshelveAlarmAsync(
        Guid alarmId,
        string user,
        CancellationToken cancellationToken = default) =>
        Volatile.Read(ref _active).Alarms.UnshelveAsync(alarmId, user, cancellationToken);

    public async ValueTask WriteAsync(
        Guid tagId,
        object? value,
        CancellationToken cancellationToken = default)
    {
        var state = Volatile.Read(ref _active);
        if (!state.DriverByTagId.TryGetValue(tagId, out var driver))
            throw new KeyNotFoundException($"Active runtime has no communication driver for TAG '{tagId}'.");

        await driver.WriteAsync(tagId, value, cancellationToken);
    }

    public async ValueTask ExecuteCommandAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        var state = Volatile.Read(ref _active);
        if (!state.Commands.TryGet(commandId, out var command) || command is null)
            throw new KeyNotFoundException($"Active runtime command '{commandId}' was not found.");
        if (!state.DriverByTagId.TryGetValue(command.TargetTagId, out var driver))
            throw new KeyNotFoundException(
                $"Active runtime command '{command.Key}' has no communication driver for target TAG '{command.TargetTagPath}'.");

        await driver.WriteAsync(command.TargetTagId, command.Value, cancellationToken);
    }

    public Task<RuntimeActivationResult> ActivateAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        CancellationToken cancellationToken = default) =>
        ActivateCoreAsync(projectKey, revision, package, null, cancellationToken);

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
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));
        if (revision < 1)
            throw new ArgumentOutOfRangeException(nameof(revision));
        ArgumentNullException.ThrowIfNull(package);

        await _activationGate.WaitAsync(cancellationToken);
        try
        {
            var compilation = _compiler.Compile(package);
            if (!compilation.CanActivate)
            {
                return new RuntimeActivationResult(
                    projectKey.Trim(),
                    revision,
                    false,
                    compilation.Issues,
                    Array.Empty<RuntimeActivationIssue>());
            }

            var runtimeIssues = new List<RuntimeActivationIssue>();
            RuntimeState? candidate = null;
            try
            {
                candidate = BuildCandidate(projectKey.Trim(), revision, compilation, runtimeIssues);
                if (runtimeIssues.Any(x => x.IsError))
                {
                    await candidate.DisposeAsync();
                    return new RuntimeActivationResult(
                        projectKey.Trim(), revision, false, compilation.Issues, runtimeIssues);
                }

                await StartDriversAsync(candidate, cancellationToken);
                var ready = await WaitUntilReadyAsync(candidate, cancellationToken);
                if (!ready)
                {
                    runtimeIssues.Add(new(
                        "RUNTIME_CANDIDATE_NOT_READY",
                        $"Candidate runtime did not reach Good quality for all communication TAGs within {_activationTimeout}.",
                        IsError: true));
                    await candidate.DisposeAsync();
                    return new RuntimeActivationResult(
                        projectKey.Trim(), revision, false, compilation.Issues, runtimeIssues);
                }

                RegisterAlarms(package, candidate, runtimeIssues);
                RegisterCommands(package, candidate, runtimeIssues);
                if (runtimeIssues.Any(x => x.IsError))
                {
                    await candidate.DisposeAsync();
                    return new RuntimeActivationResult(
                        projectKey.Trim(), revision, false, compilation.Issues, runtimeIssues);
                }

                await EvaluateCurrentAlarmsAsync(candidate, cancellationToken);

                var activatedAt = DateTimeOffset.UtcNow;
                candidate.ActivatedAtUtc = activatedAt;

                if (commitAsync is not null)
                {
                    try
                    {
                        await commitAsync(
                            new RuntimeActivationCommitContext(projectKey.Trim(), revision, activatedAt),
                            cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                    {
                        runtimeIssues.Add(new(
                            "RUNTIME_ACTIVATION_COMMIT_FAILED",
                            $"Candidate runtime was ready, but activation could not be committed: {ex.Message}"));
                        await candidate.DisposeAsync();
                        return new RuntimeActivationResult(
                            projectKey.Trim(), revision, false, compilation.Issues, runtimeIssues);
                    }
                }

                var previous = Volatile.Read(ref _active);
                previous.EventGate.DisableForwarding();
                Volatile.Write(ref _active, candidate);
                candidate.EventGate.EnableForwarding();
                candidate = null;

                try
                {
                    await previous.DisposeAsync();
                }
                catch (Exception ex)
                {
                    runtimeIssues.Add(new(
                        "PREVIOUS_RUNTIME_STOP_FAILED",
                        $"New runtime is active, but the previous runtime reported an error while stopping: {ex.Message}",
                        IsError: false));
                }

                return new RuntimeActivationResult(
                    projectKey.Trim(),
                    revision,
                    true,
                    compilation.Issues,
                    runtimeIssues,
                    activatedAt);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (candidate is not null) await candidate.DisposeAsync();
                throw;
            }
            catch (Exception ex)
            {
                if (candidate is not null) await candidate.DisposeAsync();
                runtimeIssues.Add(new("RUNTIME_ACTIVATION_FAILED", ex.Message));
                return new RuntimeActivationResult(
                    projectKey.Trim(), revision, false, compilation.Issues, runtimeIssues);
            }
        }
        finally
        {
            _activationGate.Release();
        }
    }

    private RuntimeState BuildCandidate(
        string projectKey,
        long revision,
        EngineeringDriverCompilation compilation,
        List<RuntimeActivationIssue> runtimeIssues)
    {
        var eventGate = new RuntimeEventGate(_externalEventBus, forwardingEnabled: false);
        var registry = new InMemoryTagRegistry();
        var cache = new CurrentTagCache(eventGate);
        var alarms = new InMemoryAlarmEngine(eventGate);
        var commands = new InMemoryCommandRegistry();
        var drivers = new List<ICommunicationDriver>();

        foreach (var plan in compilation.ModbusTcpPlans)
        {
            if (plan.Points.Count == 0)
            {
                runtimeIssues.Add(new(
                    "RUNTIME_DATASOURCE_NO_POINTS",
                    $"Data source '{plan.DataSourceKey}' has no Modbus points and will not create a runtime driver.",
                    plan.DataSourceKey,
                    IsError: false));
                continue;
            }

            drivers.Add(new ModbusTcpDriver(
                $"modbus.tcp:{plan.DataSourceKey}",
                plan.Name,
                plan.Host,
                cache,
                registry,
                plan.Points,
                plan.Port,
                plan.ScanRate,
                plan.RequestTimeout,
                plan.MaxGapElements));
        }

        if (drivers.Count == 0)
        {
            runtimeIssues.Add(new(
                "RUNTIME_NO_COMMUNICATION_DRIVERS",
                "Published engineering produced no supported communication drivers.",
                IsError: true));
        }

        return new RuntimeState(projectKey, revision, eventGate, registry, cache, alarms, commands, drivers);
    }

    private static async Task StartDriversAsync(RuntimeState state, CancellationToken cancellationToken)
    {
        foreach (var driver in state.Drivers)
            await driver.StartAsync(cancellationToken);
    }

    private async Task<bool> WaitUntilReadyAsync(RuntimeState state, CancellationToken cancellationToken)
    {
        var expectedTagIds = state.Drivers
            .SelectMany(x => x.Tags)
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        if (expectedTagIds.Length == 0) return false;

        var deadline = DateTimeOffset.UtcNow + _activationTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (state.Drivers.Any(x => x.Status.State == DriverState.Faulted))
                return false;

            var allGood = expectedTagIds.All(id =>
                state.Cache.TryGet(id, out var value) && value?.Quality == TagQuality.Good);
            if (allGood) return true;

            await Task.Delay(25, cancellationToken);
        }

        return expectedTagIds.All(id =>
            state.Cache.TryGet(id, out var value) && value?.Quality == TagQuality.Good);
    }

    private static void RegisterAlarms(
        EngineeringPackage package,
        RuntimeState state,
        List<RuntimeActivationIssue> issues)
    {
        foreach (var dto in package.Alarms.Where(x => x.Enabled))
        {
            TagDefinition? tag = null;
            if (dto.TagId.HasValue)
                state.Registry.TryGet(dto.TagId.Value, out tag);
            if (tag is null && !string.IsNullOrWhiteSpace(dto.TagPath))
                state.Registry.TryGetByPath(dto.TagPath, out tag);

            if (tag is null)
            {
                issues.Add(new(
                    "RUNTIME_ALARM_TAG_NOT_ACTIVE",
                    $"Alarm '{dto.Name}' references a TAG that is not present in the candidate runtime.",
                    dto.Name));
                continue;
            }

            state.Alarms.Register(new AlarmDefinition(
                dto.Id ?? Guid.NewGuid(),
                dto.Name,
                tag.Id,
                dto.Type,
                dto.Priority,
                dto.Setpoint,
                dto.DigitalActiveValue,
                dto.Area,
                dto.Message,
                dto.Enabled,
                dto.AlarmClass,
                dto.ActivationDelayMilliseconds.HasValue
                    ? TimeSpan.FromMilliseconds(dto.ActivationDelayMilliseconds.Value)
                    : null,
                dto.RequiresAcknowledgement,
                dto.ShelvingAllowed,
                dto.Metadata));
        }
    }

    private static void RegisterCommands(
        EngineeringPackage package,
        RuntimeState state,
        List<RuntimeActivationIssue> issues)
    {
        foreach (var dto in (package.Commands ?? Array.Empty<CommandEngineeringDto>()).Where(x => x.Enabled))
        {
            TagDefinition? byId = null;
            TagDefinition? byPath = null;
            if (dto.TargetTagId.HasValue)
                state.Registry.TryGet(dto.TargetTagId.Value, out byId);
            if (!string.IsNullOrWhiteSpace(dto.TargetTagPath))
                state.Registry.TryGetByPath(dto.TargetTagPath, out byPath);

            if (byId is not null && byPath is not null && byId.Id != byPath.Id)
            {
                issues.Add(new(
                    "RUNTIME_COMMAND_TARGET_MISMATCH",
                    $"Command '{dto.Key}' TargetTagId and TargetTagPath resolve to different active TAGs.",
                    dto.Key));
                continue;
            }

            var tag = byId ?? byPath;
            if (tag is null)
            {
                issues.Add(new(
                    "RUNTIME_COMMAND_TAG_NOT_ACTIVE",
                    $"Command '{dto.Key}' references a TAG that is not present in the candidate runtime.",
                    dto.Key));
                continue;
            }

            if (tag.ReadOnly)
            {
                issues.Add(new(
                    "RUNTIME_COMMAND_TAG_READ_ONLY",
                    $"Command '{dto.Key}' targets read-only TAG '{tag.Path}'.",
                    dto.Key));
                continue;
            }

            if (dto.Kind != CommandKind.WriteTagValue)
            {
                issues.Add(new(
                    "RUNTIME_COMMAND_KIND_UNSUPPORTED",
                    $"Command '{dto.Key}' uses unsupported kind '{dto.Kind}'.",
                    dto.Key));
                continue;
            }

            if (!CommandValueParser.TryParse(tag.DataType, dto.Value, out var value))
            {
                issues.Add(new(
                    "RUNTIME_COMMAND_VALUE_INVALID",
                    $"Command '{dto.Key}' value cannot be converted to target TAG data type '{tag.DataType}'.",
                    dto.Key));
                continue;
            }

            try
            {
                state.Commands.Register(new CommandDefinition(
                    dto.Id ?? Guid.NewGuid(),
                    dto.Key,
                    dto.Name,
                    dto.Kind,
                    tag.Id,
                    tag.Path,
                    value,
                    dto.Description,
                    dto.Area,
                    dto.EquipmentPath,
                    dto.Metadata));
            }
            catch (Exception ex)
            {
                issues.Add(new(
                    "RUNTIME_COMMAND_REGISTRATION_FAILED",
                    $"Command '{dto.Key}' could not be registered: {ex.Message}",
                    dto.Key));
            }
        }
    }

    private static async Task EvaluateCurrentAlarmsAsync(
        RuntimeState state,
        CancellationToken cancellationToken)
    {
        foreach (var current in state.Cache.Snapshot())
        {
            if (!state.Registry.TryGet(current.TagId, out var tag) || tag is null)
                continue;

            await state.EventGate.PublishAsync(
                new TagValueChanged(tag, null, current, DateTimeOffset.UtcNow),
                cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _activationGate.WaitAsync();
        try
        {
            var active = Volatile.Read(ref _active);
            active.EventGate.DisableForwarding();
            await active.DisposeAsync();
            Volatile.Write(ref _active, RuntimeState.Empty(_externalEventBus));
        }
        finally
        {
            _activationGate.Release();
            _activationGate.Dispose();
        }
    }

    private sealed class RuntimeState : IAsyncDisposable
    {
        public RuntimeState(
            string? projectKey,
            long? revision,
            RuntimeEventGate eventGate,
            InMemoryTagRegistry registry,
            CurrentTagCache cache,
            InMemoryAlarmEngine alarms,
            InMemoryCommandRegistry commands,
            IReadOnlyCollection<ICommunicationDriver> drivers)
        {
            ProjectKey = projectKey;
            Revision = revision;
            EventGate = eventGate;
            Registry = registry;
            Cache = cache;
            Alarms = alarms;
            Commands = commands;
            Drivers = drivers;
            DriverByTagId = drivers
                .SelectMany(driver => driver.Tags.Select(tag => (tag.Id, Driver: driver)))
                .ToDictionary(x => x.Id, x => x.Driver);
        }

        public string? ProjectKey { get; }
        public long? Revision { get; }
        public DateTimeOffset? ActivatedAtUtc { get; set; }
        public RuntimeEventGate EventGate { get; }
        public InMemoryTagRegistry Registry { get; }
        public CurrentTagCache Cache { get; }
        public InMemoryAlarmEngine Alarms { get; }
        public InMemoryCommandRegistry Commands { get; }
        public IReadOnlyCollection<ICommunicationDriver> Drivers { get; }
        public IReadOnlyDictionary<Guid, ICommunicationDriver> DriverByTagId { get; }

        public static RuntimeState Empty(IScadaEventBus externalEventBus)
        {
            var eventGate = new RuntimeEventGate(externalEventBus, forwardingEnabled: true);
            return new RuntimeState(
                null,
                null,
                eventGate,
                new InMemoryTagRegistry(),
                new CurrentTagCache(eventGate),
                new InMemoryAlarmEngine(eventGate),
                new InMemoryCommandRegistry(),
                Array.Empty<ICommunicationDriver>());
        }

        public async ValueTask DisposeAsync()
        {
            List<Exception>? errors = null;
            foreach (var driver in Drivers.Reverse())
            {
                try
                {
                    await driver.StopAsync();
                    await driver.DisposeAsync();
                }
                catch (Exception ex)
                {
                    errors ??= new List<Exception>();
                    errors.Add(ex);
                }
            }

            Alarms.Dispose();
            if (errors is { Count: > 0 })
                throw new AggregateException(errors);
        }
    }
}
