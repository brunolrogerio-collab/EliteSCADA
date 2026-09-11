using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class DriverConvergenceSharedContractsTests
{
    [Fact]
    public void ModuleRegistry_RejectsDuplicateDriverTypeCaseInsensitively()
    {
        var registry = new CommunicationDriverModuleRegistry();
        registry.Register(new CommunicationDriverModuleRegistration(new DescriptorProvider("mqtt.raw")));

        var error = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new CommunicationDriverModuleRegistration(new DescriptorProvider("MQTT.RAW"))));

        Assert.Contains("already registered", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModuleRegistration_RejectsAdvertisedEngineeringCapabilityWithoutProvider()
    {
        var registration = new CommunicationDriverModuleRegistration(
            new DescriptorProvider("opcua", DriverEngineeringCapabilities.ConnectionTest));

        var error = Assert.Throws<InvalidOperationException>(registration.Validate);

        Assert.Contains(nameof(CommunicationDriverModuleRegistration.ConnectionTester), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Readiness_IsReadyOnlyForExplicitReadyState()
    {
        var ready = new CommunicationDriverReadinessSnapshot(
            "source-1",
            "mqtt.raw",
            CommunicationDriverReadinessState.Ready,
            DateTimeOffset.UtcNow);
        var starting = ready with { State = CommunicationDriverReadinessState.Starting };
        var faulted = ready with { State = CommunicationDriverReadinessState.Faulted };

        Assert.True(ready.IsReady);
        Assert.False(starting.IsReady);
        Assert.False(faulted.IsReady);
    }

    [Fact]
    public void ProtectedMaterialRequest_RequiresScopedTrimmedTokens()
    {
        var valid = new CommunicationDriverProtectedMaterialRequest(
            "plant-a",
            "broker-primary",
            "mqtt.raw",
            "mqtt.password",
            "secret://mqtt/primary");

        valid.Validate();

        var invalid = valid with { Reference = " secret://mqtt/primary" };
        Assert.Throws<ArgumentException>(invalid.Validate);

        invalid = valid with { Purpose = "mqtt.password\nleak" };
        Assert.Throws<ArgumentException>(invalid.Validate);
    }

    [Fact]
    public void RuntimeComponentRegistry_RejectsPlannerFactoryDescriptorMismatchAndDuplicates()
    {
        var mismatch = new CommunicationDriverRuntimeComponentRegistration(
            new StubPlanner("mqtt.raw"),
            new StubFactory("opcua"),
            Descriptor("mqtt.raw"));

        Assert.Throws<InvalidOperationException>(mismatch.Validate);

        var descriptorMismatch = new CommunicationDriverRuntimeComponentRegistration(
            new StubPlanner("mqtt.raw"),
            new StubFactory("MQTT.RAW"),
            Descriptor("opcua"));

        Assert.Throws<InvalidOperationException>(descriptorMismatch.Validate);

        var registry = new CommunicationDriverRuntimeComponentRegistry();
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new StubPlanner("mqtt.raw"),
            new StubFactory("MQTT.RAW"),
            Descriptor("mqtt.raw")));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new CommunicationDriverRuntimeComponentRegistration(
                new StubPlanner("MQTT.RAW"),
                new StubFactory("mqtt.raw"),
                Descriptor("MQTT.RAW"))));
    }

    [Fact]
    public void RuntimePlanningResult_FailsClosedOnEngineeringErrors()
    {
        var plan = new StubPlan("mqtt.raw");
        var warningOnly = new CommunicationDriverRuntimePlanningResult(
            plan,
            [new EngineeringDriverIssue("WARN", "warning", "source-1", IsError: false)]);
        var withError = new CommunicationDriverRuntimePlanningResult(
            plan,
            [new EngineeringDriverIssue("ERR", "error", "source-1", IsError: true)]);

        Assert.True(warningOnly.CanActivate);
        Assert.False(withError.CanActivate);
        Assert.False(new CommunicationDriverRuntimePlanningResult(null, []).CanActivate);
    }

    private static CommunicationDriverTypeDescriptor Descriptor(string driverType) =>
        new DescriptorProvider(driverType).Descriptor;

    private sealed class DescriptorProvider : ICommunicationDriverDescriptorProvider
    {
        public DescriptorProvider(
            string driverType,
            DriverEngineeringCapabilities engineeringCapabilities = DriverEngineeringCapabilities.None)
        {
            Descriptor = new CommunicationDriverTypeDescriptor(
                driverType,
                driverType,
                DriverContractVersion: 1,
                RuntimeCapabilities: DriverCapabilities.Read,
                EngineeringCapabilities: engineeringCapabilities,
                AcquisitionModes: [DriverAcquisitionMode.Polling],
                ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
                    $"elitescada.driver.{driverType.ToLowerInvariant()}",
                    1,
                    [],
                    []));
        }

        public CommunicationDriverTypeDescriptor Descriptor { get; }
    }

    private sealed class StubPlan(string driverType) : ICommunicationDriverRuntimePlan
    {
        public string DataSourceKey => "source-1";
        public string Name => "Source 1";
        public string DriverType { get; } = driverType;
        public IReadOnlyCollection<Scada.Core.Tags.TagDefinition> Tags => [];
    }

    private sealed class StubPlanner(string driverType) : ICommunicationDriverRuntimePlanner
    {
        public string DriverType { get; } = driverType;

        public CommunicationDriverRuntimePlanningResult Plan(
            EngineeringPackage package,
            DataSourceEngineeringDto dataSource) =>
            throw new NotSupportedException();
    }

    private sealed class StubFactory(string driverType) : ICommunicationDriverRuntimeFactory
    {
        public string DriverType { get; } = driverType;

        public ICommunicationDriver Create(
            ICommunicationDriverRuntimePlan plan,
            CommunicationDriverRuntimeServices services) =>
            throw new NotSupportedException();
    }
}
