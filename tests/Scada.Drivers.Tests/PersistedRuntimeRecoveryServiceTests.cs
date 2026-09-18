using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Api.HostedServices;
using Scada.Api.Licensing;
using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Simulation;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
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
        var publishedPackage = CreatePackage(publishedServer.Port, publishedTagId, "Plant.Published.Value", "holding:20") with
        {
            EngineeringLock = publishedLock
        };
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
        var recovery = new PersistedRuntimeRecoveryService(
            persistence,
            exchange,
            runtime,
            licensing,
            authorityStore);

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
    public async Task Recovery_DemoWithAuthorityAnchor_UsesNormalPathAndPreservesRemainingWindow()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var anchor = now.AddHours(-1);
        var time = new RecordingTimeProvider(now);
        var persistedLock = new EngineeringLockSecretService().Configure("persisted-demo-lock", locked: true);
        var package = CreateSimplePackage(0) with { EngineeringLock = persistedLock };
        var snapshot = CreateSnapshot(1, package);
        var store = new RecoveryStore(snapshot, snapshot);

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
    public void EngineeringPackage_DoesNotContainRuntimeAuthorityRecoveryState()
    {
        var json = JsonSerializer.Serialize(CreateSimplePackage(1));

        Assert.DoesNotContain("authorityRevision", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("transitionPending", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("demoStartedAtUtc", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtimeSession", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("seat", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DemoHostedService_DoesNotStartSimulationAfterEngineeringRecovery()
    {
        await using var activeServer = new TestModbusTcpServer();
        activeServer.HoldingRegisters[10] = 55;
        activeServer.Start();

        var activePackage = CreatePackage(
            activeServer.Port,
            Guid.NewGuid(),
            "Plant.Active.Value",
            "holding:10");
        var activeSnapshot = CreateSnapshot(1, activePackage);
        var store = new RecoveryStore(activeSnapshot, activeSnapshot);

        var exchangeBus = new InMemoryScadaEventBus();
        using var exchangeAlarms = new InMemoryAlarmEngine(exchangeBus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), exchangeAlarms);
        var persistence = new EngineeringProjectPersistenceService(exchange, store);

        var runtimeBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            runtimeBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));
        var licensing = new TestProductLicenseService(ValidVerification());
        await using var authorityStore = new InMemoryRuntimeSessionLeaseStore();
        var recovery = new PersistedRuntimeRecoveryService(
            persistence,
            exchange,
            runtime,
            licensing,
            authorityStore);
        Assert.True((await recovery.RecoverAsync("plant-a")).Recovered);

        using var fallback = new DemoRuntimeServices(runtimeBus);
        var fallbackTag = TagDefinition.Create(
            "Demo fallback",
            "Demo.Fallback",
            TagDataType.Double,
            "builtin.simulation");
        await using var simulation = new SimulationDriver(
            fallback.Cache,
            fallback.Registry,
            new[] { new SimulationPoint(fallbackTag, SimulationSignalType.Constant, ConstantValue: 12) },
            TimeSpan.FromMilliseconds(20));

        var hosted = new SimulationDriverHostedService(
            simulation,
            fallback,
            runtime);

        await hosted.StartAsync(CancellationToken.None);

        Assert.Equal(DriverState.Stopped, simulation.Status.State);
        Assert.Empty(fallback.Registry.Snapshot());
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
        private long _timestamp;

        public TimeSpan? LastTimerDueTime { get; private set; }

        public override DateTimeOffset GetUtcNow() => _utcNow;
        public override long GetTimestamp() => _timestamp;
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
        EngineeringProjectSnapshot publishedSnapshot) : IEngineeringProjectStore
    {
        private readonly EngineeringProjectSnapshot[] _snapshots =
            [activeSnapshot, publishedSnapshot];
        private readonly EngineeringProjectActivation _activation = new(
            activeSnapshot.ProjectKey,
            activeSnapshot.Revision,
            DateTimeOffset.UtcNow,
            "operator");
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
