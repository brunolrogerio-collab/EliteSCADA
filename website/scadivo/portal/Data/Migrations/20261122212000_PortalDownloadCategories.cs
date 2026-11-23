using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Scadivo.Portal.Data.Migrations;

[DbContext(typeof(PortalDb))]
[Migration("20261122212000_PortalDownloadCategories")]
public partial class PortalDownloadCategories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Category",
            table: "Releases",
            type: "character varying(24)",
            maxLength: 24,
            nullable: false,
            defaultValue: "software");

        migrationBuilder.AddColumn<string>(
            name: "TitleEn",
            table: "Releases",
            type: "character varying(160)",
            maxLength: 160,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "TitleEs",
            table: "Releases",
            type: "character varying(160)",
            maxLength: 160,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "TitlePt",
            table: "Releases",
            type: "character varying(160)",
            maxLength: 160,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql("UPDATE \"Releases\" SET \"TitlePt\" = \"Version\", \"TitleEn\" = \"Version\", \"TitleEs\" = \"Version\"");

        migrationBuilder.DropIndex(
            name: "IX_Releases_Version_Platform",
            table: "Releases");

        migrationBuilder.CreateIndex(
            name: "IX_Releases_Version_Platform_Category",
            table: "Releases",
            columns: new[] { "Version", "Platform", "Category" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Releases_Version_Platform_Category",
            table: "Releases");

        migrationBuilder.CreateIndex(
            name: "IX_Releases_Version_Platform",
            table: "Releases",
            columns: new[] { "Version", "Platform" },
            unique: true);

        migrationBuilder.DropColumn(name: "Category", table: "Releases");
        migrationBuilder.DropColumn(name: "TitleEn", table: "Releases");
        migrationBuilder.DropColumn(name: "TitleEs", table: "Releases");
        migrationBuilder.DropColumn(name: "TitlePt", table: "Releases");
    }
}
