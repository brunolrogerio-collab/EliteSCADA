using Npgsql;

namespace Scada.Historian.TimescaleDb;

/// <summary>
/// Deployment-level target preparation for the TimescaleDB historian.
/// It owns no credentials and only applies the same canonical historian infrastructure
/// used by the runtime.
/// </summary>
public static class TimescaleDbDeploymentPreparation
{
    public static async Task InitializeAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("TimescaleDB connection string is required.", nameof(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await TimescaleHistorianInfrastructure.EnsureAllAsync(dataSource, cancellationToken);
    }
}
