using Npgsql;
using Scada.Api.Persistence;

namespace Scada.Drivers.Tests;

public sealed class LocalManagedDatabasePortTests
{
    [Fact]
    public void LocalPortOverride_PreservesQuotedPasswordAndOtherConnectionSettings()
    {
        var original = new NpgsqlConnectionStringBuilder { Host = "localhost", Port = 5432, Database = "plant", Username = "owner", Password = "secret;with=delimiters", SslMode = SslMode.Require };
        Assert.Equal(original.ConnectionString, DatabaseConnectionResolver.ApplyLocalPort(original.ConnectionString, null));
        var changed = new NpgsqlConnectionStringBuilder(DatabaseConnectionResolver.ApplyLocalPort(original.ConnectionString, 25432));
        Assert.Equal(25432, changed.Port);
        Assert.Equal(original.Password, changed.Password);
        Assert.Equal(original.Database, changed.Database);
        Assert.Equal(original.Username, changed.Username);
        Assert.Equal(original.SslMode, changed.SslMode);
        original.Host = "remote.example";
        Assert.Throws<InvalidOperationException>(() => DatabaseConnectionResolver.ApplyLocalPort(original.ConnectionString, 25432));
        Assert.Throws<ArgumentOutOfRangeException>(() => DatabaseConnectionResolver.ApplyLocalPort(changed.ConnectionString, 70000));
    }
}
