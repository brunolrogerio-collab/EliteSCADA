using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Api.Licensing;
using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Interactions;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Interactions;
using Scada.Engineering.Persistence;
using Scada.Engineering.Security;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class PersistedRuntimeRecoveryServiceTests
{
    [Fact]
    public async Task Recovery_UsesPersistedActiveRevisionAndRehydratesItsLockEvenWhenNewerRevisionIsPublished()
    {
        await using var activeServer = new TestModbusTcpServer();
        activeServer.HoldingRegisters[10] = 111;
        activeServer.Start();

        await using var publishedServer = new TestModbusTcpServer();
        publishedServer.HoldingRegisters[20] = 222;
        publishedServer.Start();

        var activeTagId = Guid.NewGuid();
        var publishedTagId = Guid.NewGuid();
        var activeLock = new EngineeringLockSecretService().Configure("active-recovery-secret", locked: true);
        var publishedLock = new EngineeringLockSecretService().Configure("newer-published-secret", locked: false);
        var activePackage = CreatePackage(activeServer.Port, activeTagId, "Plant.Active.Value", "holding:10") with
        {
            EngineeringLock = activeLock
        };
        activePackage = WithInteractions(
            activePackage,
            "active",
            out var activeEventId,
            out var activeCommandId);

        var publishedPackage = CreatePackage(publishedServer.Port, publishedTagId, "Plant.Published.Value", "holding:20") with
        {
            EngineeringLock = publishedLock
        };
        publishedPackage = WithInteractions(
            publishedPackage,
            "published",
            out var publishedEventId,
            out var publishedCommandId);
        var activeSnapshot = CreateSnapshot(1, activePackage);
        var publishedSnapshot = CreateSnapshot(2, publishedPackage);
        var store = new RecoveryStore(activeSnapshot, publishedSnapshot);

        var exchangeBus = new InMemoryScadaEventBus();
        using var exchangeAlarms = new InMemoryAlarmEngine(exchangeBus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), exchangeAlarms);
        var persistence = new EngineeringProjectPersistenceService(exchange, store);

        Assert.False(EngineeringLockContract.Normalize(exchange.ExportPackage().EngineeringLock).Locked);

        var runtimeBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            runtimeBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));
        var licensing = new TestProductLicenseService(ValidVerification());
        await using var authorityStore = new InMemoryRuntimeSessionLeaseStore();
        var interactions = new ActiveDriverInteractionRuntimeCatalog();
        var recovery = new PersistedRuntimeRecoveryService(
            persistence,
            exchange,
            runtime,
            licensing,
            authorityStore,
            driverInteractions: interactions);

        var result = await recovery.RecoverAsync("plant-a");

        Assert.True(result.Found);
        Assert.True(result.Recovered);
        Assert.Equal(1, result.PersistedActiveRevision);
        Assert.Equal(1, runtime.Describe().Revision);
        Assert.True(runtime.TryGetTag(activeTagId, out var activeTag));
        Assert.Equal("Plant.Active.Value", activeTag!.Path);
        Assert.False(runtime.TryGetTag(publishedTagId, out _));
        Assert.True(runtime.TryGetCurrent(activeTagId, out var current));
        Assert.Equal(111d, Convert.ToDouble(current!.Value));
        Assert.True(((ITransientEventDefinitionResolver)interactions).TryResolve(activeEventId, out _));
        Assert.True(((IRichCommandDefinitionResolver)interactions).TryResolve(activeCommandId, out _));
        Assert.True(((IRichCommandBindingResolver)interactions).TryResolve(activeCommandId, out _));
        Assert.False(((ITransientEventDefinitionResolver)interactions).TryResolve(publishedEventId, out _));
        Assert.False(((IRichCommandDefinitionResolver)interactions).TryResolve(publishedCommandId, out _));
        Assert.False(((IRichCommandBindingResolver)interactions).TryResolve(publishedCommandId, out _));

        var currentLock = EngineeringLockContract.Normalize(exchange.ExportPackage().EngineeringLock);
        Assert.True(currentLock.Locked);
        Assert.Equal(activeLock.Verifier, currentLock.Verifier);
        Assert.NotEqual(publishedLock.Verifier, currentLock.Verifier);

        var loadedActive = await persistence.LoadActiveAsync("plant-a");
        var loadedPublished = await persistence.LoadPublishedAsync("plant-a");
        Assert.Equal(1, loadedActive!.Revision);
        Assert.Equal(2, loadedPublished!.Revision);
    }

    [Fact]
    public async Task Recovery_InvalidInteractionGraphFailsClosedWithoutReplacingPriorResolverGraph()
    {
        var previousPackage = WithInteractions(
            CreatePackage(1, Guid.NewGuid(), "Plant.Previous.Value", "holding:10"),
            "previous",
            out var previousEventId,
            out var previousCommandId);
        var invalidPackage = WithInteractions(
            CreatePackage(1, Guid.NewGuid(), "Plant.Invalid.Value", "holding:10"),
            "invalid",
            out var invalidEventId,
            out var invalidCommandId);
        invalidPackage = invalidPackage with
        {
            DriverCommandBindings =
            [
                invalidPackage.DriverCommandBindings!.Single() with
                {
                    DataSourceId = Guid.NewGuid()
                }
            ]
        };

        var snapshot = CreateSnapshot(1, invalidPackage);
        var store = new RecoveryStore(snapshot, snapshot);
        var exchangeBus = new InMemoryScadaEventBus();
        using var exchangeAlarms = new InMemoryAlarmEngine(exchangeBus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), exchangeAlarms);
        var persistence = new EngineeringProjectPersistenceService(exchange, store);
        var runtimeBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            runtimeBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromMilliseconds(100));
        var licensing = new TestProductLicenseService(ValidVerification());
        await using var authorityStore = new InMemoryRuntimeSessionLeaseStore();
        var interactions = new ActiveDriverInteractionRuntimeCatalog();
        interactions.Commit(interactions.Prepare(previousPackage));

        var recovery = new PersistedRuntimeRecoveryService(
            persistence,
            exchange,
            runtime,
            licensing,
            authorityStore,
            driverInteractions: interactions);

        var result = await recovery.RecoverAsync("plant-a");

        Assert.True(result.Found);
        Assert.False(result.Recovered);
        Assert.Null(runtime.Describe().Revision);
        Assert.Contains(
            result.Runtime!.RuntimeIssues,
            issue => issue.Code == "DRIVER_INTERACTION_ACTIVE_GRAPH_INVALID" && issue.IsError);
        Assert.True(((ITransientEventDefinitionResolver)interactions).TryResolve(previousEventId, out _));
        Assert.True(((IRichCommandDefinitionResolver)interactions).TryResolve(previousCommandId, out _));
        Assert.True(((IRichCommandBindingResolver)interactions).TryResolve(previousCommandId, out _));
        Assert.False(((ITransientEventDefinitionResolver)interactions).TryResolve(invalidEventId, out _));
        Assert.False(((IRichCommandDefinitionResolver)interactions).TryResolve(invalidCommandId, out _));
        Assert.False(((IRichCommandBindingResolver)interactions).TryResolve(invalidCommandId, out _));
    }

    [Fact]
    public async Task Recovery_AuthorityTransitionPending_IsDeniedWithoutRuntimeOrLockMutation()
    {
        var persistedLock = new EngineeringLockSecretService().Configure("persisted-lock", locked: true);
        var currentLock = new EngineeringLockSecretService().Configure("current-lock", locked: true);
        var package = CreateSimplePackage(0) with { EngineeringLock = persistedLock };
        var snapshot = CreateSnapshot(1, package);
        var store = new RecoveryStore(snapshot, snapshot);

        var exchangeBus = new InMemoryScadaEventBus();
        using var exchangeAlarms = new InMemoryAlarmEngine(exchangeBus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), exchangeAlarms);
        EngineeringLockAccess.Replace(exchange, currentLock);
        var persistence = new EngineeringProjectPersistenceService(exchange, store);

        var runtimeBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            runtimeBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));
        var licensing = new TestProductLicenseService(ValidVerification());
        await using var authorityStore = new InMemoryRuntimeSessionLeaseStore();
        var transition = await authorityStore.BeginAuthorityTransitionAsync(
            "pending-recovery-test",
            DateTimeOffset.UtcNow);

        try
        {
            var recovery = new PersistedRuntimeRecoveryService(
                persistence,
                exchange,
                runtime,
                licensing,
                authorityStore);

            var result = await recovery.RecoverAsync("plant-a");

            Assert.True(result.Found);
            Assert.False(result.Recovered);
            Assert.Null(runtime.Describe().Revision);
            Assert.Contains(
                result.Runtime!.RuntimeIssues,
                issue =>
                    issue.Code == PersistedRuntimeRecoveryService.RecoveryDeniedIssueCode &&
                    issue.Message == PersistedRuntimeRecoveryService.TransitionPendingDiagnostic);

            var after = EngineeringLockAccess.Current(exchange);
            Assert.True(after.Locked);
            Assert.Equal(currentLock.Verifier, after.Verifier);
            Assert.NotEqual(persistedLock.Verifier, after.Verifier);
        }
        finally
        {
            Assert.True(await authorityStore.AbortAuthorityTransitionAsync(
                transition.TransitionId,
                transition.BaseAuthorityRevision));
        }
    }

    [Fact]
    public async Task Recovery_InvalidCanonicalLicense_IsDeniedBeforeRuntimeActivation()
    {
        var package = CreateSimplePackage(0);
        var snapshot = CreateSnapshot(1, package);
        var store = new RecoveryStore(snapshot, snapshot);

        var exchangeBus = new InMemoryScadaEventBus();
        using var exchangeAlarms = new InMemoryAlarmEngine(exchangeBus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), exchangeAlarms);
        var persistence = new EngineeringProjectPersistenceService(exchange, store);

        var runtimeBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            runtimeBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));
        var licensing = new TestProductLicenseService(
            LicenseVerificationResult.Invalid("test-invalid"));
        await using var authorityStore = new InMemoryRuntimeSessionLeaseStore();
        var recovery = new PersistedRuntimeRecoveryService(
            persistence,
            exchange,
            runtime,
            licensing,
            authorityStore);

        var result = await recovery.RecoverAsync("plant-a");

        Assert.True(result.Found);
        Assert.False(result.Recovered);
        Assert.Null(runtime.Describe().Revision);
        Assert.Contains(
            result.Runtime!.RuntimeIssues,
            issue =>
                issue.Code == PersistedRuntimeRecoveryService.RecoveryDeniedIssueCode &&
                issue.Message == PersistedRuntimeRecoveryService.InvalidLicenseDiagnostic);
    }

    [Fact]
    public async Task Recovery_DemoWithoutDurableAnchor_IsDeniedWithoutMintingFreshWindow()
    {
        var persistedLock = new EngineeringLockSecretService().Configure("persisted-demo-lock", locked: true);
        var package = CreateSimplePackage(0) with { EngineeringLock = persistedLock };
        var snapshot = CreateSnapshot(1, package);
        var store = new RecoveryStore(snapshot, snapshot);

        var exchangeBus = new InMemoryScadaEventBus();
        using var exchangeAlarms = new InMemoryAlarmEngine(exchangeBus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), exchangeAlarms);
        var persistence = new EngineeringProjectPersistenceService(exchange, store);

        var runtimeBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            runtimeBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));
        var licensing = new TestProductLicenseService(LicenseVerificationResult.Demo());
        await using var authorityStore = new InMemoryRuntimeSessionLeaseStore();
        var recovery = new PersistedRuntimeRecoveryService(
            persistence,
            exchange,
            runtime,
            licensing,
            authorityStore);

        var result = await recovery.RecoverAsync("plant-a");

        Assert.True(result.Found);
        Assert.False(result.Recovered);
        Assert.Null(runtime.Describe().Revision);
        Assert.Contains(
            result.Runtime!.RuntimeIssues,
            issue =>
                issue.Code == PersistedRuntimeRecoveryService.RecoveryDeniedIssueCode &&
                issue.Message == PersistedRuntimeRecoveryService.DemoAnchorMissingDiagnostic);
        Assert.False(EngineeringLockAccess.Current(exchange).Locked);
    }

    [Fact]
    public async Task Recovery_DemoWithNewAnchor_DeniesOlderActiveRevisionAfterFailedCommit()
    {
        var oldAnchor = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var newAnchor = oldAnchor.AddHours(6);
        var snapshot = CreateSnapshot(1, CreateSimplePackage(0));
        var store = new RecoveryStore(snapshot, snapshot, oldAnchor);
        var exchangeBus = new InMemoryScadaEventBus();
        using var exchangeAlarms = new InMemoryAlarmEngine(exchangeBus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), exchangeAlarms);
        var persistence = new EngineeringProjectPersistenceService(exchange, store);
        var runtimeBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            runtimeBus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2));
        var licensing = new TestProductLicenseService(LicenseVerificationResult.Demo());
        await using var authorityStore = new InMemoryRuntimeSessionLeaseStore();
        await SeedDemoAuthorityAsync(authorityStore, oldAnchor);
        await authorityStore.EstablishDemoSessionAnchorAsync(newAnchor, oldAnchor);
        var recovery = new PersistedRuntimeRecoveryService(
            persistence, exchange, runtime, licensing, authorityStore);

        var result = await recovery.RecoverAsync("plant-a");

        Assert.True(result.Found);
        Assert.False(result.Recovered);
        Assert.Null(runtime.Describe().Revision);
        Assert.Equal(newAnchor, (await authorityStore.GetAuthorityStateAsync()).DemoStartedAtUtc);
        Assert.Contains(result.Runtime!.RuntimeIssues, issue =>
            issue.Code == PersistedRuntimeRecoveryService.RecoveryDeniedIssueCode &&
            issue.Message == PersistedRuntimeRecoveryService.DemoAnchorMismatchDiagnostic);
    }

    [Fact]
    public async Task Recovery_DemoWithAuthorityAnchor_UsesNormalPathAndPreservesRemainingWindow()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var anchor = now.AddHours(-1);
        var time = new RecordingTimeProvider(now);
        var persistedLock = new EngineeringLockSecretService().Configure("persisted-demo-lock", locked: true);
        var package = CreateServerMemoryPackage() with { EngineeringLock = persistedLock };
        var snapshot = CreateSnapshot(1, package);
        var store = new RecoveryStore(snapshot, snapshot, anchor);

        var exchangeBus = new InMemoryScadaEventBus();
        using var exchangeAlarms = new InMemoryAlarmEngine(exchangeBus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), exchangeAlarms);
        var persistence = new EngineeringProjectPersistenceService(exchange, store);

        var runtimeBus = new InMemoryScadaEventBus();
        var inner = new EngineeringRuntimeCoordinator(
            runtimeBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));
        var licensing = new TestProductLicenseService(LicenseVerificationResult.Demo());
        await using var authorityStore = new InMemoryRuntimeSessionLeaseStore();
        await SeedDemoAuthorityAsync(authorityStore, anchor);
        await using var runtime = new ProductLicensedRuntimeCoordinator(
            inner,
            () => new EngineeringRuntimeCoordinator(
                runtimeBus,
                new EngineeringDriverCompiler(),
                TimeSpan.FromSeconds(2)),
            licensing,
            time,
            authorityStore);

        var recovery = new PersistedRuntimeRecoveryService(
            persistence,
            exchange,
            runtime,
            licensing,
            authorityStore);

        var result = await recovery.RecoverAsync("plant-a");
        var status = runtime.GetProductRuntimeStatus();

        Assert.True(result.Recovered);
        Assert.Equal(1, runtime.Describe().Revision);
        Assert.Equal(anchor, status.DemoStartedAtUtc);
        Assert.Equal(anchor + LicensingPolicy.DemoMaxContinuousRun, status.DemoExpiresAtUtc);
        Assert.Equal(TimeSpan.FromHours(4), status.DemoRemaining);
        Assert.Equal(TimeSpan.FromHours(4), time.LastTimerDueTime);

        var recoveredLock = EngineeringLockAccess.Current(exchange);
        Assert.True(recoveredLock.Locked);
        Assert.Equal(persistedLock.Verifier, recoveredLock.Verifier);
    }

    [Fact]
    public async Task Recovery_DemoWithExpiredAuthorityAnchor_RemainsStoppedAsExpectedAuthorityDenial()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var expiredAnchor = now - LicensingPolicy.DemoMaxContinuousRun - TimeSpan.FromMinutes(1);
        var time = new RecordingTimeProvider(now);
        var package = CreateInteractionPackage("expired-demo", out var expiredEventId, out var expiredCommandId);
        var previousPackage = CreateInteractionPackage("previous", out var previousEventId, out var previousCommandId);
        var snapshot = CreateSnapshot(1, package);
        var store = new RecoveryStore(snapshot, snapshot, expiredAnchor);

        var exchangeBus = new InMemoryScadaEventBus();
        using var exchangeAlarms = new InMemoryAlarmEngine(exchangeBus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), exchangeAlarms);
        var persistence = new EngineeringProjectPersistenceService(exchange, store);
        var runtimeBus = new InMemoryScadaEventBus();
        var licensing = new TestProductLicenseService(LicenseVerificationResult.Demo());
        await using var authorityStore = new InMemoryRuntimeSessionLeaseStore();
        await SeedDemoAuthorityAsync(authorityStore, expiredAnchor);
        var interactions = new ActiveDriverInteractionRuntimeCatalog();
        interactions.Commit(interactions.Prepare(previousPackage));
        await using var runtime = new ProductLicensedRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(runtimeBus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            () => new EngineeringRuntimeCoordinator(runtimeBus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            licensing,
            time,
            authorityStore);
        var recovery = new PersistedRuntimeRecoveryService(
            persistence,
            exchange,
            runtime,
            licensing,
            authorityStore,
            driverInteractions: interactions);

        var result = await recovery.RecoverAsync("plant-a");

        Assert.True(result.Found);
        Assert.False(result.Recovered);
        Assert.True(result.IsExpectedAuthorityDenial);
        Assert.Null(runtime.Describe().Revision);
        Assert.Equal(ProductRuntimeLifecycleState.DemoExpired, runtime.GetProductRuntimeStatus().State);
        Assert.Equal(expiredAnchor, runtime.GetProductRuntimeStatus().DemoStartedAtUtc);
        Assert.True(((ITransientEventDefinitionResolver)interactions).TryResolve(previousEventId, out _));
        Assert.True(((IRichCommandDefinitionResolver)interactions).TryResolve(previousCommandId, out _));
        Assert.True(((IRichCommandBindingResolver)interactions).TryResolve(previousCommandId, out _));
        Assert.False(((ITransientEventDefinitionResolver)interactions).TryResolve(expiredEventId, out _));
        Assert.False(((IRichCommandDefinitionResolver)interactions).TryResolve(expiredCommandId, out _));
        Assert.False(((IRichCommandBindingResolver)interactions).TryResolve(expiredCommandId, out _));
    }

    [Fact]
    public void EngineeringPackage_DoesNotContainRuntimeAuthorityRecoveryState()
    {
        var json = JsonSerializer.Serialize(CreateSimplePackage(1));

        Assert.DoesNotContain("authorityRevision", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("transitionPending", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("demoStartedAtUtc", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtimeSession", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("seat", json, StringComparison.OrdinalIgnoreCase);
    }

    private static EngineeringPackage CreateServerMemoryPackage()
    {
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "Demo Recovery Value",
            "Plant.DemoRecovery.Value",
            TagDataType.Double,
            Source: "memory.server");

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { tag },
            Array.Empty<AlarmEngineeringDto>(),
            new[]
            {
                new DataSourceEngineeringDto(
                    null,
                    "memory.server",
                    "Server Memory",
                    InternalMemoryRuntimePlanner.ServerMemoryDriverKey)
            });
    }

    private static EngineeringPackage CreateSimplePackage(int tagCount) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Enumerable.Range(0, tagCount)
                .Select(index => new TagEngineeringDto(
                    Guid.NewGuid(),
                    $"Tag {index}",
                    $"Plant.Tag{index:D4}",
                    TagDataType.Double))
                .ToArray(),
            Array.Empty<AlarmEngineeringDto>(),
            Array.Empty<DataSourceEngineeringDto>());

    private static EngineeringPackage CreateInteractionPackage(
        string key,
        out Guid eventId,
        out Guid commandId) =>
        WithInteractions(
            CreateSimplePackage(0) with
            {
                DataSources =
                [
                    new DataSourceEngineeringDto(
                        null,
                        $"source.{key}",
                        $"Source {key}",
                        "test.driver",
                        Enabled: false)
                ]
            },
            key,
            out eventId,
            out commandId);

    private static LicenseVerificationResult ValidVerification() =>
        LicenseVerificationResult.Valid(
            new EliteScadaLicensePayload(
                EliteScadaLicenseCodec.CurrentSchemaVersion,
                Guid.NewGuid().ToString("D"),
                new string('a', 64),
                LicenseTier.Unlimited,
                DateTimeOffset.UnixEpoch,
                null,
                "test-key"));

    private static async Task SeedDemoAuthorityAsync(
        InMemoryRuntimeSessionLeaseStore store,
        DateTimeOffset anchor)
    {
        var transition = await store.BeginAuthorityTransitionAsync("demo-recovery-test", anchor);
        var state = await store.CommitAuthorityChangeAsync(
            transition.TransitionId,
            transition.BaseAuthorityRevision,
            anchor,
            anchor);
        await store.CompleteAuthorityTransitionAsync(
            transition.TransitionId,
            state.AuthorityRevision);
    }

    private static EngineeringPackage CreatePackage(
        int port,
        Guid tagId,
        string path,
        string address)
    {
        var tag = new TagEngineeringDto(
            tagId,
            path.Split('.').Last(),
            path,
            TagDataType.Int16,
            Source: "plc-a",
            Address: address);

        var dataSource = new DataSourceEngineeringDto(
            null,
            "plc-a",
            "PLC A",
            EngineeringDriverCompiler.ModbusTcpDriverKey,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["scanIntervalMilliseconds"] = "20",
                ["requestTimeoutMilliseconds"] = "100",
                ["unitId"] = "1"
            });

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { tag },
            Array.Empty<AlarmEngineeringDto>(),
            new[] { dataSource });
    }

    private static EngineeringPackage WithInteractions(
        EngineeringPackage package,
        string key,
        out Guid eventId,
        out Guid commandId)
    {
        var dataSourceId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();
        eventId = Guid.NewGuid();
        commandId = Guid.NewGuid();

        var source = package.DataSources!.Single() with { Id = dataSourceId };
        return package with
        {
            DataSources = [source],
            Equipment =
            [
                new EquipmentEngineeringDto(
                    equipmentId,
                    $"Plant.Cover.{key}",
                    $"Cover {key}",
                    Capabilities:
                    [
                        new EquipmentCapabilityEngineeringDto(
                            "cover.main",
                            EquipmentCapabilityKinds.Cover)
                    ])
            ],
            TransientEventDefinitions =
            [
                new TransientEventDefinitionEngineeringDto(
                    eventId,
                    $"cover.stopped.{key}",
                    [
                        new TransientEventFieldDefinition(
                            "position",
                            new InteractionScalarSchema(
                                InteractionScalarKind.Percentage,
                                Minimum: 0,
                                Maximum: 100))
                    ],
                    equipmentId,
                    "cover.main")
            ],
            CapabilityEventReferences =
            [
                new CapabilityEventReferenceEngineeringDto(
                    equipmentId,
                    "cover.main",
                    "stopped",
                    eventId,
                    $"cover.stopped.{key}")
            ],
            RichCommandDefinitions =
            [
                new RichCommandDefinitionEngineeringDto(
                    commandId,
                    $"cover.move.{key}",
                    Array.Empty<RichCommandParameterDefinition>())
            ],
            DriverCommandBindings =
            [
                new DriverCommandBindingEngineeringDto(
                    commandId,
                    dataSourceId,
                    $"cover-{key}",
                    $"cover.move.{key}",
                    EquipmentId: equipmentId,
                    CapabilityId: "cover.main")
            ]
        };
    }

    private static EngineeringProjectSnapshot CreateSnapshot(long revision, EngineeringPackage package)
    {
        var json = JsonSerializer.Serialize(package, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });

        return new EngineeringProjectSnapshot(
            revision,
            "plant-a",
            "Plant A",
            package.Schema,
            package.SchemaVersion,
            DateTimeOffset.UtcNow,
            json,
            "test");
    }

    private sealed class TestProductLicenseService(
        LicenseVerificationResult verification) : IProductLicenseService
    {
        public LicenseVerificationResult Verification { get; set; } = verification;

        public string MachineFingerprint => new string('a', 64);
        public string MachineRequestCode => "test-request";
        public LicenseVerificationResult CurrentVerification => Verification;

        public LicenseVerificationResult VerifyCandidate(string licenseCode) => Verification;

        public RunEntitlementDecision EvaluateRun(int projectTagCount) =>
            ProductEntitlementEvaluator.Evaluate(Verification, projectTagCount);

        public void InstallLicense(string licenseCode) =>
            throw new NotSupportedException();

        public void RemoveLicense() =>
            throw new NotSupportedException();
    }

    private sealed class RecordingTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = utcNow;

        public TimeSpan? LastTimerDueTime { get; private set; }

        public override DateTimeOffset GetUtcNow() => _utcNow;
        public override long GetTimestamp() => 0;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            LastTimerDueTime = dueTime;
            return new PassiveTimer();
        }

        private sealed class PassiveTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class RecoveryStore(
        EngineeringProjectSnapshot activeSnapshot,
        EngineeringProjectSnapshot publishedSnapshot,
        DateTimeOffset? demoStartedAtUtc = null) : IEngineeringProjectStore
    {
        private readonly EngineeringProjectSnapshot[] _snapshots =
            [activeSnapshot, publishedSnapshot];
        private readonly EngineeringProjectActivation _activation = new(
            activeSnapshot.ProjectKey,
            activeSnapshot.Revision,
            DateTimeOffset.UtcNow,
            "operator",
            demoStartedAtUtc);
        private readonly EngineeringProjectPublication _publication = new(
            publishedSnapshot.ProjectKey,
            publishedSnapshot.Revision,
            DateTimeOffset.UtcNow,
            "publisher");

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<EngineeringProjectSnapshot> SaveAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            string? savedBy = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectSnapshot?>(_snapshots
                .Where(x => x.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Revision)
                .FirstOrDefault());

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectSnapshot?>(_snapshots.FirstOrDefault(x =>
                x.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase) &&
                x.Revision == revision));

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(_snapshots
                .Where(x => x.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Revision)
                .Take(limit)
                .ToArray());

        public Task<EngineeringProjectPublication?> GetPublicationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectPublication?>(
                projectKey.Equals(_publication.ProjectKey, StringComparison.OrdinalIgnoreCase)
                    ? _publication
                    : null);

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(
            string projectKey,
            long revision,
            string? publishedBy = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectActivation?> GetActivationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectActivation?>(
                projectKey.Equals(_activation.ProjectKey, StringComparison.OrdinalIgnoreCase)
                    ? _activation
                    : null);

        public Task<EngineeringProjectActivation?> RecordActivationAsync(
            string projectKey,
            long revision,
            string? activatedBy = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
