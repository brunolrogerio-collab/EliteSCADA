using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Scada.Core.Abstractions;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.Scripts;
using Scada.Engineering.VisualScripting;

namespace Scada.Api.Runtime;

public sealed record ServerScriptRuntimeSnapshot(
    string? ProjectKey,
    long? Revision,
    DateTimeOffset? ActivatedAtUtc,
    IReadOnlyCollection<ServerScriptInstanceSnapshot> Scripts);

public sealed record ServerScriptInstanceSnapshot(
    Guid ScriptId,
    string Path,
    string RuntimeInstanceId,
    ScriptRuntimeDiagnosticsSnapshot Diagnostics);

internal sealed record ServerScriptTagSnapshot(
    Guid TagId,
    string Path,
    TagDataType DataType,
    object? Value,
    bool IsServerMemory);

/// <summary>
/// Active-revision-only host for Server Engineering Scripts. Runtime activation and
/// script TAG access share a revision gate, so an execution from an obsolete Active
/// generation can never replay a write into a newer revision with the same stable TAG ID.
/// Operational Event emission uses that same gate so it has identical revision safety.
/// </summary>
public sealed class ServerScriptRuntimeManager : IAsyncDisposable
{
    private static readonly ConditionalWeakTable<IEngineeringRuntimeCoordinator, ServerScriptRuntimeManager> SharedHosts = new();

    private readonly IEngineeringRuntimeCoordinator _runtime;
    private readonly IScadaEventBus _eventBus;
    private readonly ScriptExecutionPolicy _policy;
    private readonly string _pythonExecutable;
    private readonly string _runnerPath;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _revisionGate = new(1, 1);
    private readonly EventHandler _processExitHandler;
    private readonly object _interactionBindingSync = new();
    private ActiveDriverInteractionRuntimeCatalog? _driverInteractions;
    private ActiveGeneration? _active;
    private bool _disposed;

    private ServerScriptRuntimeManager(
        IEngineeringRuntimeCoordinator runtime,
        IScadaEventBus eventBus,
        IConfiguration configuration)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        ArgumentNullException.ThrowIfNull(configuration);

        _policy = BuildPolicy(configuration);
        _pythonExecutable = configuration["ServerScripts:PythonExecutable"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_pythonExecutable))
            _pythonExecutable = OperatingSystem.IsWindows() ? "python" : "python3";
        _runnerPath = Path.Combine(AppContext.BaseDirectory, "ServerScriptRunner.py");

        _processExitHandler = (_, _) => DisposeFromProcessExit();
        AppDomain.CurrentDomain.ProcessExit += _processExitHandler;
    }

    public static ServerScriptRuntimeManager GetShared(
        IEngineeringRuntimeCoordinator runtime,
        IScadaEventBus eventBus,
        IConfiguration configuration) =>
        GetShared(runtime, eventBus, configuration, driverInteractions: null);

    public static ServerScriptRuntimeManager GetShared(
        IEngineeringRuntimeCoordinator runtime,
        IScadaEventBus eventBus,
        IConfiguration configuration,
        ActiveDriverInteractionRuntimeCatalog? driverInteractions)
    {
        var host = SharedHosts.GetValue(
            runtime,
            _ => new ServerScriptRuntimeManager(runtime, eventBus, configuration));

        if (!ReferenceEquals(host._eventBus, eventBus))
            throw new InvalidOperationException("Server Script runtime host is already bound to another event bus.");

        host.BindActiveDriverInteractions(driverInteractions);

        return host;
    }

    private void BindActiveDriverInteractions(ActiveDriverInteractionRuntimeCatalog? driverInteractions)
    {
        if (driverInteractions is null)
            return;

        lock (_interactionBindingSync)
        {
            if (_driverInteractions is not null && !ReferenceEquals(_driverInteractions, driverInteractions))
            {
                throw new InvalidOperationException(
                    "Server Script runtime host is already bound to another Active Driver Interaction catalog.");
            }

            Volatile.Write(ref _driverInteractions, driverInteractions);
        }
    }

    private ActiveTransientEventGraph CaptureActiveTransientEvents() =>
        Volatile.Read(ref _driverInteractions)?.CaptureActiveTransientEvents() ??
        new ActiveTransientEventGraph(
            Array.Empty<TransientEventDefinition>(),
            Array.Empty<CapabilityEventReference>());

    public ServerScriptRuntimeSnapshot Snapshot()
    {
        var active = Volatile.Read(ref _active);
        if (active is null)
            return new(null, null, null, Array.Empty<ServerScriptInstanceSnapshot>());

        return new(
            active.ProjectKey,
            active.Revision,
            active.ActivatedAtUtc,
            active.Instances.Select(instance => new ServerScriptInstanceSnapshot(
                instance.Definition.Id,
                instance.Definition.Path,
                instance.Coordinator.RuntimeInstanceId,
                instance.Coordinator.GetDiagnostics(instance.SubscriptionCount))).ToArray());
    }

    /// <summary>
    /// Canonical persisted-Active activation boundary. The Runtime transition and
    /// Server Script generation replacement are serialized against all script TAG access.
    /// </summary>
    public Task<RuntimeActivationResult> ActivateRuntimeAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        return ActivateRuntimeCoreAsync(
            projectKey,
            revision,
            package.Scripts,
            ct => _runtime.ActivateAsync(projectKey, revision, package, ct),
            package,
            cancellationToken);
    }

    /// <summary>
    /// Canonical persisted-Active activation boundary with the commit callback used by
    /// published activation persistence.
    /// </summary>
    public Task<RuntimeActivationResult> ActivateRuntimeAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(commitAsync);
        return ActivateRuntimeCoreAsync(
            projectKey,
            revision,
            package.Scripts,
            ct => _runtime.ActivateAsync(projectKey, revision, package, commitAsync, ct),
            package,
            cancellationToken);
    }

    public Task<RuntimeActivationResult> ActivateRuntimeForHaTakeoverAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(commitAsync);
        return ActivateRuntimeCoreAsync(
            projectKey,
            revision,
            package.Scripts,
            ct => _runtime.ActivateForHaTakeoverAsync(projectKey, revision, package, commitAsync, ct),
            package,
            cancellationToken);
    }

    /// <summary>
    /// Attaches Server Scripts to an already Active revision. Persisted production activation
    /// uses ActivateRuntimeAsync so the Runtime swap and script generation share the revision gate.
    /// </summary>
    public async Task ActivateAsync(
        string projectKey,
        long revision,
        IReadOnlyCollection<ScriptEngineeringDefinition>? scripts,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(projectKey, revision);
        var definitions = BuildDefinitions(scripts);
        ValidateServerTransientEventReferences(definitions, package: null);

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();

            ActiveGeneration? previous;
            ActiveGeneration generation;
            await _revisionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureRuntimeIdentity(projectKey, revision);
                generation = new ActiveGeneration(this, projectKey.Trim(), revision, definitions);
                previous = Interlocked.Exchange(ref _active, generation);
                previous?.Cancel();
            }
            finally
            {
                _revisionGate.Release();
            }

            if (previous is not null)
                await previous.DisposeAsync(runDisposeHandlers: false).ConfigureAwait(false);

            try
            {
                await generation.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                generation.Cancel();
                Interlocked.CompareExchange(ref _active, null, generation);
                await generation.DisposeAsync(runDisposeHandlers: false).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task DeactivateForInstallationDetachAsync(
        CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            ActiveGeneration? active;
            await _revisionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                active = Interlocked.Exchange(ref _active, null);
                active?.Cancel();
            }
            finally
            {
                _revisionGate.Release();
            }

            if (active is not null)
                await active.DisposeAsync(runDisposeHandlers: false).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public Task DispatchRuntimeEventAsync(
        string? targetReference = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        return Task.FromException(new ScriptExecutionDiagnosticException(
            "Server Runtime Script events require a canonical Active Transient Event occurrence."));
    }

    internal async Task<IReadOnlyDictionary<Guid, ServerScriptTagSnapshot>> ReadDependenciesAsync(
        string projectKey,
        long revision,
        IReadOnlyDictionary<Guid, string> dependencies,
        CancellationToken cancellationToken)
    {
        await _revisionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            EnsureRuntimeIdentity(projectKey, revision);

            var values = new Dictionary<Guid, ServerScriptTagSnapshot>();
            foreach (var dependency in dependencies)
            {
                if (!_runtime.TryGetTag(dependency.Key, out var tag) || tag is null)
                    throw new ScriptExecutionDiagnosticException("Server Script TAG dependency is not active.");

                var isServerMemory = _runtime.IsServerMemoryTag(dependency.Key);
                if (dependency.Value.Equals("ServerMemoryTag", StringComparison.OrdinalIgnoreCase) && !isServerMemory)
                    throw new ScriptExecutionDiagnosticException("ServerMemoryTag dependency does not resolve to Server Memory.");

                _runtime.TryGetCurrent(dependency.Key, out var current);
                values[dependency.Key] = new ServerScriptTagSnapshot(
                    dependency.Key,
                    tag.Path,
                    tag.DataType,
                    current?.Value,
                    isServerMemory);
            }

            return values;
        }
        finally
        {
            _revisionGate.Release();
        }
    }

    internal async ValueTask WriteTagAsync(
        string projectKey,
        long revision,
        Guid tagId,
        object? value,
        bool serverMemoryOnly,
        CancellationToken cancellationToken)
    {
        await _revisionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            EnsureRuntimeIdentity(projectKey, revision);

            if (!_runtime.TryGetTag(tagId, out var tag) || tag is null)
                throw new ScriptExecutionDiagnosticException("Python handler referenced a TAG that is not active.");
            if (tag.ReadOnly)
                throw new ScriptExecutionDiagnosticException($"TAG '{tag.Path}' is read-only.");
            if (serverMemoryOnly && !_runtime.IsServerMemoryTag(tagId))
                throw new ScriptExecutionDiagnosticException("write_server_memory may only target Server Memory TAGs.");

            await _runtime.WriteAsync(tagId, value, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _revisionGate.Release();
        }
    }

    internal void VerifyTagReference(Guid expectedTagId, string reference)
    {
        if (_runtime.TryGetTagByPath(reference, out var actual) && actual is not null)
        {
            if (actual.Id != expectedTagId)
                throw new ScriptExecutionDiagnosticException($"Declared TAG reference '{reference}' has identity drift.");

            return;
        }

        throw new ScriptExecutionDiagnosticException(
            $"Declared TAG reference '{reference}' is stale for stable TAG '{expectedTagId:D}'.");
    }

    internal async ValueTask<T> ExecuteAgainstActiveRevisionAsync<T>(
        string projectKey,
        long revision,
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await _revisionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            EnsureRuntimeIdentity(projectKey, revision);
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _revisionGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;

            var active = Interlocked.Exchange(ref _active, null);
            if (active is not null)
                await active.DisposeAsync(runDisposeHandlers: true).ConfigureAwait(false);

            _disposed = true;
            AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
            SharedHosts.Remove(_runtime);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task<RuntimeActivationResult> ActivateRuntimeCoreAsync(
        string projectKey,
        long revision,
        IReadOnlyCollection<ScriptEngineeringDefinition>? scripts,
        Func<CancellationToken, Task<RuntimeActivationResult>> activateRuntime,
        EngineeringPackage package,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(projectKey, revision);
        ArgumentNullException.ThrowIfNull(activateRuntime);
        var definitions = BuildDefinitions(scripts);
        ValidateServerTransientEventReferences(definitions, package);

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();

            RuntimeActivationResult result;
            ActiveGeneration? previous = null;
            ActiveGeneration? generation = null;

            await _revisionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                result = await activateRuntime(cancellationToken).ConfigureAwait(false);
                if (!result.Activated)
                    return result;

                EnsureRuntimeIdentity(projectKey, revision);
                generation = new ActiveGeneration(this, projectKey.Trim(), revision, definitions);
                previous = Interlocked.Exchange(ref _active, generation);
                previous?.Cancel();
            }
            finally
            {
                _revisionGate.Release();
            }

            if (previous is not null)
                await previous.DisposeAsync(runDisposeHandlers: false).ConfigureAwait(false);

            try
            {
                await generation!.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                generation!.Cancel();
                Interlocked.CompareExchange(ref _active, null, generation);
                await generation.DisposeAsync(runDisposeHandlers: false).ConfigureAwait(false);
                throw;
            }

            return result;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private IReadOnlyCollection<PythonScriptDefinition> BuildDefinitions(
        IReadOnlyCollection<ScriptEngineeringDefinition>? scripts)
    {
        var definitions = (scripts ?? Array.Empty<ScriptEngineeringDefinition>())
            .Where(script => script.Enabled && script.Scope == ScriptEngineeringScope.Server)
            .Select(ToPythonDefinition)
            .ToArray();

        foreach (var definition in definitions)
        foreach (var entry in definition.EntryPoints.Where(x => x.EventKind == PythonScriptEventKind.Timer))
        {
            var intervalMs = entry.TimerIntervalMs
                ?? throw new InvalidOperationException($"Timer entry point '{entry.HandlerName}' requires TimerIntervalMs.");
            var interval = TimeSpan.FromMilliseconds(intervalMs);
            if (interval < _policy.MinimumTimerInterval)
                throw new InvalidOperationException($"Timer entry point '{entry.HandlerName}' interval is below the configured minimum.");
        }

        return definitions;
    }

    private void ValidateServerTransientEventReferences(
        IReadOnlyCollection<PythonScriptDefinition> definitions,
        EngineeringPackage? package)
    {
        var entries = definitions
            .SelectMany(definition => definition.EntryPoints)
            .Where(entry => entry.EventKind == PythonScriptEventKind.ServerRuntimeEvent)
            .ToArray();
        if (entries.Length == 0)
            return;

        if (entries.Any(entry => !Guid.TryParse(entry.TargetReference, out _)))
        {
            throw new ScriptExecutionDiagnosticException(
                "Server Runtime Script event triggers must reference an Active Transient Event definition.");
        }

        var targets = entries
            .Select(entry => Guid.Parse(entry.TargetReference!))
            .Distinct()
            .ToArray();

        if (Volatile.Read(ref _driverInteractions) is null)
        {
            throw new ScriptExecutionDiagnosticException(
                "Active Driver Interaction authority is unavailable for Server Script event subscriptions.");
        }

        IReadOnlyCollection<TransientEventDefinition> eventDefinitions;
        IReadOnlyCollection<CapabilityEventReference> eventReferences;
        if (package is not null)
        {
            var graph = DriverInteractionEngineeringValidator.NormalizeActiveGraph(package);
            eventDefinitions = graph.EventDefinitions;
            eventReferences = graph.EventReferences;
        }
        else
        {
            var graph = CaptureActiveTransientEvents();
            eventDefinitions = graph.Definitions;
            eventReferences = graph.References;
        }

        var definitionsById = eventDefinitions.ToDictionary(definition => definition.DefinitionId);
        foreach (var target in targets)
        {
            if (!definitionsById.TryGetValue(target, out var definition) ||
                !eventReferences.Any(reference =>
                    reference.EventDefinitionId == target &&
                    reference.EquipmentId == definition.EquipmentId &&
                    StringComparer.Ordinal.Equals(reference.CapabilityId, definition.CapabilityId) &&
                    StringComparer.Ordinal.Equals(reference.SemanticEventKey, definition.SemanticKey)))
            {
                throw new ScriptExecutionDiagnosticException(
                    $"Server Script event reference '{target:D}' is not present in the Active Driver Interaction graph.");
            }
        }
    }

    private void EnsureRuntimeIdentity(string projectKey, long revision)
    {
        var descriptor = _runtime.Describe();
        if (!string.Equals(descriptor.ProjectKey, projectKey, StringComparison.Ordinal) || descriptor.Revision != revision)
        {
            throw new ScriptExecutionDiagnosticException(
                "Server Script execution belongs to an obsolete Active runtime revision.");
        }
    }

    private static void ValidateIdentity(string projectKey, long revision)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));
        if (revision <= 0)
            throw new ArgumentOutOfRangeException(nameof(revision));
    }

    private void DisposeFromProcessExit()
    {
        try { DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        catch { }
    }

    private static ScriptExecutionPolicy BuildPolicy(IConfiguration configuration)
    {
        var handlerTimeout = TimeSpan.FromMilliseconds(Math.Max(
            10,
            configuration.GetValue<int?>("ServerScripts:HandlerTimeoutMs") ?? 250));
        var minimumTimer = TimeSpan.FromMilliseconds(Math.Max(
            10,
            configuration.GetValue<int?>("ServerScripts:MinimumTimerIntervalMs") ?? 50));
        var maxQueued = Math.Max(
            1,
            configuration.GetValue<int?>("ServerScripts:MaxQueuedEvents") ?? 128);
        var maxFailures = Math.Max(
            1,
            configuration.GetValue<int?>("ServerScripts:MaxConsecutiveFailuresBeforeThrottle") ?? 5);
        var recoveryCooldown = TimeSpan.FromMilliseconds(Math.Max(
            10,
            configuration.GetValue<int?>("ServerScripts:FailureRecoveryCooldownMs") ?? 1000));
        return new ScriptExecutionPolicy(
            handlerTimeout,
            maxQueued,
            minimumTimer,
            maxFailures,
            failureRecoveryCooldown: recoveryCooldown);
    }

    private static PythonScriptDefinition ToPythonDefinition(ScriptEngineeringDefinition script) => new(
        script.Id,
        script.Path,
        script.Name,
        PythonScriptScope.Server,
        script.Source,
        script.Enabled,
        script.Language,
        script.LanguageVersion,
        script.EntryPoints.Select(entry => new PythonScriptEntryPoint(
            ToPythonEventKind(entry.EventKind),
            entry.HandlerName,
            entry.TargetReference,
            entry.TagReference,
            entry.TimerIntervalMs)).ToArray(),
        script.Dependencies.Select(dependency => new PythonScriptDependency(
            dependency.Kind.ToString(),
            dependency.StableReference,
            dependency.TagBinding is null
                ? null
                : new PythonScriptTagReferenceBinding(
                    dependency.TagBinding.Version,
                    dependency.TagBinding.Reference,
                    dependency.TagBinding.Expected))).ToArray(),
        script.Metadata);

    private static PythonScriptEventKind ToPythonEventKind(ScriptEngineeringEventKind kind) => kind switch
    {
        ScriptEngineeringEventKind.Initialize => PythonScriptEventKind.Initialize,
        ScriptEngineeringEventKind.Dispose => PythonScriptEventKind.Dispose,
        ScriptEngineeringEventKind.ObjectInteraction => PythonScriptEventKind.ObjectInteraction,
        ScriptEngineeringEventKind.TagChanged => PythonScriptEventKind.TagChanged,
        ScriptEngineeringEventKind.ClientMemoryChanged => PythonScriptEventKind.ClientMemoryChanged,
        ScriptEngineeringEventKind.Timer => PythonScriptEventKind.Timer,
        ScriptEngineeringEventKind.PropertyChanged => PythonScriptEventKind.PropertyChanged,
        ScriptEngineeringEventKind.FrameTick => PythonScriptEventKind.FrameTick,
        ScriptEngineeringEventKind.ServerRuntimeEvent => PythonScriptEventKind.ServerRuntimeEvent,
        _ => throw new InvalidOperationException($"Unsupported script event kind '{kind}'.")
    };

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ServerScriptRuntimeManager));
    }

    private sealed class ActiveGeneration : IAsyncDisposable
    {
        private readonly ServerScriptRuntimeManager _owner;
        private readonly CancellationTokenSource _cancellation = new();
        private readonly ConcurrentBag<Task> _backgroundTasks = new();
        private IDisposable? _tagSubscription;
        private IDisposable? _transientEventSubscription;
        private readonly IReadOnlyDictionary<Guid, TransientEventDefinition> _eventDefinitions;
        private readonly IReadOnlyCollection<CapabilityEventReference> _eventReferences;
        private int _started;

        public ActiveGeneration(
            ServerScriptRuntimeManager owner,
            string projectKey,
            long revision,
            IReadOnlyCollection<PythonScriptDefinition> definitions)
        {
            _owner = owner;
            ProjectKey = projectKey;
            Revision = revision;
            ActivatedAtUtc = DateTimeOffset.UtcNow;
            var activeEvents = owner.CaptureActiveTransientEvents();
            _eventDefinitions = activeEvents.Definitions.ToDictionary(definition => definition.DefinitionId);
            _eventReferences = activeEvents.References;

            var executor = new IsolatedPythonScriptHandlerExecutor(
                owner,
                projectKey,
                revision,
                owner._pythonExecutable,
                owner._runnerPath);

            Instances = definitions.Select(definition => new ScriptInstance(
                definition,
                new ScriptRuntimeExecutionCoordinator(
                    definition,
                    $"{projectKey}@{revision}:{definition.Id:D}",
                    owner._policy,
                    executor))).ToArray();
        }

        public string ProjectKey { get; }
        public long Revision { get; }
        public DateTimeOffset ActivatedAtUtc { get; }
        public IReadOnlyCollection<ScriptInstance> Instances { get; }

        public bool MatchesCurrentRuntime()
        {
            if (_cancellation.IsCancellationRequested) return false;
            var descriptor = _owner._runtime.Describe();
            return string.Equals(descriptor.ProjectKey, ProjectKey, StringComparison.Ordinal) &&
                   descriptor.Revision == Revision;
        }

        public void Cancel()
        {
            if (!_cancellation.IsCancellationRequested)
                _cancellation.Cancel();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var instance in Instances)
            foreach (var entry in instance.Definition.EntryPoints.Where(
                         x => x.EventKind == PythonScriptEventKind.Initialize))
            {
                await TriggerAndDrainAsync(instance, entry, null, cancellationToken)
                    .ConfigureAwait(false);
            }

            _tagSubscription = _owner._eventBus.Subscribe<TagValueChanged>(OnTagChangedAsync);
            _transientEventSubscription = _owner._eventBus.Subscribe<TransientEventRuntimePublication>(OnTransientEventAsync);
            Volatile.Write(ref _started, 1);

            foreach (var instance in Instances)
            foreach (var entry in instance.Definition.EntryPoints.Where(
                         x => x.EventKind == PythonScriptEventKind.Timer))
            {
                var interval = TimeSpan.FromMilliseconds(entry.TimerIntervalMs!.Value);
                Track(Task.Run(
                    () => TimerLoopAsync(instance, entry, interval, _cancellation.Token),
                    _cancellation.Token));
            }
        }

        private ValueTask OnTagChangedAsync(TagValueChanged change)
        {
            if (Volatile.Read(ref _started) == 0 || !MatchesCurrentRuntime())
                return ValueTask.CompletedTask;

            foreach (var instance in Instances)
            foreach (var entry in instance.Definition.EntryPoints.Where(x =>
                         x.EventKind == PythonScriptEventKind.TagChanged &&
                         x.TagReference?.TagId == change.Tag.Id))
            {
                Track(TriggerAndDrainAsync(
                    instance,
                    entry,
                    change.Tag.Id.ToString("D"),
                    _cancellation.Token));
            }

            return ValueTask.CompletedTask;
        }

        private ValueTask OnTransientEventAsync(TransientEventRuntimePublication publication)
        {
            if (Volatile.Read(ref _started) == 0 || !MatchesCurrentRuntime())
                return ValueTask.CompletedTask;

            var occurrence = publication.Occurrence;
            if (!_eventDefinitions.TryGetValue(occurrence.DefinitionId, out var definition))
                return ValueTask.CompletedTask;

            TransientEventOccurrence canonical;
            try
            {
                canonical = TransientEventContract.ValidateOccurrence(definition, occurrence);
            }
            catch (ArgumentException)
            {
                return ValueTask.CompletedTask;
            }

            if (!_eventReferences.Any(reference =>
                    reference.EventDefinitionId == canonical.DefinitionId &&
                    reference.EquipmentId == canonical.Source.EquipmentId &&
                    StringComparer.Ordinal.Equals(reference.CapabilityId, canonical.Source.CapabilityId) &&
                    StringComparer.Ordinal.Equals(reference.SemanticEventKey, canonical.SemanticKey)))
            {
                return ValueTask.CompletedTask;
            }

            foreach (var instance in Instances)
            foreach (var entry in instance.Definition.EntryPoints.Where(entry =>
                         entry.EventKind == PythonScriptEventKind.ServerRuntimeEvent &&
                         Guid.TryParse(entry.TargetReference, out var targetId) &&
                         targetId == canonical.DefinitionId))
            {
                try
                {
                    instance.Coordinator.Enqueue(new ScriptEventIdentity(
                        entry.EventKind,
                        entry.HandlerName,
                        entry.TargetReference,
                        coalescingKey: $"{entry.EventKind}:{entry.HandlerName}:{canonical.EventId:D}",
                        canonicalEventOccurrence: canonical));
                    ScheduleTransientEventDrain(instance);
                }
                catch (ObjectDisposedException)
                {
                    // A concurrent Active generation replacement already retired this queue.
                }
                catch (InvalidOperationException)
                {
                    // A stale or malformed subscription must not fail canonical event publication.
                }
            }

            return ValueTask.CompletedTask;
        }

        private void ScheduleTransientEventDrain(ScriptInstance instance)
        {
            if (!instance.TryBeginTransientEventDrain())
                return;

            Track(Task.Run(
                () => DrainTransientEventsAsync(instance, _cancellation.Token),
                _cancellation.Token));
        }

        private async Task DrainTransientEventsAsync(
            ScriptInstance instance,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && MatchesCurrentRuntime())
                {
                    var result = await instance.Coordinator.ProcessNextAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (result.Status == ScriptRuntimeDispatchStatus.Executed)
                        continue;
                    if (result.Status == ScriptRuntimeDispatchStatus.Throttled)
                    {
                        await Task.Delay(_owner._policy.FailureRecoveryCooldown, cancellationToken)
                            .ConfigureAwait(false);
                        continue;
                    }

                    break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            finally
            {
                instance.EndTransientEventDrain();
                if (!cancellationToken.IsCancellationRequested &&
                    MatchesCurrentRuntime() &&
                    instance.Coordinator.QueuedEventCount > 0)
                {
                    ScheduleTransientEventDrain(instance);
                }
            }
        }

        private async Task TimerLoopAsync(
            ScriptInstance instance,
            PythonScriptEntryPoint entry,
            TimeSpan interval,
            CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(interval);
            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!MatchesCurrentRuntime()) break;
                    await TriggerAndDrainAsync(
                            instance,
                            entry,
                            entry.TargetReference,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private async Task TriggerAndDrainAsync(
            ScriptInstance instance,
            PythonScriptEntryPoint entry,
            string? targetReference,
            CancellationToken cancellationToken)
        {
            if (!MatchesCurrentRuntime()) return;

            instance.Coordinator.Enqueue(new ScriptEventIdentity(
                entry.EventKind,
                entry.HandlerName,
                targetReference ?? entry.TargetReference));

            while (!cancellationToken.IsCancellationRequested && MatchesCurrentRuntime())
            {
                var result = await instance.Coordinator.ProcessNextAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (result.Status != ScriptRuntimeDispatchStatus.Executed) break;
                if (result.Execution?.Status is
                    ScriptExecutionStatus.TimedOut or
                    ScriptExecutionStatus.Cancelled)
                {
                    break;
                }
            }
        }

        private void Track(Task task)
        {
            _backgroundTasks.Add(task);
            _ = task.ContinueWith(
                _ => { },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public async ValueTask DisposeAsync() =>
            await DisposeAsync(runDisposeHandlers: true).ConfigureAwait(false);

        public async Task DisposeAsync(bool runDisposeHandlers)
        {
            _tagSubscription?.Dispose();
            _transientEventSubscription?.Dispose();
            Volatile.Write(ref _started, 0);

            if (runDisposeHandlers && MatchesCurrentRuntime())
            {
                using var disposeBudget =
                    new CancellationTokenSource(_owner._policy.HandlerTimeout);
                foreach (var instance in Instances)
                foreach (var entry in instance.Definition.EntryPoints.Where(
                             x => x.EventKind == PythonScriptEventKind.Dispose))
                {
                    try
                    {
                        await TriggerAndDrainAsync(
                                instance,
                                entry,
                                null,
                                disposeBudget.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }

            Cancel();
            var tasks = _backgroundTasks.ToArray();
            if (tasks.Length > 0)
            {
                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            foreach (var instance in Instances)
                await instance.Coordinator.DisposeAsync().ConfigureAwait(false);

            _cancellation.Dispose();
        }
    }

    public sealed record ScriptInstance(
        PythonScriptDefinition Definition,
        ScriptRuntimeExecutionCoordinator Coordinator)
    {
        private int _transientEventDrainScheduled;

        internal bool TryBeginTransientEventDrain() =>
            Interlocked.CompareExchange(ref _transientEventDrainScheduled, 1, 0) == 0;

        internal void EndTransientEventDrain() =>
            Interlocked.Exchange(ref _transientEventDrainScheduled, 0);

        public int SubscriptionCount => Definition.EntryPoints.Count(entry =>
            entry.EventKind is
                PythonScriptEventKind.Timer or
                PythonScriptEventKind.TagChanged or
                PythonScriptEventKind.ServerRuntimeEvent);
    }
}
