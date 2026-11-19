using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadivo.Portal.Data.Migrations
{
    /// <inheritdoc />
    public partial class PortalAccountClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "AspNetUsers");
        }
    }
}
