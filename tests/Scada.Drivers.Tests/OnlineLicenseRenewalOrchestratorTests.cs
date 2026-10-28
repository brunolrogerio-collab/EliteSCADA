using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Scada.Api.Licensing;
using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed partial class OnlineProductLicenseLifecycleTests
{
    private static ProtectedMaterialMutationContext RenewalAdmin() => new(new SecurityPrincipal("fictitious-admin", "Test", []),
        new AuthorizationDecision(true, SecurityCapability.EngineeringModify, "test canonical authority", []));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmedActivation_AppliesOnlyIfOriginalInstallationWasNotChanged(bool replaceBeforeConfirm)
    {
        using var c = RenewalContext();
        var summary = new OnlineLicensePresentation(c.Selection.LicenseId, c.Selection.OrganizationId,
            c.Selection.ApplicationId, c.Selection.LicenseCode, "Test application", "online-account", "annual",
            c.F.Clock.GetUtcNow().AddDays(30), "Tags1000", 3, 7, true, true, 1);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var confirmationRequests = 0;
        c.Intercept = async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/prepare", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                    confirmationToken = new string('A', 43), expiresAtUtc = c.F.Clock.GetUtcNow().AddMinutes(5),
                    onlineActivationAvailable = true, summary }, options), Encoding.UTF8, "application/json") };
            confirmationRequests++;
            using var original = await c.Response(request, ct);
            using var json = JsonDocument.Parse(await original.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                activationId = root.GetProperty("activationId").GetGuid(), lease = root.GetProperty("lease").GetString(),
                notAfterUtc = root.GetProperty("notAfterUtc").GetDateTimeOffset(), summary }, options), Encoding.UTF8, "application/json") };
        };
        var prepared = await c.Device.PrepareConfirmationAsync("account", "fictitious-password", c.Selection.LicenseCode, RenewalAdmin());
        Assert.Equal(LicenseState.Demo, c.Source.CurrentVerification.State);
        Assert.Equal(0, confirmationRequests);
        if (replaceBeforeConfirm)
        {
            var offline = EliteScadaLicenseCodec.CreateSignedLicenseV3(new(3, "offline-selected", Binding.MachineFingerprint,
                LicenseTier.Tags500, c.F.Clock.GetUtcNow(), c.F.Clock.GetUtcNow().AddDays(1), "ephemeral-offline", 2, 2, false, false), keys.Offline);
            Assert.True((await c.Lifecycle.InstallOrReplaceAsync(offline)).Succeeded);
            var error = await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => c.Device.ConfirmAsync(prepared.ActivationId, RenewalAdmin()));
            Assert.Equal("ONLINE_LICENSE_SELECTION_CHANGED", error.Code);
            Assert.Equal(0, confirmationRequests);
            Assert.Equal("offline-selected", c.Source.CurrentVerification.License!.LicenseId);
        }
        else
        {
            await c.Device.ConfirmAsync(prepared.ActivationId, RenewalAdmin());
            Assert.Equal(1, confirmationRequests);
            Assert.Equal(LicenseState.Valid, c.Source.CurrentVerification.State);
            Assert.False((await c.Journal.ReadStatusAsync())!.InstallationPending);
        }
    }

    [Fact]
    public async Task ReauthenticationConfirmsSameActivationAndRotatesCredential()
    {
        using var c = RenewalContext();
        await c.Activate();
        var activationId = (await c.Journal.ReadStatusAsync())!.ActivationId;
        var renewal = await c.Journal.PrepareRenewalAsync(RenewalAdmin());
        var previousCredential = (await c.Journal.ReadTransportAsync("renewal", default)).CurrentCredential;
        await c.Journal.RequireReauthenticationAsync(renewal.PendingRequestId!.Value, RenewalAdmin(), default);
        var summary = new OnlineLicensePresentation(c.Selection.LicenseId, c.Selection.OrganizationId,
            c.Selection.ApplicationId, c.Selection.LicenseCode, "Test application", "online-account", "annual",
            c.F.Clock.GetUtcNow().AddDays(30), "Tags1000", 3, 7, true, true, 1);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        c.Intercept = async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/prepare", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                    confirmationToken = new string('A', 43), expiresAtUtc = c.F.Clock.GetUtcNow().AddMinutes(5),
                    onlineActivationAvailable = true, summary }, options), Encoding.UTF8, "application/json") };
            using var original = await c.Response(request, ct);
            using var json = JsonDocument.Parse(await original.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                activationId = root.GetProperty("activationId").GetGuid(), lease = root.GetProperty("lease").GetString(),
                notAfterUtc = root.GetProperty("notAfterUtc").GetDateTimeOffset(), summary }, options), Encoding.UTF8, "application/json") };
        };
        var prepared = await c.Device.PrepareConfirmationAsync("account", "fictitious-password", c.Selection.LicenseCode, RenewalAdmin());
        Assert.Equal(activationId, prepared.ActivationId);
        await c.Device.ConfirmAsync(activationId, RenewalAdmin());
        Assert.False((await c.Journal.ReadStatusAsync())!.ReauthenticationRequired);
        await c.Journal.PrepareRenewalAsync(RenewalAdmin());
        Assert.NotEqual(previousCredential, (await c.Journal.ReadTransportAsync("renewal", default)).CurrentCredential);
        Assert.Equal(activationId, c.Source.ReadOnlineInstallation()!.ActivationId);
    }

    [Fact]
    public async Task ChangedConfirmationSummaryCannotInstallEvenWhenLeaseIsCorrectlySigned()
    {
        using var c = RenewalContext();
        var summary = new OnlineLicensePresentation(c.Selection.LicenseId, c.Selection.OrganizationId,
            c.Selection.ApplicationId, c.Selection.LicenseCode, "Test application", "online-account", "annual",
            c.F.Clock.GetUtcNow().AddDays(30), "Tags1000", 3, 7, true, true, 1);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        c.Intercept = async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/prepare", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                    confirmationToken = new string('A', 43), expiresAtUtc = c.F.Clock.GetUtcNow().AddMinutes(5),
                    onlineActivationAvailable = true, summary }, options), Encoding.UTF8, "application/json") };
            using var original = await c.Response(request, ct);
            using var json = JsonDocument.Parse(await original.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                activationId = root.GetProperty("activationId").GetGuid(), lease = root.GetProperty("lease").GetString(),
                notAfterUtc = root.GetProperty("notAfterUtc").GetDateTimeOffset(),
                summary = summary with { InteractiveSeats = 4 } }, options), Encoding.UTF8, "application/json") };
        };
        var prepared = await c.Device.PrepareConfirmationAsync("account", "fictitious-password", c.Selection.LicenseCode, RenewalAdmin());
        var failure = await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => c.Device.ConfirmAsync(prepared.ActivationId, RenewalAdmin()));
        Assert.Equal("ONLINE_LICENSE_PRESENTATION_MISMATCH", failure.Code);
        Assert.Equal(LicenseState.Demo, c.Source.CurrentVerification.State);
        Assert.True((await c.Journal.ReadStatusAsync())!.ConfirmationPending);
    }

    [Fact]
    public async Task RenewalRunsAtFortyEightHours_WithScopedDelegationRotatingCredentialAndCanonicalEpoch()
    {
        using var c = RenewalContext();
        await c.Activate();
        Assert.Equal(1, c.RequestCount);
        Assert.Equal("not-due", (await c.Device.RunOnceAsync()).Code);
        c.F.Clock.Advance(TimeSpan.FromHours(48) - TimeSpan.FromTicks(1));
        Assert.Equal("not-due", (await c.Device.RunOnceAsync()).Code);
        Assert.Equal(1, c.RequestCount);
        var previous = await c.Journal.ReadStatusAsync();
        c.F.Clock.Advance(TimeSpan.FromTicks(1));

        var renewed = await c.Device.RunOnceAsync();

        Assert.Equal("renewed", renewed.Code);
        Assert.True(renewed.CollectedSnapshots > 0);
        Assert.Equal(2, c.RequestCount);
        Assert.Equal(3, (await c.Store.GetAuthorityStateAsync()).AuthorityRevision);
        Assert.Equal(previous!.ActivationId, (await c.Journal.ReadStatusAsync())!.ActivationId);
        Assert.Equal(Issued.AddHours(120), c.Source.ReadOnlineInstallation()!.NotAfterUtc);
        Assert.Equal(LicenseState.Valid, c.Source.CurrentVerification.State);
        var maintenance = await c.Journal.ReadMaintenanceDelegationAsync(default);
        Assert.False(maintenance.Principal.IsAuthenticated);
        Assert.Empty(maintenance.Principal.Roles);
        Assert.False(maintenance.Authorization.Allowed);
        Assert.Throws<ProtectedMaterialException>(maintenance.DemandAuthorized);
        var events = await c.Audit.QueryAsync(action: "product-license.online-renewal");
        Assert.Equal("renewed", Assert.Single(events).Details!["result"]);
        var contents = string.Join('\n', Directory.GetFiles(c.F.Root, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain("fictitious-password", contents);
        Assert.DoesNotContain(c.LastBearer!, contents);
        Assert.Single(Directory.GetFiles(Path.Combine(c.F.Root, "material"), "*.json"));
    }

    [Fact]
    public async Task MaintenanceContext_CannotAdministerOrWriteAnotherProtectedScope_AndResetRevokesDelegation()
    {
        using var c = RenewalContext();
        await c.Activate();
        var maintenance = await c.Journal.ReadMaintenanceDelegationAsync(default);
        var status = (await c.Journal.ReadStatusAsync())!;
        await Assert.ThrowsAsync<ProtectedMaterialException>(() => c.Journal.PrepareActivationAsync(c.Selection, "account", maintenance));
        await Assert.ThrowsAsync<ProtectedMaterialException>(() => c.Journal.PrepareReauthenticationAsync(c.Selection, "account", maintenance, default));
        await Assert.ThrowsAsync<ProtectedMaterialException>(() => c.Journal.ResetAsync(status.ActivationId, maintenance));
        var foreign = new ProtectedMaterialScope("deployment:product-licensing", "ProductLicensing", new string('b', 64), "OnlineDeviceState");
        await Assert.ThrowsAsync<ProtectedMaterialException>(async () => await c.Protection.StoreAsync(foreign, new byte[] { 1 }, maintenance));
        await c.Device.ResetAsync(status.ActivationId, RenewalAdmin());
        Assert.Null(c.Source.ReadOnlineInstallation());
        Assert.True((await c.Journal.ReadStatusAsync())!.IsReset);
        var reset = Assert.Single(await c.Audit.QueryAsync(action: "product-license.online-reset"));
        Assert.Equal(status.ActivationId.ToString("D"), reset.TargetId);
        Assert.Equal("not-requested", reset.Details!["remoteRevocation"]);
        await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => c.Journal.ReadMaintenanceDelegationAsync(default));
        await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => c.Journal.PrepareRenewalAsync(maintenance));
        Assert.Equal("not-selected", (await c.Device.RunOnceAsync()).Code);
        Assert.Equal(1, c.RequestCount);
    }

    [Fact]
    public async Task ResetWithNonadministrativeAllowedCapability_CannotRemoveLicenseBeforeJournalRejectsIt()
    {
        using var c = RenewalContext();
        await c.Activate();
        var activation = (await c.Journal.ReadStatusAsync())!.ActivationId;
        var readOnly = new ProtectedMaterialMutationContext(new SecurityPrincipal("reader", "Reader", []),
            new AuthorizationDecision(true, SecurityCapability.View, "test read permission", []));
        var before = await c.Store.GetAuthorityStateAsync();
        await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => c.Device.ResetAsync(activation, readOnly));
        Assert.Equal(before, await c.Store.GetAuthorityStateAsync());
        Assert.Equal(LicenseState.Valid, c.Source.CurrentVerification.State);
        Assert.False((await c.Journal.ReadStatusAsync())!.IsReset);
    }

    [Fact]
    public async Task StaleMaintenanceContextCannotRenewNewActivationAfterExplicitReset()
    {
        using var c = RenewalContext();
        await c.Activate();
        var previous = (await c.Journal.ReadStatusAsync())!.ActivationId;
        var delegated = await c.Journal.ReadMaintenanceDelegationAsync(default);
        await c.Device.ResetAsync(previous, RenewalAdmin());
        await c.Activate();
        Assert.NotEqual(previous, (await c.Journal.ReadStatusAsync())!.ActivationId);
        await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => c.Journal.PrepareRenewalAsync(delegated));
    }

    [Fact]
    public async Task ActivationApplicationFailure_RetryReusesCommittedResponseWithoutPasswordOrHttpReplay()
    {
        using var c = RenewalContext();
        FileStream? busy = null;
        c.Intercept = async (request, ct) =>
        {
            var response = await c.Response(request, ct);
            busy = new FileStream(c.F.Path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return response;
        };
        try
        {
            var failure = await Assert.ThrowsAsync<OnlineLicenseProtocolException>(c.Activate);
            Assert.Equal("ONLINE_LICENSE_APPLICATION_PENDING", failure.Code);
            Assert.Equal(LicenseState.Demo, c.Source.CurrentVerification.State);
            Assert.True((await c.Journal.ReadStatusAsync())!.HasLease);
        }
        finally { busy?.Dispose(); }
        c.Intercept = null;
        var count = c.RequestCount;
        await c.Activate();
        Assert.Equal(count, c.RequestCount);
        Assert.Equal(LicenseState.Valid, c.Source.CurrentVerification.State);
        var epoch = (await c.Store.GetAuthorityStateAsync()).AuthorityRevision;
        await c.Activate();
        Assert.Equal(epoch, (await c.Store.GetAuthorityStateAsync()).AuthorityRevision);
    }

    [Fact]
    public async Task ResetCanFinishAfterRightsWereRemovedBeforeJournalTombstone_WithoutRestoringSelection()
    {
        using var c = RenewalContext();
        await c.Activate();
        var activation = (await c.Journal.ReadStatusAsync())!.ActivationId;
        await c.Lifecycle.RemoveAsync();
        Assert.Equal("not-selected", (await c.Device.RunOnceAsync()).Code);
        await c.Device.ResetAsync(activation, RenewalAdmin());
        Assert.True((await c.Journal.ReadStatusAsync())!.IsReset);
        Assert.Null(c.Source.ReadOnlineInstallation());
        await c.Device.ResetAsync(activation, RenewalAdmin());
        Assert.Equal(LicenseState.Demo, c.Source.CurrentVerification.State);
    }

    [Fact]
    public async Task JournalAloneCannotSelectOnline_AndLegacyStateWithoutDelegationCannotRenewAutomatically()
    {
        using var c = RenewalContext();
        await c.Client.ActivateAsync(c.Selection, "account", "fictitious-password", RenewalAdmin());
        Assert.Equal("not-selected", (await c.Device.RunOnceAsync()).Code);
        Assert.Equal(LicenseState.Demo, c.Source.CurrentVerification.State);
        var candidate = await c.Journal.ReadEntitlementCandidateAsync();
        await c.Lifecycle.InstallOnlineAsync(candidate);
        var pointerPath = Path.Combine(c.JournalRoot, "online-license-reference.json");
        using var pointer = JsonDocument.Parse(File.ReadAllText(pointerPath));
        var reference = pointer.RootElement.GetProperty("reference").GetString()!;
        var scope = new ProtectedMaterialScope("deployment:product-licensing", "ProductLicensing", Binding.MachineFingerprint, "OnlineDeviceState");
        await using var material = await c.Protection.ResolveAsync(scope, reference);
        var json = System.Text.Json.Nodes.JsonNode.Parse(material.Material.Span)!.AsObject();
        json["schemaVersion"] = 2;
        json.Remove("maintenanceActivationId");
        json.Remove("nextMaintenanceAttemptUtc");
        var stored = await c.Protection.StoreAsync(scope, JsonSerializer.SerializeToUtf8Bytes(json), RenewalAdmin());
        File.WriteAllText(pointerPath, JsonSerializer.Serialize(new { schemaVersion = 1, reference = stored.Reference }));
        c.F.Clock.Advance(TimeSpan.FromHours(48));
        var error = await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => c.Device.RunOnceAsync());
        Assert.Equal("ONLINE_LICENSE_MAINTENANCE_NOT_DELEGATED", error.Code);
        Assert.Equal(1, c.RequestCount);
        Assert.Equal(LicenseState.Valid, c.Source.CurrentVerification.State);
    }

    [Fact]
    public async Task LostResponsePersistsPacing_AndRestartReplaysIdenticalRequestInsteadOfRotatingAgain()
    {
        using var c = RenewalContext();
        await c.Activate();
        c.F.Clock.Advance(TimeSpan.FromHours(48));
        string? body = null, bearer = null;
        var first = true;
        c.Intercept = async (request, ct) =>
        {
            var response = await c.Response(request, ct);
            if (first)
            {
                first = false;
                body = c.LastBody;
                bearer = c.LastBearer;
                response.Dispose();
                throw new HttpRequestException("fictitious response lost after commit");
            }
            Assert.Equal(body, c.LastBody);
            Assert.Equal(bearer, c.LastBearer);
            return response;
        };
        await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => c.Device.RunOnceAsync());
        Assert.NotNull((await c.Journal.ReadStatusAsync())!.PendingRequestId);
        var count = c.RequestCount;
        using var reopened = c.F.Source();
        var resumed = new OnlineLicenseDeviceOrchestrator(c.Journal, c.Client, reopened,
            CreateLifecycle(reopened, c.Store, c.Runtime, c.F.Clock, c.Audit), c.Audit, c.F.Clock);
        Assert.Equal("retry-delayed", (await resumed.RunOnceAsync()).Code);
        Assert.Equal(count, c.RequestCount);
        c.F.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("renewed", (await resumed.RunOnceAsync()).Code);
        Assert.Null((await c.Journal.ReadStatusAsync())!.PendingRequestId);
        Assert.Equal(3, (await c.Store.GetAuthorityStateAsync()).AuthorityRevision);
    }

    [Fact]
    public async Task ShortLeaseRenewsAtTwoThirds_AndHostedWorkerUsesVirtualTimeWithoutPassword()
    {
        using var c = RenewalContext();
        c.Lifetime = TimeSpan.FromHours(3);
        await c.Activate();
        Assert.Equal(Issued.AddHours(2), (await c.Journal.ReadStatusAsync())!.RenewalDueUtc);
        c.F.Clock.Advance(TimeSpan.FromHours(2) - TimeSpan.FromSeconds(1));
        Assert.Equal("not-due", (await c.Device.RunOnceAsync()).Code);
        var clock = new GuardClock();
        using var worker = new OnlineLicenseRenewalHostedService(c.Device, clock, NullLogger<OnlineLicenseRenewalHostedService>.Instance);
        await worker.StartAsync(CancellationToken.None);
        await WaitForGuardAsync(() => clock.TimerCount > 0);
        c.F.Clock.Advance(TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(30));
        await WaitForGuardAsync(() => c.Source.ReadOnlineInstallation()!.NotAfterUtc == Issued.AddHours(5));
        await worker.StopAsync(CancellationToken.None);
        Assert.Equal(2, c.RequestCount);
        Assert.DoesNotContain("password", c.LastBody!);
        Assert.Equal(3, (await c.Store.GetAuthorityStateAsync()).AuthorityRevision);
    }

    [Fact]
    public async Task PersistedResponseWhoseApplicationFailed_IsAppliedBeforeAnotherHttpRotation()
    {
        using var c = RenewalContext();
        await c.Activate();
        c.F.Clock.Advance(TimeSpan.FromHours(48));
        FileStream? blocked = null;
        c.Intercept = async (request, ct) =>
        {
            var response = await c.Response(request, ct);
            blocked = new FileStream(c.F.Path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return response;
        };
        try
        {
            var error = await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => c.Device.RunOnceAsync());
            Assert.Equal("ONLINE_LICENSE_APPLICATION_PENDING", error.Code);
            Assert.Null((await c.Journal.ReadStatusAsync())!.PendingRequestId);
            Assert.Equal(2, (await c.Store.GetAuthorityStateAsync()).AuthorityRevision);
        }
        finally { blocked?.Dispose(); }
        var count = c.RequestCount;
        c.Intercept = null;
        Assert.Equal("applied-persisted-response", (await c.Device.RunOnceAsync()).Code);
        Assert.Equal(count, c.RequestCount);
        Assert.Equal(3, (await c.Store.GetAuthorityStateAsync()).AuthorityRevision);
    }

    [Fact]
    public async Task UnauthorizedRenewalStopsAutomaticHttp_WhileAuthenticLeaseSurvivesUntilItsDeadline()
    {
        using var c = RenewalContext();
        await c.Activate();
        c.F.Clock.Advance(TimeSpan.FromHours(48));
        c.Intercept = (_, _) => { c.RequestCount++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); };
        await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => c.Device.RunOnceAsync());
        var count = c.RequestCount;
        Assert.Equal("reauthentication-required", (await c.Device.RunOnceAsync()).Code);
        Assert.Equal(count, c.RequestCount);
        Assert.Equal(LicenseState.Valid, c.Source.CurrentVerification.State);
        c.F.Clock.Advance(TimeSpan.FromHours(24));
        Assert.Equal("expired-to-demo", (await Guard(c.Source, c.Lifecycle, c.Runtime, c.F.Clock).RunOnceAsync()).Code);
        Assert.Equal("reauthentication-required", (await c.Device.RunOnceAsync()).Code);
        Assert.Equal(count, c.RequestCount);
    }

    [Fact]
    public async Task RenewalCompletingAfterIndependentExpiry_ReappliesSameActivationWithNewValidEvidence()
    {
        using var c = RenewalContext();
        await c.Activate();
        var activation = (await c.Journal.ReadStatusAsync())!.ActivationId;
        c.F.Clock.Advance(TimeSpan.FromHours(48));
        c.Intercept = async (request, ct) =>
        {
            c.F.Clock.Advance(TimeSpan.FromHours(24));
            Assert.Equal("expired-to-demo", (await Guard(c.Source, c.Lifecycle, c.Runtime, c.F.Clock).RunOnceAsync()).Code);
            return await c.Response(request, ct);
        };
        Assert.Equal("renewed", (await c.Device.RunOnceAsync()).Code);
        Assert.Equal(activation, c.Source.ReadOnlineInstallation()!.ActivationId);
        Assert.Equal(LicenseState.Valid, c.Source.CurrentVerification.State);
        Assert.Equal(4, (await c.Store.GetAuthorityStateAsync()).AuthorityRevision);
    }

    [Fact]
    public async Task OfflineMutationAndResetCannotRaceHttp_AndOfflineSelectionRemainsSelectedAfterRelease()
    {
        using var c = RenewalContext();
        await c.Activate();
        var activation = (await c.Journal.ReadStatusAsync())!.ActivationId;
        c.F.Clock.Advance(TimeSpan.FromHours(48));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        c.Intercept = async (request, ct) => { entered.TrySetResult(); await release.Task.WaitAsync(ct); return await c.Response(request, ct); };
        var renewal = c.Device.RunOnceAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var offline = EliteScadaLicenseCodec.CreateSignedLicenseV3(new(3, "offline-after-renewal", Binding.MachineFingerprint,
            LicenseTier.Tags500, c.F.Clock.GetUtcNow(), c.F.Clock.GetUtcNow().AddDays(1), "ephemeral-offline", 2, 2, false, false), keys.Offline);
        try
        {
            await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => c.Lifecycle.InstallOrReplaceAsync(offline));
            await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => c.Lifecycle.RemoveAsync());
            await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => c.Device.ResetAsync(activation, RenewalAdmin()));
        }
        finally { release.TrySetResult(); }
        Assert.Equal("renewed", (await renewal).Code);
        Assert.True((await c.Lifecycle.InstallOrReplaceAsync(offline)).Succeeded);
        var count = c.RequestCount;
        Assert.Equal("not-selected", (await c.Device.RunOnceAsync()).Code);
        Assert.Equal(count, c.RequestCount);
        Assert.Equal("offline-after-renewal", c.Source.CurrentVerification.License!.LicenseId);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("https://portal.example.test/", true)]
    public void DeviceComposition_IsOptIn_WithoutProductionKeyOrPortalAccess(string? origin, bool expected)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        if (origin is not null) builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Licensing:Online:PortalOrigin"] = origin });
        builder.AddConfiguredOnlineLicenseDevice();
        Assert.Equal(expected, builder.Services.Any(x => x.ServiceType == typeof(OnlineLicenseDeviceOrchestrator)));
        Assert.Equal(expected, builder.Services.Any(x => x.ServiceType == typeof(FileOnlineLicenseStateStore)));
        Assert.Equal(expected, builder.Services.Any(x => x.ServiceType == typeof(IHostedService)));
    }

    [Theory]
    [InlineData("http://portal.example.test/")]
    [InlineData("https://user:password@portal.example.test/")]
    [InlineData("https://portal.example.test/other")]
    [InlineData("https://portal.example.test/?key=hidden")]
    public void DeviceComposition_RejectsUnpinnedOrCredentialBearingOrigins(string origin)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Licensing:Online:PortalOrigin"] = origin });
        Assert.Throws<InvalidOperationException>(() => builder.AddConfiguredOnlineLicenseDevice());
    }

    [Fact]
    public void ConfiguredDeviceAndLifecycle_ResolveWithoutDependencyCycleOrNetworkSideEffect()
    {
        using var c = RenewalContext();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Licensing:Online:PortalOrigin"] = "https://portal.example.test/",
            ["Licensing:Online:StateDirectory"] = Path.Combine(c.F.Root, "composition")
        });
        builder.Services.AddSingleton<IProductLicenseService>(c.Source);
        builder.Services.AddSingleton<IProtectedMaterialAuthority>(c.Protection);
        builder.Services.AddSingleton<IAuditStore>(c.Audit);
        builder.Services.AddSingleton<IRuntimeSessionLeaseStore>(c.Store);
        builder.Services.AddSingleton<IProductRuntimeAuthorityReevaluator>(c.Runtime);
        builder.Services.AddSingleton<TimeProvider>(c.F.Clock);
        builder.Services.AddSingleton<ProductLicenseLifecycleCoordinator>();
        builder.AddConfiguredOnlineLicenseDevice();
        using var services = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        Assert.NotNull(services.GetRequiredService<OnlineLicenseDeviceOrchestrator>());
        Assert.NotNull(services.GetRequiredService<ProductLicenseLifecycleCoordinator>());
        Assert.Equal(0, c.RequestCount);
        Assert.False(Directory.Exists(Path.Combine(c.F.Root, "composition")));
    }

    private RenewalTestContext RenewalContext() => new(this, keys);

    private sealed class RenewalTestContext : IDisposable
    {
        internal Fixture F { get; }
        internal string JournalRoot => Path.Combine(F.Root, "journal");
        internal FileHostProtectedMaterialAuthority Protection { get; }
        internal FileOnlineLicenseStateStore Journal { get; }
        internal FileProductLicenseService Source { get; }
        internal InMemoryRuntimeSessionLeaseStore Store { get; }
        internal RecordingRuntime Runtime { get; } = new();
        internal InMemoryAuditSink Audit { get; } = new();
        internal ProductLicenseLifecycleCoordinator Lifecycle { get; }
        internal OnlineLicenseDeviceClient Client { get; }
        internal OnlineLicenseDeviceOrchestrator Device { get; }
        internal OnlineLicenseSelection Selection => new(Binding.LicenseId, Binding.OrganizationId, Binding.ApplicationId, "SCV-FICTITIOUS");
        internal int RequestCount { get; set; }
        internal string? LastBody { get; private set; }
        internal string? LastBearer { get; private set; }
        internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Intercept { get; set; }
        internal TimeSpan? Lifetime { get; set; }
        private readonly OnlineProductLicenseLifecycleTests test;
        private readonly Dictionary<Guid, string> responses = [];
        internal RenewalTestContext(OnlineProductLicenseLifecycleTests test, OnlineProductLicenseTestKeys keys)
        {
            this.test = test;
            F = new(keys);
            var protectionKey = RandomNumberGenerator.GetBytes(32);
            Protection = new(new(Path.Combine(F.Root, "material"), "FICTITIOUS_TEST_KEY", null), protectionKey, Audit, F.Clock);
            CryptographicOperations.ZeroMemory(protectionKey);
            Journal = new(JournalRoot, Binding.MachineFingerprint, new Uri("https://portal.example.test/"), Protection, F.Clock,
                (code, binding, now) => OnlineLeaseVerifier.VerifyWithKey(code, binding, now, keys.Online));
            Source = F.Source();
            Store = new(F.Clock.GetUtcNow);
            Lifecycle = new(Source, Store, Runtime, F.Clock, Audit, onlineJournal: Journal);
            Client = new(Journal, new RenewalHandler((request, ct) => Intercept?.Invoke(request, ct) ?? Response(request, ct)));
            Device = new(Journal, Client, Source, Lifecycle, Audit, F.Clock);
        }
        internal Task<OnlineLicenseDeviceStatus> Activate() => Device.ActivateAsync(Selection, "account", "fictitious-password", RenewalAdmin());
        internal async Task<HttpResponseMessage> Response(HttpRequestMessage request, CancellationToken ct)
        {
            RequestCount++;
            LastBody = await request.Content!.ReadAsStringAsync(ct);
            LastBearer = request.Headers.Authorization?.Parameter;
            using var json = JsonDocument.Parse(LastBody);
            var body = json.RootElement;
            var activation = body.TryGetProperty("activationId", out var id) ? id.GetGuid() : Guid.Parse(request.RequestUri!.Segments[^2].Trim('/'));
            var requestId = body.GetProperty("requestId").GetGuid();
            if (!responses.TryGetValue(requestId, out var envelope))
            {
                envelope = test.Candidate(issued: F.Clock.GetUtcNow(), binding: Binding with { ActivationId = activation }, lifetime: Lifetime).Envelope;
                responses[requestId] = envelope;
            }
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
                { activationId = activation, lease = envelope, notAfterUtc = F.Clock.GetUtcNow().AddHours(72) }), Encoding.UTF8, "application/json") };
        }
        public void Dispose() { Client.Dispose(); Store.DisposeAsync().GetAwaiter().GetResult(); Source.Dispose(); Protection.Dispose(); F.Dispose(); }
    }

    private sealed class RenewalHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
}
