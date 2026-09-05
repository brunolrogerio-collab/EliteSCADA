using Npgsql;
using Scada.Engineering.Persistence;

namespace Scada.Persistence.PostgreSql;

public sealed class PostgreSqlEngineeringProjectCatalog(string connectionString) : IEngineeringProjectCatalog, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource = string.IsNullOrWhiteSpace(connectionString)
        ? throw new ArgumentException("PostgreSQL connection string is required.", nameof(connectionString))
        : NpgsqlDataSource.Create(connectionString);

    public async Task<IReadOnlyCollection<EngineeringProjectCatalogEntry>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT ON (project_key)
                   project_key, project_name, revision, saved_at_utc
            FROM elitescada.engineering_revisions
            ORDER BY project_key, revision DESC;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var projects = new List<EngineeringProjectCatalogEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            projects.Add(new EngineeringProjectCatalogEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetFieldValue<DateTimeOffset>(3)));
        }

        return projects;
    }

    public async Task<bool> HasAnyAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM elitescada.engineering_revisions LIMIT 1);");
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
