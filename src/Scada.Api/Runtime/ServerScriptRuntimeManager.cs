using System.Collections.Concurrent;
using Scada.Core.Abstractions;
using Scada.Core.Events;
using Scada.DriverHost.Runtime;
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

/// <summary>
/// Active-revision-only host for Server Engineering Scripts. The host owns one
/// bounded execution coordinator per script, lifecycle events, canonical timers,
/// TAG-change subscriptions and cancellation of the previous Active generation.
/// </summary>
public sealed class ServerScriptRuntimeManager : IAsyncDisposable
{
    private static readonly object SharedGate = new();
    private static ServerScriptRuntimeManager? _shared;

    private readonly IEngineeringRuntimeCoordinator _runtime;
    private readonly IScadaEventBus _eventBus;
    private readonly ScriptExecutionPolicy _policy;
    private readonly IPythonScriptHandlerExecutor _executor;
    private readonly SemaphoreSlim _activationGate = new(1, 1);
    private ActiveGeneration? _active;
    private bool _disposed;

    private ServerScriptRuntimeManager(
        IEngineeringRuntimeCoordinator runtime,
        IScadaEventBus eventBus,
        IConfiguration configuration)
    {
        _runtime = runtime;
        _eventBus = eventBus;
        _policy = BuildPolicy(configuration);
        var executable = configuration["ServerScripts:PythonExecutable"];
        if (string.IsNullOrWhiteSpace(executable))
            executable = OperatingSystem.IsWindows() ? "python" : "python3";
        var runnerPath = Path.Combine(AppContext.BaseDirectory, "ServerScriptRunner.py");
        _executor = new IsolatedPythonScriptHandlerExecutor(runtime, executable, runnerPath);

        AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeFromProcessExit();
    }

    public static ServerScriptRuntimeManager GetShared(
        IEngineeringRuntimeCoordinator runtime,
        IScadaEventBus eventBus,
        IConfiguration configuration)
    {
        lock (SharedGate)
        {
            if (_shared is null)
                _shared = new ServerScriptRuntimeManager(runtime, eventBus, configuration);
            else if (!ReferenceEquals(_shared._runtime, runtime) || !ReferenceEquals(_shared._eventBus, eventBus))
                throw new InvalidOperationException("Server Script runtime host is already bound to another runtime instance.");
            return _shared;
        }
    }

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

    public async Task ActivateAsync(
        string projectKey,
        long revision,
        IReadOnlyCollection<ScriptEngineeringDefinition>? scripts,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));
        if (revision <= 0)
            throw new ArgumentOutOfRangeException(nameof(revision));

        await _activationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            var descriptor = _runtime.Describe();
            if (!string.Equals(descriptor.ProjectKey, projectKey, StringComparison.Ordinal) || descriptor.Revision != revision)
                throw new InvalidOperationException("Server Scripts may only activate for the currently Active runtime revision.");

            var old = Interlocked.Exchange(ref _active, null);
            if (old is not null)
                await old.DisposeAsync(runDisposeHandlers: false);

            var definitions = (scripts ?? Array.Empty<ScriptEngineeringDefinition>())
                .Where(script => script.Enabled && script.Scope == ScriptEngineeringScope.Server)
                .Select(ToPythonDefinition)
                .ToArray();

            var generation = new ActiveGeneration(this, projectKey.Trim(), revision, definitions);
            Volatile.Write(ref _active, generation);
            await generation.StartAsync(cancellationToken);
        }
        finally
        {
            _activationGate.Release();
        }
    }

    public async Task DispatchRuntimeEventAsync(
        string? targetReference = null,
        CancellationToken cancellationToken = default)
    {
        var active = Volatile.Read(ref _active);
        if (active is null || !active.MatchesCurrentRuntime()) return;
        await active.DispatchRuntimeEventAsync(targetReference, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _activationGate.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            var active = Interlocked.Exchange(ref _active, null);
            if (active is not null)
                await active.DisposeAsync(runDisposeHandlers: true);
        }
        finally
        {
            _activationGate.Release();
            _activationGate.Dispose();
        }
    }

    private void DisposeFromProcessExit()
    {
        try { DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        catch { }
    }

    private static ScriptExecutionPolicy BuildPolicy(IConfiguration configuration)
    {
        var handlerTimeout = TimeSpan.FromMilliseconds(Math.Max(10,
            configuration.GetValue<int?>("ServerScripts:HandlerTimeoutMs") ?? 250));
        var minimumTimer = TimeSpan.FromMilliseconds(Math.Max(10,
            configuration.GetValue<int?>("ServerScripts:MinimumTimerIntervalMs") ?? 50));
        var maxQueued = Math.Max(1, configuration.GetValue<int?>("ServerScripts:MaxQueuedEvents") ?? 128);
        var maxFailures = Math.Max(1, configuration.GetValue<int?>("ServerScripts:MaxConsecutiveFailuresBeforeThrottle") ?? 5);
        return new ScriptExecutionPolicy(handlerTimeout, maxQueued, minimumTimer, maxFailures);
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
            dependency.StableReference)).ToArray(),
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
        if (_disposed) throw new ObjectDisposedException(nameof(ServerScriptRuntimeManager));
    }

    private sealed class ActiveGeneration : IAsyncDisposable
    {
        private readonly ServerScriptRuntimeManager _owner;
        private readonly CancellationTokenSource _cancellation = new();
        private readonly ConcurrentBag<Task> _backgroundTasks = new();
        private readonly IDisposable _tagSubscription;

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
            Instances = definitions.Select(definition => new ScriptInstance(
                definition,
                new ScriptRuntimeExecutionCoordinator(
                    definition,
                    $"{projectKey}@{revision}:{definition.Id:D}",
                    owner._policy,
                    owner._executor))).ToArray();
            _tagSubscription = owner._eventBus.Subscribe<TagValueChanged>(OnTagChangedAsync);
        }

        public string ProjectKey { get; }
        public long Revision { get; }
        public DateTimeOffset ActivatedAtUtc { get; }
        public IReadOnlyCollection<ScriptInstance> Instances { get; }

        public bool MatchesCurrentRuntime()
        {
            var descriptor = _owner._runtime.Describe();
            return string.Equals(descriptor.ProjectKey, ProjectKey, StringComparison.Ordinal) && descriptor.Revision == Revision;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var instance in Instances)
            {
                foreach (var entry in instance.Definition.EntryPoints.Where(x => x.EventKind == PythonScriptEventKind.Initialize))
                    await TriggerAndDrainAsync(instance, entry, null, cancellationToken);

                foreach (var entry in instance.Definition.EntryPoints.Where(x => x.EventKind == PythonScriptEventKind.Timer))
                {
                    var intervalMs = entry.TimerIntervalMs
                        ?? throw new InvalidOperationException($"Timer entry point '{entry.HandlerName}' requires TimerIntervalMs.");
                    var interval = TimeSpan.FromMilliseconds(intervalMs);
                    if (interval < _owner._policy.MinimumTimerInterval)
                        throw new InvalidOperationException(
                            $"Timer entry point '{entry.HandlerName}' interval is below the configured minimum.");
                    Track(Task.Run(() => TimerLoopAsync(instance, entry, interval, _cancellation.Token), _cancellation.Token));
                }
            }
        }

        public async Task DispatchRuntimeEventAsync(string? targetReference, CancellationToken cancellationToken)
        {
            foreach (var instance in Instances)
            foreach (var entry in instance.Definition.EntryPoints.Where(x =>
                         x.EventKind == PythonScriptEventKind.ServerRuntimeEvent &&
                         (string.IsNullOrWhiteSpace(x.TargetReference) ||
                          string.Equals(x.TargetReference, targetReference, StringComparison.Ordinal))))
                await TriggerAndDrainAsync(instance, entry, targetReference, cancellationToken);
        }

        private ValueTask OnTagChangedAsync(TagValueChanged change)
        {
            if (_cancellation.IsCancellationRequested || !MatchesCurrentRuntime())
                return ValueTask.CompletedTask;

            foreach (var instance in Instances)
            foreach (var entry in instance.Definition.EntryPoints.Where(x =>
                         x.EventKind == PythonScriptEventKind.TagChanged &&
                         x.TagReference?.TagId == change.Tag.Id))
            {
                var task = TriggerAndDrainAsync(
                    instance,
                    entry,
                    change.Tag.Id.ToString("D"),
                    _cancellation.Token);
                Track(task);
            }
            return ValueTask.CompletedTask;
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
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    if (!MatchesCurrentRuntime()) break;
                    await TriggerAndDrainAsync(instance, entry, entry.TargetReference, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        }

        private async Task TriggerAndDrainAsync(
            ScriptInstance instance,
            PythonScriptEntryPoint entry,
            string? targetReference,
            CancellationToken cancellationToken)
        {
            if (_cancellation.IsCancellationRequested || !MatchesCurrentRuntime()) return;
            instance.Coordinator.Enqueue(new ScriptEventIdentity(
                entry.EventKind,
                entry.HandlerName,
                targetReference ?? entry.TargetReference));

            while (!cancellationToken.IsCancellationRequested && MatchesCurrentRuntime())
            {
                var result = await instance.Coordinator.ProcessNextAsync(cancellationToken);
                if (result.Status != ScriptRuntimeDispatchStatus.Executed) break;
                if (result.Execution?.Status is ScriptExecutionStatus.TimedOut or ScriptExecutionStatus.Cancelled)
                    break;
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

        public async ValueTask DisposeAsync() => await DisposeAsync(runDisposeHandlers: true);

        public async Task DisposeAsync(bool runDisposeHandlers)
        {
            _tagSubscription.Dispose();

            if (runDisposeHandlers && MatchesCurrentRuntime())
            {
                using var disposeBudget = new CancellationTokenSource(_owner._policy.HandlerTimeout);
                foreach (var instance in Instances)
                foreach (var entry in instance.Definition.EntryPoints.Where(x => x.EventKind == PythonScriptEventKind.Dispose))
                {
                    try { await TriggerAndDrainAsync(instance, entry, null, disposeBudget.Token); }
                    catch (OperationCanceledException) { }
                }
            }

            _cancellation.Cancel();
            var tasks = _backgroundTasks.ToArray();
            if (tasks.Length > 0)
            {
                try { await Task.WhenAll(tasks); }
                catch (OperationCanceledException) { }
            }

            foreach (var instance in Instances)
                await instance.Coordinator.DisposeAsync();
            _cancellation.Dispose();
        }
    }

    public sealed record ScriptInstance(
        PythonScriptDefinition Definition,
        ScriptRuntimeExecutionCoordinator Coordinator)
    {
        public int SubscriptionCount => Definition.EntryPoints.Count(entry =>
            entry.EventKind is PythonScriptEventKind.Timer or
                PythonScriptEventKind.TagChanged or
                PythonScriptEventKind.ServerRuntimeEvent);
    }
}
