using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Scadivo.Portal.Data;

public sealed class PortalDbFactory : IDesignTimeDbContextFactory<PortalDb>
{
    public PortalDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PortalDb>()
        .UseNpgsql("Host=localhost;Database=scadivo_portal_design;Username=portal_design")
        .Options);
}
