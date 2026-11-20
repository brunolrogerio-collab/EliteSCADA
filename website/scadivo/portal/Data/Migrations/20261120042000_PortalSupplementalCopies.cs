using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Scadivo.Portal.Data.Migrations;

[DbContext(typeof(PortalDb))]
[Migration("20261120042000_PortalSupplementalCopies")]
public partial class PortalSupplementalCopies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SupplementalCopies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Reference = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                Evidence = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ChallengeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                BrowserHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                DownloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ProtectedArchive = table.Column<byte[]>(type: "bytea", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_SupplementalCopies", copy => copy.Id));

        migrationBuilder.CreateIndex("IX_SupplementalCopies_ExpiresAtUtc", "SupplementalCopies", "ExpiresAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("SupplementalCopies");
}
