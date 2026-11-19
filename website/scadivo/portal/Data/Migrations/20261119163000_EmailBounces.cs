using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Scadivo.Portal.Data.Migrations;

[DbContext(typeof(PortalDb))]
[Migration("20261119163000_EmailBounces")]
public partial class EmailBounces : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>("BouncedAtUtc", "EmailOutbox", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>("DeliveryRecipientFingerprint", "EmailOutbox", type: "character varying(64)", maxLength: 64, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("BouncedAtUtc", "EmailOutbox");
        migrationBuilder.DropColumn("DeliveryRecipientFingerprint", "EmailOutbox");
    }
}
