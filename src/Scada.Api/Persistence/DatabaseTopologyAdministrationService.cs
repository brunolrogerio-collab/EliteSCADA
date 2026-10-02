using System.Text;

namespace Scada.Api.Persistence;

public sealed class DatabaseTopologyAdministrationService
{
    private readonly IDatabaseTopologyStore _store;
    private readonly IDeploymentDatabaseSecretStore _secrets;
    private readonly DatabaseConnectionResolver _resolver;
    private readonly IDatabaseTopologyOperations _operations;
    private readonly DatabaseTopologyOptions _options;
    private readonly DatabaseMaintenanceGate _maintenance;
    private readonly IDatabaseRuntimeRebinder _rebinder;
    private readonly SemaphoreSlim _mutation = new(1, 1);
    private DatabaseConnectionHealth? _lastPrimaryHealth;
    private DatabaseConnectionHealth? _lastHistorianHealth;

    public DatabaseTopologyAdministrationService(
        IDatabaseTopologyStore store,
        IDeploymentDatabaseSecretStore secrets,
        DatabaseConnectionResolver resolver,
        IDatabaseTopologyOperations operations,
        DatabaseTopologyOptions options,
        DatabaseMaintenanceGate maintenance,
        IDatabaseRuntimeRebinder rebinder)
    {
        _store = store;
        _secrets = secrets;
        _resolver = resolver;
        _operations = operations;
        _options = options;
        _maintenance = maintenance;
        _rebinder = rebinder;
    }

    public async Task<DatabaseTopologyStatus> GetStatusAsync(
        bool refreshHealth = false,
        CancellationToken cancellationToken = default)
    {
        var document = await _store.GetAsync(cancellationToken);
        if (refreshHealth)
            await RefreshActiveHealthAsync(document.Active, cancellationToken);

        var active = ProjectProfile(document.Active);
        var previous = document.PreviousActive is null ? null : ProjectProfile(document.PreviousActive);
        var lastCheck = new[] { _lastPrimaryHealth?.CheckedAtUtc, _lastHistorianHealth?.CheckedAtUtc }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty()
            .Max();
        DateTimeOffset? lastHealth = lastCheck == default ? null : lastCheck;

        return new(
            active,
            previous,
            document.Pending?.Phase,
            document.Pending?.OperationId,
            document.Pending?.Phase is DatabaseMigrationPhase.RollbackRequired or DatabaseMigrationPhase.Failed,
            _rebinder.RestartRequested,
            _lastPrimaryHealth,
            _lastHistorianHealth,
            lastHealth,
            document.LastOperation);
    }

    public async Task<DatabaseConnectionHealth> TestConnectionAsync(
        DatabaseRemoteEndpointRequest request,
        bool requireTimescale,
        CancellationToken cancellationToken = default)
    {
        var connectionString = await _resolver.BuildTransientRemoteConnectionStringAsync(request, cancellationToken);
        return await _operations.TestAsync(connectionString, requireTimescale, cancellationToken);
    }

    public async Task<DatabaseCompatibilityResult> ValidateCompatibilityAsync(
        DatabaseRemoteProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var primary = await _resolver.BuildTransientRemoteConnectionStringAsync(request.Primary, cancellationToken);
        var historianUsesPrimary = request.HistorianOverride is null;
        var historian = historianUsesPrimary
            ? primary
            : await _resolver.BuildTransientRemoteConnectionStringAsync(request.HistorianOverride!, cancellationToken);
        return await _operations.ValidateCompatibilityAsync(primary, historian, historianUsesPrimary, cancellationToken);
    }

    public async Task<DatabasePendingMigration> PrepareAsync(
        DatabaseRemoteProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var document = await _store.GetAsync(cancellationToken);
            if (document.Pending is not null)
                throw new InvalidOperationException("A database migration is already pending.");

            var compatibility = await ValidateCompatibilityAsync(request, cancellationToken);
            if (!compatibility.Compatible)
                throw new InvalidOperationException("Remote database target is not compatible.");

            var storedReferences = new List<string>();
            try
            {
                var candidate = new DatabaseTopologyProfile(
                    DatabaseTopologyMode.Remote,
                    await PersistEndpointAsync(request.Primary, storedReferences, cancellationToken),
                    request.HistorianOverride is null
                        ? DatabaseHistorianTopology.Primary
                        : new DatabaseHistorianTopology(
                            false,
                            await PersistEndpointAsync(request.HistorianOverride, storedReferences, cancellationToken)));
                candidate.Validate();

                var source = await _resolver.ResolveAsync(document.Active, requireDurable: true, cancellationToken);
                var target = await _resolver.ResolveAsync(candidate, requireDurable: true, cancellationToken);
                EnsureDurable(source);
                EnsureDurable(target);

                var plan = await _operations.PrepareAsync(
                    source.PrimaryConnectionString!,
                    target.PrimaryConnectionString!,
                    source.HistorianConnectionString ?? source.PrimaryConnectionString!,
                    target.HistorianConnectionString ?? target.PrimaryConnectionString!,
                    candidate.Historian.UsePrimary,
                    cancellationToken);

                var now = DateTimeOffset.UtcNow;
                var pending = new DatabasePendingMigration(
                    plan.OperationId,
                    candidate,
                    DatabaseMigrationPhase.Prepared,
                    now,
                    now,
                    plan);
                await _store.SaveAsync(document with
                {
                    Pending = pending,
                    UpdatedAtUtc = now
                }, cancellationToken);
                return pending;
            }
            catch
            {
                foreach (var reference in storedReferences)
                {
                    try { await _secrets.DeleteAsync(reference, CancellationToken.None); }
                    catch { }
                }
                throw;
            }
        }
        finally
        {
            _mutation.Release();
        }
    }

    public async Task<DatabasePendingMigration> StartMigrationAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var document = await RequirePendingAsync(operationId, DatabaseMigrationPhase.Prepared, cancellationToken);
            var pending = document.Pending!;
            var expires = _maintenance.Enter(
                operationId,
                TimeSpan.FromSeconds(_options.MaintenanceLeaseSeconds));
            pending = pending with
            {
                Phase = DatabaseMigrationPhase.Quiescing,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                MaintenanceLeaseExpiresAtUtc = expires
            };
            await _store.SaveAsync(document with { Pending = pending, UpdatedAtUtc = pending.UpdatedAtUtc }, cancellationToken);

            try
            {
                var source = await _resolver.ResolveAsync(document.Active, true, cancellationToken);
                var target = await _resolver.ResolveAsync(pending.Candidate, true, cancellationToken);
                EnsureDurable(source);
                EnsureDurable(target);

                pending = pending with { Phase = DatabaseMigrationPhase.Copying, UpdatedAtUtc = DateTimeOffset.UtcNow };
                await _store.SaveAsync(document with { Pending = pending, UpdatedAtUtc = pending.UpdatedAtUtc }, cancellationToken);

                await _operations.CopyAsync(
                    pending.Plan!,
                    source.PrimaryConnectionString!,
                    target.PrimaryConnectionString!,
                    source.HistorianConnectionString ?? source.PrimaryConnectionString!,
                    target.HistorianConnectionString ?? target.PrimaryConnectionString!,
                    cancellationToken);

                pending = pending with { Phase = DatabaseMigrationPhase.Copied, UpdatedAtUtc = DateTimeOffset.UtcNow };
                await _store.SaveAsync(document with { Pending = pending, UpdatedAtUtc = pending.UpdatedAtUtc }, cancellationToken);
                return pending;
            }
            catch (Exception)
            {
                pending = pending with
                {
                    Phase = DatabaseMigrationPhase.RollbackRequired,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    FailureCode = "copy-failed",
                    Diagnostic = "Database copy failed. Active topology was not changed."
                };
                await _store.SaveAsync(document with { Pending = pending, UpdatedAtUtc = pending.UpdatedAtUtc }, CancellationToken.None);
                _maintenance.Exit(operationId);
                throw;
            }
        }
        finally
        {
            _mutation.Release();
        }
    }

    public async Task<DatabaseMigrationVerification> VerifyAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var document = await RequirePendingAsync(operationId, DatabaseMigrationPhase.Copied, cancellationToken);
            var pending = document.Pending! with { Phase = DatabaseMigrationPhase.Verifying, UpdatedAtUtc = DateTimeOffset.UtcNow };
            await _store.SaveAsync(document with { Pending = pending, UpdatedAtUtc = pending.UpdatedAtUtc }, cancellationToken);

            var source = await _resolver.ResolveAsync(document.Active, true, cancellationToken);
            var target = await _resolver.ResolveAsync(pending.Candidate, true, cancellationToken);
            EnsureDurable(source);
            EnsureDurable(target);

            try
            {
                var verification = await _operations.VerifyAsync(
                    pending.Plan!,
                    source.PrimaryConnectionString!,
                    target.PrimaryConnectionString!,
                    source.HistorianConnectionString ?? source.PrimaryConnectionString!,
                    target.HistorianConnectionString ?? target.PrimaryConnectionString!,
                    cancellationToken);

                pending = pending with
                {
                    Phase = verification.Succeeded ? DatabaseMigrationPhase.Verified : DatabaseMigrationPhase.RollbackRequired,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    Verification = verification,
                    FailureCode = verification.FailureCode,
                    Diagnostic = verification.Diagnostic
                };
                await _store.SaveAsync(document with { Pending = pending, UpdatedAtUtc = pending.UpdatedAtUtc }, cancellationToken);
                if (!verification.Succeeded) _maintenance.Exit(operationId);
                return verification;
            }
            catch
            {
                pending = pending with
                {
                    Phase = DatabaseMigrationPhase.RollbackRequired,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    FailureCode = "verify-failed",
                    Diagnostic = "Database migration verification failed. Active topology was not changed."
                };
                await _store.SaveAsync(document with { Pending = pending, UpdatedAtUtc = pending.UpdatedAtUtc }, CancellationToken.None);
                _maintenance.Exit(operationId);
                throw;
            }
        }
        finally
        {
            _mutation.Release();
        }
    }

    public async Task<DatabaseCutoverResult> CommitCutoverAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var document = await RequirePendingAsync(operationId, DatabaseMigrationPhase.Verified, cancellationToken);
            var pending = document.Pending! with { Phase = DatabaseMigrationPhase.Switching, UpdatedAtUtc = DateTimeOffset.UtcNow };
            await _store.SaveAsync(document with { Pending = pending, UpdatedAtUtc = pending.UpdatedAtUtc }, cancellationToken);

            var previous = document.Active;
            var switched = document with
            {
                Active = pending.Candidate,
                PreviousActive = previous,
                Pending = pending with { Phase = DatabaseMigrationPhase.Readiness, UpdatedAtUtc = DateTimeOffset.UtcNow },
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            await _store.SaveAsync(switched, cancellationToken);

            try
            {
                var target = await _resolver.ResolveAsync(switched.Active, true, cancellationToken);
                EnsureDurable(target);
                var readiness = await _operations.ValidateCompatibilityAsync(
                    target.PrimaryConnectionString!,
                    target.HistorianConnectionString ?? target.PrimaryConnectionString!,
                    switched.Active.Historian.UsePrimary,
                    cancellationToken);

                _lastPrimaryHealth = readiness.Primary;
                _lastHistorianHealth = readiness.Historian;
                if (!readiness.Compatible)
                    return await RollBackFailedReadinessAsync(switched, operationId, "readiness-failed", cancellationToken);

                var now = DateTimeOffset.UtcNow;
                var completed = switched with
                {
                    Pending = null,
                    LastOperation = new(operationId, DatabaseMigrationPhase.Completed, now),
                    UpdatedAtUtc = now
                };
                await _store.SaveAsync(completed, cancellationToken);
                _maintenance.Exit(operationId);
                _rebinder.RequestRestart();
                var status = await GetStatusAsync(false, cancellationToken);
                return new(true, false, true, status);
            }
            catch
            {
                return await RollBackFailedReadinessAsync(switched, operationId, "readiness-failed", CancellationToken.None);
            }
        }
        finally
        {
            _mutation.Release();
        }
    }

    public async Task<DatabaseCutoverResult> RollbackAsync(
        Guid? operationId = null,
        CancellationToken cancellationToken = default)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var document = await _store.GetAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;

            if (document.Pending is { } pending)
            {
                if (operationId.HasValue && pending.OperationId != operationId.Value)
                    throw new InvalidOperationException("Pending database migration operation id does not match.");

                var rolledBack = document with
                {
                    Pending = null,
                    LastOperation = new(
                        pending.OperationId,
                        DatabaseMigrationPhase.RolledBack,
                        now,
                        pending.FailureCode,
                        pending.Diagnostic),
                    UpdatedAtUtc = now
                };
                await _store.SaveAsync(rolledBack, cancellationToken);
                _maintenance.Exit(pending.OperationId);
                return new(false, true, false, await GetStatusAsync(false, cancellationToken), pending.FailureCode, pending.Diagnostic);
            }

            if (document.PreviousActive is null)
                throw new InvalidOperationException("No previous database topology is available for rollback.");

            var current = document.Active;
            var previous = document.PreviousActive;
            var rollbackOperation = operationId ?? Guid.NewGuid();
            var rolledBackActive = document with
            {
                Active = previous,
                PreviousActive = current,
                LastOperation = new(rollbackOperation, DatabaseMigrationPhase.RolledBack, now),
                UpdatedAtUtc = now
            };
            await _store.SaveAsync(rolledBackActive, cancellationToken);
            _rebinder.RequestRestart();
            return new(false, true, true, await GetStatusAsync(false, cancellationToken));
        }
        finally
        {
            _mutation.Release();
        }
    }

    private async Task<DatabaseCutoverResult> RollBackFailedReadinessAsync(
        DatabaseTopologyDocument switched,
        Guid operationId,
        string failureCode,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var restored = switched with
        {
            Active = switched.PreviousActive ?? throw new InvalidOperationException("Previous topology is unavailable."),
            PreviousActive = null,
            Pending = null,
            LastOperation = new(
                operationId,
                DatabaseMigrationPhase.RolledBack,
                now,
                failureCode,
                "New database failed readiness; previous topology was restored."),
            UpdatedAtUtc = now
        };
        await _store.SaveAsync(restored, cancellationToken);
        _maintenance.Exit(operationId);
        return new(
            false,
            true,
            false,
            await GetStatusAsync(false, cancellationToken),
            failureCode,
            "New database failed readiness; previous topology was restored.");
    }

    private async Task RefreshActiveHealthAsync(
        DatabaseTopologyProfile profile,
        CancellationToken cancellationToken)
    {
        try
        {
            var active = await _resolver.ResolveAsync(profile, true, cancellationToken);
            EnsureDurable(active);
            var result = await _operations.ValidateCompatibilityAsync(
                active.PrimaryConnectionString!,
                active.HistorianConnectionString ?? active.PrimaryConnectionString!,
                profile.Historian.UsePrimary,
                cancellationToken);
            _lastPrimaryHealth = result.Primary;
            _lastHistorianHealth = result.Historian;
        }
        catch
        {
            _lastPrimaryHealth = new(
                false, null, null, null, false, false, DateTimeOffset.UtcNow,
                "connection-failed", "Active database is not reachable.");
            _lastHistorianHealth = null;
        }
    }

    private async Task<DatabasePendingMigration> RequirePendingSnapshotAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var document = await _store.GetAsync(cancellationToken);
        if (document.Pending is null || document.Pending.OperationId != operationId)
            throw new InvalidOperationException("Database migration operation was not found.");
        return document.Pending;
    }

    private async Task<DatabaseTopologyDocument> RequirePendingAsync(
        Guid operationId,
        DatabaseMigrationPhase expected,
        CancellationToken cancellationToken)
    {
        var document = await _store.GetAsync(cancellationToken);
        if (document.Pending is null || document.Pending.OperationId != operationId)
            throw new InvalidOperationException("Database migration operation was not found.");
        if (document.Pending.Phase != expected)
            throw new InvalidOperationException(
                $"Database migration is in phase '{document.Pending.Phase}', expected '{expected}'.");
        return document;
    }

    private async Task<DatabaseRemoteEndpoint> PersistEndpointAsync(
        DatabaseRemoteEndpointRequest request,
        ICollection<string> storedReferences,
        CancellationToken cancellationToken)
    {
        string? reference = request.CredentialReference;
        if (!string.IsNullOrEmpty(request.Password))
        {
            var bytes = Encoding.UTF8.GetBytes(request.Password);
            try
            {
                reference = await _secrets.StoreAsync(reference, bytes, cancellationToken);
                storedReferences.Add(reference);
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
            }
        }

        var endpoint = new DatabaseRemoteEndpoint(
            request.Host.Trim(),
            request.Port,
            request.Database.Trim(),
            request.Username.Trim(),
            string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            request.TlsMode,
            string.IsNullOrWhiteSpace(request.RootCertificatePath) ? null : Path.GetFullPath(request.RootCertificatePath),
            request.TrustServerCertificate,
            request.TimeoutSeconds);
        endpoint.Validate();
        return endpoint;
    }

    private DatabaseProfileStatus ProjectProfile(DatabaseTopologyProfile profile)
    {
        if (profile.Mode == DatabaseTopologyMode.Remote)
            return DatabaseTopologyStatusProjection.Project(profile);

        return new(
            DatabaseTopologyMode.LocalManaged,
            _resolver.DescribeLocalPrimary(),
            true,
            _resolver.DescribeLocalHistorian());
    }

    private static void EnsureDurable(DatabaseRuntimeConnectionSet set)
    {
        if (string.IsNullOrWhiteSpace(set.PrimaryConnectionString))
            throw new InvalidOperationException("Durable primary PostgreSQL connection is unavailable.");
    }
}
