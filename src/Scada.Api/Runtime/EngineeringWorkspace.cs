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
    int CommandCount);

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
        Tags = new InMemoryTagRegistry(MarkDirty);
        Alarms = new InMemoryAlarmEngine(_eventBus, MarkDirty);
        DataSources = new InMemoryDataSourceEngineeringRegistry(MarkDirty);
        Assets = new InMemoryEngineeringAssetRegistry(MarkDirty);
        Views = new InMemoryEngineeringViewRegistry(MarkDirty);
        SecurityPolicies = new InMemorySecurityPolicyEngineeringRegistry(MarkDirty);
        Commands = new InMemoryCommandEngineeringRegistry(MarkDirty);
        Scripts = new InMemoryScriptEngineeringRegistry(MarkDirty);
        SeedDemo();
    }

    public InMemoryTagRegistry Tags { get; }
    public InMemoryAlarmEngine Alarms { get; }
    public InMemoryDataSourceEngineeringRegistry DataSources { get; }
    public InMemoryEngineeringAssetRegistry Assets { get; }
    public InMemoryEngineeringViewRegistry Views { get; }
    public InMemorySecurityPolicyEngineeringRegistry SecurityPolicies { get; }
    public InMemoryCommandEngineeringRegistry Commands { get; }
    public InMemoryScriptEngineeringRegistry Scripts { get; }

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
                Commands.Snapshot().Count);
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
        }
    }

    public void AcceptSave(
        string projectKey,
        string projectName,
        long revision,
        DateTimeOffset savedAtUtc,
        long savedChangeVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));

        lock (_stateGate)
        {
            _projectKey = projectKey.Trim();
            _projectName = projectName.Trim();
            _baseRevision = revision;
            _lastSavedAtUtc = savedAtUtc;
            _isDirty = _changeVersion != savedChangeVersion;
        }
    }

    public void RestoreDescriptor(EngineeringWorkspaceDescriptor descriptor)
    {
        lock (_stateGate)
        {
            _projectKey = descriptor.ProjectKey;
            _projectName = descriptor.ProjectName;
            _baseRevision = descriptor.BaseRevision;
            _checkedOutAtUtc = descriptor.CheckedOutAtUtc;
            _lastSavedAtUtc = descriptor.LastSavedAtUtc;
            _isDirty = descriptor.IsDirty;
            _changeVersion = descriptor.ChangeVersion;
        }
    }

    public void Clear()
    {
        Alarms.Clear();
        Tags.Clear();
        DataSources.Clear();
        Assets.Clear();
        Views.Clear();
        SecurityPolicies.Clear();
        Commands.Clear();
        Scripts.Clear();
    }

    private void SeedDemo()
    {
        foreach (var tag in DemoProcessModel.CreateTagDefinitions())
            Tags.Register(tag);

        foreach (var alarm in DemoProcessModel.CreateAlarmDefinitions())
            Alarms.Register(alarm);

        foreach (var command in DemoProcessModel.CreateCommandDefinitions())
        {
            Commands.Upsert(new CommandEngineeringDto(
                command.Id,
                command.Key,
                command.Name,
                command.Kind,
                Convert.ToString(command.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                command.TargetTagId,
                command.TargetTagPath,
                command.Description,
                command.Area,
                command.EquipmentPath,
                Enabled: true,
                Metadata: command.Metadata is null ? null : new Dictionary<string, string>(command.Metadata)));
        }

        DataSources.Upsert(new DataSourceEngineeringDto(
            Id: Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Key: "builtin.simulation",
            Name: "Built-in Simulation",
            Driver: "builtin.simulation",
            Enabled: true,
            Settings: new Dictionary<string, string>
            {
                ["scanIntervalMilliseconds"] = "500"
            },
            Metadata: new Dictionary<string, string>
            {
                ["system"] = "true"
            }));

        var templateBindings = new[]
        {
            new EngineeringBindingDto("running", EngineeringBindingKind.Tag, "{equipmentPath}.Running", "read"),
            new EngineeringBindingDto("fault", EngineeringBindingKind.Tag, "{equipmentPath}.Fault", "read"),
            new EngineeringBindingDto("current", EngineeringBindingKind.Tag, "{equipmentPath}.Current", "read"),
            new EngineeringBindingDto("frequency", EngineeringBindingKind.Tag, "{equipmentPath}.Frequency", "readWrite")
        };

        Assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            Id: Guid.Parse("41000000-0000-0000-0000-000000000001"),
            Key: "pump.standard",
            Name: "Standard Pump",
            Bindings: templateBindings,
            Properties: new Dictionary<string, string>
            {
                ["category"] = "pump",
                ["defaultFrequencyHz"] = "60"
            },
            Context: new Dictionary<string, string>
            {
                ["domain"] = "pumping"
            }));

        Assets.UpsertEquipment(new EquipmentEngineeringDto(
            Id: Guid.Parse("42000000-0000-0000-0000-000000000001"),
            Path: "Demo.P01",
            Name: "Pump P01",
            TemplateKey: "pump.standard",
            Bindings: new[]
            {
                new EngineeringBindingDto("running", EngineeringBindingKind.Tag, "Demo.P01.Running", "read"),
                new EngineeringBindingDto("fault", EngineeringBindingKind.Tag, "Demo.P01.Fault", "read"),
                new EngineeringBindingDto("current", EngineeringBindingKind.Tag, "Demo.P01.Current", "read"),
                new EngineeringBindingDto("frequency", EngineeringBindingKind.Tag, "Demo.P01.Frequency", "readWrite")
            },
            Properties: new Dictionary<string, string>
            {
                ["displayLabel"] = "P01"
            },
            Context: new Dictionary<string, string>
            {
                ["area"] = "Demo",
                ["process"] = "Discharge"
            }));

        Assets.UpsertDynamo(new DynamoEngineeringDto(
            Id: Guid.Parse("43000000-0000-0000-0000-000000000001"),
            Key: "dynamo.pump.standard",
            Name: "Standard Pump Dynamo",
            TemplateKey: "pump.standard",
            Bindings: templateBindings,
            Properties: new Dictionary<string, string>
            {
                ["symbol"] = "pump"
            },
            Context: new Dictionary<string, string>
            {
                ["usage"] = "process-screen"
            }));

        Views.UpsertScreen(new ScreenEngineeringDto(
            Id: Guid.Parse("44000000-0000-0000-0000-000000000001"),
            Key: "demo.overview",
            Name: "Demo Overview",
            Route: "/demo",
            Elements: new[]
            {
                new VisualElementEngineeringDto(
                    Key: "tank01",
                    Type: "tank",
                    Bindings: new[]
                    {
                        new EngineeringBindingDto("level", EngineeringBindingKind.Tag, "Demo.Tank01.Level", "read")
                    },
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["label"] = JsonSerializer.SerializeToElement("Reservatório TK01"),
                        ["x"] = JsonSerializer.SerializeToElement(100d),
                        ["y"] = JsonSerializer.SerializeToElement(100d)
                    }),
                new VisualElementEngineeringDto(
                    Key: "pump01",
                    Type: "dynamo",
                    DynamoKey: "dynamo.pump.standard",
                    EquipmentPath: "Demo.P01",
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["x"] = JsonSerializer.SerializeToElement(430d),
                        ["y"] = JsonSerializer.SerializeToElement(160d)
                    }),
                new VisualElementEngineeringDto(
                    Key: "pressure",
                    Type: "value",
                    Bindings: new[]
                    {
                        new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "Demo.Discharge.Pressure", "read")
                    },
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["label"] = JsonSerializer.SerializeToElement("Pressão")
                    }),
                new VisualElementEngineeringDto(
                    Key: "flow",
                    Type: "value",
                    Bindings: new[]
                    {
                        new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "Demo.Discharge.Flow", "read")
                    },
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["label"] = JsonSerializer.SerializeToElement("Vazão")
                    })
            },
            Properties: new Dictionary<string, string>
            {
                ["canvasWidth"] = "1366",
                ["canvasHeight"] = "768"
            },
            Context: new Dictionary<string, string>
            {
                ["area"] = "Demo",
                ["process"] = "Pumping"
            }));

        Views.UpsertPopup(new PopupEngineeringDto(
            Id: Guid.Parse("45000000-0000-0000-0000-000000000001"),
            Key: "popup.pump.standard",
            Name: "Standard Pump Popup",
            TemplateKey: "pump.standard",
            Elements: new[]
            {
                new VisualElementEngineeringDto(
                    Key: "current",
                    Type: "value",
                    Bindings: new[]
                    {
                        new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "{equipmentPath}.Current", "read")
                    },
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["label"] = JsonSerializer.SerializeToElement("Corrente")
                    }),
                new VisualElementEngineeringDto(
                    Key: "frequency",
                    Type: "value",
                    Bindings: new[]
                    {
                        new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "{equipmentPath}.Frequency", "readWrite")
                    },
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["label"] = JsonSerializer.SerializeToElement("Frequência")
                    }),
                new VisualElementEngineeringDto(
                    Key: "fault",
                    Type: "status",
                    Bindings: new[]
                    {
                        new EngineeringBindingDto("active", EngineeringBindingKind.Tag, "{equipmentPath}.Fault", "read")
                    },
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["label"] = JsonSerializer.SerializeToElement("Falha")
                    })
            },
            Properties: new Dictionary<string, string>
            {
                ["width"] = "640",
                ["height"] = "420"
            },
            Context: new Dictionary<string, string>
            {
                ["role"] = "equipment-details"
            }));

        SecurityPolicies.UpsertRole(new SecurityRoleEngineeringDto(
            Id: Guid.Parse("46000000-0000-0000-0000-000000000001"),
            Key: "operator",
            Name: "Operator",
            Description: "Demo operator: may observe, issue operational commands and acknowledge alarms, but may not change process values/setpoints.",
            Grants: new[]
            {
                new CapabilityGrantEngineeringDto(SecurityCapability.View),
                new CapabilityGrantEngineeringDto(SecurityCapability.TagRead),
                new CapabilityGrantEngineeringDto(SecurityCapability.CommandExecute),
                new CapabilityGrantEngineeringDto(SecurityCapability.AlarmAcknowledge),
                new CapabilityGrantEngineeringDto(SecurityCapability.TrendUse)
            }));

        SecurityPolicies.UpsertRole(new SecurityRoleEngineeringDto(
            Id: Guid.Parse("46000000-0000-0000-0000-000000000002"),
            Key: "developer",
            Name: "Developer",
            Description: "Demo engineering/development role with all currently defined capabilities granted explicitly.",
            Grants: Enum.GetValues<SecurityCapability>()
                .Select(capability => new CapabilityGrantEngineeringDto(capability))
                .ToArray()));

        lock (_stateGate)
        {
            _projectKey = "demo";
            _projectName = "Demo Project";
            _baseRevision = null;
            _checkedOutAtUtc = null;
            _lastSavedAtUtc = null;
            _isDirty = false;
            _changeVersion = 0;
        }
    }

    public void Dispose()
    {
        _mutationGate.Dispose();
        Alarms.Dispose();
    }

    private sealed class MutationLease(SemaphoreSlim gate) : IAsyncDisposable
    {
        private SemaphoreSlim? _gate = gate;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _gate, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
