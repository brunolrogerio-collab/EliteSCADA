using System.IO.Compression;
using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.ProjectPackages;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class DistributedRuntimeFoundationTests
{
    private static readonly HashSet<string> ForbiddenPackageProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "topologyMode",
        "clusterId",
        "nodeId",
        "activeNodeId",
        "standbyNodeId",
        "activeStandbyOwnership",
        "eliteGoEndpoint",
        "eliteGoEndpoints",
        "runtimeSession",
        "runtimeSessionLease",
        "sessionLease",
        "readyStandby",
        "haEpoch",
        "epoch",
        "fencing",
        "fencingToken",
        "healthState"
    };

    [Theory]
    [InlineData(SecurityCapability.CommandExecute)]
    [InlineData(SecurityCapability.ProcessValueWrite)]
    [InlineData(SecurityCapability.AlarmAcknowledge)]
    [InlineData(SecurityCapability.AlarmShelve)]
    [InlineData(SecurityCapability.TrendSave)]
    [InlineData(SecurityCapability.EngineeringView)]
    [InlineData(SecurityCapability.EngineeringModify)]
    [InlineData(SecurityCapability.UserRoleAdmin)]
    [InlineData(SecurityCapability.SystemAdmin)]
    [InlineData(SecurityCapability.HighAvailabilityObserve)]
    [InlineData(SecurityCapability.HighAvailabilityTransfer)]
    [InlineData(SecurityCapability.HighAvailabilityAdmin)]
    public void VoluntaryViewer_DirectModifiedClientCall_IsDeniedByBackendDownscope(
        SecurityCapability capability)
    {
        var now = DateTimeOffset.Parse("2026-09-06T18:00:00Z");
        var runtime = RuntimeDescriptor(now);
        var sessions = new RuntimeSessionLeaseRegistry(
            TimeSpan.FromMinutes(1),
            () => now);
        var lease = sessions.Admit(
            "privileged-user",
            "browser-1",
            RuntimeConnectionClass.Viewer,
            runtime);

        var baselineAuthority = Allowed(capability);
        Assert.True(baselineAuthority.Allowed);

        var effective = RuntimeSessionAccessEvaluator.Apply(
            sessions,
            lease.SessionId,
            "privileged-user",
            runtime,
            baselineAuthority);

        Assert.False(effective.Allowed);
        Assert.Equal(capability, effective.Capability);
    }

    [Theory]
    [InlineData(SecurityCapability.View)]
    [InlineData(SecurityCapability.TagRead)]
    [InlineData(SecurityCapability.TrendUse)]
    public void Viewer_PreservesOnlyReadOnlyRuntimeCapabilities(SecurityCapability capability)
    {
        var now = DateTimeOffset.Parse("2026-09-06T18:00:00Z");
        var runtime = RuntimeDescriptor(now);
        var sessions = new RuntimeSessionLeaseRegistry(
            TimeSpan.FromMinutes(1),
            () => now);
        var lease = sessions.Admit(
            "operator",
            "browser-1",
            RuntimeConnectionClass.Viewer,
            runtime);

        var effective = RuntimeSessionAccessEvaluator.Apply(
            sessions,
            lease.SessionId,
            "operator",
            runtime,
            Allowed(capability));

        Assert.True(effective.Allowed);
    }

    [Fact]
    public void Interactive_NeverElevatesAuthorityAndPreservesAnAllowedBaseline()
    {
        var now = DateTimeOffset.Parse("2026-09-06T18:00:00Z");
        var runtime = RuntimeDescriptor(now);
        var sessions = new RuntimeSessionLeaseRegistry(
            TimeSpan.FromMinutes(1),
            () => now);
        var lease = sessions.Admit(
            "operator",
            "browser-1",
            RuntimeConnectionClass.Interactive,
            runtime);

        var deniedBaseline = AuthorizationDecision.Denied(
            SecurityCapability.CommandExecute,
            "Authority denied command execution.");
        var stillDenied = RuntimeSessionAccessEvaluator.Apply(
            sessions,
            lease.SessionId,
            "operator",
            runtime,
            deniedBaseline);
        Assert.False(stillDenied.Allowed);
        Assert.Equal("Authority denied command execution.", stillDenied.Reason);

        var allowedBaseline = Allowed(SecurityCapability.ProcessValueWrite);
        var stillAllowed = RuntimeSessionAccessEvaluator.Apply(
            sessions,
            lease.SessionId,
            "operator",
            runtime,
            allowedBaseline);
        Assert.True(stillAllowed.Allowed);
    }

    [Fact]
    public void Lease_IsLogicalAcrossHeartbeatAndReconnect_AndFailsClosed()
    {
        var now = DateTimeOffset.Parse("2026-09-06T18:00:00Z");
        var runtime = RuntimeDescriptor(now);
        var sessions = new RuntimeSessionLeaseRegistry(
            TimeSpan.FromSeconds(60),
            () => now);
        var lease = sessions.Admit(
            "operator",
            "elitego-instance-1",
            RuntimeConnectionClass.Viewer,
            runtime);

        Assert.Equal("operator", lease.UserId);
        Assert.Equal("elitego-instance-1", lease.ClientInstanceId);
        Assert.Null(lease.ServerNode);
        Assert.Null(lease.ClusterId);

        // A reconnect reuses the logical lease. No transport connection identity is stored here.
        var reconnect = sessions.Validate(
            lease.SessionId,
            "operator",
            runtime,
            "elitego-instance-1");
        Assert.True(reconnect.IsValid);
        Assert.Equal(lease.SessionId, reconnect.Lease!.SessionId);

        now = now.AddSeconds(45);
        var heartbeat = sessions.Heartbeat(
            lease.SessionId,
            "operator",
            "elitego-instance-1",
            runtime);
        Assert.True(heartbeat.IsValid);
        Assert.Equal(lease.SessionId, heartbeat.Lease!.SessionId);
        Assert.True(heartbeat.Lease.ExpiresAtUtc > lease.ExpiresAtUtc);

        var wrongClient = sessions.Validate(
            lease.SessionId,
            "operator",
            runtime,
            "different-client");
        Assert.False(wrongClient.IsValid);
        Assert.Equal("session-client-mismatch", wrongClient.FailureCode);

        now = now.AddSeconds(61);
        var expired = sessions.Validate(
            lease.SessionId,
            "operator",
            runtime,
            "elitego-instance-1");
        Assert.False(expired.IsValid);
        Assert.Equal("session-expired", expired.FailureCode);
    }

    [Fact]
    public void Lease_IsInvalidatedWhenBackendActiveRevisionChanges()
    {
        var now = DateTimeOffset.Parse("2026-09-06T18:00:00Z");
        var runtime = RuntimeDescriptor(now, revision: 7);
        var sessions = new RuntimeSessionLeaseRegistry(
            TimeSpan.FromMinutes(1),
            () => now);
        var lease = sessions.Admit(
            "operator",
            "browser-1",
            RuntimeConnectionClass.Interactive,
            runtime);

        var nextActiveRevision = RuntimeDescriptor(
            now.AddSeconds(1),
            revision: 8);
        var validation = sessions.Validate(
            lease.SessionId,
            "operator",
            nextActiveRevision,
            "browser-1");

        Assert.False(validation.IsValid);
        Assert.Equal("runtime-changed", validation.FailureCode);
    }

    [Fact]
    public void Escadapkg_RemainsTopologyNeutralAndContainsNoRuntimeLeaseState()
    {
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var packages = new ProjectPackageService(exchange);

        var bytes = packages.Export("portable-demo", "Portable Demo");
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.Contains(archive.Entries, entry => entry.FullName == ProjectPackageService.ManifestPath);
        Assert.Contains(archive.Entries, entry => entry.FullName == ProjectPackageService.EngineeringPath);
        Assert.DoesNotContain(
            archive.Entries,
            entry => entry.FullName.Contains("deployment", StringComparison.OrdinalIgnoreCase) ||
                     entry.FullName.Contains("topology", StringComparison.OrdinalIgnoreCase) ||
                     entry.FullName.Contains("session", StringComparison.OrdinalIgnoreCase) ||
                     entry.FullName.Contains("cluster", StringComparison.OrdinalIgnoreCase));

        AssertArchiveJsonHasNoForbiddenProperties(archive, ProjectPackageService.ManifestPath);
        AssertArchiveJsonHasNoForbiddenProperties(archive, ProjectPackageService.EngineeringPath);

        Assert.DoesNotContain(
            typeof(ProjectPackageManifest).GetProperties(),
            property => ForbiddenPackageProperties.Contains(property.Name));
        Assert.DoesNotContain(
            typeof(EngineeringPackage).GetProperties(),
            property => ForbiddenPackageProperties.Contains(property.Name));
    }

    private static AuthorizationDecision Allowed(SecurityCapability capability) =>
        new(true, capability, "Authority granted capability.", new[] { "operator" });

    private static ScadaRuntimeDescriptor RuntimeDescriptor(
        DateTimeOffset activatedAtUtc,
        long revision = 7) =>
        new(
            "engineering",
            "portable-demo",
            revision,
            activatedAtUtc,
            [],
            [],
            0,
            0);

    private static void AssertArchiveJsonHasNoForbiddenProperties(
        ZipArchive archive,
        string path)
    {
        var entry = Assert.Single(archive.Entries, candidate => candidate.FullName == path);
        using var stream = entry.Open();
        using var document = JsonDocument.Parse(stream);
        AssertNoForbiddenProperties(document.RootElement);
    }

    private static void AssertNoForbiddenProperties(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Assert.DoesNotContain(property.Name, ForbiddenPackageProperties);
                    AssertNoForbiddenProperties(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    AssertNoForbiddenProperties(item);
                break;
        }
    }
}
