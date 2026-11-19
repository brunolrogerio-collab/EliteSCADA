using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadivo.Portal.Data.Migrations
{
    /// <inheritdoc />
    public partial class PortalDailyViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortalDailyViews",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Surface = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalDailyViews", x => new { x.Day, x.Surface });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortalDailyViews");
        }
    }
}
