using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public sealed record EngineeringWorkspaceDescriptor(
    string? ProjectKey,
    string? ProjectName,
    long? BaseRevision,
    DateTimeOffset? CheckedOutAtUtc,
    DateTimeOffset? LastSavedAtUtc,
    bool IsDirty,
    long ChangeVersion,
    int TagCount,
    int AlarmCount,
    int DataSourceCount,
    int TemplateCount,
    int EquipmentCount,
    int DynamoCount,
    int ScreenCount,
    int PopupCount,
    int SecurityRoleCount,
    int CommandCount,
    int VisualAssetCount = 0);

public sealed class EngineeringWorkspaceVersionConflictException : InvalidOperationException
{
    public EngineeringWorkspaceVersionConflictException(long expectedChangeVersion, long currentChangeVersion)
        : base($"Engineering Workspace changed from version {expectedChangeVersion} to {currentChangeVersion}.")
    {
        ExpectedChangeVersion = expectedChangeVersion;
        CurrentChangeVersion = currentChangeVersion;
    }

    public long ExpectedChangeVersion { get; }
    public long CurrentChangeVersion { get; }
}

public sealed class EngineeringWorkspace : IDisposable
{
    private readonly InMemoryScadaEventBus _eventBus = new();
    private readonly object _stateGate = new();
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private string? _projectKey;
    private string? _projectName;
    private long? _baseRevision;
    private DateTimeOffset? _checkedOutAtUtc;
    private DateTimeOffset? _lastSavedAtUtc;
    private bool _isDirty;
    private long _changeVersion;

    public EngineeringWorkspace()
    {
        SessionId = Guid.NewGuid();
        Tags = new InMemoryTagRegistry(MarkDirty);
        Alarms = new InMemoryAlarmEngine(_eventBus, MarkDirty);
        DataSources = new InMemoryDataSourceEngineeringRegistry(MarkDirty);
        Assets = new InMemoryEngineeringAssetRegistry(MarkDirty);
        Views = new InMemoryEngineeringViewRegistry(MarkDirty);
        SecurityPolicies = new InMemorySecurityPolicyEngineeringRegistry(MarkDirty);
        Commands = new InMemoryCommandEngineeringRegistry(MarkDirty);
        Scripts = new InMemoryScriptEngineeringRegistry(MarkDirty);
        VisualAssets = new InMemoryVisualAssetEngineeringRegistry(MarkDirty);
        SeedDemo();
    }

    public Guid SessionId { get; }
    public InMemoryTagRegistry Tags { get; }
    public InMemoryAlarmEngine Alarms { get; }
    public InMemoryDataSourceEngineeringRegistry DataSources { get; }
    public InMemoryEngineeringAssetRegistry Assets { get; }
    public InMemoryEngineeringViewRegistry Views { get; }
    public InMemorySecurityPolicyEngineeringRegistry SecurityPolicies { get; }
    public InMemoryCommandEngineeringRegistry Commands { get; }
    public InMemoryScriptEngineeringRegistry Scripts { get; }
    public InMemoryVisualAssetEngineeringRegistry VisualAssets { get; }

    public EngineeringWorkspaceDescriptor Describe()
    {
        lock (_stateGate)
        {
            return new EngineeringWorkspaceDescriptor(
                _projectKey,
                _projectName,
                _baseRevision,
                _checkedOutAtUtc,
                _lastSavedAtUtc,
                _isDirty,
                _changeVersion,
                Tags.Snapshot().Count,
                Alarms.Definitions().Count,
                DataSources.Snapshot().Count,
                Assets.SnapshotTemplates().Count,
                Assets.SnapshotEquipment().Count,
                Assets.SnapshotDynamos().Count,
                Views.SnapshotScreens().Count,
                Views.SnapshotPopups().Count,
                SecurityPolicies.SnapshotRoles().Count,
                Commands.Snapshot().Count,
                VisualAssets.SnapshotAssets().Count);
        }
    }

    public long CaptureChangeVersion()
    {
        lock (_stateGate)
            return _changeVersion;
    }

    public async ValueTask<IAsyncDisposable> AcquireMutationAsync(
        long? expectedChangeVersion = null,
        CancellationToken cancellationToken = default)
    {
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            if (expectedChangeVersion.HasValue)
            {
                var current = CaptureChangeVersion();
                if (current != expectedChangeVersion.Value)
                    throw new EngineeringWorkspaceVersionConflictException(expectedChangeVersion.Value, current);
            }

            return new MutationLease(_mutationGate);
        }
        catch
        {
            _mutationGate.Release();
            throw;
        }
    }

    public void MarkDirty()
    {
        lock (_stateGate)
        {
            _isDirty = true;
            _changeVersion++;
        }
    }

    public void SetCheckout(
        string projectKey,
        string projectName,
        long revision,
        DateTimeOffset? savedAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

        lock (_stateGate)
        {
            _projectKey = projectKey.Trim();
            _projectName = projectName.Trim();
            _baseRevision = revision;
            _checkedOutAtUtc = DateTimeOffset.UtcNow;
            _lastSavedAtUtc = savedAtUtc;
            _isDirty = false;
            _changeVersion++;
        }
    }

    public void SetSavedRevision(long revision, DateTimeOffset savedAtUtc)
    {
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        lock (_stateGate)
        {
            _baseRevision = revision;
            _lastSavedAtUtc = savedAtUtc;
            _isDirty = false;
            _changeVersion++;
        }
    }

    public void Dispose()
    {
        Alarms.Dispose();
        _eventBus.Dispose();
        _mutationGate.Dispose();
    }

    private void SeedDemo()
    {
        // Kept intentionally empty for deterministic host startup. Product Demo content is
        // authored/imported through canonical Engineering and package mechanisms instead.
    }

    private sealed class MutationLease : IAsyncDisposable
    {
        private SemaphoreSlim? _gate;

        public MutationLease(SemaphoreSlim gate) => _gate = gate;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _gate, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
