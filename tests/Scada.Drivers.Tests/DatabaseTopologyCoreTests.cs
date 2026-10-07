using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Scada.Persistence.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Scada.Api.Persistence;
using Scada.Core.Persistence;

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
            ["DatabaseTopology:Secrets:StoreFile"] = temp.SecretPath,
            ["ConnectionStrings:EliteScada"] = ""
        });

        var error = Assert.Throws<InvalidOperationException>(() => builder.AddDatabaseTopologyCore());

        Assert.Contains("will not silently fall back to in-memory persistence", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MaintenanceGate_DrainsInflightWriter_AndRemainsActiveUntilExplicitExit()
    {
        var gate = new DatabaseMaintenanceGate();
        var operationId = Guid.NewGuid();
        await using var writer = await gate.AcquireAsync("test-writer");

        var entering = gate.EnterAsync(operationId, TimeSpan.FromTicks(1));
        Assert.False(entering.IsCompleted);
        Assert.True(gate.IsActive);
        Assert.Equal(1, gate.ActiveWriterCount);
        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await gate.AcquireAsync("late-writer"));

        await writer.DisposeAsync();
        var expiresAtUtc = await entering;

        Assert.True(DateTimeOffset.UtcNow >= expiresAtUtc);
        Assert.True(gate.IsActive);
        Assert.Equal(operationId, gate.OperationId);
        Assert.Equal(0, gate.ActiveWriterCount);

        gate.Exit(operationId);

        Assert.False(gate.IsActive);
        Assert.Null(gate.OperationId);
    }

    [Fact]
    public async Task MaintenanceGate_AllowsScopedAuditAdmissionButKeepsOtherWritersQuiesced()
    {
        var gate = new DatabaseMaintenanceGate();
        var operationId = Guid.NewGuid();
        await gate.EnterAsync(operationId, TimeSpan.FromMinutes(1));

        using (gate.AllowMaintenanceControlAuditAdmission())
        {
            await using var audit = await gate.AcquireAsync("audit");
            Assert.Equal(1, gate.ActiveWriterCount);
            await Assert.ThrowsAsync<DurableWriteQuiescedException>(
                async () => await gate.AcquireAsync("engineering"));
        }

        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await gate.AcquireAsync("audit"));
        Assert.Equal(0, gate.ActiveWriterCount);
        Assert.True(gate.IsActive);
        gate.Exit(operationId);
    }

    [Fact]
    public async Task PendingCriticalPhase_RecoversMaintenanceFailClosedAfterRestart()
    {
        using var temp = new TemporaryDirectory();
        var store = new FileDatabaseTopologyStore(temp.TopologyPath);
        var operationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await store.SaveAsync(DatabaseTopologyDocument.CreateDefault(now) with
        {
            Pending = new DatabasePendingMigration(
                operationId,
                DatabaseTopologyProfile.LocalManaged,
                DatabaseMigrationPhase.Copying,
                now,
                now,
                new DatabaseMigrationPlan(
                    operationId,
                    now,
                    true,
                    true,
                    ["engineering"],
                    "LocalManaged",
                    "Remote"),
                MaintenanceLeaseExpiresAtUtc: now.AddMinutes(5))
        });

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

        _ = builder.AddDatabaseTopologyCore();
        await using var app = builder.Build();
        var recovered = app.Services.GetRequiredService<DatabaseMaintenanceGate>();

        Assert.True(recovered.IsActive);
        Assert.Equal(operationId, recovered.OperationId);
        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await recovered.AcquireAsync("restart-writer"));
    }

    [Fact]
    public async Task RealDurableStores_RejectWritesWhileDatabaseMaintenanceIsActive()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var gate = new DatabaseMaintenanceGate();
        await using var serverMemory = new PostgreSqlServerMemoryRetentionStore(connectionString, gate);
        await using var alarmHistory = new PostgreSqlAlarmHistoryStore(connectionString, gate);
        await using var operationalEvents = new PostgreSqlOperationalEventHistoryStore(connectionString, gate);
        await using var audit = new PostgreSqlAuditStore(connectionString, writeAdmission: gate);
        await using var runtimeSessions = new PostgreSqlRuntimeSessionLeaseStore(connectionString, gate);
        await using var engineering = new PostgreSqlEngineeringProjectStore(connectionString, gate);
        await using var binding = new PostgreSqlEngineeringInstallationBindingStore(connectionString, gate);
        await using var localIdentity = new PostgreSqlLocalIdentityStore(connectionString, gate);
        await using var authorityLifecycle = new PostgreSqlAuthorityLifecycleStore(connectionString, gate);
        await using var authorityPolicy = new PostgreSqlAuthorityPolicyStore(connectionString, gate);

        await serverMemory.InitializeAsync();
        await alarmHistory.EnsureInitializedAsync();
        await operationalEvents.EnsureInitializedAsync();
        await audit.InitializeAsync();
        await runtimeSessions.InitializeAsync();
        await engineering.InitializeAsync();
        await binding.InitializeAsync();
        await localIdentity.InitializeAsync();
        await authorityLifecycle.InitializeAsync();
        await authorityPolicy.InitializeAsync();

        var operationId = Guid.NewGuid();
        await gate.EnterAsync(operationId, TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await serverMemory.DeleteAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await alarmHistory.AppendAsync(null!, "quiesce.test"));
        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await operationalEvents.AppendAsync(null!));
        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await audit.ApplyRetentionBatchAsync(DateTimeOffset.UtcNow, 1));
        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await runtimeSessions.TerminateAsync(
                Guid.NewGuid(),
                "quiesce-subject",
                "quiesce-client",
                null!));
        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await engineering.DeleteProjectAsync("quiesce-test-project"));
        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await binding.BeginAttachAsync("quiesce-test-project"));
        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await localIdentity.ClearAllAsync());
        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await authorityLifecycle.MarkInvalidAsync());
        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await authorityPolicy.TryReplaceAsync(0, null!, null!));

        Assert.Equal(0, gate.ActiveWriterCount);
        Assert.True(gate.IsActive);
        gate.Exit(operationId);
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
        Assert.Equal(1, fixture.Operations.AuditSynchronizationCalls);

        var cutover = await fixture.Service.CommitCutoverAsync(pending.OperationId);
        Assert.True(cutover.Succeeded);
        Assert.True(cutover.RestartRequired);
        Assert.True(fixture.Rebinder.RestartRequested);
        Assert.Equal(2, fixture.Operations.AuditSynchronizationCalls);
        Assert.True(fixture.Maintenance.IsActive);
        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await fixture.Maintenance.AcquireAsync("post-cutover-writer"));

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
    public async Task ConnectExisting_MatchingProjectAndRevision_SwitchesWithoutCopying()
    {
        using var fixture = CreateServiceFixture();

        var result = await fixture.Service.ConnectExistingAsync(RemoteRequest());

        Assert.True(result.Succeeded);
        Assert.True(result.RestartRequired);
        Assert.True(fixture.Rebinder.RestartRequested);
        Assert.True(fixture.Maintenance.IsActive);
        var document = await fixture.Store.GetAsync();
        Assert.Equal(DatabaseTopologyMode.Remote, document.Active.Mode);
        Assert.Equal(DatabaseTopologyMode.LocalManaged, document.PreviousActive?.Mode);
        Assert.Null(document.Pending);
        Assert.Equal(DatabaseMigrationPhase.Completed, document.LastOperation?.Phase);
        Assert.Equal(0, fixture.Operations.CopyCalls);
        Assert.Equal(0, fixture.Operations.AuditSynchronizationCalls);
    }

    [Fact]
    public async Task ConnectExisting_ProjectMismatch_FailsClosedWithoutPersistingRemoteProfile()
    {
        using var fixture = CreateServiceFixture(operations => operations.ExistingTargetMatchesActiveProject = false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.ConnectExistingAsync(RemoteRequest()));

        var document = await fixture.Store.GetAsync();
        Assert.Equal(DatabaseTopologyMode.LocalManaged, document.Active.Mode);
        Assert.Null(document.PreviousActive);
        Assert.Null(document.Pending);
        Assert.False(fixture.Rebinder.RestartRequested);
        Assert.False(fixture.Maintenance.IsActive);
        Assert.Equal(0, fixture.Operations.CopyCalls);
        var secretJson = await File.ReadAllTextAsync(fixture.Temp.SecretPath);
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
        Assert.True(fixture.Maintenance.IsActive);

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
        Assert.True(fixture.Maintenance.IsActive);
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
        Assert.Equal(2, fixture.Operations.AuditSynchronizationCalls);

        // A real cutover exits only by process restart. Reset the in-memory gate/rebinder here
        // to model the newly started process before exercising Remote -> Local rollback.
        fixture.Maintenance.Exit(pending.OperationId);
        fixture.Rebinder.Clear();

        var rollback = await fixture.Service.RollbackAsync();

        Assert.True(rollback.RolledBack);
        Assert.True(rollback.RestartRequired);
        Assert.Equal(3, fixture.Operations.AuditSynchronizationCalls);
        Assert.True(fixture.Rebinder.RestartRequested);
        Assert.True(fixture.Maintenance.IsActive);
        await Assert.ThrowsAsync<DurableWriteQuiescedException>(
            async () => await fixture.Maintenance.AcquireAsync("rollback-window-writer"));
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


    [Fact]
    public async Task PostgreSqlOperations_ValidateConnectivityVersionTimescaleAuthHostAndTls()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var baseOptions = new DatabaseTopologyOptions("unused", "unused", 18, 18, 2, 300);
        var operations = new PostgreSqlDatabaseTopologyOperations(baseOptions);
        var valid = await operations.TestAsync(connectionString, requireTimescale: true);
        Assert.True(valid.Reachable);
        Assert.Null(valid.FailureCode);
        Assert.Equal(18, valid.PostgreSqlMajor);
        Assert.True(valid.TimescaleCapable);

        var incompatiblePg = await new PostgreSqlDatabaseTopologyOperations(
            baseOptions with { MinimumPostgreSqlMajor = 99, MaximumPostgreSqlMajor = 99 })
            .TestAsync(connectionString, requireTimescale: false);
        Assert.True(incompatiblePg.Reachable);
        Assert.Equal("database-incompatible", incompatiblePg.FailureCode);

        var incompatibleTimescale = await new PostgreSqlDatabaseTopologyOperations(
            baseOptions with { MinimumTimescaleMajor = 99 })
            .TestAsync(connectionString, requireTimescale: true);
        Assert.True(incompatibleTimescale.Reachable);
        Assert.Equal("database-incompatible", incompatibleTimescale.FailureCode);

        var auth = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Password = "definitely-wrong-" + Guid.NewGuid().ToString("N"),
            Pooling = false,
            Timeout = 2
        };
        var invalidAuth = await operations.TestAsync(auth.ConnectionString, requireTimescale: false);
        Assert.False(invalidAuth.Reachable);
        Assert.Equal("authentication-failed", invalidAuth.FailureCode);

        var host = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Host = "does-not-exist.invalid",
            Pooling = false,
            Timeout = 1
        };
        var invalidHost = await operations.TestAsync(host.ConnectionString, requireTimescale: false);
        Assert.False(invalidHost.Reachable);
        Assert.NotNull(invalidHost.FailureCode);

        var tls = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SslMode = SslMode.VerifyFull,
            RootCertificate = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "missing-elitescada-ca-" + Guid.NewGuid().ToString("N") + ".pem"),
            Pooling = false,
            Timeout = 2
        };
        var invalidTls = await operations.TestAsync(tls.ConnectionString, requireTimescale: false);
        Assert.False(invalidTls.Reachable);
        Assert.NotNull(invalidTls.FailureCode);
    }

    [Fact]
    public async Task PostgreSqlOperations_MigratePreparedCore_AndPreserveSourceDatabase()
    {
        var seed = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(seed)) return;

        var sourceDatabase = "elitescada_db_source_" + Guid.NewGuid().ToString("N")[..12];
        var targetDatabase = "elitescada_db_target_" + Guid.NewGuid().ToString("N")[..12];
        var admin = new NpgsqlConnectionStringBuilder(seed)
        {
            Database = "postgres",
            Pooling = false
        };
        var source = new NpgsqlConnectionStringBuilder(seed)
        {
            Database = sourceDatabase,
            Pooling = false
        };
        var target = new NpgsqlConnectionStringBuilder(seed)
        {
            Database = targetDatabase,
            Pooling = false
        };

        await CreateDatabaseAsync(admin.ConnectionString, sourceDatabase);
        await CreateDatabaseAsync(admin.ConnectionString, targetDatabase);
        try
        {
            await PostgreSqlDeploymentDatabasePreparation.InitializeCoreAsync(source.ConnectionString);

            var options = new DatabaseTopologyOptions("unused", "unused", 18, 18, 2, 300);
            var operations = new PostgreSqlDatabaseTopologyOperations(options);
            var plan = await operations.PrepareAsync(
                source.ConnectionString,
                target.ConnectionString,
                source.ConnectionString,
                target.ConnectionString,
                historianUsesPrimary: true);

            await operations.CopyAsync(
                plan,
                source.ConnectionString,
                target.ConnectionString,
                source.ConnectionString,
                target.ConnectionString);

            var verification = await operations.VerifyAsync(
                plan,
                source.ConnectionString,
                target.ConnectionString,
                source.ConnectionString,
                target.ConnectionString);

            Assert.True(verification.Succeeded);
            Assert.True(await TableExistsAsync(source.ConnectionString, "elitescada.authority_lifecycle_state"));
            Assert.True(await TableExistsAsync(target.ConnectionString, "elitescada.authority_lifecycle_state"));
        }
        finally
        {
            await DropDatabaseAsync(admin.ConnectionString, sourceDatabase);
            await DropDatabaseAsync(admin.ConnectionString, targetDatabase);
        }
    }

    private static async Task CreateDatabaseAsync(string adminConnectionString, string database)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\";", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string adminConnectionString, string database)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using (var terminate = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @database AND pid <> pg_backend_pid();",
            connection))
        {
            terminate.Parameters.AddWithValue("database", database);
            await terminate.ExecuteNonQueryAsync();
        }
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\";", connection);
        await drop.ExecuteNonQueryAsync();
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string table)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT to_regclass(@table) IS NOT NULL;", connection);
        command.Parameters.AddWithValue("table", table);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
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
        public bool ExistingTargetMatchesActiveProject { get; set; } = true;
        public string? TestFailureCode { get; set; }
        public int AuditSynchronizationCalls { get; private set; }
        public int CopyCalls { get; private set; }
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
            CopyCalls++;
            if (ThrowOnCopy)
                throw new InvalidOperationException("fixture copy failure");
            return Task.CompletedTask;
        }

        public Task SynchronizeAuditEventsAsync(
            string sourceConnectionString,
            string targetConnectionString,
            CancellationToken cancellationToken = default)
        {
            _ = sourceConnectionString;
            _ = targetConnectionString;
            _ = cancellationToken;
            AuditSynchronizationCalls++;
            return Task.CompletedTask;
        }

        public Task<DatabaseExistingTargetValidation> ValidateExistingTargetAsync(
            string sourceConnectionString,
            string targetConnectionString,
            CancellationToken cancellationToken = default)
        {
            _ = sourceConnectionString;
            _ = targetConnectionString;
            _ = cancellationToken;
            return Task.FromResult(ExistingTargetMatchesActiveProject
                ? new DatabaseExistingTargetValidation(true, "project-a", 7)
                : new DatabaseExistingTargetValidation(
                    false,
                    "other-project",
                    8,
                    "active-project-mismatch",
                    "Sanitized project mismatch."));
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
