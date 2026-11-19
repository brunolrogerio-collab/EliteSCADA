using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadivo.Portal.Data.Migrations
{
    /// <inheritdoc />
    public partial class PortalOfflineMachineLicenseRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfflineMachineLicenseRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ApplicationName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequestKind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ExistingLicenseName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MachineRequestCodeProtected = table.Column<string>(type: "text", nullable: false),
                    RequestNote = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflineMachineLicenseRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfflineMachineLicenseRequests_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfflineMachineLicenseRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfflineMachineLicenseRequests_ReviewedByUserId",
                table: "OfflineMachineLicenseRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflineMachineLicenseRequests_Status_SubmittedAtUtc",
                table: "OfflineMachineLicenseRequests",
                columns: new[] { "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OfflineMachineLicenseRequests_UserId_SubmittedAtUtc",
                table: "OfflineMachineLicenseRequests",
                columns: new[] { "UserId", "SubmittedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfflineMachineLicenseRequests");
        }
    }
}
