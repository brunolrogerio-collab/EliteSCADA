using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.Security;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class AlarmSuppressionEndpointTests
{
    [Theory]
    [InlineData("global", false, RuntimeConnectionClass.Interactive, HttpStatusCode.OK)]
    [InlineData("matching", false, RuntimeConnectionClass.Interactive, HttpStatusCode.OK)]
    [InlineData("matching", true, RuntimeConnectionClass.Interactive, HttpStatusCode.OK)]
    [InlineData("unrelated", false, RuntimeConnectionClass.Interactive, HttpStatusCode.Forbidden)]
    [InlineData("none", false, RuntimeConnectionClass.Interactive, HttpStatusCode.Forbidden)]
    [InlineData("global", false, RuntimeConnectionClass.ViewOnly, HttpStatusCode.Forbidden)]
    public async Task Alarm_suppression_requires_alarm_shelve_scope_and_interactive_session(
        string grantScope,
        bool linkAlarmToEquipment,
        RuntimeConnectionClass connectionClass,
        HttpStatusCode expectedStatusCode)
    {
        var projectKey = $"suppression-endpoint-success-{Guid.NewGuid():N}";
        var alarmId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();
        var activatedAt = DateTimeOffset.UtcNow;
        var definition = AlarmDefinition.Create(
            "Pump alarm", tagId, AlarmType.High, AlarmPriority.High,
            locationId: linkAlarmToEquipment ? null : locationId,
            equipmentId: linkAlarmToEquipment ? equipmentId : null) with { Id = alarmId, Area = "Plant" };
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            activatedAt,
            [],
            [new AlarmEngineeringDto(
                alarmId, "Pump alarm", tagId, "Plant.Pump01.High", AlarmType.High,
                AlarmPriority.High, 10, Area: "Plant",
                LocationId: linkAlarmToEquipment ? null : locationId,
                EquipmentId: linkAlarmToEquipment ? equipmentId : null)],
            Equipment: linkAlarmToEquipment
                ? [new EquipmentEngineeringDto(equipmentId, "Plant.Pump01", "Pump 01", LocationId: locationId)]
                : [],
            Locations: [new LocationEngineeringDto(locationId, "Plant")]);
        var runtime = new ScadaRuntimeFacade(
            new TestRuntimeCoordinator(projectKey, 7, activatedAt, [definition]),
            applicationProjection: new TestApplicationProjection(package));
        var store = new RecordingSuppressionStore();
        var audit = new InMemoryAuditSink();
        var authorityAreaScopeId = Guid.NewGuid();
        var authorityResourceScopeId = Guid.NewGuid();
        var scopeResourceId = grantScope == "matching"
            ? linkAlarmToEquipment ? equipmentId : locationId
            : Guid.NewGuid();
        var hasScopeGrant = grantScope is "matching" or "unrelated";
        var hasCapabilityGrant = grantScope != "none";
        var operatorRole = new SecurityRoleEngineeringDto(
            Guid.NewGuid(),
            "operator",
            "Operator",
            Grants: hasCapabilityGrant
                ? [new CapabilityGrantEngineeringDto(
                    SecurityCapability.AlarmShelve,
                    hasScopeGrant
                        ? new AuthorizationScopeEngineeringDto(
                            ScopeNodeId: authorityAreaScopeId,
                            IncludeDescendants: true)
                        : null)]
                : []);
        var authority = new InMemoryAuthorityPolicyStore(
            [operatorRole],
            hasScopeGrant
                ? [
                    new SecurityScopeEngineeringDto(
                        authorityAreaScopeId,
                        grantScope == "unrelated" ? "unrelated-area" : "alarm-area",
                        grantScope == "unrelated" ? "Unrelated area" : "Alarm area",
                        SecurityScopeNodeKind.Area),
                    new SecurityScopeEngineeringDto(
                        authorityResourceScopeId,
                        linkAlarmToEquipment ? "pump-01" : "plant-location",
                        linkAlarmToEquipment ? "Pump 01" : "Plant location",
                        linkAlarmToEquipment ? SecurityScopeNodeKind.Equipment : SecurityScopeNodeKind.Location,
                        ParentId: grantScope == "matching" ? authorityAreaScopeId : null,
                        ResourceId: scopeResourceId)]
                : []);
        var projectSnapshot = new EngineeringProjectSnapshot(
            7,
            projectKey,
            "Alarm suppression test",
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            activatedAt,
            "{}");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test", Args = [] });
        builder.Configuration["Authentication:Enabled"] = "true";
        builder.Services.AddSingleton(runtime);
        builder.Services.AddSingleton<IAlarmSuppressionStore>(store);
        builder.Services.AddSingleton<AlarmSuppressionRuntimeProjection>();
        builder.Services.AddSingleton<IAuthorityPolicyStore>(authority);
        builder.Services.AddSingleton<IEngineeringProjectPersistenceService>(new TestProjectPersistenceService(projectSnapshot));
        builder.Services.AddSingleton<ApiAuthorizationService>(sp => new ApiAuthorizationService(
            sp,
            authority,
            sp.GetRequiredService<IConfiguration>()));
        builder.Services.AddSingleton<IAuditSink>(audit);
        builder.Services.AddSingleton<IAuditStore>(audit);
        builder.Services.AddSingleton<ApiAuditService>(_ => new ApiAuditService(
            audit,
            audit,
            NullLogger<ApiAuditService>.Instance));

        await using var app = builder.Build();
        app.Use((context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "operator-1"), new Claim("role", operatorRole.Key)],
                "alarm-suppression-test"));
            return next();
        });
        app.MapAlarmSuppressionEndpoints();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var http = new HttpClient
        {
            BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single())
        };
        var authorization = app.Services.GetRequiredService<ApiAuthorizationService>();
        var lease = await authorization.RuntimeSessions.AdmitAsync(
            "operator-1",
            "alarm-suppression-test-client",
            connectionClass,
            runtime.Describe());
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/alarms/suppressions/alarm/{alarmId:D}")
        {
            Content = JsonContent.Create(new SetAlarmSuppressionRequest(AlarmSuppressionReasonCodes.Maintenance))
        };
        request.Headers.Add(ApiAuthorizationService.RuntimeSessionHeaderName, lease.SessionId.ToString("D"));
        request.Headers.Add(ApiAuthorizationService.RuntimeSessionClientInstanceHeaderName, lease.ClientInstanceId);

        using var response = await http.SendAsync(request);
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal(expectedStatusCode == HttpStatusCode.OK ? 1 : 0, store.SetCalls);
        if (expectedStatusCode != HttpStatusCode.OK)
        {
            Assert.Empty(store.Records);
            return;
        }

        var record = Assert.Single(store.Records);
        Assert.Equal(projectKey, record.ProjectKey);
        Assert.Equal(AlarmSuppressionTargetKind.Alarm, record.TargetKind);
        Assert.Equal(alarmId, record.TargetId);
        Assert.Equal(AlarmSuppressionReasonCodes.Maintenance, record.ReasonCode);
        Assert.Equal("operator-1", record.Actor);
        var responseBody = await response.Content.ReadFromJsonAsync<SuppressionMutationResponse>();
        Assert.NotNull(responseBody);
        Assert.True(responseBody.Changed);
        Assert.Equal(1, responseBody.AffectedAlarmCount);
    }

    [Fact]
    public async Task Location_suppression_is_atomic_when_its_alarms_cross_authority_scopes()
    {
        var projectKey = $"suppression-endpoint-partial-scope-{Guid.NewGuid():N}";
        var rootLocationId = Guid.NewGuid();
        var childLocationId = Guid.NewGuid();
        var rootAlarmId = Guid.NewGuid();
        var childAlarmId = Guid.NewGuid();
        var rootTagId = Guid.NewGuid();
        var childTagId = Guid.NewGuid();
        var activatedAt = DateTimeOffset.UtcNow;
        var definitions = new AlarmDefinition[]
        {
            AlarmDefinition.Create("Root alarm", rootTagId, AlarmType.High, AlarmPriority.High,
                locationId: rootLocationId) with { Id = rootAlarmId, Area = "Plant" },
            AlarmDefinition.Create("Child alarm", childTagId, AlarmType.High, AlarmPriority.High,
                locationId: childLocationId) with { Id = childAlarmId, Area = "Plant" }
        };
        var application = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            activatedAt,
            [],
            [
                new AlarmEngineeringDto(rootAlarmId, "Root alarm", rootTagId, "Plant.Root", AlarmType.High, AlarmPriority.High, 10,
                    Area: "Plant", LocationId: rootLocationId),
                new AlarmEngineeringDto(childAlarmId, "Child alarm", childTagId, "Plant.Root.Child", AlarmType.High, AlarmPriority.High, 10,
                    Area: "Plant", LocationId: childLocationId)
            ],
            Locations: [
                new LocationEngineeringDto(rootLocationId, "Plant"),
                new LocationEngineeringDto(childLocationId, "Subarea", rootLocationId)
            ]);
        var runtime = new ScadaRuntimeFacade(
            new TestRuntimeCoordinator(projectKey, 7, activatedAt, definitions),
            applicationProjection: new TestApplicationProjection(application));
        var store = new RecordingSuppressionStore();
        var audit = new InMemoryAuditSink();
        var allowedAuthorityAreaId = Guid.NewGuid();
        var deniedAuthorityAreaId = Guid.NewGuid();
        var operatorRole = new SecurityRoleEngineeringDto(
            Guid.NewGuid(),
            "operator",
            "Operator",
            Grants: [new CapabilityGrantEngineeringDto(
                SecurityCapability.AlarmShelve,
                new AuthorizationScopeEngineeringDto(
                    ScopeNodeId: allowedAuthorityAreaId,
                    IncludeDescendants: true))]);
        var authority = new InMemoryAuthorityPolicyStore(
            [operatorRole],
            [
                new SecurityScopeEngineeringDto(
                    allowedAuthorityAreaId, "utilities", "Utilities", SecurityScopeNodeKind.Area),
                new SecurityScopeEngineeringDto(
                    Guid.NewGuid(), "root-location", "Plant", SecurityScopeNodeKind.Location,
                    ParentId: allowedAuthorityAreaId, ResourceId: rootLocationId),
                new SecurityScopeEngineeringDto(
                    deniedAuthorityAreaId, "process-area", "Process area", SecurityScopeNodeKind.Area),
                new SecurityScopeEngineeringDto(
                    Guid.NewGuid(), "child-location", "Subarea", SecurityScopeNodeKind.Location,
                    ParentId: deniedAuthorityAreaId, ResourceId: childLocationId)
            ]);
        var projectSnapshot = new EngineeringProjectSnapshot(
            7, projectKey, "Alarm suppression scope test", EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion, activatedAt, "{}");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test", Args = [] });
        builder.Configuration["Authentication:Enabled"] = "true";
        builder.Services.AddSingleton(runtime);
        builder.Services.AddSingleton<IAlarmSuppressionStore>(store);
        builder.Services.AddSingleton<AlarmSuppressionRuntimeProjection>();
        builder.Services.AddSingleton<IAuthorityPolicyStore>(authority);
        builder.Services.AddSingleton<IEngineeringProjectPersistenceService>(new TestProjectPersistenceService(projectSnapshot));
        builder.Services.AddSingleton<ApiAuthorizationService>(sp => new ApiAuthorizationService(
            sp, authority, sp.GetRequiredService<IConfiguration>()));
        builder.Services.AddSingleton<IAuditSink>(audit);
        builder.Services.AddSingleton<IAuditStore>(audit);
        builder.Services.AddSingleton<ApiAuditService>(_ => new ApiAuditService(
            audit, audit, NullLogger<ApiAuditService>.Instance));

        await using var app = builder.Build();
        app.Use((context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "operator-1"), new Claim("role", operatorRole.Key)],
                "alarm-suppression-test"));
            return next();
        });
        app.MapAlarmSuppressionEndpoints();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var http = new HttpClient
        {
            BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single())
        };
        var authorization = app.Services.GetRequiredService<ApiAuthorizationService>();
        var lease = await authorization.RuntimeSessions.AdmitAsync(
            "operator-1", "alarm-suppression-scope-client", RuntimeConnectionClass.Interactive,
            runtime.Describe());
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/alarms/suppressions/location/{rootLocationId:D}")
        {
            Content = JsonContent.Create(new SetAlarmSuppressionRequest(AlarmSuppressionReasonCodes.Maintenance))
        };
        request.Headers.Add(ApiAuthorizationService.RuntimeSessionHeaderName, lease.SessionId.ToString("D"));
        request.Headers.Add(ApiAuthorizationService.RuntimeSessionClientInstanceHeaderName, lease.ClientInstanceId);

        using var response = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, store.SetCalls);
        Assert.Empty(store.Records);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Conflict)]
    [InlineData(true, HttpStatusCode.Unauthorized)]
    public async Task Location_suppression_is_rejected_without_partial_write_when_a_guard_fails(
        bool childAlarmAllowsSuppression,
        HttpStatusCode expectedStatusCode)
    {
        var projectKey = $"suppression-endpoint-{Guid.NewGuid():N}";
        var rootLocationId = Guid.NewGuid();
        var childLocationId = Guid.NewGuid();
        var rootAlarmId = Guid.NewGuid();
        var childAlarmId = Guid.NewGuid();
        var rootTagId = Guid.NewGuid();
        var childTagId = Guid.NewGuid();
        var activatedAt = DateTimeOffset.UtcNow;
        var definitions = new AlarmDefinition[]
        {
            AlarmDefinition.Create("Root alarm", rootTagId, AlarmType.High, AlarmPriority.High,
                locationId: rootLocationId) with { Id = rootAlarmId },
            AlarmDefinition.Create("Child alarm", childTagId, AlarmType.High, AlarmPriority.High,
                shelvingAllowed: childAlarmAllowsSuppression, locationId: childLocationId) with { Id = childAlarmId }
        };
        var application = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            activatedAt,
            [],
            [
                new AlarmEngineeringDto(rootAlarmId, "Root alarm", rootTagId, "Plant.Root", AlarmType.High, AlarmPriority.High, 10,
                    LocationId: rootLocationId),
                new AlarmEngineeringDto(childAlarmId, "Child alarm", childTagId, "Plant.Root.Child", AlarmType.High, AlarmPriority.High, 10,
                    ShelvingAllowed: childAlarmAllowsSuppression, LocationId: childLocationId)
            ],
            Locations: [
                new LocationEngineeringDto(rootLocationId, "Plant"),
                new LocationEngineeringDto(childLocationId, "Subarea", rootLocationId)
            ]);
        var runtime = new ScadaRuntimeFacade(
            new TestRuntimeCoordinator(projectKey, 7, activatedAt, definitions),
            applicationProjection: new TestApplicationProjection(application));
        var store = new RecordingSuppressionStore();
        var audit = new InMemoryAuditSink();
        var authority = new InMemoryAuthorityPolicyStore();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test", Args = [] });
        builder.Services.AddSingleton(runtime);
        builder.Services.AddSingleton<IAlarmSuppressionStore>(store);
        builder.Services.AddSingleton<AlarmSuppressionRuntimeProjection>();
        builder.Services.AddSingleton<IAuthorityPolicyStore>(authority);
        builder.Services.AddSingleton<ApiAuthorizationService>(sp => new ApiAuthorizationService(
            sp,
            authority,
            sp.GetRequiredService<IConfiguration>()));
        builder.Services.AddSingleton<IAuditSink>(audit);
        builder.Services.AddSingleton<IAuditStore>(audit);
        builder.Services.AddSingleton<ApiAuditService>(_ => new ApiAuditService(
            audit,
            audit,
            NullLogger<ApiAuditService>.Instance));

        await using var app = builder.Build();
        app.MapAlarmSuppressionEndpoints();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var http = new HttpClient
        {
            BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single())
        };

        using var response = await http.PutAsJsonAsync(
            $"/api/alarms/suppressions/location/{rootLocationId:D}",
            new SetAlarmSuppressionRequest(AlarmSuppressionReasonCodes.Maintenance));

        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal(0, store.SetCalls);
        Assert.Empty(store.Records);
    }

    private sealed class TestApplicationProjection(EngineeringPackage package) : IRuntimeApplicationProjectionProvider
    {
        public EngineeringPackage CaptureApplication() => package;
    }

    private sealed class TestProjectPersistenceService(EngineeringProjectSnapshot activeSnapshot)
        : IEngineeringProjectPersistenceService
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<EngineeringProjectSnapshot> SaveCurrentAsync(string projectKey, string projectName, string? savedBy = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EngineeringProjectSnapshot> SaveCurrentDerivedAsync(string projectKey, string projectName, long? basedOnRevision, string? savedBy = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(string projectKey, CancellationToken cancellationToken = default) => Task.FromResult<EngineeringProjectSnapshot?>(null);
        public Task<EngineeringProjectSnapshot?> LoadPublishedAsync(string projectKey, CancellationToken cancellationToken = default) => Task.FromResult<EngineeringProjectSnapshot?>(null);
        public Task<EngineeringProjectSnapshot?> LoadActiveAsync(string projectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(activeSnapshot.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase) ? activeSnapshot : null);
        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(string projectKey, int limit = 50, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EngineeringProjectLifecycle> GetLifecycleAsync(string projectKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EngineeringProjectActivation?> GetActivationAsync(string projectKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EngineeringProjectActivation?> RecordActivationAsync(string projectKey, long revision, string? activatedBy = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EngineeringPublicationResult?> PublishRevisionAsync(string projectKey, long revision, string? publishedBy = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EngineeringPersistencePreview?> PreviewLatestAsync(string projectKey, ImportMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EngineeringPersistencePreview?> PreviewRevisionAsync(string projectKey, long revision, ImportMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ImportResult?> ApplyLatestAsync(string projectKey, ImportMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ImportResult?> ApplyRevisionAsync(string projectKey, long revision, ImportMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingSuppressionStore : IAlarmSuppressionStore
    {
        private readonly Dictionary<(string ProjectKey, AlarmSuppressionTargetKind Kind, Guid TargetId), AlarmSuppressionRecord> _records = [];
        private readonly Dictionary<string, long> _revisions = new(StringComparer.Ordinal);
        public int SetCalls { get; private set; }
        public IReadOnlyCollection<AlarmSuppressionRecord> Records => _records.Values.ToArray();

        public Task<AlarmSuppressionStoreSnapshot> ReadSnapshotAsync(string projectKey, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            var records = _records.Values.Where(record => record.ProjectKey == projectKey &&
                (!record.ExpiresAtUtc.HasValue || record.ExpiresAtUtc > nowUtc)).ToArray();
            return Task.FromResult(new AlarmSuppressionStoreSnapshot(projectKey, _revisions.GetValueOrDefault(projectKey), records));
        }

        public async Task<IReadOnlyCollection<AlarmSuppressionRecord>> ReadActiveAsync(string projectKey, DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
            (await ReadSnapshotAsync(projectKey, nowUtc, cancellationToken)).Records;

        public Task<AlarmSuppressionMutationResult> SetAsync(AlarmSuppressionRecord record, CancellationToken cancellationToken = default)
        {
            SetCalls++;
            var key = (record.ProjectKey, record.TargetKind, record.TargetId);
            _records.TryGetValue(key, out var previous);
            var next = record with { Version = (previous?.Version ?? 0) + 1 };
            _records[key] = next;
            _revisions[record.ProjectKey] = _revisions.GetValueOrDefault(record.ProjectKey) + 1;
            return Task.FromResult(new AlarmSuppressionMutationResult(true, previous, next));
        }

        public Task<AlarmSuppressionMutationResult> ReleaseAsync(string projectKey, AlarmSuppressionTargetKind targetKind, Guid targetId, CancellationToken cancellationToken = default)
        {
            var key = (projectKey, targetKind, targetId);
            _records.Remove(key, out var previous);
            if (previous is not null) _revisions[projectKey] = _revisions.GetValueOrDefault(projectKey) + 1;
            return Task.FromResult(new AlarmSuppressionMutationResult(previous is not null, previous, null));
        }

        public Task ReplaceProjectSnapshotAsync(string projectKey, long revision, IReadOnlyCollection<AlarmSuppressionRecord> records, CancellationToken cancellationToken = default)
        {
            foreach (var key in _records.Keys.Where(key => key.ProjectKey == projectKey).ToArray()) _records.Remove(key);
            foreach (var record in records) _records[(projectKey, record.TargetKind, record.TargetId)] = record;
            _revisions[projectKey] = revision;
            return Task.CompletedTask;
        }
    }

    private sealed class TestRuntimeCoordinator(
        string projectKey,
        long revision,
        DateTimeOffset activatedAt,
        IReadOnlyCollection<AlarmDefinition> definitions) : IEngineeringRuntimeCoordinator
    {
        public RuntimeDescriptor Describe() => new(projectKey, revision, activatedAt, [], [], 0, 0);
        public IReadOnlyCollection<TagDefinition> Tags() => [];
        public IReadOnlyCollection<TagValue> CurrentValues() => [];
        public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => definitions;
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
        public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ResetServerMemoryRetainedValueAsync(Guid tagId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ExecuteCommandAsync(Guid commandId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public Task<RuntimeActivationResult> ActivateAsync(string projectKey, long revision, EngineeringPackage package, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RuntimeActivationResult> ActivateAsync(string projectKey, long revision, EngineeringPackage package, Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed record SuppressionMutationResponse(bool Changed, int AffectedAlarmCount);
}
