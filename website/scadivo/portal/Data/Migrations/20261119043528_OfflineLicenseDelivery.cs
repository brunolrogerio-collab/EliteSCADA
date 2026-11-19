using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadivo.Portal.Data.Migrations
{
    /// <inheritdoc />
    public partial class OfflineLicenseDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvailableAtUtc",
                table: "OfflineMachineLicenseRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OnlineLicenseId",
                table: "OfflineMachineLicenseRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedLicenseId",
                table: "OfflineMachineLicenseRequests",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedLicenseProtected",
                table: "OfflineMachineLicenseRequests",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfflineMachineLicenseRequests_OnlineLicenseId",
                table: "OfflineMachineLicenseRequests",
                column: "OnlineLicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflineMachineLicenseRequests_SignedLicenseId",
                table: "OfflineMachineLicenseRequests",
                column: "SignedLicenseId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OfflineMachineLicenseRequests_ProductLicenses_OnlineLicense~",
                table: "OfflineMachineLicenseRequests",
                column: "OnlineLicenseId",
                principalTable: "ProductLicenses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OfflineMachineLicenseRequests_ProductLicenses_OnlineLicense~",
                table: "OfflineMachineLicenseRequests");

            migrationBuilder.DropIndex(
                name: "IX_OfflineMachineLicenseRequests_OnlineLicenseId",
                table: "OfflineMachineLicenseRequests");

            migrationBuilder.DropIndex(
                name: "IX_OfflineMachineLicenseRequests_SignedLicenseId",
                table: "OfflineMachineLicenseRequests");

            migrationBuilder.DropColumn(
                name: "AvailableAtUtc",
                table: "OfflineMachineLicenseRequests");

            migrationBuilder.DropColumn(
                name: "OnlineLicenseId",
                table: "OfflineMachineLicenseRequests");

            migrationBuilder.DropColumn(
                name: "SignedLicenseId",
                table: "OfflineMachineLicenseRequests");

            migrationBuilder.DropColumn(
                name: "SignedLicenseProtected",
                table: "OfflineMachineLicenseRequests");
        }
    }
}
