using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Scada.Api.Persistence;

namespace Scada.Drivers.Tests;

public sealed class DatabaseTopologyCoreTests
{
    [Fact]
    public void RemoteProfile_ValidatesStructuredEndpoint_AndRejectsInvalidTrustShapes()
    {
        var profile = new DatabaseTopologyProfile(
            DatabaseTopologyMode.Remote,
            new DatabaseRemoteEndpoint(
                "db.example.internal",
                5432,
                "elitescada",
                "elite_service",
                "db-primary",
                DatabaseTlsMode.VerifyFull,
                "/etc/elitescada/db-ca.pem",
                false,
                15),
            DatabaseHistorianTopology.Primary);

        profile.Validate();

        Assert.Throws<ArgumentException>(() =>
            (profile with
            {
                Primary = profile.Primary! with
                {
                    TrustServerCertificate = true
                }
            }).Validate());

        Assert.Throws<ArgumentException>(() =>
            (profile with
            {
                Primary = profile.Primary! with
                {
                    Host = "bad\nhost"
                }
            }).Validate());

        Assert.Throws<ArgumentException>(() =>
            (profile with
            {
                Primary = profile.Primary! with
                {
                    RootCertificatePath = null
                }
            }).Validate());
    }

    [Fact]
    public async Task SecretStore_EncryptsCredential_AndTopologyNeverPersistsPlaintext()
    {
        using var temp = new TemporaryDirectory();
        var key = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        using var secrets = new EncryptedFileDeploymentDatabaseSecretStore(temp.SecretPath, key);
        var store = new FileDatabaseTopologyStore(temp.TopologyPath);
        const string plaintext = "remote-plaintext-secret";

        var reference = await secrets.StoreAsync(
            null,
            System.Text.Encoding.UTF8.GetBytes(plaintext));
        var endpoint = new DatabaseRemoteEndpoint(
            "db.example.internal",
            5432,
            "elitescada",
            "elite_service",
            reference,
            DatabaseTlsMode.Require,
            null,
            false,
            15);
        var document = DatabaseTopologyDocument.CreateDefault(DateTimeOffset.UtcNow) with
        {
            Active = new(
                DatabaseTopologyMode.Remote,
                endpoint,
                DatabaseHistorianTopology.Primary)
        };
        await store.SaveAsync(document);

        Assert.DoesNotContain(plaintext, await File.ReadAllTextAsync(temp.SecretPath), StringComparison.Ordinal);
        Assert.DoesNotContain(plaintext, await File.ReadAllTextAsync(temp.TopologyPath), StringComparison.Ordinal);

        await using var lease = await secrets.ResolveAsync(reference);
        Assert.Equal(plaintext, System.Text.Encoding.UTF8.GetString(lease.Material.Span));
    }

    [Fact]
    public void ProductionWithoutDurableDatabase_FailsClosedInsteadOfRegisteringMemoryFallback()
    {
        using var temp = new TemporaryDirectory();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseTopology:StateFile"] = temp.TopologyPath,
            ["DatabaseTopology:Secrets:StoreFile"] = temp.SecretPath
        });

        var error = Assert.Throws<InvalidOperationException>(() => builder.AddDatabaseTopologyCore());

        Assert.Contains("will not silently fall back to in-memory persistence", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalManaged_RemainsDefaultAndUsesExistingDeploymentConnection()
    {
        using var temp = new TemporaryDirectory();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseTopology:StateFile"] = temp.TopologyPath,
            ["DatabaseTopology:Secrets:StoreFile"] = temp.SecretPath,
            ["ConnectionStrings:EliteScada"] = "Host=local-db;Port=5432;Database=elitescada;Username=elite;Password=local"
        });

        var runtime = builder.AddDatabaseTopologyCore();

        Assert.Equal(DatabaseTopologyMode.LocalManaged, runtime.ActiveProfile.Mode);
        Assert.True(runtime.HasDurablePrimary);
        Assert.Contains("local-db", runtime.PrimaryConnectionString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigrationSuccess_SwitchesAtomically_PreservesLocalAsPrevious_AndRequestsRestart()
    {
        using var fixture = CreateServiceFixture();
        var pending = await fixture.Service.PrepareAsync(RemoteRequest());
        Assert.Equal(DatabaseMigrationPhase.Prepared, pending.Phase);

        var copied = await fixture.Service.StartMigrationAsync(pending.OperationId);
        Assert.Equal(DatabaseMigrationPhase.Copied, copied.Phase);
        Assert.True(fixture.Maintenance.IsActive);

        var verification = await fixture.Service.VerifyAsync(pending.OperationId);
        Assert.True(verification.Succeeded);

        var cutover = await fixture.Service.CommitCutoverAsync(pending.OperationId);
        Assert.True(cutover.Succeeded);
        Assert.True(cutover.RestartRequired);
        Assert.True(fixture.Rebinder.RestartRequested);
        Assert.False(fixture.Maintenance.IsActive);

        var document = await fixture.Store.GetAsync();
        Assert.Equal(DatabaseTopologyMode.Remote, document.Active.Mode);
        Assert.Equal(DatabaseTopologyMode.LocalManaged, document.PreviousActive?.Mode);
        Assert.Null(document.Pending);
        Assert.Equal(DatabaseMigrationPhase.Completed, document.LastOperation?.Phase);

        var topologyJson = await File.ReadAllTextAsync(fixture.Temp.TopologyPath);
        var secretJson = await File.ReadAllTextAsync(fixture.Temp.SecretPath);
        Assert.DoesNotContain("remote-plaintext-secret", topologyJson, StringComparison.Ordinal);
        Assert.DoesNotContain("remote-plaintext-secret", secretJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CopyFailure_LeavesLocalActive_AndRequiresRollback()
    {
        using var fixture = CreateServiceFixture(operations => operations.ThrowOnCopy = true);
        var pending = await fixture.Service.PrepareAsync(RemoteRequest());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.StartMigrationAsync(pending.OperationId));

        var document = await fixture.Store.GetAsync();
        Assert.Equal(DatabaseTopologyMode.LocalManaged, document.Active.Mode);
        Assert.Equal(DatabaseMigrationPhase.RollbackRequired, document.Pending?.Phase);
        Assert.Equal("copy-failed", document.Pending?.FailureCode);
        Assert.False(fixture.Maintenance.IsActive);

        var rollback = await fixture.Service.RollbackAsync(pending.OperationId);
        Assert.True(rollback.RolledBack);
        Assert.Equal(DatabaseTopologyMode.LocalManaged, (await fixture.Store.GetAsync()).Active.Mode);
    }

    [Fact]
    public async Task VerifyFailure_LeavesLocalActive_AndRequiresRollback()
    {
        using var fixture = CreateServiceFixture(operations => operations.VerificationSucceeds = false);
        var pending = await fixture.Service.PrepareAsync(RemoteRequest());
        await fixture.Service.StartMigrationAsync(pending.OperationId);

        var verification = await fixture.Service.VerifyAsync(pending.OperationId);

        Assert.False(verification.Succeeded);
        var document = await fixture.Store.GetAsync();
        Assert.Equal(DatabaseTopologyMode.LocalManaged, document.Active.Mode);
        Assert.Equal(DatabaseMigrationPhase.RollbackRequired, document.Pending?.Phase);
        Assert.False(fixture.Maintenance.IsActive);
    }

    [Fact]
    public async Task ReadinessFailure_RestoresPreviousTopologyWithoutRestart()
    {
        using var fixture = CreateServiceFixture(operations => operations.FailReadiness = true);
        var pending = await fixture.Service.PrepareAsync(RemoteRequest());
        await fixture.Service.StartMigrationAsync(pending.OperationId);
        Assert.True((await fixture.Service.VerifyAsync(pending.OperationId)).Succeeded);

        var result = await fixture.Service.CommitCutoverAsync(pending.OperationId);

        Assert.False(result.Succeeded);
        Assert.True(result.RolledBack);
        Assert.False(result.RestartRequired);
        Assert.False(fixture.Rebinder.RestartRequested);
        var document = await fixture.Store.GetAsync();
        Assert.Equal(DatabaseTopologyMode.LocalManaged, document.Active.Mode);
        Assert.Null(document.Pending);
        Assert.Equal(DatabaseMigrationPhase.RolledBack, document.LastOperation?.Phase);
    }

    [Fact]
    public async Task CompletedCutover_CanRollBackToPreservedLocalTopology()
    {
        using var fixture = CreateServiceFixture();
        var pending = await fixture.Service.PrepareAsync(RemoteRequest());
        await fixture.Service.StartMigrationAsync(pending.OperationId);
        await fixture.Service.VerifyAsync(pending.OperationId);
        await fixture.Service.CommitCutoverAsync(pending.OperationId);
        fixture.Rebinder.Clear();

        var rollback = await fixture.Service.RollbackAsync();

        Assert.True(rollback.RolledBack);
        Assert.True(rollback.RestartRequired);
        Assert.True(fixture.Rebinder.RestartRequested);
        Assert.Equal(DatabaseTopologyMode.LocalManaged, (await fixture.Store.GetAsync()).Active.Mode);
    }

    [Theory]
    [InlineData("authentication-failed")]
    [InlineData("tls-validation-failed")]
    [InlineData("database-incompatible")]
    public async Task TestConnection_ReturnsSanitizedFailureCodes(string failureCode)
    {
        using var fixture = CreateServiceFixture(operations => operations.TestFailureCode = failureCode);

        var result = await fixture.Service.TestConnectionAsync(
            RemoteRequest().Primary,
            requireTimescale: true);

        Assert.False(result.Reachable);
        Assert.Equal(failureCode, result.FailureCode);
        Assert.DoesNotContain("remote-plaintext-secret", result.Diagnostic ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TopologyState_RemainsReadableWhenRemoteHealthIsUnavailable()
    {
        using var fixture = CreateServiceFixture();
        var pending = await fixture.Service.PrepareAsync(RemoteRequest());
        await fixture.Service.StartMigrationAsync(pending.OperationId);
        await fixture.Service.VerifyAsync(pending.OperationId);
        await fixture.Service.CommitCutoverAsync(pending.OperationId);
        fixture.Operations.TestFailureCode = "connection-failed";

        var status = await fixture.Service.GetStatusAsync(refreshHealth: true);

        Assert.Equal(DatabaseTopologyMode.Remote, status.ActiveTopology.Mode);
        Assert.Equal(false, status.PrimaryHealth?.Reachable);
        Assert.NotNull(status.LastHealthCheckUtc);
    }

    private static DatabaseRemoteProfileRequest RemoteRequest() =>
        new(
            new DatabaseRemoteEndpointRequest(
                "remote-db.example.internal",
                5432,
                "elitescada",
                "elite_service",
                "remote-plaintext-secret",
                null,
                DatabaseTlsMode.Require,
                null,
                false,
                15));

    private static ServiceFixture CreateServiceFixture(Action<FakeDatabaseTopologyOperations>? configure = null)
    {
        var temp = new TemporaryDirectory();
        var key = Enumerable.Range(1, 32).Select(value => (byte)(value + 10)).ToArray();
        var secrets = new EncryptedFileDeploymentDatabaseSecretStore(temp.SecretPath, key);
        var store = new FileDatabaseTopologyStore(temp.TopologyPath);
        var resolver = new DatabaseConnectionResolver(
            secrets,
            "Host=local-db;Port=5432;Database=elitescada;Username=elite;Password=local",
            null);
        var operations = new FakeDatabaseTopologyOperations();
        configure?.Invoke(operations);
        var options = new DatabaseTopologyOptions(temp.TopologyPath, temp.SecretPath, 18, 18, 2, 300);
        var maintenance = new DatabaseMaintenanceGate();
        var rebinder = new FakeDatabaseRuntimeRebinder();
        var service = new DatabaseTopologyAdministrationService(
            store,
            secrets,
            resolver,
            operations,
            options,
            maintenance,
            rebinder);
        return new(temp, secrets, store, operations, maintenance, rebinder, service);
    }

    private sealed class FakeDatabaseTopologyOperations : IDatabaseTopologyOperations
    {
        public bool ThrowOnCopy { get; set; }
        public bool VerificationSucceeds { get; set; } = true;
        public bool FailReadiness { get; set; }
        public string? TestFailureCode { get; set; }
        private int _compatibilityCalls;

        public Task<DatabaseConnectionHealth> TestAsync(
            string connectionString,
            bool requireTimescale,
            CancellationToken cancellationToken = default)
        {
            _ = connectionString;
            _ = requireTimescale;
            var failure = TestFailureCode;
            return Task.FromResult(new DatabaseConnectionHealth(
                failure is null,
                failure is null ? "18.0" : null,
                failure is null ? 18 : null,
                failure is null ? "2.29.2" : null,
                failure is null,
                failure is null,
                DateTimeOffset.UtcNow,
                failure,
                failure is null ? null : "Sanitized database diagnostic."));
        }

        public Task<DatabaseCompatibilityResult> ValidateCompatibilityAsync(
            string primaryConnectionString,
            string historianConnectionString,
            bool historianUsesPrimary,
            CancellationToken cancellationToken = default)
        {
            _ = primaryConnectionString;
            _ = historianConnectionString;
            _ = historianUsesPrimary;
            var call = Interlocked.Increment(ref _compatibilityCalls);
            var fail = (FailReadiness && call > 1) || TestFailureCode is not null;
            var code = fail ? (TestFailureCode ?? "database-incompatible") : null;
            var health = new DatabaseConnectionHealth(
                !fail,
                fail ? null : "18.0",
                fail ? null : 18,
                fail ? null : "2.29.2",
                !fail,
                !fail,
                DateTimeOffset.UtcNow,
                code,
                fail ? "Sanitized database diagnostic." : null);
            return Task.FromResult(new DatabaseCompatibilityResult(!fail, health, null, code, health.Diagnostic));
        }

        public Task<DatabaseMigrationPlan> PrepareAsync(
            string sourceConnectionString,
            string targetConnectionString,
            string historianSourceConnectionString,
            string historianTargetConnectionString,
            bool historianUsesPrimary,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DatabaseMigrationPlan(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                historianUsesPrimary,
                true,
                ["engineering", "authority", "audit", "server-memory", "runtime-sessions", "historian"],
                "LocalManaged",
                "Remote"));

        public Task CopyAsync(
            DatabaseMigrationPlan plan,
            string sourceConnectionString,
            string targetConnectionString,
            string historianSourceConnectionString,
            string historianTargetConnectionString,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnCopy)
                throw new InvalidOperationException("fixture copy failure");
            return Task.CompletedTask;
        }

        public Task<DatabaseMigrationVerification> VerifyAsync(
            DatabaseMigrationPlan plan,
            string sourceConnectionString,
            string targetConnectionString,
            string historianSourceConnectionString,
            string historianTargetConnectionString,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DatabaseMigrationVerification(
                VerificationSucceeds,
                new Dictionary<string, long> { ["engineering"] = 3 },
                new Dictionary<string, long> { ["engineering"] = VerificationSucceeds ? 3 : 2 },
                "project-a",
                7,
                VerificationSucceeds ? null : "migration-count-mismatch",
                VerificationSucceeds ? null : "Sanitized verification mismatch."));
    }

    private sealed class FakeDatabaseRuntimeRebinder : IDatabaseRuntimeRebinder
    {
        public bool RestartRequested { get; private set; }
        public void RequestRestart() => RestartRequested = true;
        public void Clear() => RestartRequested = false;
    }

    private sealed class ServiceFixture(
        TemporaryDirectory temp,
        EncryptedFileDeploymentDatabaseSecretStore secrets,
        FileDatabaseTopologyStore store,
        FakeDatabaseTopologyOperations operations,
        DatabaseMaintenanceGate maintenance,
        FakeDatabaseRuntimeRebinder rebinder,
        DatabaseTopologyAdministrationService service) : IDisposable
    {
        public TemporaryDirectory Temp { get; } = temp;
        public EncryptedFileDeploymentDatabaseSecretStore Secrets { get; } = secrets;
        public FileDatabaseTopologyStore Store { get; } = store;
        public FakeDatabaseTopologyOperations Operations { get; } = operations;
        public DatabaseMaintenanceGate Maintenance { get; } = maintenance;
        public FakeDatabaseRuntimeRebinder Rebinder { get; } = rebinder;
        public DatabaseTopologyAdministrationService Service { get; } = service;

        public void Dispose()
        {
            Secrets.Dispose();
            Temp.Dispose();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "elitescada-db-topology-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public string TopologyPath => System.IO.Path.Combine(Path, "database-topology.json");
        public string SecretPath => System.IO.Path.Combine(Path, "database-secrets.json");

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { }
        }
    }
}
