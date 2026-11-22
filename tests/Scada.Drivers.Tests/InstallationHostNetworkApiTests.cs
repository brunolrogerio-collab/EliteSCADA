using Scada.Api.Engineering;
using Xunit;

namespace Scada.Drivers.Tests;

public sealed class InstallationHostNetworkApiTests
{
    [Theory]
    [InlineData("access", "webPort", 18080, "apiPort", 15120)]
    [InlineData("database", "databasePort", 15432, null, 0)]
    [InlineData("media", "mediaControlPort", 9997, "mediaPlaybackPort", 8888)]
    public void ValidInstallationPlansHaveOnlyTypedPorts(
        string module, string firstName, int firstPort, string? secondName, int secondPort)
    {
        var values = new Dictionary<string, int> { [firstName] = firstPort };
        if (secondName is not null) values[secondName] = secondPort;
        var valid = new InstallationHostPortPlan(InstallationNetworkHostExecutorApi.PlanSchema, module, values);
        Assert.True(InstallationNetworkHostExecutorApi.TryValidatePlan(valid, out _));
    }

    [Fact]
    public void InvalidPlansFailClosedBeforeHostIpc()
    {
        var candidates = new[]
        {
            new InstallationHostPortPlan("unversioned", "access", new() { ["webPort"] = 18080, ["apiPort"] = 15120 }),
            new InstallationHostPortPlan(InstallationNetworkHostExecutorApi.PlanSchema, "shell", new() { ["command"] = 1 }),
            new InstallationHostPortPlan(InstallationNetworkHostExecutorApi.PlanSchema, "access", new() { ["webPort"] = 15120, ["apiPort"] = 15120 }),
            new InstallationHostPortPlan(InstallationNetworkHostExecutorApi.PlanSchema, "access", new() { ["webPort"] = 0, ["apiPort"] = 15120 }),
            new InstallationHostPortPlan(InstallationNetworkHostExecutorApi.PlanSchema, "access", new() { ["webPort"] = 18080 }),
            new InstallationHostPortPlan(InstallationNetworkHostExecutorApi.PlanSchema, "access", new() { ["webPort"] = 18080, ["apiPort"] = 15120, ["path"] = 3 }),
            new InstallationHostPortPlan(InstallationNetworkHostExecutorApi.PlanSchema, "database", new() { ["databasePort"] = 65536 }),
        };
        foreach (var candidate in candidates)
            Assert.False(InstallationNetworkHostExecutorApi.TryValidatePlan(candidate, out _));
    }
}
