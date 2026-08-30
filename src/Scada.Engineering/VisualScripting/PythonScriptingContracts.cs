using System.Collections.ObjectModel;
using Scada.Core.Tags;

namespace Scada.Engineering.VisualScripting;

public enum PythonScriptScope
{
    ClientVisual,
    Server
}

public enum PythonScriptEventKind
{
    Initialize,
    Dispose,
    ObjectInteraction,
    TagChanged,
    ClientMemoryChanged,
    Timer,
    PropertyChanged,
    FrameTick,
    ServerRuntimeEvent
}

public sealed record PythonScriptEntryPoint(
    PythonScriptEventKind EventKind,
    string HandlerName,
    string? TargetReference = null,
    TagValueReference? TagReference = null,
    int? TimerIntervalMs = null);

public sealed record PythonScriptDependency(
    string Kind,
    string StableReference);

public sealed class PythonScriptDefinition
{
    public PythonScriptDefinition(
        Guid id,
        string path,
        string name,
        PythonScriptScope scope,
        string source,
        bool enabled = true,
        string language = "python",
        string languageVersion = "3",
        IReadOnlyCollection<PythonScriptEntryPoint>? entryPoints = null,
        IReadOnlyCollection<PythonScriptDependency>? dependencies = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Script stable ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Script path is required.", nameof(path));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Script name is required.", nameof(name));
        if (!string.Equals(language, "python", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The scripting foundation currently supports Python only.", nameof(language));
        if (string.IsNullOrWhiteSpace(languageVersion))
            throw new ArgumentException("Python language version marker is required.", nameof(languageVersion));

        ArgumentNullException.ThrowIfNull(source);

        Id = id;
        Path = path;
        Name = name;
        Scope = scope;
        Source = source;
        Enabled = enabled;
        Language = "python";
        LanguageVersion = languageVersion;
        EntryPoints = Array.AsReadOnly((entryPoints ?? Array.Empty<PythonScriptEntryPoint>()).ToArray());
        Dependencies = Array.AsReadOnly((dependencies ?? Array.Empty<PythonScriptDependency>()).ToArray());
        Metadata = new ReadOnlyDictionary<string, string>(
            (metadata ?? new Dictionary<string, string>())
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal));
    }

    public Guid Id { get; }

    public string Path { get; }

    public string Name { get; }

    public PythonScriptScope Scope { get; }

    public string Source { get; }

    public bool Enabled { get; }

    public string Language { get; }

    public string LanguageVersion { get; }

    public IReadOnlyCollection<PythonScriptEntryPoint> EntryPoints { get; }

    public IReadOnlyCollection<PythonScriptDependency> Dependencies { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }
}

[Flags]
public enum ScriptApiCapability : long
{
    None = 0,
    ReadSharedTags = 1L << 0,
    RequestAuthorizedBackendOperation = 1L << 1,
    ReadClientMemory = 1L << 2,
    WriteClientMemory = 1L << 3,
    ReadVisualProperties = 1L << 4,
    WriteVisualProperties = 1L << 5,
    RequestVisualTween = 1L << 6,
    ReadServerMemory = 1L << 7,
    WriteServerMemory = 1L << 8,
    WriteSharedTags = 1L << 9
}

public enum ScriptSandboxDeniedBoundary
{
    FileSystem,
    OperatingSystem,
    ShellOrProcessExecution,
    ArbitraryNetwork,
    Database,
    IndustrialDrivers,
    Secrets,
    BrowserDom,
    BrowserStorage
}

public sealed class ScriptApiSurface
{
    private static readonly IReadOnlyCollection<ScriptSandboxDeniedBoundary> MandatoryDeniedBoundaries =
        Array.AsReadOnly(Enum.GetValues<ScriptSandboxDeniedBoundary>());

    private ScriptApiSurface(
        PythonScriptScope scope,
        ScriptApiCapability allowedCapabilities)
    {
        Scope = scope;
        AllowedCapabilities = allowedCapabilities;
        DeniedBoundaries = MandatoryDeniedBoundaries;
        ValidateScopeCapabilities();
    }

    public PythonScriptScope Scope { get; }

    public ScriptApiCapability AllowedCapabilities { get; }

    public IReadOnlyCollection<ScriptSandboxDeniedBoundary> DeniedBoundaries { get; }

    public bool Allows(ScriptApiCapability capability) =>
        (AllowedCapabilities & capability) == capability;

    public bool Denies(ScriptSandboxDeniedBoundary boundary) =>
        DeniedBoundaries.Contains(boundary);

    public static ScriptApiSurface ClientVisual() =>
        new(
            PythonScriptScope.ClientVisual,
            ScriptApiCapability.ReadSharedTags |
            ScriptApiCapability.RequestAuthorizedBackendOperation |
            ScriptApiCapability.ReadClientMemory |
            ScriptApiCapability.WriteClientMemory |
            ScriptApiCapability.ReadVisualProperties |
            ScriptApiCapability.WriteVisualProperties |
            ScriptApiCapability.RequestVisualTween);

    public static ScriptApiSurface Server() =>
        new(
            PythonScriptScope.Server,
            ScriptApiCapability.ReadSharedTags |
            ScriptApiCapability.ReadServerMemory |
            ScriptApiCapability.WriteServerMemory |
            ScriptApiCapability.WriteSharedTags);

    public static ScriptApiSurface CreateValidated(
        PythonScriptScope scope,
        ScriptApiCapability allowedCapabilities) =>
        new(scope, allowedCapabilities);

    private void ValidateScopeCapabilities()
    {
        const ScriptApiCapability clientOnly =
            ScriptApiCapability.ReadClientMemory |
            ScriptApiCapability.WriteClientMemory |
            ScriptApiCapability.ReadVisualProperties |
            ScriptApiCapability.WriteVisualProperties |
            ScriptApiCapability.RequestVisualTween |
            ScriptApiCapability.RequestAuthorizedBackendOperation;

        const ScriptApiCapability serverOnly =
            ScriptApiCapability.ReadServerMemory |
            ScriptApiCapability.WriteServerMemory |
            ScriptApiCapability.WriteSharedTags;

        if (Scope == PythonScriptScope.ClientVisual && (AllowedCapabilities & serverOnly) != 0)
            throw new ArgumentException("Client Visual Scripts cannot receive server-only scripting capabilities.");

        if (Scope == PythonScriptScope.Server && (AllowedCapabilities & clientOnly) != 0)
            throw new ArgumentException("Server Scripts cannot receive client visual or Client Memory capabilities.");
    }
}

public enum ScriptQueueOverflowStrategy
{
    CoalesceByEventKey,
    RejectNewest,
    DropOldest
}

public enum ScriptFaultIsolationScope
{
    ScriptRuntimeInstance
}

public sealed class ScriptExecutionPolicy
{
    public ScriptExecutionPolicy(
        TimeSpan handlerTimeout,
        int maxQueuedEvents,
        TimeSpan minimumTimerInterval,
        int maxConsecutiveFailuresBeforeThrottle,
        ScriptQueueOverflowStrategy queueOverflowStrategy = ScriptQueueOverflowStrategy.CoalesceByEventKey,
        ScriptFaultIsolationScope faultIsolationScope = ScriptFaultIsolationScope.ScriptRuntimeInstance)
    {
        if (handlerTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(handlerTimeout), "Handler timeout must be positive.");
        if (maxQueuedEvents <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxQueuedEvents), "Event queue capacity must be positive and bounded.");
        if (minimumTimerInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minimumTimerInterval), "Minimum timer interval must be positive.");
        if (maxConsecutiveFailuresBeforeThrottle <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConsecutiveFailuresBeforeThrottle), "Failure throttle threshold must be positive.");

        HandlerTimeout = handlerTimeout;
        MaxQueuedEvents = maxQueuedEvents;
        MinimumTimerInterval = minimumTimerInterval;
        MaxConsecutiveFailuresBeforeThrottle = maxConsecutiveFailuresBeforeThrottle;
        QueueOverflowStrategy = queueOverflowStrategy;
        FaultIsolationScope = faultIsolationScope;
    }

    public TimeSpan HandlerTimeout { get; }

    public int MaxQueuedEvents { get; }

    public TimeSpan MinimumTimerInterval { get; }

    public int MaxConsecutiveFailuresBeforeThrottle { get; }

    public ScriptQueueOverflowStrategy QueueOverflowStrategy { get; }

    public ScriptFaultIsolationScope FaultIsolationScope { get; }

    public static ScriptExecutionPolicy SafeDefault { get; } =
        new(
            handlerTimeout: TimeSpan.FromMilliseconds(250),
            maxQueuedEvents: 128,
            minimumTimerInterval: TimeSpan.FromMilliseconds(50),
            maxConsecutiveFailuresBeforeThrottle: 5);
}

public enum ScriptExecutionStatus
{
    Completed,
    Cancelled,
    TimedOut,
    Faulted,
    RejectedQueueFull,
    Throttled
}

public sealed record ScriptExecutionResult(
    Guid ScriptId,
    string RuntimeInstanceId,
    string HandlerName,
    ScriptExecutionStatus Status,
    TimeSpan Duration,
    DateTimeOffset CompletedAt,
    string? SanitizedError = null);

public sealed record ScriptExecutionLease(
    Guid ScriptId,
    string RuntimeInstanceId,
    string HandlerName,
    DateTimeOffset StartedAt,
    DateTimeOffset Deadline,
    CancellationToken CancellationToken)
{
    public static ScriptExecutionLease Create(
        Guid scriptId,
        string runtimeInstanceId,
        string handlerName,
        ScriptExecutionPolicy policy,
        CancellationToken cancellationToken)
    {
        if (scriptId == Guid.Empty)
            throw new ArgumentException("Script ID is required.", nameof(scriptId));
        if (string.IsNullOrWhiteSpace(runtimeInstanceId))
            throw new ArgumentException("Runtime script instance ID is required.", nameof(runtimeInstanceId));
        if (string.IsNullOrWhiteSpace(handlerName))
            throw new ArgumentException("Handler name is required.", nameof(handlerName));

        ArgumentNullException.ThrowIfNull(policy);

        var startedAt = DateTimeOffset.UtcNow;
        return new(
            scriptId,
            runtimeInstanceId,
            handlerName,
            startedAt,
            startedAt + policy.HandlerTimeout,
            cancellationToken);
    }
}