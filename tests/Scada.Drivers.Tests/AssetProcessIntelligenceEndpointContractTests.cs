using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scada.Api.Historian;
using Scada.Api.Persistence;
using Scada.Api.Licensing;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Assets;
using Scada.Core.Alarms;
using Scada.Core.Abstractions;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
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
using Scada.Historian.TimescaleDb;
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
    public async Task ContextEligibilityCatalogDeclaresReadOnlyBoundedContract()
    {
        await using var api = await EndpointHarness.StartAsync(CreateVerification("valid"), includeAnalyticsEndpoints: true);

        using var response = await api.Client.GetAsync(EndpointHarness.ContextEligibilityV1CatalogPath);
        var catalog = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, catalog.GetProperty("contractVersion").GetInt32());
        Assert.Equal("context-eligibility.v1", catalog.GetProperty("ruleId").GetString());
        Assert.Equal(16, catalog.GetProperty("maximumSignals").GetInt32());
        Assert.Equal(16, catalog.GetProperty("maximumPredicates").GetInt32());
        Assert.Equal(50000, catalog.GetProperty("maximumRawRowsAcrossAllSignalsAndWindows").GetInt32());
        Assert.Contains("no nearest join", catalog.GetProperty("sourceTimestampRule").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExperimentalJointDeviationCatalogDeclaresExperimentalUnclassifiedReadOnlyContract()
    {
        await using var api = await EndpointHarness.StartAsync(CreateVerification("valid"), includeAnalyticsEndpoints: true);

        using var response = await api.Client.GetAsync(EndpointHarness.ExperimentalJointDeviationV1CatalogPath);
        var catalog = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("experimental-joint-deviation.v1", catalog.GetProperty("ruleId").GetString());
        Assert.Equal(2, catalog.GetProperty("minimumTargetSignals").GetInt32());
        Assert.Equal(8, catalog.GetProperty("maximumTargetSignals").GetInt32());
        Assert.Equal(100, catalog.GetProperty("minimumReferenceInnovations").GetInt32());
        Assert.Equal(20, catalog.GetProperty("minimumReferenceInnovationsPerTargetSignal").GetInt32());
        Assert.Equal(50000, catalog.GetProperty("maximumRawRowsAcrossAllSignalsAndWindows").GetInt32());
        Assert.Equal("experimental_not_field_validated", catalog.GetProperty("validationStatus").GetString());
        Assert.Equal("none", catalog.GetProperty("classificationStatus").GetString());
        Assert.Equal(JsonValueKind.Null, catalog.GetProperty("decisionThreshold").ValueKind);
        Assert.Contains("no imputation", catalog.GetProperty("sourceTimestampRule").GetString(), StringComparison.Ordinal);
        Assert.Contains("must not be summed", catalog.GetProperty("evidenceCounterRule").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IntelligenceAnalyticsHttpEndpointsReadSyntheticEvidenceFromIsolatedPostgreSql()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var api = await EndpointHarness.StartAsync(
            CreateVerification("valid"),
            includeActivationPipeline: true,
            includeAnalyticsEndpoints: true,
            seedAnalyticsConfiguration: true,
            postgreSqlHistorianConnectionString: connectionString);

        using var activation = await api.Client.PostAsync(api.ActivatePath, content: null);
        Assert.Equal(HttpStatusCode.OK, activation.StatusCode);

        var now = DateTimeOffset.UtcNow;
        var minuteTicks = TimeSpan.TicksPerMinute;
        var anchor = new DateTimeOffset(now.AddHours(-20).Ticks / minuteTicks * minuteTicks, TimeSpan.Zero);
        var reference = new AssetProcessIntelligenceAnalyticsWindow(anchor, anchor.AddMinutes(179));
        var comparison = new AssetProcessIntelligenceAnalyticsWindow(anchor.AddMinutes(240), anchor.AddMinutes(419));
        var evaluation = new AssetProcessIntelligenceAnalyticsWindow(anchor.AddMinutes(600), anchor.AddMinutes(749));
        var random = new Random(712);
        var state = new double[2];
        var simulatedValues = new (double Current, double Flow)[750];
        for (var minute = 0; minute < simulatedValues.Length; minute++)
        {
            var sharedNoise = random.NextDouble() - 0.5;
            state[0] = 0.4 * state[0] + sharedNoise;
            state[1] = 0.3 * state[1] + 0.35 * sharedNoise + random.NextDouble() - 0.5;
            simulatedValues[minute] = (state[0], state[1]);
        }

        var written = 0;

        for (var minute = 0; minute <= 749; minute++)
        {
            if (minute is > 179 and < 240 or > 419 and < 600) continue;
            var timestamp = anchor.AddMinutes(minute);
            var shiftedEvaluation = minute >= 600;
            var current = simulatedValues[minute].Current + (shiftedEvaluation ? 0.25d : 0d);
            var flow = simulatedValues[minute].Flow;
            await api.PublishSyntheticSampleAsync(api.TagId, timestamp, current);
            await api.PublishSyntheticSampleAsync(api.SecondTargetTagId, timestamp, flow);
            await api.PublishSyntheticSampleAsync(api.ContextTagId, timestamp, "RUN");
            written += 3;
        }

        await api.WaitForSyntheticSamplesAsync(written);

        var referenceRequest = new AssetProcessIntelligenceReferenceEvidenceV1QueryRequest(
            AssetProcessIntelligenceScopeKind.Equipment,
            api.EquipmentId,
            api.AnalyticsProfileId,
            reference,
            comparison);
        using var referenceResponse = await api.Client.PostAsJsonAsync(
            AssetProcessIntelligenceAnalyticsApi.Route + "/reference-evidence/v1/query",
            referenceRequest);
        var referenceEvidence = await referenceResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(referenceResponse.StatusCode == HttpStatusCode.OK,
            $"Reference evidence endpoint returned {(int)referenceResponse.StatusCode}: {referenceEvidence}");
        Assert.Equal(3, referenceEvidence.GetProperty("signals").GetArrayLength());
        Assert.Equal(comparison.FromUtc, referenceEvidence.GetProperty("comparisonWindow").GetProperty("fromUtc").GetDateTimeOffset());
        Assert.Equal(comparison.ToUtc, referenceEvidence.GetProperty("comparisonWindow").GetProperty("toUtc").GetDateTimeOffset());
        Assert.Equal(180, referenceEvidence.GetProperty("referenceCohort").GetProperty("completeSelectedNumericCohortCount").GetInt32());
        Assert.Equal(180, referenceEvidence.GetProperty("comparisonCohort").GetProperty("completeSelectedNumericCohortCount").GetInt32());

        var eligibilityRequest = new AssetProcessIntelligenceContextEligibilityV1QueryRequest(
            AssetProcessIntelligenceScopeKind.Equipment,
            api.EquipmentId,
            api.AnalyticsAssignmentId,
            api.OperatingContextProfileId,
            ExpectedProfileVersion: 1,
            reference,
            comparison);
        using var eligibilityResponse = await api.Client.PostAsJsonAsync(
            AssetProcessIntelligenceAnalyticsApi.Route + "/context-eligibility/v1/query",
            eligibilityRequest);
        var eligibility = await eligibilityResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, eligibilityResponse.StatusCode);
        Assert.Equal(comparison.FromUtc, eligibility.GetProperty("comparisonWindow").GetProperty("fromUtc").GetDateTimeOffset());
        Assert.Equal(comparison.ToUtc, eligibility.GetProperty("comparisonWindow").GetProperty("toUtc").GetDateTimeOffset());
        Assert.Equal(180, eligibility.GetProperty("predicates").EnumerateArray().Single().GetProperty("referenceMatchCount").GetInt32());
        Assert.Equal(180, eligibility.GetProperty("predicates").EnumerateArray().Single().GetProperty("comparisonMatchCount").GetInt32());

        var jointRequest = new AssetProcessIntelligenceExperimentalJointDeviationV1QueryRequest(
            AssetProcessIntelligenceScopeKind.Equipment,
            api.EquipmentId,
            api.AnalyticsAssignmentId,
            api.OperatingContextProfileId,
            ExpectedProfileVersion: 1,
            api.AnalyticsTargetSignalIds.ToArray(),
            ExpectedCadenceSeconds: 60,
            reference,
            evaluation);
        using var jointResponse = await api.Client.PostAsJsonAsync(
            EndpointHarness.ExperimentalJointDeviationV1QueryPath,
            jointRequest);
        var joint = await jointResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, jointResponse.StatusCode);
        Assert.Equal("experimental_not_field_validated", joint.GetProperty("validationStatus").GetString());
        Assert.Equal("none", joint.GetProperty("classificationStatus").GetString());
        Assert.Equal(JsonValueKind.Null, joint.GetProperty("decisionThreshold").ValueKind);
        Assert.Equal("available", joint.GetProperty("referenceFitStatus").GetString());
        Assert.Equal(180, joint.GetProperty("referenceCompleteCohortCount").GetInt32());
        Assert.Equal(150, joint.GetProperty("evaluationCompleteCohortCount").GetInt32());
        Assert.True(joint.GetProperty("evaluationScoredCohortCount").GetInt32() > 0, joint.ToString());
        Assert.True(joint.GetProperty("scores").GetArrayLength() > 0, joint.ToString());
    }

    [Fact]
    public async Task ExperimentalJointDeviationQueryRejectsInvalidCadenceOverHttpBeforeHistorianRead()
    {
        await using var api = await EndpointHarness.StartAsync(CreateVerification("valid"), includeAnalyticsEndpoints: true);
        var request = new AssetProcessIntelligenceExperimentalJointDeviationV1QueryRequest(
            AssetProcessIntelligenceScopeKind.Equipment,
            api.EquipmentId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ExpectedProfileVersion: 1,
            TargetSignalIds: [Guid.NewGuid(), Guid.NewGuid()],
            ExpectedCadenceSeconds: 0,
            ReferenceWindow: new(
                DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
                DateTimeOffset.Parse("2026-10-01T01:00:00Z")),
            EvaluationWindow: new(
                DateTimeOffset.Parse("2026-10-01T02:00:00Z"),
                DateTimeOffset.Parse("2026-10-01T03:00:00Z")));

        using var response = await api.Client.PostAsJsonAsync(EndpointHarness.ExperimentalJointDeviationV1QueryPath, request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "INTELLIGENCE_EXPERIMENTAL_JOINT_DEVIATION_CADENCE_INVALID",
            payload.GetProperty("code").GetString());
        Assert.Equal("The analytics query is invalid.", payload.GetProperty("error").GetString());
        Assert.Equal(0, api.HistoricalQueries.Calls);
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
    public async Task OperatingContextProfilesAreVersionedOptimisticallyAndCannotBeOverwrittenStale()
    {
        await using var api = await EndpointHarness.StartAsync(CreateVerification("valid"));
        var profileId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var contextId = Guid.NewGuid();
        var measurement = new AssetProcessIntelligenceProfileEngineeringDto(profileId, "pump", "Pump",
            [new(Guid.NewGuid(), "temperature", "process", api.TagId)]);
        var assignment = new AssetProcessIntelligenceAssignmentEngineeringDto(assignmentId, api.EquipmentId, profileId);
        var context = new AssetProcessIntelligenceOperatingContextProfileSaveRequest(contextId, "normal", "Normal",
            assignmentId,
            [new(api.TagId, AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval, 5, 12)]);
        var create = new AssetProcessIntelligenceConfigurationRequest(
            [measurement], [assignment], [], SemanticContractVersion: 1,
            OperatingContextProfiles: [context],
            ExpectedOperatingContextProfileVersions: []);

        using var created = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath, create);

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var current = api.Workspace.AssetProcessIntelligence.Snapshot();
        Assert.Equal(3, current.Version);
        Assert.Equal(1, Assert.Single(current.OperatingContextProfiles!).Version);

        var unchanged = create with
        {
            ExpectedOperatingContextProfileVersions = [new(contextId, 1)]
        };
        using var unchangedResponse = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath, unchanged);
        Assert.Equal(HttpStatusCode.OK, unchangedResponse.StatusCode);
        Assert.Equal(1, Assert.Single(api.Workspace.AssetProcessIntelligence.Snapshot().OperatingContextProfiles!).Version);

        var changedContext = context with
        {
            Predicates = [new(api.TagId, AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval, 5, 13)]
        };
        var changed = unchanged with { OperatingContextProfiles = [changedContext] };
        using var changedResponse = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath, changed);
        Assert.Equal(HttpStatusCode.OK, changedResponse.StatusCode);
        Assert.Equal(2, Assert.Single(api.Workspace.AssetProcessIntelligence.Snapshot().OperatingContextProfiles!).Version);

        var snapshotBeforeStaleSave = JsonSerializer.Serialize(api.Workspace.AssetProcessIntelligence.Snapshot());
        var changeVersionBeforeStaleSave = api.Workspace.CaptureChangeVersion();
        var stale = changed with
        {
            OperatingContextProfiles = [changedContext with { DisplayLabel = "Stale edit" }],
            ExpectedOperatingContextProfileVersions = [new(contextId, 1)]
        };
        using var staleResponse = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath, stale);
        var staleBody = await staleResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        Assert.Equal("INTELLIGENCE_OPERATING_CONTEXT_PROFILE_VERSION_CONFLICT", staleBody.GetProperty("error").GetString());
        Assert.Equal(snapshotBeforeStaleSave, JsonSerializer.Serialize(api.Workspace.AssetProcessIntelligence.Snapshot()));
        Assert.Equal(changeVersionBeforeStaleSave, api.Workspace.CaptureChangeVersion());
    }

    [Fact]
    public async Task OperatingContextAuthoringRequiresResourceScopedManagerGrant()
    {
        await using var api = await EndpointHarness.StartAsync(
            CreateVerification("valid"), managerGrantOnTargetEquipment: false);
        var profileId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var request = new AssetProcessIntelligenceConfigurationRequest(
            [new(profileId, "pump", "Pump", [new(Guid.NewGuid(), "temperature", "process", api.TagId)])],
            [new(assignmentId, api.EquipmentId, profileId)],
            [],
            SemanticContractVersion: 1,
            OperatingContextProfiles:
            [new(Guid.NewGuid(), "normal", "Normal", assignmentId,
                [new(api.TagId, AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval, 5, 12)])],
            ExpectedOperatingContextProfileVersions: []);

        using var response = await api.Client.PutAsJsonAsync(EndpointHarness.ConfigurationPath, request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, api.Workspace.AssetProcessIntelligence.Snapshot().Version);
        Assert.Empty(api.Workspace.AssetProcessIntelligence.Snapshot().OperatingContextProfiles!);
        Assert.Empty(api.Workspace.AssetProcessIntelligence.Snapshot().Assignments!);
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
        public const string ContextEligibilityV1CatalogPath = AssetProcessIntelligenceAnalyticsApi.Route + "/context-eligibility/v1/catalog";
        public const string ExperimentalJointDeviationV1CatalogPath = AssetProcessIntelligenceAnalyticsApi.Route + "/experimental-joint-deviation/v1/catalog";
        public const string ExperimentalJointDeviationV1QueryPath = AssetProcessIntelligenceAnalyticsApi.Route + "/experimental-joint-deviation/v1/query";

        private readonly WebApplication _app;
        private readonly IEngineeringProjectPersistenceService? _persistence;
        private readonly IEngineeringExchangeService? _exchange;
        private readonly InMemoryScadaEventBus _eventBus;
        private readonly TimescaleDbHistorian? _databaseHistorian;
        private EndpointHarness(
            WebApplication app,
            HttpClient client,
            Guid equipmentId,
            Guid tagId,
            EngineeringWorkspace workspace,
            AssetProcessIntelligenceExecutionGate gate,
            IEngineeringRuntimeCoordinator runtime,
            InMemoryAuditSink audit,
            RecordingHistoricalQueryService historicalQueries,
            InMemoryScadaEventBus eventBus,
            TimescaleDbHistorian? databaseHistorian,
            Guid? secondTargetTagId,
            Guid? contextTagId,
            Guid? analyticsProfileId,
            Guid? analyticsAssignmentId,
            Guid? operatingContextProfileId,
            IReadOnlyCollection<Guid>? analyticsTargetSignalIds,
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
            HistoricalQueries = historicalQueries;
            _eventBus = eventBus;
            _databaseHistorian = databaseHistorian;
            SecondTargetTagId = secondTargetTagId ?? Guid.Empty;
            ContextTagId = contextTagId ?? Guid.Empty;
            AnalyticsProfileId = analyticsProfileId ?? Guid.Empty;
            AnalyticsAssignmentId = analyticsAssignmentId ?? Guid.Empty;
            OperatingContextProfileId = operatingContextProfileId ?? Guid.Empty;
            AnalyticsTargetSignalIds = analyticsTargetSignalIds ?? Array.Empty<Guid>();
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
        public RecordingHistoricalQueryService HistoricalQueries { get; }
        public Guid SecondTargetTagId { get; }
        public Guid ContextTagId { get; }
        public Guid AnalyticsProfileId { get; }
        public Guid AnalyticsAssignmentId { get; }
        public Guid OperatingContextProfileId { get; }
        public IReadOnlyCollection<Guid> AnalyticsTargetSignalIds { get; }
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
            bool includeAnalyticsEndpoints = false,
            bool seedAnalyticsConfiguration = false,
            string? postgreSqlHistorianConnectionString = null)
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
            Guid? secondTargetTagId = null;
            Guid? contextTagId = null;
            Guid? analyticsProfileId = null;
            Guid? analyticsAssignmentId = null;
            Guid? operatingContextProfileId = null;
            Guid? targetSignalOneId = null;
            Guid? targetSignalTwoId = null;
            if (seedAnalyticsConfiguration)
            {
                secondTargetTagId = Guid.NewGuid();
                contextTagId = Guid.NewGuid();
                analyticsProfileId = Guid.NewGuid();
                analyticsAssignmentId = Guid.NewGuid();
                operatingContextProfileId = Guid.NewGuid();
                targetSignalOneId = Guid.NewGuid();
                targetSignalTwoId = Guid.NewGuid();
                var contextSignalId = Guid.NewGuid();
                workspace.Tags.Register(new TagDefinition(
                    secondTargetTagId.Value,
                    "Flow",
                    "Plant.Pump.Flow",
                    TagDataType.Double,
                    null,
                    "m3/h",
                    null,
                    true));
                workspace.Tags.Register(new TagDefinition(
                    contextTagId.Value,
                    "Operating mode",
                    "Plant.Pump.Mode",
                    TagDataType.Enum,
                    null,
                    null,
                    null,
                    true));
                workspace.AssetProcessIntelligence.Replace(new AssetProcessIntelligenceEngineeringDto(
                    Version: 3,
                    Scopes: [new(AssetProcessIntelligenceScopeKind.Equipment, equipmentId)],
                    Profiles:
                    [
                        new(analyticsProfileId.Value, "pump-measurements", "Pump measurements",
                        [
                            new(targetSignalOneId.Value, "current", "Current", tagId),
                            new(targetSignalTwoId.Value, "flow", "Flow", secondTargetTagId.Value),
                            new(contextSignalId, "operating-mode", "Operating mode", contextTagId.Value)
                        ])
                    ],
                    Assignments: [new(analyticsAssignmentId.Value, equipmentId, analyticsProfileId.Value)],
                    OperatingContextProfiles:
                    [
                        new(operatingContextProfileId.Value, "normal-running", "Normal running", analyticsAssignmentId.Value, 1,
                        [new(contextTagId.Value, AssetProcessIntelligenceOperatingContextPredicateKind.EnumEquals,
                            ExactValue: "RUN")])
                    ]));
            }
            else
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
                    Grants: CreateGrants(managerGrantOnTargetEquipment, engineeringModifyGranted, equipmentScopeId, otherScopeId, engineeringViewGranted, includeAnalyticsEndpoints))],
                [
                    new SecurityScopeEngineeringDto(equipmentScopeId, "equipment-p01", "Pump", SecurityScopeNodeKind.Equipment, ResourceId: equipmentId),
                    new SecurityScopeEngineeringDto(otherScopeId, "equipment-p02", "Fan", SecurityScopeNodeKind.Equipment, ResourceId: otherEquipmentId)
                ]);
            var gate = new AssetProcessIntelligenceExecutionGate();
            var eventBus = new InMemoryScadaEventBus();
            IEngineeringRuntimeCoordinator runtime = new TestRuntimeCoordinator(
                ProjectKey,
                1,
                seedAnalyticsConfiguration ? workspace.Tags.Snapshot() : Array.Empty<TagDefinition>());
            IEngineeringProjectPersistenceService? persistence = null;
            TimescaleDbHistorian? databaseHistorian = null;
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
                if (!seedAnalyticsConfiguration)
                {
                    var liveRuntime = new EngineeringRuntimeCoordinator(
                        eventBus,
                        new EngineeringDriverCompiler(),
                        TimeSpan.FromSeconds(2));
                    var initialRuntime = await liveRuntime.ActivateAsync(
                        ProjectKey,
                        seeded.Revision,
                        exchange.ParseJson(seeded.EngineeringJson));
                    Assert.True(initialRuntime.Activated);
                    runtime = liveRuntime;
                }
            }
            if (!string.IsNullOrWhiteSpace(postgreSqlHistorianConnectionString))
                databaseHistorian = new TimescaleDbHistorian(eventBus, postgreSqlHistorianConnectionString, batchSize: 100);
            var sink = new InMemoryAuditSink();
            var historicalQueries = new RecordingHistoricalQueryService();
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
            {
                if (string.IsNullOrWhiteSpace(postgreSqlHistorianConnectionString))
                    builder.Services.AddSingleton<IHistoricalQueryService>(historicalQueries);
                else
                {
                    var provider = new TimescaleHistoricalQueryProvider(postgreSqlHistorianConnectionString, workspace.Tags);
                    builder.Services.AddSingleton(provider);
                    builder.Services.AddSingleton<IHistoricalDatasetProvider>(provider);
                    builder.Services.AddSingleton<IHistoricalDatasetProvider>(new TimescaleHistorianV2TestAdapter(provider));
                    builder.Services.AddSingleton(new HistoricalQueryCursorCodec(RandomNumberGenerator.GetBytes(32)));
                    builder.Services.AddHttpContextAccessor();
                    builder.Services.AddSingleton<IHistoricalQueryAuthorizer>(sp => new ApiHistoricalQueryAuthorizer(
                        sp.GetRequiredService<IHttpContextAccessor>(),
                        sp.GetRequiredService<ApiAuthorizationService>(),
                        sp.GetRequiredService<ScadaRuntimeFacade>()));
                    builder.Services.AddScoped<IHistoricalQueryService, HistoricalQueryService>();
                }
                builder.Services.AddTransient<AssetProcessIntelligenceAnalyticsService>();
            }
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
                historicalQueries,
                eventBus,
                databaseHistorian,
                secondTargetTagId,
                contextTagId,
                analyticsProfileId,
                analyticsAssignmentId,
                operatingContextProfileId,
                targetSignalOneId.HasValue && targetSignalTwoId.HasValue
                    ? [targetSignalOneId.Value, targetSignalTwoId.Value]
                    : Array.Empty<Guid>(),
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
            bool engineeringViewGranted,
            bool analyticsReadGranted)
        {
            var grants = new List<CapabilityGrantEngineeringDto>
            {
                new(SecurityCapability.AssetProcessIntelligenceManage,
                    new AuthorizationScopeEngineeringDto(ScopeNodeId: managerGrantOnTargetEquipment ? equipmentScopeId : otherScopeId))
            };
            if (engineeringViewGranted) grants.Add(new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringView));
            if (engineeringModifyGranted)
                grants.Add(new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringModify));
            if (analyticsReadGranted)
            {
                grants.Add(new CapabilityGrantEngineeringDto(SecurityCapability.TrendUse));
                grants.Add(new CapabilityGrantEngineeringDto(SecurityCapability.TagRead));
            }
            return grants;
        }

        public async Task PublishSyntheticSampleAsync(Guid tagId, DateTimeOffset timestamp, object value)
        {
            if (_databaseHistorian is null)
                throw new InvalidOperationException("The isolated PostgreSQL historian is not configured.");
            if (!Workspace.Tags.TryGet(tagId, out var tag) || tag is null)
                throw new InvalidOperationException("The synthetic TAG is not part of this test workspace.");
            var sample = new TagValue(tagId, value, timestamp, TagQuality.Good, "intelligence-ui-853")
            {
                SourceTimestamp = timestamp,
                SourceTimestampEvidenceState = TimestampEvidenceState.Synchronized
            };
            await _eventBus.PublishAsync(new TagValueChanged(tag, null, sample, timestamp));
        }

        public async Task WaitForSyntheticSamplesAsync(int expected)
        {
            if (_databaseHistorian is null)
                throw new InvalidOperationException("The isolated PostgreSQL historian is not configured.");
            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (_databaseHistorian.WrittenSamples < expected && DateTimeOffset.UtcNow < deadline)
            {
                if (_databaseHistorian.LastWriteError is { } error)
                    throw new InvalidOperationException("Synthetic Historian writes failed.", error);
                await Task.Delay(50);
            }
            Assert.Equal(expected, _databaseHistorian.WrittenSamples);
            Assert.Equal(0, _databaseHistorian.DroppedSamples);
            Assert.Null(_databaseHistorian.LastWriteError);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
            if (_databaseHistorian is not null)
                await _databaseHistorian.DisposeAsync();
            if (Runtime is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
        }

        private sealed class TimescaleHistorianV2TestAdapter(TimescaleHistoricalQueryProvider provider)
            : IHistoricalDatasetProvider
        {
            public string Dataset => HistoricalDatasets.HistorianSamplesV2;

            public Task<HistoricalProviderPage> QueryAsync(
                HistoricalQueryExecution query,
                CancellationToken cancellationToken = default) =>
                provider.QueryVersion2Async(query, cancellationToken);
        }

        public sealed class RecordingHistoricalQueryService : IHistoricalQueryService
        {
            public int Calls { get; private set; }

            public Task<HistoricalQueryResponse> QueryAsync(
                HistoricalQueryRequest request,
                CancellationToken cancellationToken = default)
            {
                Calls++;
                throw new InvalidOperationException("The invalid query must be rejected before the Historian is called.");
            }
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

    private sealed class TestRuntimeCoordinator : IEngineeringRuntimeCoordinator
    {
        private readonly List<TagDefinition> _tags;
        private RuntimeDescriptor _descriptor;

        public TestRuntimeCoordinator(
            string projectKey,
            long revision,
            IEnumerable<TagDefinition>? tags = null)
        {
            _tags = tags?.ToList() ?? [];
            _descriptor = new RuntimeDescriptor(
                projectKey,
                revision,
                DateTimeOffset.UtcNow,
                [],
                [],
                _tags.Count,
                0);
        }

        public int ProcessWriteCalls { get; private set; }
        public RuntimeDescriptor Describe() => _descriptor;
        public IReadOnlyCollection<TagDefinition> Tags() => _tags.ToArray();
        public IReadOnlyCollection<TagValue> CurrentValues() => [];
        public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => [];
        public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) => [];
        public IReadOnlyCollection<CommandDefinition> Commands() => [];
        public IReadOnlyCollection<ClientMemoryRuntimeSource> ClientMemorySources() => [];
        public bool TryGetTag(Guid tagId, out TagDefinition? tag)
        {
            tag = _tags.SingleOrDefault(candidate => candidate.Id == tagId);
            return tag is not null;
        }
        public bool TryGetTagByPath(string path, out TagDefinition? tag)
        {
            tag = _tags.SingleOrDefault(candidate => string.Equals(candidate.Path, path, StringComparison.OrdinalIgnoreCase));
            return tag is not null;
        }
        public bool TryGetCurrent(Guid tagId, out TagValue? value) { value = null; return false; }
        public bool TryGetCommand(Guid commandId, out CommandDefinition? command) { command = null; return false; }
        public bool IsServerMemoryTag(Guid tagId) => false;
        public ValueTask<bool> AcknowledgeAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> ShelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> UnshelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) { ProcessWriteCalls++; return ValueTask.CompletedTask; }
        public ValueTask ResetServerMemoryRetainedValueAsync(Guid tagId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ExecuteCommandAsync(Guid commandId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public Task<RuntimeActivationResult> ActivateAsync(
            string projectKey,
            long revision,
            EngineeringPackage package,
            CancellationToken cancellationToken = default) =>
            ActivateAsync(projectKey, revision, package, (_, _) => Task.CompletedTask, cancellationToken);

        public async Task<RuntimeActivationResult> ActivateAsync(
            string projectKey,
            long revision,
            EngineeringPackage package,
            Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(package);
            ArgumentNullException.ThrowIfNull(commitAsync);
            cancellationToken.ThrowIfCancellationRequested();
            var activatedAtUtc = DateTimeOffset.UtcNow;
            await commitAsync(new RuntimeActivationCommitContext(projectKey, revision, activatedAtUtc), cancellationToken);
            _descriptor = new RuntimeDescriptor(projectKey, revision, activatedAtUtc, [], [], _tags.Count, 0);
            return new RuntimeActivationResult(
                projectKey,
                revision,
                true,
                Array.Empty<EngineeringDriverIssue>(),
                Array.Empty<RuntimeActivationIssue>(),
                activatedAtUtc);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
