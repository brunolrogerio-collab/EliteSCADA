using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scada.Api.Licensing;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Assets;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;
using Scada.Engineering.Security;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

/// <summary>
/// Stage 1 has no analytics worker, result/query endpoint, Historian read path, or
/// process write. Per-TAG TagRead plus dataset authorization, authorization-expiry
/// cancellation, and bounded analytics execution are explicit Stage 2 blockers.
/// </summary>
public sealed class AssetProcessIntelligenceEndpointContractTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("eslic1")]
    [InlineData("eslic2")]
    [InlineData("eslic3-without-entitlement")]
    [InlineData("invalid")]
    public async Task ActivateEndpoint_DeniesMissingLegacyOrInvalidIntelligenceEntitlement(string licenseCase)
    {
        await using var api = await EndpointHarness.StartAsync(CreateVerification(licenseCase));

        using var response = await api.Client.PostAsync(api.ActivatePath, content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, api.Gate.ActivePermitCount);
        Assert.Equal(0, api.Runtime.ProcessWriteCalls);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ActivateEndpoint_DeniesMissingResourceGrantOrEngineeringModify(
        bool managerGrantOnTargetEquipment,
        bool engineeringModifyGranted)
    {
        await using var api = await EndpointHarness.StartAsync(
            CreateVerification("valid"),
            managerGrantOnTargetEquipment,
            engineeringModifyGranted);

        using var response = await api.Client.PostAsync(api.ActivatePath, content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, api.Gate.ActivePermitCount);
        Assert.Equal(0, api.Runtime.ProcessWriteCalls);
    }

    [Fact]
    public async Task Stage1Routes_ExposeNoAnalyticsReadPathOrWorkWhenNoScopeIsActive()
    {
        await using var api = await EndpointHarness.StartAsync(CreateVerification("eslic1"));

        using var denied = await api.Client.PostAsync(api.ActivatePath, content: null);
        using var statusResponse = await api.Client.GetAsync(EndpointHarness.ScopesPath);
        var status = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        var execution = status.GetProperty("execution");
        using var resultsResponse = await api.Client.GetAsync($"{EndpointHarness.ScopesPath}/results");

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.False(execution.GetProperty("workerRegistered").GetBoolean());
        Assert.Equal(0, execution.GetProperty("queueDepth").GetInt32());
        Assert.Equal(0, execution.GetProperty("activePermitCount").GetInt32());
        Assert.DoesNotContain(api.Routes, route => route.Contains("results", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(api.Routes, route => route.Contains("query", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.NotFound, resultsResponse.StatusCode);
        Assert.Equal(0, api.Runtime.ProcessWriteCalls);
    }

    private static LicenseVerificationResult CreateVerification(string licenseCase)
    {
        var payload = new EliteScadaLicensePayload(
            1,
            "intelligence-stage1-test",
            "test-machine",
            LicenseTier.Tags500,
            DateTimeOffset.UtcNow,
            null,
            "test-key");
        return licenseCase switch
        {
            "missing" => LicenseVerificationResult.Demo(),
            "eslic1" => LicenseVerificationResult.Valid(payload),
            "eslic2" => LicenseVerificationResult.Valid(
                payload,
                new MachineLicenseV2Entitlements(1, 1, false)),
            "eslic3-without-entitlement" => LicenseVerificationResult.Valid(
                payload,
                new MachineLicenseV2Entitlements(1, 1, false),
                new MachineLicenseV3Entitlements(1, 1, false, false)),
            "invalid" => LicenseVerificationResult.Invalid("invalid test signature"),
            "valid" => LicenseVerificationResult.Valid(
                payload,
                new MachineLicenseV2Entitlements(1, 1, false),
                new MachineLicenseV3Entitlements(1, 1, false, true)),
            _ => throw new ArgumentOutOfRangeException(nameof(licenseCase))
        };
    }

    private sealed class EndpointHarness : IAsyncDisposable
    {
        public const string ProjectKey = "intelligence-stage1-test";
        public const string ScopesPath = "/api/engineering/asset-process-intelligence/scopes";

        private readonly WebApplication _app;
        private EndpointHarness(
            WebApplication app,
            HttpClient client,
            Guid equipmentId,
            AssetProcessIntelligenceExecutionGate gate,
            TestRuntimeCoordinator runtime,
            IReadOnlyCollection<string> routes)
        {
            _app = app;
            Client = client;
            Gate = gate;
            Runtime = runtime;
            Routes = routes;
            ActivatePath = $"{ScopesPath}/Equipment/{equipmentId:D}/activate";
        }

        public HttpClient Client { get; }
        public AssetProcessIntelligenceExecutionGate Gate { get; }
        public TestRuntimeCoordinator Runtime { get; }
        public IReadOnlyCollection<string> Routes { get; }
        public string ActivatePath { get; }

        public static async Task<EndpointHarness> StartAsync(
            LicenseVerificationResult verification,
            bool managerGrantOnTargetEquipment = true,
            bool engineeringModifyGranted = true)
        {
            var workspace = new EngineeringWorkspace(seedDemo: false);
            workspace.SetCheckout(ProjectKey, "Stage 1 contract test", 1, DateTimeOffset.UtcNow);
            var equipmentId = Guid.NewGuid();
            var otherEquipmentId = Guid.NewGuid();
            var equipmentScopeId = Guid.NewGuid();
            var otherScopeId = Guid.NewGuid();
            var authority = new InMemoryAuthorityPolicyStore(
                [new SecurityRoleEngineeringDto(
                    Guid.NewGuid(),
                    "operator",
                    "Operator",
                    Grants: CreateGrants(managerGrantOnTargetEquipment, engineeringModifyGranted, equipmentScopeId, otherScopeId))],
                [
                    new SecurityScopeEngineeringDto(equipmentScopeId, "equipment-p01", "Pump", SecurityScopeNodeKind.Equipment, ResourceId: equipmentId),
                    new SecurityScopeEngineeringDto(otherScopeId, "equipment-p02", "Fan", SecurityScopeNodeKind.Equipment, ResourceId: otherEquipmentId)
                ]);
            var gate = new AssetProcessIntelligenceExecutionGate();
            var runtime = new TestRuntimeCoordinator(ProjectKey, 1);
            var sink = new InMemoryAuditSink();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:Enabled"] = "true" })
                .Build();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Test",
                Args = []
            });
            builder.Configuration.AddConfiguration(configuration);
            builder.Services.AddSingleton(workspace);
            builder.Services.AddSingleton<IEngineeringAssetRegistry>(workspace.Assets);
            builder.Services.AddSingleton<IAssetProcessIntelligenceEngineeringRegistry>(workspace.AssetProcessIntelligence);
            builder.Services.AddSingleton<IProductLicenseService>(new TestProductLicenseService(verification));
            builder.Services.AddSingleton(gate);
            builder.Services.AddSingleton(new RuntimeHighAvailabilityService(builder.Configuration));
            builder.Services.AddSingleton<IEngineeringRuntimeCoordinator>(runtime);
            builder.Services.AddSingleton(new ScadaRuntimeFacade(runtime));
            builder.Services.AddSingleton<IAuthorityPolicyStore>(authority);
            builder.Services.AddSingleton<ApiAuthorizationService>(sp => new ApiAuthorizationService(
                sp,
                authority,
                sp.GetRequiredService<IConfiguration>()));
            builder.Services.AddSingleton<IAuditSink>(sink);
            builder.Services.AddSingleton<IAuditStore>(sink);
            builder.Services.AddSingleton<ApiAuditService>();

            var app = builder.Build();
            app.Use(async (context, next) =>
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("sub", "stage1-test-user"), new Claim("role", "operator")],
                    authenticationType: "stage1-contract-test"));
                await next();
            });
            app.MapAssetProcessIntelligenceEndpoints();
            var routes = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
                .ToArray();
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();

            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new EndpointHarness(app, new HttpClient { BaseAddress = new Uri(address) }, equipmentId, gate, runtime, routes);
        }

        private static IReadOnlyCollection<CapabilityGrantEngineeringDto> CreateGrants(
            bool managerGrantOnTargetEquipment,
            bool engineeringModifyGranted,
            Guid equipmentScopeId,
            Guid otherScopeId)
        {
            var grants = new List<CapabilityGrantEngineeringDto>
            {
                new(SecurityCapability.EngineeringView),
                new(SecurityCapability.AssetProcessIntelligenceManage,
                    new AuthorizationScopeEngineeringDto(ScopeNodeId: managerGrantOnTargetEquipment ? equipmentScopeId : otherScopeId))
            };
            if (engineeringModifyGranted)
                grants.Add(new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringModify));
            return grants;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class TestProductLicenseService(LicenseVerificationResult verification) : IProductLicenseService
    {
        public string MachineFingerprint => "test-machine";
        public string MachineRequestCode => "test-request";
        public LicenseVerificationResult CurrentVerification => verification;
        public LicenseVerificationResult VerifyCandidate(string licenseCode) => verification;
        public void InstallLicense(string licenseCode) => throw new NotSupportedException();
        public void RemoveLicense() => throw new NotSupportedException();
        public RunEntitlementDecision EvaluateRun(int projectTagCount) => ProductEntitlementEvaluator.Evaluate(verification, projectTagCount);
    }

    private sealed class TestRuntimeCoordinator(string projectKey, long revision) : IEngineeringRuntimeCoordinator
    {
        private RuntimeDescriptor _descriptor = new(projectKey, revision, DateTimeOffset.UtcNow, [], [], 0, 0);
        public int ProcessWriteCalls { get; private set; }
        public RuntimeDescriptor Describe() => _descriptor;
        public IReadOnlyCollection<TagDefinition> Tags() => [];
        public IReadOnlyCollection<TagValue> CurrentValues() => [];
        public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => [];
        public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) => [];
        public IReadOnlyCollection<CommandDefinition> Commands() => [];
        public IReadOnlyCollection<ClientMemoryRuntimeSource> ClientMemorySources() => [];
        public bool TryGetTag(Guid tagId, out TagDefinition? tag) { tag = null; return false; }
        public bool TryGetTagByPath(string path, out TagDefinition? tag) { tag = null; return false; }
        public bool TryGetCurrent(Guid tagId, out TagValue? value) { value = null; return false; }
        public bool TryGetCommand(Guid commandId, out CommandDefinition? command) { command = null; return false; }
        public bool IsServerMemoryTag(Guid tagId) => false;
        public ValueTask<bool> AcknowledgeAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> ShelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> UnshelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) { ProcessWriteCalls++; return ValueTask.CompletedTask; }
        public ValueTask ResetServerMemoryRetainedValueAsync(Guid tagId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ExecuteCommandAsync(Guid commandId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public Task<RuntimeActivationResult> ActivateAsync(string projectKey, long revision, EngineeringPackage package, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RuntimeActivationResult> ActivateAsync(string projectKey, long revision, EngineeringPackage package, Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
