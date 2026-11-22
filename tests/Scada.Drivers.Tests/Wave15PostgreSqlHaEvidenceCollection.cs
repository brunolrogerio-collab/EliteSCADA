using Xunit;

namespace Scada.Drivers.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Wave15PostgreSqlHaEvidenceCollection
{
    public const string Name = "Wave15 PostgreSQL HA evidence";
}
