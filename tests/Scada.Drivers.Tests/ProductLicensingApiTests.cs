using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Scada.Api.Licensing;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Security;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class ProductLicensingApiTests
{
    [Fact]
    public void LicenseMutation_RequiresEngineeringModify_NotEngineeringView()
    {
        using var workspace = new EngineeringWorkspace();
        workspace.SecurityPolicies.UpsertRole(new SecurityRoleEngineeringDto(
            Guid.NewGuid(), "license-reader", "License Reader",
            Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringView)]));
        workspace.SecurityPolicies.UpsertRole(new SecurityRoleEngineeringDto(
            Guid.NewGuid(), "license-maintainer", "License Maintainer",
            Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringModify)]));
        var security = CreateSecurity(workspace);

        var reader = ProductLicensingApi.CheckMutationAuthorization(
            AuthenticatedContext("license-reader"), security);
        var maintainer = ProductLicensingApi.CheckMutationAuthorization(
            AuthenticatedContext("license-maintainer"), security);

        Assert.False(reader.Allowed);
        Assert.True(maintainer.Allowed);
    }

    [Fact]
    public async Task LicenseMutationAudit_RecordsAllowedDeniedAndFailedWithSafeBoundedMetadata()
    {
        var sink = new InMemoryAuditSink();
        var audit = new ApiAuditService(sink, sink, NullLogger<ApiAuditService>.Instance);
        var context = AuthenticatedContext("license-maintainer");
        var allowed = new ApiAuthorizationCheck(
            new SecurityPrincipal("operator", "Operator", ["license-maintainer"]),
            new AuthorizationDecision(true, SecurityCapability.EngineeringModify, "allowed", ["license-maintainer"]));
        var denied = new ApiAuthorizationCheck(
            new SecurityPrincipal("viewer", "Viewer", ["license-reader"]),
            AuthorizationDecision.Denied(SecurityCapability.EngineeringModify, "capability"));
        var result = new ProductLicenseLifecycleResult(
            true, "completed", LicenseState.Demo, LicenseState.Valid,
            1, 2, DateTimeOffset.Parse("2026-09-22T12:00:00Z"), 3, "retained");

        await ProductLicensingApi.RecordLifecycleAsync(
            context, audit, allowed, AuditActions.ProductLicenseInstall, result);
        await ProductLicensingApi.RecordAuthorizationDeniedAsync(
            context, audit, denied, AuditActions.ProductLicenseReplace, "install-or-replace");
        await ProductLicensingApi.RecordLifecycleFailureAsync(
            context, audit, allowed, AuditActions.ProductLicenseRemove);

        var events = sink.Snapshot().OrderBy(item => item.TimestampUtc).ToArray();
        Assert.Equal(3, events.Length);
        Assert.Equal(AuditOutcome.Succeeded, events[0].Outcome);
        Assert.Equal(AuditOutcome.Denied, events[1].Outcome);
        Assert.Equal(AuditOutcome.Failed, events[2].Outcome);
        Assert.Equal("3", events[0].Details!["remoteLeasesFenced"]);
        Assert.Equal("retained", events[0].Details!["localRuntimeOutcome"]);
        Assert.Equal("0", events[1].Details!["remoteLeasesFenced"]);
        Assert.Equal("unknown", events[2].Details!["previousAuthorityRevision"]);
        Assert.All(events, item =>
        {
            Assert.Contains("previousLicenseState", item.Details!.Keys);
            Assert.Contains("currentLicenseState", item.Details.Keys);
            Assert.Contains("previousAuthorityRevision", item.Details.Keys);
            Assert.Contains("currentAuthorityRevision", item.Details.Keys);
            Assert.Contains("authorityChangedAtUtc", item.Details.Keys);
            Assert.Contains("remoteLeasesFenced", item.Details.Keys);
            Assert.Contains("localRuntimeOutcome", item.Details.Keys);
        });
        Assert.All(events, item =>
        {
            var serialized = string.Join('|', item.Details?.Select(pair => $"{pair.Key}={pair.Value}") ?? []);
            Assert.DoesNotContain("ESLIC", serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("licenseCode", serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("signing", serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", serialized, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static ApiAuthorizationService CreateSecurity(EngineeringWorkspace workspace)
    {
        var exchange = new EngineeringExchangeService(
            workspace.Tags, workspace.Alarms, workspace.DataSources, workspace.Assets,
            workspace.Views, workspace.SecurityPolicies, workspace.Commands);
        var configuration = new ConfigurationManager { ["Authentication:Enabled"] = "true" };
        return new ApiAuthorizationService(new NullServiceProvider(), workspace, exchange, configuration);
    }

    private static DefaultHttpContext AuthenticatedContext(string role)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "license-api-test"), new Claim("role", role)], "test"));
        return context;
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
