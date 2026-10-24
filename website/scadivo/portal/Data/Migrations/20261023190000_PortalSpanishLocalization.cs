using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadivo.Portal.Data.Migrations;

[DbContext(typeof(PortalDb))]
[Migration("20261023190000_PortalSpanishLocalization")]
public partial class PortalSpanishLocalization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "NotesEs", table: "Releases", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "SubjectEs", table: "EmailTemplates", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "BodyEs", table: "EmailTemplates", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "TitleEs", table: "FaqArticles", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "BodyEs", table: "FaqArticles", type: "text", nullable: false, defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "NotesEs", table: "Releases");
        migrationBuilder.DropColumn(name: "SubjectEs", table: "EmailTemplates");
        migrationBuilder.DropColumn(name: "BodyEs", table: "EmailTemplates");
        migrationBuilder.DropColumn(name: "TitleEs", table: "FaqArticles");
        migrationBuilder.DropColumn(name: "BodyEs", table: "FaqArticles");
    }
}
