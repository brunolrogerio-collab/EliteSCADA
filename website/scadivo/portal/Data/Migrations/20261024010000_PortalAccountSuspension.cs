using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadivo.Portal.Data.Migrations;

[DbContext(typeof(PortalDb))]
[Migration("20261024010000_PortalAccountSuspension")]
public partial class PortalAccountSuspension : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<bool>(name: "IsSuspended", table: "AspNetUsers", type: "boolean", nullable: false, defaultValue: false);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "IsSuspended", table: "AspNetUsers");
}
