using System.IO.Compression;
using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Product.Licensing;
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
        "license",
        "licenseCode",
        "licenseKey",
        "privateKey",
        "signingKey",
        "machineFingerprint",
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
    public async Task VoluntaryViewer_DirectModifiedClientCall_IsDeniedByBackendDownscope(
        SecurityCapability capability)
    {
        var now = DateTimeOffset.Parse("2026-09-06T18:00:00Z");
        var runtime = RuntimeDescriptor(now);
        var sessions = new RuntimeSessionLeaseRegistry(
            TimeSpan.FromMinutes(1),
            () => now);
        var lease = await sessions.AdmitAsync(
            "privileged-user",
            "browser-1",
            RuntimeConnectionClass.Viewer,
            runtime);

        var baselineAuthority = Allowed(capability);
        Assert.True(baselineAuthority.Allowed);

        var effective = await RuntimeSessionAccessEvaluator.ApplyAsync(
            sessions,
            lease.SessionId,
            "privileged-user",
            runtime,
            baselineAuthority);

        Assert.False(effective.Allowed);
        Assert.Equal(capability, effective.Capability);
    }

    [Fact]
    public void Admission_ExplicitViewOnly_RemainsViewOnly_ForBroadAuthority()
    {
        var decision = RuntimeSessionAdmissionPolicy.Resolve(
            RuntimeConnectionClass.ViewOnly,
            new[]
            {
                Allowed(SecurityCapability.CommandExecute),
                Allowed(SecurityCapability.ProcessValueWrite)
            });

        Assert.Equal(RuntimeConnectionClass.ViewOnly, decision.RequestedClass);
        Assert.Equal(RuntimeConnectionClass.ViewOnly, decision.GrantedClass);
        Assert.Equal(RuntimeSessionAdmissionReasonCode.ExplicitViewOnly, decision.ReasonCode);
        Assert.False(decision.RequiresCapacityReservation);
    }

    [Fact]
    public void Admission_Interactive_ReadOnlyAuthority_IsDownscopedWithoutRoleNamePolicy()
    {
        var decision = RuntimeSessionAdmissionPolicy.Resolve(
            RuntimeConnectionClass.Interactive,
            new[]
            {
                new AuthorizationDecision(true, SecurityCapability.View, "read is allowed", new[] { "arbitrary-profile" }),
                new AuthorizationDecision(false, SecurityCapability.CommandExecute, "denied", Array.Empty<string>()),
                new AuthorizationDecision(false, SecurityCapability.ProcessValueWrite, "denied", Array.Empty<string>())
            });

        Assert.Equal(RuntimeConnectionClass.ViewOnly, decision.GrantedClass);
        Assert.Equal(RuntimeSessionAdmissionReasonCode.AuthorityReadOnly, decision.ReasonCode);
    }

    [Fact]
    public void Admission_Interactive_UsesCapabilityNotRoleName_AndDoesNotGrantOtherCapability()
    {
        var decision = RuntimeSessionAdmissionPolicy.Resolve(
            RuntimeConnectionClass.Interactive,
            new[]
            {
                new AuthorizationDecision(true, SecurityCapability.CommandExecute, "command is allowed", new[] { "plant-shift" }),
                new AuthorizationDecision(false, SecurityCapability.ProcessValueWrite, "write is denied", Array.Empty<string>())
            });

        Assert.Equal(RuntimeConnectionClass.Interactive, decision.GrantedClass);
        Assert.Equal(RuntimeSessionAdmissionReasonCode.InteractiveEligible, decision.ReasonCode);

        var writeBaseline = AuthorizationDecision.Denied(
            SecurityCapability.ProcessValueWrite,
            "Authority still denies process write.");
        var effective = RuntimeSessionCapabilityProjection.Apply(decision.GrantedClass, writeBaseline);
        Assert.False(effective.Allowed);
        Assert.Equal("Authority still denies process write.", effective.Reason);
    }

    [Fact]
    public async Task Admission_ReconnectDownscopesExplicitViewOnly_AndNeverUpgradesByChangedRequest()
    {
        var now = DateTimeOffset.Parse("2026-09-17T00:00:00Z");
        var runtime = RuntimeDescriptor(now);
        var sessions = new RuntimeSessionLeaseRegistry(TimeSpan.FromMinutes(1), () => now);

        var interactive = await sessions.AdmitAsync(
            "operator",
            "browser-1",
            RuntimeConnectionClass.Interactive,
            runtime);
        var explicitViewOnly = await sessions.AdmitAsync(
            "operator",
            "browser-1",
            RuntimeConnectionClass.ViewOnly,
            runtime);

        Assert.Equal(interactive.SessionId, explicitViewOnly.SessionId);
        Assert.Equal(RuntimeConnectionClass.ViewOnly, explicitViewOnly.ConnectionClass);
        Assert.Equal(interactive.Generation + 1, explicitViewOnly.Generation);

        var changedBackToInteractive = await sessions.AdmitAsync(
            "operator",
            "browser-1",
            RuntimeConnectionClass.Interactive,
            runtime);
        Assert.Equal(explicitViewOnly.SessionId, changedBackToInteractive.SessionId);
        Assert.Equal(RuntimeConnectionClass.ViewOnly, changedBackToInteractive.ConnectionClass);
        Assert.Equal(explicitViewOnly.Generation, changedBackToInteractive.Generation);
    }

    [Theory]
    [InlineData(SecurityCapability.View)]
    [InlineData(SecurityCapability.TagRead)]
    [InlineData(SecurityCapability.TrendUse)]
    public async Task Viewer_PreservesOnlyReadOnlyRuntimeCapabilities(SecurityCapability capability)
    {
        var now = DateTimeOffset.Parse("2026-09-06T18:00:00Z");
        var runtime = RuntimeDescriptor(now);
        var sessions = new RuntimeSessionLeaseRegistry(
            TimeSpan.FromMinutes(1),
            () => now);
        var lease = await sessions.AdmitAsync(
            "operator",
            "browser-1",
            RuntimeConnectionClass.Viewer,
            runtime);

        var effective = await RuntimeSessionAccessEvaluator.ApplyAsync(
            sessions,
            lease.SessionId,
            "operator",
            runtime,
            Allowed(capability));

        Assert.True(effective.Allowed);
    }

    [Fact]
    public async Task Interactive_NeverElevatesAuthorityAndPreservesAnAllowedBaseline()
    {
        var now = DateTimeOffset.Parse("2026-09-06T18:00:00Z");
        var runtime = RuntimeDescriptor(now);
        var sessions = new RuntimeSessionLeaseRegistry(
            TimeSpan.FromMinutes(1),
            () => now);
        var lease = await sessions.AdmitAsync(
            "operator",
            "browser-1",
            RuntimeConnectionClass.Interactive,
            runtime);

        var deniedBaseline = AuthorizationDecision.Denied(
            SecurityCapability.CommandExecute,
            "Authority denied command execution.");
        var stillDenied = await RuntimeSessionAccessEvaluator.ApplyAsync(
            sessions,
            lease.SessionId,
            "operator",
            runtime,
            deniedBaseline);
        Assert.False(stillDenied.Allowed);
        Assert.Equal("Authority denied command execution.", stillDenied.Reason);

        var allowedBaseline = Allowed(SecurityCapability.ProcessValueWrite);
        var stillAllowed = await RuntimeSessionAccessEvaluator.ApplyAsync(
            sessions,
            lease.SessionId,
            "operator",
            runtime,
            allowedBaseline);
        Assert.True(stillAllowed.Allowed);
    }

    [Fact]
    public async Task Lease_IsLogicalAcrossHeartbeatAndReconnect_AndFailsClosed()
    {
        var now = DateTimeOffset.Parse("2026-09-06T18:00:00Z");
        var runtime = RuntimeDescriptor(now);
        var sessions = new RuntimeSessionLeaseRegistry(
            TimeSpan.FromSeconds(60),
            () => now);
        var lease = await sessions.AdmitAsync(
            "operator",
            "elitego-instance-1",
            RuntimeConnectionClass.Viewer,
            runtime);

        Assert.Equal("operator", lease.UserId);
        Assert.Equal("elitego-instance-1", lease.ClientInstanceId);
        Assert.Null(lease.ServerNode);
        Assert.Null(lease.ClusterId);

        // A reconnect reuses the logical lease. No transport connection identity is stored here.
        var reconnect = await sessions.ValidateAsync(
            lease.SessionId,
            "operator",
            runtime,
            "elitego-instance-1");
        Assert.True(reconnect.IsValid);
        Assert.Equal(lease.SessionId, reconnect.Lease!.SessionId);

        now = now.AddSeconds(45);
        var heartbeat = await sessions.HeartbeatAsync(
            lease.SessionId,
            "operator",
            "elitego-instance-1",
            runtime);
        Assert.True(heartbeat.IsValid);
        Assert.Equal(lease.SessionId, heartbeat.Lease!.SessionId);
        Assert.True(heartbeat.Lease.ExpiresAtUtc > lease.ExpiresAtUtc);

        var wrongClient = await sessions.ValidateAsync(
            lease.SessionId,
            "operator",
            runtime,
            "different-client");
        Assert.False(wrongClient.IsValid);
        Assert.Equal("session-client-mismatch", wrongClient.FailureCode);

        now = now.AddSeconds(61);
        var expired = await sessions.ValidateAsync(
            lease.SessionId,
            "operator",
            runtime,
            "elitego-instance-1");
        Assert.False(expired.IsValid);
        Assert.Equal("session-expired", expired.FailureCode);
    }

    [Fact]
    public async Task Lease_IsInvalidatedWhenBackendActiveRevisionChanges()
    {
        var now = DateTimeOffset.Parse("2026-09-06T18:00:00Z");
        var runtime = RuntimeDescriptor(now, revision: 7);
        var sessions = new RuntimeSessionLeaseRegistry(
            TimeSpan.FromMinutes(1),
            () => now);
        var lease = await sessions.AdmitAsync(
            "operator",
            "browser-1",
            RuntimeConnectionClass.Interactive,
            runtime);

        var nextActiveRevision = RuntimeDescriptor(
            now.AddSeconds(1),
            revision: 8);
        var validation = await sessions.ValidateAsync(
            lease.SessionId,
            "operator",
            nextActiveRevision,
            "browser-1");

        Assert.False(validation.IsValid);
        Assert.Equal("runtime-changed", validation.FailureCode);
    }

    [Fact]
    public void SeatCapacityPolicy_UsesDemoAndSignedTotals_AndRejectsLegacyOrInvalidInstalledLicenses()
    {
        var demo = RuntimeSessionSeatCapacityPolicy.Resolve(LicenseVerificationResult.Demo());
        Assert.True(demo.IsAvailable);
        Assert.Equal(2, demo.Capacity!.InteractiveSeats);
        Assert.Equal(2, demo.Capacity.ViewOnlySeats);
        Assert.Equal(RuntimeSessionCapacityReasonCode.DemoCapacity, demo.ReasonCode);

        var signed = RuntimeSessionSeatCapacityPolicy.Resolve(new LicenseVerificationResult(
            LicenseState.Valid,
            SessionEntitlements: new MachineLicenseV2Entitlements(ViewOnlySeats: 0, InteractiveSeats: 0, HaRuntime: false)));
        Assert.True(signed.IsAvailable);
        Assert.Equal(0, signed.Capacity!.InteractiveSeats);
        Assert.Equal(0, signed.Capacity.ViewOnlySeats);
        Assert.Equal(RuntimeSessionCapacityReasonCode.Eslic2SignedEntitlements, signed.ReasonCode);

        var legacy = RuntimeSessionSeatCapacityPolicy.Resolve(new LicenseVerificationResult(LicenseState.Valid));
        Assert.False(legacy.IsAvailable);
        Assert.Equal(RuntimeSessionCapacityReasonCode.LegacySessionEntitlementUnsupported, legacy.ReasonCode);

        var invalid = RuntimeSessionSeatCapacityPolicy.Resolve(LicenseVerificationResult.Invalid("signature failed"));
        Assert.False(invalid.IsAvailable);
        Assert.Equal(RuntimeSessionCapacityReasonCode.InvalidInstalledLicense, invalid.ReasonCode);
    }

    [Fact]
    public async Task SeatCapacity_DemoPool_ReservesTwoInteractiveAndTwoViewOnly_WithoutCrossPoolStealing()
    {
        var now = DateTimeOffset.Parse("2026-09-17T00:00:00Z");
        var runtime = RuntimeDescriptor(now);
        var sessions = new RuntimeSessionLeaseRegistry(TimeSpan.FromMinutes(1), () => now);
        var demo = new RuntimeSessionSeatCapacity(InteractiveSeats: 2, ViewOnlySeats: 2);

        var interactiveOne = await AdmitWithCapacity(sessions, "operator-1", "browser-1", RuntimeConnectionClass.Interactive, runtime, demo);
        var interactiveTwo = await AdmitWithCapacity(sessions, "operator-2", "browser-2", RuntimeConnectionClass.Interactive, runtime, demo);
        var interactiveFallback = await AdmitWithCapacity(sessions, "operator-3", "browser-3", RuntimeConnectionClass.Interactive, runtime, demo);
        var explicitViewOnly = await AdmitWithCapacity(sessions, "viewer-1", "browser-4", RuntimeConnectionClass.ViewOnly, runtime, demo);
        var rejectedViewOnly = await AdmitWithCapacity(sessions, "viewer-2", "browser-5", RuntimeConnectionClass.ViewOnly, runtime, demo);
        var rejectedInteractive = await AdmitWithCapacity(sessions, "operator-4", "browser-6", RuntimeConnectionClass.Interactive, runtime, demo);

        Assert.Equal(RuntimeConnectionClass.Interactive, interactiveOne.Lease!.ConnectionClass);
        Assert.Equal(RuntimeConnectionClass.Interactive, interactiveTwo.Lease!.ConnectionClass);
        Assert.Equal(RuntimeSessionSeatReservationReasonCode.InteractiveQuotaFallbackViewOnly, interactiveFallback.ReasonCode);
        Assert.Equal(RuntimeConnectionClass.ViewOnly, interactiveFallback.Lease!.ConnectionClass);
        Assert.Equal(RuntimeSessionSeatReservationReasonCode.ViewOnlyReserved, explicitViewOnly.ReasonCode);
        Assert.False(rejectedViewOnly.IsAdmitted);
        Assert.Equal(RuntimeSessionSeatReservationReasonCode.ViewOnlyQuotaExhausted, rejectedViewOnly.ReasonCode);
        Assert.False(rejectedInteractive.IsAdmitted);
        Assert.Equal(RuntimeSessionSeatReservationReasonCode.EligiblePoolsExhausted, rejectedInteractive.ReasonCode);
    }

    [Fact]
    public async Task SeatCapacity_ReusesOneLogicalLease_AndFailsClosedWhenAuthorityDownscopeCannotReserveViewOnly()
    {
        var now = DateTimeOffset.Parse("2026-09-17T00:00:00Z");
        var runtime = RuntimeDescriptor(now);
        var sessions = new RuntimeSessionLeaseRegistry(TimeSpan.FromMinutes(1), () => now);
        var capacity = new RuntimeSessionSeatCapacity(InteractiveSeats: 1, ViewOnlySeats: 1);

        _ = await AdmitWithCapacity(sessions, "viewer", "viewer-client", RuntimeConnectionClass.ViewOnly, runtime, capacity);
        var interactive = await AdmitWithCapacity(sessions, "operator", "operator-client", RuntimeConnectionClass.Interactive, runtime, capacity);
        var downscope = await AdmitWithCapacity(sessions, "operator", "operator-client", RuntimeConnectionClass.ViewOnly, runtime, capacity);

        Assert.True(interactive.IsAdmitted);
        Assert.False(downscope.IsAdmitted);
        Assert.Equal(RuntimeSessionSeatReservationReasonCode.ViewOnlyQuotaExhausted, downscope.ReasonCode);
        var stale = await sessions.ValidateAsync(interactive.Lease!.SessionId, "operator", runtime, "operator-client");
        Assert.False(stale.IsValid);

        var singleSeat = new RuntimeSessionSeatCapacity(InteractiveSeats: 1, ViewOnlySeats: 0);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ =>
            AdmitWithCapacity(sessions, "same-user", "same-client", RuntimeConnectionClass.Interactive, RuntimeDescriptor(now, revision: 8), singleSeat)));
        Assert.All(concurrent, admission => Assert.True(admission.IsAdmitted));
        Assert.Single(concurrent.Select(admission => admission.Lease!.SessionId).Distinct());

        var otherIdentity = await AdmitWithCapacity(sessions, "same-user", "another-client", RuntimeConnectionClass.Interactive, RuntimeDescriptor(now, revision: 8), singleSeat);
        Assert.False(otherIdentity.IsAdmitted);
        Assert.Equal(RuntimeSessionSeatReservationReasonCode.InteractiveQuotaExhaustedNoEligibleViewOnly, otherIdentity.ReasonCode);
    }

    [Fact]
    public async Task SeatCapacity_ReclaimsSeatsAfterTerminateAndExpiry()
    {
        var now = DateTimeOffset.Parse("2026-09-17T00:00:00Z");
        var runtime = RuntimeDescriptor(now);
        var sessions = new RuntimeSessionLeaseRegistry(TimeSpan.FromSeconds(1), () => now);
        var capacity = new RuntimeSessionSeatCapacity(InteractiveSeats: 1, ViewOnlySeats: 0);
        var first = await AdmitWithCapacity(sessions, "operator-1", "client-1", RuntimeConnectionClass.Interactive, runtime, capacity);
        var explicitViewOnly = await AdmitWithCapacity(sessions, "viewer-1", "viewer-client", RuntimeConnectionClass.ViewOnly, runtime, capacity);

        Assert.False(explicitViewOnly.IsAdmitted);
        Assert.Equal(RuntimeSessionSeatReservationReasonCode.ViewOnlyQuotaExhausted, explicitViewOnly.ReasonCode);

        var terminated = await sessions.TerminateAsync(first.Lease!.SessionId, "operator-1", "client-1", runtime);
        Assert.True(terminated.IsValid);
        var afterTerminate = await AdmitWithCapacity(sessions, "operator-2", "client-2", RuntimeConnectionClass.Interactive, runtime, capacity);
        Assert.True(afterTerminate.IsAdmitted);

        now = now.AddSeconds(2);
        var afterExpiry = await AdmitWithCapacity(sessions, "operator-3", "client-3", RuntimeConnectionClass.Interactive, runtime, capacity);
        Assert.True(afterExpiry.IsAdmitted);
    }

    [Fact]
    public void Escadapkg_RemainsTopologyNeutralAndContainsNoRuntimeLeaseOrLicenseState()
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
                     entry.FullName.Contains("license", StringComparison.OrdinalIgnoreCase) ||
                     entry.FullName.Contains("key", StringComparison.OrdinalIgnoreCase) ||
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

    private static Task<RuntimeSessionSeatAdmission> AdmitWithCapacity(
        RuntimeSessionLeaseRegistry sessions,
        string userId,
        string clientInstanceId,
        RuntimeConnectionClass connectionClass,
        ScadaRuntimeDescriptor runtime,
        RuntimeSessionSeatCapacity capacity) =>
        sessions.AdmitWithCapacityAsync(userId, clientInstanceId, connectionClass, runtime, capacity);

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
