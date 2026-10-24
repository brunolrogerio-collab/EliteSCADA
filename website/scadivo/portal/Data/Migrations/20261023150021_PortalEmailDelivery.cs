using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadivo.Portal.Data.Migrations
{
    /// <inheritdoc />
    public partial class PortalEmailDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "EmailOutbox",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                table: "EmailOutbox",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAtUtc",
                table: "EmailOutbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SentAtUtc",
                table: "EmailOutbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_Status_NextAttemptAtUtc",
                table: "EmailOutbox",
                columns: new[] { "Status", "NextAttemptAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailOutbox_Status_NextAttemptAtUtc",
                table: "EmailOutbox");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "EmailOutbox");

            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                table: "EmailOutbox");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "EmailOutbox");

            migrationBuilder.DropColumn(
                name: "SentAtUtc",
                table: "EmailOutbox");
        }
    }
}
