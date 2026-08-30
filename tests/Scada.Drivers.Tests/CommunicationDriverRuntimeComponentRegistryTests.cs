using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class CommunicationDriverRuntimeComponentRegistryTests
{
    [Fact]
    public void Registration_RequiresPlannerAndFactoryForSameDriverType()
    {
        var registration = new CommunicationDriverRuntimeComponentRegistration(
            new FakePlanner("bacnet.ip"),
            new FakeFactory("mqtt.industrial"));

        var error = Assert.Throws<InvalidOperationException>(registration.Validate);
        Assert.True(error.Message.Contains("does not match", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Registry_RejectsDuplicateDriverTypeCaseInsensitively()
    {
        var registry = new CommunicationDriverRuntimeComponentRegistry();
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new FakePlanner("opcua.client"),
            new FakeFactory("opcua.client")));

        var error = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new CommunicationDriverRuntimeComponentRegistration(
                new FakePlanner("OPCUA.CLIENT"),
                new FakeFactory("OPCUA.CLIENT"))));

        Assert.True(error.Message.Contains("already registered", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakePlanner : ICommunicationDriverRuntimePlanner
    {
        public FakePlanner(string driverType)
        {
            DriverType = driverType;
        }

        public string DriverType { get; }

        public CommunicationDriverRuntimePlanningResult Plan(
            EngineeringPackage package,
            DataSourceEngineeringDto dataSource)
            => throw new NotSupportedException();
    }

    private sealed class FakeFactory : ICommunicationDriverRuntimeFactory
    {
        public FakeFactory(string driverType)
        {
            DriverType = driverType;
        }

        public string DriverType { get; }

        public ICommunicationDriver Create(
            ICommunicationDriverRuntimePlan plan,
            CommunicationDriverRuntimeServices services)
            => throw new NotSupportedException();
    }
}
