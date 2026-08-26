using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Views;

namespace Scada.Api.Runtime;

public sealed record EngineeringWorkspaceDescriptor(
    string? ProjectKey,
    string? ProjectName,
    long? BaseRevision,
    DateTimeOffset? CheckedOutAtUtc,
    int TagCount,
    int AlarmCount,
    int DataSourceCount,
    int TemplateCount,
    int EquipmentCount,
    int DynamoCount,
    int ScreenCount,
    int PopupCount);

public sealed class EngineeringWorkspace : IDisposable
{
    private readonly InMemoryScadaEventBus _eventBus = new();
    private readonly object _stateGate = new();
    private string? _projectKey;
    private string? _projectName;
    private long? _baseRevision;
    private DateTimeOffset? _checkedOutAtUtc;

    public EngineeringWorkspace()
    {
        Tags = new InMemoryTagRegistry();
        Alarms = new InMemoryAlarmEngine(_eventBus);
        DataSources = new InMemoryDataSourceEngineeringRegistry();
        Assets = new InMemoryEngineeringAssetRegistry();
        Views = new InMemoryEngineeringViewRegistry();
        SeedDemo();
    }

    public InMemoryTagRegistry Tags { get; }
    public InMemoryAlarmEngine Alarms { get; }
    public InMemoryDataSourceEngineeringRegistry DataSources { get; }
    public InMemoryEngineeringAssetRegistry Assets { get; }
    public InMemoryEngineeringViewRegistry Views { get; }

    public EngineeringWorkspaceDescriptor Describe()
    {
        lock (_stateGate)
        {
            return new EngineeringWorkspaceDescriptor(
                _projectKey,
                _projectName,
                _baseRevision,
                _checkedOutAtUtc,
                Tags.Snapshot().Count,
                Alarms.Definitions().Count,
                DataSources.Snapshot().Count,
                Assets.SnapshotTemplates().Count,
                Assets.SnapshotEquipment().Count,
                Assets.SnapshotDynamos().Count,
                Views.SnapshotScreens().Count,
                Views.SnapshotPopups().Count);
        }
    }

    public void SetCheckout(string projectKey, string projectName, long revision)
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
        }
    }

    public void Clear()
    {
        Alarms.Clear();
        Tags.Clear();
        DataSources.Clear();
        Assets.Clear();
        Views.Clear();
    }

    private void SeedDemo()
    {
        foreach (var tag in DemoProcessModel.CreateTagDefinitions())
            Tags.Register(tag);

        foreach (var alarm in DemoProcessModel.CreateAlarmDefinitions())
            Alarms.Register(alarm);

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
                    Properties: new Dictionary<string, string>
                    {
                        ["label"] = "Reservatório TK01",
                        ["x"] = "100",
                        ["y"] = "100"
                    }),
                new VisualElementEngineeringDto(
                    Key: "pump01",
                    Type: "dynamo",
                    DynamoKey: "dynamo.pump.standard",
                    EquipmentPath: "Demo.P01",
                    Properties: new Dictionary<string, string>
                    {
                        ["x"] = "430",
                        ["y"] = "160"
                    }),
                new VisualElementEngineeringDto(
                    Key: "pressure",
                    Type: "value",
                    Bindings: new[]
                    {
                        new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "Demo.Discharge.Pressure", "read")
                    },
                    Properties: new Dictionary<string, string>
                    {
                        ["label"] = "Pressão"
                    }),
                new VisualElementEngineeringDto(
                    Key: "flow",
                    Type: "value",
                    Bindings: new[]
                    {
                        new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "Demo.Discharge.Flow", "read")
                    },
                    Properties: new Dictionary<string, string>
                    {
                        ["label"] = "Vazão"
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
                    Properties: new Dictionary<string, string> { ["label"] = "Corrente" }),
                new VisualElementEngineeringDto(
                    Key: "frequency",
                    Type: "value",
                    Bindings: new[]
                    {
                        new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "{equipmentPath}.Frequency", "readWrite")
                    },
                    Properties: new Dictionary<string, string> { ["label"] = "Frequência" }),
                new VisualElementEngineeringDto(
                    Key: "fault",
                    Type: "status",
                    Bindings: new[]
                    {
                        new EngineeringBindingDto("active", EngineeringBindingKind.Tag, "{equipmentPath}.Fault", "read")
                    },
                    Properties: new Dictionary<string, string> { ["label"] = "Falha" })
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

        lock (_stateGate)
        {
            _projectKey = "demo";
            _projectName = "Demo Project";
            _baseRevision = null;
            _checkedOutAtUtc = null;
        }
    }

    public void Dispose() => Alarms.Dispose();
}
