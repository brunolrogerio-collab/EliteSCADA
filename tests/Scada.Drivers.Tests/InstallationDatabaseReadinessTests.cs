using Scada.Api.Engineering;
using Scada.Api.Persistence;

namespace Scada.Drivers.Tests;

public sealed class InstallationDatabaseReadinessTests
{
    [Fact]
    public void Readiness_RequiresHealthySqlAndNoPendingRecoveryOrRestart()
    {
        var health = new DatabaseConnectionHealth(true, "18", 18, null, true, true, DateTimeOffset.UtcNow);
        var status = new DatabaseTopologyStatus(new(DatabaseTopologyMode.LocalManaged, null, true, null),
            null, null, null, false, false, health, null, DateTimeOffset.UtcNow, null);
        Assert.True(InstallationDatabaseReadinessApi.Project(status).Ready);
        Assert.False(InstallationDatabaseReadinessApi.Project(status with { PrimaryHealth = health with { Reachable = false } }).Ready);
        Assert.False(InstallationDatabaseReadinessApi.Project(status with { PrimaryHealth = health with { SchemaCompatible = false } }).Ready);
        Assert.False(InstallationDatabaseReadinessApi.Project(status with { RestartRequired = true }).Ready);
        Assert.False(InstallationDatabaseReadinessApi.Project(status with { RecoveryRequired = true }).Ready);
        Assert.False(InstallationDatabaseReadinessApi.Project(status with { PendingOperationId = Guid.NewGuid() }).Ready);
        Assert.DoesNotContain("Password", System.Text.Json.JsonSerializer.Serialize(InstallationDatabaseReadinessApi.Project(status)));
    }
}
