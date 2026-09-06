using Microsoft.AspNetCore.Http;
using Scada.Api.Security;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Security;

namespace Scada.Drivers.Tests;

public sealed class EngineeringLockAccessTests
{
    [Fact]
    public void Replace_ChangesOnlyCanonicalLockState()
    {
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var secretService = new EngineeringLockSecretService();
        var baselineLock = secretService.Configure("baseline-secret", locked: false);
        var tagId = Guid.NewGuid();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(tagId, "Value", "Plant.Value", TagDataType.Double)],
            Array.Empty<AlarmEngineeringDto>(),
            EngineeringLock: baselineLock);

        var initialApply = exchange.Apply(package, ImportMode.CreateAndUpdate);
        Assert.DoesNotContain(initialApply.Issues, issue => issue.IsError);
        Assert.Single(exchange.ExportPackage().Tags);

        var replacement = secretService.Lock(baselineLock);
        EngineeringLockAccess.Replace(exchange, replacement);

        var current = exchange.ExportPackage();
        Assert.Single(current.Tags);
        Assert.Equal(tagId, current.Tags.Single().Id);
        var currentLock = EngineeringLockContract.Normalize(current.EngineeringLock);
        Assert.True(currentLock.Locked);
        Assert.Equal(replacement.Verifier, currentLock.Verifier);
    }

    [Fact]
    public void ProtectedEngineeringFailure_IsForbiddenOnlyWhenLocked()
    {
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var secretService = new EngineeringLockSecretService();

        Assert.Null(EngineeringLockAccess.ProtectedEngineeringFailure(exchange));

        EngineeringLockAccess.Replace(
            exchange,
            secretService.Configure("locked-secret", locked: true));

        var failure = Assert.IsAssignableFrom<IStatusCodeHttpResult>(
            EngineeringLockAccess.ProtectedEngineeringFailure(exchange));
        Assert.Equal(StatusCodes.Status403Forbidden, failure.StatusCode);
    }

    [Theory]
    [InlineData("/api/project-package/export")]
    [InlineData("/api/project-package/inspect")]
    [InlineData("/api/project-package/import/preview")]
    [InlineData("/api/project-package/import/apply")]
    [InlineData("/api/engineering/export/json")]
    [InlineData("/api/engineering/import/json/preview")]
    [InlineData("/api/engineering/import/json/apply")]
    [InlineData("/api/engineering/lock/status")]
    [InlineData("/api/engineering/lock/unlock")]
    public void WorkspaceReadExemptions_AreRestrictedToRecoveryExportImportAndLock(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        Assert.True(EngineeringLockAccess.IsWorkspaceReadExempt(context.Request));
    }

    [Theory]
    [InlineData("/api/engineering/workspace")]
    [InlineData("/api/engineering/tags")]
    [InlineData("/api/engineering/screens")]
    [InlineData("/api/engineering/scripts")]
    [InlineData("/api/engineering/bulk/preview")]
    public void ProtectedWorkspaceReads_AreNotExempt(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        Assert.False(EngineeringLockAccess.IsWorkspaceReadExempt(context.Request));
    }

    [Theory]
    [InlineData("/api/engineering/persistence/status")]
    [InlineData("/api/engineering/persistence/projects/first")]
    [InlineData("/api/engineering/persistence/plant-a/revisions/1/checkout")]
    [InlineData("/api/engineering/persistence/plant-a/latest/preview")]
    [InlineData("/api/engineering/persistence/plant-a/latest/apply")]
    public void PersistenceRecoveryPaths_RemainExempt(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        Assert.True(EngineeringLockAccess.IsPersistenceRecoveryExempt(context.Request));
    }

    [Theory]
    [InlineData("/api/engineering/persistence/plant-a/save")]
    [InlineData("/api/engineering/persistence/plant-a/revisions/1/publish")]
    [InlineData("/api/engineering/persistence/plant-a/published/activate")]
    [InlineData("/api/engineering/persistence/plant-a/lifecycle")]
    public void ProtectedPersistencePaths_AreNotExempt(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        Assert.False(EngineeringLockAccess.IsPersistenceRecoveryExempt(context.Request));
    }
}
