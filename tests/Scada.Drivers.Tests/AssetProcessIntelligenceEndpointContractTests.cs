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
using Scada.Api.Persistence;
using Scada.Api.Licensing;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Assets;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Intelligence;
using Scada.Engineering.Persistence;
using Scada.Engineering.Reports;
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
        Assert.Equal(0, api.ProcessWriteCalls);
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
        Assert.Equal(0, api.ProcessWriteCalls);
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
        Assert.Equal(0, api.ProcessWriteCalls);
    }

    [Fact]
    public async Task SemanticCatalogEndpointReturnsVersionedV1AndV2Catalogs()
    {
        await using var api = await EndpointHarness.StartAsync(CreateVerification("valid"));

        using var response = await api.Client.GetAsync(EndpointHarness.SemanticCatalogPath);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var roles = payload.GetProperty("semanticCatalogs").EnumerateArray()
            .Single(item => item.GetProperty("catalogVersion").GetInt32() == 2)
            .GetProperty("roles").EnumerateArray().Select(item => item.GetProperty("code").GetString()).ToArray();
        var units = payload.GetProperty("controlledEngineeringUnitCatalogs").EnumerateArray()
            .Single(item => item.GetProperty("catalogVersion").GetInt32() == 2)
            .GetProperty("units").EnumerateArray().Select(item => item.GetProperty("code").GetString()).ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(1, payload.GetProperty("supportedSemanticContractVersions").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Contains(2, payload.GetProperty("supportedSemanticContractVersions").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Contains("equipment.active_input_power", roles);
        Assert.Contains("pump.direct_differential_pressure", roles);
        Assert.Contains("unit.power.kilowatt", units);
        Assert.Contains("unit.pressure.kilopascal", units);
    }

    [Fact]
    public async Task ContextualDeviationVectorCatalogEndpointReturnsBoundedVersionedContract()
    {
        await using var api = await EndpointHarness.StartAsync(CreateVerification("valid"), includeAnalyticsEndpoints: true);

        using var response = await api.Client.GetAsync(EndpointHarness.ContextualDeviationVectorCatalogPath);
        var catalog = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, catalog.GetProperty("contractVersion").GetInt32());
        Assert.Equal("contextual-deviation-vector.v1", catalog.GetProperty("ruleId").GetString());
        Assert.Equal(8, catalog.GetProperty("maximumTargetSignals").GetInt32());
        Assert.Equal(50000, catalog.GetProperty("maximumRawRowsAcrossAllSignalsAndWindows").GetInt32());
        Assert.Equal("not_assessed", catalog.GetProperty("regimeEquivalence").GetString());
        Assert.Equal("unknown", catalog.GetProperty("completeness").GetString());
    }

    [Fact]
    public async Task ContextualDeviationVectorV2CatalogEndpointIsAdditiveAndDeclaresTwoContexts()
    {
        await using var api = await EndpointHarness.StartAsync(CreateVerification("valid"), includeAnalyticsEndpoints: true);

        using var response = await api.Client.GetAsync(EndpointHarness.ContextualDeviationVectorV2CatalogPath);
        var catalog = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, catalog.GetProperty("contractVersion").GetInt32());
        Assert.Equal("contextual-deviation-vector.v2", catalog.GetProperty("ruleId").GetString());
        Assert.Equal(2, catalog.GetProperty("contextSignalCount").GetInt32());
        Assert.Equal(30, catalog.GetProperty("minimumCompleteJointReferenceVectorsForRobustScore").GetInt32());
        Assert.Equal(50000, catalog.GetProperty("maximumRawRowsAcrossAllSignalsAndWindows").GetInt32());
        Assert.Equal(500, catalog.GetProperty("maximumReturnedEvaluationVectors").GetInt32());
        Assert.Equal("not_assessed", catalog.GetProperty("regimeEquivalence").GetString());
        Assert.Equal("unknown", catalog.GetProperty("completeness").GetString());
    }

    [Fact]
    public async Task SemanticCatalogEndpointRequiresEngineeringView()
    {
        await using var api = await EndpointHarness.StartAsync(
            CreateVerification("valid"),
            engineeringViewGranted: false);

        using var response = await api.Client.GetAsync(EndpointHarness.SemanticCatalogPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Version2SemanticBindingsRequireMarker2AndLowerMarkerCannotDropThem()
    {
        await using var api = await EndpointHarness.StartAsync(CreateVerification("valid"));
        var tag = Assert.Single(api.Workspace.Tags.Snapshot());
        api.Workspace.Tags.Upsert(tag with
        {
            Name = "Active input power",
            Path = "Plant.Pump.ActiveInputPower",
            EngineeringUnit = "kW",
            ControlledEngineeringUnit = new ControlledEngineeringUnitReference(2, "unit.power.kilowatt")
        });
        var profileId = Guid.NewGuid();
        var signalId = Guid.NewGuid();
        var profile = new AssetProcessIntelligenceProfileEngineeringDto(profileId, "pump", "Pump",
        [new(signalId, "active-input-power", "Power", api.TagId,
            SemanticBinding: new(2, "equipment.active_input_power", SignalKind: "measured_total_input"))]);
        var request = new AssetProcessIntelligenceConfigurationRequest([profile], [], [], SemanticContractVersion: 1);

        using var rejectedVersion = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath, request);

        Assert.Equal(HttpStatusCode.Conflict, rejectedVersion.StatusCode);
        Assert.Empty(api.Workspace.AssetProcessIntelligence.Snapshot().Profiles!);

        using var acceptedVersion = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath,
            request with { SemanticContractVersion = 2 });
        Assert.Equal(HttpStatusCode.OK, acceptedVersion.StatusCode);
        var savedBinding = Assert.Single(api.Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single().Signals!).SemanticBinding;
        Assert.Equal(2, savedBinding!.CatalogVersion);

        using var rejectedDrop = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath,
            new AssetProcessIntelligenceConfigurationRequest([], [], [], SemanticContractVersion: 1));
        Assert.Equal(HttpStatusCode.Conflict, rejectedDrop.StatusCode);
        Assert.Equal(savedBinding, Assert.Single(api.Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single().Signals!).SemanticBinding);
    }

    [Fact]
    public async Task Version2WriterGuardsUseProfileAndSignalIdentityWhenSignalIdsRepeatAcrossProfiles()
    {
        await using var api = await EndpointHarness.StartAsync(CreateVerification("valid"));
        var tag = Assert.Single(api.Workspace.Tags.Snapshot());
        api.Workspace.Tags.Upsert(tag with
        {
            Name = "Active input power",
            Path = "Plant.Pump.ActiveInputPower",
            EngineeringUnit = "kW",
            ControlledEngineeringUnit = new ControlledEngineeringUnitReference(2, "unit.power.kilowatt")
        });
        var signalId = Guid.NewGuid();
        var binding = new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(
            2, "equipment.active_input_power", SignalKind: "measured_total_input");
        var firstProfile = new AssetProcessIntelligenceProfileEngineeringDto(
            Guid.NewGuid(), "pump-primary", "Pump primary",
            [new(signalId, "active-input-power", "Power", api.TagId, SemanticBinding: binding)]);
        var secondProfile = new AssetProcessIntelligenceProfileEngineeringDto(
            Guid.NewGuid(), "pump-secondary", "Pump secondary",
            [new(signalId, "active-input-power", "Power", api.TagId, SemanticBinding: binding)]);

        using var seeded = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath,
            new AssetProcessIntelligenceConfigurationRequest([firstProfile, secondProfile], [], [], SemanticContractVersion: 2));
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);

        var preserveBoth = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath,
            new AssetProcessIntelligenceConfigurationRequest(
                [firstProfile with { Name = "Renamed primary" }, secondProfile], [], [], SemanticContractVersion: 1));
        using (preserveBoth)
            Assert.Equal(HttpStatusCode.OK, preserveBoth.StatusCode);

        var beforeRejectedEdits = api.Workspace.AssetProcessIntelligence.Snapshot();
        var snapshotBeforeRejectedEdits = JsonSerializer.Serialize(beforeRejectedEdits);
        var versionBeforeRejectedEdits = api.Workspace.CaptureChangeVersion();
        var changedOneBinding = firstProfile with
        {
            Signals = [firstProfile.Signals!.Single() with
            {
                SemanticBinding = binding with { RoleCode = "equipment.unsupported_power" }
            }]
        };
        using var rejectedChange = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath,
            new AssetProcessIntelligenceConfigurationRequest([changedOneBinding, secondProfile], [], [], SemanticContractVersion: 1));
        Assert.Equal(HttpStatusCode.Conflict, rejectedChange.StatusCode);
        Assert.Equal(snapshotBeforeRejectedEdits, JsonSerializer.Serialize(api.Workspace.AssetProcessIntelligence.Snapshot()));
        Assert.Equal(versionBeforeRejectedEdits, api.Workspace.CaptureChangeVersion());

        var omittedOneBinding = firstProfile with
        {
            Signals = [firstProfile.Signals!.Single() with { SemanticBinding = null }]
        };
        using var rejectedOmission = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath,
            new AssetProcessIntelligenceConfigurationRequest([omittedOneBinding, secondProfile], [], [], SemanticContractVersion: 1));
        Assert.Equal(HttpStatusCode.Conflict, rejectedOmission.StatusCode);
        Assert.Equal(snapshotBeforeRejectedEdits, JsonSerializer.Serialize(api.Workspace.AssetProcessIntelligence.Snapshot()));
        Assert.Equal(versionBeforeRejectedEdits, api.Workspace.CaptureChangeVersion());

        using var explicitRemoval = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath,
            new AssetProcessIntelligenceConfigurationRequest([omittedOneBinding, secondProfile], [], [], SemanticContractVersion: 2));
        Assert.Equal(HttpStatusCode.OK, explicitRemoval.StatusCode);
        var savedProfiles = api.Workspace.AssetProcessIntelligence.Snapshot().Profiles!;
        Assert.Null(Assert.Single(savedProfiles.Single(profile => profile.Id == firstProfile.Id).Signals!).SemanticBinding);
        Assert.Equal(binding, Assert.Single(savedProfiles.Single(profile => profile.Id == secondProfile.Id).Signals!).SemanticBinding);
    }

    [Fact]
    public async Task Stage2ConfigurationEndpointSavesStaticConfigurationAndPreservesStage1Scopes()
    {
        await using var api = await EndpointHarness.StartAsync(CreateVerification("valid"));
        var profileId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var request = new AssetProcessIntelligenceConfigurationRequest(
            [new(profileId, "pump-profile", "Pump profile",
                [new(Guid.NewGuid(), "temperature", "process", api.TagId, Required: true)])],
            [new(assignmentId, api.EquipmentId, profileId)],
            [new(stageId, "treatment", "Treatment", 0, [new(assignmentId, 0)])]);

        using var saved = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath, request);
        using var statusResponse = await api.Client.GetAsync(EndpointHarness.ScopesPath);
        var status = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        var configuration = api.Workspace.AssetProcessIntelligence.Snapshot();

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.Equal(2, configuration.Version);
        Assert.Single(configuration.Scopes!);
        Assert.False(Assert.Single(configuration.Scopes!).Active);
        Assert.Single(configuration.Profiles!);
        Assert.Equal(api.TagId, Assert.Single(configuration.Profiles!.Single().Signals!).TagId);
        Assert.Single(configuration.Assignments!);
        Assert.Equal(0, Assert.Single(configuration.ProcessStages!).Order);
        Assert.True(api.Workspace.Describe().IsDirty);
        Assert.Contains(status.GetProperty("tags").EnumerateArray(), tag => tag.GetProperty("id").GetGuid() == api.TagId);
        Assert.Equal(0, api.ProcessWriteCalls);
        var reconfiguredScopes = await api.Client.PutAsJsonAsync(EndpointHarness.ScopesPath,
            new AssetProcessIntelligenceScopeConfigurationRequest(
                [new AssetProcessIntelligenceScopeSelection(AssetProcessIntelligenceScopeKind.Equipment, api.EquipmentId)]));
        Assert.Equal(HttpStatusCode.OK, reconfiguredScopes.StatusCode);
        Assert.Equal(2, api.Workspace.AssetProcessIntelligence.Snapshot().Version);
        Assert.Single(api.Workspace.AssetProcessIntelligence.Snapshot().Profiles!);
        var audit = await api.Audit.QueryAsync(action: AuditActions.AssetProcessIntelligenceConfigurationConfigure);
        Assert.Contains(audit, item => item.Outcome == AuditOutcome.Succeeded && item.TargetId == EndpointHarness.ProjectKey);
    }

    [Fact]
    public async Task Stage2ConfigurationEndpointRequiresEngineeringModify()
    {
        await using var api = await EndpointHarness.StartAsync(
            CreateVerification("valid"),
            engineeringModifyGranted: false);

        using var response = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath,
            new AssetProcessIntelligenceConfigurationRequest([], [], []));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, api.Workspace.AssetProcessIntelligence.Snapshot().Version);
        Assert.Empty(api.Workspace.AssetProcessIntelligence.Snapshot().Profiles!);
    }

    [Fact]
    public async Task LegacyConfigurationPutCannotSilentlyDropTypedSemanticAnnotations()
    {
        await using var api = await EndpointHarness.StartAsync(
            CreateVerification("valid"),
            seedControlledSemanticTag: true,
            seedSemanticAnnotations: true);
        var original = Assert.Single(api.Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single().Signals!);
        var versionBefore = api.Workspace.CaptureChangeVersion();

        using var response = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath,
            new AssetProcessIntelligenceConfigurationRequest([], [], []));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INTELLIGENCE_SEMANTIC_ANNOTATION_CONFLICT", body.GetProperty("error").GetString());
        var retained = Assert.Single(api.Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single().Signals!);
        Assert.Equal(original.SemanticBinding, retained.SemanticBinding);
        Assert.Equal(original.TagId, retained.TagId);
        Assert.Equal(versionBefore, api.Workspace.CaptureChangeVersion());
        Assert.False(api.Workspace.Describe().IsDirty);
    }

    [Fact]
    public async Task ExplicitSemanticContractAllowsValidatedAnnotationRemoval()
    {
        await using var api = await EndpointHarness.StartAsync(
            CreateVerification("valid"),
            seedControlledSemanticTag: true,
            seedSemanticAnnotations: true);
        var current = api.Workspace.AssetProcessIntelligence.Snapshot();
        var profile = Assert.Single(current.Profiles!);
        var clearedProfile = profile with
        {
            Signals = profile.Signals!.Select(signal => signal with { SemanticBinding = null }).ToArray()
        };
        var request = new AssetProcessIntelligenceConfigurationRequest(
            [clearedProfile], [], [], SemanticContractVersion: 1);

        using var response = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath, request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Assert.Single(api.Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single().Signals!).SemanticBinding);
    }

    [Fact]
    public async Task NewTypedSemanticBindingRequiresExplicitSemanticContractMarker()
    {
        await using var api = await EndpointHarness.StartAsync(
            CreateVerification("valid"),
            seedControlledSemanticTag: true);
        var profileId = Guid.NewGuid();
        var request = new AssetProcessIntelligenceConfigurationRequest(
            [new(profileId, "pump", "Pump", [new(Guid.NewGuid(), "current", "Current", api.TagId,
                SemanticBinding: new(1, "pump.electrical_current", CurrentBasis: "line_rms"))])],
            [],
            []);

        using var response = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath, request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INTELLIGENCE_SEMANTIC_CONTRACT_VERSION_REQUIRED", body.GetProperty("error").GetString());
        Assert.Empty(api.Workspace.AssetProcessIntelligence.Snapshot().Profiles!);
    }

    [Fact]
    public async Task AuthorizedActivationPersistsScopeAndArmsPermit_ThenPausePersistsAndCancels()
    {
        await using var api = await EndpointHarness.StartAsync(
            CreateVerification("valid"),
            includeActivationPipeline: true);

        using var activated = await api.Client.PostAsync(api.ActivatePath, content: null);
        var activation = await activated.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        Assert.True(activation.GetProperty("active").GetBoolean());
        Assert.True(activation.GetProperty("runtimeActivated").GetBoolean());
        Assert.True(activation.GetProperty("executionPermit").GetBoolean());
        var activeRevision = activation.GetProperty("persistedRevision").GetInt64();
        Assert.True(api.Gate.TryGetToken(
            EndpointHarness.ProjectKey,
            activeRevision,
            AssetProcessIntelligenceScopeKind.Equipment,
            api.EquipmentId,
            out var permit));
        Assert.False(permit.IsCancellationRequested);
        Assert.True(await api.IsPersistedScopeActiveAsync());

        using var paused = await api.Client.PostAsync(api.PausePath, content: null);
        var pause = await paused.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, paused.StatusCode);
        Assert.False(pause.GetProperty("active").GetBoolean());
        Assert.False(pause.GetProperty("executionPermit").GetBoolean());
        Assert.True(permit.IsCancellationRequested);
        Assert.False(api.Gate.TryGetToken(
            EndpointHarness.ProjectKey,
            pause.GetProperty("persistedRevision").GetInt64(),
            AssetProcessIntelligenceScopeKind.Equipment,
            api.EquipmentId,
            out _));
        Assert.False(await api.IsPersistedScopeActiveAsync());
        Assert.Equal(0, api.Gate.ActivePermitCount);
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
        public const string ConfigurationPath = "/api/engineering/asset-process-intelligence/configuration";
        public const string SemanticCatalogPath = "/api/engineering/asset-process-intelligence/semantic-contracts/v2/catalog";
        public const string ContextualDeviationVectorCatalogPath = AssetProcessIntelligenceAnalyticsApi.Route + "/contextual-deviation-vector/v1/catalog";
        public const string ContextualDeviationVectorV2CatalogPath = AssetProcessIntelligenceAnalyticsApi.Route + "/contextual-deviation-vector/v2/catalog";

        private readonly WebApplication _app;
        private readonly IEngineeringProjectPersistenceService? _persistence;
        private readonly IEngineeringExchangeService? _exchange;
        private EndpointHarness(
            WebApplication app,
            HttpClient client,
            Guid equipmentId,
            Guid tagId,
            EngineeringWorkspace workspace,
            AssetProcessIntelligenceExecutionGate gate,
            IEngineeringRuntimeCoordinator runtime,
            InMemoryAuditSink audit,
            IEngineeringProjectPersistenceService? persistence,
            IEngineeringExchangeService? exchange,
            IReadOnlyCollection<string> routes)
        {
            _app = app;
            _persistence = persistence;
            _exchange = exchange;
            Client = client;
            EquipmentId = equipmentId;
            TagId = tagId;
            Workspace = workspace;
            Gate = gate;
            Runtime = runtime;
            Audit = audit;
            Routes = routes;
            ActivatePath = $"{ScopesPath}/Equipment/{equipmentId:D}/activate";
        }

        public HttpClient Client { get; }
        public Guid EquipmentId { get; }
        public Guid TagId { get; }
        public EngineeringWorkspace Workspace { get; }
        public AssetProcessIntelligenceExecutionGate Gate { get; }
        public IEngineeringRuntimeCoordinator Runtime { get; }
        public InMemoryAuditSink Audit { get; }
        public int ProcessWriteCalls => (Runtime as TestRuntimeCoordinator)?.ProcessWriteCalls ?? 0;
        public IReadOnlyCollection<string> Routes { get; }
        public string ActivatePath { get; }
        public string PausePath => $"{ScopesPath}/Equipment/{EquipmentId:D}/pause";

        public static async Task<EndpointHarness> StartAsync(
            LicenseVerificationResult verification,
            bool managerGrantOnTargetEquipment = true,
            bool engineeringModifyGranted = true,
            bool includeActivationPipeline = false,
            bool seedControlledSemanticTag = false,
            bool seedSemanticAnnotations = false,
            bool engineeringViewGranted = true,
            bool includeAnalyticsEndpoints = false)
        {
            var workspace = new EngineeringWorkspace(seedDemo: false);
            var equipmentId = Guid.NewGuid();
            var tagId = Guid.NewGuid();
            var otherEquipmentId = Guid.NewGuid();
            workspace.Assets.UpsertEquipment(new EquipmentEngineeringDto(equipmentId, "Plant.Pump", "Pump"));
            workspace.Assets.UpsertEquipment(new EquipmentEngineeringDto(otherEquipmentId, "Plant.Fan", "Fan"));
            workspace.Tags.Register(new TagDefinition(
                tagId,
                seedControlledSemanticTag ? "Current" : "Temperature",
                seedControlledSemanticTag ? "Plant.Pump.Current" : "Plant.Pump.Temperature",
                TagDataType.Double,
                null,
                seedControlledSemanticTag ? "A" : "°C",
                null,
                true,
                ControlledEngineeringUnit: seedControlledSemanticTag
                    ? new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere")
                    : null));
            workspace.AssetProcessIntelligence.Replace(new AssetProcessIntelligenceEngineeringDto(Scopes:
            [new(AssetProcessIntelligenceScopeKind.Equipment, equipmentId)]));
            if (seedSemanticAnnotations)
            {
                if (!seedControlledSemanticTag)
                    throw new ArgumentException("Semantic annotations require a controlled-unit TAG.", nameof(seedSemanticAnnotations));
                workspace.AssetProcessIntelligence.Replace(new AssetProcessIntelligenceEngineeringDto(
                    Version: 2,
                    Scopes: [new(AssetProcessIntelligenceScopeKind.Equipment, equipmentId)],
                    Profiles: [new(Guid.NewGuid(), "pump", "Pump",
                        [new(Guid.NewGuid(), "current", "Current", tagId,
                            SemanticBinding: new(1, "pump.electrical_current", CurrentBasis: "line_rms"))])]));
            }
            workspace.SetCheckout(ProjectKey, "Stage 1 contract test", 1, DateTimeOffset.UtcNow);
            var equipmentScopeId = Guid.NewGuid();
            var otherScopeId = Guid.NewGuid();
            var authority = new InMemoryAuthorityPolicyStore(
                [new SecurityRoleEngineeringDto(
                    Guid.NewGuid(),
                    "operator",
                    "Operator",
                    Grants: CreateGrants(managerGrantOnTargetEquipment, engineeringModifyGranted, equipmentScopeId, otherScopeId, engineeringViewGranted))],
                [
                    new SecurityScopeEngineeringDto(equipmentScopeId, "equipment-p01", "Pump", SecurityScopeNodeKind.Equipment, ResourceId: equipmentId),
                    new SecurityScopeEngineeringDto(otherScopeId, "equipment-p02", "Fan", SecurityScopeNodeKind.Equipment, ResourceId: otherEquipmentId)
                ]);
            var gate = new AssetProcessIntelligenceExecutionGate();
            IEngineeringRuntimeCoordinator runtime = new TestRuntimeCoordinator(ProjectKey, 1);
            IEngineeringProjectPersistenceService? persistence = null;
            IEngineeringExchangeService? exchange = CreateExchange(workspace);
            if (includeActivationPipeline)
            {
                var seeded = new EngineeringProjectSnapshot(
                    1,
                    ProjectKey,
                    "Stage 1 contract test",
                    EngineeringExchangeService.CurrentSchema,
                    EngineeringExchangeService.CurrentSchemaVersion,
                    DateTimeOffset.UtcNow,
                    exchange.ExportJson(),
                    "test");
                var projectStore = new HarnessEngineeringProjectStore(seeded);
                persistence = new EngineeringProjectPersistenceService(exchange, projectStore, workspace.VisualAssets);
                var liveRuntime = new EngineeringRuntimeCoordinator(
                    new InMemoryScadaEventBus(),
                    new EngineeringDriverCompiler(),
                    TimeSpan.FromSeconds(2));
                var initialRuntime = await liveRuntime.ActivateAsync(
                    ProjectKey,
                    seeded.Revision,
                    exchange.ParseJson(seeded.EngineeringJson));
                Assert.True(initialRuntime.Activated);
                runtime = liveRuntime;
            }
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
            builder.Services.AddSingleton(exchange!);
            builder.Services.AddSingleton<IProductLicenseService>(new TestProductLicenseService(verification));
            builder.Services.AddSingleton(gate);
            builder.Services.AddSingleton(new RuntimeHighAvailabilityService(builder.Configuration));
            builder.Services.AddSingleton<IEngineeringRuntimeCoordinator>(runtime);
            builder.Services.AddSingleton(new ScadaRuntimeFacade(runtime));
            if (includeAnalyticsEndpoints)
                builder.Services.AddTransient<AssetProcessIntelligenceAnalyticsService>();
            if (persistence is not null && exchange is not null)
            {
                builder.Services.AddSingleton(persistence);
                builder.Services.AddSingleton<IPublishedRuntimeActivationService>(new PublishedRuntimeActivationService(
                    persistence,
                    exchange,
                    runtime,
                    intelligenceGate: gate));
            }
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
            if (includeAnalyticsEndpoints)
                app.MapAssetProcessIntelligenceAnalyticsEndpoints();
            var routes = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
                .ToArray();
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();

            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new EndpointHarness(
                app,
                new HttpClient { BaseAddress = new Uri(address) },
                equipmentId,
                tagId,
                workspace,
                gate,
                runtime,
                sink,
                persistence,
                exchange,
                routes);
        }

        private static EngineeringExchangeService CreateExchange(EngineeringWorkspace workspace) =>
            new(
                workspace.Tags,
                workspace.Alarms,
                workspace.DataSources,
                workspace.Assets,
                workspace.Views,
                workspace.SecurityPolicies,
                workspace.Commands,
                new InMemoryGatewayEngineeringRegistry(),
                scripts: workspace.Scripts,
                visualAssets: workspace.VisualAssets,
                reports: new InMemoryReportEngineeringRegistry(),
                operationalEvents: workspace.Scripts,
                branding: workspace.Branding,
                mediaSources: workspace.MediaSources,
                driverInteractions: workspace.Interactions,
                assetProcessIntelligence: workspace.AssetProcessIntelligence);

        public async Task<bool> IsPersistedScopeActiveAsync()
        {
            if (_persistence is null || _exchange is null) return false;
            var active = await _persistence.LoadActiveAsync(ProjectKey);
            var scopes = _exchange.ParseJson(active!.EngineeringJson).AssetProcessIntelligence!.Scopes;
            return Assert.Single(scopes!).Active;
        }

        private static IReadOnlyCollection<CapabilityGrantEngineeringDto> CreateGrants(
            bool managerGrantOnTargetEquipment,
            bool engineeringModifyGranted,
            Guid equipmentScopeId,
            Guid otherScopeId,
            bool engineeringViewGranted)
        {
            var grants = new List<CapabilityGrantEngineeringDto>
            {
                new(SecurityCapability.AssetProcessIntelligenceManage,
                    new AuthorizationScopeEngineeringDto(ScopeNodeId: managerGrantOnTargetEquipment ? equipmentScopeId : otherScopeId))
            };
            if (engineeringViewGranted) grants.Add(new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringView));
            if (engineeringModifyGranted)
                grants.Add(new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringModify));
            return grants;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
            if (Runtime is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
        }
    }

    private sealed class HarnessEngineeringProjectStore(EngineeringProjectSnapshot initial) : IEngineeringProjectStore
    {
        private readonly SortedDictionary<long, EngineeringProjectSnapshot> _revisions = new()
        {
            [initial.Revision] = initial
        };
        private EngineeringProjectPublication? _publication =
            new(initial.ProjectKey, initial.Revision, initial.SavedAtUtc, "test");
        private EngineeringProjectActivation? _activation =
            new(initial.ProjectKey, initial.Revision, initial.SavedAtUtc, "test");

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<EngineeringProjectSnapshot> SaveAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            string? savedBy = null,
            CancellationToken cancellationToken = default) =>
            SaveDerivedAsync(
                projectKey,
                projectName,
                engineeringSchema,
                engineeringSchemaVersion,
                engineeringJson,
                null,
                savedBy,
                cancellationToken);

        public Task<EngineeringProjectSnapshot> SaveDerivedAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            long? basedOnRevision,
            string? savedBy = null,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(projectKey, initial.ProjectKey, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unexpected project key.");
            var revision = checked(_revisions.Keys.Max() + 1);
            var snapshot = new EngineeringProjectSnapshot(
                revision,
                projectKey,
                projectName,
                engineeringSchema,
                engineeringSchemaVersion,
                DateTimeOffset.UtcNow,
                engineeringJson,
                savedBy,
                basedOnRevision);
            _revisions.Add(revision, snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_revisions.Values.LastOrDefault(snapshot =>
                snapshot.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase)));

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_revisions.TryGetValue(revision, out var snapshot) &&
                snapshot.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase)
                    ? snapshot
                    : null);

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(_revisions.Values
                .Where(snapshot => snapshot.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(snapshot => snapshot.Revision)
                .Take(limit)
                .ToArray());

        public Task<EngineeringProjectPublication?> GetPublicationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_publication is not null &&
                _publication.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase)
                    ? _publication
                    : null);

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(
            string projectKey,
            long revision,
            string? publishedBy = null,
            CancellationToken cancellationToken = default)
        {
            if (!_revisions.TryGetValue(revision, out var snapshot) ||
                !snapshot.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<EngineeringProjectPublication?>(null);
            _publication = new EngineeringProjectPublication(projectKey, revision, DateTimeOffset.UtcNow, publishedBy);
            return Task.FromResult<EngineeringProjectPublication?>(_publication);
        }

        public Task<EngineeringProjectActivation?> GetActivationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_activation is not null &&
                _activation.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase)
                    ? _activation
                    : null);

        public Task<EngineeringProjectActivation?> RecordActivationAsync(
            string projectKey,
            long revision,
            string? activatedBy = null,
            CancellationToken cancellationToken = default)
        {
            if (_publication?.PublishedRevision != revision || !_revisions.ContainsKey(revision))
                return Task.FromResult<EngineeringProjectActivation?>(null);
            _activation = new EngineeringProjectActivation(projectKey, revision, DateTimeOffset.UtcNow, activatedBy);
            return Task.FromResult<EngineeringProjectActivation?>(_activation);
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
